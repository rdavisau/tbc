using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Tbc.Host.Services.Patch.Models;

namespace Tbc.Host.Services.Patch
{
    public class DisambiguationRewriterConfig
    {
        public SemanticModel Model { get; set; }
        public List<CSharpReloadableInput> Inputs { get; set; }
        public List<string> Usings { get; set; }
        public List<DependencyNode> DependencyNodes { get; set; }
    }
}