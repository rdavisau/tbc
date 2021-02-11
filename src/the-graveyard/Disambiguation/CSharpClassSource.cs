using System;

namespace Tbc.Host.Services.Patch.Models
{
    public class CSharpClassSource
    {
        public string Filename { get; set; }
        public long LineNumber { get; set; }

        public string OriginalNamespace { get; set; }
        public string OriginalClassName { get; set; }
        internal string Source { get; set; }
        
        public Lazy<String> ClassSource 
            => new Lazy<string>(() => Source);
    }
}