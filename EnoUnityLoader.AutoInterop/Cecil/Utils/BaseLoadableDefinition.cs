using EnoUnityLoader.AutoInterop.Common;

namespace EnoUnityLoader.AutoInterop.Cecil.Utils;

public abstract class BaseLoadableDefinition<T>(
    ILoadOnAccess<T>.LoaderDelegate loader,
    string fullName) : Loadable<T>(loader)
    where T : notnull
{
    public string FullName { get; } = fullName;
}
