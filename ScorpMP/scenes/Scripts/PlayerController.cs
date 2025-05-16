using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    void Update()
    {
        transform.position += Input.GetAxisRaw("Vertical") * transform.up * 10f * Time.deltaTime;
        transform.position += Input.GetAxisRaw("Horizontal") * transform.right * 10f * Time.deltaTime;
    }
}
