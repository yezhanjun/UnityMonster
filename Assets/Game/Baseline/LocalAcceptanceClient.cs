using System;
using System.Collections;
using UnityEngine;

namespace MTPSKIT
{
    // Two independent local-host sandboxes; these are not a network sync acceptance pair.
    public sealed class LocalAcceptanceClient : MonoBehaviour
    {
        public static string Lane { get; } = ReadLane();
        public static bool Requested => Debug.isDebugBuild && Lane != null;
        static string ReadLane()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--acceptance-lane");
            if (index < 0 || index + 1 >= args.Length) return null;
            string lane = args[index + 1];
            return lane == "jotunn" || lane == "rain" ? lane : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Requested) return;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            var host = new GameObject("Independent acceptance client - " + Lane);
            DontDestroyOnLoad(host);
            host.AddComponent<LocalAcceptanceClient>();
        }

        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            RoomCreator creator = null;
            while (!creator && Time.realtimeSinceStartup < deadline)
            {
                creator = FindFirstObjectByType<RoomCreator>();
                yield return null;
            }
            if (!creator) { Debug.LogError("ACCEPTANCE_BOOT_FAILED RoomCreator missing"); yield break; }
            int map = Array.FindIndex(creator.Maps, entry => entry && (Lane == "jotunn"
                ? RoomCreator.IsTrainingScene(entry.Scene) : RoomCreator.IsRainScene(entry.Scene)));
            if (map < 0) { Debug.LogError("ACCEPTANCE_BOOT_FAILED map missing for " + Lane); yield break; }
            ushort port = Lane == "jotunn" ? (ushort)17941 : (ushort)17942;
            var args = Environment.GetCommandLineArgs();
            int portIndex = Array.IndexOf(args,"--acceptance-port");
            if (portIndex >= 0 && portIndex + 1 < args.Length && ushort.TryParse(args[portIndex+1],out var selectedPort)) port=selectedPort;
            DNNetworkManager.Instance.SetTransportPort(port);
            if (Array.IndexOf(args,"--acceptance-join") >= 0)
            {
                DNNetworkManager.Instance.GetComponent<DNTransportImplementer>().RestoreLocalTransport();
                if (Array.IndexOf(args, "--review-network") >= 0)
                {
                    var simulationObject = new GameObject("Acceptance latency simulation");
                    simulationObject.SetActive(false); DontDestroyOnLoad(simulationObject);
                    var simulation = simulationObject.AddComponent<Mirror.LatencySimulation>();
                    simulation.wrap = Mirror.Transport.active; simulation.latency = 200f;
                    simulation.jitter = simulation.unreliableLoss = simulation.unreliableScramble = 0f;
                    DNNetworkManager.Instance.transport = simulation; Mirror.Transport.active = simulation;
                    simulationObject.SetActive(true);
                }
                DNNetworkManager.Instance.networkAddress="127.0.0.1";
                DNNetworkManager.Instance.StartClient();
            }
            else creator.LaunchFromFrontEnd(map, Array.IndexOf(args, "--wendy-rifle-smoke") >= 0 ||
                Array.IndexOf(args, "--four-way-roll-smoke") >= 0 || Array.IndexOf(args, "--four-way-jump-smoke") >= 0 ||
                Array.IndexOf(args, "--wendy-firearms-smoke") >= 0 || Array.IndexOf(args, "--hero-motion-diagnostics") >= 0 || Array.IndexOf(args, "--run-stop-smoke") >= 0 || Array.IndexOf(args, "--root-motion-smoke") >= 0 || Array.IndexOf(args, "--full-jump-smoke") >= 0 || Array.IndexOf(args, "--sprint-smoke") >= 0 || Array.IndexOf(args, "--review-smoke") >= 0 || Array.IndexOf(args, "--review-network") >= 0 ?
                RangeCharacterSelection.FirstPortable : 0, false);
            deadline = Time.realtimeSinceStartup + 60f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter)
            { Debug.LogError("ACCEPTANCE_BOOT_FAILED player missing"); yield break; }
            Debug.Log("ACCEPTANCE_CLIENT_READY lane=" + Lane + " port=" + port + " scene=" + creator.Maps[map].Scene);
        }

        void OnGUI()
        {
            var previous = GUI.color;
            GUI.color = Lane == "jotunn" ? new Color(.65f, .9f, 1f) : new Color(.75f, 1f, .6f);
            GUI.Box(new Rect(Screen.width * .5f - 200f, 8, 400, 26),
                Lane == "jotunn" ? "JOTUNN ACCEPTANCE | local host :17941 | = GM" : "RAIN ACCEPTANCE | local host :17942 | = GM");
            GUI.color = previous;
        }
    }
}
