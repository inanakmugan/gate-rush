# Gate Rush: interview notes (one page)

> Draft talking points. Say them in your own words. Dxx refers to
> `docs/DECISIONS.md`; Bx refers to `CODE-REVIEW.md`.

**Thirty-second pitch.** "Gate Rush is a sliding-block puzzle I shipped on
itch.io:

- 30 levels, 10 mechanics, 3 jokers, and a full F2P meta game.
- The rules are plain C# that cannot reference Unity.
- One resolver drives play, jokers and the solver.
- Every level is proved solvable in my own editor before it ships."

## Three things I'm proudest of

1. **One rule pipeline for everything (D7, D8, D9).**
   - Every removal emits one `ColorCleared` event, and the resolver loops
     until nothing changes.
   - Ten mechanics combine without referring to each other. A Broom that
     opens a gate, a shutter and an elevator in one pass needs no special
     code.
   - The loop has a proven bound and throws on a cycle.
2. **A solver I trust because I tried to break it (D4, D36, D37).**
   - It gives three answers (solvable, unsolvable, indeterminate within
     budget), and "proven shortest" is reported separately.
   - Nearest-next-clear's completeness argument is cross-checked by A\* on
     every "unsolvable", and backed by a random-board property corpus.
   - That corpus found a real hole. I fixed it by deleting the feature that
     caused it (D45).
   - An independent re-check over 320 random boards found no disagreement.
3. **Architecture enforced by the compiler, and a record of why (D19, D21).**
   - `noEngineReferences` turns layer leaks into compile errors, which is why
     3,400 tests run headless in seconds.
   - The 82 decision records each give a reason and the rejected
     alternatives, so I can defend any choice.

## Three honest weaknesses, and how I'd fix them now

1. **`LevelBootstrap` is a 2,800-line, untested god class.** The rules for
   charging a life, the store, the profile and the camera all live in one
   MonoBehaviour.
   - *Fix:* a plain-C# `LevelFlow` state machine, tested like `Core`, with a
     thin adapter for the Unity callbacks.
2. **Time and failure edges were under-tested.**
   - Restoring a clock that had been set back granted lives, against my own
     spec (B1). My tests encoded the recipe, not the property.
   - An unreadable save was overwritten on the next change (B3).
   - *Done:* both fixed with property and regression tests (D83); the
     unreadable save is now kept aside.
   - Still open: the countdown ignores pause and focus loss (B2). *Fix:* a
     delta clamp from config and a stop on focus loss.
3. **CI came late.** For most of the project the tests ran only when I
   pressed Run All.
   - *Done:* GitHub Actions now builds the engine-free assemblies under
     .NET 8 and runs about 750 tests on every push, about 94% coverage.
     Next time it goes in on day one, as in Loop Blast.

## Gate Rush vs Loop Blast (use it to show growth, not to excuse)

| | Gate Rush (first) | Loop Blast (second) |
|---|---|---|
| Rules | Pure C#, immutable state, fixpoint resolver | Deterministic simulation |
| Solver | Exhaustive A\* + nearest-next-clear, proofs of solvability | Solver with a lookahead policy |
| Failure modes | Fixpoint bound throws on cycles | Stall detection |
| Levels | Hand-authored in a custom editor, validated live | Cloud-drafted |
| Tests | 3,400 Edit Mode tests; CI added after release (94%) | CI from mid-project (95%) |

**The story:** "Gate Rush taught me to separate rules from rendering and to
prove things about my content. Loop Blast kept that, added the parts I
missed (CI and coverage), and moved level drafting into a pipeline instead of
pure hand work."

## Facts to have ready

- Unity 6.3 LTS, URP 2D, uGUI + TMP, Input System, DOTween, NUnit; WebGL
  with Brotli on itch.io (D51).
- Web-only release by decision (D82).
- The store and the leaderboard are simulated and labelled as such (D72,
  D73), behind `IPurchaseService` and `ILeaderboardService`.
- Lessons from the web build:
  - Build early.
  - Pack the ZIP root correctly.
  - Short audio clips need padding for WebGL's AAC encoder.
- On AI: "I designed the architecture and wrote the specs and decisions
  first. The implementation was written with Claude Code against them, under
  my review." Be ready to walk through `MoveResolver.ResolveToFixpoint` and
  `BoardState`'s hashing live.
