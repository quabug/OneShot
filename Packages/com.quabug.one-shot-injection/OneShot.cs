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
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using JetBrains.Annotations;
#if !ENABLE_IL2CPP
using System.Linq.Expressions;
#endif

namespace OneShot
{
    /// <summary>
    /// Represents a resolver function with its associated lifetime scope.
    /// </summary>
    public readonly struct Resolver : IEquatable<Resolver>
    {
        public Func<Container, Type, object> Func { get; }
        public ResolverLifetime Lifetime { get; }
        public bool IsValid => Func != null;

        public Resolver(Func<Container, Type, object> func, ResolverLifetime lifetime)
        {
            Func = func;
            Lifetime = lifetime;
        }

        public bool Equals(Resolver other) => Func.Equals(other.Func) && Lifetime == other.Lifetime;
        public override bool Equals(object? obj) => obj is Resolver other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Func, (int)Lifetime);
        public static bool operator ==(in Resolver left, in Resolver right) => left.Equals(right);
        public static bool operator !=(in Resolver left, in Resolver right) => !(left == right);
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
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            = true;
#else
            = false;
#endif

        /// <summary>
        /// Pre-allocates constructor argument arrays during registration.
        /// Improves resolution performance at the cost of increased memory usage.
        /// Recommended for high-throughput scenarios with known type graphs.
        /// </summary>
        public bool PreAllocateArgumentArrayOnRegister { get; set; }

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
            PreAllocateArgumentArrayOnRegister = parent.PreAllocateArgumentArrayOnRegister;
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
        [MustUseReturnValue]
        public WithBuilder Register(Type type, Func<Container, Type, object> creator)
        {
            return new WithBuilder(this, creator, type);
        }

