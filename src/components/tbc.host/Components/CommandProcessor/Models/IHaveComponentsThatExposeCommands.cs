using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tbc.Protocol;

namespace Tbc.Host.Components.CommandProcessor.Models
{
    public interface IHaveComponentsThatExposeCommands
    {
        IEnumerable<IExposeCommands> Components { get; }
    }

    public interface IWantToRequestCommands
    {
        Func<ExecuteCommandRequest, Task> RequestCommand { get; set; }
    }
}