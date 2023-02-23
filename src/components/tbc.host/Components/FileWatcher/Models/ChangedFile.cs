using System;

namespace Tbc.Host.Components.FileWatcher.Models
{
    public record ChangedFile
    {
        public required string Path { get; init; }
        public required string Contents { get; init; }
        public DateTimeOffset ChangedAt { get; init; }
            = DateTimeOffset.Now;

        public bool Silent { get; set; }

        public override string ToString()
        {
            return $"{Path} ({ChangedAt})";
        }
    }
}
