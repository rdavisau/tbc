using System.Threading.Tasks;
using Tbc.Core.Models;
using Tbc.Target.Requests;

namespace Tbc.Target.Interfaces
{
    public interface IReloadManager
    {
        Task<Outcome> ProcessNewAssembly(ProcessNewAssemblyRequest req);
        Task<Outcome> ExecuteCommand(CommandRequest req);
    }
}
