using EnoUnityLoader.AutoInterop.Contexts;

namespace EnoUnityLoader.AutoInterop.Processors;

/// <summary>
/// Main processor for MonoBehaviour types.
/// Orchestrates all the individual processors for IL2CPP compatibility.
/// </summary>
public sealed class MonoBehaviourProcessor : BaseMonoBehaviourProcessor
{
    public MonoBehaviourProcessor(MonoBehaviourContext context) : base(context)
    {
    }

    public override void Process()
    {
        ProcessUnsupportedIl2CppMembers();
        ProcessDeserialization();
        ProcessAbstractToVirtualConversion();
        ProcessIntPtrConstructor();
        ProcessIl2CppComponentsRegistration();
    }

    private void ProcessIl2CppComponentsRegistration()
    {
        var processor = new Il2CppRegistrationProcessor(Context);
        processor.Process();
    }

    private void ProcessIntPtrConstructor()
    {
        var processor = new IntPtrConstructorProcessor(Context);
        processor.Process();
    }

    private void ProcessAbstractToVirtualConversion()
    {
        var processor = new AbstractToVirtualProcessor(Context);
        processor.Process();
    }

    private void ProcessDeserialization()
    {
        var processor = new SerializationProcessor(Context);
        processor.Process();
    }

    private void ProcessUnsupportedIl2CppMembers()
    {
        var processor = new UnsupportedIl2CppMemberProcessor(Context);
        processor.Process();
    }
}
