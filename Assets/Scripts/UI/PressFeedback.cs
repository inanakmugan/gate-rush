using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// A button's answer to a press (Module 47): its face shrinks to
    /// <see cref="UiConfig.PressScale"/> while pressed and springs back with a
    /// small overshoot on release or when the pointer leaves. On every
    /// <see cref="Button"/> of the interface, on the button's own object, whose
    /// rect is the visible face in every button the interface has, so the icon
    /// and the label move with it.
    /// </summary>
    /// <remarks>
    /// <para>It only scales: the button's own transition (a colour tint, a
    /// sprite swap or none) and its click are untouched, and a layout group
    /// above never reflows, since layout reads sizes, not scale. A press on a
    /// button that is not interactable does nothing.</para>
    /// <para><b>The hit area.</b> The face is also what a tap hits; shrunk, a
    /// tap on its outer edge would be released outside it and lose its click.
    /// While pressed, the button's target graphic gets a raycast padding that
    /// keeps its hit area at least its resting size (<see cref="HitPadding"/>);
    /// raycasts alone read it, so it rebuilds nothing.</para>
    /// <para>No Update: a tween runs only while the face shrinks or returns.
    /// It runs on unscaled time and carries this component as its id; a
    /// disable and a destroy kill it and leave the face at rest.</para>
    /// </remarks>
    [RequireComponent(typeof(Button))]
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        // The face's resting scale: not a tunable.
        private const float RestScale = 1f;

        [Tooltip("Assets/Config/UiConfig.asset: the press's scale and timing. Written by the UI builder or Apply Hand-Made Prefab Settings.")]
        [SerializeField] private UiConfig config;

        private Button button;
        private Graphic face;
        private Vector4 restPadding;
        private bool isPressed;

        /// <summary>True from a press on an interactable button until its release or the pointer leaving.</summary>
        public bool IsPressed => isPressed;

        /// <summary>The configuration the press reads; null until written.</summary>
        public UiConfig Config => config;

        /// <summary>
        /// The raycast padding that keeps the hit area of a face of
        /// <paramref name="size"/>, shrunk to <paramref name="scale"/> about its
        /// centre, at its resting size: negative, so it widens the area, and
        /// zero at a scale of 1. Padding is in the face's own units, which the
        /// scale shrinks too.
        /// </summary>
        public static Vector4 HitPadding(Vector2 size, float scale)
        {
            if (!(scale > 0f) || scale >= RestScale)
            {
                return Vector4.zero;
            }

            var grow = (RestScale / scale - RestScale) / 2f;
            var x = -size.x * grow;
            var y = -size.y * grow;
            return new Vector4(x, y, x, y);
        }

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || config == null || !Button.IsInteractable())
            {
                return;
            }

            isPressed = true;
            var scale = config.PressScale;
            if (Face != null)
            {
                Face.raycastPadding = restPadding + HitPadding(Face.rectTransform.rect.size, scale);
            }

            DOTween.Kill(this);
            transform.DOScale(scale, config.PressSeconds)
                .SetEase(config.PressEase)
                .SetUpdate(true)
                .SetId(this);
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        /// <inheritdoc />
        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private Button Button
        {
            get
            {
                if (button == null)
                {
                    button = GetComponent<Button>();
                }

                return button;
            }
        }

        // The graphic a tap hits — the button's target graphic, or the face's
        // own — read once with its own padding, which the press adds to and
        // the rest restores. It sits inside the scaled face, so it shrinks
        // with it.
        private Graphic Face
        {
            get
            {
                if (face == null)
                {
                    face = Button.targetGraphic != null ? Button.targetGraphic : GetComponent<Graphic>();
                    if (face != null)
                    {
                        restPadding = face.raycastPadding;
                    }
                }

                return face;
            }
        }

        /// <summary>Springs the face back, if pressed, and gives back its resting hit area.</summary>
        private void Release()
        {
            if (!isPressed)
            {
                return;
            }

            isPressed = false;
            RestorePadding();
            DOTween.Kill(this);
            transform.DOScale(RestScale, config.ReleaseSeconds)
                .SetEase(config.ReleaseEase, config.ReleaseOvershoot)
                .SetUpdate(true)
                .SetId(this);
        }

        private void RestorePadding()
        {
            if (face != null)
            {
                face.raycastPadding = restPadding;
            }
        }

        /// <summary>Kills the tween and leaves the face at rest, unpressed, with its resting hit area.</summary>
        private void Rest()
        {
            DOTween.Kill(this);
            isPressed = false;
            RestorePadding();
            transform.localScale = Vector3.one * RestScale;
        }

        private void OnDisable()
        {
            Rest();
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }
    }
}
