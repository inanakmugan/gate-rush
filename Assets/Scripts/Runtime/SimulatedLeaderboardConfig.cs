using System;
using System.Collections.Generic;
using GateRush.Platform;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The demo players of the simulated leaderboard (Module 39, D73): each
    /// an invented handle, an avatar id from the <see cref="AvatarCatalog"/>
    /// and a lifetime gold score. Their scores spread from 40 to 980, so the
    /// campaign's gold climbs most of the table but not to the top. A real
    /// backend would replace this asset along with
    /// <see cref="SimulatedLeaderboardService"/>.
    /// </summary>
    /// <remarks>
    /// A missing config, or one with <see cref="Problems"/>, stops the
    /// bootstrap, as the other configs do. An avatar id the catalog lacks is
    /// only logged (<see cref="MissingAvatars"/>): the row shows the
    /// catalog's first avatar.
    /// </remarks>
    [CreateAssetMenu(fileName = "SimulatedLeaderboardConfig", menuName = "Gate Rush/Simulated Leaderboard Config")]
    public sealed class SimulatedLeaderboardConfig : ScriptableObject
    {
        [Tooltip("The demo players the player is ranked among, in any order; the screen labels them as demo players. Names are invented handles, never a real person's or brand's name, and must pass the player name rule (1 to 16 letters, digits, spaces, - _ or .). Avatar Id is an id of the Avatar Catalog; Score is lifetime gold, 0 or above. At least one player.")]
        [SerializeField] private DemoPlayerEntry[] players =
        {
            new DemoPlayerEntry("GateKeeper", "rhino", 980),
            new DemoPlayerEntry("Yıldız Avcısı", "giraffe", 940),
            new DemoPlayerEntry("PuzzleFox", "monkey", 890),
            new DemoPlayerEntry("Kayıp Anahtar", "elephant", 845),
            new DemoPlayerEntry("BlockMason", "gorilla", 800),
            new DemoPlayerEntry("Mavi Kedi", "penguin", 760),
            new DemoPlayerEntry("SlideQueen", "parrot", 705),
            new DemoPlayerEntry("Hızlı Kaplumbağa", "sloth", 650),
            new DemoPlayerEntry("NightOwl_7", "owl", 600),
            new DemoPlayerEntry("Kırmızı Balon", "pig", 555),
            new DemoPlayerEntry("TileHopper", "rabbit", 500),
            new DemoPlayerEntry("Gece Kuşu", "chick", 450),
            new DemoPlayerEntry("Bamboo Muncher", "panda", 410),
            new DemoPlayerEntry("Kod Ustası", "frog", 365),
            new DemoPlayerEntry("CornerTurner", "hippo", 320),
            new DemoPlayerEntry("Bulmaca Kurdu", "snake", 270),
            new DemoPlayerEntry("QuietMoose", "moose", 215),
            new DemoPlayerEntry("Tembel Fok", "walrus", 160),
            new DemoPlayerEntry("PaddleDuck", "duck", 105),
            new DemoPlayerEntry("Yavaş Adım", "cow", 40),
        };

        /// <summary>
        /// Everything wrong with this asset, each message naming its field;
        /// empty when <see cref="Players"/> can be ranked.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            foreach (var problem in SimulatedLeaderboardService.ProblemsOf(Players()))
            {
                problems.Add($"{name}: Players {problem}");
            }

            return problems;
        }

        /// <summary>The demo players, in the asset's order; a new array on each call.</summary>
        public LeaderboardPlayer[] Players()
        {
            var entries = players ?? Array.Empty<DemoPlayerEntry>();
            var result = new LeaderboardPlayer[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                result[i] = entries[i]?.ToPlayer() ?? default;
            }

            return result;
        }

        /// <summary>
        /// One message for each demo player whose avatar id
        /// <paramref name="catalog"/> does not have, naming the avatar shown
        /// instead; empty when every id is in it. The bootstrap logs them at
        /// start, as it does mismatched gold packs.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        public IReadOnlyList<string> MissingAvatars(AvatarCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var missing = new List<string>();
            var all = Players();
            for (var i = 0; i < all.Length; i++)
            {
                var id = all[i].AvatarId;
                if (!catalog.Contains(id))
                {
                    missing.Add(
                        $"{name}: Players element {i}: Avatar Id '{id}' is not in {catalog.name}; " +
                        $"the row shows '{catalog.ShownId(id)}' instead.");
                }
            }

            return missing;
        }

        /// <summary>One demo player, as the Inspector edits it.</summary>
        [Serializable]
        private sealed class DemoPlayerEntry
        {
            [SerializeField] private string name;
            [SerializeField] private string avatarId;
            [SerializeField] private int score;

            public DemoPlayerEntry(string name, string avatarId, int score)
            {
                this.name = name;
                this.avatarId = avatarId;
                this.score = score;
            }

            public LeaderboardPlayer ToPlayer() => new LeaderboardPlayer(name, avatarId, score);
        }
    }
}
