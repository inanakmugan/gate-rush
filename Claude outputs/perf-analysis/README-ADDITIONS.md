# README additions (for the owner to paste)

## Badge

Paste below the title line of `README.md`:

```markdown
[![Tests](https://github.com/inanakmugan/gate-rush/actions/workflows/tests.yml/badge.svg)](https://github.com/inanakmugan/gate-rush/actions/workflows/tests.yml)
```

## Section

Paste after "Running the tests":

```markdown
### Running the tests without Unity

The engine-free assemblies (`Core`, `Platform`, `Meta`, `Solver`) and the
Edit Mode tests that use only them also build on plain .NET 8, straight
from `Assets/`. The `Tests` workflow runs them on every push and pull
request and reports line coverage per assembly in the job summary.

    dotnet test ci/tests/GateRush.Tests/GateRush.Tests.csproj

The tests that need the engine (`Runtime`, `Serialization`, `Editor`, `UI`)
run only in the Unity Test Runner. `ci/README.md` lists them.
```
