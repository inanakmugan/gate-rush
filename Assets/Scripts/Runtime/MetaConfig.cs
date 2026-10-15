using System;
using System.Collections.Generic;
using GateRush.Meta;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Every tunable number of the meta game (Module 30, D59): the life cap,
    /// how long a life takes to come back, and a new player's gold. Nothing in
    /// <c>GateRush.Meta</c> hardcodes one of these; <see cref="ToRules"/> hands
    /// them over as <see cref="MetaRules"/>.
    /// </summary>
    /// <remarks>
    /// The save cannot be read without these rules — a version-1 save takes
    /// its gold and lives from them (D60) — so a missing config, or one with
    /// <see cref="Problems"/>, stops the bootstrap.
    /// </remarks>
    [CreateAssetMenu(fileName = "MetaConfig", menuName = "Gate Rush/Meta Config")]
    public sealed class MetaConfig : ScriptableObject
    {
        [Tooltip("The most lives a player holds; lives come back one at a time up to it. At least 1. A saved count above a lowered cap reads as the cap.")]
        [SerializeField] private int lifeCap = 5;

        [Tooltip("Minutes for one life to come back, counted on real-world UTC time, also while the game is closed. Positive.")]
        [SerializeField] private int lifeRegenMinutes = 20;

        [Tooltip("The gold a new player starts with, and what a save from before Module 30 gets. It is not earned gold, so it is not in the lifetime gold the leaderboard ranks on. At least 0.")]
        [SerializeField] private int startingGold = 500;

        /// <summary>
        /// Everything wrong with this asset, each message naming its field;
        /// empty when <see cref="ToRules"/> can be called.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();

            if (lifeCap < 1)
            {
                problems.Add($"{name}: Life Cap must be at least 1.");
            }

            if (lifeRegenMinutes <= 0)
            {
                problems.Add($"{name}: Life Regen Minutes must be positive.");
            }

            if (startingGold < 0)
            {
                problems.Add($"{name}: Starting Gold may not be negative.");
            }

            return problems;
        }

        /// <summary>
        /// The rules these values describe. Call only when
        /// <see cref="Problems"/> is empty.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A value <see cref="Problems"/> reports.</exception>
        public MetaRules ToRules() => new MetaRules(lifeCap, TimeSpan.FromMinutes(lifeRegenMinutes), startingGold);
    }
}
