using System;
using System.Collections.Generic;

namespace Tbc.Host.Services.Patch.Models
{
    public class CSharpReloadableInput
    {
        public bool IsPrimary { get; set; }
        
        public string OriginalIdentifier => $"{Input.OriginalNamespace}.{Input.OriginalClassName}";
        public List<string> Usings { get; set; } = new List<string>();
        public CSharpClassSource Input { get; set; }

        public bool DisambiguateClassName { get; set; } = true;
        public bool DisambiguateNameSpace { get; set; } = true;
        
        public DateTimeOffset ChangedAt { get; set; }
    }
}