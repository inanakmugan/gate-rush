# Unity-free test project

Builds the four assemblies whose asmdefs set `noEngineReferences`
(`GateRush.Core`, `GateRush.Platform`, `GateRush.Meta`, `GateRush.Solver`)
and the Edit Mode test files that compile against them alone, on plain
.NET 8 with NUnit. Every source file is compiled **unchanged** from `Assets/`;
nothing here is a copy and nothing under `Assets/` was edited for it. No
stand-ins were needed.

```
ci/
  Directory.Build.props        C# 9, net8.0, shared settings
  src/GateRush.*/              one project per engine-free asmdef, same names and references
  tests/GateRush.Tests/        54 test files from Assets/Tests, listed explicitly
  coverage.runsettings         coverlet: line coverage of the four assemblies only
  coverage_summary.py          Cobertura -> Markdown table (standard library only)
```

The projects keep the Unity assembly names, so `InternalsVisibleTo("GateRush.Tests")`
in `Core` and `Solver` applies here too. Unity never sees this folder: it lies
outside `Assets/`.

## Run it

```bash
dotnet test ci/tests/GateRush.Tests/GateRush.Tests.csproj
```

With coverage, as the workflow does:

```bash
dotnet test ci/tests/GateRush.Tests/GateRush.Tests.csproj --settings ci/coverage.runsettings --collect "XPlat Code Coverage" --results-directory ci/coverage
```

```bash
python3 ci/coverage_summary.py ci/coverage/*/coverage.cobertura.xml
```

## Adding a test file

A new test file that uses only `NUnit`, `System.*` and the four assemblies
above is added as one more `<Compile Include=...>` line in
`tests/GateRush.Tests/GateRush.Tests.csproj`. The list is explicit on purpose:
a file that starts needing Unity fails loudly in CI instead of vanishing.

## Files left out

135 of the 189 files in `Assets/Tests` cannot build without Unity: each
references `UnityEngine`/`UnityEditor` directly, or tests an assembly that
does (`GateRush.Runtime`, `GateRush.Serialization`, `GateRush.Editor`,
`GateRush.UI`, `GateRush.UI.Editor`). `GateRush.Serialization` alone would
need a stand-in for `JsonUtility` whose behaviour the save and level tests
pin down, so it stays in the Unity Test Runner rather than being tested
against an imitation. They keep running there.

