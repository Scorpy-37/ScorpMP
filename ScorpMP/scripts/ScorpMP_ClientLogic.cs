using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ScorpMP_ClientLogic : MonoBehaviour
{
    public ScorpMP_Client client;
    public bool doConnect = true;

    void Start()
    {
        StartCoroutine(AttemptConnection());
    }

    IEnumerator AttemptConnection()
    {
        yield return new WaitForSecondsRealtime(1f);
        while (true)
        {
            if (doConnect && !client.isConnected)
                client.Connect(client.serverIP, client.serverPort);
            yield return new WaitForSecondsRealtime(5f);
        }
    }

    public enum SendTarget { Everyone, Others, Server, Client };
    public bool SendData(SendTarget target, JObject data, int specific = -1)
    {
        if (target == SendTarget.Client && specific < 0)
            return false;

        string targetStr = "server";
        switch(target)
        {
            default:
                break;
            case SendTarget.Everyone:
                targetStr = "everyone";
                break;
            case SendTarget.Others:
                targetStr = "others";
                break;
            case SendTarget.Server:
                targetStr = "server";
                break;
            case SendTarget.Client:
                targetStr = specific.ToString();
                break;
        }

        data["recipient"] = targetStr;
        client.SendMessageToServer(JsonConvert.SerializeObject(data));

        return true;
    }

    string jsonRemainder = "";
    string lastExc = "";
    public void OnMessageReceived(string message)
    {
        try
        {
            message = jsonRemainder + message;
            List<JObject> jsons = new List<JObject>();

            jsonRemainder = "";
            for (int i = 0; i < message.Length; i++)
            {
                jsonRemainder += message[i];
                if (jsonRemainder[jsonRemainder.Length-1] == '}')
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
                client.log("JSON remainder had to be cleared due to too much buildup, latest exception: " + lastExc, 1);
            }

            for (int i = 0; i < jsons.Count; i++)
                OnMessageReceivedManaged(jsons[i]);
        }
        catch (Exception e)
        {
            client.log("Error while reading packets: " + e.GetType().Name + ": " + e.Message, 1);
            client.log("Payload in question: " + message, 1);
        }
    }

    public void OnMessageReceivedManaged(JObject data)
    {
        try
        {
            if (data.ContainsKey("base"))
            {
                switch ((string)data["base"])
                {
                    case "establish":
                        client.sessionId = (int)data["id"];
                        break;
                    case "server_error":
                        client.log("Server-side error: " + data["message"], 1);
                        break;
                    case "heartbeat":
                        lastHeartbeatReceived = 0;
                        break;
                    case "player_added":
                        try
                        {
                            client.log("Player ("+data["sid"]+") has connected");
                            addPlayerBuffer.Add((int)data["sid"]);

                            JObject packet = new JObject();
                            packet["base"] = "player_notify";
                            packet["sid"] = client.sessionId;
                            SendData(SendTarget.Client, packet, (int)data["sid"]);
                        }
                        catch (Exception e)
                        {
                            client.log("Error while processing player join: " + e.GetType().Name + ": " + e.Message, 1);
                            client.log("Packet in question: " + JsonConvert.SerializeObject(data), 1);
                        }
                        break;
                    case "player_removed":
                        try
                        {
                            client.log("Player (" + data["sid"] + ") has disconnected");
                            removePlayerBuffer.Add((int)data["sid"]);
                        }
                        catch (Exception e)
                        {
                            client.log("Error while processing player leave: " + e.GetType().Name + ": " + e.Message, 1);
                            client.log("Packet in question: " + JsonConvert.SerializeObject(data), 1);
                        }
                        break;
                    case "player_notify":
                        try
                        {
                            int csid = (int)data["sid"];
                            if (!client.playerList.players.ContainsKey(csid))
                                addPlayerBuffer.Add((int)data["sid"]);
                        }
                        catch (Exception e)
                        {
                            client.log("Error while processing player notify: " + e.GetType().Name + ": " + e.Message, 1);
                            client.log("Packet in question: " + JsonConvert.SerializeObject(data), 1);
                        }
                        break;
                    case "player_update":
                        try
                        {
                            ScorpMP_GlobalPlayer player = client.playerList.players[(int)data["client"]];
                            Vector3 pos = new Vector3((float)data["px"], (float)data["py"], (float)data["pz"]);
                            Quaternion rot = new Quaternion((float)data["rx"], (float)data["ry"], (float)data["rz"], (float)data["rw"]);

                            player.syncedPosition = pos;
                            player.syncedRotation = rot;
                        }
                        catch (Exception e)
                        {
                            client.log("Error while syncing other player position: " + e.GetType().Name + ": " + e.Message, 1);
                            client.log("Packet in question: " + JsonConvert.SerializeObject(data), 1);
                        }
                        break;
                    case "print":
                        client.log("Received message from another client (print-only): " + data["message"]);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            client.log("Error while reading message from server: " + e.GetType().Name + ": " + e.Message, 1);
            client.log("Packet in question: " + JsonConvert.SerializeObject(data), 1);
        }
    }

    float lastHeartbeatSent = 0f;
    float lastHeartbeatReceived = 0f;
    public void HandleHeartbeat()
    {
        lastHeartbeatSent += Time.deltaTime;
        if (lastHeartbeatSent > 5f)
        {
            JObject packet = new JObject();
            packet["base"] = "heartbeat";
            packet["sid"] = client.sessionId;
            SendData(SendTarget.Server, packet);

            lastHeartbeatSent = 0f;
        }

        lastHeartbeatReceived += Time.deltaTime;
        if (lastHeartbeatReceived > 30f)
            client.ConnectionClosed();
    }

    IEnumerator sendUpdateLater(int i)
    {
        yield return new WaitForSecondsRealtime(1f);
        SendLocalPlayerData(localPlayer.transform, SendTarget.Client, i);
    }

    List<int> addPlayerBuffer = new List<int>();
    List<int> removePlayerBuffer = new List<int>();
    void Update()
    {
        if (client.isConnected)
        {
            if (client.client == null)
            {
                client.ConnectionClosed();
                return;
            } else if (!client.client.Connected) {
                client.ConnectionClosed();
                return;
            }

            HandleHeartbeat();

            if (addPlayerBuffer.Count > 0)
            {
                foreach (int i in addPlayerBuffer)
                {
                    client.playerList.CreateNewPlayer(i);

                    if (localPlayer)
                        StartCoroutine(sendUpdateLater(i));
                }
                addPlayerBuffer.Clear();
            }
            if (removePlayerBuffer.Count > 0)
            {
                foreach (int i in removePlayerBuffer)
                    client.playerList.RemoveOldPlayer(i);
                removePlayerBuffer.Clear();
            }

            if (Input.GetKeyDown(KeyCode.H))
                client.ConnectionClosed();
        }
    }

    public void SendLocalPlayerData(Transform player, SendTarget target = SendTarget.Others, int specific = -1)
    {
        JObject packet = new JObject();
        packet["base"] = "player_update";
        packet["client"] = client.sessionId;
        packet["px"] = player.position.x;
        packet["py"] = player.position.y;
        packet["pz"] = player.position.z;
        packet["rx"] = player.rotation.x;
        packet["ry"] = player.rotation.y;
        packet["rz"] = player.rotation.z;
        packet["rw"] = player.rotation.w;
        SendData(target, packet, specific);
    }

    public ScorpMP_LocalPlayer localPlayer;
    public void UpdateLocalPlayer(Transform player)
    {
        if (client.isConnected && client.sessionId != -1)
        {
            if (!localPlayer)
                localPlayer = player.GetComponent<ScorpMP_LocalPlayer>();

            SendLocalPlayerData(player);
        }
    }

    public void RemoveAllPlayers()
    {
        foreach (KeyValuePair<int, ScorpMP_GlobalPlayer> player in client.playerList.players)
            removePlayerBuffer.Add(player.Key);
    }

    public void CloseConnection()
    {
        RemoveAllPlayers();
    }
}
