using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Inject.Protocol;
using Microsoft.Extensions.Logging;

namespace Tbc.Host
{
    public class LoggingIntercept : ILogger, ILoggerProvider, ILoggingIntercept
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        private readonly Subject<HostLogMessage> _logMessages = new Subject<HostLogMessage>();
        public IObservable<HostLogMessage> LogMessages => _logMessages.AsObservable();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            var hostLogMessage = new HostLogMessage
            {
                LogLevel = $"{logLevel}",
                Message = message
            };
            
            _logMessages.OnNext(hostLogMessage);
        }

        public bool IsEnabled(LogLevel logLevel) 
            => logLevel >= LogLevel;

        public IDisposable BeginScope<TState>(TState state) 
            => new BooleanDisposable();

        public ILogger CreateLogger(string categoryName) 
            => this;

        public void Dispose() { }
    }
}