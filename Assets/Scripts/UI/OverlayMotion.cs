using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// The one open and close every overlay shares (Module 47), in the front
    /// end and in a level: it opens with the level panels' pop
    /// (<see cref="OverlayPop"/>) and closes with a short reverse, the panel
    /// shrinking a little while the overlay fades. On each overlay's root, with
    /// the root's CanvasGroup and the panel, written by the UI builder.
    /// </summary>
    /// <remarks>
    /// <para><b>At once and after.</b> What is open changes at once — the
    /// navigation, <see cref="IsOpen"/> — and only the look follows. A closing
    /// overlay takes no input from the start of its close (its CanvasGroup
    /// stops blocking raycasts) and is deactivated when the close ends.
    /// Opening it again during its close stops the close and pops it at once;
    /// another overlay opened meanwhile is never delayed, since each has its
    /// own motion.</para>
    /// <para>Its tweens run on unscaled time and carry this component as
    /// their id. A disable kills them and leaves the overlay at its open look;
    /// one disabled mid-close stays active but closed, and the next
    /// <see cref="ShowAt"/> deactivates it — an object is never switched off
    /// from inside its own disable.</para>
    /// </remarks>
    public sealed class OverlayMotion : MonoBehaviour
    {
        // The open look: opaque, full size. Not tunables.
        private const float OpenAlpha = 1f;
        private const float ClosedAlpha = 0f;

        [Tooltip("On the overlay's root: the pop fades it in, the close fades it out and stops its raycasts.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("The overlay's panel: the pop grows it, the close shrinks it a little.")]
        [SerializeField] private RectTransform panel;

        private bool isClosing;

        /// <summary>True while the overlay is active and not closing: open, or opening.</summary>
        public bool IsOpen => gameObject.activeSelf && !isClosing;

        /// <summary>True from the start of a close until it ends or is stopped.</summary>
        public bool IsClosing => isClosing;

        /// <summary>Activates the overlay, taking input, and plays the pop; one closing stops its close first.</summary>
        public void Open(UiConfig config)
        {
            isClosing = false;
            gameObject.SetActive(true);
            group.blocksRaycasts = true;
            OverlayPop.Play(this, group, panel, config);
        }

        /// <summary>
        /// Closes the overlay: at once it takes no input and counts as closed;
        /// it fades while its panel shrinks to
        /// <see cref="UiConfig.OverlayCloseScale"/>, then is deactivated and
        /// left at its open look. Does nothing unless open.
        /// </summary>
        public void Close(UiConfig config)
        {
            if (!IsOpen)
            {
                return;
            }

            isClosing = true;
            group.blocksRaycasts = false;
            DOTween.Kill(this);
            DOTween.To(() => group.alpha, alpha => group.alpha = alpha, ClosedAlpha, config.OverlayCloseSeconds)
                .SetEase(config.OverlayCloseEase)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(EndClose);
            panel.DOScale(config.OverlayCloseScale, config.OverlayCloseSeconds)
                .SetEase(config.OverlayCloseEase)
                .SetUpdate(true)
                .SetId(this);
        }

        /// <summary>Shows the overlay open or closed at once, at its open look, stopping any pop or close.</summary>
        public void ShowAt(bool isOpen)
        {
            Rest();
            gameObject.SetActive(isOpen);
        }

        /// <summary>
        /// Opens or closes <paramref name="motion"/>'s overlay, animated or at
        /// once; one already as it should be is left alone, so a pop or a close
        /// in progress runs on. The one rule the front end and the level's
        /// interface share.
        /// </summary>
        public static void Apply(OverlayMotion motion, bool isOpen, bool isAnimated, UiConfig config)
        {
            if (!isAnimated)
            {
                motion.ShowAt(isOpen);
            }
            else if (isOpen && !motion.IsOpen)
            {
                motion.Open(config);
            }
            else if (!isOpen && motion.IsOpen)
            {
                motion.Close(config);
            }
        }

        /// <summary>
        /// Adds to <paramref name="missing"/> an <paramref name="overlay"/> with
        /// no motion on its root, or the motion's own unassigned parts. An
        /// unassigned overlay is skipped: its owner reports it.
        /// </summary>
        public static void AddMissingOn(List<string> missing, Component overlay, string field)
        {
            if (overlay == null)
            {
                return;
            }

            var motion = overlay.GetComponent<OverlayMotion>();
            if (motion == null)
            {
                missing.Add($"an OverlayMotion on the root of '{field}'");
                return;
            }

            motion.AddMissing(missing);
        }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>, named after this overlay's object.</summary>
        public void AddMissing(List<string> missing)
        {
            var owner = $"OverlayMotion on '{name}'";
            UiParts.AddIfMissing(missing, group, owner, nameof(group));
            UiParts.AddIfMissing(missing, panel, owner, nameof(panel));
        }

        private void EndClose()
        {
            Rest();
            gameObject.SetActive(false);
        }

        /// <summary>Stops the pop or the close and leaves the open look, taking input. Either part may be missing during teardown.</summary>
        private void Rest()
        {
            isClosing = false;
            OverlayPop.Rest(this, group, panel);
            if (group != null)
            {
                group.alpha = OpenAlpha;
                group.blocksRaycasts = true;
            }
        }

        private void OnDisable()
        {
            // A close stopped here leaves the overlay active; it stays counted
            // as closed until ShowAt or Open.
            var wasClosing = isClosing;
            Rest();
            isClosing = wasClosing;
            if (wasClosing && group != null)
            {
                group.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }
    }
}
