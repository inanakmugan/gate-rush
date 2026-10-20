namespace GateRush.Platform
{
    /// <summary>
    /// One player as a leaderboard ranks them (Module 39, D73): a name, an
    /// avatar id and a score — the player's own lifetime gold (D22), or a demo
    /// player's from configuration. Plain data with no checks of its own; the
    /// service checks what it is given.
    /// </summary>
    public readonly struct LeaderboardPlayer
    {
        /// <summary>The player <paramref name="name"/>, shown with <paramref name="avatarId"/>, scoring <paramref name="score"/>.</summary>
        public LeaderboardPlayer(string name, string avatarId, int score)
        {
            Name = name;
            AvatarId = avatarId;
            Score = score;
        }

        /// <summary>The name shown on the row.</summary>
        public string Name { get; }

        /// <summary>The avatar's id, as the avatar catalog knows it.</summary>
        public string AvatarId { get; }

        /// <summary>What the ranking orders by: lifetime gold, never bought gold (D22, D72).</summary>
        public int Score { get; }
    }
}
