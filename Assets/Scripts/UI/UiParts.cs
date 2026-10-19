using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// What every front end view does with its serialized parts: report the
    /// ones left unassigned, and listen to its buttons null-safely — a button
    /// may already be destroyed when the scene is torn down and its view is
    /// disabled.
    /// </summary>
    internal static class UiParts
    {
        /// <summary>
        /// Adds "<paramref name="owner"/>.<paramref name="field"/>" to
        /// <paramref name="missing"/> when <paramref name="part"/> is not
        /// assigned, by Unity's null (a destroyed object counts as missing).
        /// </summary>
        public static void AddIfMissing(List<string> missing, UnityEngine.Object part, string owner, string field)
        {
            if (part == null)
            {
                missing.Add($"{owner}.{field}");
            }
        }

        /// <summary>Adds <paramref name="listener"/> to <paramref name="button"/>'s click, when there is a button.</summary>
        public static void Listen(Button button, UnityAction listener)
        {
            if (button != null)
            {
                button.onClick.AddListener(listener);
            }
        }

        /// <summary>Removes <paramref name="listener"/> from <paramref name="button"/>'s click, when the button still exists.</summary>
        public static void Unlisten(Button button, UnityAction listener)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(listener);
            }
        }
    }
}
