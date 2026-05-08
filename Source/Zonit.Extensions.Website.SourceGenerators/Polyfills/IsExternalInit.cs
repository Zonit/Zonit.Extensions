// Polyfill required for `record` types on netstandard2.0 (which doesn't ship IsExternalInit).
// Must be `internal` so it's scoped to this assembly and doesn't collide with BCL on net5+.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
