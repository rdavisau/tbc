using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tbc.Core.Models;

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
