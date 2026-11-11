using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using System;
using UnityEngine.UI;
using TMPro;

namespace MTPSKIT.UI
{
    public class ChatUI : MonoBehaviour
    {
        public static ChatUI _instance;
        public GameObject chatUI;
        public TMP_Text _chatText;
        [SerializeField]private FocusedInputField _inputField;

        public bool ChatWriting;
        private void Start()
        {
            _chatText.text = string.Empty;
            _inputField.gameObject.SetActive(false);
            if (_instance != null) 
            {
                Destroy(_instance);
            }
            _instance = this;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Y) && !ClientFrontend.Pause && ClientFrontend.GamePlayInput() && !ChatWriting) ShowChatInput(ChatWriting = !ChatWriting);


            if (Input.GetKeyDown(KeyCode.Escape) && ChatWriting) ShowChatInput(false);


            if (Input.GetKeyDown(KeyCode.Return) && ChatWriting)
            {
                Send();
                ShowChatInput(false);
            }
        }
        public void ShowChatInput(bool show)
        {
            UIPauseMenu._instance.AbleToPause = !show;
            ChatWriting = show;
            ShowChat(show);

        }
        public void Send()
        {
            if (string.IsNullOrWhiteSpace(MessageGet())) { return; }
            GameManager.myPlayerInstance.GetComponent<ChatBehaviour>().CmdRelayClientMessage(GameTools.CheckMessageLength(ChatUI._instance.MessageGet()));
        }



        public void WriteMessageToChat(string _msg) //adding message to all other messages and deleting additional lines
        {
            string content = _chatText.text + $"\n { _msg}";
            int extraLines = GameTools.GetLineCount(content) - 8;
            if (extraLines > 0)
            {
                content = GameTools.DeleteLines(content, extraLines);
            }
            _chatText.text = content;
            StopAllCoroutines();
            StartVanishingChat();
        }
        public float chatVanishingSpeed = 2f;
        public void ShowChat(bool show)
        {

            ClientFrontend.ShowCursor(show);

            _inputField.gameObject.SetActive(show);
            StopAllCoroutines();
            if (show)
            {
                _inputField.Select();
                _inputField.FocusInputField();
                _chatText.color = Color.white;
            }
            else
            {
                _inputField.text = string.Empty;
            }
        }
        public void StartVanishingChat() 
        {
            StartCoroutine(VanishChat());
        }
        IEnumerator VanishChat() 
        {
            _chatText.color = Color.white;
            yield return new WaitForSeconds(5f);
            while (_chatText.color.a > 0.01f) 
            {
                _chatText.color = Color.LerpUnclamped(_chatText.color, Color.clear, chatVanishingSpeed * Time.deltaTime);
                yield return null;
            }
            _chatText.color = Color.clear;
            _chatText.text = string.Empty;
        }
        public string MessageGet() 
        {
            return _inputField.text;
        }
    }
}
