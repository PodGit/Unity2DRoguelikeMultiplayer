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
        private const int maxNumPacketsToSendPerIteration = 20;
        private const int maxNumConnectionsToListenFor = 1;

        private static NetState netState = NetState.Uninitialised;
        private static NetworkManager mInstance;

        private TcpListener serverListener = null;

        // Peers
        private static int numPeers = 0;
        private static NetworkPeer[] peers = null;
        private static List<PeerCallBack> peerAddedCallbacks;
        private static List<PeerCallBack> peerRemovedCallbacks;

        private System.Threading.Thread connectionThread = null;
        private System.Threading.Thread dataThread = null;

        private static Mutex writeQueueMutex;
        private static Queue<NetworkPacket> writeQueue;

        public static NetworkManager Instance
        {
            get
            {
                if (mInstance == null)
                {
                    //GameObject go = new GameObject("Network Manager");
                    //mInstance = go.AddComponent<NetworkManager>();
                    mInstance = (NetworkManager)ScriptableObject.CreateInstance("NetworkManager");
                }
                return mInstance;
            }
        }

        public NetworkManager()
        {
            if (peers == null)
            {
                peers = new NetworkPeer[GameManager.MaxNumPlayers];
            }

            peerAddedCallbacks = new List<PeerCallBack>();
            peerRemovedCallbacks = new List<PeerCallBack>();

            writeQueueMutex = new Mutex();
            writeQueue = new Queue<NetworkPacket>();

            Reset();
        }

        public void Reset()
        {
            Debug.Log("NetworkManager: Reset() called");
            ReleaseAllSockets();

            netState = NetState.Uninitialised;

            for (int peerIdx = 0; peerIdx < numPeers; ++peerIdx)
            {
                Debug.Log("ReleaseAllSockets: Releasing peer " + peerIdx);
                NetworkPeer peer = GetPeer(peerIdx);
                peer.Release();
                peers[peerIdx] = null;
            }

            numPeers = 0;

            peerAddedCallbacks.Clear();
            peerRemovedCallbacks.Clear();

            writeQueueMutex.Close();
            writeQueueMutex = new Mutex();
            writeQueue.Clear();
        }

        public void OnApplicationQuit()
        {
            Debug.Log("NetworkManager: Application quit, releasing all sockets...");
            Reset();
        }

        void ReleaseAllSockets()
        {
            if (connectionThread != null)
            {
                Debug.Log("ReleaseAllSockets: Connection thread not null, removing...");
                if (connectionThread.IsAlive)
                {
                    Debug.Log("ReleaseAllSockets: Connection thread alive, waiting to exit...");
                    connectionThread.Interrupt();
                    connectionThread.Abort();
                    connectionThread.Join();
                }
                connectionThread = null;
            }

            if (dataThread != null)
            {
                Debug.Log("ReleaseAllSockets: Data thread not null, removing...");
                if (dataThread.IsAlive)
                {
                    Debug.Log("ReleaseAllSockets: Data thread alive, waiting to exit...");
                    dataThread.Interrupt();
                    dataThread.Abort();
                    dataThread.Join();
                }
                dataThread = null;
            }

            if (serverListener != null)
            {
                Debug.Log("ReleaseAllSockets: Server listener not null, stopping...");
                serverListener.Stop();
                serverListener.Server.Dispose();
                serverListener = null;
            }
        }

        // Start is called before the first frame update
        void Start()
        {
           
        }

        // Update is called once per frame
        void Update()
        {
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
            Debug.Assert(peerIdx >= 0 && peerIdx < numPeers, "GetPeer invalid index specified:" + peerIdx);

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

            Debug.Assert(connectionThread == null, "connection thread already initialised");
            connectionThread = new Thread(ServerConnectionThreadProc);
            connectionThread.Start();

            InitDataThread();            
        }

        void ServerConnectionThreadProc()
        {
            while (true)
            {
                Debug.Log("Waiting for connection...");
                TcpClient connectedClient = serverListener.AcceptTcpClient();

                Debug.Log("Accepted new connection!");
                NetworkPeer newPeer = AddPeer("Joining...", false, false, connectedClient, NetworkPeer.PeerState.Joining);

                if (numPeers >= GameManager.MaxNumPlayers)
                {
                    Debug.Log("Accepted max num connections. Exiting server connection thread...");
                    return;
                }
            }
        }

        void InitDataThread()
        {
            Debug.Assert(dataThread == null, "data thread already initialised");
            dataThread = new Thread(DataThreadProc);
            dataThread.Start();
        }

        void DataThreadProc()
        {
            while (true)
            {
                // Check for outstanding reads
                for (int peerIdx = 0; peerIdx < numPeers; peerIdx++)
                {
                    NetworkPeer peer = GetPeer(peerIdx);
                    if (!peer.IsLocal() && peer.GetState() != NetworkPeer.PeerState.Uninitialised)
                    {
                        TcpClient client = peer.GetClient();
                        byte[] buffer = new byte[bufferSize];

                        while (client.GetStream().DataAvailable)
                        {
                            int sizeRead = client.GetStream().Read(buffer, 0, bufferSize);
                            OnPacketRecieved(peer, buffer, sizeRead);
                        }
                    }
                }

                // Write packets
                if (writeQueue.Count > 0)
                {
                    writeQueueMutex.WaitOne();
                    Queue<NetworkPacket> packetsToSendThisIteration = new Queue<NetworkPacket>();

                    while(writeQueue.Count > 0 && packetsToSendThisIteration.Count < maxNumPacketsToSendPerIteration)
                    {
                        packetsToSendThisIteration.Enqueue(writeQueue.Dequeue());
                    }

                    writeQueueMutex.ReleaseMutex();

                    while (packetsToSendThisIteration.Count > 0)
                    {
                        NetworkPacket packet = packetsToSendThisIteration.Dequeue();                        
                    }
                }
            }
        }

        void OnPacketRecieved(NetworkPeer peer, byte[] buffer, int size)
        {
            if (peer != null)
            {
                int peerIdx = peer.GetPeerId();
                Debug.Log("Packet received from peer:" + peerIdx);
            }
        }

        public void Join(string ipAddress, string playerName)
        {
            if (netState == NetState.Uninitialised)
            {
                //NetworkPeer localPeer = AddPeer(playerName, true, false, null, NetworkPeer.PeerState.Joined);
                ClientCreateSocket(ipAddress, GetHostPeer());

                InitDataThread();
            }
        }

        void ClientCreateSocket(string ipAddress, NetworkPeer peer)
        {
            Debug.Assert(peer.GetClient() == null, "Client socket already instantiated");

            IPAddress localIpAddress = IPAddress.Parse(ipAddress);
            IPEndPoint localEndPoint = new IPEndPoint(localIpAddress, clientPort);

            TcpClient newClient = new TcpClient(localEndPoint);
            peer.SetClient(newClient);

            Debug.Log("Attempting to join " + ipAddress + "...");
            Debug.Assert(connectionThread == null, "Connection thread not null");

            IPAddress remoteIpAddress = IPAddress.Parse(ipAddress);
            connectionThread = new Thread(ClientConnectionThreadProc);
            connectionThread.Start(remoteIpAddress);
        }

        void ClientConnectionThreadProc(object data)
        {
            netState = NetState.Joining;

            NetworkPeer peer = GetMyPeer();
            TcpClient tcpClient = peer.GetClient();
            IPAddress ipAddress = (IPAddress)data;

            tcpClient.Connect(ipAddress, serverPort);

            if (tcpClient.Connected)
            {
                NetworkPeer hostPeer = AddPeer("Host...", false, true, tcpClient, NetworkPeer.PeerState.Joined);
                netState = NetState.ActiveClient;

                SendPeerData();                
            }
            else
            {
                Debug.Log("Failed to connect to host:" + ipAddress.ToString());
                ClientConnectionFailed();
            }
        }

        void ClientConnectionFailed()
        {
            Debug.Assert(netState == NetState.Joining, "Not in joining state");
            netState = NetState.Uninitialised;

            for (int peerIdx = 0; peerIdx < numPeers; ++peerIdx)
            {
                NetworkPeer peer = GetPeer(peerIdx);
                peer.Release();
                peers[peerIdx] = null;
            }

            numPeers = 0;
        }

        void SendPeerData()
        {
            NetworkPeer peer = GetMyPeer();

            NetworkPacket joinPacket = new NetworkPacket();
            joinPacket.SetType(NetworkPacket.PacketType.PEER_DATA);
            joinPacket.WriteString(peer.GetName());

            AddPacketToQueue(joinPacket);
        }

        void AddPacketToQueue(NetworkPacket packet)
        {
            writeQueueMutex.WaitOne();
            writeQueue.Enqueue(packet);
            writeQueueMutex.ReleaseMutex();
        }

        NetworkPeer GetMyPeer()
        {
            for (int peerIdx = 0; peerIdx < numPeers; ++peerIdx)
            {
                NetworkPeer peer = GetPeer(peerIdx);

                if (peer.IsLocal())
                {
                    return peer;
                }
            }

            return null;
        }

        NetworkPeer GetHostPeer()
        {
            for (int peerIdx = 0; peerIdx < numPeers; ++peerIdx)
            {
                NetworkPeer peer = GetPeer(peerIdx);

                if (peer.IsHost())
                {
                    return peer;
                }
            }

            return null;
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