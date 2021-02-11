using System.Collections.Generic;
using System.Linq;

namespace Tbc.Host.Services.Patch.Models
{
    public class DependencySet
    {
        public List<CSharpReloadableInput> AllInputs { get; set; } = new List<CSharpReloadableInput>();
        public Dictionary<CSharpReloadableInput, DependencyNode> Dependencies { get; set; } = new Dictionary<CSharpReloadableInput, DependencyNode>();

        public bool IsDependentOn(string subjectIdentifier, string dependencyIdentifier)
        {
            if (subjectIdentifier is null || dependencyIdentifier is null || !AllInputs.Any())
                return false;

            var subject = AllInputs.FirstOrDefault(x => x.OriginalIdentifier.EndsWith(subjectIdentifier));
            var target = AllInputs.FirstOrDefault(x => x.OriginalIdentifier.EndsWith(dependencyIdentifier));

            return Dependencies[subject].Dependencies.Any(x => x.Input.OriginalIdentifier == target.OriginalIdentifier);
        }

        public bool IsDependencyOf(string dependencyIdentifier, string subjectIdentifier)
            => IsDependentOn(subjectIdentifier, dependencyIdentifier);
    }
}