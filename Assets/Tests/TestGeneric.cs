using System;
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
    }
}
