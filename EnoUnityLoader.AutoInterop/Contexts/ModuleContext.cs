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

    /// <summary>
    /// Creates a module context for a library assembly that shares the GeneratedRuntime with the plugin.
    /// Used for processing library dependencies.
    /// </summary>
    /// <param name="pluginContext">The plugin's module context (provides GeneratedRuntime).</param>
    /// <param name="libraryAssembly">The library assembly to process.</param>
    public ModuleContext(ModuleContext pluginContext, AssemblyDefinition libraryAssembly)
        : base(pluginContext)
    {
        ProcessingAssembly = libraryAssembly;
        ProcessingModule = libraryAssembly.MainModule;
        InteropTypes = new InteropTypesContext(ProcessingModule);
        GeneratedRuntime = pluginContext.GeneratedRuntime; // Share the plugin's GeneratedRuntime
    }
}
