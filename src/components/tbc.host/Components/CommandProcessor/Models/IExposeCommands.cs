using System.Collections.Generic;

namespace Tbc.Host.Components.CommandProcessor.Models
{
    public interface IExposeCommands
    {
        string Identifier { get; }
        IEnumerable<TbcCommand> Commands { get; }
    }
}