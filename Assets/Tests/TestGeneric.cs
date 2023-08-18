using System;
using System.Reflection;
using NUnit.Framework;

namespace OneShot.Test
{
    public class TestGeneric
    {
        class Generic<T> {}
        class Generic<T, U> {}

        private static Type[] _genericArguments = {
            typeof(Generic<int>), typeof(Generic<float>), typeof(Generic<Generic<int>>), typeof(Generic<Generic<Generic<int>>>),
            typeof(Generic<int, float>), typeof(Generic<Generic<int>, Generic<float>>), typeof(Generic<Generic<int, float>>)
        };

        [Test, TestCaseSource(nameof(_genericArguments))]
        public void should_make_instance_of_generic(Type type)
        {
            var container = new Container();
            container.Register(typeof(Generic<>), (_, t) => Activator.CreateInstance(typeof(Generic<>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<>));
            container.Register(typeof(Generic<,>), (_, t) => Activator.CreateInstance(typeof(Generic<,>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<,>));
            Assert.That(container.Resolve(type), Is.TypeOf(type));
        }
        
        interface Label<T> : ILabel<T> {}
        
        [Test, TestCaseSource(nameof(_genericArguments))]
        public void should_make_instance_of_generic_with_label(Type type)
        {
            var container = new Container();
            container.Register(typeof(Generic<>), (_, t) => Activator.CreateInstance(typeof(Generic<>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<>), typeof(Label<>));
            container.Register(typeof(Generic<,>), (_, t) => Activator.CreateInstance(typeof(Generic<,>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<,>), typeof(Label<>));
            Assert.Catch<ArgumentException>(() => container.Resolve(type));
            Assert.That(container.Resolve(type, typeof(Label<>)), Is.TypeOf(type));
        }
        
        [Test]
        public void should_make_instance_of_concrete_generic()
        {
            var container = new Container();
            var instance = new Generic<int>();
            container.RegisterInstance(instance).As<Generic<int>>();
            Assert.That(container.Resolve<Generic<int>>(), Is.SameAs(instance));
        }

        private static Lazy<T> CreateLazy<T>(Container container, Type _)
        {
            return new Lazy<T>(() => container.Resolve<T>());
        }

        [Test]
        public void should_register_and_resolve_generic_type_by_method_name()
        {
            var container = new Container();
            var creator = GetType().GetMethod(nameof(CreateLazy), BindingFlags.Static | BindingFlags.NonPublic)!;
            container.RegisterGeneric(typeof(Lazy<>), creator).With(123).AsSelf();
            Assert.That(container.Resolve<Lazy<int>>().Value, Is.EqualTo(123));
        }

        private static Generic<T, U> CreateGeneric<T, U>(Container container, Type _)
        {
            return new Generic<T, U>();
        }

        private static void InvalidReturnCreator<T, U>(Container container, Type _)
        {
        }

        private static Generic<T, U> InvalidParameterCreator<T, U>(Container container)
        {
            return new Generic<T, U>();
        }

        [Test]
        public void should_register_and_resolve_generic_with_multiple_type_parameters_by_method_name()
        {
            var container = new Container();
            var creator = GetType().GetMethod(nameof(CreateGeneric), BindingFlags.Static | BindingFlags.NonPublic)!;
            container.RegisterGeneric(typeof(Generic<,>), creator).AsSelf();
            var instance = container.Resolve<Generic<int, float>>();
        }

        [Test]
        public void should_throw_exception_if_creator_is_not_valid()
        {
            var container = new Container();
            Assert.Throws<ArgumentNullException>(() => container.RegisterGeneric(typeof(Generic<,>), null).AsSelf());

            var lazyCreator = GetType().GetMethod(nameof(CreateLazy), BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.Throws<ArgumentException>(() => container.RegisterGeneric(typeof(Generic<>), lazyCreator).AsSelf());
            Assert.Throws<ArgumentException>(() => container.RegisterGeneric(typeof(Generic<,>), lazyCreator).AsSelf());

            var genericCreator = GetType().GetMethod(nameof(CreateGeneric), BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.Throws<ArgumentException>(() => container.RegisterGeneric(typeof(Generic<>), genericCreator).AsSelf());

            var invalidReturnCreator = GetType().GetMethod(nameof(InvalidReturnCreator), BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.Throws<ArgumentException>(() => container.RegisterGeneric(typeof(Generic<,>), invalidReturnCreator).AsSelf());

            var invalidParameterCreator = GetType().GetMethod(nameof(InvalidParameterCreator), BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.Throws<ArgumentException>(() => container.RegisterGeneric(typeof(Generic<,>), invalidParameterCreator).AsSelf());
        }
    }
}
