using System;
using SerializeReferenceEditor;
using UnityEngine;

/// <summary>
/// 按固定值或随机范围抽样的角速度转向。每次窗口首次执行时抽样，窗口内保持不变。
/// 通过 SetModifierTurn 提交有效时间内的转角；正数逆时针，负数顺时针。
/// </summary>
[Serializable, SRName("Modifier/Steer")]
public class SteerTowardModifier : BulletModifier, ISerializationCallbackReceiver
{
    // 保留旧资产和代码调用的固定角速度；Rate 为空时兼容使用此值。
    [HideInInspector] public float TurnRate = 90f;

    [SerializeReference, SR]
    [Tooltip("角速度来源：固定值或每颗子弹独立抽样的随机范围。")]
    public SteerRateStrategy Rate;

    public void OnBeforeSerialize() => MigrateLegacyRate();
    public void OnAfterDeserialize() => MigrateLegacyRate();

    void MigrateLegacyRate()
    {
        if (Rate == null) Rate = new FixedSteerRateStrategy { Value = TurnRate };
    }

    [NonSerialized] float _sampledRate;
    [NonSerialized] bool _hasSample;

    protected override void OnResetWindow()
    {
        _sampledRate = 0f;
        _hasSample = false;
    }

    protected override void OnWindowExitCleanup(Bullet bullet) => bullet.ClearModifierTurnRate();

    public override void ModifyCore(Bullet b, float dt)
    {
        if (!_hasSample)
        {
            _sampledRate = Rate != null ? Rate.Sample() : TurnRate;
            _hasSample = true;
        }
        // 使用窗口裁剪后的 dt，避免末帧多转或被退出清理吞掉。
        b.SetModifierTurn(_sampledRate * Mathf.Deg2Rad, dt);
    }

    public override BulletModifier Clone()
    {
        var copy = (SteerTowardModifier)MemberwiseClone();
        copy.StartTrigger = StartTrigger?.Clone();
        copy.Rate = Rate?.Clone();
        copy.ResetWindow();
        return copy;
    }
}
