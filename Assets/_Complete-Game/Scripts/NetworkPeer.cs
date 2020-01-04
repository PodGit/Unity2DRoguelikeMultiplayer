using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Completed
{
    public class NetworkPeer
    {
        public enum PeerState
        {
            Uninitialised,
            Joining,
            Joined
        }

        string peerName = "";
        int peerIdx = -1;
        bool local = false;
        bool host = false;
        TcpClient client = null;
        PeerState state = PeerState.Uninitialised;

        public NetworkPeer(string name, int index, bool isLocal, bool isHost, TcpClient client, PeerState peerState)
        {
            peerName = name;
            peerIdx = index;
            local = isLocal;
            host = isHost;
            this.client = client;
            state = peerState;
        }

        public string GetName()
        {
            return peerName;
        }

        public bool IsLocal()
        {
            return local;
        }

        public TcpClient GetClient()
        {
            return client;
        }

        public int GetPeerId()
        {
            return peerIdx;
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
