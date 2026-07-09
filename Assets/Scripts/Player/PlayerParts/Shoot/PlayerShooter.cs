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
         foreach (var point in playerConfig.firePointPosition)
         {
            firePoint.Fire(point, playerConfig.playerBullet, playerConfig.playerBulletSpeed, playerConfig.playerDamage);
         }
         playerShootingTimer = playerConfig.playerShootingTime;
      }
      if (optionShootingTimer < 0)
      {
         //子机射击逻辑
         optionShootingTimer = playerConfig.optionShootingTime;
      }
   }
}
