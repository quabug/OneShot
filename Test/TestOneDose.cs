using System;
using NUnit.Framework;

namespace OneDose.Test
{
    public class TestOneDose
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
            container.RegisterInstance(instance);
            Assert.AreSame(instance, container.Resolve<TypeA>());
        }

        [Test]
        public void should_resolve_singleton()
        {
            var container = new Container();
            container.RegisterSingleton<TypeA>();
            Assert.AreSame(container.Resolve<TypeA>(), container.Resolve<TypeA>());
        }

        [Test]
        public void should_resolve_transient()
        {
            var container = new Container();
            container.RegisterTransient<TypeA>();
            Assert.AreNotSame(container.Resolve<TypeA>(), container.Resolve<TypeA>());
        }

        [Test]
        public void should_able_to_register_and_resolve_interface()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance);
            container.Register<InterfaceA>(() => container.Resolve<TypeA>());
            Assert.AreSame(instance, container.Resolve<InterfaceA>());
        }

        [Test]
        public void should_inject_by_default_constructor()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance);
            container.RegisterSingleton<DefaultConstructor>();
            Assert.AreSame(instance, container.Resolve<DefaultConstructor>().TypeA);
        }

        [Test]
        public void should_inject_by_marked_constructor()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance);
            container.RegisterSingleton<InjectConstructor>();
            Assert.AreSame(instance, container.Resolve<InjectConstructor>().TypeA);
        }

        [Test]
        public void should_inject_by_constructor_with_default_parameters()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance);
            container.RegisterSingleton<ConstructorWithDefaultParameter>();
            Assert.AreSame(instance, container.Resolve<ConstructorWithDefaultParameter>().TypeA);
            Assert.AreEqual(10, container.Resolve<ConstructorWithDefaultParameter>().IntValue);
        }

        [Test]
        public void should_resolve_complex_type()
        {
            var container = new Container();
            var typeA = new TypeA();
            container.RegisterInstance(typeA);
            container.Register<InterfaceA>(() => container.Resolve<TypeA>());
            container.RegisterTransient<InjectConstructor>();
            container.RegisterSingleton<ConstructorWithDefaultParameter>();
            container.RegisterSingleton<DefaultConstructor>();
            container.Register<Func<int>>(() => container.Resolve<DefaultConstructor>().GetIntValue);
            container.RegisterTransient<ComplexClass>();
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
            container.RegisterInstance(instance);
            container.RegisterSingleton<DefaultConstructor>();
            container.Register<Func<int>>(() => container.Resolve<DefaultConstructor>().GetIntValue);
            Assert.AreEqual(100, container.Resolve<Func<int>>()());
        }

        [Test]
        public void should_resolve_types_in_parent_container()
        {
            var container = new Container();
            var instance = new TypeA();
            container.RegisterInstance(instance);

            var child1 = container.CreateChildContainer();
            child1.RegisterSingleton<DefaultConstructor>();
            Assert.AreSame(child1.Resolve<DefaultConstructor>(), child1.Resolve<DefaultConstructor>());
            Assert.AreSame(instance, child1.Resolve<DefaultConstructor>().TypeA);

            var child2 = container.CreateChildContainer();
            child2.RegisterTransient<DefaultConstructor>();
            Assert.AreNotSame(child2.Resolve<DefaultConstructor>(), child2.Resolve<DefaultConstructor>());
            Assert.AreSame(instance, child2.Resolve<DefaultConstructor>().TypeA);
        }

        [Test]
        public void should_throw_if_cannot_resolve()
        {
            var container = new Container();
            container.RegisterSingleton<DefaultConstructor>();
            Assert.Catch<Exception>(() => container.Resolve<TypeA>());
            Assert.Catch<Exception>(() => container.Resolve<DefaultConstructor>());
        }
    }
}