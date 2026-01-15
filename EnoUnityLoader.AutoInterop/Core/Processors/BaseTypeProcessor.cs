using EnoUnityLoader.AutoInterop.Core.Interfaces;

namespace EnoUnityLoader.AutoInterop.Core.Processors;

public abstract class BaseTypeProcessor<TContext> : BaseProcessor<TContext>
    where TContext : class, ITypeProcessorContext
{
    protected BaseTypeProcessor(TContext context) : base(context)
    {
    }
}
