using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;
using System.Collections;
using System;

public class ScorpMP_Client : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 7765;
    public bool doLog = true;

    public TcpClient client;
    public NetworkStream stream;
    Thread receiveThread;
    public bool isConnected = false;

    public ScorpMP_ClientLogic clientLogic;
    public ScorpMP_PlayerList playerList;

    [Header("Connection")]
    public int sessionId = 0;

    public void log(string message, int type = 0)
    {
        if (doLog)
        {
            switch (type)
            {
                default:
                    Debug.Log("[Client] " + message);
                    break;
                case 1:
                    Debug.LogWarning("[Client WARN] " + message);
                    break;
                case 2:
                    Debug.LogError("[Client ERROR] " + message);
                    break;
            }
        }
    }

    void OnApplicationQuit()
    {
        if (client != null && client.Connected)
        {
            JObject packet = new JObject();
            packet["base"] = "terminate_connection";
            clientLogic.SendData(ScorpMP_ClientLogic.SendTarget.Server, packet);
        }
        ConnectionClosed();
    }

    public void Connect(string ip, int port)
    {
        try
        {
            client = new TcpClient();
            client.Connect(ip, port);
            stream = client.GetStream();
            client.SendBufferSize = 16384;
            client.ReceiveBufferSize = 16384;

            isConnected = true;

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            log("Established connection with server");
        }
        catch (Exception e)
        {
            if (e.GetType().Name == "SocketException")
                log("Could not connect to server, either the servers are down or you aren't connected to the internet", 1);
            else
                log("Encountered error while connecting: " + e.GetType().Name + ": " + e.Message, 2);
        }
    }

    void ReceiveData()
    {
        byte[] buffer = new byte[1024];

        Socket socket = client.Client;
        while (isConnected)
        {
            if (stream.DataAvailable)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                OnServerMessage(response);
            }
            Thread.Sleep(10);

            try
            {
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                    break;
            }
            catch (ObjectDisposedException) { 
                break; 
            }
        }
        
        ConnectionClosed();
    }

    public void OnServerMessage(string message)
    {
        clientLogic.OnMessageReceived(message);
    }

    public void ConnectionClosed()
    {
        clientLogic.CloseConnection();

        stream?.Close();
        client?.Close();
        receiveThread?.Abort();

        isConnected = false;
    }

    public void SendMessageToServer(string msg)
    {
        if (isConnected)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);
        }
    }
}
