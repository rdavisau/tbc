using System.Collections.Generic;
using System.Linq;

namespace Tbc.Host.Services.Patch.Models
{
    public class Dependency
    {
        public DependencyDirection Direction { get; set; }
        public CSharpReloadableInput Input { get; set; }
        public List<DependencyKind> Kinds { get; set; }

        public Dependency(CSharpReloadableInput input, DependencyKind kind)
        {
            Input = input;
            Kinds = new[] { kind }.ToList();
        }
    }
}