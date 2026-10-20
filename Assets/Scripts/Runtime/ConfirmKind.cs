namespace GateRush.Runtime
{
    /// <summary>
    /// What the confirmation before a lost life asks about (Module 32, D57):
    /// the bootstrap opens it with one, and does that once the player
    /// answers "Lose a life".
    /// </summary>
    public enum ConfirmKind
    {
        /// <summary>Leave the level for Home.</summary>
        Leave,

        /// <summary>Restart the level, when a life is left for it.</summary>
        Restart
    }
}
