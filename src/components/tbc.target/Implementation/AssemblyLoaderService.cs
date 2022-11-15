using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Tbc.Core.Apis;
using Tbc.Core.Models;
using Tbc.Core.Socket.Abstractions;
using Tbc.Target.Config;
using Tbc.Target.Interfaces;
using Tbc.Target.Requests;
using AssemblyReference = Tbc.Core.Models.AssemblyReference;
using CachedAssemblyState = Tbc.Core.Models.CachedAssemblyState;
using ExecuteCommandRequest = Tbc.Core.Models.ExecuteCommandRequest;
using LoadDynamicAssemblyRequest = Tbc.Core.Models.LoadDynamicAssemblyRequest;
using Outcome = Tbc.Core.Models.Outcome;

namespace Tbc.Target.Implementation;

public class AssemblyLoaderService : ITbcTarget, ISendToRemote
{
    private readonly TargetConfiguration _configuration;
    private IReloadManager _reloadManager;
    private readonly Action<string> _log;
    public IRemoteEndpoint? Remote { get; set; }

    private bool _useSharedFileSystemAssemblyReferences;

    public AssemblyLoaderService(TargetConfiguration targetConfiguration, IReloadManager reloadManager,
        Action<string> log)
    {
        _configuration = targetConfiguration;
        _reloadManager = reloadManager;
        _log = log;

        OnReloadManager(reloadManager);
    }

    public Task<TargetHello> Hello(HostHello hello)
    {
        var canAccessHostFilesystem = hello.SharedHostFilePath is { } tmpPath && File.Exists(tmpPath);

        _useSharedFileSystemAssemblyReferences = canAccessHostFilesystem && _configuration.UseDependencyCache;

        return Task.FromResult(new TargetHello
        {
            CanAccessSharedHostFile = canAccessHostFilesystem,
            RootAssemblyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly()?.Location)!,
            ApplicationIdentifier = _configuration.ApplicationIdentifier,
            UseDependencyCache = _configuration.UseDependencyCache,
            UseSharedFilesystemDependencyResolution = canAccessHostFilesystem && _configuration.UseSharedFilesystemDependencyResolutionIfPossible
        });
    }

    public async Task<Outcome> LoadAssembly(LoadDynamicAssemblyRequest request)
    {
        try
        {
            var peBytes = request.PeBytes;
            var pdbBytes = request.PdbBytes;

            var asm = Assembly.Load(peBytes, pdbBytes);
            var primaryType = !String.IsNullOrWhiteSpace(request.PrimaryTypeName)
                ? asm.GetTypes().FirstOrDefault(x => x.Name.EndsWith(request.PrimaryTypeName))
                : null;

            var req = new ProcessNewAssemblyRequest(asm, primaryType);

            return await _reloadManager.ProcessNewAssembly(req);
        }
        catch (Exception ex)
        {
            _log($"An error occurred when attempting to load assembly: {request.AssemblyName}");
            _log(ex.ToString());

            return new Outcome
            {
                Success = false,
                Messages = { new OutcomeMessage { Message = ex.ToString() } }
            };
        }
    }

    public async Task<Outcome> Exec(ExecuteCommandRequest request)
    {
        try
        {
            return await _reloadManager.ExecuteCommand(new CommandRequest(request.Command, request.Args.ToList()));
        }
        catch (Exception ex)
        {
            _log($"An error occurred when attempting to exec command: {request.Command} with args {request.Args}");

            return new Outcome
            {
                Success = false,
                Messages = { new OutcomeMessage { Message = ex.ToString() } }
            };
        }
    }

    public Task<Outcome> SynchronizeDependencies(CachedAssemblyState cachedAssemblyState)
    {
        _cachedAssemblyState = cachedAssemblyState.CachedAssemblies.GroupBy(x => x.AssemblyLocation)
           .ToDictionary(x => x.Key, x => x.ToList());

        Task.Delay(TimeSpan.FromSeconds(.5))
           .ContinueWith(async _ =>
            {
                var sw = Stopwatch.StartNew();
                var currentAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

                foreach (var asm in currentAssemblies)
                    await WriteIfNecessary(asm);

                _log($"Finished sending current assemblies in {sw.Elapsed}");

                AppDomain.CurrentDomain.AssemblyLoad += async (sender, args)
                    => await WriteIfNecessary(args.LoadedAssembly);
            });

        return Task.FromResult(new Outcome { Success = true });
    }

    public Task RequestCommand(CommandRequest req)
        => Remote is {} r ? r.SendRequest<ExecuteCommandRequest, Outcome>(new ExecuteCommandRequest
        {
            Command = req.Command,
            Args = req.Args.ToList()
        }) : Task.CompletedTask;

    private readonly AsyncLock _mutex = new();
    private Dictionary<string, List<CachedAssembly>> _cachedAssemblyState = new();
    private async Task WriteIfNecessary(Assembly asm)
    {
        using (await _mutex.LockAsync())
        {
            // It's safe to await while the lock is held
            _log($"Send {asm.FullName}?");
            var sw = Stopwatch.StartNew();

            if (asm.IsDynamic || String.IsNullOrWhiteSpace(asm.Location))
                return;

            if (asm.FullName.StartsWith("r2,"))
                return;

            if (_cachedAssemblyState.ContainsKey(asm.Location))
                return;

            try
            {
                var @ref = new AssemblyReference
                {
                    AssemblyName = asm.FullName,
                    AssemblyLocation = asm.Location,
                    ModificationTime = new DateTimeOffset(new FileInfo(asm.Location).LastWriteTimeUtc, TimeSpan.Zero),
                    PeBytes = _useSharedFileSystemAssemblyReferences ? null : File.ReadAllBytes(asm.Location)
                };

                _log($"Will send {asm.FullName} - {sw.Elapsed}");

                await Remote!.SendRequest<AssemblyReference, Outcome>(@ref);

                _log($"Sent {asm.FullName} - {sw.Elapsed}");
                _cachedAssemblyState[asm.Location] = new List<CachedAssembly> { new(@ref) };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                _log(ex.ToString());
            }
        }
    }

    private void OnReloadManager(IReloadManager reloadManager)
    {
        _reloadManager = reloadManager;

        if (reloadManager is INotifyReplacement inr)
            inr.NotifyReplacement = rm =>
            {
                inr.NotifyReplacement = null;

                _log($"Replacing reload manager: {rm}");

                OnReloadManager(rm);
            };

        if (reloadManager is IRequestHostCommand irehc)
            irehc.RequestHostCommand += RequestCommand;
    }
}
