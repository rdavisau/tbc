using System.Threading.Tasks;

namespace Tbc.Host.Components.CommandProcessor
{
    public interface ICommandProcessor
    {
        void RegisterCommands(object context);
        void RegisterManyCommands(params object[] context);
        Task<object?> HandleCommand(string command);
        void PrintCommands();
    }
}
