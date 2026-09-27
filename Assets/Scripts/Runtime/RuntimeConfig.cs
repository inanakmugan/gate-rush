using System;
using System.Collections.Generic;
using GateRush.Core;
using TMPro;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Every tunable value the board's presentation and input read: palette,
    /// tints, sizes, margins, sorting orders, the drag threshold and label
    /// settings. Nothing in <c>GateRush.Runtime</c> hardcodes one of these at a
    /// call site. Sizes are in cells unless named otherwise, so the board keeps
    /// its proportions whatever <see cref="CellSize"/> is.
    /// </summary>
    [CreateAssetMenu(fileName = "RuntimeConfig", menuName = "Gate Rush/Runtime Config")]
    public sealed class RuntimeConfig : ScriptableObject
    {
        [Header("Assets")]
        [Tooltip("A plain white square sprite. Every placeholder shape is this sprite, tinted and scaled.")]
        [SerializeField] private Sprite cellSprite;

        [Tooltip("Sprite-Unlit-Default. Under the 2D Renderer a lit sprite renders black with no Light 2D.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("TextMeshPro font for every label. Leave empty to use the TMP Settings default font.")]
        [SerializeField] private TMP_FontAsset labelFont;

        [Header("Board")]
        [Tooltip("World units per cell.")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("Empty space kept around the board on every side when fitting the camera, in cells.")]
        [SerializeField] private float cameraMarginCells = 1f;

        [Tooltip("Gap between neighbouring cells, as a fraction of a cell.")]
        [SerializeField, Range(0f, 0.5f)] private float cellGap = 0.06f;

        [Tooltip("Size of the beneath-colour square drawn inside each cell of a layered block, as a fraction of a cell.")]
        [SerializeField, Range(0f, 1f)] private float beneathColorSize = 0.45f;

        [Tooltip("Thickness of gate and generator bars outside the board edge, in cells.")]
        [SerializeField] private float edgeBarThickness = 0.3f;

        [Tooltip("Thickness of an elevator's region outline, in cells.")]
        [SerializeField] private float elevatorOutlineThickness = 0.08f;

        [Tooltip("Side of a lock or key badge, in cells.")]
        [SerializeField] private float badgeSize = 0.36f;

        [Header("Input")]
        [Tooltip("How far the pointer must travel, in cells, before a release at the start reads as a push.")]
        [SerializeField] private float pushThresholdCells = 0.3f;

        [Header("Colours")]
        [Tooltip("One colour per BlockColor, in enum order: Red, Blue, Green, Yellow, Purple, Orange, Pink, Cyan.")]
        [SerializeField] private Color[] blockPalette =
        {
            new Color(0.90f, 0.24f, 0.24f),
            new Color(0.22f, 0.45f, 0.92f),
            new Color(0.25f, 0.72f, 0.33f),
            new Color(0.97f, 0.82f, 0.20f),
            new Color(0.58f, 0.34f, 0.82f),
            new Color(0.97f, 0.55f, 0.18f),
            new Color(0.96f, 0.52f, 0.74f),
            new Color(0.26f, 0.82f, 0.86f)
        };

        [Tooltip("Lock and key badge colours, indexed by lock id (M8: the identifier doubles as the badge colour). Keep them light: the count on a lock badge is drawn in Label Color.")]
        [SerializeField] private Color[] lockBadgePalette =
        {
            new Color(1.00f, 1.00f, 1.00f),
            new Color(0.72f, 0.72f, 0.72f),
            new Color(0.86f, 0.72f, 0.52f),
            new Color(0.72f, 0.95f, 0.72f),
            new Color(0.82f, 0.76f, 1.00f),
            new Color(1.00f, 0.80f, 0.80f)
        };

        [Tooltip("Badge colour used for a lock id outside the badge palette. The load logs an error naming the lock.")]
        [SerializeField] private Color unknownBadgeColor = new Color(1f, 0f, 1f);

        [SerializeField] private Color backgroundColor = new Color(0.12f, 0.13f, 0.16f);
        [SerializeField] private Color emptyCellColor = new Color(0.22f, 0.24f, 0.29f);
        [SerializeField] private Color wallColor = new Color(0.07f, 0.07f, 0.09f);
        [SerializeField] private Color frozenTint = new Color(0.72f, 0.86f, 0.95f);
        [SerializeField] private Color closedGateColor = new Color(0.60f, 0.72f, 0.80f);
        [SerializeField] private Color shutterColor = new Color(0.36f, 0.38f, 0.44f);
        [SerializeField] private Color generatorColor = new Color(0.85f, 0.85f, 0.85f);
        [SerializeField] private Color elevatorOutlineColor = new Color(1f, 1f, 1f, 0.8f);

        [Header("Labels")]
        [SerializeField] private float labelFontSize = 4f;
        [SerializeField] private float badgeLabelFontSize = 2.5f;
        [SerializeField] private Color labelColor = new Color(0.08f, 0.08f, 0.10f);
        [SerializeField] private Color lightLabelColor = new Color(0.95f, 0.95f, 0.95f);

        [Header("Sorting orders (back to front)")]
        [SerializeField] private int cellOrder;
        [SerializeField] private int edgeFeatureOrder = 1;
        [SerializeField] private int blockOrder = 2;
        [SerializeField] private int beneathColorOrder = 3;
        [SerializeField] private int badgeOrder = 4;
        [SerializeField] private int shutterOrder = 5;
        [SerializeField] private int elevatorOrder = 6;
        [SerializeField] private int labelOrder = 7;

        /// <summary>The white square every placeholder shape is drawn with.</summary>
        public Sprite CellSprite => cellSprite;

        /// <summary>The material every sprite uses: <c>Sprite-Unlit-Default</c>.</summary>
        public Material SpriteMaterial => spriteMaterial;

        /// <summary>Label font; null means the TMP Settings default.</summary>
        public TMP_FontAsset LabelFont => labelFont;

        /// <summary>World units per cell.</summary>
        public float CellSize => cellSize;

        /// <summary>Margin around the board when fitting the camera, in cells.</summary>
        public float CameraMarginCells => cameraMarginCells;

        /// <summary>Gap between neighbouring cells, as a fraction of a cell.</summary>
        public float CellGap => cellGap;

        /// <summary>Side of the beneath-colour square, as a fraction of a cell.</summary>
        public float BeneathColorSize => beneathColorSize;

        /// <summary>Thickness of gate and generator bars, in cells.</summary>
        public float EdgeBarThickness => edgeBarThickness;

        /// <summary>Thickness of an elevator outline, in cells.</summary>
        public float ElevatorOutlineThickness => elevatorOutlineThickness;

        /// <summary>Side of a lock or key badge, in cells.</summary>
        public float BadgeSize => badgeSize;

        /// <summary>Pointer travel, in cells, that makes a release at the start a push.</summary>
        public float PushThresholdCells => pushThresholdCells;

        /// <summary>Camera clear colour behind the board.</summary>
        public Color BackgroundColor => backgroundColor;

        /// <summary>Fill of a grid cell with nothing on it.</summary>
        public Color EmptyCellColor => emptyCellColor;

        /// <summary>Fill of a static wall.</summary>
        public Color WallColor => wallColor;

        /// <summary>Fill of a frozen block, whose colour is hidden (M3).</summary>
        public Color FrozenTint => frozenTint;

        /// <summary>Fill of a closed, colourless gate (M2).</summary>
        public Color ClosedGateColor => closedGateColor;

        /// <summary>Fill of a closed shutter's cover (M5).</summary>
        public Color ShutterColor => shutterColor;

        /// <summary>Fill of a generator's edge marker (M6).</summary>
        public Color GeneratorColor => generatorColor;

        /// <summary>Colour of an elevator's region outline (M9).</summary>
        public Color ElevatorOutlineColor => elevatorOutlineColor;

        /// <summary>Font size of count labels on cells.</summary>
        public float LabelFontSize => labelFontSize;

        /// <summary>Font size of the count on a badge.</summary>
        public float BadgeLabelFontSize => badgeLabelFontSize;

        /// <summary>Label colour on light fills.</summary>
        public Color LabelColor => labelColor;

        /// <summary>Label colour on dark fills: shutters, walls, the background.</summary>
        public Color LightLabelColor => lightLabelColor;

        /// <summary>Sorting order of grid cells and walls.</summary>
        public int CellOrder => cellOrder;

        /// <summary>Sorting order of gate and generator bars.</summary>
        public int EdgeFeatureOrder => edgeFeatureOrder;

        /// <summary>Sorting order of block cells.</summary>
        public int BlockOrder => blockOrder;

        /// <summary>Sorting order of the beneath-colour squares.</summary>
        public int BeneathColorOrder => beneathColorOrder;

        /// <summary>Sorting order of lock and key badges.</summary>
        public int BadgeOrder => badgeOrder;

        /// <summary>Sorting order of shutter covers.</summary>
        public int ShutterOrder => shutterOrder;

        /// <summary>Sorting order of elevator outlines.</summary>
        public int ElevatorOrder => elevatorOrder;

        /// <summary>Sorting order of every label, in front of everything else.</summary>
        public int LabelOrder => labelOrder;

        /// <summary>The fill for block colour <paramref name="color"/>.</summary>
        /// <exception cref="InvalidOperationException">The palette has no entry for it; see <see cref="Problems"/>.</exception>
        public Color BlockFill(BlockColor color)
        {
            var index = (int)color;
            if (blockPalette == null || index >= blockPalette.Length)
            {
                throw new InvalidOperationException(
                    $"{name}: the block palette has no entry for {color}.");
            }

            return blockPalette[index];
        }

        /// <summary>
        /// The badge colour for lock <paramref name="lockId"/>. False — with
        /// <see cref="unknownBadgeColor"/> in <paramref name="color"/> — when the
        /// id lies outside the badge palette.
        /// </summary>
        public bool TryGetBadgeColor(int lockId, out Color color)
        {
            if (lockBadgePalette != null && lockId >= 0 && lockId < lockBadgePalette.Length)
            {
                color = lockBadgePalette[lockId];
                return true;
            }

            color = unknownBadgeColor;
            return false;
        }

        /// <summary>
        /// Every reason this config cannot draw a board, as messages naming the
        /// field; empty when it is usable.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            var colourCount = Enum.GetValues(typeof(BlockColor)).Length;

            if (cellSprite == null)
            {
                problems.Add($"{name}: Cell Sprite is not assigned.");
            }

            if (spriteMaterial == null)
            {
                problems.Add($"{name}: Sprite Material is not assigned (use Sprite-Unlit-Default).");
            }

            if (!(cellSize > 0f))
            {
                problems.Add($"{name}: Cell Size must be positive.");
            }

            if (cameraMarginCells < 0f)
            {
                problems.Add($"{name}: Camera Margin Cells may not be negative.");
            }

            if (!(pushThresholdCells > 0f))
            {
                problems.Add($"{name}: Push Threshold Cells must be positive.");
            }

            if (blockPalette == null || blockPalette.Length < colourCount)
            {
                problems.Add($"{name}: Block Palette needs {colourCount} entries, one per BlockColor.");
            }

            return problems;
        }
    }
}
