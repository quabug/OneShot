[![NuGet Badge](https://buildstats.info/nuget/OneShot)](https://www.nuget.org/packages/OneShot/)
[![openupm](https://img.shields.io/npm/v/com.quabug.one-shot-injection?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.quabug.one-shot-injection/)

# OneShot Dependency Injection

A lightweight, high-performance, [single-file](Packages/com.quabug.one-shot-injection/OneShot.cs) dependency injection container for Unity and .NET.

## Features

- 🎯 **Single File** - Entire DI container in one file for easy copy-paste distribution
- ⚡ **High Performance** - Expression compilation for fast instance creation (with IL2CPP fallback)
- 🔒 **Thread Safe** - All public APIs are thread-safe using concurrent collections
- 🎮 **Unity Integration** - Built-in MonoBehaviour components and scene injection
- 🏗️ **Hierarchical Containers** - Parent-child relationships with proper disposal chains
- 🔧 **Flexible Registration** - Instance, Transient, Singleton, Scoped, and Factory patterns
- 🏷️ **Type-Safe Labels** - Support for labeled dependencies with compile-time safety
- 📦 **Generic Support** - Full open generic type registration and resolution

## Requirements

- **Unity**: 2019.3 or higher
- **.NET**: .NET Standard 2.1 or higher
- **C#**: 9.0 or higher

## Basic Concept of DI
- [How YOU can Learn Dependency Injection in .NET Core and C#](https://softchris.github.io/pages/dotnet-di.html)
- [vContainer](https://vcontainer.hadashikick.jp/about/what-is-di)

## Installation

### Option 1: Single File (Simplest)
Copy and paste [OneShot.cs](Packages/com.quabug.one-shot-injection/OneShot.cs) into your project.

### Option 2: Unity Package Manager
Install via [OpenUPM](https://openupm.com/packages/com.quabug.one-shot-injection):
```bash
openupm add com.quabug.one-shot-injection
```

### Option 3: NuGet Package (.NET)
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

## Core Usage

> 📚 See [Test Cases](Assets/Tests) for comprehensive examples

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
container.PreAllocateArgumentArrayOnRegister = true; // Pre-allocate for performance (default: false)
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

// Multiple registrations for array resolution
container.Register<IPlugin>().Multiple().AsInterfaces();
```

### Resolution

```csharp
// Basic resolution
var service = container.Resolve<IService>();

// Generic resolution
var repository = container.Resolve<Repository<User>>();

// Array resolution (gets all registered implementations)
var plugins = container.Resolve<IPlugin[]>();

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

## Unity Integration

### ContainerComponent

```csharp
// Attach container to GameObject
var containerComponent = gameObject.AddComponent<ContainerComponent>();
containerComponent.Value = container;

// Auto-disposal on GameObject destruction
```

### Injector Component

```csharp
// Add Injector to GameObject for automatic injection
var injector = gameObject.AddComponent<Injector>();
injector.InjectionPhase = InjectionPhase.Awake; // or Start, Update, LateUpdate, Manual

// Components on this GameObject will be injected automatically
```

### Scene Injection

```csharp
// Inject all eligible components in scene
container.InjectScene();

// Prevent injection on specific GameObjects
gameObject.AddComponent<StopInjection>();
```

### Installer Pattern

```csharp
public class GameInstaller : MonoBehaviour, IInstaller
{
    public void Install(Container container)
    {
        container.Register<PlayerController>().Singleton().AsSelf();
        container.Register<GameManager>().Scoped().AsInterfaces();
        container.Register<AudioSystem>().Singleton().AsBases();
    }
}
```

## Performance Optimization

### Configuration Options

```csharp
// For high-frequency resolution scenarios
container.PreAllocateArgumentArrayOnRegister = true;

// Disable circular dependency checking in production
#if !DEBUG
container.EnableCircularCheck = false;
#endif

// Prevent memory leaks from disposable transients
container.PreventDisposableTransient = true;
```

### IL2CPP Compatibility

OneShot automatically detects IL2CPP and falls back to reflection-based instantiation when expression compilation is unavailable.

```csharp
// Works seamlessly in both Mono and IL2CPP
container.Register<Service>().Singleton().AsSelf();
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
2. **Use Scoped for Request/Frame Lifetime** - Ideal for Unity Update loops
3. **Avoid Disposable Transients** - Can cause memory leaks
4. **Use Labels for Multiple Implementations** - Type-safe alternative to string keys
5. **Create Child Containers for Isolation** - Test scenarios or modular features

## Benchmarks

OneShot is optimized for performance. Run benchmarks:

```bash
cd .NET/
dotnet run -c Release --project Benchmark
```

## Testing

### .NET Tests
```bash
cd .NET/
dotnet test --no-build --verbosity normal
```

### Unity Tests
Run via Unity Test Runner in the Unity Editor

## Contributing

Contributions welcome! Please ensure:
- All tests pass on both Mono and IL2CPP
- No compiler warnings (warnings as errors enabled)
- Thread safety maintained
- Single-file philosophy preserved

## License

MIT License - See [LICENSE](LICENSE) file for details
