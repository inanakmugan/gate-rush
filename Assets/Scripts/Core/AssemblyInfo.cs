using System.Runtime.CompilerServices;

// BoardState exposes an internal all-fields constructor so its Edit Mode tests
// can build fixtures with a single dynamic field perturbed from a baseline —
// the only way to verify the FNV-1a hash actually depends on every field.
[assembly: InternalsVisibleTo("GateRush.Tests")]
