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
using System.Threading;
using JetBrains.Annotations;
#if !ENABLE_IL2CPP
using System.Linq.Expressions;
#endif

namespace OneShot
{
    public sealed class Container : IDisposable
    {
        internal Container? Parent { get; private set; }

        internal ConcurrentDictionary<Type, ConcurrentStack<Func<Container, Type, object>>> Resolvers => _resolvers!;
        
        private ConcurrentDictionary<Type, ConcurrentStack<Func<Container, Type, object>>>? _resolvers
            = new ConcurrentDictionary<Type, ConcurrentStack<Func<Container, Type, object>>>();
        private ConcurrentBag<IDisposable>? _disposableInstances = new ConcurrentBag<IDisposable>();
        private ConcurrentBag<Container>? _children = new ConcurrentBag<Container>();
        
        // TODO: remove container from parent?
        public void Dispose()
        {
            if (_children != null) foreach (var child in _children) child.Dispose();
            _children = null;
            if (_disposableInstances != null) foreach (var instance in _disposableInstances) instance.Dispose();
            _disposableInstances = null;
            Parent = null;
            _resolvers?.Clear();
            _resolvers = null!;
        }

        #region scope
        
        public Container CreateChildContainer()
        {
            var child =  new Container();
            child.Parent = this;
            _children!.Add(child);
            return child;
        }

        public Container BeginScope()
        {
            return CreateChildContainer();
        }

        #endregion

        #region Resolve

        public object Resolve(Type type, Type? label = null)
        {
            var instance = ResolveImpl(type, label);
            if (instance == null) throw new ArgumentException($"{type.Name} have not been registered yet");
            return instance;
        }

        public T Resolve<T>(Type? label = null)
        {
            return (T) Resolve(typeof(T), label);
        }

        public IEnumerable<T> ResolveGroup<T>()
        {
            return ResolveGroup(typeof(T)).OfType<T>();
        }

        private object? ResolveImpl(Type type, Type? label)
        {
            {
                var creatorKey = label == null ? type : label.CreateLabelType(type);
                var creator = FindFirstCreatorsInHierarchy(this, creatorKey);
                if (creator != null) return creator(this, type);
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var arrayArgument = ResolveGroup(elementType);
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
                var creator = FindFirstCreatorsInHierarchy(this, creatorKey);
                if (creator != null) return creator(this, type);
            }

            return null;
        }

        public IEnumerable<object> ResolveGroup(Type type)
        {
            var creators = FindCreatorsInHierarchy(this, type).SelectMany(c => c);
            return creators.Select(creator => creator(this, type));
        }

        #endregion

        #region Register

        [MustUseReturnValue]
        public WithBuilder Register(Type type, Func<Container, Type, object> creator)
        {
            return new WithBuilder(this, creator, type);
        }

        [MustUseReturnValue]
        public WithBuilder Register(Type type)
        {
            var ci = FindConstructorInfo(type);
            var (newFunc, parameters, labels) = ci.Compile();
            var arguments = new ThreadLocal<object[]>(() => new object[parameters.Length]);
            return Register(type, CreateInstance);

            object CreateInstance(Container resolveContainer, Type _)
            {
                using var check = new CircularCheck(type);
                var args = resolveContainer.ResolveParameterInfos(parameters, labels, arguments.Value);
                var instance = newFunc(args);
                if (instance is IDisposable disposable) resolveContainer._disposableInstances!.Add(disposable);
                return instance;
            }
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
        public ResolverBuilder RegisterInstance<T>([NotNull] T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return new ResolverBuilder(this, instance.GetType(), (c, t) => instance);
        }

        #endregion

        #region Call

        // Unity/Mono: local function with default parameter is not supported by Mono?
        public object CallFunc<T>(T func) where T : Delegate
        {
            var method = func.Method;
            if (method.ReturnType == typeof(void)) throw new ArgumentException($"{method.Name} must return void", nameof(func));
            return method.Invoke(func.Target, this);
        }

        // Unity/Mono: local function with default parameter is not supported by Mono?
        public void CallAction<T>(T action) where T : Delegate
        {
            action.Method.Invoke(action.Target, this);
        }

        #endregion

        #region Instantiate

        public object Instantiate(Type type)
        {
            return FindConstructorInfo(type).Invoke(this);
        }

        [NotNull] public T Instantiate<T>()
        {
            return (T) Instantiate(typeof(T));
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

        private static IEnumerable<ConcurrentStack<Func<Container, Type, object>>> FindCreatorsInHierarchy(Container container, Type type)
        {
            var current = container;
            while (current != null)
            {
                if (current.Resolvers.TryGetValue(type, out var creators))
                    yield return creators;
                current = current.Parent;
            }
        }

        private static Func<Container, Type, object>? FindFirstCreatorsInHierarchy(Container container, Type type)
        {
            var current = container;
            while (current != null)
            {
                if (current.Resolvers.TryGetValue(type, out var creators) && creators.TryPeek(out var creator)) return creator;
                current = current.Parent;
            }
            return null;
        }

        private object ResolveParameterInfo(ParameterInfo parameter, Type? label = null)
        {
            var parameterType = parameter.ParameterType;
            var instance = ResolveImpl(parameterType, label);
            if (instance != null) return instance;
            return parameter.HasDefaultValue ? parameter.DefaultValue! : throw new ArgumentException($"cannot resolve parameter {parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}.{parameter.Name}");
        }

        internal object[] ResolveParameterInfos(ParameterInfo[] parameters, Type?[] labels, object?[]? arguments = null)
        {
            arguments ??= new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var label = labels[i];
                arguments[i] = ResolveParameterInfo(parameter, label);
            }
            return arguments!;
        }
    }

