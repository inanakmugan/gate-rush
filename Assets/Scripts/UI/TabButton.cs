using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One tab of the tab bar (Module 35): drawn raised with its name while
    /// selected, and as its icon alone otherwise — the reference game's tab
    /// bar. Which tab it is, is the <see cref="TabBar"/> field it sits in.
    /// </summary>
    public sealed class TabButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("Shown while this tab is selected: the raised plate with the icon and the tab's name.")]
        [SerializeField] private GameObject selectedLook;

        [Tooltip("Shown while another tab is selected: the icon alone.")]
        [SerializeField] private GameObject idleLook;

        /// <summary>Raised when the tab is pressed, selected or not.</summary>
        public event Action Pressed;

        /// <summary>Shows the selected look when <paramref name="isSelected"/>, the idle look otherwise.</summary>
        public void ShowSelected(bool isSelected)
        {
            selectedLook.SetActive(isSelected);
            idleLook.SetActive(!isSelected);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>, named after this tab's object.</summary>
        public void AddMissing(List<string> missing)
        {
            var owner = $"TabButton on '{name}'";
            UiParts.AddIfMissing(missing, button, owner, nameof(button));
            UiParts.AddIfMissing(missing, selectedLook, owner, nameof(selectedLook));
            UiParts.AddIfMissing(missing, idleLook, owner, nameof(idleLook));
        }

        private void OnEnable()
        {
            UiParts.Listen(button, OnClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(button, OnClicked);
        }

        private void OnClicked() => Pressed?.Invoke();
    }
}
