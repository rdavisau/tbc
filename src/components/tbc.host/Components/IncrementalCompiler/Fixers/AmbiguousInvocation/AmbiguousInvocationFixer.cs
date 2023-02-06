using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Tbc.Host.Components.Abstractions;

namespace Tbc.Host.Components.IncrementalCompiler.Fixers;

// exists to handle modification of an extension method that already existed in the compiled app
//
// imagine in the original assembly
//      public static string MyExtension(this string x) => x;
// then you update the extension method, and a caller
//      public static string MyExtension(this string x) => x + x;
//   elsewhere.. 
//      var me = "".MyExtension();
//
// now compiler can see the original assembly MyExtension plus the new one
// 'obviously' we want the new one 
// so we will rename the new method and update all invocations to the new name.
// in order to prevent debug symbol/positions from falling out of alignment, the new random name
// is the same length as the existing name
//
// let's see if it works good
public class AmbiguousInvocationFixer : ComponentBase<AmbiguousInvocationFixer>, ICompilationFixer
{
    public int ErrorCode => 121;

    public AmbiguousInvocationFixer(ILogger<AmbiguousInvocationFixer> logger) : base(logger)
    {
    }

    public bool TryFix(CSharpCompilation c, List<Diagnostic> rawDiagnostics, out CSharpCompilation updatedCompilation)
    {
        updatedCompilation = c;

        // multiple diagnostics referring to the same ambiguous target can be handled together
        var issues = rawDiagnostics.GroupBy(x => x.GetMessage());
        
        foreach (var issue in issues)
            updatedCompilation = TryFix(updatedCompilation, issue.ToList());
		
        return true;
    }

    private CSharpCompilation TryFix(CSharpCompilation compilation, List<Diagnostic> rawDiagnostics)
    {
        var diag = rawDiagnostics[0];

        // find the invocation node
        var invocationLocation = diag.Location;
        var invocationSpan = invocationLocation.GetLineSpan();
        var invocationTree = compilation.SyntaxTrees.First(x => x.FilePath == invocationSpan.Path);
        var invocationModel = compilation.GetSemanticModel(invocationTree);
        var invocationNode = invocationTree.GetRoot().FindNode(invocationLocation.SourceSpan);
        var invocationNodeInfo = invocationModel.GetSymbolInfo(invocationNode);

        // find the correct symbol (the source one, not the assembly one)
        var declaration = invocationNodeInfo.CandidateSymbols.First(x => x.Locations[0].Kind is LocationKind.SourceFile);
        var declarationRef = declaration.DeclaringSyntaxReferences.First();
        var declarationTree = declarationRef.SyntaxTree;
        var declarationModel = compilation.GetSemanticModel(declarationTree);
        var declarationNode = declarationTree.GetRoot().FindNode(declarationRef.Span);

        if (declarationNode is not MethodDeclarationSyntax mds)
            throw new NotSupportedException("Only know how to rename methods right meow");

        // determine the new identifier
        var currentIdentifier = mds.Identifier.Text;
        var identifierLength = currentIdentifier.Length;
        var newIdentifier = ("R2" + String.Join("", Enumerable.Range(0, 1 + (identifierLength / 32)).Select(_ => Guid.NewGuid().ToString("N"))))
           .Substring(0,identifierLength);
		
        // replace the original identifier
        var newMds = mds.WithIdentifier(SyntaxFactory.Identifier(newIdentifier));
        var updatedDeclarationRoot = (CompilationUnitSyntax) declarationTree.GetRoot().ReplaceNode(mds, newMds);

        // update the declaration tree
        compilation = compilation.ReplaceSyntaxTree(declarationTree, updatedDeclarationRoot.SyntaxTree);
		
        // find all the invocations and replace them too
        // should use a syntax walker for this
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var originalTree = syntaxTree;
            var innerModel = compilation.GetSemanticModel(syntaxTree);
            var tree = innerModel.SyntaxTree;

            var any = true; var spinCount = 0;
            while (any) // yeah if only i had used a syntax walker
            {
                // todo:
                // need to verify it's an actual use of the renamed symbol
                // and not just an invocation of something else with the same name 
                var invocations =
                    tree
                       .GetRoot()
                       .DescendantNodes()
                       .OfType<InvocationExpressionSyntax>()
                       .Where(x => x.Expression is MemberAccessExpressionSyntax)
                       .Where(x => (((MemberAccessExpressionSyntax)x.Expression).Name).Identifier.Text ==
                                   mds.Identifier.Text)
                       .ToList();

                foreach (var inv in invocations)
                {
                    var newInv = inv.ReplaceNode(((MemberAccessExpressionSyntax)inv.Expression).Name,
                        ((MemberAccessExpressionSyntax)inv.Expression).Name.WithIdentifier(
                            SyntaxFactory.Identifier(newIdentifier)));

                    var from = inv.FindNode(inv.Span, getInnermostNodeForTie: true);
                    var to = newInv.FindNode(newInv.Span, getInnermostNodeForTie: true);

                    tree = tree.GetRoot().ReplaceNode(from, to).SyntaxTree;
                }

                // todo: remove this duplication
                any = (++spinCount) < maxSpinCount &&
                    tree
                       .GetRoot()
                       .DescendantNodes()
                       .OfType<InvocationExpressionSyntax>()
                       .Where(x => x.Expression is MemberAccessExpressionSyntax)
                       .Any(x => ((MemberAccessExpressionSyntax)x.Expression).Name.Identifier.Text ==
                                 mds.Identifier.Text);
            }

            compilation = compilation.ReplaceSyntaxTree(originalTree, tree);
        }
		
        return compilation;
    }

    private const int maxSpinCount = 1000;
}
