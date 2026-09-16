using System.Runtime.CompilerServices;

// AStarStrategy.EstimateRemainingMoves is internal so Edit Mode tests can check
// the heuristic's admissibility and consistency directly over the search corpus,
// not only through the answers the search returns.
[assembly: InternalsVisibleTo("GateRush.Tests")]
