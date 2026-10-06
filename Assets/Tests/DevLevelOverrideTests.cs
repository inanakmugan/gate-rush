using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 21's <see cref="DevLevelOverride"/>: the override is
    /// one-shot — taking it clears it — and one naming a level that does not
    /// load is reported, by name, and ignored.
    /// </summary>
    /// <remarks>
    /// The override lives in the editor's <c>SessionState</c>, which outlasts
    /// a test run, so every test starts and ends with it cleared: a test must
    /// not leave a start level behind for the owner's next Play.
    /// </remarks>
    public class DevLevelOverrideTests
    {
        [SetUp]
        public void ClearBefore()
        {
            DevLevelOverride.Clear();
        }

        [TearDown]
        public void ClearAfter()
        {
            DevLevelOverride.Clear();
        }

        [Test]
        public void TryTake_AfterSet_ReturnsTheNameOnce()
        {
            DevLevelOverride.Set("level-6");

            var first = DevLevelOverride.TryTake(out var firstName);
            var second = DevLevelOverride.TryTake(out var secondName);

            Assert.IsTrue(first);
            Assert.AreEqual("level-6", firstName);
            Assert.IsFalse(second);
            Assert.IsNull(secondName);
        }

        [Test]
        public void TryTake_NothingSet_ReturnsFalse()
        {
            var found = DevLevelOverride.TryTake(out var name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void TryTake_AfterClear_ReturnsFalse()
        {
            DevLevelOverride.Set("level-6");
            DevLevelOverride.Clear();

            var found = DevLevelOverride.TryTake(out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryResolve_LoadableName_NoWarning()
        {
            var loadable = new[] { "level-0", "level-6" };

            var resolved = DevLevelOverride.TryResolve("level-6", loadable, out var warning);

            Assert.IsTrue(resolved);
            Assert.IsNull(warning);
        }

        [Test]
        public void TryResolve_NameNotLoadable_WarnsNamingIt()
        {
            var loadable = new[] { "level-0", "level-6" };

            var resolved = DevLevelOverride.TryResolve("level-9", loadable, out var warning);

            Assert.IsFalse(resolved);
            StringAssert.Contains("level-9", warning);
        }
    }
}
