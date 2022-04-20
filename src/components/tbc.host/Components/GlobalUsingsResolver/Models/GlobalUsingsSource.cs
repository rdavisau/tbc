namespace Tbc.Host.Components.GlobalUsingsResolver.Models;

public record GlobalUsingsSource(GlobalUsingsSourceKind Kind, string Reference, string? Context = null);