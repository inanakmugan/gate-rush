using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>Covers Module 38's <see cref="HapticsSetting"/>: a new player has haptics on.</summary>
    public class HapticsSettingTests
    {
        [Test]
        public void IsOn_NewSetting_IsTrue()
        {
            var setting = new HapticsSetting();

            Assert.IsTrue(setting.IsOn);
        }
    }
}
