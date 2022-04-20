using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Logging;
using Tbc.Core.Models;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.CommandProcessor.Models;
using Tbc.Host.Components.FileEnvironment.Models;
using Tbc.Host.Components.FileWatcher.Models;
using Tbc.Host.Components.GlobalUsingsResolver;
using Tbc.Host.Components.GlobalUsingsResolver.Models;
using Tbc.Host.Components.IncrementalCompiler.Models;
using Tbc.Host.Components.SourceGeneratorResolver;
using Tbc.Host.Components.SourceGeneratorResolver.Models;
using Tbc.Host.Components.TargetClient;
using Tbc.Host.Config;
using Tbc.Host.Extensions;

namespace Tbc.Host.Components.IncrementalCompiler
{
    public class IncrementalCompiler : ComponentBase<IncrementalCompiler>, IIncrementalCompiler, IDisposable, IExposeCommands
    {
        private bool _disposing;
        
        private readonly AssemblyCompilationOptions _options;
        
        private readonly IFileSystem _fileSystem;
        private readonly ISourceGeneratorResolver _sourceGeneratorResolver;
        private readonly IGlobalUsingsResolver _globalUsingsResolver;

        private int _incrementalCount = 0;
        private readonly Guid _sessionGuid = Guid.NewGuid();
        private readonly object _lock = new object();
        private readonly string _identifier;

        public string OutputPath { get; set; }
        public string RootPath { get; set; }

        public ImmutableDictionary<SourceGeneratorReference,ResolveSourceGeneratorsResponse> SourceGeneratorResolution { get; set; }
        public (IEnumerable<ISourceGenerator> Srcs, IEnumerable<IIncrementalGenerator> Incs) SourceGenerators { get; set; }
        public bool AnySourceGenerators => SourceGenerators.Srcs.Any() || SourceGenerators.Incs.Any();

        public ResolveGlobalUsingsResponse GlobalUsingResolution { get; set; }
        
        public CSharpCompilation CurrentCompilation { get; set; }
        public Dictionary<string, SyntaxTree> RawTrees { get; } = new();


        public List<string> StagedFiles
            => CurrentCompilation.SyntaxTrees.Select(x => x.FilePath).ToList();

        public IncrementalCompiler(
            string clientIdentifier,
            AssemblyCompilationOptions options, 
            IFileSystem fileSystem, ILogger<IncrementalCompiler> logger,
            ISourceGeneratorResolver sourceGeneratorResolver,
            IGlobalUsingsResolver globalUsingsResolver
        ) : base(logger)
        {
            _options = options;
            _fileSystem = fileSystem;
            _sourceGeneratorResolver = sourceGeneratorResolver;
            _globalUsingsResolver = globalUsingsResolver;
            _identifier = clientIdentifier;

            var cscOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: options.Debug ? OptimizationLevel.Debug : OptimizationLevel.Release,
                allowUnsafe: true);

            SourceGeneratorResolution = ResolveSourceGenerators();
            SourceGenerators = 
                (SourceGeneratorResolution.SelectMany(x => x.Value.SourceGenerators).DistinctBySelector(x => x.GetType()).ToList(),
                 SourceGeneratorResolution.SelectMany(x => x.Value.IncrementalGenerators).DistinctBySelector(x => x.GetType()).ToList());
            
            Logger.LogInformation("Source Generator Resolution: {Count} generator(s)", SourceGeneratorResolution);
            foreach (var resolutionOutcome in SourceGeneratorResolution)
                Logger.LogDebug(
                    "Reference {@Reference}, Diagnostics: {@Diagnostics}, " +
                    "Source Generators: {@SourceGenerators}, Incremental Generators: {@IncrementalGenerators}", 
                    resolutionOutcome.Key, resolutionOutcome.Value.Diagnostics, resolutionOutcome.Value.SourceGenerators, resolutionOutcome.Value.IncrementalGenerators);
            
            CurrentCompilation =
                CSharpCompilation.Create("r2", options: cscOptions);
            
            GlobalUsingResolution =
                _globalUsingsResolver.ResolveGlobalUsings(new ResolveGlobalUsingsRequest(options.GlobalUsingsSources)).Result;
            
            if (GlobalUsingResolution.Usings.Any() && GlobalUsingResolution.UsingsSource is { } gus)
                CurrentCompilation = CurrentCompilation.AddSyntaxTrees(
                    CSharpSyntaxTree.ParseText(
                        gus,
                        CSharpParseOptions.Default
                           .WithLanguageVersion(LanguageVersion.Preview)
                           .WithKind(SourceCodeKind.Regular)
                           .WithPreprocessorSymbols(_options.PreprocessorSymbols.ToArray()),
                        path: "",
                        Encoding.Default));
        }

