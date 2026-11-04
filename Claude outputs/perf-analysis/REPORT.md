# Performance and quality analysis — WebGL

Static analysis of the code, assets and settings, made without Unity: no
build was taken and nothing was profiled. Every number below is either read
from a file or marked as an estimate. Each finding says how to confirm it in
the Editor before anyone changes anything.

Scope: `Assets/Scripts/Runtime`, `Assets/Scripts/UI`, every texture, audio,
font and atlas importer under `Assets/`, `ProjectSettings/`, and the URP
assets in `Assets/Settings`. The release is web only (D82), and it runs on both
desktop and phone browsers.

Findings are ranked by likely impact on WebGL frame time first, then memory,
then download and load time.

## Measurement setup (used by every finding)

- **Development Build** for Web, *Autoconnect Profiler* on, played in a
  browser. Deep Profile is too heavy on WebGL; add temporary
  `Profiler.BeginSample("…")`/`EndSample` pairs around the methods named
  below instead, and remove them afterwards.
- **CPU module, Timeline view**: the marker's ms and the `GC.Alloc` samples
  under it (turn on *Show GC Alloc*). Script time sits under
  `PlayerLoop > Update.ScriptRunBehaviourUpdate`.
- **UI**: `Canvas.SendWillRenderCanvases` (layout and graphic rebuild) and
  `Canvas.BuildBatch` / `UGUI.Rendering.UpdateBatches`.
- **Frame Debugger** (Editor, Game view at a phone resolution): draw calls,
  batch-break reasons, render targets.
- **Memory Profiler** package: snapshot of the dev build. Compare object
  counts and the Texture2D, Font and Mesh totals.
- **Build size**: after a release build, the *Build Report* section of
  `Editor.log` (size by category plus the per-asset list), or the Build
  Report window in Unity 6. Record the `.wasm` and `.data` sizes before and
  after each change, both raw and Brotli.
- WebGL reports no GPU timings. For GPU cost, test on one real mid-range
  phone browser and compare frame times in the browser's performance panel.

---

## Frame time

### 1. The whole board is destroyed and rebuilt after every move — high

- **Where:** `Assets/Scripts/Runtime/BoardView.Effects.cs:340` calls
  `Redraw(state)` on every arrival. `Redraw` is at
  `Assets/Scripts/Runtime/BoardView.cs:498-519`.
- **What it does:** `Redraw` destroys every child of the board except the
  floor and frame, then `Draw()` (`BoardView.cs:460-485`) rebuilds all gates,
  generators, blocks, shutters and elevators.
- **How big:** `BoardRecipes.Block` (`Runtime/BoardRecipes.cs:92-145`) makes
  about 14 sprite GameObjects per cell (`AddRenderer`, `BoardView.cs:1292`
  is a `new GameObject(name)`). Each one gets a built string name, such as
  `$"Block {i}"` or `$"Studs {cell}"`. Badges and lock counts add
  `TextMeshPro` components, each generating a mesh. On a packed board of
  about 40 cells that is an estimated 500–600+ GameObjects created and
  destroyed per move.

**Why it matters.** All of it runs on WebGL's single main thread, in the very
frame the player watches the move land. The garbage it leaves builds up
toward a full collection, and on WebGL the collector runs only between frames
with an empty stack. Do not count on `gcIncremental: 1`
(`ProjectSettings.asset:725`) to spread that cost; confirm what 6000.3 does
on Web. The heap starts at 32 MB (finding 9), so the churn also forces heap
growth early on.

**Fix.** Make the redraw incremental:

- Keep each drawn block whose visible state (`VisibilityLayer.Block`) is
  unchanged, and just move its root.
- Rebuild only blocks that were cleared, exposed a new colour, thawed,
  unlocked or spawned, and only gates and shutters that opened.
- Second best: pool block visuals by shape and colour.
- Either way, build the debug names only under `#if UNITY_EDITOR`.

**Measure.**

- A `BoardView.Redraw` sample: ms and `GC.Alloc` bytes on the arrival frame,
  plus the deferred destroy cost at the end of that frame
  (`Destroy`/`DestroyObjectsDelayed` work in the Timeline).
- The Profiler's *Memory* module, GameObject count while you repeat one move.
- Use the same level and the same move sequence before and after.

### 2. Objects are built and destroyed on every grab and at gate mouths — medium-high

Three paths, all in `Assets/Scripts/Runtime/BoardView.Effects.cs`:

