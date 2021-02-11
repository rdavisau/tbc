using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Host.Config;
using tbc.host.console.ConsoleHost;
using Tbc.Host.Extensions;

namespace tbc.host.console
{
    public static class ConsoleConfig
    {
        public static Dictionary<string, JObject> Default()
        {
            Console.WriteLine("No valid configuration path provided, using defaults.");

            return new Dictionary<string, JObject>
            {
                [ExpectedClientsConfig.ConfigKey] 
                    = new ExpectedClientsConfig
                        {
                            ExpectedClients =
                            {
                                new RemoteClient
                                {
                                    Address = "localhost",
                                    Port = 50123
                                }
                            }
                        }
                        .ToJObject(),
                
                [KnownConfigurationKeys.AssemblyCompilation] 
                    = new AssemblyCompilationOptions
                        {
                            Debug = true,
                        }
                        .ToJObject(),
                
                [KnownConfigurationKeys.FileWatch] 
                    = new FileWatchConfig
                        {
                            RootPath = Environment.CurrentDirectory,
                            Ignore = { "/obj/", "/bin/" }   
                        }
                        .ToJObject()
            };
        }
    }
}