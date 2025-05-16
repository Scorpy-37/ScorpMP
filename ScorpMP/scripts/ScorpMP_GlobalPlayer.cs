using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScorpMP_GlobalPlayer : MonoBehaviour
{
    public int sessionId = -1;

    public Vector3 syncedPosition;
    public Quaternion syncedRotation;

    public Transform self;
    void Start()
    {
        self = transform;
    }

    void Update()
    {
        //SetValues();
        LerpValues();
    }

    public void SetValues()
    {
        self.position = syncedPosition;
        self.rotation = syncedRotation;
    }

    public void LerpValues()
    {
        self.position = Vector3.Lerp(self.position, syncedPosition, 20f * Time.deltaTime);
        self.rotation = Quaternion.Slerp(self.rotation, syncedRotation, 20f * Time.deltaTime);
    }
}
