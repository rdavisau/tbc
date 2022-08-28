using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Tbc.Core.Models;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.CommandProcessor;
using Tbc.Host.Components.CommandProcessor.Models;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Host.Components.FileWatcher;
using Tbc.Host.Components.FileWatcher.Models;
using Tbc.Host.Components.IncrementalCompiler;
using Tbc.Host.Components.IncrementalCompiler.Models;
using Tbc.Host.Components.TargetClient;
using Tbc.Host.Extensions;

namespace Tbc.Host.Components.FileEnvironment
{
    public partial class FileEnvironment : TransientComponentBase<FileEnvironment>, IFileEnvironment, IExposeCommands, IHaveComponentsThatExposeCommands
    {
        private bool _running;
        
        public ITargetClient Client { get; }
        public ICommandProcessor CommandProcessor { get; }
        public IFileWatcher FileWatcher { get; }
        public IIncrementalCompiler IncrementalCompiler { get; }
        public IFileSystem FileSystem { get; set; }

        public FileEnvironment(IRemoteClientDefinition client,
            IFileWatcher fileWatcher, ICommandProcessor commandProcessor,
            Func<IRemoteClientDefinition, ITargetClient> targetClientFactory,
            Func<string, IIncrementalCompiler> incrementalCompilerFactory,
            ILogger<FileEnvironment> logger, IFileSystem fileSystem) : base(logger)
        {
            Client = targetClientFactory(client);
            CommandProcessor = commandProcessor;
            FileSystem = fileSystem;
            FileWatcher = fileWatcher;
            IncrementalCompiler = incrementalCompilerFactory($"{client.Address}:{client.Port}");
            IncrementalCompiler.RootPath = FileWatcher.WatchPath;
        }

        private string _primaryTypeHint;
        
        public async Task Run()
        {
            if (_running)
                throw new InvalidOperationException("Already running");
            
            _running = true;

            Logger.LogInformation("Waiting for channel to be established with target client {Client}", Client);
            
            await Client.WaitForConnection();

            await TryLoadLoadContext();
            
            Task.Factory.StartNew(SetupReferenceTracking, TaskCreationOptions.LongRunning);

            FileWatcher
                .Changes
                .Where(_ => !Terminated)
                .Select(IncrementalCompiler.StageFile)
                .Where(x => x != null)
                .SelectMany(SendAssemblyForReload)
                .Subscribe(x => Logger.Log(x.Success ? LogLevel.Information : LogLevel.Error, "Send incremental assembly outcome: {@Outcome}", x));
            
            Logger.LogInformation("FileEnvironment for client {@Client} initialised", Client);
            
            Task.Factory.StartNew(SetupCommandListening, TaskCreationOptions.LongRunning);
            
            await Client.WaitForTerminalState();

            Terminated = true;

            Logger.LogWarning("FileEnvironment for client {@Client} terminating", Client);
        }

        private async Task TryLoadLoadContext()
        {
            if (!String.IsNullOrWhiteSpace(_loadContext))
            {
                var ctx = JsonConvert.DeserializeObject<PersistedContext>(
                    await File.ReadAllTextAsync($"{_loadContext}.json"));

                foreach (var file in ctx.WatchedFiles.Select(x => new ChangedFile { Path = x, Contents = File.ReadAllText(x) }))
                    IncrementalCompiler.StageFile(file, silent: true);

                await SetPrimaryTypeHint(ctx.PrimaryTypeHint);
            }
        }

        public async Task Reset()
        {
            Logger.LogInformation("Resetting incremental compiler state");
            
            IncrementalCompiler.ClearTrees();
            IncrementalCompiler.ClearReferences();

            await TryLoadLoadContext();

            Task.Factory.StartNew(SetupReferenceTracking, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(SetupCommandListening, TaskCreationOptions.LongRunning);
        }

        public async Task SetupReferenceTracking()
        {
            Logger.LogInformation("Setting up reference tracking for client {Client}", Client);
            IncrementalCompiler.ClearReferences();

            var seen = new HashSet<string>();

            var tempFilePath = FileSystem.Path.GetTempFileName();
            var targetHello = await Client.Hello(new HostHello { SharedHostFilePath = tempFilePath });

            var sharedFilesystem = targetHello.CanAccessSharedHostFile;
            var existingReferences = new List<AssemblyReference>();
            if (sharedFilesystem && targetHello.UseSharedFilesystemDependencyResolution)
            {
                await AddSharedFilesystemReference(targetHello, existingReferences);

                foreach (var refSeen in existingReferences.Select(x => x.AssemblyLocation))
                    seen.Add(refSeen);

                // allow a little time for references to come back <:-)
                // should have client tell us when all current dependencies have been sent
                Task.Delay(TimeSpan.FromSeconds(.33)).ContinueWith(_ => IncrementalCompiler.DoWarmup());
            }

            if (targetHello.UseDependencyCache && targetHello.ApplicationIdentifier is { } appIdentifier)
                ProcessDependencyCache(appIdentifier);

            var iterator = await Client.AssemblyReferences(existingReferences);
            try
            {
                await iterator.ForEachAsync(async asm =>
                {
                    if (Terminated)
                        return;

                    if (seen.Contains(asm.AssemblyName) ||
                        (sharedFilesystem && !String.IsNullOrWhiteSpace(asm.AssemblyLocation) && seen.Contains(asm.AssemblyLocation)))
                    {
                        Logger.LogInformation("Not adding reference at {Path} because it has already been seen", asm.AssemblyLocation);

                        return;
                    }
                    else
                        seen.Add(asm.AssemblyName);

                    Logger.LogInformation(
                        "Adding reference to {AssemblyName} from {AssemblyLocation} for client {Client}",
                        asm.AssemblyName, asm.AssemblyLocation, Client.ClientDefinition);

                    if (sharedFilesystem && targetHello.UseSharedFilesystemDependencyResolution && asm.PeBytes is null)
                        asm = asm with { PeBytes = await FileSystem.File.ReadAllBytesAsync(asm.AssemblyLocation) };

                    IncrementalCompiler.AddMetadataReference(asm);
                });
            }
            catch (Exception ex)
            {
                if (!Terminated)
                    Logger.LogError(ex, nameof(SetupReferenceTracking));
            }
        }

        public async Task SetupCommandListening()
        {
            Logger.LogInformation("Setting up command listening for client {Client}", Client);
            
            var iterator = await Client.CommandRequests();
            try
            {
                var enumerator = iterator.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync() && !Terminated)
                {
                    var request = enumerator.Current;

                    Logger.LogInformation(
                        "Received command request {@Request} from target {Client}",
                        request, Client.ClientDefinition);

                    // doh
                    await CommandProcessor.HandleCommand($"{request.Command} {String.Join(" ", request.Args)}");
                }
            }
            catch (Exception ex)
            {
                if (!Terminated)
                    Logger.LogError(ex, nameof(SetupCommandListening));
            }
        }
        
