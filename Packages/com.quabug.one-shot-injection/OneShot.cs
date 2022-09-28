// MIT License

// Copyright (c) 2022 quabug
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
using JetBrains.Annotations;

namespace OneShot
{
    using Resolver = Func<Container, Type, object>;
    using ResolverList = IList<Func<Container, Type, object>>;

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute
    {
        public Type Label { get; }
        public InjectAttribute(Type label = null) => Label = label;
    }
    
    public interface ILabel<T> {}

    public sealed class Container : IDisposable
    {
        internal Container? Parent { get; set; }
        internal IList<Container> Children { get; } = new List<Container>();
        internal IDictionary<Type, ResolverList> Resolvers { get; } = new Dictionary<Type, ResolverList>();
        internal IList<IDisposable> DisposableInstances { get; } = new List<IDisposable>();
        
        public Container CreateChildContainer()
        {
            var child = new Container();
            child.Parent = this;
            Children.Add(child);
            return child;
        }
        
        public Container BeginScope()
        {
            return CreateChildContainer();
        }

#region Resolve

        public T Resolve<T>(Type? label = null)
        {
            return (T) Resolve(typeof(T), label);
        }

        public object Resolve(Type type, Type? label = null)
        {
            using (CircularCheck.Create())
            {
                var instance = ResolveImpl(type, label);
                return instance ?? throw new ArgumentException($"{type.Name} have not been registered yet");
            }
        }
        
        internal IEnumerable<object> ResolveGroupWithoutException(Type type)
        {
            using (CircularCheck.Create())
            {
                var creators = FindCreatorsInHierarchy(type);
                return creators.Select(creator => creator(this, type));
            }
        }

        public IEnumerable<object> ResolveGroup(Type type)
        {
            var objects = ResolveGroupWithoutException(type);
            if (objects.Any()) return objects;
            throw new ArgumentException($"{type.Name} have not been registered into containers.");
        }

        public IEnumerable<T> ResolveGroup<T>()
        {
            return ResolveGroup(typeof(T)).OfType<T>();
        }
        
        internal object ResolveParameterInfo(ParameterInfo parameter)
        {
            var label = parameter.GetCustomAttribute<InjectAttribute>()?.Label;
            var parameterType = parameter.ParameterType;
            var instance = ResolveImpl(parameterType, label);
            if (instance != null) return instance;
            return parameter.HasDefaultValue ? parameter.DefaultValue! : throw new ArgumentException($"cannot resolve parameter {parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}.{parameter.Name}");
        }

        internal object[] ResolveParameterInfos(ParameterInfo[] parameters, object[]? arguments = null)
        {
            arguments ??= new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) arguments[i] = ResolveParameterInfo(parameters[i]);
            return arguments;
        }
        
        private object? ResolveImpl(Type type, Type? label = null)
        {
            {
                var creatorKey = label == null ? type : label.CreateLabelType(type);
                var creator = FindFirstCreatorsInHierarchy(creatorKey);
                if (creator != null) return creator(this, type);
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var arrayArgument = ResolveGroupWithoutException(elementType);
                if (arrayArgument.Any())
                {
                    var source = arrayArgument.ToArray();
                    var dest = Array.CreateInstance(elementType, source.Length);
                    Array.Copy(source, dest, source.Length);
                    return dest;
                }
            }

            if (type.IsGenericType)
            {
                var generic = type.GetGenericTypeDefinition();
                var creatorKey = label == null ? generic : label.CreateLabelType(generic);
                var creator = FindFirstCreatorsInHierarchy(creatorKey);
                if (creator != null) return creator(this, type);
            }

            return null;
        }

#endregion

#region Register

        [MustUseReturnValue]
        public WithBuilder Register(Type type, Resolver creator)
        {
            return new WithBuilder(this, creator, type);
        }

        [MustUseReturnValue]
        public WithBuilder Register<T>(Func<Container, Type, T> creator) where T : class
        {
            return Register(typeof(T), creator);
        }

        [MustUseReturnValue]
        public WithBuilder Register<T>()
        {
            return Register(typeof(T));
        }

        [MustUseReturnValue]
        public WithBuilder Register(Type type)
        {
            var ci = FindConstructorInfo(type);
            return Register(type, CreateInstance());

            Resolver CreateInstance()
            {
                var parameters = ci.GetParameters();
                var arguments = new object[parameters.Length];
                return (resolveContainer, _) =>
                {
                    CircularCheck.Check(type);
                    var instance = ci.Invoke(resolveContainer.ResolveParameterInfos(parameters, arguments));
                    if (instance is IDisposable disposable) resolveContainer.DisposableInstances.Add(disposable);
                    return instance;
                };
            }
        }

        [MustUseReturnValue]
        public ResolverBuilder RegisterInstance<T>(T instance)
        {
            return new ResolverBuilder(this, instance.GetType(), (_, _) => instance);
        }

#endregion

#region Instantiate