- **Lift outline.** `Lift()` (`:484-506`) adds a `SortingGroup` and an
  outline of about 4 renderers per cell on each grab. `RemoveLift()`
  (`:555-571`) destroys them again.
- **Gate clip.** `ClipAtGate()` (`:868-912`) creates a GameObject with a
  `SpriteMask`, allocates two `GetComponentsInChildren` arrays, and toggles
  the block root with `SetActive`, which re-registers every renderer.
  `ClipDraggedAtGate()` (`:1021-1040`) runs every drag frame and adds or
  removes the clip each time the block crosses the gate's inner line, so
  wiggling at a gate repeats the whole cycle.
- **Generator preview.** `ShowGeneratorPreview()` (`:1248-1298`) builds two
  sliced renderers per target cell, with formatted names, on each grab.
  `DiscardGeneratorPreview()` (`:1388-1398`) destroys them.

**Why it matters.** Grab and release are the moments the player is most
sensitive to a hitch, and the gate-mouth case repeats it within a single
drag.

**Fix.**

- Build the outline once per block at draw time and enable or disable it.
- Cache each block's `SpriteRenderer[]` and `TMP_Text[]` in `DrawnBlock`.
- Reuse one `SpriteMask` per view.
- Add a small dead zone before the clip switches off.

**Measure.** `Lift` and `ClipAtGate` samples, ms and `GC.Alloc` on the grab
frame. Then wiggle a block across a gate mouth with the Timeline recording
and count the spikes.

### 3. The marked-joker pulse re-batches the whole level canvas every frame — medium

- **Where:** `Assets/Scripts/UI/JokerBarButton.cs:161-196`.
- **What it does:** a looping yoyo `DOVirtual.Float` sets the glow
  `Image.color` and the button scale on every frame.
- **Where it sits:** the joker bar (`Assets/Prefabs/UI/Joker Bar.prefab`) has
  no Canvas of its own. The only nested canvases in `Level UI.prefab` and
  `Level HUD.prefab` are the coin flight and the gold pill.

**Why it matters.** As long as a joker is marked, the root level canvas
(HUD, timer and joker bar) is rebuilt and re-batched every frame. The
time-bonus tweens in `UI/LevelHud.cs:141-163` do the same for a moment each.

**Fix.** Add a nested `Canvas`, with no `GraphicRaycaster` unless it needs
one, on the joker bar root and on the timer group. This is a prefab change
made through `HandMadePrefabSettings`, per D78.

**Measure.** `Canvas.SendWillRenderCanvases` and `Canvas.BuildBatch` ms, with
a joker marked and with none marked. The Frame Debugger shows which canvas
batch is emitted again.

### 4. HDR is on but nothing post-processes — medium on phones

- **Settings:** `Assets/Settings/UniversalRP.asset:26` has
  `m_SupportsHDR: 1`. The level camera in `Assets/Scenes/Level.unity` has
  `m_HDR: 1` (`:353`) and `m_RenderPostProcessing: 0` (`:288`), and the scene
  has no Volume.
- **Cost:** the camera renders into a float intermediate target and blits it
  to the backbuffer every frame, which costs bandwidth on phone GPUs. Bloom,
  the reason ARCHITECTURE.md keeps HDR on, never runs.

**Fix.** This one is the owner's call, because ARCHITECTURE.md records HDR
and post-processing as deliberately enabled. Either:

- turn HDR off on the URP asset or the camera, until glow is actually used;
  or
- enable post-processing deliberately and accept the cost.

Check first: the board shader can write values above 1 (`_ColorScale`, the
additive highlight), so compare screenshots before and after.

**Measure.**

- Frame Debugger: confirm the intermediate HDR target and the final blit, and
  that both are gone after the change.
- On a phone: frame time over 30 s on the same level.

### 5. The front end and HUD re-show everything once a second while lives regenerate — low-medium

- **Where:** `Runtime/LevelBootstrap.cs:898-924`. `RefreshMeta` compares
  before it shows anything, which is good, but the countdown to the next life
  changes every second.
- **Front end, once a second:**
  - `FrontEndState.Of` (`Runtime/FrontEndState.cs:126-198`) allocates a
    `LevelEntry[60]`, a gold-offer list and the streak arrays.
  - `FrontEnd.Refresh` (`UI/FrontEnd.cs:382-396`) re-shows every page,
    inactive ones too.
  - That includes 60 `string.Format` level numbers (`UI/LevelButton.cs:29`,
    which boxes their ints).
