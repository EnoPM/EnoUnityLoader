using EnoUnityLoader.AutoInterop.Cecil.Interfaces;
using Mono.Cecil;

namespace EnoUnityLoader.AutoInterop.Core;

public sealed class AssemblyResolver : DefaultAssemblyResolver
{
    private readonly IAssemblyLoader _context;

    public AssemblyResolver(IAssemblyLoader context)
    {
        _context = context;

        ResolveFailure += OnResolveFailure;
    }

    private AssemblyDefinition? OnResolveFailure(object sender, AssemblyNameReference reference)
    {
        var result = _context.ResolveAssembly(reference);
        if (result != null)
        {
            RegisterAssembly(result);
        }
        return result;
    }
}
