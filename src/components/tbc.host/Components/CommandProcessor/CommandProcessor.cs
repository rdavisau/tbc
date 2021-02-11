using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.CommandProcessor.Models;

namespace Tbc.Host.Components.CommandProcessor
{
    public class CommandProcessor : ComponentBase<CommandProcessor>, ICommandProcessor
    {
        public List<TbcComponentCommand> Commands { get; set; }

        public CommandProcessor(ILogger<CommandProcessor> logger) : base(logger)
        {
        }

        public void RegisterCommands(object context)
            => RegisterManyCommands(context);
        
        public void RegisterManyCommands(params object[] context)
        {
            var commands = new List<TbcComponentCommand>();
            
            foreach (var obj in context)
                commands.AddRange(Descend(obj));

            Commands = commands;
        }

        public void PrintCommands()
        {
            Logger.LogInformation(
                JsonConvert.SerializeObject(
                    Commands
                        .GroupBy(x => x.ComponentIdentifier, x => x.Command)
                        .Select(x => new { Component = x.Key, Commands = x.ToList() })
                    , Formatting.Indented));
        }

        public async Task<object> HandleCommand(string command)
        {
            if (String.IsNullOrWhiteSpace(command))
                return null;
            
            Logger.LogInformation("Received command: {Command}", command);

            var isCommandForTarget = command.StartsWith("!");
            if (isCommandForTarget)
                command = command[1..];

            var parts = command.Split(' ');
            var cmd = parts[0];
            var args = parts[1..];

            var handlers =
                
                isCommandForTarget 
                    ? Commands.Where(x => x.Command.Command == "run-on-target").ToList()
                    : Commands
                        .Where(x => String.Equals(x.Command.Command, cmd, StringComparison.InvariantCultureIgnoreCase))
                        .ToList();

            if (!handlers.Any())
            {
                Logger.LogWarning("No handler exists for command '{Command}' with arguments '{Arguments}'", cmd, args);
                return null;
            }

            foreach (var handler in handlers) 
                await handler.Command.Execute(cmd, args);

            return "cool";
        }

        private IEnumerable<TbcComponentCommand> Descend(object o)
        {
            if (o is IExposeCommands iec)
                foreach (var cmd in iec.Commands)
                    yield return new TbcComponentCommand {ComponentIdentifier = iec.Identifier, Command = cmd};
            
            if (o is IHaveComponentsThatExposeCommands ihctec)
                foreach (var component in ihctec.Components)
                foreach (var cmd in Descend(component))
                    yield return cmd;
        }
    }
}