using System.Reflection;

namespace OneShot.Test;

#pragma warning disable IL2075, IL3050, IL2091 // Test code intentionally uses reflection and dynamic generic types
public class TestGeneric
{
    class Generic<T>
    {
    }

    class Generic<T, U>
    {
    }

    public static Type[] GenericArguments() =>
    [
        typeof(Generic<int>), typeof(Generic<float>), typeof(Generic<Generic<int>>), typeof(Generic<Generic<Generic<int>>>),
        typeof(Generic<int, float>), typeof(Generic<Generic<int>, Generic<float>>), typeof(Generic<Generic<int, float>>)
    ];

    [Test]
    [MethodDataSource(nameof(GenericArguments))]
    public async Task should_make_instance_of_generic(Type type)
    {
        var container = new Container();
        container.Register(typeof(Generic<>), (_, t) => Activator.CreateInstance(typeof(Generic<>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<>));
        container.Register(typeof(Generic<,>), (_, t) => Activator.CreateInstance(typeof(Generic<,>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<,>));
        await Assert.That(container.Resolve(type).GetType()).IsEqualTo(type);
    }

    interface Label<T> : ILabel<T>
    {
    }

    [Test]
    [MethodDataSource(nameof(GenericArguments))]
    public async Task should_make_instance_of_generic_with_label(Type type)
    {
        var container = new Container();
        container.Register(typeof(Generic<>), (_, t) => Activator.CreateInstance(typeof(Generic<>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<>), typeof(Label<>));
        container.Register(typeof(Generic<,>), (_, t) => Activator.CreateInstance(typeof(Generic<,>).MakeGenericType(t.GetGenericArguments()))).As(typeof(Generic<,>), typeof(Label<>));
        await Assert.That(() => container.Resolve(type)).ThrowsException();
        await Assert.That(container.Resolve(type, typeof(Label<>)).GetType()).IsEqualTo(type);
    }

    [Test]
    public async Task should_make_instance_of_concrete_generic()
    {
        var container = new Container();
        var instance = new Generic<int>();
        container.RegisterInstance(instance).As<Generic<int>>();
        await Assert.That(container.Resolve<Generic<int>>()).IsSameReferenceAs(instance);
    }

    private static Lazy<T> CreateLazy<T>(Container container, Type _)
    {
        return new Lazy<T>(() => container.Resolve<T>());
    }

    [Test]
    public async Task should_register_and_resolve_generic_type_by_method_name()
    {
        var container = new Container();
        var creator = GetType().GetMethod(nameof(CreateLazy), BindingFlags.Static | BindingFlags.NonPublic);
        container.RegisterGeneric(typeof(Lazy<>), creator).With(123).AsSelf();
        await Assert.That(container.Resolve<Lazy<int>>().Value).IsEqualTo(123);
    }

    private static Generic<T, U> CreateGeneric<T, U>(Container _, Type _1)
    {
        return new Generic<T, U>();
    }

    private static void InvalidReturnCreator<T, U>(Container _, Type _1)
    {
    }

    private static Generic<T, U> InvalidParameterCreator<T, U>(Container _)
    {
        return new Generic<T, U>();
    }

    [Test]
    public void should_register_and_resolve_generic_with_multiple_type_parameters_by_method_name()
    {
        var container = new Container();
        var creator = GetType().GetMethod(nameof(CreateGeneric), BindingFlags.Static | BindingFlags.NonPublic);
        container.RegisterGeneric(typeof(Generic<,>), creator).AsSelf();
        var instance = container.Resolve<Generic<int, float>>();
    }

    [Test]
    public async Task should_throw_exception_if_creator_is_not_valid()
    {
        var container = new Container();
        await Assert.That(() => container.RegisterGeneric(typeof(Generic<,>), null).AsSelf()).ThrowsExactly<ArgumentNullException>();

        var lazyCreator = GetType().GetMethod(nameof(CreateLazy), BindingFlags.Static | BindingFlags.NonPublic);
        await Assert.That(() => container.RegisterGeneric(typeof(Generic<>), lazyCreator).AsSelf()).ThrowsExactly<ArgumentException>();
        await Assert.That(() => container.RegisterGeneric(typeof(Generic<,>), lazyCreator).AsSelf()).ThrowsExactly<ArgumentException>();

        var genericCreator = GetType().GetMethod(nameof(CreateGeneric), BindingFlags.Static | BindingFlags.NonPublic);
        await Assert.That(() => container.RegisterGeneric(typeof(Generic<>), genericCreator).AsSelf()).ThrowsExactly<ArgumentException>();

        var invalidReturnCreator = GetType().GetMethod(nameof(InvalidReturnCreator), BindingFlags.Static | BindingFlags.NonPublic);
        await Assert.That(() => container.RegisterGeneric(typeof(Generic<,>), invalidReturnCreator).AsSelf()).ThrowsExactly<ArgumentException>();

        var invalidParameterCreator = GetType().GetMethod(nameof(InvalidParameterCreator), BindingFlags.Static | BindingFlags.NonPublic);
        await Assert.That(() => container.RegisterGeneric(typeof(Generic<,>), invalidParameterCreator).AsSelf()).ThrowsExactly<ArgumentException>();
    }
}
#pragma warning restore IL2075, IL3050, IL2091
