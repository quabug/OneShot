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
        internal ConcurrentBag<IDisposable> DisposableInstances { get; set; } = new ConcurrentBag<IDisposable>();
        public void Dispose() => this.DisposeContainer();
    }

    [UsedImplicitly]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute
    {
        public Type? Label { get; }
        public InjectAttribute(Type? label = null) => Label = label;
    }
    
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

        private List<Func<Container, Type, object>> GetOrCreateResolver(Type type)
        {
            return _Container.GetOrCreateResolver(type);
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
            if (!labeledInstances.Any()) return this;
            var container = _Container.CreateChildContainer();
            foreach (var (instance, label) in labeledInstances) container.RegisterInstance(instance).AsSelf(label).AsBases(label).AsInterfaces(label);
            return new LifetimeBuilder(_Container, (_, contractType) => _Creator(container, contractType), _ConcreteType);
        }
    }

    public static class TypeCreatorRegister
    {
        private static readonly Dictionary<Container, Container> _containerParentMap = new Dictionary<Container, Container>();

        private static readonly Dictionary<Container, Dictionary<Type, List<Func<Container, Type, object>>>> _containerResolvers =
            new Dictionary<Container, Dictionary<Type, List<Func<Container, Type, object>>>>()
        ;

        public static Container CreateChildContainer(this Container container)
        {
            var child = new Container();
            _containerParentMap[child] = container;
            return child;
        }

        public static Container BeginScope(this Container container)
        {
            return CreateChildContainer(container);
        }

        internal static List<Func<Container, Type, object>> GetOrCreateResolver(this Container container, Type type)
        {
            return _containerResolvers.GetOrCreate(container).GetOrCreate(type);
        }

        public static object Resolve(this Container container, Type type, Type? label = null)
        {
            using (CircularCheck.Create())
            {
                var instance = ResolveImpl(container, type, label);
                if (instance == null) throw new ArgumentException($"{type.Name} have not been registered yet");
                return instance;
            }
        }

        public static T Resolve<T>(this Container container, Type? label = null)
        {
            return (T) container.Resolve(typeof(T), label);
        }

        private static object? ResolveImpl(this Container container, Type type, Type? label)
        {
            {
                var creatorKey = label == null ? type : label.CreateLabelType(type);
                var creator = container.FindFirstCreatorsInHierarchy(creatorKey);
                if (creator != null) return creator(container, type);
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var arrayArgument = container.ResolveGroupWithoutException(elementType);
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
                var creator = container.FindFirstCreatorsInHierarchy(creatorKey);
                if (creator != null) return creator(container, type);
            }

            return null;
        }

        private static IEnumerable<object> ResolveGroupWithoutException(this Container container, Type type)
        {
            using (CircularCheck.Create())
            {
                var creators = FindCreatorsInHierarchy(container, type).SelectMany(c => c);
                return creators.Select(creator => creator(container, type));
            }
        }

        public static IEnumerable<object> ResolveGroup(this Container container, Type type)
        {
            var objects = container.ResolveGroupWithoutException(type);
            if (objects.Any()) return objects;
            throw new ArgumentException($"{type.Name} have not been registered into containers.");
        }

        public static IEnumerable<T> ResolveGroup<T>(this Container container)
        {
            return container.ResolveGroup(typeof(T)).OfType<T>();
        }

        [MustUseReturnValue]
        public static WithBuilder Register(this Container container, Type type, Func<Container, Type, object> creator)
        {
            return new WithBuilder(container, creator, type);
        }

        [MustUseReturnValue]
        public static WithBuilder Register<T>(this Container container, Func<Container, Type, T> creator) where T : class
        {
            return container.Register(typeof(T), creator);
        }

        [MustUseReturnValue]
        public static WithBuilder Register<T>(this Container container)
        {
            return container.Register(typeof(T));
        }

        [MustUseReturnValue]
        public static WithBuilder Register(this Container container, Type type)
        {
            var ci = FindConstructorInfo(type);
            var (newFunc, parameters, labels) = ci.Compile();
            var arguments = new ThreadLocal<object[]>(() => new object[parameters.Length]);
            return container.Register(type, CreateInstance);

            object CreateInstance(Container resolveContainer, Type _)
            {
                CircularCheck.Check(type);
                var args = resolveContainer.ResolveParameterInfos(parameters, labels, arguments.Value);
                var instance = newFunc(args);
                if (instance is IDisposable disposable) resolveContainer.DisposableInstances.Add(disposable);
                return instance;
            }
        }

        [MustUseReturnValue]
        public static ResolverBuilder RegisterInstance<T>(this Container container, [NotNull] T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return new ResolverBuilder(container, instance.GetType(), (c, t) => instance);
        }

        // Unity/Mono: local function with default parameter is not supported by Mono?
        public static object CallFunc<T>(this Container container, T func) where T : Delegate
        {
            using (CircularCheck.Create())
            {
                var method = func.Method;
                if (method.ReturnType == typeof(void)) throw new ArgumentException($"{method.Name} must return void", nameof(func));
                return method.Invoke(func.Target, container);
            }
        }

        // Unity/Mono: local function with default parameter is not supported by Mono?
        public static void CallAction<T>(this Container container, T action) where T : Delegate
        {
            using (CircularCheck.Create())
            {
                action.Method.Invoke(action.Target, container);
            }
        }

        public static object Instantiate(this Container container, Type type)
        {
            using (CircularCheck.Create())
            {
                var ci = FindConstructorInfo(type);
                return ci.Invoke(container);
            }
        }

        [NotNull] public static T Instantiate<T>(this Container container)
        {
            return (T) container.Instantiate(typeof(T));
        }

        public static void DisposeContainer(this Container container)
        {
            var descendantsAndSelf = new HashSet<Container> { container };
            var irrelevantContainers = new HashSet<Container>();
            var containerHierarchy = new List<Container>();
            foreach (var check in _containerParentMap.Keys)
            {
                containerHierarchy.Clear();
                var containers = IsDescendant(check) ? descendantsAndSelf : irrelevantContainers;
                foreach (var c in containerHierarchy) containers.Add(c);
            }

            foreach (var disposed in descendantsAndSelf)
            {
                _containerResolvers.Remove(disposed);
                _containerParentMap.Remove(disposed);
                foreach (var instance in disposed.DisposableInstances) instance.Dispose();
                disposed.DisposableInstances = new ConcurrentBag<IDisposable>();
            }

            bool IsDescendant(Container check)
            {
                containerHierarchy.Add(check);
                if (!_containerParentMap.TryGetValue(check, out var parent)) return false;
                if (descendantsAndSelf.Contains(parent)) return true;
                if (irrelevantContainers.Contains(parent)) return false;
                return IsDescendant(parent);
            }
        }

        private static ConstructorInfo FindConstructorInfo(Type type)
        {
            var constructors = type.GetConstructors();
            ConstructorInfo? ci = null;
            if (constructors.Length == 1) ci = constructors[0];
            else if (constructors.Length > 1) ci = constructors.Single(c => c.GetCustomAttribute<InjectAttribute>() != null);
            if (ci == null) throw new NotSupportedException($"cannot found constructor of type {type}");
            return ci;
        }

        private static IEnumerable<List<Func<Container, Type, object>>> FindCreatorsInHierarchy(Container container, Type type)
        {
            for (;;)
            {
                if (_containerResolvers.TryGetValue(container, out var resolvers) && resolvers.TryGetValue(type, out var creators))
                    yield return creators;
                if (!_containerParentMap.TryGetValue(container, out container)) break;
            }
        }

        private static Func<Container, Type, object>? FindFirstCreatorsInHierarchy(this Container container, Type type)
        {
            for (;;)
            {
                if (_containerResolvers.TryGetValue(container, out var resolvers)
                    && resolvers.TryGetValue(type, out var creators)
                    && creators.Count > 0
                   ) return creators[0];
                if (!_containerParentMap.TryGetValue(container, out container)) break;
            }
            return null;
        }

        internal static object ResolveParameterInfo(this Container container, ParameterInfo parameter, Type? label = null)
        {
            var parameterType = parameter.ParameterType;
            var instance = container.ResolveImpl(parameterType, label);
            if (instance != null) return instance;
            return parameter.HasDefaultValue ? parameter.DefaultValue! : throw new ArgumentException($"cannot resolve parameter {parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}.{parameter.Name}");
        }

        internal static object[] ResolveParameterInfos(this Container container, ParameterInfo[] parameters, Type?[] labels, object?[]? arguments = null)
        {
            arguments ??= new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var label = labels[i];
                arguments[i] = ResolveParameterInfo(container, parameter, label);
            }
            return arguments!;
        }
    }

    public static class InjectExtension
    {
        private static readonly Dictionary<Type, TypeInjector> _injectors = new Dictionary<Type, TypeInjector>();

        public static void InjectAll(this Container container, object instance, Type instanceType)
        {
            _injectors.GetOrCreate(instanceType, () => new TypeInjector(instanceType)).InjectAll(container, instance);
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
            using (CircularCheck.Create())
            {
                foreach (var method in _methods) method.Invoke(instance, container);
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
        private static readonly ThreadLocal<HashSet<Type>> _circularCheckSet = new ThreadLocal<HashSet<Type>>(() => new HashSet<Type>());

        public static CircularCheck Create()
        {
            _circularCheckSet.Value.Clear();
            return new CircularCheck();
        }

        public static void Check(Type type)
        {
            if (_circularCheckSet.Value.Contains(type)) throw new CircularDependencyException($"circular dependency on {type.Name}");
            _circularCheckSet.Value.Add(type);
        }

        public void Dispose()
        {
            _circularCheckSet.Value.Clear();
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
        private static readonly ConcurrentDictionary<ConstructorInfo, (Func<object[], object>, ParameterInfo[], Type?[])> _compiled =
            new ConcurrentDictionary<ConstructorInfo, (Func<object[], object>, ParameterInfo[], Type?[])>();

        public static (Func<object[], object> newFunc, ParameterInfo[] parameters, Type?[] labels) Compile(this ConstructorInfo ci)
        {
            if (_compiled.TryGetValue(ci, out var t)) return t;
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
            _compiled.TryAdd(ci, (func, parameters, labels));
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
        private static readonly ConcurrentDictionary<MethodInfo, (Func<object, object[], object>, ParameterInfo[], Type?[])> _compiled =
            new ConcurrentDictionary<MethodInfo, (Func<object, object[], object>, ParameterInfo[], Type?[])>();

        public static (Func<object, object[], object> call, ParameterInfo[] parameters, Type?[] labels) Compile(this MethodInfo mi)
        {
            if (_compiled.TryGetValue(mi, out var t)) return t;
            var parameters = mi.GetParameters();
            var labels = parameters.Select(param => param.GetCustomAttribute<InjectAttribute>()?.Label).ToArray();
            _compiled.TryAdd(mi, (mi.Invoke, parameters, labels));
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
        private static readonly ConcurrentDictionary<PropertyInfo, (Action<object, object>, Type?)> _compiled =
            new ConcurrentDictionary<PropertyInfo, (Action<object, object>, Type?)>();

        public static (Action<object, object> setValue, Type? label) Compile(this PropertyInfo pi)
        {
            if (_compiled.TryGetValue(pi, out var t)) return t;
            var label = pi.GetCustomAttribute<InjectAttribute>()?.Label;
            _compiled.TryAdd(pi, (pi.SetValue, label));
            return (pi.SetValue, label);
        }
    }
    
    static class FieldInfoCache
    {
        private static readonly ConcurrentDictionary<FieldInfo, (Action<object, object>, Type?)> _compiled =
            new ConcurrentDictionary<FieldInfo, (Action<object, object>, Type?)>();

        public static (Action<object, object> setValue, Type? label) Compile(this FieldInfo fi)
        {
            if (_compiled.TryGetValue(fi, out var t)) return t;
            var label = fi.GetCustomAttribute<InjectAttribute>()?.Label;
            _compiled.TryAdd(fi, (fi.SetValue, label));
            return (fi.SetValue, label);
        }
    }
}