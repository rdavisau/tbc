using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.CommandProcessor;
using Tbc.Host.Components.CommandProcessor.Models;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Host.Components.FileWatcher;
using Tbc.Host.Components.IncrementalCompiler;
using Tbc.Host.Components.IncrementalCompiler.Models;
using Tbc.Protocol;

namespace Tbc.Host.Components.FileEnvironment
{
    public partial class FileEnvironment : TransientComponentBase<FileEnvironment>, IFileEnvironment, IExposeCommands, IHaveComponentsThatExposeCommands
    {
        private bool _running;
        
        public TargetClient.TargetClient Client { get; }
        public ICommandProcessor CommandProcessor { get; }
        public IFileWatcher FileWatcher { get; }
        public IIncrementalCompiler IncrementalCompiler { get; }
        
        public FileEnvironment(IClient client,
            IFileWatcher fileWatcher, ICommandProcessor commandProcessor,
            Func<IClient, TargetClient.TargetClient> targetClientFactory, 
            Func<IClient, IIncrementalCompiler> incrementalCompilerFactory,
            ILogger<FileEnvironment> logger) : base(logger)
        {
            Client = targetClientFactory(client);
            CommandProcessor = commandProcessor;
            FileWatcher = fileWatcher;
            IncrementalCompiler = incrementalCompilerFactory(client);
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

            Task.Factory.StartNew(SetupReferenceTracking, TaskCreationOptions.LongRunning);
            
            FileWatcher
                .Changes
                .Select(IncrementalCompiler.StageFile)
                .Where(x => x != null)
                .SelectMany(SendAssemblyForReload)
                .Subscribe(x => Logger.LogInformation("Send incremental assembly outcome: {@Outcome}", x));
            
            Logger.LogInformation("FileEnvironment for client {@Client} initialised", Client);
            
            Task.Factory.StartNew(SetupCommandListening, TaskCreationOptions.LongRunning);
            
            await Client.WaitForTerminalState();

            Terminated = true;
            
            Logger.LogWarning("FileEnvironment for client {@Client} terminating", Client);
        }

        public Task Reset()
        {
            Logger.LogInformation("Resetting incremental compiler state");
            
            IncrementalCompiler.ClearTrees();
            IncrementalCompiler.ClearReferences();

            Task.Factory.StartNew(SetupReferenceTracking, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(SetupCommandListening, TaskCreationOptions.LongRunning);
            
            return Task.CompletedTask;
        }

        public async Task SetupReferenceTracking()
        {
            Logger.LogInformation("Setting up reference tracking for client {Client}", Client);
            IncrementalCompiler.ClearReferences();
            var seen = new HashSet<string>();
            
            var iterator = await Client.AssemblyReferences();
            try
            {
                while (await iterator.MoveNext(default) && !Terminated)
                {
                    var asm = iterator.Current;

                    if (seen.Contains(asm.AssemblyName))
                        continue;
                    else
                        seen.Add(asm.AssemblyName);

                    Logger.LogInformation(
                        "Adding reference to {AssemblyName} from {AssemblyLocation} for client {Client}",
                        asm.AssemblyName, asm.AssemblyLocation, Client.Client);

                    IncrementalCompiler.AddMetadataReference(asm);
                }
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
                while (await iterator.MoveNext(default) && !Terminated)
                {
                    var request = iterator.Current;

                    Logger.LogInformation(
                        "Received command request {@Request} from target {Client}",
                        request, Client.Client);

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

        public async Task<Outcome> SendAssemblyForReload(EmittedAssembly asm)
        {
            _lastEmittedAssembly = asm;
            var req = new LoadDynamicAssemblyRequest
            {
                AssemblyName = asm.AssemblyName,
                PeBytes = ByteString.CopyFrom(asm.Pe),
                PdbBytes = asm.Pd == null ? ByteString.Empty : ByteString.CopyFrom(asm.Pd),
                PrimaryTypeName = 
                    String.IsNullOrWhiteSpace(_primaryTypeHint) 
                    ? "" 
                    : TryResolvePrimaryType(_primaryTypeHint)
            };
            
            return await Client.Loader.LoadAssemblyAsync(req);
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
            => $"env-{Client.Client.Address}-{Client.Client.Port}";

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

                    var outcome = await Client.Loader.ExecAsync(req);
                    
                    Logger.LogInformation("{@Outcome}", outcome);
                }
            }
        };

        public IEnumerable<IExposeCommands> Components 
            => new object [] { IncrementalCompiler, FileWatcher }.OfType<IExposeCommands>();
    }
}