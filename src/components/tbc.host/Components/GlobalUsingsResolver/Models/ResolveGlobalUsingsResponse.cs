using System.Collections.Generic;
using System.Collections.Immutable;

namespace Tbc.Host.Components.GlobalUsingsResolver.Models;

public record ResolveGlobalUsingsResponse
(
    List<GlobalUsingsSource> Sources,
    ImmutableList<string> Usings,
    string? UsingsSource,
    ImmutableDictionary<string, object> Diagnostics
);