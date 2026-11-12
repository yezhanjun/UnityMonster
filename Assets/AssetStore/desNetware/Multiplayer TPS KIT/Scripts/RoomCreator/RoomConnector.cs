using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{
    public class RoomConnector : MonoBehaviour
    {
        [Tooltip("Drag and drop here input field for typing address")]
        public TMPro.TMP_InputField Address;

        [Tooltip("Drag and drop here input field for typing port")]
        public TMPro.TMP_InputField Port;

        [Tooltip("Drag and drop here button for connecting to the existing game")]
        public Button ConnectBtn;

        private void Start()
        {
            Address.onValueChanged.AddListener(OnSetValues);
            Port.onValueChanged.AddListener(OnSetValues);
            ConnectBtn.onClick.AddListener(Btn_Connect);

            OnSetValues();
        }
        public void OnSetValues(string s = "")
        {
            Port.text = Port.text.Replace("-", string.Empty);

            if (Port.text == string.Empty) return;

            if (!int.TryParse(Port.text, out int port)) return;
            port = Mathf.Clamp(port, 0, ushort.MaxValue);

            Port.text = port.ToString();

            DNNetworkManager.Instance.SetAddressAndPort(Address.text, System.Convert.ToUInt16(Port.text));
        }
        public void Btn_Connect()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (SteamRoomSession.Instance && SteamRoomSession.Instance.Busy) return;
#endif
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            if (!DNNetworkManager.Instance.GetComponent<DNTransportImplementer>().RestoreLocalTransport()) return;
            OnSetValues();
            DNNetworkManager.Instance.StartClient();
        }

    }
}
