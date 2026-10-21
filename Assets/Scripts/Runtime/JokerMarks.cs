using System;
using GateRush.Meta;

namespace GateRush.Runtime
{
    /// <summary>
    /// The jokers whose buttons are marked on the level being played (Module
    /// 43, D76): each joker a New Joker card introduced, from the moment the
    /// last card closes until that joker is used or the level ends. Belongs
    /// to the level and is never saved, so the next load of the level marks
    /// it again.
    /// </summary>
    public sealed class JokerMarks
    {
        private bool isClockMarked;
        private bool isRocketMarked;
        private bool isBroomMarked;

        /// <summary>True while <paramref name="kind"/> is marked.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public bool IsMarked(JokerKind kind) => Slot(kind);

        /// <summary>Marks <paramref name="kind"/>; marking it again changes nothing.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public void Mark(JokerKind kind)
        {
            Slot(kind) = true;
        }

        /// <summary>Unmarks <paramref name="kind"/>, once it is used; unmarking one that is not marked changes nothing.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public void Unmark(JokerKind kind)
        {
            Slot(kind) = false;
        }

        /// <summary>Unmarks every joker: the level ended, restarted or was left.</summary>
        public void Clear()
        {
            isClockMarked = false;
            isRocketMarked = false;
            isBroomMarked = false;
        }

        private ref bool Slot(JokerKind kind)
        {
            switch (kind)
            {
                case JokerKind.Clock:
                    return ref isClockMarked;
                case JokerKind.Rocket:
                    return ref isRocketMarked;
                case JokerKind.Broom:
                    return ref isBroomMarked;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, $"{(int)kind} is not a joker kind.");
            }
        }
    }
}
