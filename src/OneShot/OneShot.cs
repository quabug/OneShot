// MIT License

// Copyright (c) 2023 quabug
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OneShot;

/// <summary>
/// Provides type metadata for source-generated dependency injection, replacing runtime reflection.
/// The source generator emits an implementation of this interface for each injectable type.
/// </summary>
public interface ITypeInfo
{
    /// <summary>The concrete type this metadata describes.</summary>
    Type ConcreteType { get; }

    /// <summary>Creates a new instance of the type, resolving constructor parameters from the container.</summary>
    object Create(Container container);

    /// <summary>Injects all <see cref="InjectAttribute"/>-marked fields, properties, and methods on the instance.</summary>
    void InjectAll(Container container, object instance);

    /// <summary>All interfaces implemented by the concrete type.</summary>
    IReadOnlyList<Type> Interfaces { get; }

    /// <summary>All base types of the concrete type (excluding <see cref="object"/> and <see cref="ValueType"/>).</summary>
    IReadOnlyList<Type> BaseTypes { get; }
}

/// <summary>
/// Global registry mapping <see cref="Type"/> to <see cref="ITypeInfo"/>.
/// Source-generated module initializers populate this registry at startup.
/// </summary>
public static class TypeInfoRegistry
{
    private static readonly ConcurrentDictionary<Type, ITypeInfo> s_registry = new();

    /// <summary>
    /// Registers an <see cref="ITypeInfo"/> for its concrete type. Called by generated module initializers.
    /// </summary>
    public static void Register(ITypeInfo typeInfo)
    {
        s_registry[typeInfo.ConcreteType] = typeInfo;
    }

    /// <summary>
    /// Attempts to retrieve the <see cref="ITypeInfo"/> for <paramref name="type"/>.
    /// </summary>
    public static bool TryGet(Type type, out ITypeInfo? typeInfo)
    {
        return s_registry.TryGetValue(type, out typeInfo);
    }
}

/// <summary>
/// Pairs a factory function with its associated <see cref="ResolverLifetime"/>.
/// </summary>
public readonly record struct Resolver
{
    public Func<Container, Type, object> Func { get; }
    public ResolverLifetime Lifetime { get; }
    public bool IsValid => Func != null;

    public Resolver(Func<Container, Type, object> func, ResolverLifetime lifetime)
    {
        Func = func;
        Lifetime = lifetime;
    }
}

/// <summary>
/// A lightweight dependency injection container supporting hierarchical scopes,
/// thread-safe registration, and automatic disposal management.
/// </summary>
public sealed class Container : IDisposable
{
    internal Container? Parent { get; private set; }
    internal ConcurrentDictionary<Type, ConcurrentStack<Resolver>> Resolvers { get; private set; } = new();
    private ConcurrentStack<IDisposable> _disposableInstances = new();
    private ConcurrentStack<Container> _children = new();

    /// <summary>
    /// Enables or disables circular dependency checking during type resolution.
    /// Disabling improves performance but may cause stack overflow on circular dependencies.
    /// Default: true in DEBUG/UNITY_EDITOR/DEVELOPMENT_BUILD, false in release builds.
    /// </summary>
    public bool EnableCircularCheck { get; set; }
#if DEBUG
        = true;
#endif

    /// <summary>
    /// Prevents registration of disposable types as transient to avoid memory leaks.
    /// When enabled, throws an exception if attempting to register IDisposable as transient.
    /// See: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines
    /// </summary>
    public bool PreventDisposableTransient { get; set; }

    #region creation

    /// <summary>
    /// Creates a new root container with default settings.
    /// </summary>
    public Container() { }

    /// <summary>
    /// Creates a child container that inherits settings from the parent.
    /// </summary>
    /// <param name="parent">The parent container to inherit from.</param>
    public Container(Container parent)
    {
        Parent = parent;
        EnableCircularCheck = parent.EnableCircularCheck;
        PreventDisposableTransient = parent.PreventDisposableTransient;
        parent._children.Push(this);
    }

    /// <summary>
    /// Creates a child container with shared type registrations.
    /// Child containers can override parent registrations.
    /// </summary>
    /// <returns>A new child container.</returns>
    public Container CreateChildContainer()
    {
        return new Container(this);
    }

    /// <summary>
    /// Creates a scoped container for lifetime management.
    /// Scoped instances are shared within the scope but not with parent/siblings.
    /// </summary>
    /// <returns>A new scoped container.</returns>
    public Container BeginScope()
    {
        return CreateChildContainer();
    }

    #endregion

