using System.Collections.Generic;
using System.Threading.Tasks;
using Inject.Protocol;

namespace Tbc.Host.Services.Patch
{
    public interface ICompilationDependencyCache
    {
        Task ClearCache();
        Task<List<CachedAssemblyReference>> GetCachedAssemblies();
        Task CacheAssembly(AssemblyReference @ref);
    }
}