namespace GateRush.Platform
{
    /// <summary>
    /// Vibration feedback (Module 25). The one seam between the moments the
    /// game vibrates at and how a platform does it: the browser's Vibration
    /// API in a web build, nothing where there is none. Nothing above this
    /// interface knows which.
    /// </summary>
    public interface IHaptics
    {
        /// <summary>
        /// Vibrates once for <paramref name="milliseconds"/>. Does nothing
        /// where the platform cannot vibrate, and for a length that is not
        /// positive; it never throws.
        /// </summary>
        void Pulse(int milliseconds);
    }
}