    /// <summary>
    /// Disposes the container, all child containers, and tracked disposable instances.
    /// </summary>
    public void Dispose()
    {
        if (_children != null!) while (_children.TryPop(out var child)) child.Dispose();
        _children = null!;
        if (_disposableInstances != null!) while (_disposableInstances.TryPop(out var instance)) instance.Dispose();
        _disposableInstances = null!;
        Parent = null;
        Resolvers?.Clear();
        Resolvers = null!;
    }

    #region Resolve

    /// <summary>
    /// Resolves an instance of the specified type from the container hierarchy.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>The resolved instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the type is not registered.</exception>
    public object Resolve(Type type, Type? label = null)
    {
        var instance = TryResolve(type, label);
        if (instance == null) throw new ArgumentException($"{type.Name} have not been registered yet");
        return instance;
    }

    /// <summary>
    /// Resolves an instance of type T from the container hierarchy.
    /// </summary>
    /// <typeparam name="T">The type to resolve.</typeparam>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>The resolved instance of type T.</returns>
    public T Resolve<T>(Type? label = null)
    {
        return (T)Resolve(typeof(T), label);
    }

    /// <summary>
    /// Attempts to resolve an instance of type T, returning null if not registered.
    /// </summary>
    /// <typeparam name="T">The type to resolve.</typeparam>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>The resolved instance or null if not found.</returns>
    public object? TryResolve<T>(Type? label = null)
    {
        return TryResolve(typeof(T), label);
    }

    /// <summary>
    /// Attempts to resolve an instance of the specified type, returning null if not registered.
    /// Supports array resolution and generic type definitions.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>The resolved instance or null if not found.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Dynamic code paths (CreateLabelType, Array.CreateInstance) only fire for label-typed or array-typed resolves; callers opting into those features accept the AOT requirement.")]
    public object? TryResolve(Type type, Type? label)
    {
        {
            var creatorKey = label == null ? type : label.CreateLabelType(type);
            var creator = FindFirstCreatorInHierarchy(creatorKey);
            if (creator.IsValid) return creator.Func(this, type);
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var arrayArgument = ResolveGroup(elementType);
            var source = arrayArgument.ToArray();
            if (source.Length > 0)
            {
                var dest = Array.CreateInstance(elementType, source.Length);
                Array.Copy(source, dest, source.Length);
                return dest;
            }
        }

        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            var creatorKey = label == null ? generic : label.CreateLabelType(generic);
            var creator = FindFirstCreatorInHierarchy(creatorKey);
            if (creator.IsValid) return creator.Func(this, type);
        }

