using System;
using NUnit.Framework;

namespace OneShot.Test
{
    public class TestOneShot
    {
        [Test]
        public void should_not_able_to_register_invalid_type()
        {
            var container = new Container();
            Assert.Catch<Exception>(() => container.Register<InterfaceA>());
            Assert.Catch<Exception>(() => container.Register<ComplexClass>().As<TypeA>());
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
            container.Register<Func<int>>((c, t) => container.Resolve<DefaultConstructor>().GetIntValue).AsSelf();
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
            container.Register<Func<int>>((c, t) => container.Resolve<DefaultConstructor>().GetIntValue).AsSelf();
            Assert.AreEqual(100, container.Resolve<Func<int>>()());
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
            container.RegisterInstance(10).AsSelf();
            Assert.AreEqual(20, container.CallFunc<Func<int, int>>(v => v * 2));
        }

        [Test]
        public void should_inject_and_call_func_with_default_argument()
        {
            var container = new Container();
            container.RegisterInstance(100).AsSelf();
            Assert.AreEqual(200f, container.CallFunc<Func<int, float, float>>(AddFunc));
        }

        float AddFunc(int a, float b = 100) => a + b;

#if UNITY_EDITOR
        [Test, Ignore("fixed?")]
        public void unity_mono_is_not_able_to_resolve_default_parameter_of_local_function()
        {
            var container = new Container();
            container.RegisterInstance(100).AsSelf();
            Assert.Catch<Exception>(() => container.CallFunc<Func<int, float, float>>(LocalAddFunc));
            float LocalAddFunc(int a, float b = 100) => a + b;
        }
#endif

        [Test]
        public void should_inject_and_call_action()
        {
            var container = new Container();
            var intValue = 0;
            container.RegisterInstance(10).AsSelf();
            container.CallAction<Action<int>>(value => intValue = value * 2);
            Assert.AreEqual(20, intValue);
        }

        [Test]
        public void should_inject_and_call_action_with_default_argument()
        {
            var container = new Container();
            container.RegisterInstance(10).AsSelf();
            container.CallAction<Action<int, float>>(AddAction);
            Assert.AreEqual(30f, _value);
        }

        private float _value;
        void AddAction(int a, float b = 20) => _value = a + b;

        [Test]
        public void should_instantiate_by_type()
        {
            var container = new Container();
            container.Register<TypeA>().Singleton().AsSelf();
            Assert.AreEqual(container.Resolve<TypeA>(), container.Instantiate<DefaultConstructor>().TypeA);
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

            // Assert.Catch<Exception>(() => container.ResolveGroup<int>());
            Assert.That(container.ResolveGroup<int>(), Is.Empty);

            container.RegisterInstance(10).AsSelf();
            container.RegisterInstance(11).AsSelf();
            child1.RegisterInstance(20).AsSelf();
            child1.RegisterInstance(22).AsSelf();
            child2.RegisterInstance(30).AsSelf();
            child11.RegisterInstance(40).AsSelf();
            child12.RegisterInstance(50).AsSelf();
            Assert.That(new[] { 50, 22, 20, 11, 10 }, Is.EqualTo(child12.ResolveGroup<int>()));
            Assert.That(new[] { 40, 22, 20, 11, 10 }, Is.EqualTo(child11.ResolveGroup<int>()));
            Assert.That(new[] { 30, 11, 10 }, Is.EqualTo(child2.ResolveGroup<int>()));
            Assert.That(new[] { 22, 20, 11, 10 }, Is.EqualTo(child1.ResolveGroup<int>()));
            Assert.That(new[] { 11, 10 }, Is.EqualTo(container.ResolveGroup<int>()));

            var instance = child12.Instantiate<IntArrayClass>();
            Assert.AreEqual(50, instance.IntValue);
            Assert.That(new[] { 50, 22, 20, 11, 10 }, Is.EqualTo(instance.IntArray));

            instance = container.Instantiate<IntArrayClass>();
            Assert.AreEqual(11, instance.IntValue);
            Assert.That(new[] { 11, 10 }, Is.EqualTo(instance.IntArray));
        }

        class InjectMethod
        {
            [Inject] void Inject(InterfaceA _) {}
        }

        [Test]
        public void should_able_to_inject_without_resolve()
        {
            var container = new Container();
            container.Register<TypeA>().Singleton().AsInterfaces();
            Assert.DoesNotThrow(() => container.InjectAll(new InjectMethod()));
        }

        class InjectTypeA
        {
            [Inject] public void Inject(TypeA _) {}
        }

        [Test]
        public void should_inject_method_to_same_instance_repeatedly()
        {
            var container = new Container();
            container.Register<TypeA>().AsSelf();
            var instance = new InjectTypeA();
            container.InjectAll(instance);
            container.InjectAll(instance);
            container.InjectAll(instance);
        }

        class TypeAA : TypeA {}
        class TypeAAA : TypeAA {}

        [Test]
        public void should_register_and_resolve_by_bases()
        {
            var container = new Container();
            container.Register<TypeAAA>().AsBases();
            Assert.That(container.Resolve<TypeAA>(), Is.InstanceOf<TypeAAA>());
            Assert.That(container.Resolve<TypeA>(), Is.InstanceOf<TypeAAA>());
        }

        class InjectFloat
        {
            [Inject] public float FloatValue;
        }

        class InjectIntFloat : InjectFloat
        {
            [Inject] public int IntValue;
        }

        [Test]
        public void should_inject_all_for_instance_by_contract_type()
        {
            var container = new Container();
            container.RegisterInstance(123).AsSelf();
            container.RegisterInstance(222.222f).AsSelf();
            var instance = new InjectIntFloat();
            container.InjectAll((InjectFloat)instance);
            Assert.That(instance.IntValue, Is.EqualTo(123));
            Assert.That(instance.FloatValue, Is.EqualTo(222.222f));
        }

        [Test]
        public void should_get_null_if_try_resolved_type_not_registered()
        {
            var container = new Container();
            Assert.That(container.TryResolve<int>(), Is.Null);
        }

        [Test]
        public void should_check_registered_of_a_type()
        {
            var container = new Container();
            container.RegisterInstance(123).AsSelf();
            Assert.That(container.IsRegisteredInHierarchy<int>(), Is.True);
            Assert.That(container.IsRegisteredInHierarchy<float>(), Is.False);
            Assert.That(container.IsRegistered<int>(), Is.True);
            Assert.That(container.IsRegistered<float>(), Is.False);

            var child = container.CreateChildContainer();
            Assert.That(child.IsRegisteredInHierarchy<int>(), Is.True);
            Assert.That(child.IsRegisteredInHierarchy<float>(), Is.False);
            Assert.That(child.IsRegistered<int>(), Is.False);
            Assert.That(child.IsRegistered<float>(), Is.False);
        }
    }
}
