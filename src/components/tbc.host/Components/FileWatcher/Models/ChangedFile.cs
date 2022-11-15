using System;

namespace Tbc.Host.Components.FileWatcher.Models
{
    public class ChangedFile
    {
        public required string Path { get; init; }
        public required string Contents { get; init; }
        public DateTimeOffset ChangedAt { get; init; }
            = DateTimeOffset.Now;

        public override string ToString()
        {
            return $"{Path} ({ChangedAt})";
        }
    }
}
