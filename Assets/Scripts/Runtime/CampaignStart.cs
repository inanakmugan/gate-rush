namespace GateRush.Runtime
{
    /// <summary>
    /// What starting the campaign level does (D57, D58, Module 49), from Play
    /// or from Next after a campaign win: decided by
    /// <see cref="CampaignLevels.ChooseStart"/>, carried out by the bootstrap.
    /// </summary>
    public enum CampaignStart
    {
        /// <summary>The campaign level starts; starting spends nothing.</summary>
        Level,

        /// <summary>No life is left: the Lives overlay opens and nothing starts (D57).</summary>
        NoLife,

        /// <summary>The campaign is finished: the End of Content overlay opens and nothing starts, lives or none (Module 49).</summary>
        Over
    }
}
