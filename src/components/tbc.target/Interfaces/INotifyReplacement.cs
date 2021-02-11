using System;

namespace Tbc.Target.Interfaces
{
    public interface INotifyReplacement
    {
        Action<IReloadManager> NotifyReplacement { get; set; }
    }
}