using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{

    public static BulletPool Instance { get; private set; }

    readonly Stack<Bullet> _available = new();
    readonly HashSet<Bullet> _active = new();
    public Bullet DefaultPrefab;
    public int InitialSize = 100;

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < InitialSize; i++)
        {
            var b = Instantiate(DefaultPrefab, transform);
            b.gameObject.SetActive(false);
            _available.Push(b);
        }
    }

    /// 取一颗子弹。
    public Bullet Get(Bullet prefab, Vector2 pos, float fireAngleRad, float speed, float angularSpeed)
    {
        // prefab 为空时兜底使用 DefaultPrefab（避免某些 Pattern 未配置时崩溃）
        var usePrefab = prefab != null ? prefab : DefaultPrefab;
        var b = _available.Count > 0 ? _available.Pop() : Instantiate(usePrefab, transform);
        b.gameObject.SetActive(true);
        b.Init(pos, fireAngleRad, speed, angularSpeed);
        _active.Add(b);
        return b;
    }

    /// 回收一颗。
    public void Return(Bullet bullet)
    {
        bullet.ClearModifiers();
        bullet.gameObject.SetActive(false);
        _active.Remove(bullet);
        _available.Push(bullet);
    }

    /// 提供给 Enemy 调用：发射一组 bullets（通过 FirePattern）。
    /// BulletPrefab 由 Pattern 自身携带，无需再传入。
    public void FireGroup(FirePattern pattern, Vector2 pos, float rotationRad)
    {
        pattern.Fire(pos, rotationRad, this);
        // Boss 系统钩子:每发一弹自动累计,供 ShotsFiredSignal 读取。
        // 没有挂 BossShotCounter 时(BossShotCounter.Instance == null)直接跳过,不影响普通敌人。
        ShinySTG.EnemyAI.Boss.BossShotCounter.Instance?.OnBossFired(pattern);
    }
}
