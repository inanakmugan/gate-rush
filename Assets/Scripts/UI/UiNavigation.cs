using System;
using System.Collections.Generic;

namespace GateRush.UI
{
    /// <summary>
    /// Where the front end is (Module 35): one of three tabs, and a stack of
    /// overlays over it, newest last. Plain code with no scene, so every rule
    /// of moving between them is tested on its own.
    /// </summary>
    /// <remarks>
    /// <para>Selecting a tab closes every overlay. Each overlay is in the
    /// stack at most once: opening one already in it brings it to the top.
    /// Back closes the top overlay; with none, Store and Leaderboard return
    /// to Home, and Home stays.</para>
    /// <para><see cref="Changed"/> is raised once for each call that changed
    /// the tab or the stack, and never for one that changed nothing.</para>
    /// </remarks>
    public sealed class UiNavigation
    {
        private readonly List<UiOverlay> overlays = new List<UiOverlay>();

        /// <summary>Home, with no overlay.</summary>
        public UiNavigation()
        {
            Overlays = overlays.AsReadOnly();
        }

        /// <summary>Raised once after any call that changed <see cref="Tab"/> or <see cref="Overlays"/>.</summary>
        public event Action Changed;

        /// <summary>The selected tab; Home at start.</summary>
        public UiTab Tab { get; private set; } = UiTab.Home;

        /// <summary>The open overlays, oldest first: the last is on top.</summary>
        public IReadOnlyList<UiOverlay> Overlays { get; }

        /// <summary>Selects <paramref name="tab"/> and closes every overlay.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tab"/> is not a declared tab.</exception>
        public void SelectTab(UiTab tab)
        {
            RequireDeclared(tab);
            if (tab == Tab && overlays.Count == 0)
            {
                return;
            }

            Tab = tab;
            overlays.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// Opens <paramref name="overlay"/> on top of the stack. Already on
        /// top, nothing changes; lower in the stack, it is moved to the top.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="overlay"/> is not a declared overlay.</exception>
        public void Open(UiOverlay overlay)
        {
            RequireDeclared(overlay);
            if (overlays.Count > 0 && overlays[overlays.Count - 1] == overlay)
            {
                return;
            }

            overlays.Remove(overlay);
            overlays.Add(overlay);
            Changed?.Invoke();
        }

        /// <summary>
        /// Closes the top overlay; with none open, returns from Store or
        /// Leaderboard to Home. False, with nothing changed, on Home with no
        /// overlay.
        /// </summary>
        public bool Back()
        {
            if (overlays.Count > 0)
            {
                overlays.RemoveAt(overlays.Count - 1);
            }
            else if (Tab != UiTab.Home)
            {
                Tab = UiTab.Home;
            }
            else
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Goes back to Home with no overlay, as at start.</summary>
        public void Reset()
        {
            SelectTab(UiTab.Home);
        }

        private static void RequireDeclared(UiTab tab)
        {
            if (!Enum.IsDefined(typeof(UiTab), tab))
            {
                throw new ArgumentOutOfRangeException(nameof(tab), tab, "Not a front end tab.");
            }
        }

        private static void RequireDeclared(UiOverlay overlay)
        {
            if (!Enum.IsDefined(typeof(UiOverlay), overlay))
            {
                throw new ArgumentOutOfRangeException(nameof(overlay), overlay, "Not a front end overlay.");
            }
        }
    }
}
