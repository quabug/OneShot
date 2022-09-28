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
    }
}
