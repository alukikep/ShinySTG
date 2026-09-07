namespace ShinySTG.Hitbox
{
    /// <summary>
    /// 阵营枚举。碰撞服务按阵营配对:
    ///   - Player 弹 vs Enemy 实体
    ///   - Enemy  弹 vs Player 实体
    ///   - Neutral 与所有人互不伤害(道具、装饰物、背景粒子)
    /// 阵营挂载位置:Hero / 敌人 / 子弹 prefab 上的 HitboxComponent.Team 字段。
    /// </summary>
    public enum CollisionTeam
    {
        Neutral = 0,
        Player  = 1,
        Enemy   = 2,
    }
}