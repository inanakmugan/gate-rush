using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// The one way <see cref="LevelBootstrap"/> shows and hears the front end
    /// (Module 35, D68): Home, its tabs and the overlays over them. Declared
    /// here and implemented in <c>GateRush.UI</c>, so the dependency points
    /// one way — <c>Runtime</c> never references the screens.
    /// </summary>
    /// <remarks>
    /// The front end decides nothing: it shows the <see cref="FrontEndState"/>
    /// it is given and raises an event for every request; the bootstrap
    /// decides what follows. Moving between tabs and overlays is its own
    /// business.
    /// </remarks>
    public interface IFrontEnd
    {
        /// <summary>Raised when the player presses Play: the campaign (D58).</summary>
        event Action PlayRequested;

        /// <summary>Raised when the player presses a level in the level grid, with its file name: free play (D58).</summary>
        event Action<string> LevelRequested;

        /// <summary>Raised when the player presses the sound toggle; the bootstrap flips the setting and refreshes.</summary>
        event Action SoundToggleRequested;

        /// <summary>
        /// Raised when the player presses the refill button, which is only
        /// pressable while <see cref="FrontEndState.CanRefill"/> (D62).
        /// </summary>
        event Action RefillRequested;

        /// <summary>
        /// Raised when the player presses any button of the front end, or Back
        /// changed something, after the press's own event — so sound turned
        /// on by the press is on before the press sounds (Module 25).
        /// </summary>
        event Action ButtonPressed;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>.</summary>
        bool IsOpen { get; }

        /// <summary>
        /// Null when every part the front end needs is assigned and its
        /// configuration has no problem; otherwise one message naming each
        /// missing part and each problem.
        /// </summary>
        string MissingSetup();

        /// <summary>Opens the front end on the Home tab with no overlay and no notice, showing <paramref name="state"/>.</summary>
        void Show(FrontEndState state);

        /// <summary>Shows <paramref name="state"/> in place, on whatever tab and overlays are showing.</summary>
        void Refresh(FrontEndState state);

        /// <summary>Opens the Lives overlay over the current tab.</summary>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowLives();

        /// <summary>
        /// Shows <paramref name="text"/> in the line under Home's play button,
        /// selecting Home first. It stays until the tab changes or the front
        /// end is shown again.
        /// </summary>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowNotice(string text);

        /// <summary>Hides the front end.</summary>
        void Hide();
    }
}
