using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftRush.Core
{
    /// <summary>
    /// The mouse pointer is hidden and held in the window whenever a race or free roam scene is up, and shown again
    /// in the main menu. The pause menu shows it while paused and puts this back when the race resumes.
    /// Everything in a race (results included) works from the keyboard or a pad.
    /// </summary>
    public static class CursorKeeper
    {
        const string MenuScene = "MainMenu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => { if (m == LoadSceneMode.Single) Apply(s); };
            Apply(SceneManager.GetActiveScene());
        }

        static void Apply(Scene scene)
        {
            bool menu = scene.name == MenuScene;
#if UNITY_EDITOR
            // In the editor, keep the pointer free so the editor stays usable; just hide it over the game view.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = menu;
#else
            Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menu;
#endif
        }
    }
}
