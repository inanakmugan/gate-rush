using GateRush.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GateRush.Editor
{
    /// <summary>
    /// Plays one level from the editor (Module 21): sets the one-shot
    /// <see cref="DevLevelOverride"/>, opens the Level scene — asking to save
    /// open scene changes as Unity does — and enters Play Mode. The scene file
    /// is never changed, so a plain Play afterwards starts where a build
    /// starts. The Level Editor's Play button and the Play Level window both
    /// go through here.
    /// </summary>
    /// <remarks>
    /// Holds no state. The override is set only once nothing can stop Play
    /// Mode from being entered, and whatever is left of it is dropped when
    /// Play Mode ends, so a request can never leak into a later plain Play.
    /// </remarks>
    public static class DevPlayLauncher
    {
        /// <summary>The one scene that plays levels.</summary>
        private const string LevelScenePath = "Assets/Scenes/Level.unity";

        private const string DialogTitle = "Play Level";

        /// <summary>
        /// Drops an override nobody took — the scene had no bootstrap to take
        /// it — as Play Mode ends. Hooked once per script load; unhooking
        /// first keeps it single however often this runs.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ClearOverrideWhenPlayEnds()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                DevLevelOverride.Clear();
            }
        }

        /// <summary>
        /// Starts Play Mode on the level file <paramref name="levelName"/>.
        /// Returns false, having said why in a dialog and set nothing, when
        /// Play Mode is already running or starting, scripts are compiling or
        /// do not compile, the Level scene is missing, or the owner cancels
        /// the save-changes prompt.
        /// </summary>
        public static bool Play(string levelName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return Refuse(
                    $"Play Mode is already running, so '{levelName}' was not started. Stop Play Mode first, " +
                    "or move between levels while playing with the level keys named in the Level Bootstrap's Level tooltip.");
            }

            // Play Mode would not be entered, and the override would wait for
            // the next plain Play.
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
            {
                return Refuse(
                    $"Scripts are compiling or have compile errors, so '{levelName}' was not started. " +
                    "Try again once they compile.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelScenePath) == null)
            {
                return Refuse($"There is no scene at {LevelScenePath}, so '{levelName}' was not started.");
            }

            // An already open Level scene is left as it is: reopening it would
            // discard unsaved changes, and Unity plays an unsaved scene anyway.
            if (!IsLevelSceneTheOnlyOpenScene())
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return false;
                }

                EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
            }

            DevLevelOverride.Set(levelName);
            EditorApplication.EnterPlaymode();
            return true;
        }

        private static bool IsLevelSceneTheOnlyOpenScene() =>
            SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == LevelScenePath;

        private static bool Refuse(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
