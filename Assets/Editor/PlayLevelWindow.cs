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
    /// error as a tooltip. Beside each, Edit opens the file in the Level
    /// Editor. The window decides nothing: <see cref="LevelRoster"/> reads the
    /// files as the game does, <see cref="PlayLevelList"/> lays the rows out,
    /// <see cref="DevPlayLauncher"/> plays, and <see cref="LevelEditorWindow"/>
    /// opens.
    /// </summary>
    public sealed class PlayLevelWindow : EditorWindow
    {
        /// <summary>How strongly a row or button that cannot be used is drawn, against one that can.</summary>
        private const float UnplayableRowAlpha = 0.5f;

        /// <summary>How wide a row's Edit button is, in pixels.</summary>
        private const float EditButtonWidth = 44f;

        private const string EditLabel = "Edit";

        private PlayLevelList list;

        /// <summary>
        /// Each level file's project path by its name, for Edit. Of two files
        /// with one name the first is kept, as <see cref="LevelRoster"/> keeps it.
        /// </summary>
        private Dictionary<string, string> pathsByName;

        /// <summary>
        /// The names more than one file has. A row is known by its name alone,
        /// so a row with one of these cannot say which file it stands for.
        /// </summary>
        private HashSet<string> sharedNames;

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
            pathsByName = new Dictionary<string, string>();
            sharedNames = new HashSet<string>();
            foreach (var asset in Resources.LoadAll<TextAsset>(LevelBootstrap.LevelsResourcePath))
            {
                files.Add((asset.name, asset.text));
                if (pathsByName.ContainsKey(asset.name))
                {
                    sharedNames.Add(asset.name);
                }
                else
                {
                    pathsByName.Add(asset.name, AssetDatabase.GetAssetPath(asset));
                }
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
            EditorGUILayout.BeginHorizontal();
            DrawPlay(row);
            DrawEdit(row);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlay(PlayLevelRow row)
        {
            if (!row.IsPlayable)
            {
                DrawUnavailable(new GUIContent(row.Label, row.Error), rowStyle);
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

        /// <summary>
        /// Edit opens the row's file in the Level Editor, whether or not it
        /// plays: a file that does not load is the one that needs editing. It
        /// is unavailable only where the name does not say which file is meant.
        /// </summary>
        private void DrawEdit(PlayLevelRow row)
        {
            var width = GUILayout.Width(EditButtonWidth);
            if (sharedNames.Contains(row.Name))
            {
                var why = $"Two level files are named '{row.Name}', so this row cannot say which one to edit. " +
                          "Open the one you mean from the Level Editor's Open menu.";
                DrawUnavailable(new GUIContent(EditLabel, why), GUI.skin.button, width);
                return;
            }

            if (!pathsByName.TryGetValue(row.Name, out var path))
            {
                DrawUnavailable(new GUIContent(EditLabel, $"No level file named '{row.Name}' was found."), GUI.skin.button, width);
                return;
            }

            if (GUILayout.Button(new GUIContent(EditLabel, $"Open {path} in the Level Editor."), width))
            {
                // After this GUI pass, as Play is: opening a window and asking
                // about unsaved changes do not belong inside one.
                EditorApplication.delayCall += () => LevelEditorWindow.OpenFile(path);
            }
        }

        /// <summary>
        /// A dimmed label rather than a disabled button, so the tooltip saying
        /// why shows on hover.
        /// </summary>
        private static void DrawUnavailable(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            var previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * UnplayableRowAlpha);
            GUILayout.Label(content, style, options);
            GUI.color = previous;
        }
    }
}
