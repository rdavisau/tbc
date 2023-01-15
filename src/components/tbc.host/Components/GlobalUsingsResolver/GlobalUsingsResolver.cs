using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.GlobalUsingsResolver.Models;

namespace Tbc.Host.Components.GlobalUsingsResolver;

public class GlobalUsingsResolver : ComponentBase<GlobalUsingsResolver>, IGlobalUsingsResolver
{
    private IFileSystem _fileSystem;
    
    public GlobalUsingsResolver(ILogger<GlobalUsingsResolver> logger, IFileSystem fileSystem) : base(logger)
    {
        _fileSystem = fileSystem;
    }

    public Task<ResolveGlobalUsingsResponse> ResolveGlobalUsings(ResolveGlobalUsingsRequest request, CancellationToken canceller = default)
    {
        var sources = request.Sources;
        var usings = ImmutableList.Create<string>();
        var diagnostics = ImmutableDictionary.Create<string, object>();

        foreach (var source in sources)
        {
            try
            {
                var (newUsings, newDiagnostics) =
                    source.Kind switch
                    {
                        GlobalUsingsSourceKind.Text => GetUsingsFromText(source.Reference),
                        GlobalUsingsSourceKind.SearchPath => GetUsingsFromSearchPath(source.Reference, source.Context),
                        _ => throw new ArgumentOutOfRangeException()
                    };

                usings = usings.AddRange(newUsings);
                diagnostics = diagnostics.AddRange(newDiagnostics);
            }
            catch (Exception ex)
            {
                diagnostics = diagnostics.Add(source.Reference, $"Failed to process global using reference {ex}");
            }
        }

        usings = usings.Distinct().ToImmutableList();

        return Task.FromResult(new ResolveGlobalUsingsResponse(
            sources, usings.Distinct().ToImmutableList(), 
            usings.Any() ? String.Join(Environment.NewLine, usings) : null, 
            diagnostics));
    }

    public (List<string> Usings, Dictionary<string, object> Diagnostics) GetUsingsFromText(string text)
    {
        try
        {
            var usings = text.Split(';').Select(x => $"global using global::{x}").ToList();
            
            return new(usings, new() { [text] = $"{usings.Count} usings extracted" });
        }
        catch (Exception ex) { return (new(), new() { [text] = ex }); }
    }
    
    public (List<string> Usings, Dictionary<string, object> Diagnostics) GetUsingsFromSearchPath(
        string path, string? maybeResolutionMethod)
    {
        maybeResolutionMethod ??= KnownGlobalUsingSearchPathResolutionApproach.LastModified;
        
        var matches = _fileSystem.Directory.GetFiles(path, "*.GlobalUsings.g.cs", SearchOption.AllDirectories)
           .OrderByDescending(x => _fileSystem.FileInfo.FromFileName(x).LastWriteTime)
           .ToList();

        var usings = matches
           .Take(maybeResolutionMethod == KnownGlobalUsingSearchPathResolutionApproach.LastModified ? 1 : Int32.MaxValue)
           .Select(f => (f, u: _fileSystem.File.ReadAllLines(f).Where(x => x.StartsWith("global using ")).ToList()))
           .ToList();

        return (usings.SelectMany(x => x.u).ToList(), usings.ToDictionary(x => x.f, x => (object) $"{x.u.Count} usings extracted"));
    }
}
