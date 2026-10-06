using System.Collections.Generic;
using GateRush.Runtime;
using UnityEditor;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// Gate Rush → Play Level… (Module 21): every level file the game would
    /// read, in the game's own order with the HUD's numbers; clicking one
    /// plays it. A file that does not load is listed greyed out with its
    /// error as a tooltip. The window decides nothing: <see cref="LevelRoster"/>
    /// reads the files as the game does, <see cref="PlayLevelList"/> lays the
    /// rows out, and <see cref="DevPlayLauncher"/> plays.
    /// </summary>
    public sealed class PlayLevelWindow : EditorWindow
    {
        /// <summary>How strongly a file that does not load is drawn, against a playable one.</summary>
        private const float UnplayableRowAlpha = 0.5f;

        private PlayLevelList list;
        private Vector2 scroll;
        private GUIStyle rowStyle;

        [MenuItem("Gate Rush/Play Level…")]
        public static void Open() => GetWindow<PlayLevelWindow>("Play Level");

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>Level files may have been saved from the Level Editor since the window last had focus.</summary>
        private void OnFocus()
        {
            Refresh();
        }

        private void OnProjectChange()
        {
            Refresh();
            Repaint();
        }

        /// <summary>
        /// Re-reads the level files through <c>Resources</c>, the way the
        /// bootstrap does, so the list is the game's own view of them. Called
        /// on enable, focus and project change, never per frame.
        /// </summary>
        private void Refresh()
        {
            var files = new List<(string name, string json)>();
            foreach (var asset in Resources.LoadAll<TextAsset>(LevelBootstrap.LevelsResourcePath))
            {
                files.Add((asset.name, asset.text));
            }

            list = PlayLevelList.Build(LevelRoster.Read(files));
        }

        private void OnGUI()
        {
            if (list == null)
            {
                Refresh();
            }

            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            }

            if (list.OrderError != null)
            {
                EditorGUILayout.HelpBox(
                    $"{list.OrderError} The levels have no order, so none has a number; each still plays.",
                    MessageType.Error);
            }

            var rows = list.Rows;
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"There are no level files in Resources/{LevelBootstrap.LevelsResourcePath}.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var i = 0; i < rows.Count; i++)
            {
                DrawRow(rows[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(PlayLevelRow row)
        {
            if (!row.IsPlayable)
            {
                // A dimmed label rather than a disabled button, so the tooltip
                // with the error shows on hover.
                var previous = GUI.color;
                GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * UnplayableRowAlpha);
                GUILayout.Label(new GUIContent(row.Label, row.Error), rowStyle);
                GUI.color = previous;
                return;
            }

            if (GUILayout.Button(row.Label, rowStyle))
            {
                // After this GUI pass: opening a scene and entering Play Mode
                // do not belong inside one.
                var levelName = row.Name;
                EditorApplication.delayCall += () => DevPlayLauncher.Play(levelName);
            }
        }
    }
}