        return null;
    }

    /// <summary>
    /// Resolves all registered instances of type T across the container hierarchy.
    /// </summary>
    /// <typeparam name="T">The type to resolve.</typeparam>
    /// <returns>All registered instances of type T.</returns>
    public IEnumerable<T> ResolveGroup<T>()
    {
        return ResolveGroup(typeof(T)).Cast<T>();
    }

    /// <summary>
    /// Resolves all registered instances of the specified type across the container hierarchy.
    /// </summary>
    /// <param name="type">The type to resolve.</param>
    /// <returns>All registered instances of the specified type.</returns>
    public IEnumerable<object> ResolveGroup(Type type)
    {
        var creators = FindCreatorsInHierarchy(this, type).SelectMany(c => c);
        return creators.Select(creator => creator.Func(this, type));
    }

    #endregion

    #region Register

    /// <summary>
    /// Registers a custom factory function for creating instances of the specified type.
    /// </summary>
    /// <param name="type">The type to register.</param>
    /// <param name="creator">Factory function to create instances.</param>
    /// <returns>A builder for configuring the registration.</returns>
    public WithBuilder Register(Type type, Func<Container, Type, object> creator)
    {
        return new WithBuilder(this, creator, type);
    }

    /// <summary>
    /// Registers a type for automatic constructor injection using source-generated type info.
    /// </summary>
    /// <param name="type">The type to register.</param>
    /// <returns>A builder for configuring the registration.</returns>
    /// <exception cref="NotSupportedException">Thrown when the type has no generated type info.</exception>
    public WithBuilder Register(Type type)
    {
        if (!TypeInfoRegistry.TryGet(type, out var typeInfo))
            throw new NotSupportedException($"Type {type} has no generated type info. Ensure it is used via Register<T>() or Instantiate<T>(), or has [Inject] on a member.");

        return Register(type, CreateInstanceFromTypeInfo);

        object CreateInstanceFromTypeInfo(Container resolveContainer, Type resolvedType)
        {
            var enableCircularCheck = EnableCircularCheck;
            if (enableCircularCheck) CircularCheck.Begin(type);
            try
            {
                var instance = typeInfo!.Create(resolveContainer);
                if (instance is IDisposable disposable && resolveContainer._disposableInstances != null)
                    resolveContainer._disposableInstances.Push(disposable);
                return instance;
            }
            finally
            {
                if (enableCircularCheck) CircularCheck.End();
            }
        }
    }

    /// <summary>
    /// Registers a custom factory function for creating instances of type T.
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    /// <param name="creator">Factory function to create instances.</param>
    /// <returns>A builder for configuring the registration.</returns>
    public WithBuilder Register<T>(Func<Container, Type, T> creator) where T : class
    {
        return Register(typeof(T), creator);
    }

    /// <summary>
    /// Registers type T for automatic constructor injection.
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    /// <returns>A builder for configuring the registration.</returns>
    public WithBuilder Register<T>()
    {
        return Register(typeof(T));
    }

    /// <summary>
    /// Registers an existing instance as a singleton.
    /// </summary>
    /// <typeparam name="T">The type of the instance.</typeparam>
    /// <param name="instance">The instance to register.</param>
    /// <returns>A builder for configuring the registration.</returns>
    public ResolverBuilder RegisterInstance<T>(T instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        return new ResolverBuilder(this, instance.GetType(), (_, _) => instance, ResolverLifetime.Singleton);
    }

    /// <summary>
    /// Checks if a type is registered in this container or any parent container.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is registered in the hierarchy.</returns>
    public bool IsRegisteredInHierarchy(Type type)
    {
        return FindFirstCreatorInHierarchy(type).IsValid;
    }

    /// <summary>
    /// Checks if type T is registered in this container or any parent container.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if type T is registered in the hierarchy.</returns>
    public bool IsRegisteredInHierarchy<T>()
    {
        return FindFirstCreatorInHierarchy(typeof(T)).IsValid;
    }

    /// <summary>
    /// Checks if a type is registered in this container only (excludes parents).
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is registered in this container.</returns>
    public bool IsRegistered(Type type)
    {
        return FindFirstCreatorInThisContainer(type).IsValid;
    }

    /// <summary>
    /// Checks if type T is registered in this container only (excludes parents).
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if type T is registered in this container.</returns>
    public bool IsRegistered<T>()
    {
        return FindFirstCreatorInThisContainer(typeof(T)).IsValid;
    }

    #endregion

    #region Call

    /// <summary>
    /// Invokes a delegate function with dependency injection for its parameters.
    /// </summary>
    /// <typeparam name="T">The delegate type.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <returns>The function's return value.</returns>
    /// <exception cref="ArgumentException">Thrown if the function returns void.</exception>
    public object CallFunc<T>(T func) where T : Delegate
    {
        var method = func.Method;
        if (method.ReturnType == typeof(void)) throw new ArgumentException($"{method.Name} must return void", nameof(func));
        return method.Invoke(func.Target, this)!;
    }

    /// <summary>
    /// Invokes a delegate action with dependency injection for its parameters.
    /// </summary>
    /// <typeparam name="T">The delegate type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    public void CallAction<T>(T action) where T : Delegate
    {
        action.Method.Invoke(action.Target, this);
    }

    #endregion

    #region Instantiate

    /// <summary>
    /// Creates a new instance of the specified type with dependency injection.
    /// Does not register the instance in the container.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>A new instance with injected dependencies.</returns>
    public object Instantiate(Type type)
    {
        if (!TypeInfoRegistry.TryGet(type, out var typeInfo))
            throw new NotSupportedException($"Type {type} has no generated type info. Ensure it is used via Register<T>() or Instantiate<T>(), or has [Inject] on a member.");
        return typeInfo!.Create(this);
    }

    /// <summary>
    /// Creates a new instance of type T with dependency injection.
    /// Does not register the instance in the container.
    /// </summary>
    /// <typeparam name="T">The type to instantiate.</typeparam>
    /// <returns>A new instance with injected dependencies.</returns>
    public T Instantiate<T>()
    {
        return (T)Instantiate(typeof(T));
    }

    #endregion

    private static IEnumerable<IReadOnlyCollection<Resolver>> FindCreatorsInHierarchy(Container container, Type type)
    {
        var current = container;
        while (current != null)
        {
            if (current.Resolvers.TryGetValue(type, out var creators))
                yield return creators;
            current = current.Parent;
        }
    }

    private Resolver FindFirstCreatorInThisContainer(Type type)
    {
        return Resolvers.TryGetValue(type, out var creators) && creators.TryPeek(out var creator) ? creator : default;
    }

    private Resolver FindFirstCreatorInHierarchy(Type type)
    {
        var current = this;
        while (current != null)
        {
            var creator = current.FindFirstCreatorInThisContainer(type);
            if (creator.IsValid) return creator;
            current = current.Parent;
        }
        return default;
    }

    private object ResolveParameterInfo(ParameterInfo parameter, Type? label = null)
    {
        var parameterType = parameter.ParameterType;
        var instance = TryResolve(parameterType, label);
        if (instance != null) return instance;
        return parameter.HasDefaultValue ? parameter.DefaultValue! : throw new ArgumentException($"cannot resolve parameter {parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}.{parameter.Name}");
    }

    internal object[] ResolveParameterInfos(ParameterInfo[] parameters, Type?[] labels, object[] arguments)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var label = labels[i];
            arguments[i] = ResolveParameterInfo(parameter, label);
        }
        return arguments;
    }
}

