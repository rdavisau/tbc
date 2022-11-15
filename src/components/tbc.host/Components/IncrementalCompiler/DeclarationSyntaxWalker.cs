using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Tbc.Host.Components.IncrementalCompiler
{
    public class DeclarationSyntaxWalker<T> : SyntaxWalker
        where T : SyntaxNode
    {
        public List<T> Members { get; private set; } = new List<T>();

        public override void Visit(SyntaxNode node)
        {
            if (node is T syntaxNode)
                Members.Add(syntaxNode);
            else
                base.Visit(node);
        }

        public List<T> Visit(SyntaxTree tree)
        {
            Members = new List<T>();
            Visit(tree.GetRoot());
            return Members;
        }
    }
}
