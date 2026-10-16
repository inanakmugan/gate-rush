using System;
using GateRush.Meta;

namespace GateRush.Runtime
{
    /// <summary>
    /// The Rocket or Broom the player has selected and not yet used (D67):
    /// pressing a button selects its joker, pressing it again unselects it,
    /// and pressing the other one switches. Selecting spends nothing; the
    /// next valid tap on a block uses the joker. The Clock has no target and
    /// is never selected. Belongs to the level being played and is never
    /// saved.
    /// </summary>
    public sealed class JokerSelection
    {
        /// <summary>The selected joker, or null when none is.</summary>
        public JokerKind? Selected { get; private set; }

        /// <summary>
        /// A press on <paramref name="kind"/>'s button: selects it, or
        /// unselects it when it is already selected, or switches to it from
        /// the other one.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="kind"/> is neither <see cref="JokerKind.Rocket"/>
        /// nor <see cref="JokerKind.Broom"/>: the Clock is used at once and an
        /// undefined kind is a bug.
        /// </exception>
        public void Press(JokerKind kind)
        {
            if (kind != JokerKind.Rocket && kind != JokerKind.Broom)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind), kind, "Only the Rocket and the Broom are selected; the Clock is used at once.");
            }

            Selected = Selected == kind ? (JokerKind?)null : kind;
        }

        /// <summary>Unselects whatever is selected.</summary>
        public void Clear()
        {
            Selected = null;
        }
    }
}
