using System.Diagnostics.CodeAnalysis;

namespace OneShot.Test;

[SuppressMessage("Sonar", "S3257",
    Justification = "Factory callbacks document the (Container, Type) signature; discards lose that intent.")]
public class TestOneShot
{
    [Test]
    public async Task should_not_able_to_register_invalid_type()
    {
        using var container = new Container();
        await Assert.That(() => container.Register<InterfaceA>()).ThrowsException();
        await Assert.That(() => container.Register<ComplexClass>().As<TypeA>()).ThrowsException();
    }

    [Test]
    public async Task should_able_to_register_and_resolve_interface()
    {
        using var container = new Container();
        var instance = new TypeA();
        container.RegisterInstance(instance).AsSelf().As<InterfaceA>();
        await Assert.That(container.Resolve<InterfaceA>()).IsSameReferenceAs(instance);
    }

    [Test]
    public async Task should_inject_by_default_constructor()
    {
        using var container = new Container();
        var instance = new TypeA();
        container.RegisterInstance(instance).AsSelf();
        container.Register<DefaultConstructor>().Singleton().AsSelf();
        await Assert.That(container.Resolve<DefaultConstructor>().TypeA).IsSameReferenceAs(instance);
    }

    [Test]
    public async Task should_inject_by_marked_constructor()
    {
        using var container = new Container();
        var instance = new TypeA();
        container.RegisterInstance(instance).AsSelf();
        container.Register<InjectConstructor>().Singleton().AsSelf();
        await Assert.That(container.Resolve<InjectConstructor>().TypeA).IsSameReferenceAs(instance);
    }

    [Test]
    public async Task should_inject_by_constructor_with_default_parameters()
    {
        using var container = new Container();
        var instance = new TypeA();
        container.RegisterInstance(instance).AsSelf();
        container.Register<ConstructorWithDefaultParameter>().Singleton().AsSelf();
        await Assert.That(container.Resolve<ConstructorWithDefaultParameter>().TypeA).IsSameReferenceAs(instance);
        await Assert.That(container.Resolve<ConstructorWithDefaultParameter>().IntValue).IsEqualTo(10);
    }

    [Test]
    public async Task should_resolve_complex_type()
    {
        using var container = new Container();
        var typeA = new TypeA();
        container.RegisterInstance(typeA).AsSelf().As<InterfaceA>();
        container.Register<InjectConstructor>().AsSelf();
        container.Register<ConstructorWithDefaultParameter>().Singleton().AsSelf();
        container.Register<DefaultConstructor>().Singleton().AsSelf();
        container.Register<Func<int>>((c, t) => container.Resolve<DefaultConstructor>().GetIntValue).AsSelf();
        container.Register<ComplexClass>().AsSelf();
        var complex1 = container.Resolve<ComplexClass>();
        await Assert.That(complex1.A).IsSameReferenceAs(typeA);
        await Assert.That(complex1.B).IsSameReferenceAs(typeA);
        await Assert.That(complex1.C.TypeA).IsSameReferenceAs(typeA);
        await Assert.That(complex1.C.TypeA).IsSameReferenceAs(typeA);
        await Assert.That(complex1.D).IsEqualTo(22f);
        await Assert.That(complex1.E.TypeA).IsSameReferenceAs(typeA);
        await Assert.That(complex1.E.IntValue).IsEqualTo(10);
        await Assert.That(complex1.GetIntValue()).IsEqualTo(100);

        var complex2 = container.Resolve<ComplexClass>();
        await Assert.That(complex2.A).IsSameReferenceAs(typeA);
        await Assert.That(complex2.B).IsSameReferenceAs(typeA);
        await Assert.That(complex2.C.TypeA).IsSameReferenceAs(typeA);
        await Assert.That(complex2.C.TypeA).IsSameReferenceAs(typeA);
        await Assert.That(complex2.D).IsEqualTo(22f);
        await Assert.That(complex2.E.TypeA).IsSameReferenceAs(typeA);
        await Assert.That(complex2.E.IntValue).IsEqualTo(10);
        await Assert.That(complex2.GetIntValue()).IsEqualTo(100);

        await Assert.That(complex2.C).IsNotSameReferenceAs(complex1.C);
        await Assert.That(complex2.E).IsSameReferenceAs(complex1.E);
    }

