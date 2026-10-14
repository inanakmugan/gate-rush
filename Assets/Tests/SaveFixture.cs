using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// The shared save fixtures. <see cref="EveryField"/> sets every field of
    /// <see cref="SaveData"/> away from its default, and
    /// <see cref="AssertSameSave"/> compares every field: a section added later
    /// (Modules 30 and 31) must be added to both, or the round trips that use
    /// them stop proving that nothing is lost on the way through the codec.
    /// </summary>
    public static class SaveFixture
    {
        /// <summary>
        /// A save with no field at its default: ids that are not contiguous,
        /// not in order and include 0, and sound off.
        /// </summary>
        public static SaveData EveryField() =>
            new SaveData(CompletedLevels.Of(new[] { 7, 0, 3, 12 }), new SoundSetting { IsOn = false });

        /// <summary>Asserts that <paramref name="actual"/> holds what <paramref name="expected"/> holds, field by field.</summary>
        public static void AssertSameSave(SaveData expected, SaveData actual)
        {
            Assert.IsNotNull(actual);
            CollectionAssert.AreEqual(expected.Completed.Ids, actual.Completed.Ids, "completed level ids");
            Assert.AreEqual(expected.Sound.IsOn, actual.Sound.IsOn, "sound on");
        }
    }
}
