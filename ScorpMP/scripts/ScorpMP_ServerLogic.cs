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

    public void OnMessageReceived(TcpClient client, string message)
    {
        List<JObject> jsons = new List<JObject>();

        using (var stringReader = new StringReader(message))
        using (var jsonReader = new JsonTextReader(stringReader))
        {
            jsonReader.SupportMultipleContent = true;

            var serializer = new JsonSerializer();
            while (jsonReader.Read())
            {
                var obj = serializer.Deserialize<JObject>(jsonReader);
                if (obj != null)
                    jsons.Add(obj);
            }
        }

        for (int i = 0; i < jsons.Count; i++)
            OnMessageReceivedManaged(client, jsons[i]);
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
                        server.connections[clientSessionId].LastHeartbeat = serverTime;
                        break;
                    case "terminate_connection":
                        server.CloseConnection(server.connections[clientSessionId]);
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

    public float serverTime = 0f;
    public void HandleHeartbeat()
    {
        serverTime += Time.deltaTime;

        List<ScorpMP_Server.ClientData> drop_connections = new List<ScorpMP_Server.ClientData>();
        foreach (KeyValuePair<int, ScorpMP_Server.ClientData> connection in server.connections)
        {
            if (serverTime - connection.Value.LastHeartbeat > 30f)
                drop_connections.Add(connection.Value);
        }
        for (int i = 0; i < drop_connections.Count; i++)
            server.CloseConnection(drop_connections[i], "Timed out");
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