/// <summary>
/// Marks a constructor, method, field, or property for dependency injection.
/// Can also be applied to parameters to specify a label for named resolution.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InjectAttribute : Attribute
{
    /// <summary>Optional label type for named/keyed resolution.</summary>
    public Type? Label { get; }
    public InjectAttribute(Type? label = null) => Label = label;
}

/// <summary>
/// Marker interface for typed labels used in named/keyed dependency resolution.
/// Implement this on a label type to associate it with a contract type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The contract type this label applies to, or a generic parameter for untyped labels.</typeparam>
// ReSharper disable once UnusedTypeParameter
#pragma warning disable CA1040
public interface ILabel<T> { }
#pragma warning restore CA1040

/// <summary>
/// Defines the lifetime scope of registered types.
/// </summary>
public enum ResolverLifetime
{
    /// <summary>Creates a new instance for each resolution.</summary>
    Transient,
    /// <summary>Creates a single instance shared across all resolutions.</summary>
    Singleton,
    /// <summary>Creates a single instance per scope/child container.</summary>
    Scoped
}

/// <summary>
/// Final-stage fluent builder for binding a resolved type to contract types.
/// Reached after lifetime has been chosen (or defaulted to transient).
/// </summary>
public readonly ref struct ResolverBuilder
{
    internal Container Container { get; }
    internal Resolver Resolver { get; }
    internal Type ConcreteType { get; }

    internal ResolverBuilder(Container container, Type concreteType, Func<Container, Type, object> creator, ResolverLifetime lifetime)
    {
        Container = container;
        Resolver = new Resolver(creator, lifetime);
        ConcreteType = concreteType;
    }

    /// <summary>
    /// Registers the type as implementing the specified contract type.
    /// </summary>
    /// <param name="contractType">The contract/interface type to register as.</param>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "CreateLabelType (MakeGenericType) is only invoked when a non-null label is supplied; callers using labels accept the AOT requirement.")]
    public ResolverBuilder As(Type contractType, Type? label = null)
    {
        if (Container.PreventDisposableTransient && Resolver.Lifetime == ResolverLifetime.Transient && typeof(IDisposable).IsAssignableFrom(ConcreteType))
            throw new ArgumentException($"disposable type {ConcreteType} cannot register as transient. this check can be disabled by set {nameof(OneShot.Container.PreventDisposableTransient)} to false.");
        if (!contractType.IsAssignableFrom(ConcreteType)) throw new ArgumentException($"concreteType({ConcreteType}) must derived from contractType({contractType})", nameof(contractType));
        if (label != null) contractType = label.CreateLabelType(contractType);
        var resolverStack = GetOrCreateResolverStack(contractType);
        if (!resolverStack.Contains(Resolver)) resolverStack.Push(Resolver);
        return this;
    }

    /// <summary>
    /// Registers the type as implementing type T.
    /// </summary>
    /// <typeparam name="T">The contract/interface type to register as.</typeparam>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public ResolverBuilder As<T>(Type? label = null)
    {
        return As(typeof(T), label);
    }

    /// <summary>
    /// Registers the type as itself (concrete type registration).
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public ResolverBuilder AsSelf(Type? label = null)
    {
        return As(ConcreteType, label);
    }

    /// <summary>
    /// Registers the type as all interfaces it implements.
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "ConcreteType is a registered type that always has its interfaces preserved at runtime.")]
    public ResolverBuilder AsInterfaces(Type? label = null)
    {
        foreach (var @interface in ConcreteType.GetInterfaces()) As(@interface, label);
        return this;
    }

    /// <summary>
    /// Registers the type as all its base classes (excluding Object and ValueType).
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public ResolverBuilder AsBases(Type? label = null)
    {
        var baseType = ConcreteType.BaseType;
        while (baseType != null && baseType != typeof(Object) && baseType != typeof(ValueType))
        {
            As(baseType, label);
            baseType = baseType.BaseType;
        }
        return this;
    }

    private ConcurrentStack<Resolver> GetOrCreateResolverStack(Type type)
    {
        if (!Container.Resolvers.TryGetValue(type, out var resolvers))
        {
            resolvers = new ConcurrentStack<Resolver>();
            Container.Resolvers[type] = resolvers;
        }
        return resolvers;
    }
}

