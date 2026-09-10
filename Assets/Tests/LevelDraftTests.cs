using System;
using System.Text.RegularExpressions;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelDraft"/>: that <c>draft &lt;-&gt; LevelDto</c> is
    /// lossless over every field (the drift guard), that <c>draft -&gt;
    /// LevelContext</c> applies <c>Core</c>'s rules and surfaces its errors
    /// unchanged, and that a draft can hold states <see cref="LevelContext"/>
    /// would reject without throwing.
    /// </summary>
    public class LevelDraftTests
    {
        private static string Canonical(string json) => Regex.Replace(json, @"\s+", string.Empty);

        [Test]
        public void DraftDtoRoundTrip_OverEveryField_IsLossless()
        {
            // ctx -> json -> dto -> draft -> dto -> json. Any field LevelDraft
            // drops (or invents) shows up as a JSON diff. The fixture is the same
            // one the serializer round trip uses, so a new schema field has to be
            // added here to keep either test green.
            var beforeJson = LevelSerializer.ToJson(Corpus.EveryFieldLevel());

            var draft = LevelDraft.FromDto(LevelSerializer.ParseDto(beforeJson));
            var afterJson = LevelSerializer.ToJson(draft.ToDto());

            Assert.AreEqual(Canonical(beforeJson), Canonical(afterJson));
        }

        [Test]
        public void ToContext_ValidDraft_ProducesTheContext()
        {
            var draft = LevelDraft.FromDto(LevelSerializer.ParseDto(LevelSerializer.ToJson(Corpus.EveryFieldLevel())));

            var ctx = draft.ToContext();

            Assert.AreEqual(5, ctx.Blocks.Count);
            Assert.AreEqual(new Coord(1, 0), ctx.Elevators[0].Waves[0][1].RegionOrigin);
            Assert.IsNull(ctx.Generators[0].Queue[0].RegionOrigin);
        }

        [Test]
        public void ToContext_InvalidDraft_SurfacesCoresErrorUnchanged()
        {
            var draft = LevelDraft.NewEmpty(5, 5);
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                StartOrigin = new Coord(99, 99),
            });

            var ex = Assert.Throws<ArgumentException>(() => draft.ToContext());

            StringAssert.Contains("outside the 5x5 grid", ex.Message);
        }

        [Test]
        public void Draft_HoldsBlockOutsideGridAndKeyWithNoLock_WithoutThrowing()
        {
            var draft = LevelDraft.NewEmpty(3, 3);

            Assert.DoesNotThrow(() =>
            {
                draft.Blocks.Add(new BlockDraft
                {
                    Id = 1,
                    Cells = { new Coord(0, 0) },
                    ColorStack = { BlockColor.Red },
                    StartOrigin = new Coord(50, 50),
                    KeyTargetLockId = 999,
                });
            });

            Assert.AreEqual(1, draft.Blocks.Count);
            Assert.AreEqual(999, draft.Blocks[0].KeyTargetLockId);
        }

        [Test]
        public void FromDto_UnreadableEnumOrColourName_DefaultsButRecordsALoadIssueRatherThanRepairingSilently()
        {
            var dto = new LevelDto
            {
                formatVersion = LevelSerializer.FormatVersion,
                levelId = 1,
                width = 3,
                height = 3,
                blocks = new[]
                {
                    new BlockDto
                    {
                        id = 3,
                        cells = new[] { new CoordDto { x = 0, y = 0 } },
                        colorStack = new[] { "Mauve" },
                        axis = "NotARealAxis",
                        keyEffect = "UnlockMovement",
                        unfreezeAtClearCount = -1,
                        lockId = -1,
                        keyTargetLockId = -1,
                    },
                },
            };

            var draft = LevelDraft.FromDto(dto);

            // Still falls back to a default so the level opens...
            Assert.AreEqual(MovementAxis.Free, draft.Blocks[0].Axis);
            Assert.AreEqual(BlockColor.Red, draft.Blocks[0].ColorStack[0]);

            // ...but the substitution is recorded, naming the element and raw value.
            Assert.AreEqual(2, draft.LoadIssues.Count);
            Assert.IsTrue(draft.LoadIssues.Exists(i => i.RawValue == "Mauve" && i.ElementLabel.Contains("Block 3")));
            Assert.IsTrue(draft.LoadIssues.Exists(i => i.RawValue == "NotARealAxis"));
        }
    }
}
