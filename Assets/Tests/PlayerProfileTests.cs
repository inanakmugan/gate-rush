using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 38's <see cref="PlayerProfile"/>: the name rule trims,
    /// collapses inner runs of spaces, keeps 1 and 16 characters, letters of
    /// any alphabet — Turkish ones included — digits and <c>- _ .</c>, and
    /// counts the length after collapsing; it refuses 0 and 17 characters and
    /// any other symbol, emoji and tabs included. <see cref="PlayerProfile.TrySetName"/>
    /// leaves the name unchanged when refused, <see cref="PlayerProfile.SetAvatar"/>
    /// refuses an empty id, and <see cref="PlayerProfile.Starting"/> takes the
    /// rules' defaults.
    /// </summary>
    public class PlayerProfileTests
    {
        private const string SixteenCharacters = "abcdefghijklmnop";
        private const string SeventeenCharacters = "abcdefghijklmnopq";
        private const string SomeAvatar = "panda";

        [Test]
        public void TryNormalizeName_SurroundingSpaces_AreTrimmed()
        {
            var isAccepted = PlayerProfile.TryNormalizeName("  Ayşe ", out var name);

            Assert.IsTrue(isAccepted);
            Assert.AreEqual("Ayşe", name);
        }

        [Test]
        public void TryNormalizeName_RunOfInnerSpaces_CollapsesToOne()
        {
            var isAccepted = PlayerProfile.TryNormalizeName("Ali   Veli  Can", out var name);

            Assert.IsTrue(isAccepted);
            Assert.AreEqual("Ali Veli Can", name);
        }

        [TestCase("A", TestName = "TryNormalizeName_OneCharacter_IsKept")]
        [TestCase(SixteenCharacters, TestName = "TryNormalizeName_SixteenCharacters_AreKept")]
        [TestCase("ĞğİıŞşÇçÖöÜü", TestName = "TryNormalizeName_TurkishLetters_AreKept")]
        [TestCase("Ελένη", TestName = "TryNormalizeName_GreekLetters_AreKept")]
        [TestCase("Иван", TestName = "TryNormalizeName_CyrillicLetters_AreKept")]
        [TestCase("李小龍", TestName = "TryNormalizeName_ChineseLetters_AreKept")]
        [TestCase("Player 42", TestName = "TryNormalizeName_DigitsAndASpace_AreKept")]
        [TestCase("a-b_c.d", TestName = "TryNormalizeName_HyphenUnderscoreAndPeriod_AreKept")]
        public void TryNormalizeName_AllowedName_IsKeptAsIs(string raw)
        {
            var isAccepted = PlayerProfile.TryNormalizeName(raw, out var name);

            Assert.IsTrue(isAccepted);
            Assert.AreEqual(raw, name);
        }

        [TestCase(null, TestName = "TryNormalizeName_Null_IsRefused")]
        [TestCase("", TestName = "TryNormalizeName_Empty_IsRefused")]
        [TestCase("   ", TestName = "TryNormalizeName_OnlySpaces_AreRefused")]
        [TestCase(SeventeenCharacters, TestName = "TryNormalizeName_SeventeenCharacters_AreRefused")]
        [TestCase("Ali!", TestName = "TryNormalizeName_ExclamationMark_IsRefused")]
        [TestCase("a@b", TestName = "TryNormalizeName_AtSign_IsRefused")]
        [TestCase("Ali😀", TestName = "TryNormalizeName_Emoji_IsRefused")]
        [TestCase("Ali\tVeli", TestName = "TryNormalizeName_TabInside_IsRefused")]
        [TestCase("<b>x</b>", TestName = "TryNormalizeName_Markup_IsRefused")]
        public void TryNormalizeName_RefusedName_IsFalseWithNoName(string raw)
        {
            var isAccepted = PlayerProfile.TryNormalizeName(raw, out var name);

            Assert.IsFalse(isAccepted);
            Assert.IsNull(name);
        }

        [Test]
        public void TryNormalizeName_SixteenAfterCollapsing_IsKept()
        {
            const string raw = "abcdefgh  ijklmno";

            var isAccepted = PlayerProfile.TryNormalizeName(raw, out var name);

            Assert.AreEqual(PlayerProfile.MaxNameLength + 1, raw.Length, "the raw text is one too long");
            Assert.IsTrue(isAccepted);
            Assert.AreEqual(PlayerProfile.MaxNameLength, name.Length);
        }

        [Test]
        public void TryNormalizeName_SixteenAfterTrimming_IsKept()
        {
            var isAccepted = PlayerProfile.TryNormalizeName($"  {SixteenCharacters}  ", out var name);

            Assert.IsTrue(isAccepted);
            Assert.AreEqual(SixteenCharacters, name);
        }

        [Test]
        public void TrySetName_AcceptedName_SetsItNormalized()
        {
            var profile = new PlayerProfile("Old", SomeAvatar);

            var isAccepted = profile.TrySetName("  New   Name ");

            Assert.IsTrue(isAccepted);
            Assert.AreEqual("New Name", profile.Name);
        }

        [TestCase("")]
        [TestCase(SeventeenCharacters)]
        [TestCase("x!")]
        public void TrySetName_RefusedName_IsFalseAndLeavesTheName(string raw)
        {
            var profile = new PlayerProfile("Old", SomeAvatar);

            var isAccepted = profile.TrySetName(raw);

            Assert.IsFalse(isAccepted);
            Assert.AreEqual("Old", profile.Name);
        }

        [Test]
        public void SetAvatar_Id_SetsIt()
        {
            var profile = new PlayerProfile("Old", SomeAvatar);

            profile.SetAvatar("zebra");

            Assert.AreEqual("zebra", profile.AvatarId);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void SetAvatar_EmptyId_ThrowsAndLeavesTheAvatar(string avatarId)
        {
            var profile = new PlayerProfile("Old", SomeAvatar);

            Assert.Throws<ArgumentException>(() => profile.SetAvatar(avatarId));

            Assert.AreEqual(SomeAvatar, profile.AvatarId);
        }

        [Test]
        public void Starting_Rules_TakesTheRulesDefaults()
        {
            var rules = MetaRulesFixture.Build();

            var profile = PlayerProfile.Starting(rules);

            Assert.AreEqual(MetaRulesFixture.DefaultPlayerName, profile.Name);
            Assert.AreEqual(MetaRulesFixture.DefaultAvatarId, profile.AvatarId);
        }

        [Test]
        public void Starting_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PlayerProfile.Starting(null));
        }

        [Test]
        public void Constructor_RefusedName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PlayerProfile("x!", SomeAvatar));
        }

        [Test]
        public void Constructor_EmptyAvatarId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PlayerProfile("Name", " "));
        }
    }
}