/// <summary>
/// Mid-stage fluent builder for selecting lifetime scope after <see cref="WithBuilder.With(object[])"/> overrides.
/// Supports <see cref="Transient"/>, <see cref="Singleton"/>, and <see cref="Scoped"/> lifetimes.
/// </summary>
public readonly ref struct LifetimeBuilder
{
    internal Container Container { get; }
    internal Resolver Resolver { get; }
    internal Type ConcreteType { get; }

    internal LifetimeBuilder(Container container, Func<Container, Type, object> creator, Type concreteType)
    {
        Container = container;
        Resolver = new Resolver(creator, ResolverLifetime.Transient);
        ConcreteType = concreteType;
    }

    /// <summary>
    /// Configures the registration as transient (new instance per resolution).
    /// </summary>
    /// <returns>A ResolverBuilder for further configuration.</returns>
    public ResolverBuilder Transient()
    {
        return new ResolverBuilder(Container, ConcreteType, Resolver.Func, ResolverLifetime.Transient);
    }

    /// <summary>
    /// Configures the registration as singleton (single instance for container hierarchy).
    /// </summary>
    /// <returns>A ResolverBuilder for further configuration.</returns>
    public ResolverBuilder Singleton()
    {
        var container = Container;
        var concreteType = ConcreteType;
        var resolverFunc = Resolver.Func;
        var lazyValue = new Lazy<object>(() => resolverFunc(container, concreteType));
        return new ResolverBuilder(container, concreteType, (_, _) => lazyValue.Value, ResolverLifetime.Singleton);
    }

    [Obsolete("use Scoped instead")]
    public ResolverBuilder Scope()
    {
        return Scoped();
    }

    /// <summary>
    /// Configures the registration as scoped (single instance per scope/child container).
    /// </summary>
    /// <returns>A ResolverBuilder for further configuration.</returns>
    public ResolverBuilder Scoped()
    {
        var containerCapture = Container;
        var concreteType = ConcreteType;
        var resolverFunc = Resolver.Func;
        var lazyValue = new Lazy<object>(() => resolverFunc(containerCapture, concreteType));
        return new ResolverBuilder(containerCapture, concreteType, ResolveScopedInstance, ResolverLifetime.Scoped);

        object ResolveScopedInstance(Container container, Type contractType)
        {
            if (container == containerCapture) return lazyValue.Value;
            // Runtime registration during scoped resolution (thread-safe via container's ConcurrentDictionary)
            var lifetimeBuilder = new LifetimeBuilder(container, resolverFunc, concreteType);
            lifetimeBuilder.Scoped().As(contractType);
            return container.Resolve(contractType);
        }
    }

    /// <summary>
    /// Registers the type as implementing the specified contract type.
    /// </summary>
    /// <param name="contractType">The contract/interface type to register as.</param>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public LifetimeBuilder As(Type contractType, Type? label = null)
    {
        var resolverBuilder = new ResolverBuilder(Container, ConcreteType, Resolver.Func, Resolver.Lifetime);
        resolverBuilder.As(contractType, label);
        return this;
    }

    /// <summary>
    /// Registers the type as implementing type T.
    /// </summary>
    /// <typeparam name="T">The contract/interface type to register as.</typeparam>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public LifetimeBuilder As<T>(Type? label = null)
    {
        return As(typeof(T), label);
    }

    /// <summary>
    /// Registers the type as itself (concrete type registration).
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public LifetimeBuilder AsSelf(Type? label = null)
    {
        return As(ConcreteType, label);
    }

    /// <summary>
    /// Registers the type as all interfaces it implements.
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public LifetimeBuilder AsInterfaces(Type? label = null)
    {
#pragma warning disable IL2075
        foreach (var @interface in ConcreteType.GetInterfaces()) As(@interface, label);
#pragma warning restore IL2075
        return this;
    }

    /// <summary>
    /// Registers the type as all its base classes (excluding Object and ValueType).
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public LifetimeBuilder AsBases(Type? label = null)
    {
        var baseType = ConcreteType.BaseType;
        while (baseType != null && baseType != typeof(Object) && baseType != typeof(ValueType))
        {
            As(baseType, label);
            baseType = baseType.BaseType;
        }
        return this;
    }
}

