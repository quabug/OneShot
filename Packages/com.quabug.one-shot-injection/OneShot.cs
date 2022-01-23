using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace OneShot
{
    public sealed class Container {}

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute {}

    public static class TypeCreatorRegister
    {
        private readonly struct Creator
        {
            [NotNull] public readonly Func<object> CreatorFunc;
            public Creator([NotNull] Func<object> creatorFunc) => CreatorFunc = creatorFunc;
        }

        private static readonly Dictionary<Container, Container> _containerParentMap =
            new Dictionary<Container, Container>()
        ;

        private static readonly Dictionary<Container, Dictionary<Type, Creator>> _containerResolvers =
            new Dictionary<Container, Dictionary<Type, Creator>>()
        ;

        [NotNull] public static Container CreateChildContainer([NotNull] this Container container)
        {
            var child = new Container();
            _containerParentMap[child] = container;
            return child;
        }

        [NotNull] public static object Resolve([NotNull] this Container container, [NotNull] Type type)
        {
            return _containerResolvers[container][type].CreatorFunc();
        }

        [NotNull] public static T Resolve<T>([NotNull] this Container container)
        {
            return (T) container.Resolve(typeof(T));
        }

        public static void Register(
            [NotNull] this Container container,
            [NotNull] Type type,
            [NotNull] Func<object> creator
        )
        {
            var resolvers = _containerResolvers.GetOrCreate(container, () => new Dictionary<Type, Creator>());
            resolvers.Add(type, new Creator(creator));
        }

        public static void Register<T>(
            [NotNull] this Container container,
            [NotNull] Func<T> creator
        )
        {
            container.Register(typeof(T), () => creator());
        }

        public static void RegisterInstance([NotNull] this Container container, [NotNull] Type type, [NotNull] object instance)
        {
            container.Register(type, () => instance);
        }

        public static void RegisterSingleton([NotNull] this Container container, [NotNull] Type type)
        {
            var ci = FindConstructorInfo(type);
            var lazyValue = new Lazy<object>(CreateInstance(container, ci));
            container.Register(type, () => lazyValue.Value);
        }

        public static void RegisterTransient([NotNull] this Container container, [NotNull] Type type)
        {
            var ci = FindConstructorInfo(type);
            container.Register(type, CreateInstance(container, ci));
        }

        public static void RegisterInstance<T>([NotNull] this Container container, [NotNull] T instance)
        {
            container.RegisterInstance(typeof(T), instance);
        }

        public static void RegisterSingleton<T>([NotNull] this Container container)
        {
            container.RegisterSingleton(typeof(T));
        }

        public static void RegisterTransient<T>([NotNull] this Container container)
        {
            container.RegisterTransient(typeof(T));
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

        private static Creator? FindCreatorInHierarchy(Container container, Type type)
        {
            if (_containerResolvers[container].TryGetValue(type, out var creator)) return creator;
            if (!_containerParentMap.TryGetValue(container, out var parent)) return null;
            return FindCreatorInHierarchy(parent, type);
        }

        // TODO: check circular dependency
        private static Func<object> CreateInstance(Container container, ConstructorInfo ci)
        {
            var parameters = ci.GetParameters();
            return () => ci.Invoke(container.ResolveParameterInfos(parameters));
        }

        internal static object ResolveParameterInfo(this Container container, ParameterInfo parameter)
        {
            var creator = FindCreatorInHierarchy(container, parameter.ParameterType);
            if (creator.HasValue) return creator.Value.CreatorFunc();
            return parameter.HasDefaultValue ? parameter.DefaultValue : throw new ArgumentException();
        }

        internal static object[] ResolveParameterInfos(this Container container, ParameterInfo[] parameters)
        {
            return parameters.Select(parameter => ResolveParameterInfo(container, parameter)).ToArray();
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
        public static TValue GetOrCreate<TKey, TValue>([NotNull] this Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue> createValue)
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = createValue();
                dictionary[key] = value;
            }
            return value;
        }
    }
}