using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class ScorpMP_ServerLogic : MonoBehaviour
{
    public ScorpMP_Server server;

    public float Epoch()
    {
        return (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
    }

    string jsonRemainder = "";
    string lastExc = "";
    public void OnMessageReceived(TcpClient client, string message)
    {
        try
        {
            message = jsonRemainder + message;
            List<JObject> jsons = new List<JObject>();

            jsonRemainder = "";
            for (int i = 0; i < message.Length; i++)
            {
                jsonRemainder += message[i];
                if (jsonRemainder[jsonRemainder.Length - 1] == '}')
                {
                    try
                    {
                        JObject json = (JObject)JsonConvert.DeserializeObject(jsonRemainder);
                        if (json != null)
                        {
                            jsons.Add(json);
                            jsonRemainder = "";
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.GetType().Name == "IndexOutOfRangeException")
                        {
                            jsonRemainder = "";
                        }
                        else
                        {
                            lastExc = e.GetType().Name;
                        }
                    }
                }
            }
            if (jsonRemainder.Length > 512)
            {
                jsonRemainder = "";
                server.log("JSON remainder had to be cleared due to too much buildup, latest exception: " + lastExc, 1);
            }

            for (int i = 0; i < jsons.Count; i++)
                OnMessageReceivedManaged(client, jsons[i]);
        }
        catch(Exception e)
        {
            server.log("Error while reading packets: " + e.GetType().Name + ": " + e.Message, 1);
            server.log("Payload in question: " + message, 1);
        }
    }
    public void OnMessageReceivedManaged(TcpClient client, JObject data)
    {
        int clientSessionId = -1;
        foreach(KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
        {
            if (connection.Value.Client == client)
            {
                clientSessionId = connection.Value.SessionId;
                break;
            }
        }
        if (clientSessionId == -1)
            return;

        try
        {
            if (data.ContainsKey("recipient"))
            {
                string target = (string)data["recipient"];
                switch (target)
                {
                    default:
                        // Send to specific client
                        try { server.SendMessageToClient(server.connections[int.Parse((string)data["recipient"])].Client, JsonConvert.SerializeObject(data)); }
                        catch {}
                        break;
                    case "everyone":
                        foreach (KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
                            server.SendMessageToClient(connection.Value.Client, JsonConvert.SerializeObject(data));
                        break;
                    case "others":
                        foreach (KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
                        {
                            if (clientSessionId != connection.Value.SessionId)
                                server.SendMessageToClient(connection.Value.Client, JsonConvert.SerializeObject(data));
                        }
                        break;
                    case "server":
                        HandlePacket(client, clientSessionId, data);
                        break;
                }
            }
            else
                HandlePacket(client, clientSessionId, data);
        }
        catch (Exception e)
        {
            server.log("Error while reading message from server: " + e.GetType().Name + ": " + e.Message, 1);
            server.log("Packet in question: " + JsonConvert.SerializeObject(data), 1);
        }
    }

    public void HandlePacket(TcpClient client, int clientSessionId, JObject data)
    {
        try
        {
            if (data.ContainsKey("base"))
            {
                switch ((string)data["base"])
                {
                    case "heartbeat":
                        server.connections[clientSessionId].LastHeartbeat = Epoch();
                        break;
                }
            }
        }
        catch (Exception e)
        {
            server.log("Error while reading message from server: " + e.GetType().Name + ": " + e.Message, 1);
            server.log("Packet in question: " + JsonConvert.SerializeObject(data), 1);
        }
    }

    float lastHeartbeatSent = 0f;
    public void HandleHeartbeat()
    {
        List<ScorpMP_Server.ClientData> drop_connections = new List<ScorpMP_Server.ClientData>();
        foreach (KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
        {
            if (Epoch() - connection.Value.LastHeartbeat > 30f)
                drop_connections.Add(connection.Value);
        }
        for (int i = 0; i < drop_connections.Count; i++)
            server.CloseConnection(drop_connections[i], "Timed out");

        lastHeartbeatSent += Time.deltaTime;
        if (lastHeartbeatSent > 2f)
        {
            lastHeartbeatSent = 0f;
            JObject packet = new JObject();
            packet["base"] = "heartbeat";
            foreach (KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
                server.SendMessageToClient(connection.Value.Client, JsonConvert.SerializeObject(packet));
        }
    }

    public void PlayerAdd(ScorpMP_Server.ClientData player)
    {
        JObject packet = new JObject();
        packet["base"] = "player_added";
        packet["sid"] = player.SessionId;

        foreach (KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
        {
            if (player.SessionId != connection.Value.SessionId)
                server.SendMessageToClient(connection.Value.Client, JsonConvert.SerializeObject(packet));
        }
    }
    public void PlayerRemove(ScorpMP_Server.ClientData player)
    {
        JObject packet = new JObject();
        packet["base"] = "player_removed";
        packet["sid"] = player.SessionId;

        foreach (KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
        {
            if (player.SessionId != connection.Value.SessionId)
                server.SendMessageToClient(connection.Value.Client, JsonConvert.SerializeObject(packet));
        }
    }

    void Update()
    {
        HandleHeartbeat();
    }
}
