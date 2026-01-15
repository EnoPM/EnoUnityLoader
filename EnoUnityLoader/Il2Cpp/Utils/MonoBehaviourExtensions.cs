using System.Collections;
using EnoUnityLoader.Il2Cpp.Utils.Collections;
using UnityEngine;

namespace EnoUnityLoader.Il2Cpp.Utils;

public static class MonoBehaviourExtensions
{
    public static Coroutine StartCoroutine(this MonoBehaviour self, IEnumerator coroutine) =>
        self.StartCoroutine(coroutine.WrapToIl2Cpp());
}
