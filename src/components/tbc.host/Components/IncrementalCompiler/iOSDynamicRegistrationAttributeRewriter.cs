using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Reflection;

namespace Tbc.Host.Components.IncrementalCompiler;

// adds a unique [Register] attribute to every nsobject-derived type
public class iOSDynamicRegistrationAttributeRewriter : CSharpSyntaxRewriter
{
    private readonly MetadataLoadContext _metadataLoadContext;
    private readonly SemanticModel _semanticModel;
    private readonly Type _nsObject;

    public iOSDynamicRegistrationAttributeRewriter(MetadataLoadContext metadataLoadContext, SemanticModel semanticModel)
    {
        _metadataLoadContext = metadataLoadContext;
        _semanticModel = semanticModel;

        _nsObject = _metadataLoadContext.ResolveType("Foundation.NSObject");
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbolForNode(node);
        if (symbol is null) return base.VisitClassDeclaration(node);

        var symbolReference = $"{symbol.ContainingNamespace}.{symbol.MetadataName}";
        var type = _metadataLoadContext.ResolveType(symbolReference);
        if (type is null)
            return base.VisitClassDeclaration(node);

        if (type.IsSubclassOf(_nsObject))
        {
            var attributeArgument =
                SyntaxFactory.AttributeList
                    (SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Register"),
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SeparatedList(new[] {
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal($"{symbol.MetadataName}_{Guid.NewGuid()}"))
                                )
                            })))));

            var updatedNode = node.WithAttributeLists(node.AttributeLists.Add(attributeArgument));

            return updatedNode;
        }

        return base.VisitClassDeclaration(node);
    }
}
