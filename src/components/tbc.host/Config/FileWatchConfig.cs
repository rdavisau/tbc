using System.Collections.Generic;

namespace Tbc.Host.Config
{
    public class FileWatchConfig
    {
        public required string RootPath { get; init; }
        public List<string> Ignore { get; set; } = new List<string>();
        public string FileMask { get; set; } = "*.cs";
    }
}
