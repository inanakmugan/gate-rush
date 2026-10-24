using System;
using System.Collections.Generic;
using GateRush.Meta;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The joker icons (Module 37): one sprite per <see cref="JokerKind"/>,
    /// the one place every screen that draws a joker reads it — the Store's
    /// cards, given theirs when the UI prefabs are built, and the win
    /// streak's bar, ladder and reward at runtime — so replacing the
    /// stand-ins of Module 36 changes them all at once. It lives in
    /// <c>Runtime</c> so the level's own interface can read it as well.
    /// </summary>
    [CreateAssetMenu(fileName = "JokerIcons", menuName = "Gate Rush/Joker Icons")]
    public sealed class JokerIcons : ScriptableObject
    {
        [Tooltip("The Clock's icon: Assets/Art/Sprites/UI/hourglass.png (Module 45).")]
        [SerializeField] private Sprite clockIcon;

        [Tooltip("The Rocket's icon: Assets/Art/Sprites/UI/rocket.png (Module 45).")]
        [SerializeField] private Sprite rocketIcon;

        [Tooltip("The Broom's icon: Assets/Art/Sprites/UI/broom.png (Module 45).")]
        [SerializeField] private Sprite broomIcon;

        /// <summary>The icon of <paramref name="kind"/>; null when it is not assigned, which <see cref="Problems"/> reports.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public Sprite IconOf(JokerKind kind)
        {
            switch (kind)
            {
                case JokerKind.Clock:
                    return clockIcon;
                case JokerKind.Rocket:
                    return rocketIcon;
                case JokerKind.Broom:
                    return broomIcon;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a joker kind.");
            }
        }

        /// <summary>Every icon left unassigned, each message naming its field. Empty when every joker has one.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            AddIfMissing(problems, clockIcon, "Clock Icon");
            AddIfMissing(problems, rocketIcon, "Rocket Icon");
            AddIfMissing(problems, broomIcon, "Broom Icon");
            return problems;
        }

        private void AddIfMissing(List<string> problems, Sprite icon, string field)
        {
            if (icon == null)
            {
                problems.Add($"{name}: {field} is not assigned.");
            }
        }
    }
}