- **In a level:** the same happens through `UI/LevelUi.cs:255-260`, which
  re-shows the HUD, the three joker buttons and the closed gold-packs
  overlay.
- **Knock-on:** the lives label sits under layout groups (`Pills` in
  `Level HUD.prefab:302`, and `Top Bar.prefab`), so a width change triggers a
  layout rebuild.

**Fix.**

- When only the seconds changed, update just the lives label.
- Cache the level-number strings.
- Re-show only the active page and open overlays.
- Give the label a fixed width, or move it out of the layout group.

**Measure.** A `RefreshMeta` sample. `GC.Alloc` on Home recurs once a second;
the Layout part of `Canvas.SendWillRenderCanvases` shows the rebuild.

### 6. DOTween details — low

- **Recycling:** `Assets/Resources/DOTweenSettings.asset` has
  `defaultRecyclable: 0`. That is right, because the code holds tween
  references.
- **Safe mode:** `useSafeMode: 1` adds a check to every tween update. It can
  be turned off for release once no tween targets a destroyed object.
- **Closures:** effects are `DOVirtual.Float` with capturing lambdas, for
  example `BoardView.Effects.cs:446, 524, 627, 748, 1199` and
  `BoardView.cs:298`. These run per event, not per frame, so they are fine.
- **Capacity:** call `DOTween.SetTweensCapacity` at boot, using values from
  `RuntimeConfig`, so DOTween does not resize mid-level.
- **Unused modules:** the Physics and Physics2D DOTween modules are enabled
  but nothing uses them.

**Measure.** DOTween's component Update in the CPU module, with safe mode on
and off.

### 7. Smaller frame-time items — low

- **3D physics:** it still simulates automatically
  (`ProjectSettings/DynamicsManager.asset:21`, `m_AutoSimulation: 1`), though
  the project has no colliders. Set it to Script mode, as Physics2D already
  is (`Physics2DSettings.asset:42`).
- **Volume updates:** `UniversalRP.asset:94` has
  `m_VolumeFrameworkUpdateMode: 0`, which updates volumes every frame even
  though the scene has none. Set it to *Via Scripting*.
- **Frame rate:** no `targetFrameRate` is set, so 120 Hz phones render at
  120 fps. Capping at 60 halves the work and the battery use, but can add
  pacing jitter. Decide by testing on a device.
- **GPU preference:** `ProjectSettings.asset:686` has
  `webGLPowerPreference: 2` (high performance), which wakes a laptop's
  discrete GPU for a 2D puzzle. Use 0 (default) or 1 (low power).

---

## Memory

### 8. The UI atlas is uncompressed RGBA32 on WebGL — high (memory and download)

- **Setting:** `Assets/Art/UI.spriteatlasv2.meta:31-39` holds the WebGL
  override, with `textureFormat: 4` (RGBA32), `maxTextureSize: 1024` and
  `overridden: 1`.
- **Contents:** the atlas packs `Art/Sprites/UI` (34 sprites) and
  `Art/Sprites/Avatars` (30 × 256²).
- **Estimate (not measured):** about 4 M packed pixels, which needs 4–5 pages
  of 1024². That is roughly 16–20 MB of GPU memory, and as much raw in
  `.data` before Brotli.

**Fix.** Set the WebGL format to Automatic (compressed) and the max size to
2048, so there are fewer pages. Check the button gradients and avatars by
eye; if banding shows, keep the UI pages uncompressed and compress only the
avatars as their own atlas.

**Measure.**

- The atlas inspector's page count and preview.
- The Memory Profiler Texture2D list, filtered by "UI".
- The size of the atlas in the Build Report's asset list.

### 9. The generated board sprites are uncompressed and in no atlas — medium

- **What:** 106 textures in `Assets/Art/Generated/` are forced to
  Uncompressed by `Assets/Editor/ArtGenerator.cs:837-839`. They take about
  4 MB as RGBA, and no atlas holds them.
- **Why it matters:** every board sprite is its own texture, so the 2D
  renderer cannot batch across them, and a packed board costs many draw
  calls (finding 1 multiplies the objects).

**Fix.** Add a board atlas over `Art/Generated`. Keep it RGBA32 if the
generator's no-compression intent is about quality, or set it to compressed
if not; max size 2048. SpriteMask stencil use at gates may still split
batches.

