using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Tbc.Host.Components.SourceGeneratorResolver.Models;

public record ResolveSourceGeneratorsResponse(
    SourceGeneratorReference Reference,
    ImmutableList<ISourceGenerator> SourceGenerators,
    ImmutableList<IIncrementalGenerator> IncrementalGenerators,
    ImmutableDictionary<string, object> Diagnostics
);
