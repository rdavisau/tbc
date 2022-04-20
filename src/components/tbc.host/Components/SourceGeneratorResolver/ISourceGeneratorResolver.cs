using System.Threading;
using System.Threading.Tasks;
using Tbc.Host.Components.SourceGeneratorResolver.Models;

namespace Tbc.Host.Components.SourceGeneratorResolver;

public interface ISourceGeneratorResolver
{
    Task<ResolveSourceGeneratorsResponse> ResolveSourceGenerators(ResolveSourceGeneratorsRequest request, 
        CancellationToken canceller = default);
}
