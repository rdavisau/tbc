using System.Collections.Generic;
using Tbc.Host.Components.FileEnvironment.Models;

namespace tbc.host.console.ConsoleHost
{
    public class ExpectedClientsConfig
    {
        public const string ConfigKey = "ConsoleHost";
        
        public List<RemoteClient> ExpectedClients { get; set; }
            = new List<RemoteClient>();
    }
}