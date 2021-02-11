using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using RxFileSystemWatcher;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.FileWatcher.Models;
using Tbc.Host.Config;

namespace Tbc.Host.Components.FileWatcher
{
    public class FileWatcher : ComponentBase<FileWatcher>, IFileWatcher
    {
        private readonly FileWatchConfig _config;
        private readonly IFileSystem _fileSystem;

        public string WatchPath { get; private set; }
        
        public IObservable<ChangedFile> Changes { get; set; }

        public FileWatcher(FileWatchConfig config, IFileSystem fileSystem, ILogger<FileWatcher> logger) : base(logger)
        {
            _fileSystem = fileSystem;
            _config = config;
            
            Init();
        }

        private void Init()
        {
            Changes = CreateFileSystemWatcher(_config.RootPath, _config.FileMask);
        }
        
        public IObservable<ChangedFile> CreateFileSystemWatcher(string path, string mask)
        {
            WatchPath = path;
                
            if (!_fileSystem.Path.IsPathRooted(WatchPath))
                WatchPath = _fileSystem.Path.Combine(Environment.CurrentDirectory, path);
            
            Logger.LogInformation("Watching files under path {Path}", WatchPath);
            
            var dir = _fileSystem.Path.GetDirectoryName(WatchPath);
            var ofsw = 
                new ObservableFileSystemWatcher(
                    new FileSystemWatcher(dir, mask ?? "*.cs")
                    {
                        EnableRaisingEvents = true, 
                        IncludeSubdirectories = true,
                    });

            Logger.LogInformation("File watcher {FileWatcher} started with config {@Config}", ofsw, _config);

            var ret = 
                Observable
                    .Merge(ofsw.Changed, ofsw.Created, ofsw.Renamed, ofsw.Deleted)
                    .Where(x => !_config.Ignore.Any(i => x.FullPath.Contains((string) i)))
                    .Select(TryGetChangedFile)
                    .Where(x => x != null)
                    .Do(f => Logger.LogInformation("Changed File: {ChangedFile}", f.Path.Substring(WatchPath.Length)));
            
            ofsw.Start();
            
            return ret;
        }

        private ChangedFile TryGetChangedFile(FileSystemEventArgs x)
        {
            try
            {
                return new ChangedFile
                {
                    Path = x.FullPath,
                    Contents = _fileSystem.File.ReadAllText(x.FullPath)
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "An error occurred when attempting to read changed file at '{FilePath}'",
                    x.FullPath);

                return null;
            }
        }
    }
}