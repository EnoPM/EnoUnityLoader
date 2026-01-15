using EnoUnityLoader.AutoInterop.Common;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Cecil.Utils;

public sealed class LoadableType(
    ILoadOnAccess<TypeDefinition>.LoaderDelegate loader,
    string fullName) : BaseLoadableDefinition<TypeDefinition>(loader, fullName);
