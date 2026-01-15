using System;
using EnoUnityLoader.Attributes;
using EnoUnityLoader.Il2Cpp;
using HarmonyLib;
using UnityEngine;

namespace ExampleMod;

[ModInfos("example.mod", "ExampleMod", "1.0.0")]
public sealed class Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogMessage("Hello World!");
        
        AddComponent<TestBehaviour>();
        
        var harmony = new Harmony("example.mod");
        harmony.PatchAll();
    }
}

public class TestBehaviour : MonoBehaviour
{
    private void Start()
    {
        System.Console.WriteLine("TestBehaviour.Start");
    }
}

[HarmonyPatch(typeof(PlayerControl))]
internal static class Patches
{
    [HarmonyPostfix, HarmonyPatch(nameof(PlayerControl.Awake))]
    private static void Postfix(PlayerControl __instance)
    {
        System.Console.WriteLine("PlayerControl.Awake");
    }
}