using System.Threading;
using System.Threading.Tasks;

namespace Tbc.Core.Socket.Abstractions;

public interface IRemoteEndpoint
{
    public Task<TResponse?> SendRequest<TRequest, TResponse>(TRequest request, CancellationToken canceller = default);
}