    [Test]
    public async Task should_able_to_register_and_resolve_func()
    {
        using var container = new Container();
        var instance = new TypeA();
        container.RegisterInstance(instance).AsSelf();
        container.Register<DefaultConstructor>().Singleton().AsSelf();
        container.Register<Func<int>>((c, t) => container.Resolve<DefaultConstructor>().GetIntValue).AsSelf();
        await Assert.That(container.Resolve<Func<int>>()()).IsEqualTo(100);
    }

    [Test]
    public async Task should_override_register()
    {
        using var container = new Container();
        container.RegisterInstance(10).AsSelf();
        container.RegisterInstance(20).AsSelf();
        await Assert.That(container.Resolve<int>()).IsEqualTo(20);
    }

    [Test]
    public async Task should_throw_if_cannot_resolve()
    {
        using var container = new Container();
        container.Register<DefaultConstructor>().Singleton().AsSelf();
        await Assert.That(() => container.Resolve<TypeA>()).ThrowsException();
        await Assert.That(() => container.Resolve<DefaultConstructor>()).ThrowsException();
    }

    internal class Injected
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
#pragma warning disable IDE0051 // Intentionally not marked with [Inject] to verify unmarked methods are not called
        void Init(int e)
        {
            E = e;
        }
#pragma warning restore IDE0051
    }

    [Test]
    public async Task should_inject_marked_members()
    {
        using var container = new Container();
        container.RegisterInstance<int>(10).AsSelf();
        container.RegisterInstance<float>(100f).AsSelf();
        container.RegisterInstance<double>(0.999).AsSelf();
        var instance = new Injected();
        container.InjectAll(instance);
        await Assert.That(instance.Int).IsEqualTo(10);
        await Assert.That(instance.Float).IsEqualTo(100f);
        await Assert.That(instance.Double).IsEqualTo(0.999);
        await Assert.That(instance.A).IsEqualTo(10);
        await Assert.That(instance.B).IsEqualTo(0.999);
        await Assert.That(instance.C).IsEqualTo(100f);
        await Assert.That(instance.D).IsEqualTo(20L);
        await Assert.That(instance.E).IsEqualTo(0);
    }

    [Test]
    public async Task should_throw_if_not_able_to_inject()
    {
        using var container = new Container();
        await Assert.That(() => container.InjectAll(new Injected())).ThrowsException();
    }

    [Test]
    public async Task should_inject_and_call_function()
    {
        using var container = new Container();
        container.RegisterInstance(10).AsSelf();
        await Assert.That(container.CallFunc<Func<int, int>>(v => v * 2)).IsEqualTo(20);
    }

    [Test]
    public async Task should_inject_and_call_func_with_default_argument()
    {
        using var container = new Container();
        container.RegisterInstance(100).AsSelf();
        await Assert.That(container.CallFunc<Func<int, float, float>>(AddFunc)).IsEqualTo(200f);
    }

    float AddFunc(int a, float b = 100) => a + b;

    [Test]
    public async Task should_inject_and_call_action()
    {
        using var container = new Container();
        var intValue = 0;
        container.RegisterInstance(10).AsSelf();
        container.CallAction<Action<int>>(value => intValue = value * 2);
        await Assert.That(intValue).IsEqualTo(20);
    }

    [Test]
    public async Task should_inject_and_call_action_with_default_argument()
    {
        using var container = new Container();
        container.RegisterInstance(10).AsSelf();
        container.CallAction<Action<int, float>>(AddAction);
        await Assert.That(_value).IsEqualTo(30f);
    }

    private float _value;
    void AddAction(int a, float b = 20) => _value = a + b;

    [Test]
    public async Task should_instantiate_by_type()
    {
        using var container = new Container();
        container.Register<TypeA>().Singleton().AsSelf();
        await Assert.That(container.Instantiate<DefaultConstructor>().TypeA).IsEqualTo(container.Resolve<TypeA>());
    }

    internal class IntArrayClass
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
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "TUnit's IsEquivalentTo uses reflection for structural comparison; the test only compares int arrays which are trim-safe in practice.")]
    public async Task should_resolve_group_of_type()
    {
        using var container = new Container();
        using var child1 = container.CreateChildContainer();
        using var child11 = child1.CreateChildContainer();
        using var child12 = child1.CreateChildContainer();
        using var child2 = container.CreateChildContainer();

        await Assert.That(container.ResolveGroup<int>()).IsEmpty();

        container.RegisterInstance(10).AsSelf();
        container.RegisterInstance(11).AsSelf();
        child1.RegisterInstance(20).AsSelf();
        child1.RegisterInstance(22).AsSelf();
        child2.RegisterInstance(30).AsSelf();
        child11.RegisterInstance(40).AsSelf();
        child12.RegisterInstance(50).AsSelf();
        await Assert.That(child12.ResolveGroup<int>().ToArray()).IsEquivalentTo(new[] { 50, 22, 20, 11, 10 });
        await Assert.That(child11.ResolveGroup<int>().ToArray()).IsEquivalentTo(new[] { 40, 22, 20, 11, 10 });
        await Assert.That(child2.ResolveGroup<int>().ToArray()).IsEquivalentTo(new[] { 30, 11, 10 });
        await Assert.That(child1.ResolveGroup<int>().ToArray()).IsEquivalentTo(new[] { 22, 20, 11, 10 });
        await Assert.That(container.ResolveGroup<int>().ToArray()).IsEquivalentTo(new[] { 11, 10 });

        var instance = child12.Instantiate<IntArrayClass>();
        await Assert.That(instance.IntValue).IsEqualTo(50);
        await Assert.That(instance.IntArray).IsEquivalentTo(new[] { 50, 22, 20, 11, 10 });

        instance = container.Instantiate<IntArrayClass>();
        await Assert.That(instance.IntValue).IsEqualTo(11);
        await Assert.That(instance.IntArray).IsEquivalentTo(new[] { 11, 10 });
    }
