using Tbc.Host.Components.FileWatcher.Models;
using Tbc.Host.Components.IncrementalCompiler.Models;
using Tbc.Protocol;

namespace Tbc.Host.Components.IncrementalCompiler
{
    public interface IIncrementalCompiler
    {
        string OutputPath { get; set; }
        string RootPath { get; set; }
        EmittedAssembly StageFile(ChangedFile file);
        void AddMetadataReference(AssemblyReference asm);
        void ClearReferences();
        void ClearTrees();
        void PrintTrees(bool withDetail);
        string TryResolvePrimaryType(string typeHint);
    }
}