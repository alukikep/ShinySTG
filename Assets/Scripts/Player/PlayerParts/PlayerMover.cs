using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMover : MonoBehaviour
{

    public void Move(float speed, Vector3 Dir)
    {
        gameObject.transform.position += speed * Dir * Time.deltaTime;
    }
}
