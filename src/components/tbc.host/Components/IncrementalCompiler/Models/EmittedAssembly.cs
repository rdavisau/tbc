namespace Tbc.Host.Components.IncrementalCompiler.Models
{
    public record EmittedAssembly
    {
        public required string AssemblyName { get; init; }
        public required byte[] Pe { get; init; }
        public byte[]? Pd { get; init; }

        public bool HasDebugSymbols =>
            Pd is { Length: > 0 };
    }
}
