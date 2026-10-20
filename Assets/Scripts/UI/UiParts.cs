using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// What every front end view does with its serialized parts: report the
    /// ones left unassigned, listen to its buttons null-safely — a button
    /// may already be destroyed when the scene is torn down and its view is
    /// disabled — and create and destroy the instances it makes from a
    /// prefab.
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

        /// <summary>Adds the missing target of every tint under <paramref name="root"/>, inactive ones included, to <paramref name="missing"/>.</summary>
        public static void AddMissingTints(List<string> missing, Component root)
        {
            var tints = root.GetComponentsInChildren<UiTint>(true);
            for (var i = 0; i < tints.Length; i++)
            {
                tints[i].AddMissing(missing);
            }
        }

        /// <summary>
        /// Destroys every instance in <paramref name="shown"/> and creates
        /// <paramref name="count"/> new ones from <paramref name="prefab"/>
        /// under <paramref name="parent"/>, each with its tints applied. Only
        /// for instances that raise no events, so there is nothing to
        /// unsubscribe from.
        /// </summary>
        public static void Respawn<T>(List<T> shown, int count, T prefab, Transform parent, UiConfig config) where T : Component
        {
            DestroyAll(shown);
            for (var i = 0; i < count; i++)
            {
                var instance = Object.Instantiate(prefab, parent);
                UiTint.ApplyAll(instance, config);
                shown.Add(instance);
            }
        }

        /// <summary>Destroys every instance in <paramref name="shown"/> that still exists, and empties it.</summary>
        public static void DestroyAll<T>(List<T> shown) where T : Component
        {
            for (var i = 0; i < shown.Count; i++)
            {
                if (shown[i] != null)
                {
                    Object.Destroy(shown[i].gameObject);
                }
            }

            shown.Clear();
        }
    }
}
