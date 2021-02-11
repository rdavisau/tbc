using System.Collections.Generic;
using Tbc.Host.Services.Patch.Models;

namespace Tbc.Host.Services.Patch.Extensions
{
    public static class DependencyExtensions
    {
        public static List<DependencyNode> OrderDependencies(this List<DependencyNode> dependencySet)
        {
            var results = new List<DependencyNode>();
            var seen = new List<DependencyNode>();
            var pending = new List<DependencyNode>();

            Visit(dependencySet, results, seen, pending);

            return results;
        }

        private static void Visit(List<DependencyNode> graph, List<DependencyNode> results, List<DependencyNode> dead,
            List<DependencyNode> pending, DependencyNode parent = null)
        {
            // Foreach node in the graph
            foreach (var n in graph)
            {
                // Skip if node has been visited
                if (!dead.Contains(n))
                {
                    if (!pending.Contains(n))
                    {
                        pending.Add(n);
                    }
                    else
                    {
                        n.InvolvedInCycle = true;
                        if (parent != null)
                            parent.InvolvedInCycle = true;

                        return;
                    }

                    // recursively call this function for every child of the current node
                    Visit(n.Dependencies, results, dead, pending);

                    if (pending.Contains(n))
                    {
                        pending.Remove(n);
                    }

                    dead.Add(n);

                    // Made it past the recusion part, so there are no more dependents.
                    // Therefore, append node to the output list.
                    results.Add(n);    
                }
            }
        }
    }
}