        private ImmutableDictionary<SourceGeneratorReference, ResolveSourceGeneratorsResponse> ResolveSourceGenerators()
            => Enumerable.Aggregate(_options.SourceGeneratorReferences,
                ImmutableDictionary.Create<SourceGeneratorReference, ResolveSourceGeneratorsResponse>(),
                (curr, next) => curr.Add(next, _sourceGeneratorResolver.ResolveSourceGenerators(new(next)).Result));

        public EmittedAssembly StageFile(ChangedFile file, bool silent = false)
        {
            var sw = Stopwatch.StartNew();

            file.Contents = file.Contents.Replace(
                "_MYGUID_MYGUID_MYGUID_MYGUID_MYGUID_",
                Guid.NewGuid().ToString().Replace("-", "_"));
                            
            var syntaxTree = 
                CSharpSyntaxTree.ParseText(
                    file.Contents,
                    CSharpParseOptions.Default
                        .WithLanguageVersion(LanguageVersion.Preview)
                        .WithKind(SourceCodeKind.Regular)
                        .WithPreprocessorSymbols(_options.PreprocessorSymbols.ToArray()),
                    path: file.Path, 
                    Encoding.Default);

            EmittedAssembly emittedAssembly = null;
            
            WithCompilation(c =>
            {
                var newCompilation = RawTrees.TryGetValue(file.Path, out var oldSyntaxTree)
                    ? c.ReplaceSyntaxTree(oldSyntaxTree, syntaxTree)
                    : c.AddSyntaxTrees(syntaxTree);

                RawTrees[file.Path] = syntaxTree;

                if (silent)
                {
                    Logger.LogInformation(
                        "Stage '{FileName}' without emit, Duration: {Duration:N0}ms, Types: [ {Types} ]",
                        file, sw.ElapsedMilliseconds, syntaxTree.GetContainedTypes());
                    
                    return newCompilation;
                }

                // keep a separate source generated compilation so we don't poison the raw tree with generated content
                Compilation? sourceGeneratedCompilation = null;
                if (AnySourceGenerators)
                {
                    var (srcs, incs) = SourceGenerators;
                    var driver = 
                        CSharpGeneratorDriver.Create(incs.ToArray())
                            .AddGenerators(srcs.ToImmutableArray())
                            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: _options.PreprocessorSymbols));
                    
                    driver.RunGeneratorsAndUpdateCompilation(
                        newCompilation, out sourceGeneratedCompilation, out var generatorDiagnostics);
                    
                    if (generatorDiagnostics.Any())
                        Logger.LogWarning("Applying source generators resulted in diagnostics: {@Diagnostics}", 
                            generatorDiagnostics);
                }
                
                var result = EmitAssembly(sourceGeneratedCompilation ?? newCompilation, out emittedAssembly);

                if (!String.IsNullOrWhiteSpace(_options.WriteAssembliesPath)) 
                    WriteEmittedAssembly(emittedAssembly);

                var elapsed = sw.ElapsedMilliseconds;

                Logger.LogInformationAsync(
                    "Stage '{FileName}' and emit - Success: {Success}, Duration: {Duration:N0}ms, Types: [ {Types} ], Diagnostics: {@Diagnostics}", 
                    Path.GetFileName(file.Path), result.Success, elapsed, syntaxTree.GetContainedTypes(),
                    result.Success 
                        ? "" 
                        : String.Join(
                            Environment.NewLine,
                            result.Diagnostics
                                .Where(x => x.Severity == DiagnosticSeverity.Error)
                                .Select(x => $"{x.Location}: {x.GetMessage()}")));

                return newCompilation;
            });
            
            return emittedAssembly;
        }
        
        public EmitResult EmitAssembly(Compilation compilation, out EmittedAssembly emittedAssembly)
        {
            var asmStream = new MemoryStream();
            var pdbStream = new MemoryStream();

            var result =
                _options.EmitDebugInformation
                    ? compilation.Emit(
                        asmStream,
                        pdbStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb))
                    : compilation.Emit(asmStream);

            emittedAssembly = new EmittedAssembly
            {
                Pe = asmStream.GetBuffer(),
                Pd = pdbStream.GetBuffer(),
                AssemblyName = $"emit-{(++_incrementalCount)}"
            };
            
            return result;
        }
        
        public void WriteEmittedAssembly(EmittedAssembly emittedAssembly)
        {
            Logger.LogInformation("Writing emitted assembly {@Assembly}", emittedAssembly);
            
            var peOut = _fileSystem.Path.Combine(_options.WriteAssembliesPath, $"{emittedAssembly.AssemblyName}.dll");
            var pdOut = _fileSystem.Path.Combine(_options.WriteAssembliesPath, $"{emittedAssembly.AssemblyName}.pdb");

            _fileSystem.File.WriteAllBytes(peOut, emittedAssembly.Pe);

            if (_options.EmitDebugInformation)
                _fileSystem.File.WriteAllBytes(pdOut, emittedAssembly.Pd);
        }

        public void AddMetadataReference(AssemblyReference asm)
            => WithCompilation(c =>
            {
                /*
                Logger.LogInformation(
                    "Adding reference to '{AssemblyLocation}' for client '{Client}'",
                    Path.GetFileName(asm.AssemblyLocation), _client.Client);
                */
                return c.AddReferences(MetadataReference.CreateFromImage(asm.PeBytes));
            });
        
        private void WithCompilation(Func<CSharpCompilation, CSharpCompilation> action)
        {
            lock (_lock) CurrentCompilation = action(CurrentCompilation);
        }

        public void ClearReferences() =>
            WithCompilation(c =>
            {
                Logger.LogInformation("Clearing references");
                
                return c.RemoveAllReferences();
            });

        public void ClearTrees() =>
            WithCompilation(c =>
            {
                Logger.LogInformation("Clearing syntax trees");
                
                RawTrees.Clear();
                return c.RemoveAllSyntaxTrees();
            });
        
        public void PrintTrees(bool withDetail)
        {           
            Logger.LogInformation(RootPath);
            
            var output = RawTrees
                .Select(rt =>
                {
                    var (fn, tree) = rt;
                    var relativePath = fn.Replace(RootPath, "");
                    if (relativePath.StartsWith("/"))
                        relativePath = relativePath.Substring(1);

                    if (withDetail)
                        return new {Tree = relativePath, Contents = tree.ToString()}.ToJson();
                    else
                    {
                        var walker = new DeclarationSyntaxWalker<TypeDeclarationSyntax>();
                        var classes = walker.Visit(tree).Select(c => c.Identifier.ToString());

                        return new {Tree = relativePath, Contents = $"[ {(String.Join(", ", classes))} ]" }.ToJson();
                    }
                })
                .ToList();
            
            Logger.LogInformation(
                "== TREES ==\r\n{TreeOutput}", 
                String.Join(Environment.NewLine, output));
        }

        public void TryRemoveTree(string treeHint)
        {
            var candidates = 
                RawTrees.Keys
                    .Where(x => x.Contains(treeHint, StringComparison.InvariantCultureIgnoreCase))
                    .OrderByDescending(x => x.Replace(".cs","").EndsWith(treeHint, StringComparison.InvariantCultureIgnoreCase))
                    .ThenBy(x => x.Length)
                    .ToList();

            var preferred = candidates.FirstOrDefault();
            
            Logger.LogInformation("Resolved remove tree result: '{ResolvedTree}' for hint '{Hint}'. Candidates: '{@Candidates}'", 
                Path.GetFileName(preferred), treeHint, candidates.Select(Path.GetFileName));

            if (preferred == null)
            {
                Logger.LogWarning("No match for tree hint, nothing to do");
                return;
            }
            
            WithCompilation(c =>
            {
                Logger.LogInformation("Removing syntax tree {TreePath}", preferred);

                var tree = RawTrees[preferred];
                RawTrees.Remove(preferred);

                return c.RemoveSyntaxTrees(tree);
            });
        }

        public string TryResolvePrimaryType(string typeHint)
        {
            var candidates = 
                RawTrees.Values
                    .SelectMany(v => new DeclarationSyntaxWalker<TypeDeclarationSyntax>().Visit(v))
                    .Select(v => v.Identifier.ToString())
                    .Where(x => x?.Contains(typeHint, StringComparison.InvariantCultureIgnoreCase) ?? false)
                    .OrderByDescending(x => x?.EndsWith(typeHint, StringComparison.InvariantCultureIgnoreCase) ?? false)
                    .ThenBy(x => x.Length)
                    .ToList();

            var preferred = candidates.FirstOrDefault();
            
            Logger.LogInformation("Resolved primary type result: {ResolvedPrimaryType} for hint {Hint}. Candidates: {@Candidates}", preferred, typeHint, candidates);

            return preferred;
        }

        public void Dispose()
        {
            _disposing = true;
        }

        string IExposeCommands.Identifier 
            => $"inc-{_identifier}";

        IEnumerable<TbcCommand> IExposeCommands.Commands => new List<TbcCommand>
        {
            new TbcCommand
            {
                Command = "trees",
                Execute = (_, args) =>
                {
                    var detail = false;
                    if (args.Any() && bool.TryParse(args[0], out detail)) ;
                    
                    PrintTrees(detail); 
                    return Task.CompletedTask;
                }
            },

            new TbcCommand
            {
                Command = "tree",
                Execute = (_, args) =>
                {
                    if (!args.Any())
                        Logger.LogWarning("Need subcommand for tree operation");

                    var sub = args[0];

                    switch (sub)
                    {
                        case "remove":
                            var treeHint = args[1];
                            TryRemoveTree(treeHint);
                            break;
                        
                        default:
                            Logger.LogWarning("Don't know how to handle subcommand '{SubCommand}' of tree");
                            break;                            
                    }
                                        
                    return Task.CompletedTask;
                }
            }
        };
    }
}
