using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;

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

    TcpListener listener;
    Thread listenerThread;

    public int nextId = 0;
    public Dictionary<int, ClientData> connections = new Dictionary<int, ClientData>();

    public ScorpMP_ServerLogic serverLogic;

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
        }
    }
    
    void Start()
    {
        listenerThread = new Thread(ListenForClients);
        listenerThread.IsBackground = true;
        listenerThread.Start();
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

                JObject data = new JObject();
                data["base"] = "establish";
                data["id"] = nextId;
                SendMessageToClient(client, JsonConvert.SerializeObject(data));
                connections.Add(nextId, new ClientData(nextId, client, serverLogic.serverTime));

                log("Client (" + nextId.ToString() + ") connected");

                serverLogic.PlayerAdd(connections[nextId]);

                Thread clientThread = new Thread(() => HandleClient(client, nextId));
                clientThread.IsBackground = true;
                clientThread.Start();

                nextId++;
            }
        }
        catch(Exception e)
        {
            if (e.GetType().Name == "SocketException")
                log("Error occured likely due to server exiting abruptly: " + e.GetType().Name + ": " + e.Message, 1);
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

        while (client.Connected)
        {
            if (stream.DataAvailable)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, stream.Read(buffer, 0, buffer.Length));
                OnClientMessage(client, message);
            }
            Thread.Sleep(10);
        }

        if (connections.ContainsKey(sessionId))
            CloseConnection(connections[sessionId]);
    }

    public void CloseConnection(ClientData c, string reason = "Disconnected")
    {
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
        byte[] response = Encoding.UTF8.GetBytes(message);
        client.GetStream().Write(response, 0, response.Length);
    }
}