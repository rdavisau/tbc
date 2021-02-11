using System;
using Tbc.Host.Components.FileWatcher.Models;

namespace Tbc.Host.Components.FileWatcher
{
    public interface IFileWatcher
    {
        IObservable<ChangedFile> Changes { get; set; }
        string WatchPath { get; }
    }
}