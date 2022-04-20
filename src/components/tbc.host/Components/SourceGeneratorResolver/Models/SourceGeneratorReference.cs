namespace Tbc.Host.Components.SourceGeneratorResolver.Models;

public record SourceGeneratorReference(SourceGeneratorReferenceKind Kind, string Reference, string? Context = null);