using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Tbc.Host.Services.Patch.Models;

namespace Tbc.Host.Services.Patch
{
    public class DisambiguationRewriter : CSharpSyntaxRewriter
    {
        protected ILogger<DisambiguationRewriter> Logger { get; }
        
        public SemanticModel Model { get; set; }
        public List<CSharpReloadableInput> Inputs { get; set; }
        public List<string> Usings { get; set; }

        public List<DependencyNode> DependencyNodes = new List<DependencyNode>();
        public DependencyNode ThisDependencyNode { get; set; }

        public DisambiguationRewriter(ILogger<DisambiguationRewriter> logger)
        {
            Logger = logger;
        }

        public void Init(DisambiguationRewriterConfig config, CSharpReloadableInput targetInput)
        {
            Model = config.Model;
            Inputs = config.Inputs;
            Usings = config.Usings;
            DependencyNodes = config.DependencyNodes;

            var thisNode = DependencyNodes.FirstOrDefault(x => x.Input == targetInput);
            if (thisNode == null)
            {
                Logger.LogInformation("Creating node for {TypeIdentifier}", targetInput.OriginalIdentifier);
                thisNode = new DependencyNode { Input = targetInput };
                DependencyNodes.Add(thisNode);
            }

            ThisDependencyNode = thisNode;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var transformedNode = base.VisitClassDeclaration(node) as ClassDeclarationSyntax;

            if (node.BaseList?.ChildNodes().Any() ?? false)
                ProcessBaseType(node.BaseList);

            return transformedNode; // .WithIdentifier(SyntaxFactory.Identifier(node.Identifier.Text + "XXX"));
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            var visited = base.VisitIdentifierName(node);
            if (visited is null || !(Model.GetSymbolInfo(node).Symbol is INamedTypeSymbol))
                return visited;

            var matched = TryMatchInput(node, out var matchingInput);
            if (matched)
                AddDependency(matchingInput, DependencyKind.Reference);
            else
                Logger.LogWarning("Couldn't match input for model-recognised identifier {Identifier}", node.Identifier);

            return node; // SyntaxFactory.IdentifierName(node.Identifier + "ABC").WithTriviaFrom(node);
        }

        public override SyntaxNode VisitQualifiedName(QualifiedNameSyntax node)
        {
            var visited = base.VisitQualifiedName(node);
            if (visited is null)
                return visited;

            var matched = TryMatchInput(node, out var matchingInput);
            if (matched)
                AddDependency(matchingInput, DependencyKind.Reference);

            return node; //SyntaxFactory.QualifiedName(node.Left, SyntaxFactory.IdentifierName(node.Right + "QUALIFIED")).WithTriviaFrom(node);
        }

        private void ProcessBaseType(BaseListSyntax baseList)
        {
            var baseType = baseList.DescendantNodes().First().ToString();
            var references = baseList.DescendantNodes().OfType<IdentifierNameSyntax>().ToList();

            var matchingInputs =
                references
                .Select(x =>
                    TryMatchInput(x, out var input)
                    ? input : null
                )
                .Where(x => x != null);

            foreach (var matchingInput in matchingInputs)
                AddDependency(matchingInput, DependencyKind.BaseClass);
        }

        private bool TryMatchInput(SyntaxNode node, out CSharpReloadableInput input)
        {
            if (node is IdentifierNameSyntax idns)
            {
                var className = idns.Identifier.ToString();

                var matchingInputs = Inputs.Where(x => x.Input.OriginalClassName == className).ToList();
                if (!matchingInputs.Any())
                {
                    input = null;
                    return false;
                }

                if (matchingInputs.Count == 1)
                {
                    input = matchingInputs[0];
                    return true;
                }

                foreach (var u in Usings)
                {
                    var matchingWithNamespace =
                        matchingInputs
                            .FirstOrDefault(x => x.OriginalIdentifier == $"{u}.{className}");

                    if (matchingWithNamespace != null)
                    {
                        input = matchingWithNamespace;
                        return true;
                    }
                }

                input = null;
                return false;
            }
            else if (node is QualifiedNameSyntax qns)
            {
                var nodeIdentifier = $"{qns}";

                input = Inputs.FirstOrDefault(x => x.OriginalIdentifier == nodeIdentifier);
                return input != null;
            }
            else
            {
                Logger.LogError("Don't know how to match input of type {node.GetType().Name}");
                input = null;
                return false;
            }
        }

        public void AddDependency(CSharpReloadableInput input, DependencyKind kind)
        {
            if (input.OriginalIdentifier == this.ThisDependencyNode.Input.OriginalIdentifier)
            {
                Logger.LogDebug("Not adding dependency to self: {OriginalIdentifier}", input.OriginalIdentifier);
                return;
            }
            
            var existingNode = DependencyNodes.FirstOrDefault(x => x.Input == input);
            if (existingNode == null)
            {
                existingNode = new DependencyNode { Input = input };
                DependencyNodes.Add(existingNode);
            }

            if (!ThisDependencyNode.Dependencies.Contains(existingNode))
                ThisDependencyNode.Dependencies.Add(existingNode);
        }
    }
}