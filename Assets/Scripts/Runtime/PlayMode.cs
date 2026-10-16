namespace GateRush.Runtime
{
    /// <summary>
    /// The two ways a level is played (D58). Decided where the level starts
    /// and kept with the run, so a restart plays the same way.
    /// </summary>
    public enum PlayMode
    {
        /// <summary>
        /// Started from Play or from Next after a campaign win: an attempt
        /// needs a life, a loss costs one, and a win pays the level's gold
        /// and advances the campaign (D57, D63).
        /// </summary>
        Campaign,

        /// <summary>
        /// Started from the level select, from Next after a free-play win, or
        /// by a development tool: needs no life, costs none and pays nothing.
        /// </summary>
        FreePlay
    }
}