/// <summary>
/// Entry-point fluent builder returned by <see cref="Container.Register{T}()"/>.
/// Supports overriding dependencies via <see cref="With(object[])"/>, choosing lifetime, or binding directly.
/// </summary>
public readonly ref struct WithBuilder
{
    internal Container Container { get; }
    internal Resolver Resolver { get; }
    internal Type ConcreteType { get; }
    internal WithBuilder(Container container, Func<Container, Type, object> creator, Type concreteType)
    {
        Container = container;
        Resolver = new Resolver(creator, ResolverLifetime.Transient);
        ConcreteType = concreteType;
    }

    /// <summary>
    /// Configures the registration as transient (new instance per resolution).
    /// </summary>
    /// <returns>A ResolverBuilder for further configuration.</returns>
    public ResolverBuilder Transient()
    {
        return new ResolverBuilder(Container, ConcreteType, Resolver.Func, ResolverLifetime.Transient);
    }

    /// <summary>
    /// Configures the registration as singleton (single instance for container hierarchy).
    /// </summary>
    /// <returns>A ResolverBuilder for further configuration.</returns>
    public ResolverBuilder Singleton()
    {
        var container = Container;
        var concreteType = ConcreteType;
        var resolverFunc = Resolver.Func;
        var lazyValue = new Lazy<object>(() => resolverFunc(container, concreteType));
        return new ResolverBuilder(container, concreteType, (_, _) => lazyValue.Value, ResolverLifetime.Singleton);
    }

    /// <summary>
    /// Configures the registration as scoped (single instance per scope/child container).
    /// </summary>
    /// <returns>A ResolverBuilder for further configuration.</returns>
    public ResolverBuilder Scoped()
    {
        var containerCapture = Container;
        var concreteType = ConcreteType;
        var resolverFunc = Resolver.Func;
        var lazyValue = new Lazy<object>(() => resolverFunc(containerCapture, concreteType));
        return new ResolverBuilder(containerCapture, concreteType, ResolveScopedInstance, ResolverLifetime.Scoped);

        object ResolveScopedInstance(Container container, Type contractType)
        {
            if (container == containerCapture) return lazyValue.Value;
            // Runtime registration during scoped resolution (thread-safe via container's ConcurrentDictionary)
            var lifetimeBuilder = new LifetimeBuilder(container, resolverFunc, concreteType);
            lifetimeBuilder.Scoped().As(contractType);
            return container.Resolve(contractType);
        }
    }

    /// <summary>
    /// Provides specific instances to use for constructor parameters.
    /// </summary>
    /// <param name="instances">Instances to inject into the constructor.</param>
    /// <returns>A LifetimeBuilder for further configuration.</returns>
    public LifetimeBuilder With(params object[] instances)
    {
        return WithImpl(instances.Select(instance => (instance, (Type?)null)));
    }

    /// <summary>
    /// Provides specific labeled instances to use for constructor parameters.
    /// </summary>
    /// <param name="labeledInstances">Labeled instances to inject into the constructor.</param>
    /// <returns>A LifetimeBuilder for further configuration.</returns>
    public LifetimeBuilder With(params (object instance, Type? label)[] labeledInstances)
    {
        return WithImpl(labeledInstances);
    }

    private LifetimeBuilder WithImpl(IEnumerable<(object instance, Type? label)> labeledInstances)
    {
        // Create child container to hold the provided instances
        // This container will be disposed when parent disposes
#pragma warning disable CA2000 // Child container lifecycle managed by parent
        Container container = Container.CreateChildContainer();
#pragma warning restore CA2000
        // Register each provided instance in the child container for override resolution
        foreach ((object instance, Type? label) in labeledInstances)
            container.RegisterInstance(instance).AsSelf(label).AsBases(label).AsInterfaces(label);
        // Return builder that resolves from child container with overrides
        var parentContainer = Container;
        var concreteType = ConcreteType;
        var resolverFunc = Resolver.Func;
        return new LifetimeBuilder(parentContainer, (_, contractType) => resolverFunc(container, contractType), concreteType);
    }

    /// <summary>
    /// Registers the type as implementing the specified contract type.
    /// </summary>
    /// <param name="contractType">The contract/interface type to register as.</param>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public WithBuilder As(Type contractType, Type? label = null)
    {
        var resolverBuilder = new ResolverBuilder(Container, ConcreteType, Resolver.Func, Resolver.Lifetime);
        resolverBuilder.As(contractType, label);
        return this;
    }

    /// <summary>
    /// Registers the type as implementing type T.
    /// </summary>
    /// <typeparam name="T">The contract/interface type to register as.</typeparam>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public WithBuilder As<T>(Type? label = null)
    {
        return As(typeof(T), label);
    }

    /// <summary>
    /// Registers the type as itself (concrete type registration).
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public WithBuilder AsSelf(Type? label = null)
    {
        return As(ConcreteType, label);
    }

    /// <summary>
    /// Registers the type as all interfaces it implements.
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "ConcreteType is a registered type that always has its interfaces preserved at runtime.")]
    public WithBuilder AsInterfaces(Type? label = null)
    {
        foreach (var @interface in ConcreteType.GetInterfaces()) As(@interface, label);
        return this;
    }

    /// <summary>
    /// Registers the type as all its base classes (excluding Object and ValueType).
    /// </summary>
    /// <param name="label">Optional label for named registrations.</param>
    /// <returns>This builder for method chaining.</returns>
    public WithBuilder AsBases(Type? label = null)
    {
        var baseType = ConcreteType.BaseType;
        while (baseType != null && baseType != typeof(Object) && baseType != typeof(ValueType))
        {
            As(baseType, label);
            baseType = baseType.BaseType;
        }
        return this;
    }
}

