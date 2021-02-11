using Inject.Protocol;

namespace Tbc.Host.Services.Patch
{
    public class CachedAssemblyReference
    {
        public static CachedAssemblyReference FromReference(AssemblyReference @ref, string forLocation)
        {
            return new CachedAssemblyReference
            {
                AssemblyName = @ref.AssemblyName,
                AssemblyLocation = forLocation,
                SourceAssemblyLocation = @ref.AssemblyLocation,
                ModificationTime = @ref.ModificationTime
            };
        }
        
        public string AssemblyName { get; set; }
        public string AssemblyLocation { get; set; }
        public string SourceAssemblyLocation { get; set; }
        public ulong ModificationTime { get; set; }
    }
}