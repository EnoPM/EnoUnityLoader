using EnoUnityLoader.AutoInterop.Contexts;
using EnoUnityLoader.AutoInterop.Core.Processors;

namespace EnoUnityLoader.AutoInterop.Processors;

public abstract class BaseMonoBehaviourProcessor<TContext> : BaseTypeProcessor<TContext>
    where TContext : MonoBehaviourContext
{
    protected BaseMonoBehaviourProcessor(TContext context) : base(context)
    {
    }
}

public abstract class BaseMonoBehaviourProcessor : BaseMonoBehaviourProcessor<MonoBehaviourContext>
{
    protected BaseMonoBehaviourProcessor(MonoBehaviourContext context) : base(context)
    {
    }
}
