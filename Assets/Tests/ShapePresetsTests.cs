using System;
using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ShapePresets"/>: every preset the window can place must
    /// be a non-empty, orthogonally connected cell set — the same shape rule
    /// <see cref="BlockDefinition"/> enforces — so placing one never produces a
    /// block <c>Core</c> would reject on shape alone.
    /// </summary>
    public class ShapePresetsTests
    {
        [Test]
        public void EveryFixedPreset_IsANonEmptyConnectedCellSet()
        {
            foreach (ShapePreset preset in Enum.GetValues(typeof(ShapePreset)))
            {
                if (preset == ShapePreset.Free)
                {
                    Assert.Throws<ArgumentException>(() => ShapePresets.Cells(preset));
                    continue;
                }

                var cells = ShapePresets.Cells(preset);

                Assert.IsNotEmpty(cells, preset.ToString());
                Assert.AreEqual(cells.Count, cells.Distinct().Count(), $"{preset} has a duplicate cell");
                Assert.DoesNotThrow(
                    () => new BlockDefinition(
                        id: 1,
                        cells: cells,
                        colorStack: new[] { BlockColor.Red },
                        startOrigin: new Coord(0, 0),
                        axis: MovementAxis.Free,
                        unfreezeAtClearCount: null,
                        lockId: null,
                        requiredKeyCount: 0,
                        keyTargetLockId: null,
                        keyEffect: KeyEffect.UnlockMovement,
                        timeBonusSeconds: 0),
                    $"{preset} is not a valid block shape");
            }
        }
    }
}
