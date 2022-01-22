using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace OneDose
{
    public sealed class Container {}

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Parameter | AttributeTargets.Field)]
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
            GetOrCreateTypeResolvers(container).Add(type, new Creator(creator));
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
            return () =>
            {
                var arguments = ci.GetParameters()
                    .Select(parameter => ResolveParameterInfo(container, parameter))
                    .ToArray()
                ;
                return ci.Invoke(arguments);
            };
        }

        private static object ResolveParameterInfo(Container container, ParameterInfo parameter)
        {
            var creator = FindCreatorInHierarchy(container, parameter.ParameterType);
            if (creator.HasValue) return creator.Value.CreatorFunc();
            return parameter.HasDefaultValue ? parameter.DefaultValue : throw new ArgumentException();
        }

        private static Dictionary<Type, Creator> GetOrCreateTypeResolvers(Container container)
        {
            if (_containerResolvers.TryGetValue(container, out var resolvers)) return resolvers;
            resolvers = new Dictionary<Type, Creator>();
            _containerResolvers[container] = resolvers;
            return resolvers;
        }
    }
}