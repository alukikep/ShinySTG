using UnityEngine;
using ShinySTG.Hitbox;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 单条直线激光。位置 + 方向 + 长度,几何用 LaserGeometry 算端点。
    /// PR1 阶段:用 Data.MaxLength 作默认长度;LengthMultiplier 提供简单倍率覆盖。
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Laser/Pattern/Straight")]
    public class StraightLaserPattern : LaserPattern
    {
        [Tooltip("激光长度倍率(相对 LaserData.MaxLength)。1 = 整长。")]
        public float LengthMultiplier = 1f;

        public override LaserEntity Fire(Vector2 position, float angleRad, LaserPool pool,
                                         HitboxComponent ownerHitbox,
                                         LaserModifier[] extraModifiers = null)
        {
            var data = Data;
            if (data == null || pool == null) return null;

            float length = data.MaxLength * LengthMultiplier;
            var laser = pool.Get(data, position, angleRad, length, ownerHitbox);
            if (laser == null) return null;

            // 挂 modifier(ModifierPrefabs + extraModifiers,与 BulletPool.AttachModifiers 同思路)
            AttachModifiers(laser, ModifierPrefabs, extraModifiers);
            // 触发 FireSounds(由本 pattern 触发一次,不走 LaserPool 中心化,避免与 FirePattern 语义不一致)
            PlayFireSounds(position, ownerHitbox);
            return laser;
        }

        static void AttachModifiers(LaserEntity laser, LaserModifier[] a, LaserModifier[] b)
        {
            if (laser == null) return;
            if (a != null)
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != null) laser.AddModifier(a[i].Clone());
            if (b != null)
                for (int i = 0; i < b.Length; i++)
                    if (b[i] != null) laser.AddModifier(b[i].Clone());
            // PR3 起会替换为 ResetAllModifierWindows + AttachSignalTriggers(对齐 BulletModifier)
            laser.ResetAllModifierWindows();
        }

        void PlayFireSounds(Vector2 pos, HitboxComponent owner)
        {
            if (FireSounds == null) return;
            for (int i = 0; i < FireSounds.Length; i++)
                FireSounds[i]?.OnFireTriggered(pos, owner);
        }
    }
}
