using System.Collections.Generic;
using Tbc.Host.Components.SourceGeneratorResolver;
using Tbc.Host.Components.SourceGeneratorResolver.Models;

namespace Tbc.Host.Config
{
    public class AssemblyCompilationOptions
    {
        public bool Debug { get; set; } = true;
        public bool EmitDebugInformation { get; set; }
        public bool DisambiguateClassNames { get; set; }
        public List<string> PreprocessorSymbols { get; set; } 
            = new List<string>();
        public string WriteAssembliesPath { get; set; }
        public List<SourceGeneratorReference> SourceGeneratorReferences { get; set; } = new();
    }
}
