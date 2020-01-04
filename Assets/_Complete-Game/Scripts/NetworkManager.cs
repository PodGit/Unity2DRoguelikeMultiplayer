//#define USE_RAW_SOCKETS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Completed
{
    public delegate void PeerCallBack(NetworkPeer peer);

    public class NetworkManager : ScriptableObject
    {
        enum NetState
        {
            Invalid = -1,
            Uninitialised,
            Hosting,
            Joining,
            ActiveHost,
            ActiveClient,
            Error
        }

        private const int serverPort = 12000;
        private const int clientPort = 13000;
        private const int bufferSize = 256;
        private const int maxNumConnectionsToListenFor = 1;

        private static NetState netState = NetState.Uninitialised;
        private static NetworkManager mInstance;


        private TcpListener serverListener = null;
        private TcpClient joiningTcpClient = null;
        private byte[][] readBuffers;

        // Peers
        private static int numPeers = 0;
        private static NetworkPeer[] peers = null;
        private static List<PeerCallBack> peerAddedCallbacks;
        private static List<PeerCallBack> peerRemovedCallbacks;

        public static NetworkManager Instance
        {
            get
            {
                if (mInstance == null)
                {
                    //GameObject go = new GameObject();
                    //mInstance = go.AddComponent<NetworkManager>();
                    mInstance = ScriptableObject.CreateInstance<NetworkManager>();
                }
                return mInstance;
            }
        }

        public NetworkManager()
        {
            readBuffers = new byte[GameManager.MaxNumPlayers][];
            for (int bufferIndex = 0; bufferIndex < GameManager.MaxNumPlayers; ++bufferIndex)
            {
                readBuffers[bufferIndex] = new byte[bufferSize];
            }

            if (peers == null)
            {
                peers = new NetworkPeer[GameManager.MaxNumPlayers];
            }

            peerAddedCallbacks = new List<PeerCallBack>();
            peerRemovedCallbacks = new List<PeerCallBack>();
        }

        // Start is called before the first frame update
        void Start()
        {
           
        }

        // Update is called once per frame
        void Update()
        {
            for (int peerdIdx = 0; peerdIdx < numPeers; ++peerdIdx)
            {
                NetworkPeer peer = GetPeer(peerdIdx);
                if (!peer.IsLocal())
                {
                    TcpClient client = peer.GetClient();
                    NetworkStream stream = client.GetStream();
                }
            }
        }

        public bool IsActive()
        {
            return (netState == NetState.ActiveClient || netState == NetState.ActiveHost);
        }

        public int GetNumPeers()
        {
            return numPeers;
        }

        public NetworkPeer AddPeer(string playerName, bool isLocal, bool isHost, TcpClient client, NetworkPeer.PeerState peerState)
        {
            NetworkPeer peer = null;

            if (numPeers < GameManager.MaxNumPlayers)
            {
                peers[numPeers] = new NetworkPeer(playerName, numPeers, isLocal, isHost, client, peerState);
                peer = peers[numPeers];
                numPeers++;

                peerAddedCallbacks.ForEach( x=> { x.Invoke(peer); });
            }
            else
            {
                Debug.Log("Trying to add too many network peers");
            }

            return peer;
        }

        public NetworkPeer GetPeer(int peerIdx)
        {
            Debug.Assert(peerIdx >= 0 /*&& peerIdx < NetworkManager.Instance.numPeers*/, "GetPeer invalid index specified:" + peerIdx);

            return peers[peerIdx];
        }

        public void Host(string hostPlayerName)
        {
            if (netState == NetState.Uninitialised)
            {
                netState = NetState.Hosting;
                AddPeer(hostPlayerName, true, true, null, NetworkPeer.PeerState.Joined);

                ServerCreateSocket();
            }
        }

        void ServerCreateSocket()
        {
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");

            serverListener = new TcpListener(localAddr, serverPort);
            serverListener.Start();

            netState = NetState.ActiveHost;

            Debug.Log("Waiting for connection...");
            serverListener.BeginAcceptTcpClient(new AsyncCallback(ServerAcceptCallback), serverListener);
        }

        void ServerAcceptCallback(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            TcpClient connectedClient = listener.EndAcceptTcpClient(ar);

            Debug.Log("Accepted new connection!");
            NetworkPeer newPeer = AddPeer("Joining...", false, false, connectedClient, NetworkPeer.PeerState.Joining);
            connectedClient.GetStream().BeginRead(readBuffers[newPeer.GetPeerId()], 0, bufferSize, new AsyncCallback(ServerReadCallback), connectedClient);

            if (numPeers < GameManager.MaxNumPlayers)
            {
                Debug.Log("Waiting for connection...");
                listener.BeginAcceptTcpClient(new AsyncCallback(ServerAcceptCallback), listener);
            }
            else
            {
                listener.Stop();
            }
        }

        void ServerReadCallback(IAsyncResult ar)
        {
            TcpClient client = (TcpClient)ar.AsyncState;
            int size = client.GetStream().EndRead(ar);

            if (size > 0)
            {
                OnPacketRecieved(client, size);
            }

            NetworkPeer peer = GetPeerForTcpClient(client);

            if (peer != null)
            {
                client.GetStream().BeginRead(readBuffers[peer.GetPeerId()], 0, bufferSize, new AsyncCallback(ServerReadCallback), client);
            }
        }

        public void Join(string ipAddress)
        {
            if (netState == NetState.Uninitialised)
            {
                ClientCreateSocket(ipAddress);
            }
        }

        void ClientCreateSocket(string ipAddress)
        {
            Debug.Assert(joiningTcpClient == null, "Client socket already instantiated");

            IPAddress localIpAddress = IPAddress.Parse(ipAddress);
            IPEndPoint localEndPoint = new IPEndPoint(localIpAddress, clientPort);

            joiningTcpClient = new TcpClient(localEndPoint);

            Debug.Log("Attempting to join " + ipAddress + "...");

            IPAddress remoteIpAddress = IPAddress.Parse(ipAddress);
            IAsyncResult result = joiningTcpClient.BeginConnect(remoteIpAddress, serverPort, new AsyncCallback(ClientConnectCallback), joiningTcpClient);
            netState = NetState.Joining;
        }

        void ClientConnectCallback(IAsyncResult ar)
        {
            TcpClient client = (TcpClient)ar.AsyncState;
            client.EndConnect(ar);

            if (client.Connected)
            {
                Debug.Log("Received connection");
                NetworkPeer hostPeer = AddPeer("Host...", false, true, client, NetworkPeer.PeerState.Joined);
                netState = NetState.ActiveClient;

                client.GetStream().BeginRead(readBuffers[hostPeer.GetPeerId()], 0, bufferSize, new AsyncCallback(ClientReadCallback), client);
            }
            else
            {
                Debug.Log("Connection failed");
                ClientConnectionFailed();
            }

            joiningTcpClient = null;
        }

        void ClientReadCallback(IAsyncResult ar)
        {
            TcpClient client = (TcpClient)ar.AsyncState;
            int size = client.GetStream().EndRead(ar);

            if (size > 0)
            {
                OnPacketRecieved(client, size);
            }
        }

        void ClientConnectionFailed()
        {
            Debug.Assert(netState == NetState.Joining, "Not in joining state");
            netState = NetState.Uninitialised;

            joiningTcpClient.Close();
            joiningTcpClient.Dispose();
            joiningTcpClient = null;
        }

        NetworkPeer GetPeerForTcpClient(TcpClient client)
        {
            for (int peerIdx = 0; peerIdx < GameManager.MaxNumPlayers; ++peerIdx)
            {
                NetworkPeer peer = GetPeer(peerIdx);

                if (peer.GetClient() == client)
                {
                    return peer;
                }
            }

            return null;
        }

        void OnPacketRecieved(TcpClient fromClient, int size)
        {
            NetworkPeer peer = GetPeerForTcpClient(fromClient);

            if (peer != null)
            {
                int peerIdx = peer.GetPeerId();
                byte[] buffer = readBuffers[peerIdx];
                Debug.Log("Packet received from peer:" + peerIdx);
            }
        }

        public void RegisterPeerAddedCallback(PeerCallBack cb)
        {
            // Check cb isn't already registered
            peerAddedCallbacks.ForEach(x => { if (x == cb) return; });
            peerAddedCallbacks.Add(cb);
        }

        public void UnregisterPeerAddedCallback(PeerCallBack cb)
        {
            peerAddedCallbacks.ForEach(x => { if (x == cb) peerAddedCallbacks.Remove(x); });
        }

        public void RegisterPeerRemovedCallback(PeerCallBack cb)
        {
            peerRemovedCallbacks.ForEach(x => { if (x == cb) return; });
            peerRemovedCallbacks.Add(cb);
        }

        public void UnregisterPeerRemovedCallback(PeerCallBack cb)
        {
            peerRemovedCallbacks.ForEach(x => { if (x == cb) peerRemovedCallbacks.Remove(x); });
        }
    }
}