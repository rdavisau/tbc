using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Tbc.Host.Components.IncrementalCompiler.Fixers;

public interface ICompilationFixer
{
    int ErrorCode { get; }

    public bool TryFix(CSharpCompilation c, List<Diagnostic> diagnostic, out CSharpCompilation updatedCompilation);
}
