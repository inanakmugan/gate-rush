using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 25's <see cref="SoundSetting"/>: it starts on; the legacy
    /// text, now only read (Module 29), is read both ways from literal
    /// strings; and legacy data that cannot be read — nothing saved, or
    /// garbage — reads as on without throwing, with
    /// <see cref="SoundSetting.TryFromData"/> telling garbage apart from
    /// nothing saved.
    /// </summary>
    public class SoundSettingTests
    {
        [Test]
        public void IsOn_NewSetting_IsTrue()
        {
            var setting = new SoundSetting();

            Assert.IsTrue(setting.IsOn);
        }

        [TestCase("on", true)]
        [TestCase("off", false)]
        public void TryFromData_LegacyText_GivesThatSetting(string data, bool expectedIsOn)
        {
            var isReadable = SoundSetting.TryFromData(data, out var read);

            Assert.IsTrue(isReadable);
            Assert.AreEqual(expectedIsOn, read.IsOn);
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
        public void LegacySaveKey_IsTheSpecified()
        {
            Assert.AreEqual("GateRush.SoundOn", SoundSetting.LegacySaveKey);
        }
    }
}
