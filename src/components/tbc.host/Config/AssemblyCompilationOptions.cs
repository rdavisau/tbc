using System.Collections.Generic;
using Tbc.Host.Components.GlobalUsingsResolver;
using Tbc.Host.Components.GlobalUsingsResolver.Models;
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
        public List<GlobalUsingsSource> GlobalUsingsSources { get; set; } = new();
        public AssemblyFixerOptions FixerOptions { get; set; } = new() { Enabled = true };
        public iOSDynamicRegistrationOptions iOSDynamicRegistrationOptions { get; set; } = new();
    }
}
