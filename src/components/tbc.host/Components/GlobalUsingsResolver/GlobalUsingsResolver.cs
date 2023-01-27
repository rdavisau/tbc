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
    private const string DefaultGlobalUsingsPattern = "*.GlobalUsings.g.cs";

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

        var (search, mask) = GetFilePathAndMask(path);
        var matches = _fileSystem.Directory.GetFiles(search, mask, SearchOption.AllDirectories)
           .OrderByDescending(x => _fileSystem.FileInfo.New(x).LastWriteTime)
           .ToList();

        var usings = matches
           .Take(maybeResolutionMethod == KnownGlobalUsingSearchPathResolutionApproach.LastModified ? 1 : Int32.MaxValue)
           .Select(f => (f, u: _fileSystem.File.ReadAllLines(f).Where(x => x.StartsWith("global using ")).ToList()))
           .ToList();

        return (usings.SelectMany(x => x.u).ToList(), usings.ToDictionary(x => x.f, x => (object) $"{x.u.Count} usings extracted"));
    }

    // use a gross heuristic to decide whether we think the user provided a filemask
    // one day implement polymorphic deserialization in config so that the different kinds of global using references
    // can use their own sensible fields
    private (string Path, string Kask) GetFilePathAndMask(string path)
    {
        // this will be
        // - nothing, if a directory was specified with trailing /
        // - the leaf directory name, if a directory was specified without trailing /
        // - a filename/mask, if one was specified
        var maybeFn = _fileSystem.Path.GetFileName(path);
        if (!maybeFn.EndsWith(".cs")) // assume any filemask specified ends in .cs
            return (path, DefaultGlobalUsingsPattern);

        var search = _fileSystem.Path.GetDirectoryName(path);
        if (String.IsNullOrWhiteSpace(search)) // allow the user to just specify a mask/filename
            search = ".";

        return (search, maybeFn);
    }
}
