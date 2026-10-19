using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// The three tabs at the bottom, Store, Home and Leaderboard (Module 35).
    /// Each field decides which tab its button is, so a button can never
    /// stand for the wrong tab.
    /// </summary>
    public sealed class TabBar : MonoBehaviour
    {
        [SerializeField] private TabButton storeTab;
        [SerializeField] private TabButton homeTab;
        [SerializeField] private TabButton leaderboardTab;

        /// <summary>Raised with the tab whose button was pressed.</summary>
        public event Action<UiTab> TabSelected;

        /// <summary>Draws <paramref name="tab"/> raised and the others as icons.</summary>
        public void ShowSelected(UiTab tab)
        {
            storeTab.ShowSelected(tab == UiTab.Store);
            homeTab.ShowSelected(tab == UiTab.Home);
            leaderboardTab.ShowSelected(tab == UiTab.Leaderboard);
        }

        /// <summary>Adds each unassigned part, the tabs' own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            AddTabMissing(missing, storeTab, nameof(storeTab));
            AddTabMissing(missing, homeTab, nameof(homeTab));
            AddTabMissing(missing, leaderboardTab, nameof(leaderboardTab));
        }

        private static void AddTabMissing(List<string> missing, TabButton tab, string field)
        {
            UiParts.AddIfMissing(missing, tab, nameof(TabBar), field);
            if (tab != null)
            {
                tab.AddMissing(missing);
            }
        }

        private void OnEnable()
        {
            if (storeTab != null)
            {
                storeTab.Pressed += OnStorePressed;
            }

            if (homeTab != null)
            {
                homeTab.Pressed += OnHomePressed;
            }

            if (leaderboardTab != null)
            {
                leaderboardTab.Pressed += OnLeaderboardPressed;
            }
        }

        private void OnDisable()
        {
            // Unsubscribing from a C# event of a destroyed component is safe;
            // only an unassigned field is skipped.
            if (!ReferenceEquals(storeTab, null))
            {
                storeTab.Pressed -= OnStorePressed;
            }

            if (!ReferenceEquals(homeTab, null))
            {
                homeTab.Pressed -= OnHomePressed;
            }

            if (!ReferenceEquals(leaderboardTab, null))
            {
                leaderboardTab.Pressed -= OnLeaderboardPressed;
            }
        }

        private void OnStorePressed() => TabSelected?.Invoke(UiTab.Store);

        private void OnHomePressed() => TabSelected?.Invoke(UiTab.Home);

        private void OnLeaderboardPressed() => TabSelected?.Invoke(UiTab.Leaderboard);
    }
}
