using System.Threading.Tasks;

namespace Tbc.Host.Components.FileEnvironment
{
    public interface IFileEnvironment
    {
        Task Run();
        Task Reset();
        Task PrintTrees(bool withDetail = false);
        Task SetPrimaryTypeHint(string typeHint);
    }
}