using UnityEngine;
using UnityEngine.SceneManagement;

namespace MTPSKIT.Preloader
{
    public static class Preloader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Preload()
        {
            // The resource viewer deliberately runs without the multiplayer menu.
            if (SceneManager.GetActiveScene().path == "Assets/Monsters/Jotunnslayer/MonsterPreview/MonsterPreview.unity")
                return;
            // Imported asset preview scenes run independently of the multiplayer menu.
            if (BossPreview.EditorScenePaths.IsPreview(SceneManager.GetActiveScene().path))
                return;
            var index = SceneManager.GetActiveScene().buildIndex;

            if (index != 0) SceneManager.LoadScene(0);
        }
    }
}
