using System;
using System.Linq;
using EnoUnityLoader.AutoInterop.Cecil.Extensions;
using EnoUnityLoader.AutoInterop.Cecil.Utils;
using EnoUnityLoader.AutoInterop.Common;
using EnoUnityLoader.AutoInterop.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace EnoUnityLoader.AutoInterop.Contexts;

/// <summary>
/// Manages the generated runtime infrastructure for IL2CPP component registration.
/// Creates static helper methods for registering MonoBehaviour types with IL2CPP.
/// </summary>
public sealed class GeneratedRuntime : BaseRuntimeManager
{
    private readonly ModuleContext _context;
    private readonly Loadable<MethodDefinition> _pluginEntryPoint;
    private readonly Loadable<TypeDefinition> _componentRegistererType;
    private readonly Loadable<FieldDefinition> _isLastPassField;
    private bool _entryPointInjected;
    public Loadable<MethodDefinition> ComponentRegistererMethod { get; }

    public Loadable<MethodDefinition> SimpleComponentRegisterer { get; }
    public Loadable<MethodDefinition> InterfaceComponentRegisterer { get; }
    public Loadable<MethodDefinition> RegisterAllTypesPassMethod { get; }

    public GeneratedRuntime(ModuleContext context) : base(context.ProcessingModule.Name)
    {
        _context = context;

        _pluginEntryPoint = new Loadable<MethodDefinition>(FindEntryPoint);

        _componentRegistererType = new Loadable<TypeDefinition>(CreateComponentRegistererType);
        _isLastPassField = new Loadable<FieldDefinition>(CreateIsLastPassField);
        SimpleComponentRegisterer = new Loadable<MethodDefinition>(CreateSimpleComponentRegisterer);
        InterfaceComponentRegisterer = new Loadable<MethodDefinition>(CreateInterfaceComponentRegisterer);
        RegisterAllTypesPassMethod = new Loadable<MethodDefinition>(CreateRegisterAllTypesPassMethod);
        ComponentRegistererMethod = new Loadable<MethodDefinition>(CreateComponentRegistererMethod);
    }

    /// <summary>
    /// Generates the runtime infrastructure (type and registration methods) if they haven't been created yet.
    /// Injects the call to RegisterCurrentPlugin at the top of the plugin entry point.
    /// </summary>
    public void GenerateRuntimeInfrastructure()
    {
        if (_entryPointInjected)
            return;

        // Force loading of all infrastructure components
        _componentRegistererType.Load();
        _isLastPassField.Load();
        SimpleComponentRegisterer.Load();
        InterfaceComponentRegisterer.Load();
        RegisterAllTypesPassMethod.Load();
        ComponentRegistererMethod.Load();

        // Inject the call to RegisterCurrentPlugin at the top of the plugin entry point
        // This must be done after all component registrations have been added
        CallInTopOfEntryPoint(ComponentRegistererMethod.Value);
        _entryPointInjected = true;
    }

    private MethodDefinition CreateComponentRegistererMethod()
    {
        var method = new MethodDefinition(
            "RegisterCurrentPlugin",
            MethodAttributes.Static | MethodAttributes.Assembly,
            _context.ProcessingModule.TypeSystem.Void
        );

        _componentRegistererType.Value.Methods.Add(method);

        var il = method.Body.GetILProcessor();

        // _isLastPass = false;
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stsfld, _isLastPassField.Value);

        // RegisterAllTypesPass(); (first pass - errors silently ignored)
        il.Emit(OpCodes.Call, RegisterAllTypesPassMethod.Value);

