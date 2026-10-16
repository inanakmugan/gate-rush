using System;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 34's <see cref="JokerSelection"/> (D67): pressing a
    /// Rocket or Broom button selects it, pressing it again unselects it,
    /// pressing the other switches, and <see cref="JokerSelection.Clear"/>
    /// unselects. The Clock is never selected.
    /// </summary>
    public class JokerSelectionTests
    {
        [Test]
        public void Selected_AtFirst_IsNothing()
        {
            var selection = new JokerSelection();

            Assert.IsNull(selection.Selected);
        }

        [TestCase(JokerKind.Rocket)]
        [TestCase(JokerKind.Broom)]
        public void Press_NothingSelected_SelectsThatKind(JokerKind kind)
        {
            var selection = new JokerSelection();

            selection.Press(kind);

            Assert.AreEqual(kind, selection.Selected);
        }

        [TestCase(JokerKind.Rocket)]
        [TestCase(JokerKind.Broom)]
        public void Press_SameKindAgain_Unselects(JokerKind kind)
        {
            var selection = new JokerSelection();
            selection.Press(kind);

            selection.Press(kind);

            Assert.IsNull(selection.Selected);
        }

        [TestCase(JokerKind.Rocket, JokerKind.Broom)]
        [TestCase(JokerKind.Broom, JokerKind.Rocket)]
        public void Press_OtherKind_SwitchesToIt(JokerKind first, JokerKind second)
        {
            var selection = new JokerSelection();
            selection.Press(first);

            selection.Press(second);

            Assert.AreEqual(second, selection.Selected);
        }

        [Test]
        public void Clear_SomethingSelected_Unselects()
        {
            var selection = new JokerSelection();
            selection.Press(JokerKind.Rocket);

            selection.Clear();

            Assert.IsNull(selection.Selected);
        }

        [Test]
        public void Press_Clock_ThrowsAndChangesNothing()
        {
            var selection = new JokerSelection();
            selection.Press(JokerKind.Broom);

            Assert.Throws<ArgumentOutOfRangeException>(() => selection.Press(JokerKind.Clock));

            Assert.AreEqual(JokerKind.Broom, selection.Selected);
        }

        [Test]
        public void Press_UndefinedKind_Throws()
        {
            var selection = new JokerSelection();

            Assert.Throws<ArgumentOutOfRangeException>(() => selection.Press((JokerKind)99));
        }
    }
}
