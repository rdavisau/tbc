using System.Threading.Tasks;

namespace Tbc.Host.Components.Abstractions
{
    public interface IHost
    {
        Task Run();
    }
}