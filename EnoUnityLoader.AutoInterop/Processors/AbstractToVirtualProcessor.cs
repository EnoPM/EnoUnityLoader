using EnoUnityLoader.AutoInterop.Contexts;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace EnoUnityLoader.AutoInterop.Processors;

/// <summary>
/// Processor that converts abstract methods to virtual methods.
/// IL2CPP requires virtual methods instead of abstract for injected types.
/// </summary>
public class AbstractToVirtualProcessor : BaseMonoBehaviourProcessor
{
    public AbstractToVirtualProcessor(MonoBehaviourContext context) : base(context)
    {
    }

    public override void Process()
    {
        foreach (var method in Context.ProcessingType.Methods)
        {
            if (!method.IsAbstract) continue;
            Process(method);
        }
    }

    private void Process(MethodDefinition method)
    {
        method.IsAbstract = false;
        method.IsVirtual = true;

        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Newobj, Context.ProcessingModule.ImportReference(Context.InteropTypes.NotImplementedExceptionConstructor.Value));
        il.Emit(OpCodes.Throw);
    }
}
