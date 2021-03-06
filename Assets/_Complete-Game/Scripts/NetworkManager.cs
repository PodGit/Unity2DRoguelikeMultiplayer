﻿//#define USE_RAW_SOCKETS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        struct PendingPacket
        {
            public NetworkPacket packet;
            public NetworkPeer recipient;
        }

        //game stuff
        private bool gameStarted = false;

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
        private static Queue<PendingPacket> writeQueue;

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
            writeQueue = new Queue<PendingPacket>();

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

        public bool IsHost()
        {
            return GetMyPeer() == GetHostPeer();
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

                SendPeerData(newPeer);

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
                    if (!peer.IsLocal() && peer.GetClient().Connected)
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
                    Queue<PendingPacket> packetsToSendThisIteration = new Queue<PendingPacket>();

                    while(writeQueue.Count > 0 && packetsToSendThisIteration.Count < maxNumPacketsToSendPerIteration)
                    {
                        packetsToSendThisIteration.Enqueue(writeQueue.Dequeue());
                    }

                    writeQueueMutex.ReleaseMutex();

                    while (packetsToSendThisIteration.Count > 0)
                    {
                        PendingPacket pendingPacket = packetsToSendThisIteration.Dequeue();
                        Debug.Assert(pendingPacket.recipient.GetClient() != null && pendingPacket.recipient.GetClient().Connected, "Trying to send packet to peer with no TcpClient");

                        byte[] buffer;
                        int bufferSize;
                        pendingPacket.packet.GetBytes(out buffer, out bufferSize);
                        pendingPacket.recipient.GetClient().GetStream().Write(buffer, 0, bufferSize);
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

                NetworkPacket packet = new NetworkPacket(buffer, size);

                switch (packet.GetPacketType())
                {
                    case NetworkPacket.PacketType.INIT_PEER_DATA:
                        string name = packet.ReadString();

                        Debug.Log("Peer " + peer.GetPeerId() + " name recieved:" + name);
                        peer.SetName(name);
                        peer.SetState(NetworkPeer.PeerState.Joined);
                        break;
                    case NetworkPacket.PacketType.START_GAME:
                        gameStarted = true;
                        break;
                    case NetworkPacket.PacketType.MOVE_REQUEST:
                        Debug.Assert(IsHost(), "Only host should recieve move requests");
                        int direction = packet.ReadInt();

                        peer.SetRequestedMovement((Player.MovementDirection)direction);
                        break;
                    case NetworkPacket.PacketType.INIT_BOARD:
                        Debug.Assert(!IsHost(), "Only clients should recieve init boards");

                        ReadPacketInitBoard(packet);                        
                        break;
                    case NetworkPacket.PacketType.PLAYER_MOVE:
                        Debug.Assert(!IsHost(), "Only clients should recieve player states");

                        ReadPacketPlayerMove(packet);
                        break;
                    default:
                        break;
                }
            }
        }

        void ReadPacketInitBoard(NetworkPacket packet)
        {
            int level = packet.ReadInt();
            Debug.Log("Level: " + level);

            int numWallTiles = packet.ReadInt();
            Debug.Log("Num Wall tiles: " + numWallTiles);

            BoardManager.PlacedObject[] wallTilePositions = new BoardManager.PlacedObject[numWallTiles];
            for (int wallTileIdx = 0; wallTileIdx < numWallTiles; ++wallTileIdx)
            {
                wallTilePositions[wallTileIdx].locationIndex = packet.ReadInt();
                wallTilePositions[wallTileIdx].tileIndex = packet.ReadInt();
                Debug.Log("Wall tile[" + wallTileIdx + "]: LocationIdnex: " + wallTilePositions[wallTileIdx].locationIndex + "  tileIndex: " + wallTilePositions[wallTileIdx].tileIndex);
            }

            int numFoodTiles = packet.ReadInt();
            Debug.Log("Num food tiles: " + numFoodTiles);

            BoardManager.PlacedObject[] foodTilePositions = new BoardManager.PlacedObject[numFoodTiles];
            for (int foodTileIdx = 0; foodTileIdx < numFoodTiles; ++foodTileIdx)
            {
                foodTilePositions[foodTileIdx].locationIndex = packet.ReadInt();
                foodTilePositions[foodTileIdx].tileIndex = packet.ReadInt();
                Debug.Log("Food tile[" + foodTileIdx + "]: LocationIdnex: " + foodTilePositions[foodTileIdx].locationIndex + "  tileIndex: " + foodTilePositions[foodTileIdx].tileIndex);
            }

            int numEnemyTiles = packet.ReadInt();
            Debug.Log("Num enemy tiles: " + numEnemyTiles);

            BoardManager.PlacedObject[] enemyTilePositions = new BoardManager.PlacedObject[numEnemyTiles];
            for (int enemyTileIdx = 0; enemyTileIdx < numEnemyTiles; ++enemyTileIdx)
            {
                enemyTilePositions[enemyTileIdx].locationIndex = packet.ReadInt();
                enemyTilePositions[enemyTileIdx].tileIndex = packet.ReadInt();
                Debug.Log("Enemy tile[" + enemyTileIdx + "]: LocationIdnex: " + enemyTilePositions[enemyTileIdx].locationIndex + "  tileIndex: " + enemyTilePositions[enemyTileIdx].tileIndex);
            }

            GameManager.instance.GetBoardManager().InitBoard(wallTilePositions, foodTilePositions, enemyTilePositions);
            GameManager.instance.SetLevel(level);
            GameManager.instance.SetClientReadyToStart(true);
        }

        void ReadPacketPlayerMove(NetworkPacket packet)
        {
            int playerId = packet.ReadInt();

            Vector3 move = new Vector3();
            move.x = packet.ReadInt();
            move.y = packet.ReadInt();

            GameManager.instance.RecievedPlayerMove(playerId, move);
        }

        public void Join(string ipAddress, string playerName)
        {
            if (netState == NetState.Uninitialised)
            {
                NetworkPeer hostPeer = AddPeer("Host", false, true, null, NetworkPeer.PeerState.Joining);
                NetworkPeer myPeer = AddPeer(playerName, true, false, null, NetworkPeer.PeerState.Joining);

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

            NetworkPeer peer = GetHostPeer();
            TcpClient tcpClient = peer.GetClient();
            IPAddress ipAddress = (IPAddress)data;

            tcpClient.Connect(ipAddress, serverPort);

            if (tcpClient.Connected)
            {
                GetMyPeer().SetState(NetworkPeer.PeerState.Joined);
                netState = NetState.ActiveClient;

                SendPeerData(GetHostPeer());                
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

        void SendPeerData(NetworkPeer toPeer)
        {
            NetworkPeer peer = GetMyPeer();

            NetworkPacket joinPacket = new NetworkPacket();
            joinPacket.SetPacketType(NetworkPacket.PacketType.INIT_PEER_DATA);
            joinPacket.WriteString(peer.GetName());

            SendPacketToPeer(joinPacket, toPeer);
        }

        public void BroadcastLoadToGame()
        {
            Debug.Assert(IsHost(), "Can't call start game as non-host");

            NetworkPacket startPacket = new NetworkPacket();
            startPacket.SetPacketType(NetworkPacket.PacketType.START_GAME);

            BroadcastPacket(startPacket);
        }

        public void SetRequestedInput(Player.MovementDirection direction)
        {
            SendRequestMove(GetHostPeer(), direction);
        }

        void SendRequestMove(NetworkPeer toPeer, Player.MovementDirection direction)
        {
            Debug.Assert(!IsHost(), "Host cant send move requests");

            NetworkPeer myPeer = GetMyPeer();
            if (direction != Player.MovementDirection.none && direction != myPeer.GetRequestedMovement())
            {

                NetworkPacket startPacket = new NetworkPacket();
                startPacket.SetPacketType(NetworkPacket.PacketType.MOVE_REQUEST);
                startPacket.WriteInt((int)direction);

                myPeer.SetRequestedMovement(direction);
                SendPacketToPeer(startPacket, toPeer);
            }
        }

        void SendPacketToPeer(NetworkPacket packet, NetworkPeer peer)
        {
            PendingPacket newPacket;
            newPacket.packet = packet;
            newPacket.recipient = peer;
            Debug.Log("Sending " + (int)packet.GetPacketType() + " to peer " + peer.GetPeerId());

            writeQueueMutex.WaitOne();
            writeQueue.Enqueue(newPacket);
            writeQueueMutex.ReleaseMutex();
        }

        public bool GetGameStarted()
        {
            return gameStarted;
        }

        public void BroadcastPlayerMove(int playerId, Vector3 move)
        {
            Debug.Assert(IsHost(), "Only host should update player states");

            NetworkPacket packet = new NetworkPacket();
            packet.SetPacketType(NetworkPacket.PacketType.PLAYER_MOVE);

            packet.WriteInt(playerId);
            packet.WriteInt((int)move.x);
            packet.WriteInt((int)move.y);

            BroadcastPacket(packet);
        }

        public void BroadcastRoundStart(int level, BoardManager.PlacedObject[] wallTiles, BoardManager.PlacedObject[] foodTiles, BoardManager.PlacedObject[] enemyTiles)
        {
            Debug.Assert(IsHost(), "Only host can broadcast round start");
            Debug.Log("BroadcastRoundStart begin");

            NetworkPacket packet = new NetworkPacket();
            packet.SetPacketType(NetworkPacket.PacketType.INIT_BOARD);
            packet.WriteInt(level);
            Debug.Log("Level: " + level);

            packet.WriteInt(wallTiles.Length);
            Debug.Log("Num wall tiles: " + wallTiles.Length);

            for (int wallTileIndex = 0; wallTileIndex < wallTiles.Length; ++wallTileIndex)
            {
                packet.WriteInt(wallTiles[wallTileIndex].locationIndex);
                packet.WriteInt(wallTiles[wallTileIndex].tileIndex);
                Debug.Log("Wall tile[" + wallTileIndex + "]: LocationIdnex: " + wallTiles[wallTileIndex].locationIndex + "  tileIndex: " + wallTiles[wallTileIndex].tileIndex);
            }

            packet.WriteInt(foodTiles.Length);
            Debug.Log("Num wall tiles: " + foodTiles.Length);

            for (int foodTileIndex = 0; foodTileIndex < foodTiles.Length; ++foodTileIndex)
            {
                packet.WriteInt(foodTiles[foodTileIndex].locationIndex);
                packet.WriteInt(foodTiles[foodTileIndex].tileIndex);
                Debug.Log("Floor tile[" + foodTileIndex + "]: LocationIdnex: " + foodTiles[foodTileIndex].locationIndex + "  tileIndex: " + foodTiles[foodTileIndex].tileIndex);
            }

            int numEnemies = enemyTiles != null ? enemyTiles.Length : 0 ;
            packet.WriteInt(numEnemies);
            Debug.Log("Num enemy tiles: " + numEnemies);

            for (int enemyTileIndex = 0; enemyTileIndex < numEnemies; ++enemyTileIndex)
            {
                packet.WriteInt(enemyTiles[enemyTileIndex].locationIndex);
                packet.WriteInt(enemyTiles[enemyTileIndex].tileIndex);
                Debug.Log("Enemy tile[" + enemyTileIndex + "]: LocationIdnex: " + enemyTiles[enemyTileIndex].locationIndex + "  tileIndex: " + enemyTiles[enemyTileIndex].tileIndex);
            }

            BroadcastPacket(packet);
        }

        void BroadcastPacket(NetworkPacket packet)
        {
            Debug.Assert(IsHost(), "Only session host can broadcast packets");

            for (int peerIdx = 0; peerIdx < numPeers; ++peerIdx)
            {
                NetworkPeer peer = GetPeer(peerIdx);

                if (!peer.IsLocal())
                {
                    SendPacketToPeer(packet, peer);
                }
            }
        }

        public NetworkPeer GetMyPeer()
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