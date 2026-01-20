using EnoUnityLoader.AutoInterop.Contexts;

namespace EnoUnityLoader.AutoInterop.Processors;

/// <summary>
/// Processor for MonoBehaviour types in library assemblies (non-plugin dependencies).
/// Applies type modifications but does NOT handle registration (that's done by the main plugin).
/// </summary>
public sealed class LibraryMonoBehaviourProcessor : BaseMonoBehaviourProcessor
{
    public LibraryMonoBehaviourProcessor(MonoBehaviourContext context) : base(context)
    {
    }

    public override void Process()
    {
        ProcessUnsupportedIl2CppMembers();
        ProcessDeserialization();
        ProcessAbstractToVirtualConversion();
        ProcessIntPtrConstructor();
        // Note: Registration is NOT done here - it's handled by the main plugin's GeneratedRuntime
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