        /// <summary>
        /// Registers a type for automatic constructor injection.
        /// Selects constructor marked with [Inject] or single public constructor.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <returns>A builder for configuring the registration.</returns>
        [MustUseReturnValue]
        public WithBuilder Register(Type type)
        {
            var ci = FindConstructorInfo(type);
            var (newFunc, parameters, labels) = ci.Compile();
            ThreadLocal<object[]>? preAllocatedArguments = null;
            if (PreAllocateArgumentArrayOnRegister)
            {
#pragma warning disable CA2000 // dispose arguments on disposing phase of container
                preAllocatedArguments = new ThreadLocal<object[]>(() => new object[parameters.Length]);
#pragma warning restore CA2000
                _disposableInstances.Push(preAllocatedArguments);
            }
            return Register(type, CreateInstance);

            object CreateInstance(Container resolveContainer, Type resolvedType)
            {
                var enableCircularCheck = EnableCircularCheck;
                if (enableCircularCheck) CircularCheck.Begin(type);
                try
                {
                    var arguments = preAllocatedArguments?.Value ?? new object[parameters.Length];
                    var args = resolveContainer.ResolveParameterInfos(parameters, labels, arguments);
                    var instance = newFunc(args);
                    if (instance is IDisposable disposable) resolveContainer._disposableInstances!.Push(disposable);
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
        [MustUseReturnValue]
        public WithBuilder Register<T>(Func<Container, Type, T> creator) where T : class
        {
            return Register(typeof(T), creator);
        }

        /// <summary>
        /// Registers type T for automatic constructor injection.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <returns>A builder for configuring the registration.</returns>
        [MustUseReturnValue]
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
        [MustUseReturnValue]
        public ResolverBuilder RegisterInstance<T>(T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return new ResolverBuilder(this, instance.GetType(), (c, t) => instance, ResolverLifetime.Singleton);
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
            return method.Invoke(func.Target, this);
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
            return FindConstructorInfo(type).Invoke(this);
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

        private static ConstructorInfo FindConstructorInfo(Type type)
        {
            var constructors = type.GetConstructors();
            ConstructorInfo? ci = null;
            if (constructors.Length == 1) ci = constructors[0];
            else if (constructors.Length > 1) ci = constructors.Single(c => c.GetCustomAttribute<InjectAttribute>() != null);
            if (ci == null) throw new NotSupportedException($"cannot found constructor of type {type}");
            return ci;
        }

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

    [UsedImplicitly]
    [MeansImplicitUse(ImplicitUseKindFlags.Assign)]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute
    {
        public Type? Label { get; }
        public InjectAttribute(Type? label = null) => Label = label;
    }

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
    /// Fluent builder for configuring type registrations.
    /// </summary>
    public class ResolverBuilder
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
    /// Builder for configuring the lifetime scope of registered types.
    /// </summary>
    public class LifetimeBuilder : ResolverBuilder
    {
        internal LifetimeBuilder(Container container, Func<Container, Type, object> creator, Type concreteType)
            : base(container, concreteType, creator, ResolverLifetime.Transient) { }

        /// <summary>
        /// Configures the registration as transient (new instance per resolution).
        /// </summary>
        /// <returns>A ResolverBuilder for further configuration.</returns>
        [MustUseReturnValue]
        public ResolverBuilder Transient()
        {
            return this;
        }

        /// <summary>
        /// Configures the registration as singleton (single instance for container hierarchy).
        /// </summary>
        /// <returns>A ResolverBuilder for further configuration.</returns>
        [MustUseReturnValue]
        public ResolverBuilder Singleton()
        {
            var lazyValue = new Lazy<object>(() => Resolver.Func(Container, ConcreteType));
            return new ResolverBuilder(Container, ConcreteType, (_, __) => lazyValue.Value, ResolverLifetime.Singleton);
        }

        [MustUseReturnValue, Obsolete("use Scoped instead")]
        public ResolverBuilder Scope()
        {
            return Scoped();
        }

        /// <summary>
        /// Configures the registration as scoped (single instance per scope/child container).
        /// </summary>
        /// <returns>A ResolverBuilder for further configuration.</returns>
        [MustUseReturnValue]
        public ResolverBuilder Scoped()
        {
            var lazyValue = new Lazy<object>(() => Resolver.Func(Container, ConcreteType));
            return new ResolverBuilder(Container, ConcreteType, ResolveScopedInstance, ResolverLifetime.Scoped);

            object ResolveScopedInstance(Container container, Type contractType)
            {
                if (container == Container) return lazyValue.Value;
                // Runtime registration during scoped resolution (thread-safe via container's ConcurrentDictionary)
                container.Register(ConcreteType, Resolver.Func).Scoped().As(contractType);
                return container.Resolve(contractType);
            }
        }
    }

    /// <summary>
    /// Builder for registering types with specific dependency instances.
    /// </summary>
    public class WithBuilder : LifetimeBuilder
    {
        public WithBuilder(Container container, Func<Container, Type, object> creator, Type concreteType) : base(container, creator, concreteType) { }

        /// <summary>
        /// Provides specific instances to use for constructor parameters.
        /// </summary>
        /// <param name="instances">Instances to inject into the constructor.</param>
        /// <returns>A LifetimeBuilder for further configuration.</returns>
        [MustUseReturnValue]
        public LifetimeBuilder With(params object[] instances)
        {
            return WithImpl(instances.Select(instance => (instance, (Type?)null)));
        }

        /// <summary>
        /// Provides specific labeled instances to use for constructor parameters.
        /// </summary>
        /// <param name="labeledInstances">Labeled instances to inject into the constructor.</param>
        /// <returns>A LifetimeBuilder for further configuration.</returns>
        [MustUseReturnValue]
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
            return new LifetimeBuilder(Container, (_, contractType) => Resolver.Func(container, contractType), ConcreteType);
        }
    }

    /// <summary>
    /// Extension methods for injecting dependencies into existing instances.
    /// </summary>
    public static class InjectExtension
    {
        private static readonly Dictionary<Type, TypeInjector> s_injectors = new();

        /// <summary>
        /// Injects dependencies into all fields, properties, and methods marked with [Inject].
        /// </summary>
        /// <param name="container">The container to resolve dependencies from.</param>
        /// <param name="instance">The instance to inject into.</param>
        /// <param name="instanceType">The type of the instance.</param>
        public static void InjectAll(this Container container, object instance, Type instanceType)
        {
            s_injectors.GetOrCreate(instanceType, () => new TypeInjector(instanceType)).InjectAll(container, instance);
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
    /// Internal helper for performing field/property/method injection on instances.
    /// </summary>
    sealed class TypeInjector
    {
        private readonly Type _type;
        private readonly List<FieldInfo> _fields = new List<FieldInfo>();
        private readonly List<PropertyInfo> _properties = new List<PropertyInfo>();
        private readonly List<MethodInfo> _methods = new List<MethodInfo>();

        public TypeInjector(Type type)
        {
            _type = type;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var member in type.GetMembers(flags).Where(mi => mi.GetCustomAttribute<InjectAttribute>() != null))
            {
                switch (member)
                {
                    case FieldInfo field:
                        _fields.Add(field);
                        break;
                    case PropertyInfo property:
                        if (property.CanWrite) _properties.Add(property);
                        else throw new NotSupportedException($"cannot inject on read-only property {property.DeclaringType.Name}.{property.Name}");
                        break;
                    case MethodInfo method:
                        // TODO: Validate method signature and parameters
                        _methods.Add(method);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public void InjectFields(Container container, object instance)
        {
            CheckInstanceType(instance);
            foreach (var field in _fields)
            {
                var (setter, label) = field.Compile();
                setter(instance, container.Resolve(field.FieldType, label));
            }
        }

        public void InjectProperties(Container container, object instance)
        {
            CheckInstanceType(instance);
            foreach (var property in _properties)
            {
                var (setter, label) = property.Compile();
                setter(instance, container.Resolve(property.PropertyType, label));
            }
        }

        public void InjectMethods(Container container, object instance)
        {
            foreach (var method in _methods) method.Invoke(instance, container);
        }

        public void InjectAll(Container container, object instance)
        {
            InjectFields(container, instance);
            InjectProperties(container, instance);
            InjectMethods(container, instance);
        }

        private void CheckInstanceType(object instance)
        {
            if (instance.GetType() != _type) throw new ArgumentException($"mismatch types between {nameof(instance)}({instance.GetType()}) and type({_type})");
        }
    }

    /// <summary>
    /// Internal extension methods for dictionary operations.
    /// </summary>
    internal static class DictionaryExtension
    {
        public static TValue GetOrCreate<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key
        ) where TValue : new()
        {
            return dictionary.GetOrCreate(key, () => new TValue());
        }

        public static TValue GetOrCreate<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TValue> newValue
        )
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = newValue();
                dictionary[key] = value;
            }
            return value;
        }
    }

    /// <summary>
    /// Exception thrown when a circular dependency is detected during resolution.
    /// </summary>
    [Serializable]
    public class CircularDependencyException : Exception
    {
        public CircularDependencyException() { }
        public CircularDependencyException(string message) : base(message) { }
        public CircularDependencyException(string message, Exception inner) : base(message, inner) { }
        protected CircularDependencyException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Thread-safe circular dependency detection during type resolution.
    /// </summary>
    static class CircularCheck
    {
        private static readonly ThreadLocal<Stack<Type>> s_circularCheckSet = new(() => new Stack<Type>());

        public static void Begin(Type type)
        {
            if (s_circularCheckSet.Value.Contains(type)) throw new CircularDependencyException($"circular dependency on {type.Name}");
            s_circularCheckSet.Value.Push(type);
        }

        public static void End()
        {
            s_circularCheckSet.Value.Pop();
        }
    }

    /// <summary>
    /// Extensions for handling labeled/named type registrations.
    /// </summary>
    static class LabelExtension
    {
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

    /// <summary>
    /// Cache for compiled constructor invocation delegates.
    /// </summary>
    static class ConstructorInfoCache
    {
        private static readonly ConcurrentDictionary<ConstructorInfo, (Func<object[], object>, ParameterInfo[], Type?[])> s_compiled = new();

        public static (Func<object[], object> newFunc, ParameterInfo[] parameters, Type?[] labels) Compile(this ConstructorInfo ci)
        {
            if (s_compiled.TryGetValue(ci, out var t)) return t;
            var parameters = ci.GetParameters();
            var labels = parameters.Select(param => param.GetCustomAttribute<InjectAttribute>()?.Label).ToArray();
#if ENABLE_IL2CPP
            Func<object[], object> func = ci.Invoke;
#else
            var @params = Expression.Parameter(typeof(object[]));
            var args = parameters.Select((parameter, index) => Expression.Convert(
                Expression.ArrayIndex(@params, Expression.Constant(index)),
                parameter.ParameterType)
            ).Cast<Expression>().ToArray();
            var @new = Expression.New(ci, args);
            var lambda = Expression.Lambda(typeof(Func<object[], object>), Expression.Convert(@new, typeof(object)), @params);
            var func = (Func<object[], object>)lambda.Compile();
#endif
            s_compiled.TryAdd(ci, (func, parameters, labels));
            return (func, parameters, labels);
        }

        public static object Invoke(this ConstructorInfo ci, Container container)
        {
            var (newFunc, parameters, labels) = ci.Compile();
            var arguments = new object[parameters.Length];
            var args = container.ResolveParameterInfos(parameters, labels, arguments);
            return newFunc(args);
        }
    }

    /// <summary>
    /// Cache for compiled method invocation delegates.
    /// </summary>
    static class MethodInfoCache
    {
        private static readonly ConcurrentDictionary<MethodInfo, (Func<object, object[], object>, ParameterInfo[], Type?[])> s_compiled = new();

        public static (Func<object, object[], object> call, ParameterInfo[] parameters, Type?[] labels) Compile(this MethodInfo mi)
        {
            if (s_compiled.TryGetValue(mi, out var t)) return t;
            var parameters = mi.GetParameters();
            var labels = parameters.Select(param => param.GetCustomAttribute<InjectAttribute>()?.Label).ToArray();
            s_compiled.TryAdd(mi, (mi.Invoke, parameters, labels));
            return (mi.Invoke, parameters, labels);
        }

        public static object Invoke(this MethodInfo mi, object target, Container container)
        {
            var (call, parameters, labels) = mi.Compile();
            var arguments = new object[parameters.Length];
            var args = container.ResolveParameterInfos(parameters, labels, arguments);
            return call(target, args);
        }
    }

    /// <summary>
    /// Cache for compiled property setter delegates.
    /// </summary>
    static class PropertyInfoCache
    {
        private static readonly ConcurrentDictionary<PropertyInfo, (Action<object, object>, Type?)> s_compiled = new();

        public static (Action<object, object> setValue, Type? label) Compile(this PropertyInfo pi)
        {
            if (s_compiled.TryGetValue(pi, out var t)) return t;
            var label = pi.GetCustomAttribute<InjectAttribute>()?.Label;
            s_compiled.TryAdd(pi, (pi.SetValue, label));
            return (pi.SetValue, label);
        }
    }

    /// <summary>
    /// Cache for compiled field setter delegates.
    /// </summary>
    static class FieldInfoCache
    {
        private static readonly ConcurrentDictionary<FieldInfo, (Action<object, object>, Type?)> s_compiled = new();

        public static (Action<object, object> setValue, Type? label) Compile(this FieldInfo fi)
        {
            if (s_compiled.TryGetValue(fi, out var t)) return t;
            var label = fi.GetCustomAttribute<InjectAttribute>()?.Label;
            s_compiled.TryAdd(fi, (fi.SetValue, label));
            return (fi.SetValue, label);
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
        [MustUseReturnValue]
        public static WithBuilder RegisterGeneric(this Container container, Type genericType, MethodInfo creator)
        {
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));
            if (creator == null) throw new ArgumentNullException(nameof(creator));
            if (!genericType.IsGenericType) throw new ArgumentException($"{genericType.FullName} is not a generic type", nameof(genericType));
            if (!creator.IsStatic) throw new ArgumentException($"{creator.Name} is not static", nameof(creator));
            if (!creator.ReturnType.IsGenericType || creator.ReturnType.GetGenericTypeDefinition() != genericType) throw new ArgumentException($"the return type ({creator.ReturnType}) of {creator.Name} require to be the same as {nameof(genericType)} ({genericType})", nameof(creator));
            // TODO: Validate generic type constraints match between genericType and creator
            if (creator.GetGenericArguments().Length != genericType.GetGenericArguments().Length) throw new ArgumentException($"the method has different generic arguments: actual={creator.GetGenericArguments().Length} expected={genericType.GetGenericArguments()}", nameof(creator));
            var parameters = creator.GetParameters();
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(Container) || parameters[1].ParameterType != typeof(Type)) throw new ArgumentException("creator must have exact parameter of (Container, Type)", nameof(creator));
            return container.Register(genericType, GetInstanceCreator(creator));

            static Func<Container, Type, object> GetInstanceCreator(MethodInfo creator)
            {
                return (container, type) => creator.MakeGenericMethod(type.GetGenericArguments()).Invoke(null, new object[] { container, type });
            }
        }
    }
}
