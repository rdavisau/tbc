using System;
using System.Threading.Tasks;
using Tbc.Target.Requests;

namespace Tbc.Target.Interfaces
{
    public interface IRequestHostCommand
    {
        Func<CommandRequest, Task> RequestHostCommand { get; set; }
    }
}