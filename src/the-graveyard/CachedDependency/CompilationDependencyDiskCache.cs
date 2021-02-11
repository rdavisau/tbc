using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Inject.Protocol;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Tbc.Host.Services.Patch
{
    public class CompilationDependencyDiskCache : ComponentBase<CompilationDependencyDiskCache>, ICompilationDependencyCache
    {
        private readonly string _identifier;
        private readonly IFileSystem _fileSystem;

        public string CachePath { get; set; }

        public CompilationDependencyDiskCache(
            string identifier, IFileSystem fileSystem, ILogger<CompilationDependencyDiskCache> logger) 
            : base(logger)
        {
            _identifier = identifier;
            _fileSystem = fileSystem;

            CachePath = 
                _fileSystem.Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    ".tbc", "asm-cache", $"{_identifier}");

            _fileSystem.Directory.CreateDirectory(CachePath);
        }

        public Task ClearCache()
        {
            _fileSystem.Directory.Delete(CachePath, true);

            return Task.CompletedTask;
        }

        public async Task<List<CachedAssemblyReference>> GetCachedAssemblies()
        {
            return _fileSystem.Directory.GetFiles(CachePath, "*.dll")
                .Select(FromCachedFilePath)
                .ToList();
        }

        public async Task CacheAssembly(AssemblyReference @ref)
        {
            var fn = _fileSystem.Path.GetFileName(@ref.AssemblyLocation);
            var targetPath = _fileSystem.Path.Combine(CachePath, fn);
            var cacheDataPath = targetPath.Replace(".dll", ".json");
            var cacheData = CachedAssemblyReference.FromReference(@ref, targetPath);
            
            await _fileSystem.File.WriteAllBytesAsync(targetPath, @ref.PeBytes.ToByteArray());
            await _fileSystem.File.WriteAllTextAsync(cacheDataPath, JsonConvert.SerializeObject(cacheData));
        }

        private CachedAssemblyReference FromCachedFilePath(string filePath)
        {
            var cacheDataPath = filePath.Replace(".dll", ".json");
            var cacheData = JsonConvert.DeserializeObject<CachedAssemblyReference>(
                _fileSystem.File.ReadAllText(cacheDataPath));

            return cacheData;
        }
    }
}