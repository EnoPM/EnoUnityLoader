using EnoUnityLoader.AutoInterop.Common;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Utils;

public sealed class LoadableMethod(
    ILoadOnAccess<MethodDefinition>.LoaderDelegate loader,
    string fullName) : BaseLoadableDefinition<MethodDefinition>(loader, fullName);
