using System.Runtime.CompilerServices;

// MoveChanges.Of is internal so Edit Mode tests can build the changes of a
// move no board produces — two clears in one move — and check that a stage
// plays each kind of sound cue once (MoveCues, Module 25).
[assembly: InternalsVisibleTo("GateRush.Tests")]
