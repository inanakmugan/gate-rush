using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// Gives one graphic its theme colour from <see cref="UiConfig"/> (D69):
    /// the white icons, the background and the plain highlight and ring;
    /// never a sprite drawn in its own colours (Module 45). Put it beside the
    /// Image it tints; <see cref="FrontEnd"/> applies every one under it when
    /// it opens, and the level grid applies those of the buttons it creates.
    /// </summary>
    public sealed class UiTint : MonoBehaviour
    {
        [Tooltip("Which UI Config colour the graphic takes.")]
        [SerializeField] private UiColorRole role;

        [Tooltip("The graphic to tint; filled with the Image beside this component when it is added.")]
        [SerializeField] private Graphic target;

        /// <summary>Tints the target with <paramref name="config"/>'s colour for its role. Does nothing without a target.</summary>
        public void Apply(UiConfig config)
        {
            if (target != null)
            {
                target.color = config.ColorOf(role);
            }
        }

        /// <summary>
        /// Adds this tint's path to <paramref name="missing"/> when it has no
        /// target, and when its role is no longer declared — a coin, heart or
        /// badge tint left from before Module 45, which would throw on apply.
        /// </summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, target, $"UiTint on '{name}'", nameof(target));
            if (!Enum.IsDefined(typeof(UiColorRole), role))
            {
                missing.Add($"UiTint on '{name}' has the role {(int)role}, which is no longer a colour role; remove the tint");
            }
        }

        /// <summary>Applies every tint under <paramref name="root"/>, inactive ones included.</summary>
        public static void ApplyAll(Component root, UiConfig config)
        {
            var tints = root.GetComponentsInChildren<UiTint>(true);
            for (var i = 0; i < tints.Length; i++)
            {
                tints[i].Apply(config);
            }
        }

        private void Reset()
        {
            target = GetComponent<Graphic>();
        }
    }
}
