using System.Diagnostics.CodeAnalysis;

namespace OneShot.Test;

[SuppressMessage("Sonar", "S3257",
    Justification = "Factory callbacks document the (Container, Type) signature; discards lose that intent.")]
public class TestLifetime
{
    [Test]
    public async Task should_resolve_instance()
    {
        using var container = new Container();
        var instance = new TypeA();
        container.RegisterInstance(instance).AsSelf();
        await Assert.That(container.Resolve<TypeA>()).IsSameReferenceAs(instance);
    }

    [Test]
    public async Task should_resolve_singleton()
    {
        using var container = new Container();
        container.Register<TypeA>().Singleton().AsSelf();
        await Assert.That(container.Resolve<TypeA>()).IsSameReferenceAs(container.Resolve<TypeA>());
    }

    [Test]
    public async Task should_resolve_singleton_func()
    {
        using var container = new Container();
        Func<Container, Type, TypeA> createTypeA = (c, t) => new TypeA();
        container.Register(createTypeA).Singleton().AsSelf();
        await Assert.That(container.Resolve<TypeA>()).IsSameReferenceAs(container.Resolve<TypeA>());
    }

    [Test]
    public async Task should_resolve_transient()
    {
        using var container = new Container();
        container.Register<TypeA>().AsSelf();
        await Assert.That(container.Resolve<TypeA>()).IsNotSameReferenceAs(container.Resolve<TypeA>());
    }

    [Test]
    public async Task should_resolve_scoped()
    {
        using var container = new Container();
        container.Register<TypeA>().Scoped().AsSelf();
        await Assert.That(container.Resolve<TypeA>()).IsSameReferenceAs(container.Resolve<TypeA>());
        using var childContainer = container.CreateChildContainer();
        await Assert.That(container.Resolve<TypeA>()).IsNotSameReferenceAs(childContainer.Resolve<TypeA>());
        await Assert.That(childContainer.Resolve<TypeA>()).IsSameReferenceAs(childContainer.Resolve<TypeA>());
        using var grandChildContainer = childContainer.CreateChildContainer();
        await Assert.That(childContainer.Resolve<TypeA>()).IsNotSameReferenceAs(grandChildContainer.Resolve<TypeA>());
        await Assert.That(grandChildContainer.Resolve<TypeA>()).IsSameReferenceAs(grandChildContainer.Resolve<TypeA>());
    }

    [Test]
    public async Task should_resolve_types_in_parent_container()
    {
        using var container = new Container();
        var instance = new TypeA();
        container.RegisterInstance(instance).AsSelf();

        using var child1 = container.CreateChildContainer();
        child1.Resolve<TypeA>();
        child1.Register<DefaultConstructor>().Singleton().AsSelf();
        await Assert.That(child1.Resolve<DefaultConstructor>()).IsSameReferenceAs(child1.Resolve<DefaultConstructor>());
        await Assert.That(child1.Resolve<DefaultConstructor>().TypeA).IsSameReferenceAs(instance);

        using var child2 = container.CreateChildContainer();
        child2.Resolve<TypeA>();
        child2.Register<DefaultConstructor>().AsSelf();
        await Assert.That(child2.Resolve<DefaultConstructor>()).IsNotSameReferenceAs(child2.Resolve<DefaultConstructor>());
        await Assert.That(child2.Resolve<DefaultConstructor>().TypeA).IsSameReferenceAs(instance);
    }

    [Test]
    public async Task should_dispose_container()
    {
        using var container = new Container();
        container.RegisterInstance(10).AsSelf();
        container.Resolve<int>();
        container.Dispose();
        await Assert.That(() => container.Resolve<int>()).ThrowsException();
    }

    [Test]
    public async Task should_dispose_container_hierarchy()
    {
        using var container = new Container();
        container.RegisterInstance(10).AsSelf();
        using var child1 = container.CreateChildContainer();
        using var child11 = child1.CreateChildContainer();
        using var child12 = child1.CreateChildContainer();
        using var child121 = child12.CreateChildContainer();
        using var child122 = child12.CreateChildContainer();
        await Assert.That(child122.Resolve<int>()).IsEqualTo(10);
        using var child2 = container.CreateChildContainer();
        child1.Dispose();
        await Assert.That(() => child1.Resolve<int>()).ThrowsException();
        await Assert.That(() => child11.Resolve<int>()).ThrowsException();
        await Assert.That(() => child12.Resolve<int>()).ThrowsException();
        await Assert.That(() => child121.Resolve<int>()).ThrowsException();
        await Assert.That(() => child122.Resolve<int>()).ThrowsException();
        await Assert.That(child2.Resolve<int>()).IsEqualTo(10);
    }

    [Test]
    public async Task should_dispose_transient_instances_of_container()
    {
        using var container = new Container();
        using var childContainer = container.CreateChildContainer();
        container.Register<Disposable>().AsSelf();
        var disposable1 = container.Resolve<Disposable>();
        var disposable2 = container.Resolve<Disposable>();
        var childDisposable = childContainer.Resolve<Disposable>();

        await Assert.That(disposable2).IsNotSameReferenceAs(disposable1);
        await Assert.That(childDisposable).IsNotSameReferenceAs(disposable1);
        await Assert.That(disposable1.DisposedCount).IsEqualTo(0);
        await Assert.That(disposable2.DisposedCount).IsEqualTo(0);
        await Assert.That(childDisposable.DisposedCount).IsEqualTo(0);

        childContainer.Dispose();
        await Assert.That(disposable1.DisposedCount).IsEqualTo(0);
        await Assert.That(disposable2.DisposedCount).IsEqualTo(0);
        await Assert.That(childDisposable.DisposedCount).IsEqualTo(1);

        container.Dispose();
        await Assert.That(disposable1.DisposedCount).IsEqualTo(1);
        await Assert.That(disposable2.DisposedCount).IsEqualTo(1);
        await Assert.That(childDisposable.DisposedCount).IsEqualTo(1);
    }