        // Unity/Mono: local function with default parameter is not supported by Mono?
        public object CallFunc<T>(T func) where T : Delegate
        {
            using (CircularCheck.Create())
            {
                var method = func.Method;
                if (method.ReturnType == typeof(void)) throw new ArgumentException($"{method.Name} must return void", nameof(func));
                return method.Invoke(func.Target, ResolveParameterInfos(method.GetParameters()));
            }
        }

        // Unity/Mono: local function with default parameter is not supported by Mono?
        public void CallAction<T>(T action) where T : Delegate
        {
            using (CircularCheck.Create())
            {
                var method = action.Method;
                method.Invoke(action.Target, ResolveParameterInfos(method.GetParameters()));
            }
        }

        public object Instantiate(Type type)
        {
            using (CircularCheck.Create())
            {
                var ci = FindConstructorInfo(type);
                var parameters = ci.GetParameters();
                return ci.Invoke(ResolveParameterInfos(parameters));
            }
        }

        public T Instantiate<T>()
        {
            return (T) Instantiate(typeof(T));
        }

#endregion

        public void Dispose()
        {
            DisposeSelfAndChildren();
            RemoveFromParent();
        }

        private void DisposeSelfAndChildren()
        {
            foreach (var child in Children) child.DisposeSelfAndChildren();
            Children.Clear();
            foreach (var instance in DisposableInstances) instance.Dispose();
            DisposableInstances.Clear();
            Resolvers.Clear();
            Parent = null;
        }

        private void RemoveFromParent()
        {
            Parent?.Children.Remove(this);
        }
        
        internal IEnumerable<Resolver> FindCreatorsInHierarchy(Type type)
        {
            var container = this;
            while (container != null)
            {
                if (container.Resolvers.TryGetValue(type, out var creators))
                    foreach (var creator in creators) yield return creator;
                container = container.Parent;
            }
        }

        internal Resolver? FindFirstCreatorsInHierarchy(Type type)
        {
            var container = this;
            while (container != null)
            {
                if (container.Resolvers.TryGetValue(type, out var creators) && creators.Count > 0) return creators[0];
                container = container.Parent;
            }
            return null;
        }

        internal ConstructorInfo FindConstructorInfo(Type type)
        {
            var constructors = type.GetConstructors();
            ConstructorInfo ci = null;
            if (constructors.Length == 1) ci = constructors[0];
            else if (constructors.Length > 1) ci = constructors.Single(c => c.GetCustomAttribute<InjectAttribute>() != null);
            if (ci == null) throw new NotSupportedException($"cannot found constructor of type {type}");
            return ci;
        }

