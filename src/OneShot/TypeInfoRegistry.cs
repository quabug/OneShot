using System;
using System.Collections.Concurrent;

namespace OneShot;

/// <summary>
/// Static registry mapping Type -> ITypeInfo.
/// Source generators register their generated ITypeInfo implementations here via module initializers.
/// </summary>
public static class TypeInfoRegistry
{
    private static readonly ConcurrentDictionary<Type, ITypeInfo> s_registry = new();

    /// <summary>
    /// Registers an ITypeInfo for a given type. Called by generated module initializers.
    /// </summary>
    public static void Register(ITypeInfo typeInfo)
    {
        s_registry[typeInfo.ConcreteType] = typeInfo;
    }

    /// <summary>
    /// Tries to get the ITypeInfo for a given type.
    /// </summary>
    public static bool TryGet(Type type, out ITypeInfo? typeInfo)
    {
        return s_registry.TryGetValue(type, out typeInfo);
    }
}
