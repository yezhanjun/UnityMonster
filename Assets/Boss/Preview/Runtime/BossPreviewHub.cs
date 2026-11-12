using CyberSamurai;
using DarklordPreview;
using UnityEngine;

namespace BossPreview
{
    // A single active character owns animation, cloth and camera input at a time.
    [DefaultExecutionOrder(-20000)]
    public sealed class BossPreviewHub : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Character
        {
            public string displayName;
            public GameObject root;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation = Quaternion.identity;
            public SamuraiKatanaPreview katana;
            public DarklordActionPreview swordShield;
        }

        public Camera previewCamera;
        public Character[] characters;
        public int currentCharacter;
        GUIContent[] characterButtons;
        Rect SwitcherRect => new Rect(Mathf.Max(340, Screen.width - 300), 16, 284, 70);

        void Awake()
        {
            if (characters == null || characters.Length == 0 || !previewCamera)
            {
                enabled = false;
                return;
            }
            characterButtons = new GUIContent[characters.Length];
            for (int i = 0; i < characters.Length; i++) characterButtons[i] = new GUIContent(characters[i].displayName);
            int initial = Mathf.Clamp(currentCharacter, 0, characters.Length - 1);
            currentCharacter = -1;
            UpdateInputBlocks();
            SelectCharacter(initial);
        }

        public void SelectCharacter(int index)
        {
            if (characters == null || index < 0 || index >= characters.Length || index == currentCharacter) return;
            for (int i = 0; i < characters.Length; i++) characters[i].root.SetActive(false);
            currentCharacter = index;
            var selected = characters[index];
            previewCamera.transform.SetPositionAndRotation(selected.cameraPosition, selected.cameraRotation);
            selected.root.SetActive(true);
            if (selected.katana && selected.katana.bootstrap.Ready)
                foreach (var cloth in selected.katana.bootstrap.cloths) if (cloth) cloth.ResetCloth();
        }

        void UpdateInputBlocks()
        {
            foreach (var character in characters)
            {
                if (character.katana) character.katana.additionalInputBlock = SwitcherRect;
                if (character.swordShield) character.swordShield.additionalInputBlock = SwitcherRect;
            }
        }

        void Update() { UpdateInputBlocks(); }

        void OnGUI()
        {
            if (characterButtons == null) return;
            GUILayout.BeginArea(SwitcherRect, GUI.skin.box);
            GUILayout.Label("Boss skill preview / Character");
            int selected = GUILayout.Toolbar(currentCharacter, characterButtons);
            if (selected != currentCharacter) SelectCharacter(selected);
            GUILayout.EndArea();
        }
    }
}
