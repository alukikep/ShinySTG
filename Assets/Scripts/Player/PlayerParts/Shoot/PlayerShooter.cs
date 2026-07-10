using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
   private FirePoint firePoint;
   private OptionManager optionManager;
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
      optionManager = this.GetComponent<OptionManager>();
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
         optionManager.OptionFire();
         optionShootingTimer = playerConfig.optionShootingTime;

      }
   }
}
