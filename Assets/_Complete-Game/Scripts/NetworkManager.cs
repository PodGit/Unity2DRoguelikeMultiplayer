using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

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

    private const int serverPort = 12000;
    private const int clientPort = 13000;
    private const int maxNumConnectionsToListenFor = 1;

    private static TcpClient        tcpClient;
    private static TcpListener      tcpServer;
    private static NetState         netState = NetState.Uninitialised;    
    private static NetworkManager   mInstance;

    private Socket serverSocket = null;
    private Socket clientSocket = null;

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

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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

    public static void ThreadProc()
    {
        Byte[] bytes = new Byte[256];
        String data = null;

        while (true)
        {
            Debug.Log("Waiting for connection");

            TcpClient client = tcpServer.AcceptTcpClient();
            Debug.Log("Connected!");

            data = null;

            NetworkStream stream = client.GetStream();

            int i = 0;

            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                // Translate data bytes to a ASCII string.
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Debug.Log("Received: " + data);

                // Process the data sent by the client.
                data = data.ToUpper();

                byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

                // Send back a response.
                stream.Write(msg, 0, msg.Length);
                Debug.Log("Sent: " + data);
            }

            // Shutdown and end connection
            client.Close();
        }
    }

    void CreateListener()
    {
        IPAddress localAddr = IPAddress.Parse("127.0.0.1");

        tcpServer = new TcpListener(localAddr, serverPort);
        tcpServer.Start();

        Thread t = new Thread(new ThreadStart(ThreadProc));
        t.Start();
    }

    //void Connect(string server, string message)
    //{
    //    try
    //    {
    //        // Create a TcpClient.
    //        // Note, for this client to work you need to have a TcpServer 
    //        // connected to the same address as specified by the server, port
    //        // combination.
    //        int port = 13000;
    //        TcpClient client = new TcpClient(server, port);

    //        // Translate the passed message into ASCII and store it as a Byte array.
    //        byte[] data = System.Text.Encoding.ASCII.GetBytes(message);

    //        // Get a client stream for reading and writing.
    //        //  Stream stream = client.GetStream();

    //        NetworkStream stream = client.GetStream();

    //        // Send the message to the connected TcpServer. 
    //        stream.Write(data, 0, data.Length);

    //        Debug.Log("Sent: {0}", message);

    //        // Receive the TcpServer.response.

    //        // Buffer to store the response bytes.
    //        data = new byte[256];

    //        // String to store the response ASCII representation.
    //        string responseData = string.Empty;

    //        // Read the first batch of the TcpServer response bytes.
    //        int bytes = stream.Read(data, 0, data.Length);
    //        responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
    //        Debug.Log("Received: {0}", responseData);

    //        tcpClient = client;
    //        networkStream = stream;
    //    }
    //    catch (ArgumentNullException e)
    //    {
    //        Debug.Log("ArgumentNullException: {0}", e);
    //    }
    //    catch (SocketException e)
    //    {
    //        Debug.Log("SocketException: {0}", e);
    //    }

    //    Debug.Log("\n Press Enter to continue...");
    //    Console.Read();
    //}

    //public void Disconnect()
    //{
    //    if (netState > NetState.Uninitialised)
    //    {
    //        networkStream.Close();
    //        tcpClient.Close();
    //        netState = NetState.Uninitialised;
    //    }
    //}
}
