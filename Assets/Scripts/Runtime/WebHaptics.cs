using GateRush.Platform;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace GateRush.Runtime
{
    /// <summary>
    /// The <see cref="IHaptics"/> of the web build (Module 25):
    /// <c>navigator.vibrate</c>, reached through
    /// <c>Assets/Plugins/WebGL/Haptics.jslib</c>. In the Editor and in every
    /// build that is not a web build it does nothing; so does a browser with
    /// no Vibration API, which the script checks itself. iOS browsers have
    /// none, so an iPhone does not vibrate until a native build (Phase 8).
    /// </summary>
    public sealed class WebHaptics : IHaptics
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GateRushVibrate(int milliseconds);
#endif

        /// <inheritdoc />
        public void Pulse(int milliseconds)
        {
            if (milliseconds <= 0)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            GateRushVibrate(milliseconds);
#endif
        }
    }
}
