// Internal polyfill for the JetBrains.Annotations attributes that the library
// uses for IDE hints. Declaring them locally (in the JetBrains.Annotations
// namespace) lets Rider/ReSharper users get the warnings without forcing
// every consumer to take a NuGet dependency on JetBrains.Annotations.
// When a consumer does pull in the package, the `internal` visibility keeps
// the two definitions from conflicting at link time.

#pragma warning disable CA1019 // Define accessors for attribute arguments
#pragma warning disable CA1813 // Avoid unsealed attributes

using System;

namespace JetBrains.Annotations;

/// <summary>
/// Indicates that the return value of the method must be used. JetBrains tools
/// flag callers that discard the result.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class MustUseReturnValueAttribute : Attribute
{
    public MustUseReturnValueAttribute() { }
    public MustUseReturnValueAttribute(string justification) => Justification = justification;
    public string? Justification { get; }
}
