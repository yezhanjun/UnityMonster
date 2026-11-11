using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Threading;
using System.Collections.Generic;
using System;

#if !UNITY_WEBGL
using DNComClient;
#endif

namespace DNServerList
{
    public class ServerCommunicator : MonoBehaviour
    {
        public static ServerCommunicator Singleton { get; private set; }

        //This event will be launched when server list app will boot this unity build. We should
        //to this event method that will further setup and run the game so it will be able to be connected to
        public UnityEvent<ushort, string> ServeGame;
        public UnityEvent<bool> ClientUseWss;
        

        [SerializeField] Text _text;

        public static string AccesCode;

#if !UNITY_WEBGL
        bool _terminateIfLobbyIsEmpty  = false;
        int _currentPlayerCount;

        public static int unityThread;
        static public Queue<Action> runInUpdate = new Queue<Action>();

        Coroutine _c_checkIgGameIsEmpty;

        public void Awake()
        {
            unityThread = Thread.CurrentThread.ManagedThreadId;
        }

        private void Update()
        {
            while (runInUpdate.Count > 0)
            {
                Action action = null;
                lock (runInUpdate)
                {
                    if (runInUpdate.Count > 0)
                        action = runInUpdate.Dequeue();
                }
                action?.Invoke();
            }
        }

        protected async virtual void Start()
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                string[] command = args[i].Split('>');

                if (command[0] == "connect" && command.Length > 3)
                {
                    if (Singleton)
                    {
                        Destroy(this.gameObject);
                        return;
                    }

                    Singleton = this;
                    DontDestroyOnLoad(this.gameObject);

                    _text.text += $"Connecting to {command[1]}:{command[2]}";

                    DNComInterface.RegisterCommand("serve", Cmd_ServeGame);
                    DNComInterface.RegisterCommand("kill", Cmd_KillGame);
                    
                    DNComInterface.Init();
                    DNComInterface.DNComEvent_OnConnected += OnConnectedToMatchmakingSystem;
                    DNComInterface.DNComEvent_OnDisconnected += OnDisconnectedFromMatchmakingSystem;
                    DNComInterface.DNComEvent_CouldNotConnect += OnDisconnectedFromMatchmakingSystem;

                    //connect to the server list manager app
                    await DNComInterface.Connect(Convert.ToUInt16(command[1]), command[2], Convert.ToUInt16(command[3]));
                }

                if (command[0] == "terminatewhenempty" && command.Length > 1) 
                {
                    _terminateIfLobbyIsEmpty  = Convert.ToBoolean(command[1]);

                    if (_terminateIfLobbyIsEmpty )
                        _c_checkIgGameIsEmpty = StartCoroutine(Server_CheckIfGameIsEmpty());
                }

                if (command[0] == "setAccessCode" && command.Length > 1) 
                {
                    AccesCode = command[1];
                }

                if (command[0] == "clientusewss" && command.Length > 1)
                {
                    ClientUseWss?.Invoke(Convert.ToBoolean(command[1]));
                }
            }
        }

        void OnConnectedToMatchmakingSystem() 
        {
            RunOnUnityThread(()=>_text.text += "Connected to master");
        }
        void OnDisconnectedFromMatchmakingSystem()
        {
            RunOnUnityThread(() => Application.Quit());
        }

        ///This should be launched every single time someone joins or disconnects. Otherwise DNServerList may recognize
        ///game incorrectly as being empty and shutdown it
        public virtual void OnPlayerCountChanged(int currentPlayerCount, string lobbyProperties)
        {
            _currentPlayerCount = currentPlayerCount;

            if (_c_checkIgGameIsEmpty != null)
                StopCoroutine(_c_checkIgGameIsEmpty);

            if (currentPlayerCount <= 0 && _terminateIfLobbyIsEmpty)
                //Application.Quit();
                DNComInterface.Disconnect();

            DNComInterface.SendCommandToServer("updatelobbymetadata", lobbyProperties);
        }

        //Run this method when game is fully booted, so DNServer list will list this game and others will be able to
        //see it in list and join it
        public void OnGameReady(string gameProperties) 
        {
            DNComInterface.SendCommandToServer("gameready", gameProperties);
            _text.text += "SENDED READY INFO";
        }


        //if game running on server is empty (no players in game) then terminate it
        IEnumerator Server_CheckIfGameIsEmpty()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(20f);
                //close game if player count is 0
                if(_currentPlayerCount == 0)
                    Application.Quit();
            }
        }

        /// <summary>
        /// Kill game on DNServerList command
        /// </summary>
        protected void Cmd_KillGame(string[] args)
        {
            RunOnUnityThread(Action);

            void Action()
            {
                for (int i = 0; i < args.Length; i++)
                {
                    _text.text += $"{ args[i]}\n";
                }
            }
        }

        /// <summary>
        /// Boot server on DNServerList command
        /// </summary>
        void Cmd_ServeGame(string[] args) 
        {
            RunOnUnityThread(Action); //pass port and json with game settings
            void Action() 
            {
                _text.text += $"COMMANDED TO SERVE {args[1]}";
                ServeGame?.Invoke(System.Convert.ToUInt16(args[0]), args[1]);
            }
        }

        public static void RunOnUnityThread(Action action)
        {
            if (unityThread == Thread.CurrentThread.ManagedThreadId)
            {
                action();
            }
            else
            {
                lock (runInUpdate)
                {
                    runInUpdate.Enqueue(action);
                }
            }
        }
#endif
    }
}