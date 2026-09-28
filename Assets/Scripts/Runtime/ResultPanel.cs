using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.Runtime
{
    /// <summary>
    /// The end-of-level panel: a title with Restart and, after a win, Next.
    /// Holds no rules — the bootstrap decides what to show and handles what
    /// the buttons ask for.
    /// </summary>
    /// <remarks>
    /// Put this component on an object that stays active (the canvas) and
    /// point <see cref="panelRoot"/> at the child that is shown and hidden.
    /// </remarks>
    public sealed class ResultPanel : MonoBehaviour
    {
        [Tooltip("The child shown for a result and hidden while the level is played.")]
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private TMP_Text title;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button nextButton;

        /// <summary>Raised when the player presses Restart.</summary>
        public event Action RestartRequested;

        /// <summary>Raised when the player presses Next.</summary>
        public event Action NextRequested;

        /// <summary>
        /// A message naming the references this panel needs when any is
        /// missing; empty when it is usable.
        /// </summary>
        public string MissingReferences()
        {
            if (panelRoot != null && title != null && restartButton != null && nextButton != null)
            {
                return string.Empty;
            }

            return $"{name}: ResultPanel needs Panel Root, Title, Restart Button and Next Button assigned.";
        }

        /// <summary>Shows the win result; Next only when there is a next level.</summary>
        public void ShowWin(string titleText, bool hasNext)
        {
            Show(titleText, hasNext);
        }

        /// <summary>Shows the loss result, with Restart only.</summary>
        public void ShowLoss(string titleText)
        {
            Show(titleText, false);
        }

        /// <summary>Hides the panel.</summary>
        public void Hide()
        {
            panelRoot.SetActive(false);
        }

        private void Show(string titleText, bool showsNext)
        {
            title.text = titleText;
            nextButton.gameObject.SetActive(showsNext);
            panelRoot.SetActive(true);
        }

        private void OnEnable()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(OnNextClicked);
            }
        }

        private void OnDisable()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(OnNextClicked);
            }
        }

        private void OnRestartClicked()
        {
            RestartRequested?.Invoke();
        }

        private void OnNextClicked()
        {
            NextRequested?.Invoke();
        }
    }
}
