# EnoUnityLoader

A modern mod loader for IL2CPP Unity games, inspired by [BepInEx](https://github.com/BepInEx/BepInEx). Built entirely on **.NET 10** for maximum performance and modern language features.

## Features

- **IL2CPP Support**: Full support for IL2CPP Unity games via [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
- **Harmony Patching**: Integrated [HarmonyX](https://github.com/BepInEx/HarmonyX) for runtime method patching
- **Plugin System**: Simple plugin architecture with automatic dependency injection
- **Configuration**: Built-in configuration system for plugins
- **Logging**: Comprehensive logging system
- **.NET 10**: Leverages the latest .NET features and performance improvements

## Installation

1. Download the latest release from the [Releases](https://github.com/EnoPM/EnoUnityLoader/releases) page
2. Extract the `EnoUnityLoader` folder into your game's root directory
3. Place your plugins (`.dll` files) in the `EnoUnityLoader/mods` folder
4. Launch the game

## Creating a Plugin

### Basic Plugin

Create a new .NET 10 class library project and reference `EnoUnityLoader.dll`.

```csharp
using EnoUnityLoader.Attributes;
using EnoUnityLoader.Il2Cpp;
using HarmonyLib;

namespace MyFirstMod;

[ModInfos("com.example.myfirstmod", "My First Mod", "1.0.0")]
public sealed class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogMessage("My First Mod loaded!");

        // Apply Harmony patches
        var harmony = new Harmony("com.example.myfirstmod");
        harmony.PatchAll();
    }
}
```

### Adding MonoBehaviour Components

You can add custom Unity components to the game:

```csharp
using EnoUnityLoader.Attributes;
using EnoUnityLoader.Il2Cpp;
using UnityEngine;

namespace MyMod;

[ModInfos("com.example.mymod", "My Mod", "1.0.0")]
public sealed class Plugin : BasePlugin
{
    public override void Load()
    {
        // Add a custom MonoBehaviour to the scene
        AddComponent<MyCustomBehaviour>();
    }
}

public class MyCustomBehaviour : MonoBehaviour
{
    private void Start()
    {
        Log.LogInfo("MyCustomBehaviour started!");
    }

    private void Update()
    {
        // Your update logic here
    }
}
```

### Harmony Patching

Patch game methods using Harmony:

```csharp
using HarmonyLib;

namespace MyMod;

[HarmonyPatch(typeof(SomeGameClass))]
internal static class SomeGameClassPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SomeGameClass.SomeMethod))]
    private static void SomeMethodPostfix(SomeGameClass __instance)
    {
        // Code here runs after SomeMethod
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(SomeGameClass.AnotherMethod))]
    private static bool AnotherMethodPrefix(SomeGameClass __instance)
    {
        // Return false to skip the original method
        return true;
    }
}
```

### Configuration

Use the built-in configuration system:

```csharp
using EnoUnityLoader.Attributes;
using EnoUnityLoader.Il2Cpp;
using EnoUnityLoader.Configuration;

namespace MyMod;

[ModInfos("com.example.mymod", "My Mod", "1.0.0")]
public sealed class Plugin : BasePlugin
{
    private ConfigEntry<bool> _enableFeature;
    private ConfigEntry<int> _maxItems;

    public override void Load()
    {
        _enableFeature = Config.Bind(
            "General",
            "EnableFeature",
            true,
            "Enable the main feature of this mod"
        );

        _maxItems = Config.Bind(
            "Settings",
            "MaxItems",
            10,
            "Maximum number of items"
        );

        if (_enableFeature.Value)
        {
            Log.LogInfo($"Feature enabled with max items: {_maxItems.Value}");
        }
    }
}
```

## EnoUnityLoader.AutoInterop

AutoInterop is a plugin patcher that automatically processes your MonoBehaviour classes to make them compatible with IL2CPP at runtime.

### What it does

- **Automatic IL2CPP Registration**: Registers your custom MonoBehaviour types with the IL2CPP type system
- **SerializeField Support**: Transforms `[SerializeField]` fields to work with IL2CPP serialization
- **Runtime Infrastructure**: Generates necessary runtime code for proper IL2CPP interop

### Usage

Simply place `EnoUnityLoader.AutoInterop.dll` in the `EnoUnityLoader/patchers/plugins` folder. It will automatically process all plugins that contain MonoBehaviour classes.

### Example with SerializeField

```csharp
using UnityEngine;

public class MyComponent : MonoBehaviour
{
    [SerializeField]
    private float speed = 5.0f;

    [SerializeField]
    private GameObject targetObject;

    private void Update()
    {
        // Use serialized fields normally
        transform.position += Vector3.forward * speed * Time.deltaTime;
    }
}
```

AutoInterop automatically transforms these fields to use IL2CPP-compatible field wrappers at load time.

## Project Structure

```
EnoUnityLoader/
├── core/                    # Core loader DLLs and dependencies
├── patchers/
│   └── plugins/             # Plugin patchers (like AutoInterop)
├── mods/                    # Your plugin DLLs go here
├── config/                  # Plugin configuration files
└── cache/                   # Cached data (interop assemblies, etc.)
```

## Requirements

- **.NET 10 Runtime**: Plugins must target .NET 10
- **IL2CPP Unity Game**: Designed for IL2CPP-compiled Unity games

## Building from Source

```bash
git clone https://github.com/EnoPM/EnoUnityLoader.git
cd EnoUnityLoader
dotnet build -c Release
```

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx) - Original inspiration and architecture
- [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) - IL2CPP interoperability
- [HarmonyX](https://github.com/BepInEx/HarmonyX) - Runtime patching framework
- [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) - IL2CPP analysis

## License

This project is licensed under the LGPL-2.1 license - see the [LICENSE](LICENSE) file for details.
