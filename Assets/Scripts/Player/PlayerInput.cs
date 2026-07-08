using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public bool isSlowMode;


    public Vector3 MoveDir()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        return new Vector3(horizontal, vertical, 0);
    }

    public void SlowMode()
    {
        if (Input.GetKey(KeyCode.K))
        {
            isSlowMode = true;
        }
        else
        {
            isSlowMode = false;
        }
    }

    public bool isShooting()
    {
        if (Input.GetKey(KeyCode.J))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

}
