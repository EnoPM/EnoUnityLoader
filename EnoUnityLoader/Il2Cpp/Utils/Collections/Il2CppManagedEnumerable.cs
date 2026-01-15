using System.Collections;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem;
using ArgumentNullException = System.ArgumentNullException;
using IEnumerator = Il2CppSystem.Collections.IEnumerator;
using IntPtr = System.IntPtr;

namespace EnoUnityLoader.Il2Cpp.Utils.Collections;

public class Il2CppManagedEnumerable : Object
{
    private readonly IEnumerable? _enumerable;

    static Il2CppManagedEnumerable()
    {
        ClassInjector.RegisterTypeInIl2Cpp<Il2CppManagedEnumerable>(new RegisterTypeOptions
        {
            Interfaces = new[] { typeof(Il2CppSystem.Collections.IEnumerable) }
        });
    }

    public Il2CppManagedEnumerable(IntPtr ptr) : base(ptr) { }

    public Il2CppManagedEnumerable(IEnumerable enumerable)
        : base(ClassInjector.DerivedConstructorPointer<Il2CppManagedEnumerable>())
    {
        this._enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        ClassInjector.DerivedConstructorBody(this);
    }

    public IEnumerator? GetEnumerator() =>
        _enumerable is null ? null : new Il2CppManagedEnumerator(_enumerable.GetEnumerator()).Cast<IEnumerator>();
}
