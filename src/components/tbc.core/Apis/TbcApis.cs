using System.Threading.Tasks;
using Refit;
using Tbc.Core.Models;

namespace Tbc.Core.Apis;

public interface ITbcTarget
{
    [Post("/hello")]
    Task<TargetHello> Hello(HostHello hello);

    [Post("/load-assembly")]
    Task<Outcome> LoadAssembly(LoadDynamicAssemblyRequest request);

    [Post("/eval")]
    Task<Outcome> Exec(ExecuteCommandRequest request);

    [Post("/synchronize-dependencies")]
    Task<Outcome> SynchronizeDependencies(CachedAssemblyState cachedAssemblyState);
}

public interface ITbcHost
{
    [Post("/add-assembly-reference")]
    Task<Outcome> AddAssemblyReference(AssemblyReference reference);

    [Post("/add-assembly-reference")]
    Task<Outcome> AddManyAssemblyReferences(ManyAssemblyReferences references);

    [Post("/execute-command")]
    Task<Outcome> ExecuteCommand(ExecuteCommandRequest request);

    [Post("/heartbeat")]
    Task<Outcome> Heartbeat(HeartbeatRequest request);
}

public interface ITbcConnectable
{
    [Post("/connect")]
    Task<ConnectResponse> Connect(ConnectRequest req);
}

public interface ITbcConnectableTarget : ITbcTarget, ITbcConnectable { }

public interface ITbcProtocol : ITbcHost, ITbcTarget {}
