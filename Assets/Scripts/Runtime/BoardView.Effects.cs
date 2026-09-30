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
        /// surviving layered block peels. Then the arrive stage, at once when
        /// there is no peel to wait for.
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

                leave = leave ?? DOTween.Sequence().SetId(this);
                leave.Insert(0f, PeelEffect(block, clear.ExposedColor.Value));
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
        /// reach past the face (<see cref="LiftOutline"/>).
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
                PoseQuarter(
                    block.OutlineQuarters[i], tile,
                    CellRectToLocal(LiftOutline.QuarterRect(tile, width, config.LipOffsetCells)));
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
        /// its depth times <see cref="RuntimeConfig.ExitSecondsPerCell"/> — while
        /// its lift drops, cubes stream out beneath the gate
        /// (<see cref="BurstLayout.Stream"/>) and the gate glows inward. Every
        /// tween carries <see cref="debrisId"/>, so the stages and the redraw
        /// never touch it and it never holds <see cref="IsBusy"/>; the pass is
        /// counted, so a win's panel waits for it (<see cref="EndPresentation"/>).
        /// </summary>
        private void PassThroughGate(DrawnBlock block, ClearedBlock clear)
        {
            var edge = clear.GateEdge;
            var gate = ctx.Gates[clear.GateIndex];
            var root = block.Root;

            block.LiftTween?.Kill();
            block.LiftTween = null;
            drawnBlocks.Remove(clear.BlockIndex);
            root.SetParent(debris, true);
            var mask = ClipAtGate(block, edge, out var maskCenter);

            var passSeconds = GateExit.DepthCells(block.Cells, edge) * config.ExitSecondsPerCell;
            var travel = GateExit.Outward(edge) * CellsToWorld(GateExit.PassCells(block.Cells, edge, config.LipOffsetCells));
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

            var cubes = BurstLayout.Stream(
                ctx.Width, ctx.Height, edge, gate.Offset, gate.Width, layout.FrameThicknessCells,
                block.Cells.Count, passSeconds,
                BurstLayout.Seed(BurstKind.Exit, clear.BlockIndex, presentedMoveNumber), cubeBurst);
            PlayBurst(
                cubes, GridToLocal, layout.CellSize, config.CubeSprite, config.BlockFill(clear.RemovedColor),
                config.CubeSeconds, config.CubeEase, config.CubeEndScale, 0f);

            PlayGateGlow(clear.GateIndex, passSeconds);
        }

        /// <summary>
        /// Clips a passing block at its gate's inner line with a sprite mask in
        /// the block's own sorting group — the lift's, or a new one at the same
        /// order — so it masks this block and nothing else on the board. Only
        /// this block's sprites are set to show inside it. The mask covers
        /// <see cref="GateExit.MaskRect"/>: exactly the inner line on the exit
        /// side, and past the grid by the block's overhang
        /// (<see cref="GateExit.OverhangCells"/>) on the other three, so a block
        /// on a border row keeps its lip and outline there. It is the root's
        /// child, so the pass keeps it still on the board by moving it against
        /// the root; <paramref name="maskCenter"/> is where it stays, in the
        /// view's frame. Labels ignore sprite masks, and a block a move can
        /// destroy carries none; any there are hidden.
        /// </summary>
        private Transform ClipAtGate(DrawnBlock block, BoardEdge edge, out Vector2 maskCenter)
        {
            var root = block.Root;
            if (block.LiftGroup == null)
            {
                block.LiftGroup = root.gameObject.AddComponent<SortingGroup>();
                block.LiftGroup.sortingOrder = config.LiftedBlockOrder;
            }

            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                label.gameObject.SetActive(false);
            }

            foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }

            var margin = GateExit.OverhangCells(block.Cells, config.LiftScale, config.OutlineWidthCells, config.LipOffsetCells);
            var area = GridRectToLocal(GateExit.MaskRect(ctx.Width, ctx.Height, edge, margin));
            maskCenter = area.center;

            var go = new GameObject("Exit clip");
            go.transform.SetParent(root, false);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = config.CellSprite;
            var bounds = config.CellSprite.bounds.size;
            go.transform.localScale = new Vector3(area.width / bounds.x, area.height / bounds.y, 1f);
            go.transform.localPosition = maskCenter - (Vector2)root.localPosition;
            return go.transform;
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
            var gate = ctx.Gates[gateIndex];
            var area = GridRectToLocal(GateExit.GlowRect(
                ctx.Width, ctx.Height, gate.Edge, gate.Offset, gate.Width, config.GateGlowDepthCells));

            // Drawn along the gate before it is turned: the sprite's strong
            // bottom edge faces the way out once turned from "down" to outward.
            var size = new Vector2(CellsToWorld(gate.Width), CellsToWorld(config.GateGlowDepthCells));
            var turn = Vector2.SignedAngle(Vector2.down, GateExit.Outward(gate.Edge));
            var color = Color.Lerp(config.BlockFill(gate.Color), Color.white, config.GateGlowWhiten);
            color.a = config.GateGlowAlpha;

            var glow = AddSprite(debris, "Gate glow", config.GateGlowSprite, area.center, size, color, config.EffectOrder, turn);
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
        /// A surviving layered block peels its removed outer colour: a new face
        /// in the exposed colour is drawn beneath, the lip takes the exposed
        /// colour at once, and the old face — lifted above every part of the new
        /// one by a sorting group, and still below the block's marks — shrinks
        /// about the footprint's centre and fades away.
        /// </summary>
        private Tween PeelEffect(DrawnBlock block, BlockColor exposed)
        {
            for (var i = 0; i < block.BeneathSquares.Count; i++)
            {
                block.BeneathSquares[i].gameObject.SetActive(false);
            }

            var exposedFill = config.BlockFill(exposed);
            var lipFill = config.LipFill(exposedFill);
            foreach (var lip in block.Lip.GetComponentsInChildren<SpriteRenderer>())
            {
                lip.color = lipFill;
            }

            var peeling = block.Face;
            var group = peeling.gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = config.PeelOrder;
            block.Face = DrawFace(block, exposedFill);

            var center = block.FootprintCenter;
            var fade = new Fade(peeling);

            // The face group sits at the body's origin; shifting it by
            // centre · t while it shrinks to 1 − t keeps the footprint's centre
            // fixed.
            return DOVirtual.Float(0f, 1f, config.PeelSeconds, t =>
                {
                    var scale = 1f - t;
                    peeling.localScale = new Vector3(scale, scale, 1f);
                    peeling.localPosition = center * t;
                    fade.Apply(1f - t);
                })
                .SetEase(config.PeelEase);
        }

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
            var wasFrozen = before != null && visibility.Block(before, blockIndex).IsFrozen;
            var icon = IconPosition(cells, wasFrozen);
            var pivot = AddGroup(stage, $"Opening lock {blockIndex}", OriginToLocal(origin) + icon);
            var frame = AddGroup(pivot, "Lock", -icon);
            DrawLock(frame, cells, icon, null);
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
