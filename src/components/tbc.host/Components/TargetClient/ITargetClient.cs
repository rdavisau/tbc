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

    Task<IAsyncEnumerable<AssemblyReference>> AssemblyReferences();
    Task<IAsyncEnumerable<ExecuteCommandRequest>> CommandRequests();
    Task<Outcome> ExecAsync(ExecuteCommandRequest req);
    Task<Outcome> LoadAssemblyAsync(LoadDynamicAssemblyRequest req);
}
