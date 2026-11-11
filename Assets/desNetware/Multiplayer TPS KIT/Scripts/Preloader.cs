using UnityEngine;
using UnityEngine.SceneManagement;

namespace MTPSKIT.Preloader
{
    public static class Preloader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Preload()
        {
            var index = SceneManager.GetActiveScene().buildIndex;

            if (index != 0) SceneManager.LoadScene(0);
        }
    }
}
