namespace OneShot.Test
{
    public class TestWithBuilder
    {
        internal interface InterfaceA {}
        internal class TypeA : InterfaceA {}

        internal class Foo
        {
            public int A;
            public float B;
            public InterfaceA C;

            public Foo(int a, float b, InterfaceA c)
            {
                A = a;
                B = b;
                C = c;
            }
        }

        [Test]
        public async Task should_create_singleton_instance_with_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.Register<Foo>().With(typeA, 1, 123f).Singleton().AsSelf();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsEqualTo(container.Resolve<Foo>());
        }

        [Test]
        public async Task should_create_singleton_instance_with_partial_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(123f).AsSelf();
            container.Register<Foo>().With(typeA, 1).Singleton().AsSelf();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsEqualTo(container.Resolve<Foo>());
        }

        [Test]
        public async Task should_create_singleton_instance_with_partial_and_override_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(100).AsSelf();
            container.RegisterInstance(123f).AsSelf();
            container.Register<Foo>().With(typeA, 1).Singleton().AsSelf();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsEqualTo(container.Resolve<Foo>());
        }

        [Test]
        public async Task should_create_transient_instance_with_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.Register<Foo>().With(typeA, 1, 123f).Transient().AsSelf();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsNotEqualTo(container.Resolve<Foo>());
        }

        [Test]
        public async Task should_create_transient_instance_with_partial_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(1).AsSelf();
            container.Register<Foo>().With(typeA, 123f).Transient().AsSelf();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsNotEqualTo(container.Resolve<Foo>());
        }

        [Test]
        public async Task should_create_transient_instance_with_partial_and_override_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(456f).AsSelf();
            container.RegisterInstance(1).AsSelf();
            container.Register<Foo>().With(typeA, 123f).Transient().AsSelf();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsNotEqualTo(container.Resolve<Foo>());
        }

        [Test]
        public async Task should_create_scoped_instance_with_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.Register<Foo>().With(typeA, 1, 123f).Scoped().AsSelf();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsEqualTo(container.Resolve<Foo>());

            var scopedFoo = container.BeginScope().Resolve<Foo>();
            await Assert.That(scopedFoo.A).IsEqualTo(1);
            await Assert.That(scopedFoo.B).IsEqualTo(123f);
            await Assert.That(scopedFoo.C).IsEqualTo(typeA);
            await Assert.That(instance).IsNotEqualTo(scopedFoo);
        }

        [Test]
        public async Task should_create_scoped_instance_with_partial_additional_parameters()
        {
            var container = new Container();
            container.Register<TypeA>().Singleton().AsInterfaces();
            container.Register<Foo>().With(1, 123f).Scoped().AsSelf();
            var typeA = container.Resolve<InterfaceA>();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsEqualTo(container.Resolve<Foo>());

            var scopedFoo = container.BeginScope().Resolve<Foo>();
            await Assert.That(scopedFoo.A).IsEqualTo(1);
            await Assert.That(scopedFoo.B).IsEqualTo(123f);
            await Assert.That(scopedFoo.C).IsEqualTo(typeA);
            await Assert.That(instance).IsNotEqualTo(scopedFoo);
        }

        [Test]
        public async Task should_create_scoped_instance_with_partial_and_override_additional_parameters()
        {
            var container = new Container();
            container.RegisterInstance(100).AsSelf();
            container.RegisterInstance(666f).AsSelf();
            container.Register<TypeA>().Singleton().AsInterfaces();
            container.Register<Foo>().With(1, 123f).Scoped().AsSelf();

            var typeA = container.Resolve<InterfaceA>();
            var instance = container.Resolve<Foo>();
            await Assert.That(instance.A).IsEqualTo(1);
            await Assert.That(instance.B).IsEqualTo(123f);
            await Assert.That(instance.C).IsEqualTo(typeA);
            await Assert.That(container.Resolve<Foo>()).IsEqualTo(container.Resolve<Foo>());

            var scopedFoo = container.BeginScope().Resolve<Foo>();
            await Assert.That(scopedFoo.A).IsEqualTo(1);
            await Assert.That(scopedFoo.B).IsEqualTo(123f);
            await Assert.That(scopedFoo.C).IsEqualTo(typeA);
            await Assert.That(instance).IsNotEqualTo(scopedFoo);
        }
    }
}
