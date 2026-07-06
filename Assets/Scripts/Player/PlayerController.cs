using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private PlayerConfig playerConfig;
    private PlayerInput playerInput;
    private PlayerMover playerMover;
    private PlayerShooter playerShooter;

    void Start()
    {
        playerInput = this.GetComponent<PlayerInput>();
        playerMover = this.GetComponent<PlayerMover>();
        playerShooter = this.GetComponent<PlayerShooter>();
        playerConfig = this.GetComponent<PlayerConfig>();
    }
    void Update()
    {
        float speed = playerInput.isSlowMode ? playerConfig.focusSpeed : playerConfig.normalSpeed;
        playerInput.SlowMode();
        playerMover.Move(speed, playerInput.MoveDir());
    }
}