        public bool Terminated { get; set; }

        private EmittedAssembly _lastEmittedAssembly;
        private string _loadContext;

        public async Task<Outcome> SendAssemblyForReload(EmittedAssembly asm)
        {
            _lastEmittedAssembly = asm;
            var req = new LoadDynamicAssemblyRequest
            {
                AssemblyName = asm.AssemblyName,
                PeBytes = asm.Pe,
                PdbBytes = asm.Pd,
                PrimaryTypeName = 
                    String.IsNullOrWhiteSpace(_primaryTypeHint) 
                    ? "" 
                    : TryResolvePrimaryType(_primaryTypeHint)
            };
            
            return await Client.RequestClientLoadAssemblyAsync(req);
        }

        public string TryResolvePrimaryType(string typeHint) 
            => IncrementalCompiler.TryResolvePrimaryType(typeHint);

        public Task PrintTrees(bool withDetail = false)
        {
            IncrementalCompiler.PrintTrees(withDetail);
            
            return Task.CompletedTask;
        }

        public async Task SetPrimaryTypeHint(string typeHint)
        {
            _primaryTypeHint = typeHint;

            if (_lastEmittedAssembly != null)
                await SendAssemblyForReload(_lastEmittedAssembly);
        }

        string IExposeCommands.Identifier 
            => $"env-{Client.ClientDefinition.Address}-{Client.ClientDefinition.Port}";

        IEnumerable<TbcCommand> IExposeCommands.Commands => new List<TbcCommand>
        {
            new TbcCommand
            {
                Command = "reset",
                Execute = delegate { return Reset(); }
            },
            new TbcCommand
            {
                Command = "primary",
                Execute = async (cmd, args) => { await SetPrimaryTypeHint(args[0]); }
            },
            
            new TbcCommand
            {
                Command = "run-on-target",
                Execute = async (cmd, args) =>
                {
                    var req = new ExecuteCommandRequest
                    {
                        Command = cmd
                    };
                    
                    foreach (var arg in args)
                        req.Args.Add(arg);

                    var outcome = await Client.RequestClientExecAsync(req);
                    
                    Logger.LogInformation("Command {Command} success: {@Outcome}", cmd, outcome.Success);
                    foreach (var message in outcome.Messages)
                        Logger.LogInformation("{Message}", message.Message);
                }
            },
            
            new TbcCommand
            {
                Command = "context",
                Execute = (_, args) =>
                {
                    if (!args.Any())
                        Logger.LogWarning("Need subcommand for context operation");

                    var sub = args[0];

                    switch (sub)
                    {
                        case "load":
                            var toLoad = args[1];
                            SetLoadContext(toLoad);
                            break;
                        
                        case "save":
                            var toSave = args[1];
                            SaveContext(toSave);
                            break;
                        
                        default:
                            Logger.LogWarning("Don't know how to handle subcommand '{SubCommand}' of context");
                            break;                            
                    }
                                        
                    return Task.CompletedTask;
                }
            }
        };
        
        public void SaveContext(string saveIdentifier)
        {
            var ctx = new PersistedContext
            {
                PrimaryTypeHint = _primaryTypeHint,
                WatchedFiles = IncrementalCompiler.StagedFiles
            };
            
            File.WriteAllText($"{saveIdentifier}.json", ctx.ToJson());
        }

        public void SetLoadContext(string saveIdentifier)
        {
            _loadContext = saveIdentifier;
            
            Reset();
        }

        public IEnumerable<IExposeCommands> Components 
            => new object [] { IncrementalCompiler, FileWatcher }.OfType<IExposeCommands>();

        private static void ProcessDependencyCache(string appId)
        {
            // todo :)
        }

        private async Task AddSharedFilesystemReference(TargetHello targetHello, List<AssemblyReference> existingReferences)
        {
            Logger.LogInformation("Target and host share filesystem, attempting to preload dependencies");

            var files = FileSystem.Directory.GetFiles(targetHello.RootAssemblyPath, "*.dll");
            foreach (var f in files)
            {
                var reference = new AssemblyReference
                {
                    AssemblyLocation = f,
                    ModificationTime = FileSystem.FileInfo.FromFileName(f).LastWriteTime,
                    PeBytes = await FileSystem.File.ReadAllBytesAsync(f)
                };

                IncrementalCompiler.AddMetadataReference(reference);
                existingReferences.Add(reference);
            }
        }
    }

    public class PersistedContext
    {
        public string? PrimaryTypeHint { get; set; }
        public List<string> WatchedFiles { get; set; }
    }
}
