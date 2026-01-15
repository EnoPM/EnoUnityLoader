using System.Collections.Generic;
using System.Linq;
using EnoUnityLoader.Attributes;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Contexts;
using EnoUnityLoader.AutoInterop.Processors;
using EnoUnityLoader.AutoInterop.Utils;
using EnoUnityLoader.Il2Cpp;
using EnoUnityLoader.PluginPatching;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop;

[PluginPatcherInfo("pm.eno.interop.auto", "AutoInterop", "1.0.0")]
public sealed class AutoInterop : BasePluginPatcher
{
    internal static AutoInterop Instance { get; private set; } = null!;

    public AutoInterop()
    {
        Instance = this;
    }

    public override bool ShouldPatch(ModInfosAttribute modInfo, AssemblyDefinition assembly)
    {
        return assembly.MainModule.Types
            .Any(x => x.IsAssignableTo<BasePlugin>());
    }

    public override void Patch(ModInfosAttribute modInfo, AssemblyDefinition assembly)
    {
        Log.LogMessage($"Processing AutoInterop to '{modInfo.Guid}' v{modInfo.Version?.ToString() ?? "0.0.0"}...");

        // Create the context for this assembly - use assembly's own resolver
        var assemblyFilePaths = new HashSet<string> { assembly.MainModule.FileName };
        var context = new AutoInteropContext(assemblyFilePaths, Log, useUnitySerializationInterface: false);

        // Create module context
        var moduleContext = new ModuleContext(context, assembly);

        // Find MonoBehaviour types in this assembly
        var monoBehaviourTypes = UnityUtility.GetMonoBehaviourTypes(
            assembly.MainModule,
            moduleContext.InteropTypes);

        if (monoBehaviourTypes.Count == 0)
        {
            Log.LogDebug($"No MonoBehaviour types found in '{modInfo.Name}'.");
            return;
        }

        Log.LogInfo($"Found {monoBehaviourTypes.Count} MonoBehaviour types in '{modInfo.Name}'.");

        // Process each MonoBehaviour type
        foreach (var monoBehaviourType in monoBehaviourTypes)
        {
            var monoBehaviourContext = new MonoBehaviourContext(moduleContext, monoBehaviourType);
            var processor = new MonoBehaviourProcessor(monoBehaviourContext);
            processor.Process();

            Log.LogDebug($"Processed MonoBehaviour: {monoBehaviourType.FullName}");
        }

        // Generate runtime infrastructure if needed
        if (context.InteropSummary.RegisteredMonoBehaviourFullNames.Count > 0)
        {
            moduleContext.GeneratedRuntime.GenerateRuntimeInfrastructure();
            Log.LogInfo($"Generated runtime infrastructure for {context.InteropSummary.RegisteredMonoBehaviourFullNames.Count} registered types.");
        }

        Log.LogMessage($"AutoInterop completed for '{modInfo.Name}'.");
    }
}
