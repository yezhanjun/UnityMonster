using kcp2k;
using Mirror.SimpleWeb;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MTPSKIT {

    public class DNTransportImplementer : MonoBehaviour
    {
        DNNetworkManager _manager;

        [Header("Select desired transport layer")]
        [SerializeField]Transports Transport;

        KcpTransport _t_kcp;
        SimpleWebTransport _t_simpleWebTransport;
        Mirror.Transport _sessionTransport;

        // Only switch transports while offline. Steam sessions do not use an IP port.
        public bool SelectSessionTransport(Mirror.Transport selected)
        {
            if (!selected || Mirror.NetworkClient.active || Mirror.NetworkServer.active) return false;
            _sessionTransport = selected;
            GetComponent<DNNetworkManager>().transport = selected;
            Mirror.Transport.active = selected;
            return true;
        }

        public bool RestoreLocalTransport()
        {
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active) return false;
            _sessionTransport = null;
            var selected = Transport == Transports.KCP
                ? (Mirror.Transport)GetComponent<KcpTransport>() : GetComponent<SimpleWebTransport>();
            if (!selected) return false;
            GetComponent<DNNetworkManager>().transport = selected;
            Mirror.Transport.active = selected;
            return true;
        }

        void OnDestroy()
        {
            if (_manager) _manager.Callback_OnNetworkTransportPortSet -= SetPort;
        }

        void Awake()
        {
            _manager = GetComponent<DNNetworkManager>();
            _manager.Callback_OnNetworkTransportPortSet += SetPort;

            _t_kcp = GetComponent<KcpTransport>();
            _t_simpleWebTransport = GetComponent<SimpleWebTransport>();

            switch (Transport)
            {
                case Transports.KCP:
                    if (!_t_kcp) { 
                        DisplayTransportMissingErrorMessgae(Transport);
                        return;
                    }

                    _manager.transport = _t_kcp;
                    break;

                case Transports.SimpleWebTransport:
                    if (!_t_simpleWebTransport) { 
                        DisplayTransportMissingErrorMessgae(Transport);
                        return;
                    }

                    _manager.transport = _t_simpleWebTransport;
                    break;
            }
        }

        void DisplayTransportMissingErrorMessgae(Transports transport)
        {
            Debug.LogError($"Transport {transport} is selected to be used, but it is not added alongside network manager. Please add this transport script to NetworkManager");
        }
        void SetPort(ushort port)
        {
            if (_sessionTransport || Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            switch (Transport) 
            {
                case Transports.KCP:
                    if (!_t_kcp)
                    {
                        DisplayTransportMissingErrorMessgae(Transport);
                        return;
                    }
                    _manager.GetComponent<KcpTransport>().port = port;
                    _manager.transport = _t_kcp;
                    break;

                case Transports.SimpleWebTransport:
                    if (!_t_simpleWebTransport) { 
                        DisplayTransportMissingErrorMessgae(Transport);
                        return;
                    }
                    _manager.GetComponent<SimpleWebTransport>().port = port;
                    _manager.transport = _t_simpleWebTransport;
                    break;
            }
        }

        public enum Transports
        {
            KCP,
            SimpleWebTransport,
        }
    }
}
