using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tbc.Core.Apis;
using Tbc.Core.Models;
using Tbc.Core.Socket;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.FileEnvironment.Models;

namespace Tbc.Host.Components.TargetClient;

public class SocketTargetClient : ComponentBase<SocketTargetClient>, ITargetClient, ITbcHost
{
    private readonly IFileSystem _fileSystem;

    protected TcpClient? TcpClient { get; private set; }
    protected SocketServer<ITbcProtocol>? Target { get; private set; }

    public SocketTargetClient(ILogger<SocketTargetClient> logger, IRemoteClientDefinition clientDefinition, IFileSystem fileSystem) : base(logger)
    {
        _fileSystem = fileSystem;

        ClientDefinition = clientDefinition;
    }

    public IRemoteClientDefinition ClientDefinition { get; }
    public IObservable<CanonicalChannelState> ClientChannelState
        => _clientChannelState.AsObservable();

    private readonly Subject<CanonicalChannelState> _clientChannelState = new();
    private readonly Subject<AssemblyReference> _incomingAssemblyReferences = new();
    private readonly Subject<ExecuteCommandRequest> _incomingCommandRequests = new();

    public async Task WaitForConnection()
    {
        _clientChannelState.OnNext(CanonicalChannelState.Connecting);

        var success = false;
        while (!success)
        {
            _clientChannelState.OnNext(CanonicalChannelState.Connecting);

            try
            {
                TcpClient = new TcpClient();
                await TcpClient.ConnectAsync(ClientDefinition.Address, ClientDefinition.Port);
                Target = new SocketServer<ITbcProtocol>(TcpClient, this, "host", x => Logger.LogInformation("{@Message}", x),
                    () =>
                    {
                        try { TcpClient.Dispose(); }
                        catch (Exception ex) { Logger.LogError(ex, "On tcpclient dispose"); }

                        _clientChannelState.OnNext(CanonicalChannelState.Shutdown);
                        return Task.CompletedTask;
                    });

                await Target.Run();

                Logger.LogInformation("TcpClient connected: {@State}", TcpClient.Connected);

                _clientChannelState.OnNext(CanonicalChannelState.Ready);

                success = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "TcpClient loop instance errored");
                _clientChannelState.OnNext(CanonicalChannelState.TransientFailure);

                success = false;
            }

            if (!success)
                await Task.Delay(TimeSpan.FromSeconds(.5));
        }
    }

    public Task<TargetHello> Hello(HostHello hello) =>
        Target!.SendRequest<HostHello, TargetHello>(hello);

    public Task<IAsyncEnumerable<AssemblyReference>> AssemblyReferences(List<AssemblyReference> assemblyReferences)
    {
        var cachedAssemblies = assemblyReferences.Select(x => new CachedAssembly(x)).ToList();

        Task.Delay(TimeSpan.FromSeconds(.1))
           .ContinueWith(_ => Target!.SendRequest<CachedAssemblyState, Outcome>(new CachedAssemblyState { CachedAssemblies = cachedAssemblies }));

        return Task.FromResult(_incomingAssemblyReferences.ToAsyncEnumerable());
    }

    public Task<IAsyncEnumerable<ExecuteCommandRequest>> CommandRequests()
        => Task.FromResult(_incomingCommandRequests.ToAsyncEnumerable());

    public Task<Outcome> RequestClientExecAsync(ExecuteCommandRequest req)
        => Target!.SendRequest<ExecuteCommandRequest, Outcome>(req);

    public async Task<Outcome> RequestClientLoadAssemblyAsync(LoadDynamicAssemblyRequest req)
    {
        var sw = Stopwatch.StartNew();
        var result = await Target!.SendRequest<LoadDynamicAssemblyRequest, Outcome>(req);
        Logger.LogInformation("Round trip for LoadAssembly with primary type {PrimaryTypeName}, {Duration}ms", req.PrimaryTypeName, sw.ElapsedMilliseconds);
        return result;
    }

    public Task WaitForTerminalState() =>
        ClientChannelState
           .TakeUntil(x =>
                x == CanonicalChannelState.Shutdown
             || x == CanonicalChannelState.TransientFailure
             || x == CanonicalChannelState.Idle)
           .ToTask();

    public Task<Outcome> AddAssemblyReference(AssemblyReference reference)
    {
        _incomingAssemblyReferences.OnNext(reference);

        return Task.FromResult(new Outcome { Success = true });
    }

    public Task<Outcome> AddManyAssemblyReferences(ManyAssemblyReferences references)
    {
        var sw = Stopwatch.StartNew();
        Logger.LogInformation("Begin loading {AssemblyCount} references", references.AssemblyReferences.Count);

        foreach (var asm in references.AssemblyReferences)
            _incomingAssemblyReferences.OnNext(asm);

        Logger.LogInformation("{Elapsed:N0}ms to load {AssemblyCount} references", sw.ElapsedMilliseconds, references.AssemblyReferences.Count);

        return Task.FromResult(new Outcome { Success = true });
    }

    public Task<Outcome> ExecuteCommand(ExecuteCommandRequest request)
    {
        _incomingCommandRequests.OnNext(request);

        return Task.FromResult(new Outcome { Success = true });
    }

    public Task<Outcome> Heartbeat(HeartbeatRequest request)
        => throw new NotImplementedException();

    public void Dispose()
    {

    }
}
