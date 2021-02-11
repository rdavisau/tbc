using System;

namespace Tbc.Host.Components.FileWatcher.Models
{
    public class ChangedFile
    {
        public string Path { get; set; }
        internal string Contents { get; set; }
        public DateTimeOffset ChangedAt { get; set; } 
            = DateTimeOffset.Now;

        public override string ToString()
        {
            return $"{Path} ({ChangedAt})";
        }
    }
}