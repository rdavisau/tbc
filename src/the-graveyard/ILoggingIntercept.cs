using System;
using Inject.Protocol;

namespace Tbc.Host
{
    public interface ILoggingIntercept
    {
        IObservable<HostLogMessage> LogMessages { get; }
    }
}