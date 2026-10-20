namespace GateRush.Platform
{
    /// <summary>
    /// One row of a leaderboard (Module 39): its rank, from 1, one per row
    /// with no shared ranks, the player it shows, and whether that player is
    /// the one playing. Plain data with no checks of its own.
    /// </summary>
    public readonly struct LeaderboardEntry
    {
        /// <summary>The row ranked <paramref name="rank"/> for the player <paramref name="name"/>.</summary>
        public LeaderboardEntry(int rank, string name, string avatarId, int score, bool isPlayer)
        {
            Rank = rank;
            Name = name;
            AvatarId = avatarId;
            Score = score;
            IsPlayer = isPlayer;
        }

        /// <summary>The rank, 1 at the top.</summary>
        public int Rank { get; }

        /// <summary>The name shown on the row.</summary>
        public string Name { get; }

        /// <summary>The avatar's id, as the avatar catalog knows it.</summary>
        public string AvatarId { get; }

        /// <summary>The score shown on the row.</summary>
        public int Score { get; }

        /// <summary>True on the one row of the player playing, drawn in the highlight colour.</summary>
        public bool IsPlayer { get; }
    }
}
