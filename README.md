[![NuGet Badge](https://buildstats.info/nuget/OneShot)](https://www.nuget.org/packages/OneShot/)
[![openupm](https://img.shields.io/npm/v/com.quabug.one-shot-injection?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.quabug.one-shot-injection/)

# One Shot Dependency Injection
A [single file](Packages/com.quabug.one-shot-injection/OneShot.cs) DI container

## Basic Concept of DI
- [How YOU can Learn Dependency Injection in .NET Core and C#](https://softchris.github.io/pages/dotnet-di.html)
- [vContainer](https://vcontainer.hadashikick.jp/about/what-is-di)

## Installation
- Copy and paste [OneShot.cs](Packages/com.quabug.one-shot-injection/OneShot.cs) into your project.
- [Unity only] or follow instructions on [OpenUPM](https://openupm.com/packages/com.quabug.one-shot-injection) to install it as a package of Unity.
- [.NET only] or follow instruction on [NuGet](https://www.nuget.org/packages/OneShot/) to install it for .NET project.

## Usage
[Test Cases](Assets/Tests)

### [Container](Packages/com.quabug.one-shot-injection/OneShot.cs#L39)
A scope mark for registered types.

``` c#
// create new container
var container = new Container();

// create child container/scope
var child = container.CreateChildContainer();

// create a scope container (exactly same as child container)
using (var scope = container.BeginScope())

// container options
// enable/disable circular check, enabled by default
container.EnableCircularCheck = false;

// pre-allocate argument array of registered type, disabled by default
container.PreAllocateArgumentArrayOnRegister = true;

// throw on register disposable transient, disabled by default
container.PreventDisposableTransient = true;
```

### [Register Types](Packages/com.quabug.one-shot-injection/OneShot.cs#L147)
``` c#
container.RegisterInstance<int>(10).AsSelf(); // register instance of int
container.Register<Foo>().Singleton().AsSelf(); // register a singleton of `Foo`
container.Register<Bar>().AsSelf(); // register transient of `Bar`
container.Register<Func<int>>((resolveContainer, contractType) => container.Resolve<Foo>().GetIntValue).AsSelf(); // register `Func<int>`
conatiner.Register<Foo>().As<IFoo>(); // register interface of `IFoo`
container.Register<Foo>().With(123, new Bar()).AsSelf(); // register with certain instances
container.Register(typeof(Generic<>), (_, type) => Activator.CreateInstance(type)).As(typeof(Generic<>)); // register generic type
```

### [Resolve](Packages/com.quabug.one-shot-injection/OneShot.cs#L182)
``` c#
container.Resolve<int>();
container.Resolve<IFoo>();
container.Resolve<Generic<int>>();
```

### [InjectAttribute](Packages/com.quabug.one-shot-injection/OneShot.cs#L39)
``` c#
class Foo
{
    public Foo() {}
    // mark a constructor to use on instantialize
    [Inject] public Foo(int value) {}

    [Inject] int IntValue; // field able to inject
    [Inject] float FloatValue { get; set; } // property albe to inject
    [Inject] void Init(int value) {} // method albe to inject
}

container.Register<Foo>().Singleton().AsSelf();
var foo = container.Resolve<Foo>(); // instantial `Foo` by `Foo(int value)`
container.InjectAll(foo); // inject its fields, properteis and methods
```

### [Label](Packages/com.quabug.one-shot-injection/OneShot.cs#L45)
``` c#
class Foo {}
interface TypedLabelFoo : ILabel<Foo> {} // declare type-specific label, will throw on labeling other type
interface AnyLabel<T> : ILabel<T> {} // declare generic-typed label, works on any types

class Bar
{
    public Bar(
        Foo foo, // un-labeled type
        [Inject(typeof(TypedLabelFoo))] Foo labeledFoo, // labeled type with type-specific label
        [Inject(AnyLabel<>)] Foo anyLabeledFoo, // labeled type
        [Inject(typeof(AnyLabel<>)] int anyLabeledInt  // labeled type
    ) {}
    
    [Inject(typeof(AnyLabel<>))] Foo LabeledProperty { get; private set; }
    [Inject(typeof(FooLabel))] Foo LabeledField;
}

container.Register<Foo>().AsSelf(typeof(TypedLabel)); // register typed label foo
container.Register<Bar>().With((123, typeof(AnyLabel<>)), (new Foo(), typeof(AnyLabel<>))).AsSelf(); // register additional-labeled instances
```

### A Complex Use Case
[GraphExt](https://github.com/quabug/GraphExt/tree/main/Packages/com.quabug.graph-ext/DI)
