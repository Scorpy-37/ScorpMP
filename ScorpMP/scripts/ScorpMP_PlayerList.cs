
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScorpMP_PlayerList : MonoBehaviour
{
    public Dictionary<int, ScorpMP_GlobalPlayer> players = new Dictionary<int, ScorpMP_GlobalPlayer>();
    public Transform playerPrefab;

    public void CreateNewPlayer(int sessionId)
    {
        Transform newPlayer = Instantiate(playerPrefab);
        newPlayer.parent = transform;
        newPlayer.name = sessionId.ToString();

        ScorpMP_GlobalPlayer player = newPlayer.gameObject.AddComponent<ScorpMP_GlobalPlayer>();
        player.sessionId = sessionId;

        players.Add(sessionId, player);
    }
    public void RemoveOldPlayer(int sessionId)
    {
        ScorpMP_GlobalPlayer player = players[sessionId];
        if (player)
            Destroy(player.gameObject);
    }
}
