using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace EnoUnityLoader.Il2Cpp.Utils;

internal static class Il2CppUtils
{
    // TODO: Check if we can safely initialize this in Chainloader instead
    private static GameObject? _managerGo;

    public static Il2CppObjectBase AddComponent(Type t)
    {
        if (_managerGo == null)
            _managerGo = new GameObject { hideFlags = HideFlags.HideAndDontSave, name = "ModLoader_Manager" };

        if (!ClassInjector.IsTypeRegisteredInIl2Cpp(t))
            ClassInjector.RegisterTypeInIl2Cpp(t);

        return _managerGo.AddComponent(Il2CppType.From(t));
    }
}
