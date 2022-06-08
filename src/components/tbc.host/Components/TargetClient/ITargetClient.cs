using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tbc.Core.Models;
using Tbc.Host.Components.FileEnvironment.Models;

namespace Tbc.Host.Components.TargetClient;

public interface ITargetClient : IDisposable
{
    IRemoteClientDefinition ClientDefinition { get; }

    Task WaitForConnection();
    Task WaitForTerminalState();
    IObservable<CanonicalChannelState> ClientChannelState { get; }

    Task<TargetHello> Hello(HostHello hello);
    Task<IAsyncEnumerable<AssemblyReference>> AssemblyReferences(List<AssemblyReference> existing);
    Task<IAsyncEnumerable<ExecuteCommandRequest>> CommandRequests();
    Task<Outcome> RequestClientExecAsync(ExecuteCommandRequest req);
    Task<Outcome> RequestClientLoadAssemblyAsync(LoadDynamicAssemblyRequest req);
}
