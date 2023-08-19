using NUnit.Framework;

namespace OneShot.Test
{
    public class TestWithBuilder
    {
        interface InterfaceA {}
        class TypeA : InterfaceA {}

        class Foo
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
        public void should_create_singleton_instance_with_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.Register<Foo>().With(typeA, 1, 123f).Singleton().AsSelf();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.EqualTo(container.Resolve<Foo>()));
        }

        [Test]
        public void should_create_singleton_instance_with_partial_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(123f).AsSelf();
            container.Register<Foo>().With(typeA, 1).Singleton().AsSelf();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.EqualTo(container.Resolve<Foo>()));
        }

        [Test]
        public void should_create_singleton_instance_with_partial_and_override_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(100).AsSelf();
            container.RegisterInstance(123f).AsSelf();
            container.Register<Foo>().With(typeA, 1).Singleton().AsSelf();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.EqualTo(container.Resolve<Foo>()));
        }

        [Test]
        public void should_create_transient_instance_with_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.Register<Foo>().With(typeA, 1, 123f).Transient().AsSelf();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.Not.EqualTo(container.Resolve<Foo>()));
        }

        [Test]
        public void should_create_transient_instance_with_partial_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(1).AsSelf();
            container.Register<Foo>().With(typeA, 123f).Transient().AsSelf();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.Not.EqualTo(container.Resolve<Foo>()));
        }

        [Test]
        public void should_create_transient_instance_with_partial_and_override_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(456f).AsSelf();
            container.RegisterInstance(1).AsSelf();
            container.Register<Foo>().With(typeA, 123f).Transient().AsSelf();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.Not.EqualTo(container.Resolve<Foo>()));
        }

        [Test]
        public void should_create_scoped_instance_with_additional_parameters()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.Register<Foo>().With(typeA, 1, 123f).Scoped().AsSelf();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.EqualTo(container.Resolve<Foo>()));

            var scopedFoo = container.BeginScope().Resolve<Foo>();
            Assert.That(scopedFoo.A, Is.EqualTo(1));
            Assert.That(scopedFoo.B, Is.EqualTo(123));
            Assert.That(scopedFoo.C, Is.EqualTo(typeA));
            Assert.That(instance, Is.Not.EqualTo(scopedFoo));
        }

        [Test]
        public void should_create_scoped_instance_with_partial_additional_parameters()
        {
            var container = new Container();
            container.Register<TypeA>().Singleton().AsInterfaces();
            container.Register<Foo>().With(1, 123f).Scoped().AsSelf();
            var typeA = container.Resolve<InterfaceA>();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.EqualTo(container.Resolve<Foo>()));

            var scopedFoo = container.BeginScope().Resolve<Foo>();
            Assert.That(scopedFoo.A, Is.EqualTo(1));
            Assert.That(scopedFoo.B, Is.EqualTo(123));
            Assert.That(scopedFoo.C, Is.EqualTo(typeA));
            Assert.That(instance, Is.Not.EqualTo(scopedFoo));
        }

        [Test]
        public void should_create_scoped_instance_with_partial_and_override_additional_parameters()
        {
            var container = new Container();
            container.RegisterInstance(100).AsSelf();
            container.RegisterInstance(666f).AsSelf();
            container.Register<TypeA>().Singleton().AsInterfaces();
            container.Register<Foo>().With(1, 123f).Scoped().AsSelf();

            var typeA = container.Resolve<InterfaceA>();
            var instance = container.Resolve<Foo>();
            Assert.That(instance.A, Is.EqualTo(1));
            Assert.That(instance.B, Is.EqualTo(123));
            Assert.That(instance.C, Is.EqualTo(typeA));
            Assert.That(container.Resolve<Foo>(), Is.EqualTo(container.Resolve<Foo>()));

            var scopedFoo = container.BeginScope().Resolve<Foo>();
            Assert.That(scopedFoo.A, Is.EqualTo(1));
            Assert.That(scopedFoo.B, Is.EqualTo(123));
            Assert.That(scopedFoo.C, Is.EqualTo(typeA));
            Assert.That(instance, Is.Not.EqualTo(scopedFoo));
        }
    }
}
