# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OneShot is a lightweight dependency injection container for .NET with source generator support for AOT-friendly, zero-reflection DI.

## Key Commands

```bash
# Build everything
dotnet build

# Run all tests (TUnit, via dotnet run)
dotnet run --project tests/OneShot.Tests
dotnet run --project tests/OneShot.Generator.Tests

# Run benchmarks
dotnet run -c Release --project benchmarks/OneShot.Benchmarks

# Publish NativeAOT binary
dotnet publish tests/OneShot.Tests -c Release

# Pack NuGet packages
dotnet pack -c Release -o out
```

## Architecture Overview

### Project Structure

```
src/OneShot/                  # Runtime library (NuGet: OneShot), net10.0, NativeAOT compatible
src/OneShot.Generator/        # Source generator (NuGet: OneShot.Generator), netstandard2.0
tests/OneShot.Tests/          # Unit tests (net10.0, TUnit, NativeAOT publishable)
tests/OneShot.Generator.Tests/ # Generator snapshot tests (net10.0, TUnit)
benchmarks/OneShot.Benchmarks/ # BenchmarkDotNet (net10.0)
```

### Key Design Decisions

1. **Source Generator**: Uses C# incremental source generator to emit `ITypeInfo` implementations, replacing runtime reflection for AOT compatibility
2. **ITypeInfo**: Interface for generated type metadata - `Create()` replaces constructor reflection, `InjectAll()` replaces member injection reflection
3. **TypeInfoRegistry**: Static registry mapping `Type -> ITypeInfo`, populated by generated module initializers
4. **Thread Safety**: Uses `ConcurrentDictionary` for all registrations
5. **Hierarchical Containers**: Supports parent-child container relationships with proper disposal chains

### Container Features

- **Registration Types**: Instance, Transient, Singleton, Scoped, Factory
- **Injection Methods**: Constructor (primary), Field, Property, Method via `[Inject]`
- **Source Generation Triggers**: `[Inject]` on any member, or `Register<T>()` / `Instantiate<T>()` call sites on `Container`
- **Advanced Features**: Circular dependency detection, labeled dependencies, array resolution, open generic registration

### What stays reflection-based

- `RegisterGeneric(Type, MethodInfo)` - inherently runtime, uses `MakeGenericMethod`
- `CallFunc<T>` / `CallAction<T>` - delegate parameter discovery is runtime
- `Type.IsArray` / `Type.GetElementType()` / `Array.CreateInstance()` in `TryResolve` - cheap metadata queries
- `LabelExtension.CreateLabelType` - runtime generic type construction for labels

## Important Development Notes

1. **Code Quality**: Strict static analysis with warnings as errors
2. **NativeAOT**: The runtime library is marked `IsAotCompatible` - avoid reflection-based invocation in new code
3. **Performance**: Container is designed for high performance - benchmark any changes
4. **Thread Safety**: All public APIs must remain thread-safe
5. **Generator**: Must target netstandard2.0 (Roslyn analyzer requirement)