/// <summary>
/// Extension methods for injecting dependencies into existing instances.
/// </summary>
public static class InjectExtension
{
    /// <summary>
    /// Injects dependencies into all fields, properties, and methods marked with [Inject].
    /// Uses source-generated type info. If no type info is found, assumes no [Inject] members and does nothing.
    /// </summary>
    /// <param name="container">The container to resolve dependencies from.</param>
    /// <param name="instance">The instance to inject into.</param>
    /// <param name="instanceType">The type of the instance.</param>
    public static void InjectAll(this Container container, object instance, Type instanceType)
    {
        if (TypeInfoRegistry.TryGet(instanceType, out var typeInfo))
            typeInfo!.InjectAll(container, instance);
    }

    /// <summary>
    /// Injects dependencies into all fields, properties, and methods marked with [Inject].
    /// </summary>
    /// <typeparam name="T">The type of the instance.</typeparam>
    /// <param name="container">The container to resolve dependencies from.</param>
    /// <param name="instance">The instance to inject into.</param>
    public static void InjectAll<T>(this Container container, T instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        InjectAll(container, instance, instance.GetType());
    }
}

/// <summary>
/// Exception thrown when a circular dependency is detected during resolution.
/// </summary>
public class CircularDependencyException : Exception
{
    public CircularDependencyException() { }
    public CircularDependencyException(string message) : base(message) { }
    public CircularDependencyException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thread-safe circular dependency detection during type resolution.
/// </summary>
static class CircularCheck
{
    private static readonly ThreadLocal<Stack<Type>> s_circularCheckSet = new(() => new Stack<Type>());

    public static void Begin(Type type)
    {
        if (s_circularCheckSet.Value!.Contains(type)) throw new CircularDependencyException($"circular dependency on {type.Name}");
        s_circularCheckSet.Value!.Push(type);
    }

    public static void End()
    {
        s_circularCheckSet.Value!.Pop();
    }
}

/// <summary>
/// Constructs closed generic label types for named/keyed resolution.
/// Validates that label types implement <see cref="ILabel{T}"/> correctly.
/// </summary>
#pragma warning disable IL2070, IL2055, IL3050 // Label type resolution is inherently runtime-dynamic
static class LabelExtension
{
    /// <summary>
    /// Creates a closed label type from an <see cref="ILabel{T}"/> implementation and a contract type.
    /// </summary>
    [RequiresDynamicCode("Constructs a closed generic label type at runtime via MakeGenericType.")]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Label types are user-defined and always have their ILabel<> interface preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "Label types are user-defined; the generic type arguments are reachable at call sites.")]
    public static Type CreateLabelType(this Type label, Type contractType)
    {
        if (label.BaseType != null) throw new ArgumentException($"label {label.FullName} cannot have base type", nameof(label));
        var interfaces = label.GetInterfaces();
        if (interfaces.Length != 1 && interfaces[0].GetGenericTypeDefinition() != typeof(ILabel<>))
            throw new ArgumentException($"label {label.FullName} must implement and only implement {nameof(ILabel<int>)}<> interface.", nameof(label));
        var labelValueType = interfaces[0].GenericTypeArguments[0];
        if (labelValueType.IsGenericParameter) label = label.MakeGenericType(contractType);
        else if (labelValueType != contractType) throw new ArgumentException($"Type mismatch between typed label {label.FullName} and {contractType.FullName}");
        return label;
    }
}
#pragma warning restore IL2070, IL2055, IL3050

