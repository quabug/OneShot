[![NuGet Version](https://img.shields.io/nuget/v/OneShot)](https://www.nuget.org/packages/OneShot/)

# OneShot Dependency Injection

A lightweight, high-performance dependency injection container for .NET with source generator support for AOT-friendly, zero-reflection DI.

## Features

- **Source Generator** - Compile-time code generation replaces runtime reflection for NativeAOT compatibility
- **High Performance** - Zero-reflection instance creation via generated `ITypeInfo` implementations
- **Thread Safe** - All public APIs are thread-safe using concurrent collections
- **Hierarchical Containers** - Parent-child relationships with proper disposal chains
- **Flexible Registration** - Instance, Transient, Singleton, Scoped, and Factory patterns
- **Type-Safe Labels** - Support for labeled dependencies with compile-time safety
- **Generic Support** - Open generic type registration and resolution

## Requirements

- **.NET**: 10.0 or higher
- **C#**: 12.0 or higher

## Basic Concept of DI
- [How YOU can Learn Dependency Injection in .NET Core and C#](https://softchris.github.io/pages/dotnet-di.html)
- [vContainer](https://vcontainer.hadashikick.jp/about/what-is-di)

## Installation

Install via [NuGet](https://www.nuget.org/packages/OneShot/):
```bash
dotnet add package OneShot
```

## Quick Start

```csharp
using OneShot;

// Create container
var container = new Container();

// Register types
container.Register<DatabaseService>().Singleton().AsInterfaces();
container.Register<UserRepository>().Scoped().AsSelf();
container.RegisterInstance<ILogger>(new ConsoleLogger()).AsSelf();

// Resolve dependencies
var repository = container.Resolve<UserRepository>();
```

## Source Generation

OneShot uses a C# incremental source generator to emit `ITypeInfo` implementations at compile time, replacing runtime reflection entirely. Types are discovered for generation through two mechanisms:

### Call-Site Scanning
Types used via `Register<T>()` or `Instantiate<T>()` are automatically discovered — no attributes required:

```csharp
// Source generator detects these call sites and generates ITypeInfo for MyService
container.Register<MyService>().Singleton().AsSelf();
container.Instantiate<MyService>();
```

### `[Inject]` Attribute
Types with `[Inject]` on any member (field, property, method, constructor, or parameter) are also discovered:

```csharp
class MyService
{
    [Inject] public ILogger Logger;  // Triggers source generation for MyService
}
```

### Runtime Registration
When using runtime `Register(Type)` with a variable type, the source generator cannot discover the type from call sites. Ensure the type is referenced via `Register<T>()` or `Instantiate<T>()` somewhere (even in a never-called method), or has `[Inject]` on a member:

```csharp
// This alone won't trigger source generation:
container.Register(someType);

// Add a manifest method to ensure generation:
static void _SourceGenManifest(Container c)
{
    c.Register<MyType>();  // Triggers generation even though never called
}
```

## Core Usage

> See [Test Cases](tests/OneShot.Tests) for comprehensive examples

### Container Management

```csharp
// Create root container
var container = new Container();

// Create child container (inherits parent registrations)
var child = container.CreateChildContainer();

// Create scoped container (auto-disposed)
using (var scope = container.BeginScope())
{
    // Scoped registrations live here
}

// Performance Options
container.EnableCircularCheck = false; // Disable circular dependency checking (default: true in DEBUG)
container.PreventDisposableTransient = true; // Prevent memory leaks (default: false)
```

### Registration Patterns

#### Lifetimes
```csharp
// Transient - New instance each time (default)
container.Register<Service>().AsSelf();

// Singleton - Single instance per container hierarchy
container.Register<Service>().Singleton().AsSelf();

// Scoped - Single instance per container scope
container.Register<Service>().Scoped().AsSelf();

// Instance - Register existing instance
container.RegisterInstance<IConfig>(new AppConfig()).AsSelf();
```

#### Interface and Base Class Registration
```csharp
// Register as specific interface
container.Register<Service>().As<IService>();

// Register as all interfaces
container.Register<Service>().AsInterfaces();

// Register as all base classes
container.Register<Service>().AsBases();

// Register as self and interfaces
container.Register<Service>().AsSelf().AsInterfaces();
```

#### Advanced Registration
```csharp
// Factory registration
container.Register<Func<int>>((container, type) => () => 42).AsSelf();

// With specific constructor parameters
container.Register<Service>().With("config", 123).AsSelf();

// Generic type registration
container.RegisterGeneric(typeof(Repository<>), CreateRepository).AsSelf();
```

### Resolution

```csharp
// Basic resolution
var service = container.Resolve<IService>();

// Generic resolution
var repository = container.Resolve<Repository<User>>();

// Group resolution
var services = container.ResolveGroup<IService>();

// Create instance without registration
var instance = container.Instantiate<MyClass>();
```

### Injection Types

```csharp
class Service
{
    // Constructor injection (preferred)
    [Inject]
    public Service(IDatabase db, ILogger logger) { }

    // Field injection
    [Inject] private ICache _cache;

    // Property injection
    [Inject] public IConfig Config { get; set; }

    // Method injection
    [Inject]
    public void Initialize(IEventBus eventBus) { }
}

// Manual injection
var service = new Service();
container.InjectAll(service); // Injects fields, properties, and methods
```

### Labels (Named Dependencies)

```csharp
// Define labels
interface PrimaryDb : ILabel<IDatabase> { }  // Type-specific label
interface SecondaryDb : ILabel<IDatabase> { }
interface Cache<T> : ILabel<T> { }  // Generic label

// Register with labels
container.Register<PostgresDb>().As<IDatabase>(typeof(PrimaryDb));
container.Register<MySqlDb>().As<IDatabase>(typeof(SecondaryDb));
container.Register<CachedRepository>().As<IRepository>(typeof(Cache<>));

// Use labeled dependencies
class Service
{
    public Service(
        [Inject(typeof(PrimaryDb))] IDatabase primary,
        [Inject(typeof(SecondaryDb))] IDatabase secondary,
        [Inject(typeof(Cache<>))] IRepository cached
    ) { }
}
```

## Advanced Features

### Circular Dependency Detection

```csharp
// Automatically detected and throws descriptive exception
container.Register<A>().AsSelf(); // A depends on B
container.Register<B>().AsSelf(); // B depends on A
var a = container.Resolve<A>(); // Throws CircularDependencyException
```

### Disposal Management

```csharp
// IDisposable instances are automatically disposed
using (var scope = container.BeginScope())
{
    var service = scope.Resolve<DisposableService>();
} // service.Dispose() called automatically

// Child containers cascade disposal
container.Dispose(); // Disposes all child containers and registered IDisposables
```

## Best Practices

1. **Prefer Constructor Injection** - Most explicit and testable
2. **Use Scoped for Request/Frame Lifetime** - Ideal for per-request isolation
3. **Avoid Disposable Transients** - Can cause memory leaks
4. **Use Labels for Multiple Implementations** - Type-safe alternative to string keys
5. **Create Child Containers for Isolation** - Test scenarios or modular features

## Development

### Build
```bash
dotnet build
```

### Test
```bash
dotnet test
```

### Benchmarks
```bash
dotnet run -c Release --project benchmarks/OneShot.Benchmarks
```

## Contributing

Contributions welcome! Please ensure:
- All tests pass
- No compiler warnings (warnings as errors enabled)
- Thread safety maintained
- NativeAOT compatibility preserved (no runtime reflection in new code)

## License

MIT License - See [LICENSE](LICENSE) file for details
