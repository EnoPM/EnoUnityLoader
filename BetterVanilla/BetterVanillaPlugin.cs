using BetterVanilla.Components;
using BetterVanilla.Core;
using EnoUnityLoader.Attributes;
using EnoUnityLoader.Il2Cpp;

namespace BetterVanilla;

[ModInfos(GeneratedProps.Guid, GeneratedProps.Name, GeneratedProps.Version)]
internal sealed class BetterVanillaPlugin : BasePlugin
{
    public override void Load()
    {
        Ls.SetLogSource(Log);
        AddComponent<UnityThreadDispatcher>();
        AddComponent<FeatureCodeBehaviour>();
        AddComponent<BetterVanillaManager>();
        AddComponent<ModUpdaterBehaviour>();
        AddComponent<PlayerShieldBehaviour>();
    }
}