    [UsedImplicitly]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute
    {
        public Type? Label { get; }
        public InjectAttribute(Type? label = null) => Label = label;
    }
    
    // ReSharper disable once UnusedTypeParameter
    public interface ILabel<T> {}

    public class ResolverBuilder
    {
        protected readonly Container _Container;
        protected readonly Func<Container, Type, object> _Creator;
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
            var resolver = GetOrCreateResolver(contractType);
            if (!resolver.Contains(_Creator)) resolver.Push(_Creator);
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

        private ConcurrentStack<Func<Container, Type, object>> GetOrCreateResolver(Type type)
        {
            if (!_Container.Resolvers.TryGetValue(type, out var resolvers))
            {
                resolvers = new ConcurrentStack<Func<Container, Type, object>>();
                _Container.Resolvers[type] = resolvers;
            }
            return resolvers;
        }
    }

    public class LifetimeBuilder : ResolverBuilder
    {
        public LifetimeBuilder(Container container, Func<Container, Type, object> creator, Type concreteType) : base(container, concreteType, creator) {}

        [MustUseReturnValue]
        public ResolverBuilder Transient()
        {
            return this;
        }

        [MustUseReturnValue]
        public ResolverBuilder Singleton()
        {
            var lazyValue = new Lazy<object>(() => _Creator(_Container, _ConcreteType));
            return new ResolverBuilder(_Container, _ConcreteType, (container, contractType) => lazyValue.Value);
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
        public WithBuilder(Container container, Func<Container, Type, object> creator, Type concreteType) : base(container, creator, concreteType) {}
        
        [MustUseReturnValue]
        public LifetimeBuilder With(params object[] instances)
        {
            return WithImpl(instances.Select(instance => (instance, (Type?)null)));
        }
        
        [MustUseReturnValue]
        public LifetimeBuilder With(params (object instance, Type? label)[] labeledInstances)
        {
            return WithImpl(labeledInstances);
        }

        private LifetimeBuilder WithImpl(IEnumerable<(object instance, Type? label)> labeledInstances)
        {
            var container = _Container.CreateChildContainer();
            foreach (var (instance, label) in labeledInstances) container.RegisterInstance(instance).AsSelf(label).AsBases(label).AsInterfaces(label);
            return new LifetimeBuilder(_Container, (_, contractType) => _Creator(container, contractType), _ConcreteType);
        }
    }

    public static class InjectExtension
    {
        private static readonly Dictionary<Type, TypeInjector> _INJECTORS = new Dictionary<Type, TypeInjector>();

        public static void InjectAll(this Container container, object instance, Type instanceType)
        {
            _INJECTORS.GetOrCreate(instanceType, () => new TypeInjector(instanceType)).InjectAll(container, instance);
        }

        public static void InjectAll<T>(this Container container, [NotNull] T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            InjectAll(container, instance, instance.GetType());
        }
    }
    
