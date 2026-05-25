using System.Diagnostics.CodeAnalysis;

namespace OneShot.Test;

// v4-specific surface area: the source generator's call-site discovery, the
// generated ITypeInfo path (vs runtime reflection), and child-container
// override semantics. These don't exercise new APIs - they verify the v4
// rewrite preserved behavior callers may have depended on.
public class TestSourceGenerator
{
    internal class GenCallSiteService
    {
        public TypeA Dep { get; }
        public GenCallSiteService(TypeA dep) => Dep = dep;
    }

    [Test]
    public async Task generic_register_call_site_emits_typeinfo()
    {
        // Register<T>() is the trigger - if the generator hadn't seen this site
        // in the assembly, container.Register<T>() would throw NotSupportedException
        // because TypeInfoRegistry would have no entry for GenCallSiteService.
        using var container = new Container();
        var typeA = new TypeA();
        container.RegisterInstance(typeA).AsSelf();
        container.Register<GenCallSiteService>().Singleton().AsSelf();
        await Assert.That(container.Resolve<GenCallSiteService>().Dep).IsSameReferenceAs(typeA);
    }

    internal class InstantiateOnlyService
    {
        public TypeA Dep { get; }
        public InstantiateOnlyService(TypeA dep) => Dep = dep;
    }

    [Test]
    public async Task instantiate_call_site_alone_triggers_generation()
    {
        // Instantiate<T>() is the only call site for InstantiateOnlyService anywhere
        // in the assembly. The generator must have emitted ITypeInfo for it even
        // though we never Register<T>() it.
        using var container = new Container();
        container.RegisterInstance(new TypeA()).AsSelf();
        var instance = container.Instantiate<InstantiateOnlyService>();
        await Assert.That(instance.Dep).IsNotNull();
    }

    internal class InjectOnlyService
    {
        // The class is never used at a Register<T>() / Instantiate<T>() call site.
        // [Inject] on the constructor is what must trigger the generator.
        public TypeA Dep { get; }
        [Inject] public InjectOnlyService(TypeA dep) => Dep = dep;
    }

    [Test]
    [SuppressMessage("Usage", "CA2263",
        Justification = "Test deliberately uses Register(Type) so the only generator trigger for InjectOnlyService is the [Inject] attribute, not a Register<T>() call site.")]
    public async Task inject_attribute_alone_triggers_generation()
    {
        using var container = new Container();
        container.RegisterInstance(new TypeA()).AsSelf();
        container.Register(typeof(InjectOnlyService)).AsSelf();
        await Assert.That(container.Resolve<InjectOnlyService>().Dep).IsNotNull();
    }

    internal class PrivateSetterTarget
    {
        [Inject] public TypeA Dep { get; private set; }
    }

    [Test]
    public async Task inject_into_private_setter_via_unsafeaccessor()
    {
        // The generator must emit an UnsafeAccessor for the compiler-generated
        // backing field of a [Inject] property with a private setter. Verifies
        // injection sets it without falling back to reflection.
        using var container = new Container();
        var typeA = new TypeA();
        container.RegisterInstance(typeA).AsSelf();
        var target = new PrivateSetterTarget();
        container.InjectAll(target);
        await Assert.That(target.Dep).IsSameReferenceAs(typeA);
    }

    internal class OverridableService
    {
        public int Value { get; }
        public OverridableService(int value) => Value = value;
    }

    [Test]
    public async Task child_container_overrides_parent_registration_for_self()
    {
        using var parent = new Container();
        parent.RegisterInstance(1).AsSelf();
        parent.Register<OverridableService>().Scoped().AsSelf();

        using var child = parent.CreateChildContainer();
        child.RegisterInstance(99).AsSelf();

        await Assert.That(parent.Resolve<OverridableService>().Value).IsEqualTo(1);
        await Assert.That(child.Resolve<OverridableService>().Value).IsEqualTo(99);
    }

    [Test]
    public async Task scope_is_distinct_per_subtree_root()
    {
        using var parent = new Container();
        parent.Register<TypeA>().Scoped().AsSelf();

        using var siblingA = parent.CreateChildContainer();
        using var siblingB = parent.CreateChildContainer();

        var a = siblingA.Resolve<TypeA>();
        var b = siblingB.Resolve<TypeA>();
        var pRoot = parent.Resolve<TypeA>();

        await Assert.That(a).IsNotSameReferenceAs(b);
        await Assert.That(a).IsNotSameReferenceAs(pRoot);
        await Assert.That(b).IsNotSameReferenceAs(pRoot);
    }
}
