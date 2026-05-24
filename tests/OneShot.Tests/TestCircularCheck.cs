namespace OneShot.Test;

public class TestCircularCheck
{
    internal class A
    {
        public A(B _)
        {
        }
    }

    internal class B
    {
        public B(A _)
        {
        }
    }

    [Test]
    public async Task should_throw_on_directly_circular_dependency()
    {
        var container = new Container();
        container.Register<A>().AsSelf();
        container.Register<B>().AsSelf();
        await Assert.That(() => container.Resolve<A>()).ThrowsExactly<CircularDependencyException>();
        await Assert.That(() => container.Resolve<B>()).ThrowsExactly<CircularDependencyException>();
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
    public async Task should_throw_on_indirectly_circular_dependency()
    {
        var container = new Container();
        container.Register<C>().AsSelf();
        container.Register<D>().AsSelf();
        container.Register<E>().AsSelf();
        await Assert.That(() => container.Resolve<C>()).ThrowsExactly<CircularDependencyException>();
        await Assert.That(() => container.Resolve<D>()).ThrowsExactly<CircularDependencyException>();
        await Assert.That(() => container.Resolve<E>()).ThrowsExactly<CircularDependencyException>();
    }
}
