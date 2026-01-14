using System.Collections;
using EnoUnityLoader.IL2CPP.Utils.Collections;
using UnityEngine;

namespace EnoUnityLoader.IL2CPP.Utils;

public static class MonoBehaviourExtensions
{
    public static Coroutine StartCoroutine(this MonoBehaviour self, IEnumerator coroutine) =>
        self.StartCoroutine(coroutine.WrapToIl2Cpp());
}
