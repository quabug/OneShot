using System;
using System.Collections.Generic;

namespace OneShot;

/// <summary>
/// Interface for generated type metadata, replacing runtime reflection.
/// Source generator emits implementations of this interface for each injectable type.
/// </summary>
public interface ITypeInfo
{
    /// <summary>The concrete type this metadata describes.</summary>
    Type ConcreteType { get; }

    /// <summary>Creates a new instance of the type, resolving constructor parameters from the container.</summary>
    object Create(Container container);

    /// <summary>Injects all [Inject]-marked fields, properties, and methods on the instance.</summary>
    void InjectAll(Container container, object instance);

    /// <summary>All interfaces implemented by the concrete type.</summary>
    IReadOnlyList<Type> Interfaces { get; }

    /// <summary>All base types of the concrete type (excluding Object and ValueType).</summary>
    IReadOnlyList<Type> BaseTypes { get; }
}
