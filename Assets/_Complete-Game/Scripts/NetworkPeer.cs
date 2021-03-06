﻿using System.Collections;
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

        private Player.MovementDirection requestedMovement = Player.MovementDirection.none;

        public NetworkPeer(string name, int index, bool isLocal, bool isHost, TcpClient client, PeerState peerState)
        {
            peerName = name;
            peerIdx = index;
            local = isLocal;
            host = isHost;
            this.client = client;
            state = peerState;
        }

        public void Release()
        {
            if (client != null)
            {
                if (client.Connected)
                {
                    client.GetStream().Close();
                    client.GetStream().Dispose();
                }
                client.Close();
                client.Dispose();
                client = null;
            }
        }

        public string GetName()
        {
            return peerName;
        }

        public void SetName(string name)
        {
            peerName = name;
        }

        public bool IsLocal()
        {
            return local;
        }

        public bool IsHost()
        {
            return host;
        }

        public void SetState(PeerState state)
        {
            this.state = state;
        }

        public TcpClient GetClient()
        {
            return client;
        }

        public void SetClient(TcpClient client)
        {
            this.client = client;
        }

        public int GetPeerId()
        {
            return peerIdx;
        }

        public PeerState GetState()
        {
            return state;
        }

        public Player.MovementDirection GetRequestedMovement()
        {
            return requestedMovement;
        }

        public void SetRequestedMovement(Player.MovementDirection direction)
        {
            requestedMovement = direction;
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