| File | Why it needs Unity |
|---|---|
| `ArtGeneratorTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `ArtPainterTests.Mechanics.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `ArtPainterTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `ArtRecipeTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `AvatarCatalogFillerTests.cs` | tests GateRush.Runtime, GateRush.UI.Editor (references UnityEngine); uses UnityEditor, UnityEngine |
| `AvatarCatalogTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `BlockOutlineTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `BlockTilingTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine) |
| `BoardLayoutTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `BoardRecipesTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `BoardViewTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `BurstLayoutTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `BurstSettingsTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `CampaignBoardFitTests.cs` | tests GateRush.Runtime, GateRush.Serialization (references UnityEngine); uses UnityEditor, UnityEngine |
| `CampaignEndTests.cs` | tests GateRush.Runtime, GateRush.Serialization (references UnityEngine) |
| `CampaignLevelsTests.cs` | tests GateRush.Runtime, GateRush.Serialization (references UnityEngine) |
| `CellNormalizationTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `CoinFlightTests.cs` | tests GateRush.UI (references UnityEngine) |
| `CountdownTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `DebrisPoolTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `DevCampaignMoveTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine) |
| `DevLevelOverrideTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `DraftClickRoutingTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `DraftDragTests.cs` | tests GateRush.Editor, GateRush.Serialization (references UnityEngine) |
| `DraftHistoryTests.cs` | tests GateRush.Editor, GateRush.Serialization (references UnityEngine) |
| `DraftHitTestTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `DraftLockColorsTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `DraftMetricsTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `DraftTilingTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `DraftValidatorTests.cs` | tests GateRush.Editor, GateRush.Serialization (references UnityEngine) |
| `DragControllerTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `DragSettingsTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `EdgeFeaturesTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `EditorBlockLookTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine) |
| `EditorCellSpaceTests.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `EditorGridLayoutTests.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `EditorLayerInsetTests.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `FaceShapeTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `FormatStringsTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `FrameTilingTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `FreePlayJokersTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `FrontEndStateTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `GateExitTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `GateMarkerTests.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `GeneratorMachineTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `GeneratorPreviewTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `GoldCounterTests.cs` | tests GateRush.UI (references UnityEngine) |
| `GoldOffersTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `GridResizeTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `HandMadePrefabSettingsTests.cs` | tests GateRush.UI, GateRush.UI.Editor (references UnityEngine); uses TMPro, UnityEditor, UnityEngine, UnityEngine.UI |
| `HomeTextsTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `InterfaceArtImportRulesTests.cs` | tests GateRush.UI.Editor (references UnityEngine); uses UnityEditor, UnityEngine |
| `JokerButtonsTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `JokerIconsTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `JokerMarksTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `JokerSelectionTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `JsonSaveCodecTests.cs` | tests GateRush.Serialization (references UnityEngine); uses UnityEngine |
| `LayerInsetTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `LayerStudsTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `LeaderboardRankingTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `LeaderboardScreenTests.cs` | tests GateRush.Runtime, GateRush.UI (references UnityEngine); uses TMPro, UnityEditor, UnityEngine, UnityEngine.UI |
| `LeaderboardScrollTests.cs` | tests GateRush.UI (references UnityEngine) |
| `LeaderboardTextsTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEngine |
| `LevelCatalogTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `LevelDraftTests.cs` | tests GateRush.Editor, GateRush.Serialization (references UnityEngine) |
| `LevelEditorSettingsTests.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `LevelHudTextsTests.cs` | tests GateRush.Runtime, GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `LevelIdIndexTests.cs` | tests GateRush.Editor, GateRush.Serialization (references UnityEngine) |
| `LevelMechanicsTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `LevelOverlayStackTests.cs` | tests GateRush.UI (references UnityEngine) |
| `LevelPanelTextsTests.cs` | tests GateRush.Runtime, GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `LevelResultTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `LevelRosterTests.cs` | tests GateRush.Runtime, GateRush.Serialization (references UnityEngine) |
| `LevelRunTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `LevelSerializerTests.cs` | tests GateRush.Serialization (references UnityEngine) |
| `LevelSessionTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `LevelSolveRunnerTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `LevelUiStateTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `LiftOutlineTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `LightingComparisonTests.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `MarkLayoutTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `MechanicsComparisonTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `MetaConfigTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `MetaTextTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `MoveChangesTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `MoveCuesTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `NextClearRunnerTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `OverlayMotionTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `PlayLevelListTests.cs` | tests GateRush.Editor, GateRush.Runtime, GateRush.Serialization (references UnityEngine) |
| `PlayableLevelFileTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `PressFeedbackCoverageTests.cs` | tests GateRush.UI, GateRush.UI.Editor (references UnityEngine); uses UnityEditor, UnityEngine, UnityEngine.UI |
| `PressFeedbackTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine, UnityEngine.EventSystems, UnityEngine.UI |
| `ProfileTextsTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `ProportionalSizeTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `QuarterLayersTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `QuarterTileTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `QueueEntryDrawBoundsTests.cs` | tests GateRush.Editor (references UnityEngine); uses UnityEngine |
| `RegionBoundsTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `ResolutionDiffTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `ResultPanelShapesTests.cs` | tests GateRush.Runtime, GateRush.UI (references UnityEngine) |
| `RuntimeConfigTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `SaveRepositoryTests.cs` | tests GateRush.Serialization (references UnityEngine); uses UnityEngine |
| `ScreenBandsTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `ScreenFadeSequenceTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `ScreenFadeTests.cs` | tests GateRush.Runtime, GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine, UnityEngine.UI |
| `SelectionKeyTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `ShapePresetsTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `ShippedLevelsTests.cs` | tests GateRush.Runtime, GateRush.Serialization (references UnityEngine); uses UnityEngine |
| `SimulatedLeaderboardConfigTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `SimulatedLeaderboardServiceTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `SimulatedPurchaseServiceTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `SimulatedStoreConfigTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `SolverLineKindTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `SoundConfigTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `StoreTextsTests.cs` | tests GateRush.Runtime, GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `StreakPrizesTests.cs` | tests GateRush.UI (references UnityEngine) |
| `StreakProgressTests.cs` | tests GateRush.UI (references UnityEngine) |
| `StreakTextsTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `StudFitTests.cs` | tests GateRush.Editor, GateRush.Runtime (references UnityEngine); uses UnityEditor, UnityEngine |
| `SystemTimeProviderTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `TabPagesTests.cs` | tests GateRush.UI (references UnityEngine) |
| `ThirdPartyCleanupTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `ThirdPartyFontMoveTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `ThirdPartyReferenceTests.cs` | tests GateRush.Editor (references UnityEngine); uses TMPro, UnityEditor, UnityEngine, UnityEngine.UI |
| `TimeBudgetFormulaTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `TimeFormatTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `TimerTicksTests.cs` | tests GateRush.Runtime (references UnityEngine) |
| `TopBarTests.cs` | tests GateRush.Runtime, GateRush.UI (references UnityEngine); uses TMPro, UnityEditor, UnityEngine, UnityEngine.UI |
| `UiBuilderTests.cs` | tests GateRush.Runtime (references UnityEngine); uses UnityEngine |
| `UiConfigTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine |
| `UiNavigationTests.cs` | tests GateRush.UI (references UnityEngine) |
| `UiPrefabBuilderTests.cs` | tests GateRush.Runtime, GateRush.UI, GateRush.UI.Editor (references UnityEngine); uses TMPro, UnityEditor, UnityEditor.SceneManagement, UnityEngine, UnityEngine.SceneManagement, UnityEngine.UI |
| `UiTintTests.cs` | tests GateRush.UI (references UnityEngine); uses UnityEditor, UnityEngine, UnityEngine.UI |
| `ValidationPipelineTests.cs` | tests GateRush.Editor (references UnityEngine) |
| `VisibilityLayerTests.cs` | tests GateRush.Runtime (references UnityEngine) |
