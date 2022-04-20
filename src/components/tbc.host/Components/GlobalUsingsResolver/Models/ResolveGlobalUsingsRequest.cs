using System.Collections.Generic;

namespace Tbc.Host.Components.GlobalUsingsResolver.Models;

public record ResolveGlobalUsingsRequest(List<GlobalUsingsSource> Sources);