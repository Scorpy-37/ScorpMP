using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using TMPro;

public class ScorpMP_Server : MonoBehaviour
{
    public class ClientData
    {
        public int SessionId;
        public TcpClient Client;
        public float LastHeartbeat;
        public ClientData(int sessionId, TcpClient client, float lastHeartbeat)
        {
            SessionId = sessionId;
            Client = client;
            LastHeartbeat = lastHeartbeat;
        }
    }

    public int serverPort = 7765;
    public bool doLog = true;
    public TextMeshProUGUI visualLog;

    TcpListener listener;
    Thread listenerThread;

    public int nextId = 0;
    public Dictionary<int, ClientData> connections = new Dictionary<int, ClientData>();

    public ScorpMP_ServerLogic serverLogic;

    List<string> visualLogQueue = new List<string>();

    public void log(string message, int type = 0)
    {
        if (doLog)
        {
            switch(type)
            {
                default:
                    Debug.Log("[Server] " + message);
                    break;
                case 1:
                    Debug.LogWarning("[Server WARN] " + message);
                    break;
                case 2:
                    Debug.LogError("[Server ERROR] " + message);
                    break;
            }
            if (visualLog)
                visualLogQueue.Add(message + "\n");
        }
    }
    
    void Start()
    {
        listenerThread = new Thread(ListenForClients);
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    private void Update()
    {
        if (visualLog && visualLogQueue.Count > 0)
        {
            foreach(string i in visualLogQueue)
                visualLog.text += i + "\n";
            visualLogQueue.Clear();
        }
    }

    void ListenForClients()
    {
        listener = new TcpListener(IPAddress.Any, serverPort);
        listener.Start();

        log("Server started on port " + serverPort.ToString());

        try
        {
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                client.SendBufferSize = 16384;
                client.ReceiveBufferSize = 16384;

                int sid = nextId;
                nextId++;

                JObject data = new JObject();
                data["base"] = "establish";
                data["id"] = sid;
                SendMessageToClient(client, JsonConvert.SerializeObject(data));
                connections.Add(sid, new ClientData(sid, client, serverLogic.Epoch()));

                log("Client (" + sid.ToString() + ") connected");

                serverLogic.PlayerAdd(connections[sid]);

                Thread clientThread = new Thread(() => HandleClient(client, sid));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }
        catch(Exception e)
        {
            if (e.GetType().Name == "SocketException")
                log("Error occured likely due to server exiting abruptly: " + e.GetType().Name + ": " + e.Message);
            else
                log("Error in ListenForClients thread: "+e.GetType().Name+": "+e.Message, 2);
        }
    }

    void OnApplicationQuit()
    {
        listener?.Stop();
    }

    void HandleClient(TcpClient client, int sessionId)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        bool clientConnected = true;
        Socket socket = client.Client;
        while (clientConnected)
        {
            if (stream.DataAvailable)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, stream.Read(buffer, 0, buffer.Length));
                OnClientMessage(client, message);
            }
            Thread.Sleep(10);

            try { clientConnected = !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0); }
            catch(ObjectDisposedException) { break; }
        }

        if (connections.ContainsKey(sessionId))
            CloseConnection(connections[sessionId]);
    }

    public void CloseConnection(ClientData c, string reason = "Disconnected")
    {
        if (c.Client != null && c.Client.Connected)
            c.Client.Close();

        try { connections.Remove(c.SessionId); } catch { }

        log("Client ("+c.SessionId+") disconnected: " + reason);

        serverLogic.PlayerRemove(c);
    }    

    public void OnClientMessage(TcpClient client, string message)
    {
        serverLogic.OnMessageReceived(client, message);
    }

    public void SendMessageToClient(TcpClient client, string message)
    {
        try
        {
            byte[] response = Encoding.UTF8.GetBytes(message);
            client.GetStream().Write(response, 0, response.Length);
        }
        catch (Exception e)
        {
            log("Error while sending message to client: " + e.GetType().Name + ": " + e.Message, 1);
            log("Packet in question: " + message, 1);
        }
    }
}