        [ItemNotNull] internal ResolverList GetOrCreateResolverList(Type type)
        {
            return Resolvers.GetOrCreate(type, () => new List<Resolver>());
        }
    }

    public class ResolverBuilder
    {
        protected readonly Container _Container;
        protected readonly Resolver _Creator;
        protected readonly Type _ConcreteType;

        public ResolverBuilder(Container container, Type concreteType, Func<Container, Type, object> creator)
        {
            _Container = container;
            _Creator = creator;
            _ConcreteType = concreteType;
        }

        public ResolverBuilder As(Type contractType, Type? label = null)
        {
            if (!contractType.IsAssignableFrom(_ConcreteType)) throw new ArgumentException($"concreteType({_ConcreteType}) must derived from contractType({contractType})", nameof(contractType));
            if (label != null) contractType = label.CreateLabelType(contractType);
            var resolver = _Container.GetOrCreateResolverList(contractType);
            if (!resolver.Contains(_Creator)) resolver.Insert(0, _Creator);
            return this;
        }

        public ResolverBuilder As<T>(Type? label = null)
        {
            return As(typeof(T), label);
        }

        public ResolverBuilder AsSelf(Type? label = null)
        {
            return As(_ConcreteType, label);
        }

        public ResolverBuilder AsInterfaces(Type? label = null)
        {
            foreach (var @interface in _ConcreteType.GetInterfaces()) As(@interface, label);
            return this;
        }

        public ResolverBuilder AsBases(Type? label = null)
        {
            var baseType = _ConcreteType.BaseType;
            while (baseType != null && baseType != typeof(Object) && baseType != typeof(ValueType))
            {
                As(baseType, label);
                baseType = baseType.BaseType;
            }
            return this;
        }
    }

    public class LifetimeBuilder : ResolverBuilder
    {
        public LifetimeBuilder(Container container, Resolver creator, Type concreteType) : base(container, concreteType, creator) {}

        [MustUseReturnValue]
        public ResolverBuilder Transient()
        {
            return this;
        }

        [MustUseReturnValue]
        public ResolverBuilder Singleton()
        {
            var lazyValue = new Lazy<object>(() => _Creator(_Container, _ConcreteType));
            return new ResolverBuilder(_Container, _ConcreteType, (_, _) => lazyValue.Value);
        }

        [MustUseReturnValue]
        public ResolverBuilder Scope()
        {
            var lazyValue = new Lazy<object>(() => _Creator(_Container, _ConcreteType));
            return new ResolverBuilder(_Container, _ConcreteType, ResolveScopeInstance);

            object ResolveScopeInstance(Container container, Type contractType)
            {
                if (container == _Container) return lazyValue.Value;
                // register on runtime should be thread safe?
                container.Register(_ConcreteType, _Creator).Scope().As(contractType);
                return container.Resolve(contractType);
            }
        }
    }

    public class WithBuilder : LifetimeBuilder
    {
        public WithBuilder(Container container, Resolver creator, Type concreteType) : base(container, creator, concreteType) {}
        
        [NotNull, MustUseReturnValue]
        public LifetimeBuilder With([NotNull, ItemNotNull] params object[] instances)
        {
            return WithImpl(instances.Select(instance => (instance, (Type)null)));
        }
        
        [NotNull, MustUseReturnValue]
        public LifetimeBuilder With(params (object instance, Type label)[] labeledInstances)
        {
            return WithImpl(labeledInstances);
        }

        private LifetimeBuilder WithImpl(IEnumerable<(object instance, Type label)> labeledInstances)
        {
            if (labeledInstances == null || !labeledInstances.Any()) return this;
            var container = _Container.CreateChildContainer();
            foreach (var (instance, label) in labeledInstances) container.RegisterInstance(instance).AsSelf(label).AsBases(label).AsInterfaces(label);
            return new LifetimeBuilder(_Container, (_, contractType) => _Creator(container, contractType), _ConcreteType);
        }
    }

    public static class InjectExtension
    {
        private static readonly ConcurrentDictionary<Type, TypeInjector> _injectors = new ConcurrentDictionary<Type, TypeInjector>();

        public static void InjectAll(this Container container, object instance, Type instanceType)
        {
            _injectors.GetOrCreate(instanceType, () => new TypeInjector(instanceType)).InjectAll(container, instance);
        }

        public static void InjectAll<T>(this Container container, T instance)
        {
            InjectAll(container, instance, instance.GetType());
        }
    }

    class TypeInjector
    {
        private readonly Type _type;
        private readonly IReadOnlyList<FieldInfo> _fields;
        private readonly IReadOnlyList<PropertyInfo> _properties;
        private readonly IReadOnlyList<MethodInfo> _methods;

        public TypeInjector(Type type)
        {
            var fields = new List<FieldInfo>();
            var properties = new List<PropertyInfo>();
            var methods = new List<MethodInfo>();
            
            _type = type;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var member in type.GetMembers(flags).Where(mi => mi.GetCustomAttribute<InjectAttribute>() != null))
            {
                switch (member)
                {
                    case FieldInfo field:
                        fields.Add(field);
                        break;
                    case PropertyInfo property:
                        if (property.CanWrite) properties.Add(property);
                        else throw new NotSupportedException($"cannot inject on read-only property {property.DeclaringType.Name}.{property.Name}");
                        break;
                    case MethodInfo method: // TODO: check method validation
                        methods.Add(method);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            _fields = fields;
            _properties = properties;
            _methods = methods;
        }

        public void InjectFields(Container container, object instance)
        {
            CheckInstanceType(instance);
            foreach (var field in _fields) field.SetValue(instance, container.Resolve(field.FieldType));
        }

        public void InjectProperties(Container container, object instance)
        {
            CheckInstanceType(instance);
            foreach (var property in _properties) property.SetValue(instance, container.Resolve(property.PropertyType));
        }

        public void InjectMethods(Container container, object instance)
        {
            using (CircularCheck.Create())
            {
                foreach (var method in _methods) method.Invoke(instance, container.ResolveParameterInfos(method.GetParameters()));
            }
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

    internal static class DictionaryExtension
    {
        public static TValue GetOrCreate<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key
        ) where TValue : new()
        {
            return dictionary.GetOrCreate(key, () => new TValue());
        }

        public static TValue GetOrCreate<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
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

    [Serializable]
    public class CircularDependencyException : Exception
    {
        public CircularDependencyException() {}
        public CircularDependencyException(string message) : base(message) {}
        public CircularDependencyException(string message, Exception inner) : base(message, inner) {}
        protected CircularDependencyException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }

    readonly struct CircularCheck : IDisposable
    {
        [ThreadStatic] private static HashSet<Type> _circularCheckSet;

        public static CircularCheck Create()
        {
            if (_circularCheckSet == null) _circularCheckSet = new HashSet<Type>();
            _circularCheckSet.Clear();
            return new CircularCheck();
        }

        public static void Check(Type type)
        {
            if (_circularCheckSet.Contains(type)) throw new CircularDependencyException($"circular dependency on {type.Name}");
            _circularCheckSet.Add(type);
        }

        public void Dispose()
        {
            _circularCheckSet.Clear();
        }
    }

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
}