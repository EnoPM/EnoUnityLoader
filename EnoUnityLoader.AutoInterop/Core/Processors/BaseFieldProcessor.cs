using EnoUnityLoader.AutoInterop.Core.Interfaces;

namespace EnoUnityLoader.AutoInterop.Core.Processors;

public abstract class BaseFieldProcessor<TContext> : BaseProcessor<TContext>
    where TContext : class, IFieldProcessorContext
{
    protected BaseFieldProcessor(TContext context) : base(context)
    {
    }
}
