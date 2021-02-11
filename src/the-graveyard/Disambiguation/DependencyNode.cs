using System.Collections.Generic;

namespace Tbc.Host.Services.Patch.Models
{
    public class DependencyNode
    {
        public CSharpReloadableInput Input { get; set; }
        public List<DependencyNode> Dependencies { get; set; } = new List<DependencyNode>();
        public bool InvolvedInCycle { get; set; }
    }
}