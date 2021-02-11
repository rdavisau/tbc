using Microsoft.Extensions.Logging;

namespace Tbc.Host.Components.Abstractions
{
    public abstract class TransientComponentBase<T> : GenericComponentBase<T>, ITransientComponent
    {
        protected TransientComponentBase(ILogger<T> logger) : base(logger)
        {
        }
    }
}