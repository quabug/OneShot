# One Shot Dependency Injection
A [single file](Packages/com.quabug.one-shot-injection/OneShot.cs) DI container

## Basic Concept of DI
- [How YOU can Learn Dependency Injection in .NET Core and C#](https://softchris.github.io/pages/dotnet-di.html)
- [vContainer](https://vcontainer.hadashikick.jp/about/what-is-di)

## Installation
- Copy and paste [OneShot.cs](Packages/com.quabug.one-shot-injection/OneShot.cs) into your project.
- [Unity-Only] or follow instructions on [OpenUPM](https://openupm.com/packages/com.quabug.one-shot-injection) to install it as a package of Unity.

## Usage
[Test Cases](Test/TestOneShot.cs)

### [Container](Packages/com.quabug.one-shot-injection/OneShot.cs#L9)
A scope mark for registered types.

``` c#
// create new container
var container = new Container();

// create child container/scope
var child = container.CreateChildContainer();

// create a scope container (exactly same as child container)
using (var scope = container.BeginScope())
```

### [Register Types](Packages/com.quabug.one-shot-injection/OneShot.cs#L47)
``` c#
container.RegisterInstance<int>(10).AsSelf(); // register instance of int
container.Register<Foo>().Singleton().AsSelf(); // register a singleton of `Foo`
container.Register<Bar>().AsSelf(); // register transient of `Bar`
container.Register<Func<int>>((resolveContainer, contractType) => container.Resolve<Foo>().GetIntValue).AsSelf(); // register `Func<int>`
conatiner.Register<IFoo>((resolveContainer, contractType) => container.Resolve<Foo>()).AsSelf(); // register interface of `IFoo`
```

### [Resolve](Packages/com.quabug.one-shot-injection/OneShot.cs#L37)
``` c#
container.Resolve<int>();
container.Resolve<IFoo>();
```

### [InjectAttribute](Packages/com.quabug.one-shot-injection/OneShot.cs#L12)
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

### A Complex Use Case
[GraphExt](https://github.com/quabug/GraphExt/tree/main/Packages/com.quabug.graph-ext/DI)
