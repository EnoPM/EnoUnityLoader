using EnoUnityLoader.AutoInterop.Core.Interfaces;

namespace EnoUnityLoader.AutoInterop.Core.Processors;

public abstract class BaseProcessor<TContext> : IProcessor
    where TContext : class, IContext
{
    protected TContext Context { get; }

    protected BaseProcessor(TContext context)
    {
        Context = context;
    }

    public abstract void Process();
}
