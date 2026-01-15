using System;
using System.Collections;

namespace EnoUnityLoader.Il2Cpp.Utils.Collections;

public class ManagedIl2CppEnumerable : IEnumerable
{
    private readonly Il2CppSystem.Collections.IEnumerable _enumerable;

    public ManagedIl2CppEnumerable(Il2CppSystem.Collections.IEnumerable enumerable)
    {
        this._enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
    }

    public IEnumerator GetEnumerator() => new ManagedIl2CppEnumerator(_enumerable.GetEnumerator());
}
