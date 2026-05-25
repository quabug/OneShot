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

- **Runtime library** (`OneShot`): netstandard2.1 — usable from any compatible TFM (net5.0+, Unity 2021 LTS+, etc.)
- **Source generator** (`OneShot.Generator`): targets netstandard2.0 (Roslyn analyzer requirement); requires the host project's Roslyn to be 4.12 or newer (.NET 8 SDK / Visual Studio 17.10+)
- **NativeAOT**: net8.0 or newer
- **C# language version**: 14.0 (set in `Directory.Build.props`)

## Basic Concept of DI
- [How YOU can Learn Dependency Injection in .NET Core and C#](https://softchris.github.io/pages/dotnet-di.html)
- [vContainer](https://vcontainer.hadashikick.jp/about/what-is-di)

## Installation

Both packages from [NuGet](https://www.nuget.org/packages/OneShot/):

```bash
dotnet add package OneShot              # runtime container
dotnet add package OneShot.Generator    # incremental source generator
```

The generator is a separate package because it ships as a Roslyn analyzer (netstandard2.0). Without it, the container falls back to throwing `NotSupportedException` for any type it can't find in the registry — runtime reflection is not used at all.

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
When you call `Register(Type)` with a `Type` known only at runtime, the source generator has no syntactic site to scan from. Make sure the type is also referenced from a generic call site somewhere in the assembly (even in dead code), or carries `[Inject]` on at least one member:

```csharp
// This alone won't trigger source generation:
container.Register(someType);

// Add a manifest method to anchor generation. The method never has to be called -
// the generator only cares about the call sites' syntax.
file static class _SourceGenManifest
{
    static void Anchor(Container c)
    {
        c.Register<MyType>();   // generator sees this and emits ITypeInfo for MyType
        c.Register<OtherType>();
    }
}
```

## NativeAOT compatibility

The runtime library is annotated so the AOT analyzer can tell callers which APIs are safe under trimming:

| API | AOT status |
| --- | --- |
| `Register<T>()` / `Instantiate<T>()` + `[Inject]`-driven constructor/field/property/method injection | Fully AOT-safe — flows through the source generator with no warnings |
| `GenericExtension.RegisterGeneric(Type, MethodInfo)` | `[RequiresDynamicCode]` + `[RequiresUnreferencedCode]` — callers get warnings |
| `LabelExtension.CreateLabelType` | `[RequiresDynamicCode]` — callers get warnings |
| `Container.TryResolve` (label or array path), `ResolverBuilder.As(contractType, label)` | Suppressed at the boundary; the `MakeGenericType` / `Array.CreateInstance` paths only fire when you opt in by passing a label or resolving an array type. No analyzer warnings, but callers using those code paths are responsible for keeping the requested types reachable for the trimmer. |

When publishing with `PublishAot=true`, the only diagnostics you'll see come from `RegisterGeneric` and explicit `CreateLabelType` calls — typical apps that stick to `Register<T>()` / `Instantiate<T>()` build clean.

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

// Generic type registration. Uses MakeGenericMethod at runtime, so this API is
// annotated [RequiresDynamicCode]/[RequiresUnreferencedCode] - not AOT-friendly.
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

Tests use [TUnit](https://github.com/thomhurst/TUnit), which compiles into a self-running executable. Run them with `dotnet run`, not `dotnet test`:

```bash
dotnet run --project tests/OneShot.Tests              # runtime container tests (75 tests)
dotnet run --project tests/OneShot.Generator.Tests    # generator snapshot tests (14 tests)
```

### NativeAOT smoke test
```bash
dotnet publish tests/OneShot.Tests -c Release         # publishes a NativeAOT binary; CI runs this
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