    [Test]
    public async Task should_dispose_singleton_instances_of_container()
    {
        using var container = new Container();
        using var childContainer = container.CreateChildContainer();
        container.Register<Disposable>().Singleton().AsSelf();
        var disposable1 = container.Resolve<Disposable>();
        var disposable2 = container.Resolve<Disposable>();
        var childDisposable = childContainer.Resolve<Disposable>();

        await Assert.That(disposable2).IsSameReferenceAs(disposable1);
        await Assert.That(childDisposable).IsSameReferenceAs(disposable1);
        await Assert.That(disposable1.DisposedCount).IsEqualTo(0);

        childContainer.Dispose();
        await Assert.That(disposable1.DisposedCount).IsEqualTo(0);

        container.Dispose();
        await Assert.That(disposable1.DisposedCount).IsEqualTo(1);
    }

    [Test]
    public async Task should_dispose_scope_instances_of_container()
    {
        using var container = new Container();
        using var childContainer = container.CreateChildContainer();
        container.Register<Disposable>().Scoped().AsSelf();
        var disposable1 = container.Resolve<Disposable>();
        var disposable2 = container.Resolve<Disposable>();
        var childDisposable1 = childContainer.Resolve<Disposable>();
        var childDisposable2 = childContainer.Resolve<Disposable>();

        await Assert.That(disposable2).IsSameReferenceAs(disposable1);
        await Assert.That(childDisposable2).IsSameReferenceAs(childDisposable1);
        await Assert.That(childDisposable1).IsNotSameReferenceAs(disposable1);

        await Assert.That(disposable1.DisposedCount).IsEqualTo(0);
        await Assert.That(childDisposable1.DisposedCount).IsEqualTo(0);

        childContainer.Dispose();
        await Assert.That(disposable1.DisposedCount).IsEqualTo(0);
        await Assert.That(childDisposable1.DisposedCount).IsEqualTo(1);

        container.Dispose();
        await Assert.That(disposable1.DisposedCount).IsEqualTo(1);
        await Assert.That(childDisposable1.DisposedCount).IsEqualTo(1);
    }

    [Test]
    public async Task should_dispose_all_instances_in_hierarchy_of_container()
    {
        using var container = new Container();
        using var childContainer = container.CreateChildContainer();
        container.Register<Disposable>().AsSelf();
        var disposable1 = container.Resolve<Disposable>();
        var disposable2 = container.Resolve<Disposable>();
        var childDisposable = childContainer.Resolve<Disposable>();

        container.Dispose();
        await Assert.That(disposable1.DisposedCount).IsEqualTo(1);
        await Assert.That(disposable2.DisposedCount).IsEqualTo(1);
        await Assert.That(childDisposable.DisposedCount).IsEqualTo(1);
    }

    [Test]
    public async Task should_create_singleton_instance_based_on_registered_container()
    {
        using var container = new Container();
        container.Register<InjectInt>().Singleton().AsSelf();
        container.RegisterInstance(123).AsSelf();
        await Assert.That(container.Resolve<InjectInt>().Value).IsEqualTo(123);

        using var subContainer = container.CreateChildContainer();
        subContainer.RegisterInstance(234).AsSelf();
        await Assert.That(subContainer.Resolve<InjectInt>().Value).IsEqualTo(123);
    }

    [Test]
    public async Task should_create_scoped_instance_based_on_resolved_container()
    {
        using var container = new Container();
        container.Register<InjectInt>().Scoped().AsSelf();
        container.RegisterInstance(123).AsSelf();
        await Assert.That(container.Resolve<InjectInt>().Value).IsEqualTo(123);

        using var subContainer = container.CreateChildContainer();
        subContainer.RegisterInstance(234).AsSelf();
        await Assert.That(subContainer.Resolve<InjectInt>().Value).IsEqualTo(234);
    }

    [Test]
    public async Task should_create_transient_instance_based_on_resolved_container()
    {
        using var container = new Container();
        container.Register<InjectInt>().Transient().AsSelf();
        container.RegisterInstance(123).AsSelf();
        await Assert.That(container.Resolve<InjectInt>().Value).IsEqualTo(123);

        using var subContainer = container.CreateChildContainer();
        subContainer.RegisterInstance(234).AsSelf();
        await Assert.That(subContainer.Resolve<InjectInt>().Value).IsEqualTo(234);
    }

    [Test]
    public async Task should_throw_on_register_disposable_transient_if_check_on()
    {
        var container = new Container { PreventDisposableTransient = true };
        await Assert.That(() => container.Register<Disposable>().AsSelf()).ThrowsException();
        await Assert.That(() => container.Register<Disposable>().AsInterfaces()).ThrowsException();
    }

    [Test]
    public void should_not_throw_on_register_disposable_transient_if_check_off()
    {
        var container = new Container { PreventDisposableTransient = false };
        container.Register<Disposable>().AsSelf();
        container.Register<Disposable>().AsInterfaces();
    }

    [Test]
    public void should_not_check_disposable_on_other_lifetime()
    {
        var container = new Container { PreventDisposableTransient = true };
        container.Register<Disposable>().Singleton().AsSelf();
        container.Register<Disposable>().Scoped().AsInterfaces();
    }

    [Test]
    public async Task should_not_able_to_create_singleton_with_child_instance()
    {
        using var container = new Container();
        using var child = container.CreateChildContainer();
        container.Register<DefaultConstructor>().Singleton().AsSelf();
        child.Register<TypeA>().Singleton().AsSelf();
        await Assert.That(() => child.Resolve<DefaultConstructor>()).ThrowsException();
    }
}
