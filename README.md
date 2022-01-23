# One Shot Dependency Injection
A [single file](Packages/com.quabug.one-shot-injection/OneShot.cs) DI container

## Basic Concept of DI
- [How YOU can Learn Dependency Injection in .NET Core and C#](https://softchris.github.io/pages/dotnet-di.html)
- [vContainer](https://vcontainer.hadashikick.jp/about/what-is-di)

## Usage
[Test Cases](Test/TestOneShot.cs)

### [Container](Packages/com.quabug.one-shot-injection/OneShot.cs#L9)
A scope mark for registered types.

``` c#
// create new container
var container = new Container();

// create child container/scope
var child = container.CreateChildContainer();
```

### [Register Types](Packages/com.quabug.one-shot-injection/OneShot.cs#L47)
``` c#
container.RegisterInstance<int>(10); // register instance of int
container.RegisterSingleton<Foo>(); // register a singleton of `Foo`
container.RegisterTransient<Bar>(); // register transient of `Bar`
container.Register<Func<int>>(() => container.Resolve<Foo>().GetIntValue); // register `Func<int>`
conatiner.Register<IFoo>(() => container.Resolve<Foo>()); // register interface of `IFoo`
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

container.RegisterSingleton<Foo>();
var foo = container.Resolve<Foo>(); // instantial `Foo` by `Foo(int value)`
container.InjectAll(foo); // inject its fields, properteis and methods
```
