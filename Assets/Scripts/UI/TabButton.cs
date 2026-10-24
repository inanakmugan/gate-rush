using System;
using System.Collections.Generic;
using DG.Tweening;
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
        // The plate's resting scale: not a tunable.
        private const float RestScale = 1f;

        [SerializeField] private Button button;

        [Tooltip("Shown while this tab is selected: the raised plate with the icon and the tab's name.")]
        [SerializeField] private GameObject selectedLook;

        [Tooltip("Shown while another tab is selected: the icon alone.")]
        [SerializeField] private GameObject idleLook;

        /// <summary>Raised when the tab is pressed, selected or not.</summary>
        public event Action Pressed;

        /// <summary>
        /// Shows the selected look when <paramref name="isSelected"/>, the idle
        /// look otherwise. With <paramref name="pops"/>, the selected look —
        /// the raised plate with the icon and the name — grows to
        /// <see cref="UiConfig.TabPopScale"/> and back once (Module 47), on
        /// unscaled time, carrying this tab as its id; a disable or a destroy
        /// stops it at rest. The pop scales the plate, not the tab's own
        /// object, which a press scales.
        /// </summary>
        public void ShowSelected(bool isSelected, bool pops, UiConfig config)
        {
            RestPop();
            selectedLook.SetActive(isSelected);
            idleLook.SetActive(!isSelected);
            if (!isSelected || !pops)
            {
                return;
            }

            var plate = selectedLook.transform;
            var grow = config.TabPopScale - RestScale;
            DOVirtual.Float(0f, 1f, config.TabPopSeconds, t =>
                {
                    var scale = RestScale + grow * Mathf.Sin(Mathf.PI * t);
                    plate.localScale = new Vector3(scale, scale, RestScale);
                })
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetId(this);
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
            RestPop();
            UiParts.Unlisten(button, OnClicked);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        /// <summary>Stops the pop and leaves the plate at rest. The plate may be missing during teardown.</summary>
        private void RestPop()
        {
            DOTween.Kill(this);
            if (selectedLook != null)
            {
                selectedLook.transform.localScale = Vector3.one * RestScale;
            }
        }

        private void OnClicked() => Pressed?.Invoke();
    }
}
