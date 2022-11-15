using System.Collections.Generic;
using Tbc.Core.Models;
using Tbc.Host.Components.FileWatcher.Models;
using Tbc.Host.Components.IncrementalCompiler.Models;

namespace Tbc.Host.Components.IncrementalCompiler
{
    public interface IIncrementalCompiler
    {
        List<string> StagedFiles { get; }
        EmittedAssembly? StageFile(ChangedFile file) => StageFile(file, false);
        EmittedAssembly? StageFile(ChangedFile file, bool silent = false);
        void AddMetadataReference(AssemblyReference asm);
        void ClearReferences();
        void ClearTrees();
        void PrintTrees(bool withDetail);
        string? TryResolvePrimaryType(string typeHint);
        void DoWarmup();
        void SetRootPath(string rootPath);
    }
}
