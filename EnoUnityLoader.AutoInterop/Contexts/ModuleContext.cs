using EnoUnityLoader.AutoInterop.Core.Interfaces;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Contexts;

/// <summary>
/// Context for processing a single module.
/// Contains the InteropTypesContext and GeneratedRuntime.
/// </summary>
public class ModuleContext : AutoInteropContext, IModuleProcessorContext
{
    public AssemblyDefinition ProcessingAssembly { get; }
    public ModuleDefinition ProcessingModule { get; }
    public InteropTypesContext InteropTypes { get; }
    public GeneratedRuntime GeneratedRuntime { get; }

    public ModuleContext(AutoInteropContext context, AssemblyDefinition assembly)
        : base(context)
    {
        ProcessingAssembly = assembly;
        ProcessingModule = assembly.MainModule;
        InteropTypes = new InteropTypesContext(ProcessingModule);
        GeneratedRuntime = new GeneratedRuntime(this);
    }

    protected ModuleContext(ModuleContext context)
        : base(context)
    {
        ProcessingAssembly = context.ProcessingAssembly;
        ProcessingModule = context.ProcessingModule;
        InteropTypes = context.InteropTypes;
        GeneratedRuntime = context.GeneratedRuntime;
    }
}
