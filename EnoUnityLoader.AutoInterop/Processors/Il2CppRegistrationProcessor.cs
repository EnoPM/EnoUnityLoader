using System.Linq;
using EnoUnityLoader.AutoInterop.Common;
using EnoUnityLoader.AutoInterop.Contexts;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace EnoUnityLoader.AutoInterop.Processors;

/// <summary>
/// Processor that registers MonoBehaviour types with IL2CPP runtime.
/// Injects calls to ClassInjector.RegisterTypeInIl2Cpp in the plugin entry point.
/// </summary>
public class Il2CppRegistrationProcessor : BaseMonoBehaviourProcessor
{
    public Il2CppRegistrationProcessor(MonoBehaviourContext context) : base(context)
    {
    }

    public override void Process()
    {
        var loader = Context.GeneratedRuntime.ComponentRegistererMethod.Value;
        var il = loader.Body.GetILProcessor();

        var ret = il.Body.Instructions.First(x => x.OpCode == OpCodes.Ret);

        var registererType = GetBaseRegisterer();
        var registererMethod = new GenericInstanceMethod(Context.ProcessingModule.ImportReference(registererType.Value));
        registererMethod.GenericArguments.Add(Context.ProcessingType);

        il.InsertBefore(ret, il.Create(OpCodes.Call, registererMethod));
    }

    private Loadable<MethodDefinition> GetBaseRegisterer()
    {
        if (!Context.UseUnitySerializationInterface ||
            Context.InteropSummary.SerializedMonoBehaviourFullNames.Count == 0 ||
            !Context.InteropSummary.SerializedMonoBehaviourFullNames.Contains(Context.ProcessingType.FullName))
        {
            return Context.GeneratedRuntime.SimpleComponentRegisterer;
        }

        return Context.GeneratedRuntime.InterfaceComponentRegisterer;
    }
}