**Measure.** Frame Debugger draw-call and batch counts on the busiest level,
before and after. Also check the batch-break reason ("different texture"
should disappear).

### 10. The WebGL texture default is DXT, so phones decompress on the CPU — medium (phones)

- **Setting:** `ProjectSettings/ProjectSettings.asset:420-422` sets the WebGL
  default texture compression to `m_Formats: 05000000` (DXT).
- **On phones:** phone GPUs have no DXT support, so the browser build
  decompresses every DXT texture to RGBA32 at load. The three
  `Assets/Art/Backgrounds/bg_town_*.png` (1024×1536) grow from about
  0.75 MB to about 6 MB each on a phone, and loading takes longer.

**Fix.** Pick one:

- Make ASTC the default, if phones matter more than old desktops.
- Ship both formats, using Unity 6's multiple-format web build option and a
  loader that chooses per device.
- Accept the cost and record it as a decision.

**Measure.** On a phone, a Memory Profiler snapshot (Texture2D totals), and
time to first frame from the browser's performance panel.

### 11. The WebGL heap starts at 32 MB — low-medium

- **Setting:** `ProjectSettings.asset:664,680` has `webGLMemorySize: 32` and
  `webGLInitialMemorySize: 32`.
- **Cost:** the heap grows several times during startup and the first level,
  and each growth copies the heap.

**Fix.** Measure the peak, then set the initial size a little above it
(likely 64–128).

**Measure.** The *Memory* module's total reserved size in the dev build, and
the browser task manager's memory for the tab.

---

## Download and load time

### 12. Managed code stripping is at Unity's default — medium (`.wasm` size)

- **Setting:** `ProjectSettings.asset:718` is `managedStrippingLevel: {}`.
  That is the default (Minimal), and there is no `link.xml`.
- **Fix:**
  - Try Medium, then High, for Web.
  - Smoke-test `JsonUtility` saves and levels, DOTween, and TMP after each
    step, and add a `link.xml` for anything stripped wrongly.
  - Also try *Code Optimization: Disk Size with LTO* in the Web build
    profile.
  - Try `webWasm2023: 1` (`:690`), which brings native wasm exceptions and
    SIMD, after testing iOS Safari 16.4+.
- **Measure:** `.wasm` raw and Brotli size, the Build Report's assembly list,
  and load-to-first-frame time.

### 13. Unused TMP assets ship from Resources — low-medium

- **What:** everything in a `Resources` folder ships, used or not. That
  includes:
  - `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`
    and its dynamic fallback with `LiberationSans.ttf` (350 KB). Nothing
    references them.
  - `EmojiOne`, the default sprite asset in `TMP Settings.asset:39`, whose
    512² texture has mipmaps on.
- **Fix:** move them out of `Resources`, and clear the default sprite asset if
  no text uses emoji. Keep the default font asset field pointing at Fredoka.
- **Measure:** the Build Report asset list should no longer show them.

### 14. Fredoka SDF is a 5 MB asset — low

- **What:** `Assets/ThirdParty/Fonts/Fredoka/Fredoka-Bold SDF.asset` is a
  static 1024² atlas. Most of the file is kerning and mark-to-base tables.
- **Fix:** regenerate it with only the characters the game uses. Drop
  mark-to-base, which Latin text does not need.
- **Nunito:** `Nunito-ExtraBold SDF` is a dynamic fallback with
  `m_IsReadable: 1` and `m_ClearDynamicDataOnBuild: 0`. Setting clear-on-build
  shrinks the shipped asset.
- **Measure:** the font's size in the Build Report, and its Memory Profiler
  entry.

### 15. Quality and correctness notes — low

- **Two lift sounds load late.** `Assets/Audio/Sfx/lift_soft.wav.meta:14` and
  `drop_soft.wav.meta` have `preloadAudioData: 0`, so the first lift sound can
  load on first use and play late. Set preload to 1. `drop_soft` is stereo
  with `forceToMono: 0`; force mono unless the stereo is deliberate. The
  padding lesson in `docs/ROADMAP.md:436-440` still applies; keep these PCM.
  The other audio is fine: 15 small Vorbis clips, preloaded, mono.
- **New sprites default to Lit.** `Renderer2D.asset:65` has
  `m_DefaultMaterialType: 0` (Lit), against the project rule. Code assigns
  Unlit explicitly, so nothing renders black today, but any sprite added in
  the Editor will. Set it to Unlit.
