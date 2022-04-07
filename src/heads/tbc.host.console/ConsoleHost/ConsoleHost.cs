using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.CommandProcessor;
using Tbc.Host.Components.FileEnvironment;
using Tbc.Host.Components.FileEnvironment.Models;
using static Tbc.Host.Extensions.HelpfulExtensions;

namespace tbc.host.console.ConsoleHost
{
    public class ConsoleHost : ComponentBase<ConsoleHost>, IHost
    {
        private readonly List<RemoteClient> _expectedClients;
        private readonly Func<IRemoteClientDefinition, IFileEnvironment> _fileEnvironmentFactory;
        private readonly ICommandProcessor _commandProcessor;

        public ConsoleHost(
            Dictionary<string, JObject> rawConfig, ExpectedClientsConfig clientsConfig, 
            ICommandProcessor commandProcessor, Func<IRemoteClientDefinition, IFileEnvironment> fileEnvironmentFactory, 
            ILogger<ConsoleHost> logger) : base(logger)
        {
            _expectedClients = clientsConfig.ExpectedClients;
            _commandProcessor = commandProcessor;
            _fileEnvironmentFactory = fileEnvironmentFactory;
            
            Logger.LogInformation("{Component} init with config {@Config}", this, rawConfig);
        }

        public Task Run() 
            => Task.WhenAll(
                RunLoopForCommands(),
                RunLoopForExpectedClients());

        public Task RunLoopForCommands()
            => WhileTrue(
                () => _commandProcessor.HandleCommand(Console.ReadLine()));
        
        public Task RunLoopForExpectedClients()
            => Task.WhenAll(
                _expectedClients.Select(LoopForExpectedClient));

        private Task LoopForExpectedClient(RemoteClient c) 
            => WhileTrue(
                () => _fileEnvironmentFactory(c)
                    .Do(_commandProcessor.RegisterCommands)
                    .Run());
    }
}