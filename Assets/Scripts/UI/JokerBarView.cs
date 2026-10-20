using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using UnityEngine;

namespace GateRush.UI
{
    /// <summary>
    /// The level's joker bar in the bottom band (Module 40): the Clock, the
    /// Rocket and the Broom, left to right. Shows each button of the
    /// <see cref="LevelUiState"/> it is given and which joker is selected,
    /// and raises <see cref="JokerPressed"/>; it holds no rules.
    /// </summary>
    public sealed class JokerBarView : MonoBehaviour
    {
        [SerializeField] private JokerBarButton clock;
        [SerializeField] private JokerBarButton rocket;
        [SerializeField] private JokerBarButton broom;

        private bool isSubscribed;

        /// <summary>Raised when a joker's button is pressed, with its kind.</summary>
        public event Action<JokerKind> JokerPressed;

        /// <summary>Shows every button of <paramref name="state"/>, the selected one raised.</summary>
        public void Show(LevelUiState state, UiConfig config)
        {
            ShowButton(clock, JokerKind.Clock, state, config);
            ShowButton(rocket, JokerKind.Rocket, state, config);
            ShowButton(broom, JokerKind.Broom, state, config);
        }

        /// <summary>Adds each unassigned part, the buttons' own included, to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, clock, nameof(JokerBarView), nameof(clock));
            UiParts.AddIfMissing(missing, rocket, nameof(JokerBarView), nameof(rocket));
            UiParts.AddIfMissing(missing, broom, nameof(JokerBarView), nameof(broom));
            foreach (var button in new[] { clock, rocket, broom })
            {
                if (button != null)
                {
                    button.AddMissing(missing);
                }
            }
        }

        private static void ShowButton(JokerBarButton view, JokerKind kind, LevelUiState state, UiConfig config)
        {
            view.Show(state.Button(kind), state.SelectedJoker == kind, config);
        }

        private void OnEnable()
        {
            if (isSubscribed || clock == null || rocket == null || broom == null)
            {
                return;
            }

            clock.Pressed += OnClockPressed;
            rocket.Pressed += OnRocketPressed;
            broom.Pressed += OnBroomPressed;
            isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!isSubscribed)
            {
                return;
            }

            // Unsubscribing from a C# event of a destroyed button is safe, so
            // this runs during teardown as well.
            clock.Pressed -= OnClockPressed;
            rocket.Pressed -= OnRocketPressed;
            broom.Pressed -= OnBroomPressed;
            isSubscribed = false;
        }

        private void OnClockPressed() => JokerPressed?.Invoke(JokerKind.Clock);

        private void OnRocketPressed() => JokerPressed?.Invoke(JokerKind.Rocket);

        private void OnBroomPressed() => JokerPressed?.Invoke(JokerKind.Broom);
    }
}
