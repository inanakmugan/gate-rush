// The web build's vibration (Module 25): WebHaptics calls this through
// DllImport("__Internal"). Browsers without the Vibration API — every iOS
// browser — do nothing; so does a browser that refuses the call.
mergeInto(LibraryManager.library, {
    GateRushVibrate: function (milliseconds) {
        if (typeof navigator === 'undefined' || typeof navigator.vibrate !== 'function') {
            return;
        }

        try {
            navigator.vibrate(milliseconds);
        } catch (e) {
            // A refused vibration is not an error the game can act on.
        }
    }
});
