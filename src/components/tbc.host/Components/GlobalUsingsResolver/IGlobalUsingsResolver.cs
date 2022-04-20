using System.Threading;
using System.Threading.Tasks;
using Tbc.Host.Components.GlobalUsingsResolver.Models;

namespace Tbc.Host.Components.GlobalUsingsResolver;

public interface IGlobalUsingsResolver
{
    Task<ResolveGlobalUsingsResponse> ResolveGlobalUsings(ResolveGlobalUsingsRequest request, CancellationToken canceller = default);
}
