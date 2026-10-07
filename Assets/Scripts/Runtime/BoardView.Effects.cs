using System;
using System.Collections.Generic;
using DG.Tweening;
using GateRush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GateRush.Runtime
{
    /// <summary>
    /// The feedback animations of <see cref="BoardView"/> (Module 18): the
    /// grabbed block's lift and outline, and the two stages that present a
    /// move.
    /// </summary>
    /// <remarks>
    /// <para><b>Two stages.</b> <see cref="Present"/> waits for the settle, then
    /// <b>leave</b> plays the clear on the old drawing — a destroyed block is
    /// handed over to debris and passes through its gate, a layered one
    /// peels — and
    /// <b>arrive</b> redraws from the new state and plays every other change
    /// on the new drawing: what went away (a shutter, a padlock and chains) as
    /// a temporary piece under <c>Effects/Stage</c> animated away, what came in
    /// (a spawned block) from its source to its place, and badges that
    /// counted down popping. A stage with nothing to play is skipped at once.
    /// <see cref="IsBusy"/> holds through both.</para>
    /// <para><b>Debris</b> — a destroyed block passing through its gate, the
    /// cubes it streams out, the gate's glow, and ice shards — plays on its
    /// own, under <c>Effects/Debris</c>, with <see cref="debrisId"/> as its
    /// tweens' id. It does not hold <see cref="IsBusy"/>, and the view's own
    /// redraw between the stages, which kills the view's id, leaves it
    /// playing. Only <see cref="Rebuild"/>, <see cref="Clear"/>, disabling and
    /// destroying remove it.</para>
    /// <para><b>Waiting for a pass.</b> A presentation that ends while a block
    /// is still passing through its gate reports done only once the last one
    /// is through, so the win panel waits for it without holding input.</para>
    /// </remarks>
    public sealed partial class BoardView
    {
        // Doors of the board drawing hidden while a wave rises, shown again
        // when the arrive stage ends.
        private readonly List<GameObject> hiddenDoors = new List<GameObject>();

        // A presentation holds its state, changes and callback from Present
        // until it ends; the stages flag says whether leave has started.
        private bool isPresenting;
        private bool hasStagesStarted;
        private BoardState presentedState;
        private BoardState leftState;
        private MoveChanges presentedChanges;
        private int presentedMoveNumber;
        private Action presentationDone;

        // Destroyed blocks still passing through their gates, and the
        // presentations that ended while one was: reported done once the last
        // is through.
        private readonly List<Action> doneAfterPasses = new List<Action>();
        private int passesInFlight;

        // The glow of the gate pulling the dragged block (Module 22), while
        // the drag holds it: its sprite, gate, full-opacity colour and the
        // opacity shown. Null and -1 when no gate pulls.
        private SpriteRenderer pullGlow;
        private int pullGlowGate = -1;
        private Color pullGlowTint;
        private float pullGlowAlpha;

        /// <summary>
        /// Shows the result of a move: waits for the dragged block to settle,
        /// plays the leave stage on the old drawing, redraws from
        /// <paramref name="state"/>, plays the arrive stage, and calls
        /// <paramref name="onDone"/>. <see cref="IsBusy"/> holds throughout. A
        /// <see cref="Rebuild"/> before then abandons it, and
        /// <paramref name="onDone"/> is never called.
        /// </summary>
        /// <param name="state">The state the move produced.</param>
        /// <param name="changes">What the move changed (<see cref="MoveChanges"/>).</param>
        /// <param name="moveNumber">The move's number in this attempt at the level; seeds the bursts.</param>
        /// <param name="onDone">
        /// Called once the board shows <paramref name="state"/> at rest and
        /// every destroyed block has passed wholly through its gate.
        /// </param>
        public void Present(BoardState state, MoveChanges changes, int moveNumber, Action onDone)
        {
            isPresenting = true;
            hasStagesStarted = false;
            presentedState = state ?? throw new ArgumentNullException(nameof(state));
            presentedChanges = changes ?? throw new ArgumentNullException(nameof(changes));
            presentedMoveNumber = moveNumber;
            presentationDone = onDone;
            leftState = drawnState;

            // A block that is destroyed is still lifted when its exit starts.
            // Settle began its drop in this same frame, so undoing it shows
            // nothing.
            var clears = changes.Clears;
            for (var i = 0; i < clears.Count; i++)
            {
                if (clears[i].IsDestroyed
                    && drawnBlocks.TryGetValue(clears[i].BlockIndex, out var block)
                    && block.LiftGroup != null)
                {
                    block.LiftTween?.Kill();
                    block.LiftTween = null;
                    ApplyLift(block, 1f);
                }
            }

            if (settleTween == null)
            {
                PlayLeave();
            }
        }

        private void ResetPresentation()
        {
            isPresenting = false;
            hasStagesStarted = false;
            presentedState = null;
            leftState = null;
            presentedChanges = null;
            presentationDone = null;
            hiddenDoors.Clear();
        }

        /// <summary>
        /// Ends the presentation, freeing input, and reports it done — at once
        /// when no block is still passing through a gate, otherwise when the
        /// last one is through (<see cref="OnPassEnded"/>), so a win's panel
        /// never opens over a block still leaving.
        /// </summary>
        private void EndPresentation()
        {
            var done = presentationDone;
            ResetPresentation();
            if (done == null)
            {
                return;
            }

            if (passesInFlight > 0)
            {
                doneAfterPasses.Add(done);
                return;
            }

            done();
        }

        /// <summary>
        /// A block is wholly through its gate. When it was the last one
        /// passing, every presentation waiting on the passes is reported done,
        /// in the order they ended.
        /// </summary>
        private void OnPassEnded()
        {
            passesInFlight = Math.Max(0, passesInFlight - 1);
            if (passesInFlight == 0)
            {
                ReleaseDoneAfterPasses();
            }
        }

        private void ReleaseDoneAfterPasses()
        {
            if (doneAfterPasses.Count == 0)
            {
                return;
            }

            var waiting = doneAfterPasses.ToArray();
            doneAfterPasses.Clear();
            for (var i = 0; i < waiting.Length; i++)
            {
                waiting[i]();
            }
        }

        /// <summary>
        /// Finishes an interrupted presentation at its end state, skipping what
        /// was left to play, and reports it done.
        /// </summary>
        private void FinishPresentationAtOnce()
        {
            var state = presentedState;
            var done = presentationDone;

            Rebuild(state);
            done?.Invoke();
        }

        /// <summary>
        /// The leave stage, on the old drawing: every destroyed block is handed
        /// over to debris and starts passing through its gate
        /// (<see cref="PassThroughGate"/>), which never holds the stage; every
        /// surviving layered block peels, bumps into its gate
        /// (<see cref="PeelBump"/>) and sets off its gate's glow and cubes
        /// (<see cref="PlayPeelDebris"/>), which do not hold the stage either.
        /// Then the arrive stage, at once when there is no peel or bump to
        /// wait for.
        /// </summary>
        private void PlayLeave()
        {
            hasStagesStarted = true;
            Sequence leave = null;

            var clears = presentedChanges.Clears;
            for (var i = 0; i < clears.Count; i++)
            {
                var clear = clears[i];
                if (!drawnBlocks.TryGetValue(clear.BlockIndex, out var block))
                {
                    continue;
                }

                if (clear.IsDestroyed)
                {
                    PassThroughGate(block, clear);
                    continue;
                }

                // The glow, the cubes and the bump play for every surviving
                // clear, also for a block drawn without an inner shape, whose
                // peel is null.
                PlayPeelDebris(block, clear);

                var peel = PeelEffect(block, clear.ExposedColor.Value);
                if (peel != null)
                {
                    leave = leave ?? DOTween.Sequence().SetId(this);
                    leave.Insert(0f, peel);
                }

                var bump = PeelBump(block, clear.GateEdge);
                if (bump != null)
                {
                    leave = leave ?? DOTween.Sequence().SetId(this);
                    leave.Insert(0f, bump);
                }
            }

            if (leave == null)
            {
                PlayArrive();
                return;
            }

            leave.OnComplete(PlayArrive);
        }

        /// <summary>
        /// The arrive stage: redraws from the new state, sends the ice of thawed
        /// blocks and opened gates flying as debris, and plays the rest on the
        /// new drawing — shutters lifting, locks opening, badges popping, spawned
        /// blocks arriving. Ends the presentation at once when nothing plays.
        /// </summary>
        private void PlayArrive()
        {
            var before = leftState;
            var changes = presentedChanges;
            var state = presentedState;
            Redraw(state);

            for (var i = 0; i < changes.ThawedBlocks.Count; i++)
            {
                ShatterBlockIce(changes.ThawedBlocks[i], state);
            }

            for (var i = 0; i < changes.OpenedGates.Count; i++)
            {
                ShatterGateIce(changes.OpenedGates[i]);
            }

            Sequence arrive = null;
            Sequence Arrive() => arrive = arrive ?? DOTween.Sequence().SetId(this);

            for (var i = 0; i < changes.OpenedShutters.Count; i++)
            {
                Arrive().Insert(0f, LiftShutter(changes.OpenedShutters[i]));
            }

            for (var i = 0; i < changes.UnlockedBlocks.Count; i++)
            {
                var blockIndex = changes.UnlockedBlocks[i];
                Arrive().Insert(0f, OpenLock(blockIndex, state.Origins[blockIndex], before));
            }

            for (var i = 0; i < changes.CountPops.Count; i++)
            {
                if (badges.TryGetValue(changes.CountPops[i], out var badge))
                {
                    Arrive().Insert(0f, PopBadge(badge));
                }
            }

            var waves = new Dictionary<int, List<DrawnBlock>>();
            for (var i = 0; i < changes.Spawns.Count; i++)
            {
                var spawn = changes.Spawns[i];
                if (!drawnBlocks.TryGetValue(spawn.BlockIndex, out var block))
                {
                    continue;
                }

                if (spawn.Source == SpawnerKind.Generator)
                {
                    Arrive().Insert(0f, ArriveFromGenerator(block, spawn.SpawnerIndex));
                    continue;
                }

                if (!waves.TryGetValue(spawn.SpawnerIndex, out var wave))
                {
                    wave = new List<DrawnBlock>();
                    waves.Add(spawn.SpawnerIndex, wave);
                }

                wave.Add(block);
            }

            foreach (var wave in waves)
            {
                RiseFromElevator(Arrive(), wave.Key, wave.Value);
            }

            if (arrive == null)
            {
                EndPresentation();
                return;
            }

            arrive.OnComplete(FinishArrive);
        }

        /// <summary>The arrive stage is over: its temporary pieces go, hidden doors come back.</summary>
        private void FinishArrive()
        {
            DestroyChildren(stage);
            for (var i = 0; i < hiddenDoors.Count; i++)
            {
                if (hiddenDoors[i] != null)
                {
                    hiddenDoors[i].SetActive(true);
                }
            }

            EndPresentation();
        }

        /// <summary>
        /// Lifts a grabbed block: its body grows about the footprint's centre, a
        /// white outline grows out from under it, and a sorting group draws the
        /// whole block above the board.
        /// </summary>
        private void Lift(DrawnBlock block)
        {
            block.LiftTween?.Kill();

            if (block.LiftGroup == null)
            {
                block.LiftGroup = block.Root.gameObject.AddComponent<SortingGroup>();
                block.LiftGroup.sortingOrder = config.LiftedBlockOrder;
            }

            if (block.Outline == null)
            {
                block.Outline = AddGroup(block.Body, "Outline", Vector2.zero);
                block.OutlineQuarters = AddQuarters(
                    block.Outline, block.Tiles, blockQuarterRect, config.OutlineColor, config.OutlineOrder);
                ApplyLift(block, block.LiftAmount);
            }

            block.LiftTween = TweenLift(block, 1f, null);
        }

        /// <summary>
        /// Drops a lifted block back to rest: the body shrinks back, the outline
        /// retracts to nothing under the face, and then the outline and the
        /// sorting group go. Does nothing for a block at rest.
        /// </summary>
        private void Drop(DrawnBlock block)
        {
            if (block.LiftGroup == null && block.Outline == null)
            {
                return;
            }

            block.LiftTween?.Kill();
            block.LiftTween = TweenLift(block, 0f, () => RemoveLift(block));
        }

        private Tween TweenLift(DrawnBlock block, float to, Action onDone) =>
            DOVirtual.Float(block.LiftAmount, to, config.LiftSeconds, amount => ApplyLift(block, amount))
                .SetEase(config.LiftEase)
                .SetId(this)
                .OnComplete(() =>
                {
                    block.LiftTween = null;
                    onDone?.Invoke();
                });

        /// <summary>
        /// Shows <paramref name="block"/> lifted by <paramref name="amount"/>,
        /// from 0 (at rest) to 1 (held): its body's scale, and its outline's
        /// reach past the block's drawing (<see cref="LiftOutline"/>).
        /// </summary>
        private void ApplyLift(DrawnBlock block, float amount)
        {
            block.LiftAmount = amount;
            SetBodyScale(block, Mathf.LerpUnclamped(1f, config.LiftScale, amount));

            if (block.OutlineQuarters == null)
            {
                return;
            }

            var width = Mathf.Max(0f, config.OutlineWidthCells * amount);
            for (var i = 0; i < block.Tiles.Count; i++)
            {
                var tile = block.Tiles[i];
                PoseQuarter(block.OutlineQuarters[i], tile, CellRectToLocal(LiftOutline.QuarterRect(tile, width)));
            }
        }

        private void RemoveLift(DrawnBlock block)
        {
            if (block.Outline != null)
            {
                Destroy(block.Outline.gameObject);
            }

            if (block.LiftGroup != null)
            {
                Destroy(block.LiftGroup);
            }

            block.Outline = null;
            block.OutlineQuarters = null;
            block.LiftGroup = null;
            block.LiftAmount = 0f;
            SetBodyScale(block, 1f);
        }

        /// <summary>
        /// Scales a block's body about its footprint's centre: shifting the
        /// body by <c>centre · (1 − scale)</c> keeps that centre where it is.
        /// </summary>
        private static void SetBodyScale(DrawnBlock block, float scale)
        {
            block.Body.localScale = new Vector3(scale, scale, 1f);
            block.Body.localPosition = block.FootprintCenter * (1f - scale);
        }

        /// <summary>
        /// A destroyed block passes through its gate as debris: it leaves the
        /// drawing for <c>Effects/Debris</c>, is clipped at the gate's inner
        /// line (<see cref="ClipAtGate"/>), and slides out at a steady speed —
        /// its depth, which takes it wholly through
        /// (<see cref="GateExit.DepthCells"/>), at
        /// <see cref="RuntimeConfig.ExitSecondsPerCell"/> — while
        /// its lift drops, cubes stream out beneath the gate
        /// (<see cref="BurstLayout.Stream"/>) and the gate glows inward. Every
        /// tween carries <see cref="debrisId"/>, so the stages and the redraw
        /// never touch it and it never holds <see cref="IsBusy"/>; the pass is
        /// counted, so a win's panel waits for it (<see cref="EndPresentation"/>).
        /// </summary>
        private void PassThroughGate(DrawnBlock block, ClearedBlock clear)
        {
            var edge = clear.GateEdge;
            var root = block.Root;

            block.LiftTween?.Kill();
            block.LiftTween = null;
            drawnBlocks.Remove(clear.BlockIndex);
            root.SetParent(debris, true);
            var mask = ClipAtGate(block, edge, out var maskCenter);

            var depth = GateExit.DepthCells(block.Cells, edge);
            var passSeconds = depth * config.ExitSecondsPerCell;
            var travel = GateExit.Outward(edge) * CellsToWorld(depth);
            Vector2 start = root.localPosition;

            Tween drop = null;
            if (block.LiftAmount > 0f)
            {
                drop = DOVirtual.Float(block.LiftAmount, 0f, config.LiftSeconds, amount => ApplyLift(block, amount))
                    .SetEase(config.LiftEase)
                    .SetId(debrisId);
            }

            passesInFlight++;
            DOVirtual.Float(0f, 1f, passSeconds, t =>
                {
                    root.localPosition = start + travel * t;
                    mask.localPosition = maskCenter - (Vector2)root.localPosition;
                })
                .SetEase(config.ExitEase)
                .SetId(debrisId)
                .OnComplete(() =>
                {
                    drop?.Kill();
                    if (root != null)
                    {
                        Destroy(root.gameObject);
                    }

                    OnPassEnded();
                });

            StreamCubes(block, clear, BurstKind.Exit, passSeconds, 1f);
            PlayGateGlow(clear.GateIndex, passSeconds);
        }

        /// <summary>
        /// Cubes in the colour <paramref name="clear"/> removed stream out
        /// beneath its gate over <paramref name="seconds"/>, as debris:
        /// <paramref name="countFraction"/> of what a destroyed block of
        /// <paramref name="block"/>'s footprint streams
        /// (<see cref="BurstLayout.StreamCount"/>), seeded from
        /// <paramref name="kind"/>, the block and the move, so an exit's and a
        /// peel's never share pieces. The one stream for both; only the
        /// arguments differ.
        /// </summary>
        private void StreamCubes(DrawnBlock block, ClearedBlock clear, BurstKind kind, float seconds, float countFraction)
        {
            var gate = ctx.Gates[clear.GateIndex];
            var cubes = BurstLayout.Stream(
                ctx.Width, ctx.Height, clear.GateEdge, gate.Offset, gate.Width, layout.FrameThicknessCells,
                block.Cells.Count, seconds,
                BurstLayout.Seed(kind, clear.BlockIndex, presentedMoveNumber), cubeBurst, countFraction);
            if (cubes.Count == 0)
            {
                return;
            }

            PlayBurst(
                cubes, GridToLocal, layout.CellSize, config.CubeSprite, config.BlockFill(clear.RemovedColor),
                config.CubeSeconds, config.CubeEase, config.CubeEndScale, 0f);
        }

        /// <summary>
        /// What a peel shows at its gate beside the peel itself (Module 20):
        /// the gate glows for the peel's duration and cubes of the removed
        /// colour stream out beneath it, a
        /// <see cref="RuntimeConfig.PeelCubeFraction"/> of an exit's. Both are
        /// debris, like an exit's: the redraw between the stages never cuts
        /// them short and they never hold <see cref="IsBusy"/>. The block
        /// stays on the board, so this is not counted as a pass.
        /// </summary>
        private void PlayPeelDebris(DrawnBlock block, ClearedBlock clear)
        {
            StreamCubes(block, clear, BurstKind.Peel, config.PeelSeconds, config.PeelCubeFraction);
            PlayGateGlow(clear.GateIndex, config.PeelSeconds);
        }

        /// <summary>
        /// A peeling block's push into its gate: its root moves toward the
        /// gate by <see cref="RuntimeConfig.PeelBumpCells"/> over the first
        /// half of the peel and back over the second, each half shaped by
        /// <see cref="RuntimeConfig.PeelBumpEase"/>, and ends exactly where it
        /// started. It draws over the gate's mouth, unclipped, by that much.
        /// </summary>
        /// <remarks>
        /// <para><b>Order.</b> The leave stage starts only once the settle has
        /// finished or been stopped (<see cref="Present"/>,
        /// <see cref="OnSettled"/>), so the root is at rest in its cell and
        /// nothing else moves it while this plays. The lift's drop may still
        /// be running: it scales the block's body about the footprint's
        /// centre and never touches the root, so the two do not overwrite
        /// each other. The bump lasts as long as the peel and runs in the
        /// leave stage's sequence, so the root is back before the arrive
        /// stage redraws.</para>
        /// </remarks>
        /// <returns>The bump, to run in the leave stage's sequence; or null when the bump is 0, which disables it.</returns>
        private Tween PeelBump(DrawnBlock block, BoardEdge gateEdge)
        {
            if (!(config.PeelBumpCells > 0f))
            {
                return null;
            }

            var root = block.Root;
            Vector2 rest = root.localPosition;
            var reach = GateExit.Outward(gateEdge) * CellsToWorld(config.PeelBumpCells);
            var ease = config.PeelBumpEase;

            // Linear time: the there-and-back is the bump's whole shape. Its
            // height is 0 at both ends of the peel and 1 at the middle.
            return DOVirtual.Float(0f, 1f, config.PeelSeconds, t =>
                {
                    var height = 1f - Mathf.Abs(2f * t - 1f);
                    root.localPosition = rest + reach * DOVirtual.EasedValue(0f, 1f, height, ease);
                })
                .SetEase(Ease.Linear)
                .OnComplete(() => root.localPosition = rest);
        }

        /// <summary>
        /// Clips a passing block at its gate's inner line with a sprite mask in
        /// the block's own sorting group — the lift's, or a new one at the same
        /// order — so it masks this block and nothing else on the board. Only
        /// this block's sprites are set to show inside it. The mask covers
        /// <see cref="GateExit.MaskRect"/>: exactly the inner line on the exit
        /// side, and past the grid by the block's overhang
        /// (<see cref="GateExit.OverhangCells"/>) on the other three, so a
        /// lifted block on a border row keeps its outline there. It is the root's
        /// child, so the pass keeps it still on the board by moving it against
        /// the root; <paramref name="maskCenter"/> is where it stays, in the
        /// view's frame. Labels ignore sprite masks, and a block a move can
        /// destroy carries none; any there are hidden.
        /// </summary>
        private Transform ClipAtGate(DrawnBlock block, BoardEdge edge, out Vector2 maskCenter)
        {
            // One clip per block: an exit that follows a nudged drag replaces
            // the drag's clip rather than adding a second mask. No refresh
            // for the removal: this call ends with one.
            RemoveGateClip(block, refreshMasking: false);

            var root = block.Root;
            if (block.LiftGroup == null)
            {
                block.LiftGroup = root.gameObject.AddComponent<SortingGroup>();
                block.LiftGroup.sortingOrder = config.LiftedBlockOrder;
            }

            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.gameObject.activeSelf)
                {
                    label.gameObject.SetActive(false);
                    block.GateClipHiddenLabels.Add(label.gameObject);
                }
            }

            foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }

            var margin = GateExit.OverhangCells(block.Cells, config.LiftScale, config.OutlineWidthCells);
            var area = GridRectToLocal(GateExit.MaskRect(ctx.Width, ctx.Height, edge, margin));
            maskCenter = area.center;

            var go = new GameObject("Exit clip");
            go.transform.SetParent(root, false);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = config.CellSprite;
            var bounds = config.CellSprite.bounds.size;
            go.transform.localScale = new Vector3(area.width / bounds.x, area.height / bounds.y, 1f);
            go.transform.localPosition = maskCenter - (Vector2)root.localPosition;
            block.GateClip = go.transform;
            block.GateClipEdge = edge;
            block.GateClipCenter = maskCenter;
            RefreshMasking(root);
            return go.transform;
        }

        /// <summary>
        /// Makes Unity take up a change of masking under
        /// <paramref name="root"/> by turning the root off and on again within
        /// this call.
        /// </summary>
        /// <remarks>
        /// <para>Observed in Play Mode: when a <see cref="SpriteMask"/> is
        /// added and the renderers' <c>maskInteraction</c> is changed at
        /// runtime inside a <see cref="SortingGroup"/> that is already active,
        /// the block keeps drawing as it was registered before — a clipped
        /// block showed as a pale white silhouette for its whole pass — until
        /// its root is re-enabled, which registers its renderers and mask
        /// afresh. Nothing is drawn between the two calls, so the toggle is
        /// never seen.</para>
        /// <para>Nothing under a block's root is a component of this project
        /// and no tween is linked to its objects, so the toggle runs no logic
        /// and stops nothing. Labels the clip hid, and a mask on its way out,
        /// were turned off on their own objects, so they stay off.</para>
        /// <para>Skipped for a root that is not active in the hierarchy — it
        /// registers when it is activated — and when this view is not active
        /// and enabled: a clip can be removed while the view is being torn
        /// down, and nothing is activated then.</para>
        /// </remarks>
        private void RefreshMasking(Transform root)
        {
            if (!isActiveAndEnabled || !root.gameObject.activeInHierarchy)
            {
                return;
            }

            root.gameObject.SetActive(false);
            root.gameObject.SetActive(true);
        }

        /// <summary>
        /// Undoes <see cref="ClipAtGate"/>: the mask goes, the block's sprites
        /// stop answering to masks, the block's masking is refreshed
        /// (<see cref="RefreshMasking"/>) and the labels the clip hid show
        /// again. The sorting group stays: while a block is dragged it is the
        /// lift's, which removes it. Does nothing at all — no refresh either —
        /// for a block that is not clipped: the drag asks this every frame.
        /// </summary>
        private void RemoveGateClip(DrawnBlock block) => RemoveGateClip(block, refreshMasking: true);

        /// <summary>
        /// The one removal of a gate clip. <paramref name="refreshMasking"/> is
        /// false only for <see cref="ClipAtGate"/>, which replaces the clip and
        /// refreshes once itself; everything else goes through
        /// <see cref="RemoveGateClip(DrawnBlock)"/>.
        /// </summary>
        private void RemoveGateClip(DrawnBlock block, bool refreshMasking)
        {
            if (block.GateClip == null)
            {
                return;
            }

            // Destroy waits for the end of the frame; off now, so it cannot
            // mask alongside a clip added in the same frame.
            block.GateClip.gameObject.SetActive(false);
            Destroy(block.GateClip.gameObject);
            block.GateClip = null;

            foreach (var sprite in block.Root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.maskInteraction = SpriteMaskInteraction.None;
            }

            // Before the labels come back, so they are not toggled with it.
            if (refreshMasking)
            {
                RefreshMasking(block.Root);
            }

            foreach (var label in block.GateClipHiddenLabels)
            {
                if (label != null)
                {
                    label.SetActive(true);
                }
            }

            block.GateClipHiddenLabels.Clear();
        }

        /// <summary>
        /// Keeps a clipped block's mask still on the board while the block's
        /// root moves: the mask is the root's child, so it is moved against
        /// it. Does nothing for a block that is not clipped.
        /// </summary>
        private static void HoldGateClip(DrawnBlock block)
        {
            if (block.GateClip != null)
            {
                block.GateClip.localPosition = block.GateClipCenter - (Vector2)block.Root.localPosition;
            }
        }

        /// <summary>
        /// The clip of a dragged block (Module 22): while a gate pulls it and
        /// its own footprint, drawn at <paramref name="drawn"/>, crosses that
        /// gate's inner line (<see cref="GateExit.FootprintCrossesInnerLine"/>)
        /// — the nudge into the gate's mouth — it is clipped there exactly as
        /// a passing block is (<see cref="ClipAtGate"/>), so it goes into the
        /// mouth rather than over the gate and the frame. The clip goes the
        /// moment that stops being true. Called after the root has been moved.
        /// </summary>
        private void ClipDraggedAtGate(DrawnBlock block, Vector2 drawn, PullTarget? pull)
        {
            if (pull.HasValue)
            {
                var edge = ctx.Gates[pull.Value.GateIndex].Edge;
                if (GateExit.FootprintCrossesInnerLine(ctx.Width, ctx.Height, edge, block.Cells, drawn))
                {
                    if (block.GateClip == null || block.GateClipEdge != edge)
                    {
                        ClipAtGate(block, edge, out _);
                    }

                    HoldGateClip(block);
                    return;
                }
            }

            RemoveGateClip(block);
        }

        /// <summary>
        /// The glow inside gate <paramref name="gateIndex"/> while a block
        /// passes through it: the gradient sprite over
        /// <see cref="GateExit.GlowRect"/>, turned so its strong edge lies on the
        /// gate, tinted from the gate's colour toward white. It fades in, holds
        /// until the block is through, and fades out, as debris.
        /// </summary>
        private void PlayGateGlow(int gateIndex, float passSeconds)
        {
            // Above everything: this glow lights the passing block on purpose.
            var glow = AddGateGlow(gateIndex, config.EffectOrder, out var color);
            color.a = config.GateGlowAlpha;
            var fadeIn = config.GateGlowFadeInSeconds;
            var fadeOut = config.GateGlowFadeOutSeconds;
            var holdEnd = Mathf.Max(passSeconds, fadeIn);
            var total = holdEnd + fadeOut;
            var ease = config.GateGlowEase;

            void Show(float time)
            {
                var strength = time < fadeIn
                    ? DOVirtual.EasedValue(0f, 1f, time / fadeIn, ease)
                    : time < holdEnd
                        ? 1f
                        : 1f - DOVirtual.EasedValue(0f, 1f, (time - holdEnd) / fadeOut, ease);
                var shown = color;
                shown.a *= strength;
                glow.color = shown;
            }

            Show(0f);
            DOVirtual.Float(0f, total, total, Show)
                .SetEase(Ease.Linear)
                .SetId(debrisId)
                .OnComplete(() =>
                {
                    if (glow != null)
                    {
                        Destroy(glow.gameObject);
                    }
                });
        }

        /// <summary>
        /// The glow sprite of gate <paramref name="gateIndex"/>, under
        /// <c>Effects/Debris</c> and fully clear: the gradient over
        /// <see cref="GateExit.GlowRect"/>, turned so its strong edge lies on
        /// the gate. <paramref name="tint"/> is its colour at full opacity,
        /// from the gate's colour toward white. The one placement the exit's
        /// glow and the pull's glow share; they differ in
        /// <paramref name="order"/>, the exit's above the passing block and
        /// the pull's below every block.
        /// </summary>
        private SpriteRenderer AddGateGlow(int gateIndex, int order, out Color tint)
        {
            var gate = ctx.Gates[gateIndex];
            var area = GridRectToLocal(GateExit.GlowRect(
                ctx.Width, ctx.Height, gate.Edge, gate.Offset, gate.Width, config.GateGlowDepthCells));

            // Drawn along the gate before it is turned: the sprite's strong
            // bottom edge faces the way out once turned from "down" to outward.
            var size = new Vector2(CellsToWorld(gate.Width), CellsToWorld(config.GateGlowDepthCells));
            var turn = Vector2.SignedAngle(Vector2.down, GateExit.Outward(gate.Edge));
            tint = Color.Lerp(config.BlockFill(gate.Color), Color.white, config.GateGlowWhiten);
            tint.a = 1f;

            var clear = tint;
            clear.a = 0f;
            return AddSprite(debris, "Gate glow", config.GateGlowSprite, area.center, size, clear, order, turn);
        }

        /// <summary>
        /// Shows the pull of the drag in progress (Module 22): the glow of the
        /// gate <paramref name="pull"/> names, at its strength times the
        /// configured opacity, or none for null. Fed every frame of a drag by
        /// <see cref="ShowDragged"/>.
        /// </summary>
        /// <remarks>
        /// The glow is not debris while the drag holds it: no tween moves it,
        /// its opacity follows the pull, closing on it at a full fade per
        /// <c>Pull Glow Fade Seconds</c> on unscaled time, so a target that
        /// appears at close range does not pop. It sorts at
        /// <see cref="RuntimeConfig.PullGlowOrder"/>, below every block, so it
        /// lights the gate's mouth and not the block in it. It sits under
        /// <c>Effects/Debris</c> because that group outlives the board's
        /// redraws. When the target goes or changes gate, and on
        /// <see cref="Settle"/>, <see cref="Snap"/> and
        /// <see cref="BeginDrag"/>, it is handed to debris to fade out
        /// (<see cref="ReleasePullGlow"/>).
        /// </remarks>
        private void ShowPullGlow(PullTarget? pull)
        {
            if (!pull.HasValue || debris == null)
            {
                ReleasePullGlow();
                return;
            }

            var target = pull.Value;
            if (pullGlow == null || pullGlowGate != target.GateIndex)
            {
                ReleasePullGlow();
                pullGlow = AddGateGlow(target.GateIndex, config.PullGlowOrder, out pullGlowTint);
                pullGlowGate = target.GateIndex;
                pullGlowAlpha = 0f;
            }

            var goal = target.Strength * config.PullGlowAlpha;
            var fullFadePerSecond = config.PullGlowAlpha / config.PullGlowFadeSeconds;
            pullGlowAlpha = Mathf.MoveTowards(pullGlowAlpha, goal, fullFadePerSecond * Time.unscaledDeltaTime);

            var shown = pullGlowTint;
            shown.a = pullGlowAlpha;
            pullGlow.color = shown;
        }

        /// <summary>
        /// Lets go of the pull's glow: from here it is debris, fading from the
        /// opacity it has to none over the fade time and then destroyed, with
        /// <see cref="debrisId"/> as its tween's id — so a restart, a level
        /// change, disabling and destroying remove it like any debris. Does
        /// nothing when there is no glow.
        /// </summary>
        private void ReleasePullGlow()
        {
            var glow = pullGlow;
            var tint = pullGlowTint;
            var from = pullGlowAlpha;
            ForgetPullGlow();

            if (glow == null)
            {
                return;
            }

            if (!(from > 0f))
            {
                Destroy(glow.gameObject);
                return;
            }

            DOVirtual.Float(from, 0f, config.PullGlowFadeSeconds, alpha =>
                {
                    if (glow != null)
                    {
                        var shown = tint;
                        shown.a = alpha;
                        glow.color = shown;
                    }
                })
                .SetEase(Ease.Linear)
                .SetId(debrisId)
                .OnComplete(() =>
                {
                    if (glow != null)
                    {
                        Destroy(glow.gameObject);
                    }
                });
        }

        /// <summary>
        /// Forgets the pull's glow without touching the sprite: for when the
        /// debris group it sits in is being destroyed anyway.
        /// </summary>
        private void ForgetPullGlow()
        {
            pullGlow = null;
            pullGlowGate = -1;
            pullGlowAlpha = 0f;
        }

        /// <summary>Ice shards fly out from the centre of a block that thawed, over its unfrozen face.</summary>
        private void ShatterBlockIce(int blockIndex, BoardState state)
        {
            var cells = ctx.SpecAt(blockIndex).Cells;
            var origin = state.Origins[blockIndex];
            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var centre = new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
            var pieces = BurstLayout.Pieces(
                CellAreas(cells), null, centre,
                BurstLayout.Seed(BurstKind.Thaw, blockIndex, presentedMoveNumber), shardBurst);

            PlayShards(pieces, cellsInBlock => GridToLocal(new Vector2(origin.X, origin.Y) + cellsInBlock));
        }

        /// <summary>Ice shards burst from the ice of a gate that opened, over the coloured gate now drawn.</summary>
        private void ShatterGateIce(int gateIndex)
        {
            var gate = ctx.Gates[gateIndex];
            var areas = new Rect[gate.Width];
            for (var i = 0; i < areas.Length; i++)
            {
                areas[i] = GateCellGridRect(gateIndex, i);
            }

            var span = FrameTiling.EdgeSpanRect(
                ctx.Width, ctx.Height, gate.Edge, gate.Offset, gate.Width, layout.FrameThicknessCells);
            var pieces = BurstLayout.Pieces(
                areas, null, span.center,
                BurstLayout.Seed(BurstKind.GateOpening, gateIndex, presentedMoveNumber), shardBurst);

            PlayShards(pieces, GridToLocal);
        }

        private void PlayShards(IReadOnlyList<BurstPiece> pieces, Func<Vector2, Vector2> toLocal)
        {
            PlayBurst(
                pieces, toLocal, layout.CellSize, config.ShardSprite, config.IceColor,
                config.ShardSeconds, config.ShardEase, config.ShardEndScale, config.ShardFallCells);
        }

        /// <summary>
        /// Plays <paramref name="pieces"/> as debris: one sprite each, flying
        /// its travel, turning, shrinking to <paramref name="endScale"/>,
        /// falling <paramref name="fallCells"/> and fading, each after its
        /// delay. One tween drives them all, with <see cref="debrisId"/> as its
        /// id, and destroys them when it ends. The one path for cubes and
        /// shards.
        /// </summary>
        /// <param name="toLocal">Where a piece's start, in the burst's cell units, is in this view's frame.</param>
        /// <param name="worldPerCell">World units per cell of travel and size.</param>
        private void PlayBurst(
            IReadOnlyList<BurstPiece> pieces, Func<Vector2, Vector2> toLocal, float worldPerCell, Sprite sprite,
            Color color, float seconds, Ease ease, float endScale, float fallCells)
        {
            var group = AddGroup(debris, "Burst", Vector2.zero);
            var renderers = new SpriteRenderer[pieces.Count];
            var starts = new Vector2[pieces.Count];
            var baseScales = new Vector3[pieces.Count];
            var longestDelay = 0f;

            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                starts[i] = toLocal(piece.Start);
                var side = piece.Size * worldPerCell;
                renderers[i] = AddSprite(group, "Piece", sprite, starts[i], new Vector2(side, side), color, config.EffectOrder);
                baseScales[i] = renderers[i].transform.localScale;
                longestDelay = Mathf.Max(longestDelay, piece.DelaySeconds);
            }

            var fall = fallCells * worldPerCell;
            var total = longestDelay + seconds;
            DOVirtual.Float(0f, total, total, time =>
                {
                    for (var i = 0; i < pieces.Count; i++)
                    {
                        var piece = pieces[i];
                        var u = Mathf.Clamp01((time - piece.DelaySeconds) / seconds);
                        var eased = DOVirtual.EasedValue(0f, 1f, u, ease);
                        var transformOf = renderers[i].transform;
                        transformOf.localPosition =
                            starts[i] + piece.Travel * (worldPerCell * eased) + Vector2.down * (fall * u * u);
                        transformOf.localRotation = Quaternion.Euler(0f, 0f, piece.SpinDegrees * eased);
                        transformOf.localScale = baseScales[i] * Mathf.LerpUnclamped(1f, endScale, eased);

                        var faded = color;
                        faded.a *= 1f - u;
                        renderers[i].color = faded;
                    }
                })
                .SetEase(Ease.Linear)
                .SetId(debrisId)
                .OnComplete(() =>
                {
                    if (group != null)
                    {
                        Destroy(group.gameObject);
                    }
                });
        }

        /// <summary>One unit rectangle per cell, in the cells' own frame: where a block's pieces start.</summary>
        private static Rect[] CellAreas(IReadOnlyList<Coord> cells)
        {
            var areas = new Rect[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                areas[i] = new Rect(cells[i].X, cells[i].Y, 1f, 1f);
            }

            return areas;
        }

        /// <summary>
        /// A surviving layered block peels its removed outer colour (Module
        /// 20), on the renderers it was drawn with: the inner shape grows from
        /// its inset to the whole face (<see cref="FaceShape"/>), its studs
        /// with it, while the
        /// outer face shrinks inward by that inset and fades, the inner
        /// shape's edge fades, and the lip turns from the outer colour's to
        /// <paramref name="exposed"/>'s. It ends as a plain block of the
        /// exposed colour, which is what the redraw then shows — with a third
        /// colour's inner shape inside it when the stack goes deeper.
        /// </summary>
        /// <returns>
        /// The peel, to run in the leave stage's sequence, which carries this
        /// view as its id; or null for a block drawn without an inner shape —
        /// one that was cleared while frozen (M3 hides its colours) — which has
        /// nothing to peel and is left to the redraw.
        /// </returns>
        private Tween PeelEffect(DrawnBlock block, BlockColor exposed)
        {
            if (block.LayerQuarters == null)
            {
                return null;
            }

            var inset = config.LayerInsetCells;
            var edgeInset = inset - config.LayerEdgeCells;

            // A footprint has at least one cell, so each list holds at least
            // four quarters, all drawn in one colour.
            var faceColor = block.FaceQuarters[0].color;
            var edgeColor = block.LayerEdgeQuarters[0].color;
            var lipFrom = block.LipQuarters[0].color;
            var lipTo = config.LipFill(config.BlockFill(exposed));

            return DOVirtual.Float(0f, 1f, config.PeelSeconds, t =>
                {
                    // An easing may overshoot; a size may follow it, an
                    // opacity may not.
                    var opacity = 1f - Mathf.Clamp01(t);

                    PoseInset(block.LayerQuarters, block.Tiles, inset * (1f - t));
                    PoseInset(block.LayerEdgeQuarters, block.Tiles, edgeInset * (1f - t));
                    PoseInset(block.FaceQuarters, block.Tiles, inset * t);
                    block.SetStudScale(Mathf.LerpUnclamped(config.LayerStudScale, 1f, t));

                    SetColor(block.FaceQuarters, WithOpacity(faceColor, opacity));
                    SetColor(block.LayerEdgeQuarters, WithOpacity(edgeColor, opacity));
                    SetColor(block.LipQuarters, Color.Lerp(lipFrom, lipTo, t));
                })
                .SetEase(config.PeelEase);
        }

        private static void SetColor(List<SpriteRenderer> renderers, Color color)
        {
            for (var i = 0; i < renderers.Count; i++)
            {
                renderers[i].color = color;
            }
        }

        /// <summary><paramref name="color"/> at <paramref name="opacity"/> times its own alpha.</summary>
        private static Color WithOpacity(Color color, float opacity) =>
            new Color(color.r, color.g, color.b, color.a * opacity);

        /// <summary>
        /// An opened shutter's panel, drawn again as a temporary piece, lifts
        /// off the blocks now drawn beneath it: it shrinks toward its top edge
        /// while fading.
        /// </summary>
        private Tween LiftShutter(int shutterIndex)
        {
            var shutter = ctx.Shutters[shutterIndex];
            var top = GridToLocal(new Vector2((shutter.Min.X + shutter.Max.X + 1) * 0.5f, shutter.Max.Y + 1));
            var pivot = AddGroup(stage, $"Lifting shutter {shutter.Id}", top);
            var frame = AddGroup(pivot, "Panel", -top);
            DrawShutterPanel(frame, shutterIndex, null);
            var fade = new Fade(pivot);

            return DOVirtual.Float(0f, 1f, config.ShutterLiftSeconds, t =>
                {
                    pivot.localScale = new Vector3(1f, 1f - t, 1f);
                    fade.Apply(1f - t);
                })
                .SetEase(config.ShutterLiftEase);
        }

        /// <summary>
        /// An opened lock's padlock and chains, drawn again as a temporary piece
        /// where the old drawing had them, fade out while growing slightly about
        /// the padlock. Nothing flies.
        /// </summary>
        private Tween OpenLock(int blockIndex, Coord origin, BoardState before)
        {
            var cells = ctx.SpecAt(blockIndex).Cells;

            // The marks as the old drawing had them: the block was locked, so
            // it had its padlock, beside a layer badge and a time-bonus mark
            // if it carried them.
            var shown = before != null ? visibility.Block(before, blockIndex) : default;
            var marks = MarksOf(
                cells, shown.IsFrozen, hasIcon: true, hasBonus: shown.TimeBonusSeconds > 0,
                hasLayers: shown.LayerNumeral.HasValue);
            var icon = marks.Icon;
            var pivot = AddGroup(stage, $"Opening lock {blockIndex}", OriginToLocal(origin) + icon);
            var frame = AddGroup(pivot, "Lock", -icon);
            DrawLock(frame, cells, marks, null);
            var fade = new Fade(pivot);

            return DOVirtual.Float(0f, 1f, config.LockOpenSeconds, t =>
                {
                    var scale = Mathf.LerpUnclamped(1f, config.LockOpenScale, t);
                    pivot.localScale = new Vector3(scale, scale, 1f);
                    fade.Apply(1f - t);
                })
                .SetEase(config.LockOpenEase);
        }

        /// <summary>A badge scales up to the pop scale and back once, along half a sine.</summary>
        private Tween PopBadge(Transform badge)
        {
            // Linear time: the sine is the pop's whole shape.
            return DOVirtual.Float(0f, 1f, config.BadgePopSeconds, t =>
                {
                    var scale = 1f + (config.BadgePopScale - 1f) * Mathf.Sin(Mathf.PI * t);
                    badge.localScale = new Vector3(scale, scale, 1f);
                })
                .SetEase(Ease.Linear);
        }

        /// <summary>
        /// A generated block starts at its machine's screen, at the size the
        /// screen showed it, and grows while moving to its cells.
        /// </summary>
        private Tween ArriveFromGenerator(DrawnBlock block, int generatorIndex)
        {
            var screen = GridRectToLocal(machines[generatorIndex].Screen);
            var startScale = MiniatureFit(block.Cells, screen, out _) / layout.CellSize;
            Vector2 target = block.Root.localPosition;
            var start = screen.center - block.FootprintCenter;

            void Apply(float e)
            {
                block.Root.localPosition = Vector2.LerpUnclamped(start, target, e);
                SetBodyScale(block, Mathf.LerpUnclamped(startScale, 1f, e));
            }

            Apply(0f);
            return DOVirtual.Float(0f, 1f, config.GeneratorSpawnSeconds, Apply).SetEase(config.GeneratorSpawnEase);
        }

        /// <summary>
        /// An elevator delivers a wave: its doors, drawn again as two temporary
        /// halves over the hidden doors, slide apart into the region's sides;
        /// the wave's blocks rise from slightly small and low to their cells;
        /// and the halves close again beneath them.
        /// </summary>
        private void RiseFromElevator(Sequence arrive, int elevatorIndex, List<DrawnBlock> wave)
        {
            var elevator = ctx.Elevators[elevatorIndex];
            var center = RegionCenter(elevator.Min, elevator.Max);
            var size = RegionSize(elevator.Min, elevator.Max);

            if (elevatorDoors.TryGetValue(elevatorIndex, out var doors))
            {
                for (var i = 0; i < doors.Length; i++)
                {
                    doors[i].SetActive(false);
                    hiddenDoors.Add(doors[i]);
                }
            }

            var sprite = config.DoorPanelSprite;
            var scale = CellTileScale(sprite);
            var halfWidth = size.x * 0.5f;
            var leftEdge = center.x - halfWidth;
            var rightEdge = center.x + halfWidth;
            var left = AddTiled(stage, "Left door", sprite, center, size, scale, config.ElevatorDoorColor, config.ElevatorDoorOrder);
            var right = AddTiled(stage, "Right door", sprite, center, size, scale, config.ElevatorDoorColor, config.ElevatorDoorOrder);

            // Each half keeps its outer side against the region's side and
            // narrows toward it, so the doors slide under the sides unmasked.
            void SetDoors(float width)
            {
                left.size = new Vector2(width / scale, size.y / scale);
                left.transform.localPosition = new Vector2(leftEdge + width * 0.5f, center.y);
                right.size = new Vector2(width / scale, size.y / scale);
                right.transform.localPosition = new Vector2(rightEdge - width * 0.5f, center.y);
            }

            SetDoors(halfWidth);

            var targets = new Vector2[wave.Count];
            for (var i = 0; i < wave.Count; i++)
            {
                targets[i] = wave[i].Root.localPosition;
                wave[i].Root.gameObject.SetActive(false);
            }

            var drop = CellsToWorld(config.RiseDropCells);

            void Rise(float e)
            {
                for (var i = 0; i < wave.Count; i++)
                {
                    wave[i].Root.localPosition = targets[i] + Vector2.down * (drop * (1f - e));
                    SetBodyScale(wave[i], Mathf.LerpUnclamped(config.RiseStartScale, 1f, e));
                }
            }

            var open = config.DoorsOpenSeconds;
            var rise = config.RiseSeconds;
            arrive.Insert(0f, DOVirtual.Float(0f, 1f, open, t => SetDoors(halfWidth * (1f - t))).SetEase(config.DoorsEase));
            arrive.InsertCallback(open, () =>
            {
                for (var i = 0; i < wave.Count; i++)
                {
                    wave[i].Root.gameObject.SetActive(true);
                }

                Rise(0f);
            });
            arrive.Insert(open, DOVirtual.Float(0f, 1f, rise, Rise).SetEase(config.RiseEase));
            arrive.Insert(
                open + rise,
                DOVirtual.Float(0f, 1f, config.DoorsCloseSeconds, t => SetDoors(halfWidth * t)).SetEase(config.DoorsEase));
        }
    }
}
