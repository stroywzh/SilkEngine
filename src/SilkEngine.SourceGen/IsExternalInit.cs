namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler-required polyfill for <c>init</c> accessors on <c>netstandard2.0</c>; internal so it is
/// never part of the analyzer's public surface.
/// </summary>
internal static class IsExternalInit { }