#pragma warning restore IL2026

    internal class InjectMethod
    {
#pragma warning disable IDE0051 // Method is invoked by DI framework via [Inject] attribute
        [Inject]
        void Inject(InterfaceA _)
        {
            // No-op: test verifies the DI framework invokes the [Inject] method without throwing.
        }
#pragma warning restore IDE0051
    }

    [Test]
    public async Task should_able_to_inject_without_resolve()
    {
        using var container = new Container();
        container.Register<TypeA>().Singleton().AsInterfaces();
        await Assert.That(() => container.InjectAll(new InjectMethod())).ThrowsNothing();
    }

    internal class InjectTypeA
    {
        [Inject]
        public void Inject(TypeA _)
        {
            // No-op: test verifies repeated injection on the same instance.
        }
    }

    [Test]
    public void should_inject_method_to_same_instance_repeatedly()
    {
        using var container = new Container();
        container.Register<TypeA>().AsSelf();
        var instance = new InjectTypeA();
        container.InjectAll(instance);
        container.InjectAll(instance);
        container.InjectAll(instance);
    }

    internal class TypeAA : TypeA
    {
    }

    internal class TypeAAA : TypeAA
    {
    }

    [Test]
    public async Task should_register_and_resolve_by_bases()
    {
        using var container = new Container();
        container.Register<TypeAAA>().AsBases();
        await Assert.That(container.Resolve<TypeAA>()).IsTypeOf<TypeAAA>();
        await Assert.That(container.Resolve<TypeA>()).IsTypeOf<TypeAAA>();
    }

    internal class InjectFloat
    {
        [Inject] public float FloatValue;
    }

    internal class InjectIntFloat : InjectFloat
    {
        [Inject] public int IntValue;
    }

    [Test]
    public async Task should_inject_all_for_instance_by_contract_type()
    {
        using var container = new Container();
        container.RegisterInstance(123).AsSelf();
        container.RegisterInstance(222.222f).AsSelf();
        var instance = new InjectIntFloat();
        container.InjectAll((InjectFloat)instance);
        await Assert.That(instance.IntValue).IsEqualTo(123);
        await Assert.That(instance.FloatValue).IsEqualTo(222.222f);
    }

    [Test]
    public async Task should_get_null_if_try_resolved_type_not_registered()
    {
        using var container = new Container();
        await Assert.That(container.TryResolve<int>()).IsNull();
    }

    [Test]
    public async Task should_check_registered_of_a_type()
    {
        using var container = new Container();
        container.RegisterInstance(123).AsSelf();
        await Assert.That(container.IsRegisteredInHierarchy<int>()).IsTrue();
        await Assert.That(container.IsRegisteredInHierarchy<float>()).IsFalse();
        await Assert.That(container.IsRegistered<int>()).IsTrue();
        await Assert.That(container.IsRegistered<float>()).IsFalse();

        using var child = container.CreateChildContainer();
        await Assert.That(child.IsRegisteredInHierarchy<int>()).IsTrue();
        await Assert.That(child.IsRegisteredInHierarchy<float>()).IsFalse();
        await Assert.That(child.IsRegistered<int>()).IsFalse();
        await Assert.That(child.IsRegistered<float>()).IsFalse();
    }
}
