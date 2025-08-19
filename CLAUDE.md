# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OneShot is a lightweight, single-file dependency injection container for Unity and .NET. The core philosophy is simplicity - the entire DI container is implemented in a single C# file that can be copied into any project.

## Key Commands

### .NET Development (run from `.NET/` directory)

```bash
# Build the solution
dotnet build --no-restore

# Run all tests
dotnet test --no-build --verbosity normal

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Pack NuGet package
dotnet pack -c Release -o out ./OneShotInjection

# Benchmark tests
dotnet run -c Release --project Benchmark
```

### Unity Development

Unity tests are run through the Unity Test Runner. The project supports both Mono2x and IL2CPP scripting backends.

## Architecture Overview

### Core Structure

The project has two main distribution paths:

1. **Unity Package** (`Packages/com.quabug.one-shot-injection/`)
   - `OneShot.cs` - The main DI container implementation
   - `ContainerComponent.cs` - Unity MonoBehaviour integration
   - `Injector.cs` - Static injection utilities

2. **.NET Library** (`.NET/OneShotInjection/`)
   - Same `OneShot.cs` core file
   - NuGet packaging configuration
   - Cross-platform .NET support

### Key Design Decisions

1. **Single File Design**: The entire DI container is in `OneShot.cs` to allow easy copy-paste distribution
2. **Expression Compilation**: Uses `System.Linq.Expressions` for fast instance creation (automatically disabled for IL2CPP)
3. **Thread Safety**: Uses `ConcurrentDictionary` for all registrations
4. **Hierarchical Containers**: Supports parent-child container relationships with proper disposal chains

### Container Features

- **Registration Types**: Instance, Transient, Singleton, Factory
- **Injection Methods**: Constructor (primary), Field, Property, Method
- **Advanced Features**: Circular dependency detection, labeled dependencies, array resolution
- **Performance Options**: `PreAllocateArgumentArrayOnRegister`, `EnableCircularCheck` flags

### Testing Strategy

- Unit tests in both Unity (`Assets/Tests/`) and .NET (`Test/`)
- Benchmarks comparing against Microsoft.Extensions.DependencyInjection
- CI/CD runs tests on multiple platforms and Unity versions

## Important Development Notes

1. **Code Quality**: The project uses strict static analysis with warnings as errors
2. **Unity Compatibility**: Always test changes with both Mono2x and IL2CPP backends
3. **Performance**: Container is designed for high performance - benchmark any changes
4. **Thread Safety**: All public APIs must remain thread-safe