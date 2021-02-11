using System.Threading.Tasks;
using Tbc.Protocol;
using Tbc.Target.Requests;

namespace Tbc.Target.Interfaces
{
    public interface IReloadManager
    {
        Task<Outcome> ProcessNewAssembly(ProcessNewAssemblyRequest req);
        Task<Outcome> ExecuteCommand(CommandRequest req);
    }
}