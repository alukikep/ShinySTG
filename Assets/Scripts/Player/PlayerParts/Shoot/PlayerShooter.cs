using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
   private FirePoint firePoint;
   private float playerShootingTimer;
   private float optionShootingTimer;

   void Update()
   {
      playerShootingTimer -= Time.deltaTime;
      optionShootingTimer -= Time.deltaTime;
   }

   void Start()
   {
      firePoint = this.GetComponent<FirePoint>();
   }
   public void Shoot(PlayerConfig playerConfig)
   {
      if (playerShootingTimer < 0)
      {
         firePoint.Fire(playerConfig.firePointPosition, playerConfig.playerBullet, playerConfig.playerBulletSpeed);
         playerShootingTimer = playerConfig.playerShootingTime;
      }
      if (optionShootingTimer < 0)
      {
         //子机射击逻辑
         optionShootingTimer = playerConfig.optionShootingTime;
      }
   }
}
