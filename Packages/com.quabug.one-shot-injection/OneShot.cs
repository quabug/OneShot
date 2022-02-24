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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace OneShot
{
    public sealed class Container : IDisposable
    {
        public void Dispose() => this.DisposeContainer();
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute {}

    public class ResolverBuilder
    {
        [NotNull] protected readonly Container _Container;
        [NotNull] protected readonly Func<object> _Creator;
        [NotNull] protected readonly Type _Type;

        public ResolverBuilder([NotNull] Container container, [NotNull] Func<object> creator, [NotNull] Type type)
        {
            _Container = container;
            _Creator = creator;
            _Type = type;
        }

        [NotNull] public ResolverBuilder As([NotNull] Type type)
        {
            if (!type.IsAssignableFrom(_Type)) throw new ArgumentException();
            var resolver = GetOrCreateResolver(type);
            if (!resolver.Contains(_Creator)) resolver.Insert(0, _Creator);
            return this;
        }

        [NotNull] public ResolverBuilder As<T>()
        {
            return As(typeof(T));
        }

        [NotNull] public ResolverBuilder AsSelf()
        {
            return As(_Type);
        }

        [NotNull] public ResolverBuilder AsInterfaces()
        {
            foreach (var @interface in _Type.GetInterfaces()) As(@interface);
            return this;
        }

        private List<Func<object>> GetOrCreateResolver(Type type)
        {
            return _Container.GetOrCreateResolver(type);
        }
    }

    public class LifetimeBuilder : ResolverBuilder
    {
        public LifetimeBuilder([NotNull] Container container, [NotNull] Func<object> creator, [NotNull] Type type) : base(container, creator, type) {}

        public ResolverBuilder Transient()
        {
            return this;
        }

        public ResolverBuilder Singleton()
        {
            var lazyValue = new Lazy<object>(_Creator);
            return new ResolverBuilder(_Container, () => lazyValue.Value, _Type);
        }

        public ResolverBuilder Scope()
        {
            throw new NotImplementedException();
        }
    }

    public static class TypeCreatorRegister
    {
        private static readonly Dictionary<Container, Container> _containerParentMap =
            new Dictionary<Container, Container>()
        ;

        private static readonly Dictionary<Container, Dictionary<Type, List<Func<object>>>> _containerResolvers =
            new Dictionary<Container, Dictionary<Type, List<Func<object>>>>()
        ;

        [NotNull] public static Container CreateChildContainer([NotNull] this Container container)
        {
            var child = new Container();
            _containerParentMap[child] = container;
            return child;
        }

        [NotNull] internal static List<Func<object>> GetOrCreateResolver(this Container container, Type type)
        {
            return _containerResolvers.GetOrCreate(container).GetOrCreate(type);
        }

        [NotNull] public static object Resolve([NotNull] this Container container, [NotNull] Type type)
        {
            var creator = container.FindFirstCreatorsInHierarchy(type);
            return creator != null ? creator() : throw new ArgumentException($"{type.Name} have not been registered yet");
        }

        [NotNull] public static T Resolve<T>([NotNull] this Container container)
        {
            return (T) container.Resolve(typeof(T));
        }

        [NotNull] private static IEnumerable<object> ResolveGroupWithoutException([NotNull] this Container container, Type type)
        {
            var creators = FindCreatorsInHierarchy(container, type).SelectMany(creators => creators);
            return creators.Select(creator => creator());
        }

        [NotNull] public static IEnumerable<object> ResolveGroup([NotNull] this Container container, Type type)
        {
            var objects = container.ResolveGroupWithoutException(type);
            if (objects.Any()) return objects;
            throw new ArgumentException($"{type.Name} have not been registered into containers.");
        }

        [NotNull] public static IEnumerable<T> ResolveGroup<T>([NotNull] this Container container)
        {
            return container.ResolveGroup(typeof(T)).OfType<T>();
        }

        public static LifetimeBuilder Register([NotNull] this Container container, [NotNull] Type type, [NotNull] Func<object> creator)
        {
            return new LifetimeBuilder(container, creator, type);
        }

        public static LifetimeBuilder Register<T>([NotNull] this Container container, [NotNull] Func<T> creator) where T : class
        {
            return container.Register(typeof(T), creator);
        }

        public static LifetimeBuilder Register<T>([NotNull] this Container container)
        {
            return container.Register(typeof(T));
        }

        public static LifetimeBuilder Register([NotNull] this Container container, [NotNull] Type type)
        {
            var ci = FindConstructorInfo(type);
            return container.Register(type, CreateInstance(container, ci));
        }

        public static ResolverBuilder RegisterInstance([NotNull] this Container container, [NotNull] Type type, [NotNull] object instance)
        {
            return container.Register(instance.GetType(), () => instance).As(type);
        }

        public static ResolverBuilder RegisterInstance<T>([NotNull] this Container container, [NotNull] T instance)
        {
            return container.RegisterInstance(typeof(T), instance);
        }

        public static object Call([NotNull] this Container container, Delegate func)
        {
            var invoke = func.GetType().GetMethod("Invoke");
            if (invoke.ReturnType == typeof(void)) throw new ArgumentException();
            return invoke.Invoke(func, container.ResolveParameterInfos(invoke.GetParameters()));
        }

        public static TReturn Call<TReturn>([NotNull] this Container container, Delegate func)
        {
            var invoke = func.GetType().GetMethod("Invoke");
            if (!typeof(TReturn).IsAssignableFrom(invoke.ReturnType)) throw new ArgumentException();
            return (TReturn) invoke.Invoke(func, container.ResolveParameterInfos(invoke.GetParameters()));
        }

        public static void CallAction([NotNull] this Container container, Delegate action)
        {
            var invoke = action.GetType().GetMethod("Invoke");
            invoke.Invoke(action, container.ResolveParameterInfos(invoke.GetParameters()));
        }

        public static object Instantiate([NotNull] this Container container, Type type)
        {
            var ci = FindConstructorInfo(type);
            var parameters = ci.GetParameters();
            return ci.Invoke(container.ResolveParameterInfos(parameters));
        }

        public static T Instantiate<T>([NotNull] this Container container)
        {
            return (T) container.Instantiate(typeof(T));
        }

        public static void DisposeContainer([NotNull] this Container container)
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
            ConstructorInfo ci = null;
            if (constructors.Length == 1) ci = constructors[0];
            else if (constructors.Length > 1) ci = constructors.Single(c => c.GetCustomAttribute<InjectAttribute>() != null);
            if (ci == null) throw new NotSupportedException();
            return ci;
        }

        private static IEnumerable<List<Func<object>>> FindCreatorsInHierarchy(Container container, Type type)
        {
            for (;;)
            {
                if (_containerResolvers.TryGetValue(container, out var resolvers) && resolvers.TryGetValue(type, out var creators))
                    yield return creators;
                if (!_containerParentMap.TryGetValue(container, out container)) break;
            }
        }

        private static Func<object> FindFirstCreatorsInHierarchy(this Container container, Type type)
        {
            do
            {
                var creators = container.GetOrCreateResolver(type);
                if (creators.Count > 0) return creators[0];
            } while (_containerParentMap.TryGetValue(container, out container));
            return null;
        }

        // TODO: check circular dependency
        internal static Func<object> CreateInstance(this Container container, ConstructorInfo ci)
        {
            var parameters = ci.GetParameters();
            var arguments = new object[parameters.Length];
            return () => ci.Invoke(container.ResolveParameterInfos(parameters, arguments));
        }

        internal static object ResolveParameterInfo(this Container container, ParameterInfo parameter)
        {
            var creator = container.FindFirstCreatorsInHierarchy(parameter.ParameterType);
            if (creator != null) return creator();

            if (parameter.ParameterType.IsArray)
            {
                var elementType = parameter.ParameterType.GetElementType();
                var arrayArgument = container.ResolveGroupWithoutException(elementType);
                if (arrayArgument.Any())
                {
                    var source = arrayArgument.ToArray();
                    var dest = Array.CreateInstance(elementType, source.Length);
                    Array.Copy(source, dest, source.Length);
                    return dest;
                }
            }
            return parameter.HasDefaultValue ? parameter.DefaultValue : throw new ArgumentException($"cannot resolve parameter {parameter.Member.DeclaringType?.Name}.{parameter.Member.Name}.{parameter.Name}");
        }

        internal static object[] ResolveParameterInfos(this Container container, ParameterInfo[] parameters, object[] arguments = null)
        {
            arguments ??= new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) arguments[i] = ResolveParameterInfo(container, parameters[i]);
            return arguments;
        }
    }

    public static class InjectExtension
    {
        private static readonly Dictionary<Type, TypeInjector> _injectors = new Dictionary<Type, TypeInjector>();

        public static void InjectAll([NotNull] this Container container, object instance, Type instanceType)
        {
            _injectors.GetOrCreate(instanceType, () => new TypeInjector(instanceType)).InjectAll(container, instance);
        }

        public static void InjectAll<T>([NotNull] this Container container, T instance)
        {
            InjectAll(container, instance, typeof(T));
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
            foreach (var field in _fields) field.SetValue(instance, container.Resolve(field.FieldType));
        }

        public void InjectProperties(Container container, object instance)
        {
            CheckInstanceType(instance);
            foreach (var property in _properties) property.SetValue(instance, container.Resolve(property.PropertyType));
        }

        public void InjectMethods(Container container, object instance)
        {
            CheckInstanceType(instance);
            foreach (var method in _methods) method.Invoke(instance, container.ResolveParameterInfos(method.GetParameters()));
        }

        public void InjectAll(Container container, object instance)
        {
            InjectFields(container, instance);
            InjectProperties(container, instance);
            InjectMethods(container, instance);
        }

        private void CheckInstanceType(object instance)
        {
            if (instance.GetType() != _type) throw new ArgumentException();
        }
    }

    internal static class DictionaryExtension
    {
        [NotNull] public static TValue GetOrCreate<TKey, TValue>(
            [NotNull] this Dictionary<TKey, TValue> dictionary,
            [NotNull] TKey key
        ) where TValue : new()
        {
            return dictionary.GetOrCreate(key, () => new TValue());
        }

        [NotNull] public static TValue GetOrCreate<TKey, TValue>(
            [NotNull] this Dictionary<TKey, TValue> dictionary,
            [NotNull] TKey key,
            [NotNull] Func<TValue> newValue
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
}