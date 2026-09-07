using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{

    public static BulletPool Instance { get; private set; }

    /// 按 prefab 分桶：不同 FirePattern.BulletPrefab 各自走各自的栈，避免互相覆盖外观/行为。
    readonly Dictionary<Bullet, Stack<Bullet>> _available = new();
    readonly HashSet<Bullet> _active = new();

    /// <summary>供 CollisionService 等外部系统访问当前活跃子弹。HashSet 迭代时禁止外部修改(碰撞服务用 deferred return 规避)。</summary>
    public System.Collections.Generic.IReadOnlyCollection<Bullet> ActiveBullets => _active;

    public Bullet DefaultPrefab;
    public int InitialSize = 100;

    void Awake()
    {
        Instance = this;
        // 只为 DefaultPrefab 预热一颗桶，避免在用户尚未配置任何 FirePattern 时就 NRE。
        // 其他 prefab 在第一次被 FirePattern 真正使用时按需扩容。
        if (DefaultPrefab != null && InitialSize > 0)
        {
            var stack = GetOrCreateStack(DefaultPrefab);
            for (int i = 0; i < InitialSize; i++)
            {
                var b = Instantiate(DefaultPrefab, transform);
                b.SourcePrefab = DefaultPrefab;
                b.gameObject.SetActive(false);
                stack.Push(b);
            }
        }
    }

    void OnDestroy()
    {
        // 场景切换时解除 Instance 指针。子弹实例随 GameObject 自动销毁,不需手动清 _active
        // (外部系统此刻也已停止 Update,无并发风险)。
        if (Instance == this) Instance = null;
    }

    /// 获取（或懒创建）某个 prefab 对应的桶。
    Stack<Bullet> GetOrCreateStack(Bullet prefab)
    {
        if (!_available.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<Bullet>();
            _available[prefab] = stack;
        }
        return stack;
    }

    /// 取一颗子弹。
    /// </summary>
    /// <param name="prefab">要实例化的子弹 prefab。空时兜底使用 DefaultPrefab。</param>
    /// <param name="pos">发射位置</param>
    /// <param name="fireAngleRad">发射角度(弧度)</param>
    /// <param name="speed">飞行速度</param>
    /// <param name="angularSpeed">角速度(弧度/秒)</param>
    /// <param name="damage">伤害值(玩家弹才用,经 bullet.Damage 传给 CollisionService)</param>
    /// <param name="ownerTeam">发射者阵营(透传给子弹 Hitbox.Team)。null = Neutral(不参与碰撞)。</param>
    public Bullet Get(Bullet prefab, Vector2 pos, float fireAngleRad, float speed, float angularSpeed,
                      float damage, ShinySTG.Hitbox.CollisionTeam ownerTeam)
    {
        // prefab 为空时兜底使用 DefaultPrefab(避免某些 Pattern 未配置时崩溃)
        var usePrefab = prefab != null ? prefab : DefaultPrefab;
        if (usePrefab == null)
        {
            // 没有任何可用的 prefab:直接 return,避免 NRE。
            return null;
        }

        var stack = GetOrCreateStack(usePrefab);
        var b = stack.Count > 0 ? stack.Pop() : Instantiate(usePrefab, transform);
        b.SourcePrefab = usePrefab;
        b.gameObject.SetActive(true);
        b.Init(pos, fireAngleRad, speed, angularSpeed, damage, ownerTeam);
        _active.Add(b);
        return b;
    }

    /// 回收一颗。
    public void Return(Bullet bullet)
    {
        if (bullet == null) return;
        bullet.ClearModifiers();
        bullet.gameObject.SetActive(false);
        _active.Remove(bullet);

        // 按该弹自己的 SourcePrefab 路由到正确的桶，保证不同 prefab 的子弹不会互相污染。
        var key = bullet.SourcePrefab != null ? bullet.SourcePrefab : DefaultPrefab;
        if (key != null)
        {
            var stack = GetOrCreateStack(key);
            stack.Push(bullet);
        }
        // 若 key 也为 null，则直接丢弃该子弹实例（极端兜底，不让池逻辑崩溃）。
    }

    /// 提供给 Enemy / Player 调用:发射一组 bullets(通过 FirePattern)。
    /// BulletPrefab 由 Pattern 自身携带,无需再传入。
    /// </summary>
    /// <param name="pattern">要发射的 FirePattern</param>
    /// <param name="pos">发射位置</param>
    /// <param name="rotationRad">整体朝向增量(弧度)</param>
    /// <param name="ownerHitbox">
    /// 发射者的 Hitbox(可为 null)。null 时子弹阵营 = Neutral(不参与碰撞)。
    /// 玩家发射传 PlayerHitbox;敌人发射传 EnemyHitbox。
    /// 子弹阵营 = ownerHitbox.Team,经 bullet.Init() 透传到 HitboxComponent.Team。
    /// </param>
    public void FireGroup(FirePattern pattern, Vector2 pos, float rotationRad,
                          ShinySTG.Hitbox.HitboxComponent ownerHitbox = null)
    {
        pattern.Fire(pos, rotationRad, this, ownerHitbox);
        // Boss 系统钩子:每发一弹自动累计,供 ShotsFiredSignal 读取。
        // 没有挂 BossShotCounter 时(BossShotCounter.Instance == null)直接跳过,不影响普通敌人。
        ShinySTG.EnemyAI.Boss.BossShotCounter.Instance?.OnBossFired(pattern);
    }
}