- **The URP asset enables features a 2D renderer never uses:** main light
  shadows, light cookies, lens flares and adaptive performance
  (`UniversalRP.asset:45,75,79,84-85`). Turning them off, together with
  `m_StripUnusedPostProcessingVariants: 1` in the global settings, cuts shader
  variants and build time.
- **Graphics settings:** `GraphicsSettings.asset:40` has
  `m_CustomRenderPipeline: {fileID: 0}`, so URP comes only from the quality
  level. Set it there too.
- **Splash screen:** `m_ShowUnitySplashScreen: 1` (`ProjectSettings.asset:20`)
  adds time before the first frame.
- **Packages:** the manifest includes packages that look unused (Visual
  Scripting, Timeline, SpriteShape, Tilemap, Multiplayer Center). Removing
  them speeds up builds, and can shrink the build if any of their code ships.
  Check the Build Report's assembly list first.

---

## Already good (checked, not problems)

- **Code:**
  - No LINQ, `Camera.main`, `Find*`, `SendMessage` or physics anywhere in
    `Runtime` or `UI`.
  - No runtime `Texture2D`, `Sprite.Create` or `new Material`.
- **Allocation-free paths:**
  - Drag maths (`DragController.Update`) and input (`InputController` caches
    its `PointerEventData` and raycast list, and raycasts only on press).
  - The HUD timer formats only when the whole second changes
    (`UI/LevelHud.cs:101-117`).
- **Pooling:** `DebrisPool` for burst pieces; `CoinFlightView` has its coins
  pre-built and its own canvas; `SoundPlayer` keeps a voice pool.
- **Canvases:** closed overlays and pages are `SetActive(false)`, not left at
  alpha 0.
- **WebGL settings:**
  - Brotli without decompression fallback (D51), data caching on, and
    exceptions set to "explicitly thrown only".
  - Linear colour space, engine code stripping, SRP Batcher, MSAA off, render
    scale 1, and depth and opaque textures off.
- **Importers:** sprite mipmaps are off, and nothing is Read/Write enabled.
- **Engine-free layers:** `Core`, `Meta` and `Solver` reach 95%, 88% and 96%
  line coverage in the new CI run.

---

## The five changes most worth making first

1. **Make `BoardView.Redraw` incremental** (finding 1). This is the largest
   per-move CPU and GC cost, on the frame the player watches.
2. **Compress the UI atlas on WebGL and add a board atlas** (findings 8 and
   9). This removes an estimated 16–20 MB of texture memory, shrinks `.data`,
   and cuts draw calls.
3. **Build the lift outline and gate mask once and toggle them** (finding 2).
   This removes the hitches on grab, release and the gate mouth.
4. **Give the joker bar and timer their own nested Canvas, and narrow the
   once-a-second lives refresh** (findings 3 and 5).
5. **Raise managed stripping and try the disk-size LTO code optimization**
   (finding 12), then decide HDR (finding 4) and the phone texture format
   (finding 10).

## Measurement checklist (record before and after each change)

Use the same build settings, the same level (pick the busiest campaign level
plus one with a generator at a gate), the same browser and the same device.

- [ ] **Build size:** `.wasm`, `.data` and `.framework.js`, both raw and
  Brotli. Also the top 20 assets in the Build Report section of `Editor.log`.
- [ ] **Load:** time from page open to the first interactive frame, in the
  browser performance panel, three runs, median.
- [ ] **Per move:** `BoardView.Redraw` sample ms and `GC.Alloc` bytes,
  averaged over ten moves.
- [ ] **Per grab:** `Lift`, `ClipAtGate` and `ShowGeneratorPreview` ms and
  `GC.Alloc`.
- [ ] **Idle in level, joker marked:** `Canvas.SendWillRenderCanvases` and
  `Canvas.BuildBatch` ms per frame.
- [ ] **Home screen, lives regenerating:** `GC.Alloc` per second.
- [ ] **Frame Debugger, busiest level:** draw calls, batches, and the
  render-target list (is the HDR target present?).
- [ ] **Memory Profiler snapshot after one level:** totals for Texture2D,
  Font and Mesh, the GameObject count, and the managed heap size. Also the
  Memory module's total reserved size.
- [ ] **One mid-range phone browser:** median frame time over 30 s of play,
  and the tab's memory.
- [ ] **Garbage collections:** count over one full level, from `GC.Collect`
  markers in the Timeline.
