using UnityEngine.SceneManagement;
using UnityEngine;
using MTPSKIT.Gameplay;

namespace MTPSKIT.UI {
    public class ClientInterfaceManager : MonoBehaviour
    {
        public GameObject PauseMenuUI;
        public GameObject ChatUI;
        public GameObject ScoreboardUI;
        public GameObject KillfeedUI;
        public GameObject PlayerHudUI;
        public GameObject DeathPanel;
        public GameObject GameplayCamera;
        public GameObject GamemodeMsg;
        public GameObject DamagePopup;
        //these colors are here because we may want to adjust them easily in the inspector
        public UIColorSet UIColorSet;
        

        public GameObject ThirdPersonCamera;
        private ThirdPersonCamera _spawnedTPPCamera;

        public static ClientInterfaceManager Instance;

        public void Start()
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);

            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else 
            {
                //if this happens it means that player returns to hub scene with Client Manager from previous hub scene load, so we dont
                //need another one, so destroy this one
                
                //this ClientManager spawning method is done like this to avoid using loading prefabs from Resources folder, in order to not complicate
                //this package more
                Destroy(gameObject);
                return;
            }

            ClientFrontend.ShowCursor(true);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            var index = SceneManager.GetActiveScene().buildIndex;

            ClientFrontend.Hub = (index == 0);

            ClientFrontend.ClearCursorBlockers();

            ClientFrontend.ShowCursor(index == 0);
            ClientFrontend.ThisClientTeam = -1;
            
            //if we loaded non-hub scene, then spawn all the UI prefabs for player, then on disconnecting they will
            //be destroyed by scene unloading
            if (index != 0)
            {
                Instantiate(PauseMenuUI);
                Instantiate(ChatUI);
                Instantiate(ScoreboardUI);
                Instantiate(KillfeedUI);
                Instantiate(PlayerHudUI);
                
                Instantiate(GamemodeMsg);
                Instantiate(DamagePopup);
                Instantiate(DeathPanel);
            }
        }

        public void AssignTPPCamera(CharacterInstance characterInstance) 
        {
            if (_spawnedTPPCamera == null)
                _spawnedTPPCamera = Instantiate(ThirdPersonCamera).GetComponent<ThirdPersonCamera>();

            _spawnedTPPCamera.SetThirdPersonCameraFor(characterInstance);
        }

        public void TPPCameraAssignCharacterToLookAt(Health h) 
        {
            if (_spawnedTPPCamera == null)
                _spawnedTPPCamera = Instantiate(ThirdPersonCamera).GetComponent<ThirdPersonCamera>();

            _spawnedTPPCamera.FollowObject(h);
        }
    }
}
