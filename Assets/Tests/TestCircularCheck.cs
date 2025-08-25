using NUnit.Framework;

namespace OneShot.Test
{
    public class TestCircularCheck
    {
        sealed class A
        {
            public A(B b)
            {
            }
        }

        sealed class B
        {
            public B(A a)
            {
            }
        }

        [Test]
        public void should_throw_on_directly_circular_dependency()
        {
            var container = new Container();
            container.Register<A>().AsSelf();
            container.Register<B>().AsSelf();
            Assert.Catch<CircularDependencyException>(() => container.Resolve<A>());
            Assert.Catch<CircularDependencyException>(() => container.Resolve<B>());
        }

        private sealed class C
        {
            public C(D _)
            {
            }
        }

        private sealed class D
        {
            public D(E _)
            {
            }
        }

        private sealed class E
        {
            public E(C _)
            {
            }
        }

        [Test]
        public void should_throw_on_indirectly_circular_dependency()
        {
            var container = new Container();
            container.Register<C>().AsSelf();
            container.Register<D>().AsSelf();
            container.Register<E>().AsSelf();
            Assert.Catch<CircularDependencyException>(() => container.Resolve<C>());
            Assert.Catch<CircularDependencyException>(() => container.Resolve<D>());
            Assert.Catch<CircularDependencyException>(() => container.Resolve<E>());
        }
    }
}
