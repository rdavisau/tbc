using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RxFileSystemWatcher;
using Tbc.Host.Components.Abstractions;
using Tbc.Host.Components.CommandProcessor.Models;
using Tbc.Host.Components.FileWatcher.Models;
using Tbc.Host.Config;

namespace Tbc.Host.Components.FileWatcher
{
    public class FileWatcher : ComponentBase<FileWatcher>, IFileWatcher, IExposeCommands
    {
        private readonly FileWatchConfig _config;
        private readonly IFileSystem _fileSystem;

        public string WatchPath { get; private set; }
        public ChangedFile? LastChangedFile { get; private set; }

        private readonly Subject<ChangedFile> _manualWatchFiles = new Subject<ChangedFile>();
        public IObservable<ChangedFile> Changes { get; set; }

        public FileWatcher(FileWatchConfig config, IFileSystem fileSystem, ILogger<FileWatcher> logger) : base(logger)
        {
            _fileSystem = fileSystem;
            _config = config;

            WatchPath = _config.RootPath;
            Changes = Observable.Merge(
                        _manualWatchFiles, // files added to the incremental by a 'watch' command
                        CreateFileSystemWatcher(_config.FileMask)); // files actually changed
        }

        public IObservable<ChangedFile> CreateFileSystemWatcher(string mask)
        {
            if (!_fileSystem.Path.IsPathRooted(WatchPath))
                WatchPath = _fileSystem.Path.Combine(Environment.CurrentDirectory, WatchPath);
            
            Logger.LogInformation("Watching files under path {Path}", WatchPath);

            var dir = WatchPath;
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
                   .Where(x => !_config.Ignore.Any(i => x.FullPath.Contains((string)i)))
                   .Select(x => x.FullPath)
                   .Select(TryGetChangedFile)
                   .Where(x => x != null)
                   .Select(x => (ChangedFile) x!) // in order to return non-null
                   .Do(f => Logger.LogInformation("Changed File: {ChangedFile}", f!.Path.Substring(WatchPath.Length)));

            ofsw.Start();

            return ret;
        }

        private ChangedFile? TryGetChangedFile(string filePath)
        {
            try
            {
                return LastChangedFile = new ChangedFile
                {
                    Path = filePath,
                    Contents = _fileSystem.File.ReadAllText(filePath)
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "An error occurred when attempting to read changed file at '{FilePath}'",
                    filePath);

                return null;
            }
        }

        public string Identifier => $"fw";
        public IEnumerable<TbcCommand> Commands => new[]
        {
            new TbcCommand
            {
                Command = "watch",
                Execute = HandleWatchCommand
            }
        };

        private Task HandleWatchCommand(string cmd, string[] args)
        {
            if (args.Length != 1)
            {
                Logger.LogWarning("Need exactly one argument ('relative path') for command 'watch'");
                return Task.CompletedTask;
            }

            if (LastChangedFile is null)
            {
                Logger.LogWarning("No previously changed file relative to which to watch");
                return Task.CompletedTask;
            }

            var inputPath = args[0];

            var lastPath = LastChangedFile.Path;
            var lastDirectory = Path.GetDirectoryName(lastPath)!;

            var targetPath = Path.Combine(lastDirectory, inputPath);
            var filesInPath =
                _fileSystem.Directory.GetFiles(targetPath, "*.cs", SearchOption.AllDirectories)
                   .Select(x => new FileInfo(x).FullName)
                   .ToList();

            Logger.LogInformation("Watch with target {Target} resolve to {ResolvedPath} and includes files {@Files}",
                inputPath, targetPath, filesInPath);

            foreach (var file in filesInPath)
                if (TryGetChangedFile(file) is {} cf)
                    _manualWatchFiles.OnNext(cf);

            return Task.CompletedTask;
        }
    }
}
