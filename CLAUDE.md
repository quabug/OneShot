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
src/OneShot/                   # Runtime library (NuGet: OneShot), netstandard2.1
src/OneShot.Generator/         # Source generator (NuGet: OneShot.Generator), netstandard2.0
tests/OneShot.Tests/           # Unit tests (net10.0, TUnit, PublishAot=true)
tests/OneShot.Generator.Tests/ # Generator snapshot tests (net10.0, TUnit)
benchmarks/OneShot.Benchmarks/ # BenchmarkDotNet (net10.0)
```

`src/OneShot/AotPolyfills.cs` declares `internal` polyfills for trim/AOT attributes (`UnconditionalSuppressMessageAttribute`, `RequiresDynamicCodeAttribute`, `RequiresUnreferencedCodeAttribute`, `DynamicallyAccessedMembersAttribute`, `DynamicallyAccessedMemberTypes`) that don't exist in netstandard2.1. The IL trimmer and ILC recognize them by full name, so internal duplicates work. Guarded by `#if NETSTANDARD || NETFRAMEWORK` for when the library eventually multi-targets.

### Key Design Decisions

1. **Source Generator**: Uses a C# incremental source generator to emit `ITypeInfo` implementations, replacing runtime reflection for AOT compatibility.
2. **ITypeInfo**: Interface for generated type metadata - `Create()` replaces constructor reflection, `InjectAll()` replaces member injection reflection.
3. **TypeInfoRegistry**: Static registry mapping `Type -> ITypeInfo`, populated by generated module initializers.
4. **Thread Safety**: Uses `ConcurrentDictionary` for all registrations.
5. **Hierarchical Containers**: Parent-child container relationships with proper disposal chains.

### Container Features

- **Registration Types**: Instance, Transient, Singleton, Scoped, Factory
- **Injection Methods**: Constructor (primary), Field, Property, Method via `[Inject]`
- **Source Generation Triggers**: `[Inject]` on any member, or `Register<T>()` / `Instantiate<T>()` call sites on `Container`
- **Advanced Features**: Circular dependency detection, labeled dependencies, array resolution, open generic registration

### What stays reflection-based (annotated, not removed)

These APIs are properly annotated with `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]` (or `[UnconditionalSuppressMessage]` at the call site) so the AOT analyzer can warn callers:

- `GenericExtension.RegisterGeneric(Type, MethodInfo)` - `[RequiresDynamicCode]` + `[RequiresUnreferencedCode]`; uses `MakeGenericMethod`
- `LabelExtension.CreateLabelType` - `[RequiresDynamicCode]`; uses `MakeGenericType` for label-type construction
- `Container.TryResolve` / `ResolverBuilder.As` - `[UnconditionalSuppressMessage("AOT", "IL3050")]`; the dynamic-code path only fires when a `label` is passed or an array element type is resolved
- `ResolverBuilder.AsInterfaces` / `WithBuilder.AsInterfaces` - `[UnconditionalSuppressMessage("Trimming", "IL2075")]`; calls `GetInterfaces()` on the registered `ConcreteType`, which is always preserved
- `CallFunc<T>` / `CallAction<T>` - delegate parameter discovery is runtime
- `Type.IsArray` / `Type.GetElementType()` - cheap metadata queries, no analyzer impact

## Important Development Notes

1. **Code Quality**: Strict static analysis. `TreatWarningsAsErrors=true` is set globally in `Directory.Build.props` for `src/` projects; tests and benchmarks inherit it too, with per-folder analyzer overrides in `.editorconfig` that disable test-noise rules (snake_case, sealable fixtures, `ConfigureAwait`, etc.) without weakening source analysis.
2. **Line endings**: `.gitattributes` forces LF on all text. Windows CI was previously failing IDE0055 when `core.autocrlf=true` clashed with `end_of_line = lf` in editorconfig.
3. **NativeAOT**: Tests are AOT-published in CI (`dotnet publish tests/OneShot.Tests -c Release`). Don't add runtime reflection to `src/OneShot/` without annotating it. The runtime library targets netstandard2.1 but uses the polyfilled trim attributes so the AOT analyzer behaves correctly when the library is consumed from net8.0+.
4. **Performance**: Container is designed for high performance — benchmark any changes.
5. **Thread Safety**: All public APIs must remain thread-safe.
6. **Generator**: Must stay on netstandard2.0 (Roslyn analyzer requirement) and Microsoft.CodeAnalysis.CSharp 4.12.
7. **Generator test compilation**: When writing snapshot tests, the in-memory `CSharpCompilation` must reference `netstandard.dll` in addition to `System.Runtime.dll`. The library targets netstandard2.1 and forwards BCL types (Attribute, Type, …) through netstandard; without it the semantic model can't bind `[Inject]`'s base type and the generator silently emits zero trees.
