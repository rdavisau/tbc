using System.Collections.Generic;

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
    }
}