using System;
using NUnit.Framework;

namespace OneShot.Test
{
    public class TestLifetime
    {
        [Test]
        public void should_resolve_instance()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance).AsSelf();
            Assert.AreSame(instance, container.Resolve<TypeA>());
        }

        [Test]
        public void should_resolve_singleton()
        {
            var container = new Container();
            container.Register<TypeA>().Singleton().AsSelf();
            Assert.AreSame(container.Resolve<TypeA>(), container.Resolve<TypeA>());
        }

        [Test]
        public void should_resolve_singleton_func()
        {
            var container = new Container();
            Func<Container, Type, TypeA> createTypeA = (c, t) => new TypeA();
            container.Register(createTypeA).Singleton().AsSelf();
            Assert.AreSame(container.Resolve<TypeA>(), container.Resolve<TypeA>());
        }

        [Test]
        public void should_resolve_transient()
        {
            var container = new Container();
            container.Register<TypeA>().AsSelf();
            Assert.AreNotSame(container.Resolve<TypeA>(), container.Resolve<TypeA>());
        }

        [Test]
        public void should_resolve_scoped()
        {
            var container = new Container();
            container.Register<TypeA>().Scoped().AsSelf();
            Assert.AreSame(container.Resolve<TypeA>(), container.Resolve<TypeA>());
            var childContainer = container.CreateChildContainer();
            Assert.AreNotSame(container.Resolve<TypeA>(), childContainer.Resolve<TypeA>());
            Assert.AreSame(childContainer.Resolve<TypeA>(), childContainer.Resolve<TypeA>());
            var grandChildContainer = childContainer.CreateChildContainer();
            Assert.AreNotSame(childContainer.Resolve<TypeA>(), grandChildContainer.Resolve<TypeA>());
            Assert.AreSame(grandChildContainer.Resolve<TypeA>(), grandChildContainer.Resolve<TypeA>());
        }

        [Test]
        public void should_resolve_types_in_parent_container()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance).AsSelf();

            var child1 = container.CreateChildContainer();
            child1.Resolve<TypeA>();
            child1.Register<DefaultConstructor>().Singleton().AsSelf();
            Assert.AreSame(child1.Resolve<DefaultConstructor>(), child1.Resolve<DefaultConstructor>());
            Assert.AreSame(instance, child1.Resolve<DefaultConstructor>().TypeA);

            var child2 = container.CreateChildContainer();
            child2.Resolve<TypeA>();
            child2.Register<DefaultConstructor>().AsSelf();
            Assert.AreNotSame(child2.Resolve<DefaultConstructor>(), child2.Resolve<DefaultConstructor>());
            Assert.AreSame(instance, child2.Resolve<DefaultConstructor>().TypeA);
        }

        [Test]
        public void should_dispose_container()
        {
            var container = new Container();
            container.RegisterInstance(10).AsSelf();
            container.Resolve<int>();
            container.Dispose();
            Assert.Catch<Exception>(() => container.Resolve<int>());
        }

        [Test]
        public void should_dispose_container_hierarchy()
        {
            var container = new Container();
            container.RegisterInstance(10).AsSelf();
            var child1 = container.CreateChildContainer();
            var child11 = child1.CreateChildContainer();
            var child12 = child1.CreateChildContainer();
            var child121 = child12.CreateChildContainer();
            var child122 = child12.CreateChildContainer();
            Assert.AreEqual(10, child122.Resolve<int>());
            var child2 = container.CreateChildContainer();
            child1.Dispose();
            Assert.Catch<Exception>(() => child1.Resolve<int>());
            Assert.Catch<Exception>(() => child11.Resolve<int>());
            Assert.Catch<Exception>(() => child12.Resolve<int>());
            Assert.Catch<Exception>(() => child121.Resolve<int>());
            Assert.Catch<Exception>(() => child122.Resolve<int>());
            Assert.AreEqual(10, child2.Resolve<int>());
        }

        [Test]
        public void should_dispose_transient_instances_of_container()
        {
            var container = new Container();
            var childContainer = container.CreateChildContainer();
            container.Register<Disposable>().AsSelf();
            var disposable1 = container.Resolve<Disposable>();
            var disposable2 = container.Resolve<Disposable>();
            var childDisposable = childContainer.Resolve<Disposable>();

            Assert.AreNotSame(disposable1, disposable2);
            Assert.AreNotSame(disposable1, childDisposable);
            Assert.AreEqual(0, disposable1.DisposedCount);
            Assert.AreEqual(0, disposable2.DisposedCount);
            Assert.AreEqual(0, childDisposable.DisposedCount);

            childContainer.Dispose();
            Assert.AreEqual(0, disposable1.DisposedCount);
            Assert.AreEqual(0, disposable2.DisposedCount);
            Assert.AreEqual(1, childDisposable.DisposedCount);

            container.Dispose();
            Assert.AreEqual(1, disposable1.DisposedCount);
            Assert.AreEqual(1, disposable2.DisposedCount);
            Assert.AreEqual(1, childDisposable.DisposedCount);
        }

        [Test]
        public void should_dispose_singleton_instances_of_container()
        {
            var container = new Container();
            var childContainer = container.CreateChildContainer();
            container.Register<Disposable>().Singleton().AsSelf();
            var disposable1 = container.Resolve<Disposable>();
            var disposable2 = container.Resolve<Disposable>();
            var childDisposable = childContainer.Resolve<Disposable>();

            Assert.AreSame(disposable1, disposable2);
            Assert.AreSame(disposable1, childDisposable);
            Assert.AreEqual(0, disposable1.DisposedCount);

            childContainer.Dispose();
            Assert.AreEqual(0, disposable1.DisposedCount);

            container.Dispose();
            Assert.AreEqual(1, disposable1.DisposedCount);
        }

        [Test]
        public void should_dispose_scope_instances_of_container()
        {
            var container = new Container();
            var childContainer = container.CreateChildContainer();
            container.Register<Disposable>().Scoped().AsSelf();
            var disposable1 = container.Resolve<Disposable>();
            var disposable2 = container.Resolve<Disposable>();
            var childDisposable1 = childContainer.Resolve<Disposable>();
            var childDisposable2 = childContainer.Resolve<Disposable>();

            Assert.AreSame(disposable1, disposable2);
            Assert.AreSame(childDisposable1, childDisposable2);
            Assert.AreNotSame(disposable1, childDisposable1);

            Assert.AreEqual(0, disposable1.DisposedCount);
            Assert.AreEqual(0, childDisposable1.DisposedCount);

            childContainer.Dispose();
            Assert.AreEqual(0, disposable1.DisposedCount);
            Assert.AreEqual(1, childDisposable1.DisposedCount);

            container.Dispose();
            Assert.AreEqual(1, disposable1.DisposedCount);
            Assert.AreEqual(1, childDisposable1.DisposedCount);
        }

        [Test]
        public void should_dispose_all_instances_in_hierarchy_of_container()
        {
            var container = new Container();
            var childContainer = container.CreateChildContainer();
            container.Register<Disposable>().AsSelf();
            var disposable1 = container.Resolve<Disposable>();
            var disposable2 = container.Resolve<Disposable>();
            var childDisposable = childContainer.Resolve<Disposable>();

            container.Dispose();
            Assert.AreEqual(1, disposable1.DisposedCount);
            Assert.AreEqual(1, disposable2.DisposedCount);
            Assert.AreEqual(1, childDisposable.DisposedCount);
        }

        [Test]
        public void should_create_singleton_instance_based_on_registered_container()
        {
            var container = new Container();
            container.Register<InjectInt>().Singleton().AsSelf();
            container.RegisterInstance(123).AsSelf();
            Assert.That(container.Resolve<InjectInt>().Value, Is.EqualTo(123));

            var subContainer = container.CreateChildContainer();
            subContainer.RegisterInstance(234).AsSelf();
            Assert.That(subContainer.Resolve<InjectInt>().Value, Is.EqualTo(123));
        }

        [Test]
        public void should_create_scoped_instance_based_on_resolved_container()
        {
            var container = new Container();
            container.Register<InjectInt>().Scoped().AsSelf();
            container.RegisterInstance(123).AsSelf();
            Assert.That(container.Resolve<InjectInt>().Value, Is.EqualTo(123));

            var subContainer = container.CreateChildContainer();
            subContainer.RegisterInstance(234).AsSelf();
            Assert.That(subContainer.Resolve<InjectInt>().Value, Is.EqualTo(234));
        }

        [Test]
        public void should_create_transient_instance_based_on_resolved_container()
        {
            var container = new Container();
            container.Register<InjectInt>().Transient().AsSelf();
            container.RegisterInstance(123).AsSelf();
            Assert.That(container.Resolve<InjectInt>().Value, Is.EqualTo(123));

            var subContainer = container.CreateChildContainer();
            subContainer.RegisterInstance(234).AsSelf();
            Assert.That(subContainer.Resolve<InjectInt>().Value, Is.EqualTo(234));
        }

        [Test]
        public void should_throw_on_register_disposable_transient_if_check_on()
        {
            var container = new Container { PreventDisposableTransient = true };
            Assert.Catch<Exception>(() => container.Register<Disposable>().AsSelf());
            Assert.Catch<Exception>(() => container.Register<Disposable>().AsInterfaces());
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
        public void should_not_able_to_create_singleton_with_child_instance()
        {
            var container = new Container();
            var child = container.CreateChildContainer();
            container.Register<DefaultConstructor>().Singleton().AsSelf();
            child.Register<TypeA>().Singleton().AsSelf();
            Assert.Catch<Exception>(() => child.Resolve<DefaultConstructor>());
        }
    }
}