/// <summary>
/// Caches parameter metadata and <see cref="InjectAttribute"/> labels for <see cref="MethodInfo"/> invocation.
/// Used by <see cref="Container.CallFunc{T}"/> and <see cref="Container.CallAction{T}"/> for runtime delegate injection.
/// </summary>
static class MethodInfoCache
{
    private static readonly ConcurrentDictionary<MethodInfo, (Func<object?, object?[]?, object?>, ParameterInfo[], Type?[])> s_compiled = new();

    /// <summary>
    /// Retrieves or caches the parameter info and labels for a method.
    /// </summary>
    public static (Func<object?, object?[]?, object?> call, ParameterInfo[] parameters, Type?[] labels) Compile(this MethodInfo mi)
    {
        if (s_compiled.TryGetValue(mi, out var t)) return t;
        var parameters = mi.GetParameters();
        var labels = parameters.Select(param => param.GetCustomAttribute<InjectAttribute>()?.Label).ToArray();
        s_compiled.TryAdd(mi, (mi.Invoke, parameters, labels));
        return (mi.Invoke, parameters, labels);
    }

    /// <summary>
    /// Invokes a method with parameters resolved from the container.
    /// </summary>
    public static object? Invoke(this MethodInfo mi, object? target, Container container)
    {
        var (call, parameters, labels) = mi.Compile();
        var arguments = new object[parameters.Length];
        var args = container.ResolveParameterInfos(parameters, labels, arguments);
        return call(target, args);
    }
}

/// <summary>
/// Extension methods for registering open generic types.
/// </summary>
public static class GenericExtension
{
    /// <summary>
    /// Registers an open generic type with a custom factory method.
    /// </summary>
    /// <param name="container">The container to register in.</param>
    /// <param name="genericType">The open generic type to register.</param>
    /// <param name="creator">Static factory method for creating instances.</param>
    /// <returns>A builder for further configuration.</returns>
    [RequiresDynamicCode("Open generic registration uses MakeGenericMethod at runtime.")]
    [RequiresUnreferencedCode("Open generic registration discovers methods at runtime via reflection.")]
    public static WithBuilder RegisterGeneric(this Container container, Type genericType, MethodInfo creator)
    {
        if (genericType is null) throw new ArgumentNullException(nameof(genericType));
        if (creator is null) throw new ArgumentNullException(nameof(creator));
        if (!genericType.IsGenericType) throw new ArgumentException($"{genericType.FullName} is not a generic type", nameof(genericType));
        if (!creator.IsStatic) throw new ArgumentException($"{creator.Name} is not static", nameof(creator));
        if (!creator.ReturnType.IsGenericType || creator.ReturnType.GetGenericTypeDefinition() != genericType) throw new ArgumentException($"the return type ({creator.ReturnType}) of {creator.Name} require to be the same as {nameof(genericType)} ({genericType})", nameof(creator));
        // TODO: Validate generic type constraints match between genericType and creator
        if (creator.GetGenericArguments().Length != genericType.GetGenericArguments().Length) throw new ArgumentException($"the method has different generic arguments: actual={creator.GetGenericArguments().Length} expected={genericType.GetGenericArguments()}", nameof(creator));
        var parameters = creator.GetParameters();
        if (parameters.Length != 2 || parameters[0].ParameterType != typeof(Container) || parameters[1].ParameterType != typeof(Type)) throw new ArgumentException("creator must have exact parameter of (Container, Type)", nameof(creator));
        return container.Register(genericType, GetInstanceCreator(creator));

        [UnconditionalSuppressMessage("Trimming", "IL2060",
            Justification = "RegisterGeneric is annotated with [RequiresUnreferencedCode]; callers acknowledge the requirement.")]
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "RegisterGeneric is annotated with [RequiresDynamicCode]; callers acknowledge the requirement.")]
        static Func<Container, Type, object> GetInstanceCreator(MethodInfo creator)
        {
            return (container, type) => creator.MakeGenericMethod(type.GetGenericArguments()).Invoke(null, new object[] { container, type })!;
        }
    }
}
