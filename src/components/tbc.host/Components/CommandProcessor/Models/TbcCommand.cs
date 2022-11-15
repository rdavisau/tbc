using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tbc.Host.Components.CommandProcessor.Models
{
    public class TbcCommand
    {
        public required string Command { get; init; }
        
        [JsonIgnore]
        public required Func<string, string[], Task> Execute { get; init; }
    }
}
