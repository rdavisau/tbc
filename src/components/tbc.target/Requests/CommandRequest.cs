using System.Collections.Generic;

namespace Tbc.Target.Requests
{
    public class CommandRequest
    {
        public CommandRequest(string command, List<string> args)
        {
            Command = command;
            Args = args;
        }
        
        public string Command { get; set; }
        public List<string> Args { get; set; }
    }
}
