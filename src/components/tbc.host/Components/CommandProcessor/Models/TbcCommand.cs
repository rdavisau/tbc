using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tbc.Host.Components.CommandProcessor.Models
{
    public class TbcCommand
    {
        public string Command { get; set; }
        
        [JsonIgnore]
        public Func<string, string[], Task> Execute { get; set; }
    }
}