        // _isLastPass = true;
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stsfld, _isLastPassField.Value);

        // RegisterAllTypesPass(); (second pass - errors thrown)
        il.Emit(OpCodes.Call, RegisterAllTypesPassMethod.Value);

        il.Emit(OpCodes.Ret);

        return method;
    }

    private MethodDefinition CreateInterfaceComponentRegisterer()
    {
        var method = new MethodDefinition(
            "RegisterSerializedComponent",
            MethodAttributes.Static | MethodAttributes.Private,
            _context.ProcessingModule.TypeSystem.Void
        );

        var genericParameter = new GenericParameter("T", method)
        {
            Attributes = GenericParameterAttributes.NonVariant
        };
        var monoBehaviourType = _context.ProcessingModule.ImportReference(_context.InteropTypes.MonoBehaviour.Value);
        genericParameter.Constraints.Add(new GenericParameterConstraint(monoBehaviourType));
        method.GenericParameters.Add(genericParameter);

        var registerer = new GenericInstanceMethod(
            _context.ProcessingModule.ImportReference(_context.InteropTypes.RegisterTypeInIl2CppWithOptionsMethod.Value));
        registerer.GenericArguments.Add(genericParameter);

        // IsTypeRegisteredInIl2Cpp<T>() check
        var isRegistered = new GenericInstanceMethod(
            _context.ProcessingModule.ImportReference(_context.InteropTypes.IsTypeRegisteredInIl2CppMethod.Value));
        isRegistered.GenericArguments.Add(genericParameter);

        var registererOptionsConstructor = _context.ProcessingModule.ImportReference(
            _context.InteropTypes.ClassInjectorRegisterOptionsConstructor.Value);
        var registerOptionsSetInterfaceMethod = _context.ProcessingModule.ImportReference(
            _context.InteropTypes.Il2CppInterfaceCollectionSetInterfaceMethod.Value);
        var systemType = _context.ProcessingModule.ImportReference(_context.InteropTypes.SystemType.Value);
        var serializationInterface = _context.ProcessingModule.ImportReference(
            _context.InteropTypes.SerializationCallbackReceiverInterface.Value);
        var getTypeHandle = _context.ProcessingModule.ImportReference(
            _context.InteropTypes.GetSystemTypeFromHandleMethod.Value);
        var interfaceCollectionConstructor = _context.ProcessingModule.ImportReference(
            _context.InteropTypes.Il2CppInterfaceCollectionConstructor.Value);

        // Import required types for exception handling
        var exceptionType = _context.ProcessingModule.ImportReference(_context.InteropTypes.SystemException.Value);
        var typeGetFullName = _context.ProcessingModule.ImportReference(_context.InteropTypes.TypeGetFullNameMethod.Value);
        var stringConcat = _context.ProcessingModule.ImportReference(_context.InteropTypes.StringConcatMethod.Value);
        var invalidOpExCtor = _context.ProcessingModule.ImportReference(_context.InteropTypes.InvalidOperationExceptionConstructor.Value);

        var var0 = new VariableDefinition(
            _context.ProcessingModule.ImportReference(registererOptionsConstructor.DeclaringType));
        method.Body.Variables.Add(var0);

        // Add local variable for caught exception
        var exceptionVar = new VariableDefinition(exceptionType);
        method.Body.Variables.Add(exceptionVar);
        method.Body.InitLocals = true;

        var il = method.Body.GetILProcessor();

        // Create key instructions
        var ret = il.Create(OpCodes.Ret);
        var tryStart = il.Create(OpCodes.Newobj, registererOptionsConstructor);
        var catchStart = il.Create(OpCodes.Stloc, exceptionVar);
        var throwBlock = il.Create(OpCodes.Ldstr, "Failed to register type: ");

        // if (ClassInjector.IsTypeRegisteredInIl2Cpp<T>()) return;
        il.Append(il.Create(OpCodes.Call, isRegistered));
        il.Append(il.Create(OpCodes.Brtrue_S, ret));

        // try block: setup options and call registerer
        il.Append(tryStart);
        il.Append(il.Create(OpCodes.Stloc_0));

        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Ldc_I4_1));
        il.Append(il.Create(OpCodes.Newarr, systemType));
        il.Append(il.Create(OpCodes.Dup));
        il.Append(il.Create(OpCodes.Ldc_I4_0));
        il.Append(il.Create(OpCodes.Ldtoken, serializationInterface));
        il.Append(il.Create(OpCodes.Call, getTypeHandle));
        il.Append(il.Create(OpCodes.Stelem_Ref));

        il.Append(il.Create(OpCodes.Newobj, interfaceCollectionConstructor));

        il.Append(il.Create(OpCodes.Callvirt, registerOptionsSetInterfaceMethod));

        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Call, registerer));
        il.Append(il.Create(OpCodes.Leave_S, ret));

        // catch (Exception ex) {
        il.Append(catchStart);
        //   if (_isLastPass) throw new InvalidOperationException(...);
        il.Append(il.Create(OpCodes.Ldsfld, _isLastPassField.Value));
        il.Append(il.Create(OpCodes.Brtrue_S, throwBlock));
        //   else return; (silently ignore on first pass)
        il.Append(il.Create(OpCodes.Leave_S, ret));

        // throw block
        il.Append(throwBlock);
        il.Append(il.Create(OpCodes.Ldtoken, genericParameter));
        il.Append(il.Create(OpCodes.Call, getTypeHandle));
        il.Append(il.Create(OpCodes.Callvirt, typeGetFullName));
        il.Append(il.Create(OpCodes.Call, stringConcat));
        il.Append(il.Create(OpCodes.Ldloc, exceptionVar));
        il.Append(il.Create(OpCodes.Newobj, invalidOpExCtor));
        il.Append(il.Create(OpCodes.Throw));

        il.Append(ret);

        // Add exception handler
        var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = tryStart,
            TryEnd = catchStart,
            HandlerStart = catchStart,
            HandlerEnd = ret,
            CatchType = exceptionType
        };
        method.Body.ExceptionHandlers.Add(handler);

        _componentRegistererType.Value.Methods.Add(method);

        return method;
    }

    private MethodDefinition CreateSimpleComponentRegisterer()
    {
        var method = new MethodDefinition(
            "RegisterComponent",
            MethodAttributes.Static | MethodAttributes.Private,
            _context.ProcessingModule.TypeSystem.Void
        );

        var genericParameter = new GenericParameter("T", method)
        {
            Attributes = GenericParameterAttributes.NonVariant
        };
        var monoBehaviourType = _context.ProcessingModule.ImportReference(_context.InteropTypes.MonoBehaviour.Value);
        genericParameter.Constraints.Add(new GenericParameterConstraint(monoBehaviourType));
        method.GenericParameters.Add(genericParameter);

        var registerer = new GenericInstanceMethod(
            _context.ProcessingModule.ImportReference(_context.InteropTypes.SimpleRegisterTypeInIl2CppMethod.Value));
        registerer.GenericArguments.Add(genericParameter);

        // IsTypeRegisteredInIl2Cpp<T>() check
        var isRegistered = new GenericInstanceMethod(
            _context.ProcessingModule.ImportReference(_context.InteropTypes.IsTypeRegisteredInIl2CppMethod.Value));
        isRegistered.GenericArguments.Add(genericParameter);

        // Import required types for exception handling
        var exceptionType = _context.ProcessingModule.ImportReference(_context.InteropTypes.SystemException.Value);
        var getTypeFromHandle = _context.ProcessingModule.ImportReference(_context.InteropTypes.GetSystemTypeFromHandleMethod.Value);
        var typeGetFullName = _context.ProcessingModule.ImportReference(_context.InteropTypes.TypeGetFullNameMethod.Value);
        var stringConcat = _context.ProcessingModule.ImportReference(_context.InteropTypes.StringConcatMethod.Value);
        var invalidOpExCtor = _context.ProcessingModule.ImportReference(_context.InteropTypes.InvalidOperationExceptionConstructor.Value);

        // Add local variable for caught exception
        var exceptionVar = new VariableDefinition(exceptionType);
        method.Body.Variables.Add(exceptionVar);
        method.Body.InitLocals = true;

        var il = method.Body.GetILProcessor();

        // Create key instructions
        var ret = il.Create(OpCodes.Ret);
        var tryStart = il.Create(OpCodes.Call, registerer);
        var catchStart = il.Create(OpCodes.Stloc, exceptionVar);
        var throwBlock = il.Create(OpCodes.Ldstr, "Failed to register type: ");

        // if (ClassInjector.IsTypeRegisteredInIl2Cpp<T>()) return;
        il.Append(il.Create(OpCodes.Call, isRegistered));
        il.Append(il.Create(OpCodes.Brtrue_S, ret));

        // try { ClassInjector.RegisterTypeInIl2Cpp<T>(); return; }
        il.Append(tryStart);
        il.Append(il.Create(OpCodes.Leave_S, ret));

        // catch (Exception ex) {
        il.Append(catchStart);
        //   if (_isLastPass) throw new InvalidOperationException(...);
        il.Append(il.Create(OpCodes.Ldsfld, _isLastPassField.Value));
        il.Append(il.Create(OpCodes.Brtrue_S, throwBlock));
        //   else return; (silently ignore on first pass)
        il.Append(il.Create(OpCodes.Leave_S, ret));

        // throw block
        il.Append(throwBlock);
        il.Append(il.Create(OpCodes.Ldtoken, genericParameter));
        il.Append(il.Create(OpCodes.Call, getTypeFromHandle));
        il.Append(il.Create(OpCodes.Callvirt, typeGetFullName));
        il.Append(il.Create(OpCodes.Call, stringConcat));
        il.Append(il.Create(OpCodes.Ldloc, exceptionVar));
        il.Append(il.Create(OpCodes.Newobj, invalidOpExCtor));
        il.Append(il.Create(OpCodes.Throw));

        il.Append(ret);

        // Add exception handler
        var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = tryStart,
            TryEnd = catchStart,
            HandlerStart = catchStart,
            HandlerEnd = ret,
            CatchType = exceptionType
        };
        method.Body.ExceptionHandlers.Add(handler);

        _componentRegistererType.Value.Methods.Add(method);

        return method;
    }

    /// <summary>
    /// Registers an external type (from a library) in the plugin's RegisterAllTypesPass method.
    /// </summary>
    public void RegisterExternalType(TypeReference typeReference, bool useSerializationInterface)
    {
        var passMethod = RegisterAllTypesPassMethod.Value;
        var il = passMethod.Body.GetILProcessor();
        var ret = il.Body.Instructions.First(x => x.OpCode == OpCodes.Ret);

        var registererMethod = useSerializationInterface
            ? InterfaceComponentRegisterer.Value
            : SimpleComponentRegisterer.Value;

        var genericMethod = new GenericInstanceMethod(_context.ProcessingModule.ImportReference(registererMethod));
        genericMethod.GenericArguments.Add(_context.ProcessingModule.ImportReference(typeReference));

        il.InsertBefore(ret, il.Create(OpCodes.Call, genericMethod));
    }

    private void CallInTopOfEntryPoint(MethodReference methodToCall)
    {
        var il = _pluginEntryPoint.Value.Body.GetILProcessor();
        il.Prepend(il.Create(OpCodes.Call, methodToCall));
    }

    private TypeDefinition CreateComponentRegistererType()
    {
        var type = new TypeDefinition(
            BaseRuntimeNamespace,
            "ComponentRegisterer",
            TypeAttributesUtility.Internal | TypeAttributesUtility.Static | TypeAttributes.Class,
            _context.ProcessingModule.TypeSystem.Object
        );

        _context.ProcessingModule.Types.Add(type);

        return type;
    }

    private FieldDefinition CreateIsLastPassField()
    {
        var field = new FieldDefinition(
            "_isLastPass",
            FieldAttributes.Private | FieldAttributes.Static,
            _context.ProcessingModule.TypeSystem.Boolean
        );

        _componentRegistererType.Value.Fields.Add(field);

        return field;
    }

    private MethodDefinition CreateRegisterAllTypesPassMethod()
    {
        var method = new MethodDefinition(
            "RegisterAllTypesPass",
            MethodAttributes.Static | MethodAttributes.Private,
            _context.ProcessingModule.TypeSystem.Void
        );

        _componentRegistererType.Value.Methods.Add(method);

        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ret);

        return method;
    }

    private MethodDefinition FindEntryPoint()
    {
        var entryPoint = GetPluginEntryPointMethod(_context.ProcessingModule, _context.InteropTypes);

        if (entryPoint == null)
        {
            throw new Exception(
                $"{_context.ProcessingModule.Name} doesn't contain a EnoUnityLoader plugin entry point");
        }

        return entryPoint;
    }

    private static MethodDefinition? GetPluginEntryPointMethod(ModuleDefinition module, InteropTypesContext types)
    {
        var type = module.GetAllTypes()
            .FirstOrDefault(x => x.IsAssignableTo(types.BasePlugin));

        var method = type?.Methods
            .FirstOrDefault(x => x is { IsVirtual: true, IsReuseSlot: true, Name: "Load" });

        return method;
    }
}
