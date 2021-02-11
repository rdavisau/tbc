using System;

namespace Tbc.Target.Requests
{
    public class HostCommandRequestedEventArgs : EventArgs
    {
        public CommandRequest Command { get; set; }
    }
}