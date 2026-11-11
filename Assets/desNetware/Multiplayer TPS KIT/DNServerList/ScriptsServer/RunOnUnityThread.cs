using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DNServerList
{

    public class RunOnUnityThread : MonoBehaviour
    {
        public static int unityThread;
        static public Queue<Action> runInUpdate = new Queue<Action>();

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

        public static void RUNOnUnityThread(Action action)
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
    }
}