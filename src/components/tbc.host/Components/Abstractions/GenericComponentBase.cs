using Microsoft.Extensions.Logging;

namespace Tbc.Host.Components.Abstractions
{
    public abstract class GenericComponentBase<T> : ComponentBase
    {
        protected ILogger<T> Logger { get; }
        
        protected GenericComponentBase(ILogger<T> logger)
        {
            Logger = logger;
        }
    }
}