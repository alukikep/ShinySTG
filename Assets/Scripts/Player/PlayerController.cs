using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] public PlayerConfig playerConfig;
    private PlayerInput playerInput;
    private PlayerMover playerMover;
    private PlayerShooter playerShooter;
    private OptionManager optionManager;
    private PlayerPower playerPower;
    private PlayerRuntimeData playerRuntimeData;

    void Start()
    {
        playerInput = this.GetComponent<PlayerInput>();
        playerMover = this.GetComponent<PlayerMover>();
        playerShooter = this.GetComponent<PlayerShooter>();
        optionManager = this.GetComponent<OptionManager>();
        playerPower = this.GetComponent<PlayerPower>();
        playerRuntimeData = this.GetComponent<PlayerRuntimeData>();
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
        optionManager.SetFocus(playerInput.isSlowMode);

        if (Input.GetKeyDown(KeyCode.P))
        {
            playerRuntimeData.AddPower(1f);
        }
    }
}
