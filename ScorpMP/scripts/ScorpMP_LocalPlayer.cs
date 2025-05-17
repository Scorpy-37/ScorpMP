using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScorpMP_LocalPlayer : MonoBehaviour
{
    public ScorpMP_Client client;

    public float updateRate = 20f;
    public float movementThreshold = 0.05f;
    public float rotationThreshold = 0.05f;
    public Vector3 syncedPosition;
    public Quaternion syncedRotation;

    void Start()
    {
        if (!client)
            client = FindFirstObjectByType<ScorpMP_Client>();
        StartCoroutine(UpdateTick(1f / updateRate));
    }

    IEnumerator UpdateTick(float interval)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);

            if (Vector3.Distance(syncedPosition, transform.position) > movementThreshold || Quaternion.Angle(syncedRotation, transform.rotation) > rotationThreshold)
            {
                client.clientLogic.UpdateLocalPlayer(transform);
                syncedPosition = transform.position;
                syncedRotation = transform.rotation;
            }
        }
    }
}
