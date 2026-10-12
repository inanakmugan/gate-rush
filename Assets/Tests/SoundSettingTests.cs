using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 25's <see cref="SoundSetting"/>: it starts on, survives
    /// <see cref="SoundSetting.ToData"/> and back both ways, and data that
    /// cannot be read — nothing saved, or garbage — reads as on without
    /// throwing, with <see cref="SoundSetting.TryFromData"/> telling garbage
    /// apart from nothing saved.
    /// </summary>
    public class SoundSettingTests
    {
        [Test]
        public void IsOn_NewSetting_IsTrue()
        {
            var setting = new SoundSetting();

            Assert.IsTrue(setting.IsOn);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryFromData_OfToData_GivesTheSameSetting(bool isOn)
        {
            var setting = new SoundSetting { IsOn = isOn };

            var isReadable = SoundSetting.TryFromData(setting.ToData(), out var read);

            Assert.IsTrue(isReadable);
            Assert.AreEqual(isOn, read.IsOn);
        }

        [Test]
        public void ToData_OnAndOff_Differ()
        {
            var on = new SoundSetting { IsOn = true }.ToData();
            var off = new SoundSetting { IsOn = false }.ToData();

            Assert.AreNotEqual(on, off);
        }

        [TestCase((string)null)]
        [TestCase("")]
        public void TryFromData_NoData_IsReadableAndOn(string data)
        {
            var isReadable = SoundSetting.TryFromData(data, out var setting);

            Assert.IsTrue(isReadable, "nothing saved is not unreadable");
            Assert.IsNotNull(setting);
            Assert.IsTrue(setting.IsOn);
        }

        [TestCase("garbage")]
        [TestCase(" ")]
        [TestCase("OFF")]
        [TestCase("off ")]
        [TestCase("0")]
        [TestCase("false")]
        public void TryFromData_UnreadableData_IsReportedAndOn(string data)
        {
            var isReadable = SoundSetting.TryFromData(data, out var setting);

            Assert.IsFalse(isReadable);
            Assert.IsNotNull(setting);
            Assert.IsTrue(setting.IsOn);
        }

        [Test]
        public void SaveKey_IsTheSpecified()
        {
            Assert.AreEqual("GateRush.SoundOn", SoundSetting.SaveKey);
        }
    }
}
