using Microsoft.Extensions.Logging;

namespace Tbc.Host.Components.Abstractions
{
    public abstract class ComponentBase { }
    
    public abstract class ComponentBase<T> : GenericComponentBase<T>
    {
        protected ComponentBase(ILogger<T> logger) : base(logger)
        {
        }
    }

}