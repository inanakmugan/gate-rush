using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="SaveData"/> as Module 38 extends it: a new player's
    /// save has haptics on and the rules' default name and avatar, and a
    /// missing profile or haptics section throws.
    /// </summary>
    public class SaveDataTests
    {
        [Test]
        public void CreateDefault_Rules_HapticsOnAndTheRulesNameAndAvatar()
        {
            var save = SaveData.CreateDefault(SaveFixture.Rules);

            Assert.IsTrue(save.Haptics.IsOn);
            Assert.AreEqual(MetaRulesFixture.DefaultPlayerName, save.Profile.Name);
            Assert.AreEqual(MetaRulesFixture.DefaultAvatarId, save.Profile.AvatarId);
        }

        [Test]
        public void Constructor_NullProfile_Throws()
        {
            var save = SaveFixture.EveryField();

            Assert.Throws<ArgumentNullException>(() => new SaveData(
                save.Completed, save.Sound, save.Wallet, save.Lives, save.Jokers, save.Streak, save.Campaign, null, save.Haptics));
        }

        [Test]
        public void Constructor_NullHaptics_Throws()
        {
            var save = SaveFixture.EveryField();

            Assert.Throws<ArgumentNullException>(() => new SaveData(
                save.Completed, save.Sound, save.Wallet, save.Lives, save.Jokers, save.Streak, save.Campaign, save.Profile, null));
        }
    }
}
