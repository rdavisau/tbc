using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.SourceGeneratorResolver.Models;

namespace Tbc.Host.Components.SourceGeneratorResolver;

public class SourceGeneratorResolver : ComponentBase<SourceGeneratorResolver>, ISourceGeneratorResolver
{
    private readonly IFileSystem _fileSystem;
    
    private string GetNugetPackageCachePath
        => NuGet.Configuration.SettingsUtility.GetGlobalPackagesFolder(NuGet.Configuration.Settings.LoadDefaultSettings(null));
    
    private static readonly ImmutableHashSet<Type> SourceGeneratorTypes 
        = new[] { typeof(IIncrementalGenerator), typeof(ISourceGenerator) }.ToImmutableHashSet();
    
    public SourceGeneratorResolver(ILogger<SourceGeneratorResolver> logger, IFileSystem fileSystem) : base(logger)
    {
        _fileSystem = fileSystem;
    }

    public Task<ResolveSourceGeneratorsResponse> ResolveSourceGenerators(ResolveSourceGeneratorsRequest request,
        CancellationToken canceller = default)
    {
        var (req, (kind, reference, context)) = (request.Reference, request.Reference);
        
        var (srcs, incs, diags) = kind switch
        {
            SourceGeneratorReferenceKind.AssemblyPath => GetGeneratorsFromAssembly(reference),
            SourceGeneratorReferenceKind.NuGetPackageReference => GetGeneratorsFromNugetPackageReference(reference, context),
            SourceGeneratorReferenceKind.Csproj => GetGeneratorsFromCsproj(reference),
            
            _ => throw new NotImplementedException()
        };

        // remove any dups
        srcs = srcs.GroupBy(x => x.GetType()).Select(x => x.First()).ToImmutableList();
        incs = incs.GroupBy(x => x.GetType()).Select(x => x.First()).ToImmutableList();

        return Task.FromResult(new ResolveSourceGeneratorsResponse(req, srcs, incs, diags));
    }

    private (ImmutableList<ISourceGenerator> Srcs, ImmutableList<IIncrementalGenerator> Incs, ImmutableDictionary<string, object> Diags) 
        GetGeneratorsFromNugetPackageReference(string package, string? maybeVersion)
    {
        var version = maybeVersion
            ?? _fileSystem.Directory.GetDirectories(_fileSystem.Path.Combine(GetNugetPackageCachePath, package))
                   .OrderByDescending(x => x)
                   .FirstOrDefault();

        if (String.IsNullOrWhiteSpace(version))
        {
            Logger.LogWarning("No version provided for source generator package {Package} and none could be inferred",
                package);

            return (Srcs: ImmutableList.Create<ISourceGenerator>(),
                Incs: ImmutableList.Create<IIncrementalGenerator>(),
                Diags: ImmutableDictionary.Create<string, object>());
        }

        var searches = new[] { "roslyn4.0/cs", "roslyn4.0\\cs", "analyzers/dotnet/cs", "analyzers\\dotnet\\cs", "analyzers/dotnet/roslyn4.0", "analyzers\\dotnet\\roslyn4.0", };

        var nugetPath = _fileSystem.Path.Combine(GetNugetPackageCachePath, package, version);
        var dllPaths = _fileSystem.Directory.GetFiles(nugetPath, "*.dll", SearchOption.AllDirectories)
           .Where(x => searches.Any(s => x.Contains(s, StringComparison.InvariantCultureIgnoreCase)));

        return Enumerable.Aggregate(
            dllPaths,
            (   Srcs: ImmutableList.Create<ISourceGenerator>(), 
                Incs: ImmutableList.Create<IIncrementalGenerator>(), 
                Diags: ImmutableDictionary.Create<string, object>()),
            (curr, path) =>
            {
                var (newSrc, newInc, newDiagnostics) = GetGeneratorsFromAssembly(path);

                return (curr.Srcs.AddRange(newSrc), curr.Incs.AddRange(newInc), curr.Diags.AddRange(newDiagnostics));
            });
    }
    
    private (ImmutableList<ISourceGenerator>, ImmutableList<IIncrementalGenerator>, ImmutableDictionary<string, object>) 
        GetGeneratorsFromAssembly(string path)
    {
        var diagnostics = ImmutableDictionary.Create<string, object>();

        Assembly asm = null!;
        try { asm = Assembly.LoadFile(path); }
        catch (Exception ex)
        {
            return (ImmutableList<ISourceGenerator>.Empty, ImmutableList<IIncrementalGenerator>.Empty,
                diagnostics.Add($"load-assembly {path}", ex));
        }
        
        var generatorTypes = asm.GetTypes()
           .Where(x => x.GetInterfaces().Any(SourceGeneratorTypes.Contains))
           .Where(x => !x.IsAbstract)
           .ToList();

        var instances = generatorTypes.Select(x =>
        {
            try { return Activator.CreateInstance(x); }
            catch (Exception ex) {
                diagnostics = diagnostics.Add($"instantiate {x.FullName}", ex); 
                return null;
            }
        })
        .Where(x => x != null)
        .ToList();

        var srcs = instances.OfType<ISourceGenerator>().ToImmutableList();
        var incs = instances.OfType<IIncrementalGenerator>().ToImmutableList();

        return (srcs, incs, diagnostics);
    }
    
    private (ImmutableList<ISourceGenerator> Srcs, ImmutableList<IIncrementalGenerator> Incs, ImmutableDictionary<string, object> Diags) 
        GetGeneratorsFromCsproj(string csProjPath)
    {
        var xml = _fileSystem.File.ReadAllText(csProjPath);
        var doc = XDocument.Parse(xml);
        var packageReferences =
            doc.DescendantNodes()
               .OfType<XElement>()
               .Where(x => x.Name.LocalName == "PackageReference")
               .Select(pr => new { Include = pr.Attribute("Include")?.Value, Version = pr.Attribute("Version")?.Value })
               .Where(x => x.Include is not null && x.Version is not null)
               .Select(pr => new PackageReference(pr.Include!, pr.Version!))
               .ToList();

        foreach (var packageReference in packageReferences)
            Logger.LogDebug("Found nuget ref in {CsProjName}: {PackageIdentifier}, version {PackageVersion}",
                Path.GetFileName(csProjPath), packageReference.Include, packageReference.Version);

        return Enumerable.Aggregate(
            packageReferences,
            (   Srcs: ImmutableList.Create<ISourceGenerator>(), 
                Incs: ImmutableList.Create<IIncrementalGenerator>(), 
                Diags: ImmutableDictionary.Create<string, object>()),
            (curr, packageRef) =>
            {
                var (newSrc, newInc, newDiagnostics) = 
                    GetGeneratorsFromNugetPackageReference(packageRef.Include, packageRef.Version.ToString());

                return (curr.Srcs.AddRange(newSrc), curr.Incs.AddRange(newInc), curr.Diags.AddRange(newDiagnostics));
            });
    }
}