    class TypeInjector
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
                    case MethodInfo method: // TODO: check method validation
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

    internal static class DictionaryExtension
    {
        [NotNull] public static TValue GetOrCreate<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            [NotNull] TKey key
        ) where TValue : new()
        {
            return dictionary.GetOrCreate(key, () => new TValue());
        }

        [NotNull] public static TValue GetOrCreate<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            [NotNull] TKey key,
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
        private static ThreadLocal<HashSet<Type>> _CIRCULAR_CHECK_SET = new ThreadLocal<HashSet<Type>>(() => new HashSet<Type>());

        private readonly Type _type;
        
        public CircularCheck(Type type)
        {
            if (_CIRCULAR_CHECK_SET.Value.Contains(type)) throw new CircularDependencyException($"circular dependency on {type.Name}");
            _CIRCULAR_CHECK_SET.Value.Add(type);
            _type = type;
        }

        public void Dispose()
        {
            _CIRCULAR_CHECK_SET.Value.Remove(_type);
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

    static class ConstructorInfoCache
    {
        private static readonly ConcurrentDictionary<ConstructorInfo, (Func<object[], object>, ParameterInfo[], Type?[])> _COMPILED =
            new ConcurrentDictionary<ConstructorInfo, (Func<object[], object>, ParameterInfo[], Type?[])>();

        public static (Func<object[], object> newFunc, ParameterInfo[] parameters, Type?[] labels) Compile(this ConstructorInfo ci)
        {
            if (_COMPILED.TryGetValue(ci, out var t)) return t;
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
            var func = (Func<object[], object>) lambda.Compile();
#endif
            _COMPILED.TryAdd(ci, (func, parameters, labels));
            return (func, parameters, labels);
        }

        public static object Invoke(this ConstructorInfo ci, Container container)
        {
            var (newFunc, parameters, labels) = ci.Compile();
            var args = container.ResolveParameterInfos(parameters, labels);
            return newFunc(args);
        }
    }
    
    static class MethodInfoCache
    {
        private static readonly ConcurrentDictionary<MethodInfo, (Func<object, object[], object>, ParameterInfo[], Type?[])> _COMPILED =
            new ConcurrentDictionary<MethodInfo, (Func<object, object[], object>, ParameterInfo[], Type?[])>();

        public static (Func<object, object[], object> call, ParameterInfo[] parameters, Type?[] labels) Compile(this MethodInfo mi)
        {
            if (_COMPILED.TryGetValue(mi, out var t)) return t;
            var parameters = mi.GetParameters();
            var labels = parameters.Select(param => param.GetCustomAttribute<InjectAttribute>()?.Label).ToArray();
            _COMPILED.TryAdd(mi, (mi.Invoke, parameters, labels));
            return (mi.Invoke, parameters, labels);
        }

        public static object Invoke(this MethodInfo mi, object target, Container container)
        {
            var (call, parameters, labels) = mi.Compile();
            var args = container.ResolveParameterInfos(parameters, labels);
            return call(target, args);
        }
    }
    
    static class PropertyInfoCache
    {
        private static readonly ConcurrentDictionary<PropertyInfo, (Action<object, object>, Type?)> _COMPILED =
            new ConcurrentDictionary<PropertyInfo, (Action<object, object>, Type?)>();

        public static (Action<object, object> setValue, Type? label) Compile(this PropertyInfo pi)
        {
            if (_COMPILED.TryGetValue(pi, out var t)) return t;
            var label = pi.GetCustomAttribute<InjectAttribute>()?.Label;
            _COMPILED.TryAdd(pi, (pi.SetValue, label));
            return (pi.SetValue, label);
        }
    }
    
    static class FieldInfoCache
    {
        private static readonly ConcurrentDictionary<FieldInfo, (Action<object, object>, Type?)> _COMPILED =
            new ConcurrentDictionary<FieldInfo, (Action<object, object>, Type?)>();

        public static (Action<object, object> setValue, Type? label) Compile(this FieldInfo fi)
        {
            if (_COMPILED.TryGetValue(fi, out var t)) return t;
            var label = fi.GetCustomAttribute<InjectAttribute>()?.Label;
            _COMPILED.TryAdd(fi, (fi.SetValue, label));
            return (fi.SetValue, label);
        }
    }
}