using NUnit.Framework;

namespace OneShot.Test
{
    public class TestCircularCheck
    {
        internal class A
        {
            public A(B b)
            {
            }
        }

        internal class B
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

        internal class C
        {
            public C(D _)
            {
            }
        }

        internal class D
        {
            public D(E _)
            {
            }
        }

        internal class E
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