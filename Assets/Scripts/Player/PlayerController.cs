using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerConfig playerConfig;
    private PlayerInput playerInput;
    private PlayerMover playerMover;
    private PlayerShooter playerShooter;

    void Start()
    {
        playerInput = this.GetComponent<PlayerInput>();
        playerMover = this.GetComponent<PlayerMover>();
        playerShooter = this.GetComponent<PlayerShooter>();
    }
    void Update()
    {

        float speed = playerInput.isSlowMode ? playerConfig.focusSpeed : playerConfig.normalSpeed;
        playerInput.SlowMode();
        playerMover.Move(speed, playerInput.MoveDir());

        if (playerInput.isShooting())
        {
            playerShooter.Shoot(playerConfig);
        }
    }
}
