using System;
using System.Linq;
using NUnit.Framework;

namespace OneShot.Test
{
    public class TestOneShot
    {
        interface InterfaceA {}
        class TypeA : InterfaceA {}

        class DefaultConstructor
        {
            public readonly TypeA TypeA;
            public DefaultConstructor(TypeA typeA) => TypeA = typeA;
            public int GetIntValue() => 100;
        }

        class InjectConstructor
        {
            public readonly TypeA TypeA;
            [Inject] public InjectConstructor(TypeA typeA) => TypeA = typeA;
            public InjectConstructor(DefaultConstructor defaultConstructor) => TypeA = null;
        }

        class ConstructorWithDefaultParameter
        {
            public readonly TypeA TypeA;
            public readonly int IntValue = 0;
            public ConstructorWithDefaultParameter(TypeA typeA, int intValue = 10)
            {
                TypeA = typeA;
                IntValue = intValue;
            }
        }

        class ComplexClass
        {
            public readonly TypeA A;
            public readonly InterfaceA B;
            public readonly InjectConstructor C;
            public readonly float D;
            public readonly ConstructorWithDefaultParameter E;
            public readonly Func<int> GetIntValue;

            public ComplexClass(TypeA a, InterfaceA b, InjectConstructor c, float d = 22, ConstructorWithDefaultParameter e = null, Func<int> getIntValue = null)
            {
                A = a;
                B = b;
                C = c;
                D = d;
                E = e;
                GetIntValue = getIntValue;
            }
        }

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
            Func<TypeA> createTypeA = () => new TypeA();
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
        public void should_able_to_register_and_resolve_interface()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance).AsSelf().As<InterfaceA>();
            Assert.AreSame(instance, container.Resolve<InterfaceA>());
        }

        [Test]
        public void should_inject_by_default_constructor()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance).AsSelf();
            container.Register<DefaultConstructor>().Singleton().AsSelf();
            Assert.AreSame(instance, container.Resolve<DefaultConstructor>().TypeA);
        }

        [Test]
        public void should_inject_by_marked_constructor()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance).AsSelf();
            container.Register<InjectConstructor>().Singleton().AsSelf();
            Assert.AreSame(instance, container.Resolve<InjectConstructor>().TypeA);
        }

        [Test]
        public void should_inject_by_constructor_with_default_parameters()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance).AsSelf();
            container.Register<ConstructorWithDefaultParameter>().Singleton().AsSelf();
            Assert.AreSame(instance, container.Resolve<ConstructorWithDefaultParameter>().TypeA);
            Assert.AreEqual(10, container.Resolve<ConstructorWithDefaultParameter>().IntValue);
        }

        [Test]
        public void should_resolve_complex_type()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(typeA).AsSelf().As<InterfaceA>();
            container.Register<InjectConstructor>().AsSelf();
            container.Register<ConstructorWithDefaultParameter>().Singleton().AsSelf();
            container.Register<DefaultConstructor>().Singleton().AsSelf();
            container.Register<Func<int>>(() => container.Resolve<DefaultConstructor>().GetIntValue).AsSelf();
            container.Register<ComplexClass>().AsSelf();
            var complex1 = container.Resolve<ComplexClass>();
            Assert.AreSame(typeA, complex1.A);
            Assert.AreSame(typeA, complex1.B);
            Assert.AreSame(typeA, complex1.C.TypeA);
            Assert.AreSame(typeA, complex1.C.TypeA);
            Assert.AreEqual(22, complex1.D);
            Assert.AreSame(typeA, complex1.E.TypeA);
            Assert.AreEqual(10, complex1.E.IntValue);
            Assert.AreEqual(100, complex1.GetIntValue());

            var complex2 = container.Resolve<ComplexClass>();
            Assert.AreSame(typeA, complex2.A);
            Assert.AreSame(typeA, complex2.B);
            Assert.AreSame(typeA, complex2.C.TypeA);
            Assert.AreSame(typeA, complex2.C.TypeA);
            Assert.AreEqual(22, complex2.D);
            Assert.AreSame(typeA, complex2.E.TypeA);
            Assert.AreEqual(10, complex2.E.IntValue);
            Assert.AreEqual(100, complex2.GetIntValue());

            Assert.AreNotSame(complex1.C, complex2.C);
            Assert.AreSame(complex1.E, complex2.E);
        }

        [Test]
        public void should_able_to_register_and_resolve_func()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance).AsSelf();
            container.Register<DefaultConstructor>().Singleton().AsSelf();
            container.Register<Func<int>>(() => container.Resolve<DefaultConstructor>().GetIntValue).AsSelf();
            Assert.AreEqual(100, container.Resolve<Func<int>>()());
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
        public void should_override_register()
        {
            var container = new Container();
            container.RegisterInstance(10).AsSelf();
            container.RegisterInstance(20).AsSelf();
            Assert.AreEqual(20, container.Resolve<int>());
        }

        [Test]
        public void should_throw_if_cannot_resolve()
        {
            var container = new Container();
            container.Register<DefaultConstructor>().Singleton().AsSelf();
            Assert.Catch<Exception>(() => container.Resolve<TypeA>());
            Assert.Catch<Exception>(() => container.Resolve<DefaultConstructor>());
        }

        class A
        {
            public A(B b) {}
        }

        class B
        {
            public B(A a) {}
        }

        [Test, Ignore("not implemented yet")]
        public void should_throw_on_circular_dependency()
        {
            var container = new Container();
            container.Register<A>().AsSelf();
            container.Register<B>().AsSelf();
            Assert.Catch<Exception>(() => container.Resolve<A>());
        }

        class Injected
        {
            [Inject] public int Int;
            [Inject] public float Float { get; private set; }
            [field: Inject] public Double Double { get; }

            public int A;
            public double B;

            [Inject]
            public void Init(int a, double b)
            {
                A = a;
                B = b;
            }

            public float C;
            public long D;

            [Inject]
            public int Init(float c, long d = 20)
            {
                C = c;
                D = d;
                return (int)d;
            }

            public int E;
            void Init(int e)
            {
                E = e;
            }
        }

        [Test]
        public void should_inject_marked_members()
        {
            var container = new Container();
            container.RegisterInstance<int>(10).AsSelf();
            container.RegisterInstance<float>(100f).AsSelf();
            container.RegisterInstance<double>(0.999).AsSelf();
            var instance = new Injected();
            container.InjectAll(instance);
            Assert.AreEqual(10, instance.Int);
            Assert.AreEqual(100f, instance.Float);
            Assert.AreEqual(0.999, instance.Double);
            Assert.AreEqual(10, instance.A);
            Assert.AreEqual(0.999, instance.B);
            Assert.AreEqual(100f, instance.C);
            Assert.AreEqual(20, instance.D);
            Assert.AreEqual(0, instance.E);
        }

        class CannotInject
        {
            [Inject] public int A { get; }
        }

        [Test]
        public void should_throw_on_inject_to_readonly_property()
        {
            var container = new Container();
            container.RegisterInstance<int>(10).AsSelf();
            Assert.Catch<Exception>(() => container.InjectAll(new CannotInject()));
        }

        [Test]
        public void should_throw_if_not_able_to_inject()
        {
            var container = new Container();
            Assert.Catch<Exception>(() => container.InjectAll(new Injected()));
        }

        [Test]
        public void should_inject_and_call_function()
        {
            var container = new Container();
            Func<int, int> returnInt = value => value * 2;
            container.RegisterInstance(10).AsSelf();
            Assert.AreEqual(20, container.Call<int>(returnInt));
        }

        [Test]
        public void should_inject_and_call_action()
        {
            var container = new Container();
            var intValue = 0;
            Action<int> action = value => intValue = value * 2;
            container.RegisterInstance(10).AsSelf();
            container.CallAction(action);
            Assert.AreEqual(20, intValue);
        }

        [Test]
        public void should_instantiate_by_type()
        {
            var container = new Container();
            container.Register<TypeA>().Singleton().AsSelf();
            Assert.AreEqual(container.Resolve<TypeA>(), container.Instantiate<DefaultConstructor>().TypeA);
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

        class IntArrayClass
        {
            public readonly int IntValue;
            public readonly int[] IntArray;

            public IntArrayClass(int intValue, int[] intArray)
            {
                IntValue = intValue;
                IntArray = intArray;
            }
        }

        [Test]
        public void should_resolve_group_of_type()
        {
            var container = new Container();
            var child1 = container.CreateChildContainer();
            var child11 = child1.CreateChildContainer();
            var child12 = child1.CreateChildContainer();
            var child2 = container.CreateChildContainer();

            Assert.Catch<Exception>(() => container.ResolveGroup<int>());

            container.RegisterInstance(10).AsSelf();
            container.RegisterInstance(11).AsSelf();
            child1.RegisterInstance(20).AsSelf();
            child1.RegisterInstance(22).AsSelf();
            child2.RegisterInstance(30).AsSelf();
            child11.RegisterInstance(40).AsSelf();
            child12.RegisterInstance(50).AsSelf();
            Assert.That(new[] { 50, 22, 20, 11, 10 }, Is.EqualTo(child12.ResolveGroup<int>().ToArray()));
            Assert.That(new[] { 40, 22, 20, 11, 10 }, Is.EqualTo(child11.ResolveGroup<int>().ToArray()));
            Assert.That(new[] { 30, 11, 10 }, Is.EqualTo(child2.ResolveGroup<int>().ToArray()));
            Assert.That(new[] { 22, 20, 11, 10 }, Is.EqualTo(child1.ResolveGroup<int>().ToArray()));
            Assert.That(new[] { 11, 10 }, Is.EqualTo(container.ResolveGroup<int>().ToArray()));

            var instance = child12.Instantiate<IntArrayClass>();
            Assert.AreEqual(50, instance.IntValue);
            Assert.That(new[] { 50, 22, 20, 11, 10 }, Is.EqualTo(instance.IntArray));

            instance = container.Instantiate<IntArrayClass>();
            Assert.AreEqual(11, instance.IntValue);
            Assert.That(new[] { 11, 10 }, Is.EqualTo(instance.IntArray));
        }
    }
}