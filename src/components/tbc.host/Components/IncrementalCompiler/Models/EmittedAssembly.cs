namespace Tbc.Host.Components.IncrementalCompiler.Models
{
    public class EmittedAssembly
    {
        public string AssemblyName { get; set; }
        public byte[] Pe { get; set; }
        public byte[] Pd { get; set; }

        public bool HasDebugSymbols =>
            Pd != null && Pd.Length > 0;
    }
}