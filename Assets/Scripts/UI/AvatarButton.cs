using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// One avatar in Profile's grid (Module 38): its sprite, and a mark when
    /// it is the one shown as chosen. Created by <see cref="ProfileOverlay"/>
    /// from a prefab; pressing it only asks.
    /// </summary>
    public sealed class AvatarButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("The avatar's image; also the button's target graphic.")]
        [SerializeField] private Image icon;

        [Tooltip("Shown only on the avatar shown as chosen.")]
        [SerializeField] private GameObject selectedMark;

        /// <summary>Raised with this button when it is pressed.</summary>
        public event Action<AvatarButton> Pressed;

        /// <summary>Shows <paramref name="sprite"/>, marked when <paramref name="isSelected"/>.</summary>
        public void Show(Sprite sprite, bool isSelected)
        {
            icon.sprite = sprite;
            selectedMark.SetActive(isSelected);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, button, nameof(AvatarButton), nameof(button));
            UiParts.AddIfMissing(missing, icon, nameof(AvatarButton), nameof(icon));
            UiParts.AddIfMissing(missing, selectedMark, nameof(AvatarButton), nameof(selectedMark));
        }

        private void OnEnable()
        {
            UiParts.Listen(button, OnClicked);
        }

        private void OnDisable()
        {
            UiParts.Unlisten(button, OnClicked);
        }

        private void OnClicked() => Pressed?.Invoke(this);
    }
}
