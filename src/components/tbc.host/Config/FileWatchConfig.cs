using System.Collections.Generic;

namespace Tbc.Host.Config
{
    public class FileWatchConfig
    {
        public string RootPath { get; set; }
        public List<string> Ignore { get; set; } = new List<string>();
        public string FileMask { get; set; }
    }
}