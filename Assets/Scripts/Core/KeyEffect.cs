namespace GateRush.Core
{
    /// <summary>
    /// What happens to a lock's target block when its key is consumed (M8).
    /// </summary>
    public enum KeyEffect
    {
        UnlockMovement,
        ClearOuterColor
    }
}
