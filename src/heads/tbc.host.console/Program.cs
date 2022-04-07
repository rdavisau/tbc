using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tbc.Host;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Host.Components.TargetClient;
using Tbc.Host.Components.TargetClient.GrpcCore;
using tbc.host.console.ConsoleHost;

namespace tbc.host.console
{
    public class Program
    {
        static Program()
            => Configurator.KnownConfigMappings[ExpectedClientsConfig.ConfigKey] = typeof(ExpectedClientsConfig);
        
        static async Task Main(string[] args)
        {
            var configPath =  
                args?.Any() ?? false
                    ? args[0]
                    : "reload-config.json";

            var configuration =
                File.Exists(configPath)
                    ? JsonConvert.DeserializeObject<Dictionary<string, JObject>>
                        (await File.ReadAllTextAsync(configPath))
                    : ConsoleConfig.Default();

            await
                Configurator
                    .ConfigureServices(configuration, withAssemblies: typeof(Program).Assembly,
                        configure: c => c.RegisterDelegate<Func<IRemoteClientDefinition, ITargetClient>>(
                            sp => client
                               => sp.GetRequiredService<Func<IRemoteClientDefinition, GrpcCoreTargetClient>>()(client)))
                    .Resolve<IHost>()
                    .Run();
        }
    }
}
