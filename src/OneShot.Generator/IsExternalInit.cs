// netstandard2.0 (the Roslyn analyzer TFM) doesn't ship IsExternalInit, which is
// required for `init` accessors and therefore for record/record struct positional
// syntax. The compiler resolves this attribute by full name, so an internal
// definition is sufficient.

#pragma warning disable IDE0161 // Block-scoped namespace needed for the #if guard

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
