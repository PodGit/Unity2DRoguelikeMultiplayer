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
    public class NetworkManager : MonoBehaviour
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

        public class StateObject
        {
            public Socket workSocket = null;
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
            public StringBuilder sb = new StringBuilder();
        }

        public class Peer
        {
            public Peer(string name, int index, bool isLocal, bool isHost)
            {
                this.name = name;
                peerIdx = index;
                local = isLocal;
                host = isHost;
            }

            public string GetName()
            {
                return name;
            }

            string name = "";
            int peerIdx = -1;
            bool local = false;
            bool host = false;
        }

        private const int serverPort = 12000;
        private const int clientPort = 13000;
        private const int maxNumConnectionsToListenFor = 1;

        private static NetState netState = NetState.Uninitialised;
        private static NetworkManager mInstance;

        private Socket serverSocket = null;
        private Socket clientSocket = null;

        // Peers
        private static int numPeers = 0;
        private static Peer[] peers = null;

        public static NetworkManager Instance
        {
            get
            {
                if (mInstance == null)
                {
                    GameObject go = new GameObject();
                    mInstance = go.AddComponent<NetworkManager>();
                }
                return mInstance;
            }
        }

        public NetworkManager()
        {
            if (peers == null)
            {
                peers = new Peer[GameManager.MaxNumPlayers];
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

        public void AddPeer(string playerName, bool isLocal, bool isHost)
        {
            if (numPeers < GameManager.MaxNumPlayers)
            {
                peers[numPeers] = new Peer(playerName, numPeers, isLocal, isHost);
                numPeers++;
            }
            else
            {
                Debug.Log("Trying to add too many network peers");
            }
        }

        public Peer GetPeer(int peerIdx)
        {
            Debug.Assert(peerIdx >= 0 /*&& peerIdx < NetworkManager.Instance.numPeers*/, "GetPeer invalid index specified:" + peerIdx);

            return peers[peerIdx];
        }

        public void Host()
        {
            if (netState == NetState.Uninitialised)
            {
                netState = NetState.Hosting;
                ServerCreateSocket();
            }
        }

        void ServerCreateSocket()
        {
            Debug.Assert(serverSocket == null, "Server socket already instantiated");
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, serverPort);

            serverSocket.Bind(localEndPoint);
            serverSocket.Listen(maxNumConnectionsToListenFor);

            netState = NetState.ActiveHost;

            Debug.Log("Waiting for a connection...");
            serverSocket.BeginAccept(new AsyncCallback(ServerAcceptCallback), serverSocket);
        }

        void ServerAcceptCallback(IAsyncResult ar)
        {
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ServerReadCallback), state);
        }

        void ServerReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.  
            int read = handler.EndReceive(ar);

            // Data was read from the client socket.  
            if (read > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, read));
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ServerReadCallback), state);
            }
            else
            {
                if (state.sb.Length > 1)
                {
                    // All the data has been read from the client;  
                    // display it on the console.  
                    string content = state.sb.ToString();
                    Debug.Log($"Read {content.Length} bytes from socket.\n Data : {content}");
                }
            }
        }

        public void Join(string ipAddress)
        {
            if (netState == NetState.Uninitialised)
            {
                netState = NetState.Joining;
                Debug.Log("Attempting to join host. IP:" + ipAddress);

                ClientCreateSocket(ipAddress);
            }
        }

        void ClientCreateSocket(string ipAddress)
        {
            Debug.Assert(clientSocket == null, "Client socket already instantiated");

            IPAddress localIpAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(localIpAddress, clientPort);

            clientSocket = new Socket(localIpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Bind(localEndPoint);

            IPAddress remoteIpAddress = IPAddress.Parse(ipAddress);
            IPEndPoint ipe = new IPEndPoint(remoteIpAddress, serverPort);

            bool error = false;
            try
            {
                clientSocket.BeginConnect(ipe,
            new AsyncCallback(ClientConnectCallback), clientSocket);
            }
            catch (ArgumentNullException ae)
            {
                error = true;
                Debug.Log("ArgumentNullException : " + ae.ToString());
            }
            catch (SocketException se)
            {
                error = true;
                Debug.Log("SocketException : " + se.ToString());
            }
            catch (Exception e)
            {
                error = true;
                Debug.Log("Unexpected exception : " + e.ToString());
            }

            if (error)
            {
                ClientConnectionFailed();
            }
        }

        void ClientConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                Debug.Log("Socket connected to " + client.RemoteEndPoint.ToString());

                netState = NetState.ActiveClient;
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                ClientConnectionFailed();
            }
        }

        void ClientConnectionFailed()
        {
            netState = NetState.Uninitialised;

            clientSocket.Close();
            clientSocket = null;
        }
    }
}