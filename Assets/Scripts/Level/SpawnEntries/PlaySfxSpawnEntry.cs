using System;
using ShinySTG.Audio;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>
    /// 在关卡时间轴的指定时间点播放一个 SFX(任意短促音效 —— UI 点击、warning、阶段切换声、剧情音效等)。
    ///
    /// 与 BossHealth._hitSfx / EnemyHealth._deathSfx 的区别:
    ///   - 后者是"事件型"挂点(某个组件在某个回调里自动播放),需要业务逻辑触发;
    ///   - 本类是"时间型"挂点 —— 纯粹按时间轴触发,与具体敌人/Boss 解耦,
    ///     适合"第 30 秒播 warning"、"Boss 出场前 0.5 秒播 charge-up"等场景。
    ///
    /// 用法:
    ///   - TriggerTime = 触发时间(秒)
    ///   - Cue = SfxCue 资产(必填;空 = 跳过,不报错)
    ///   - Position = 世界坐标(留 UsePosition=false = 走 2D 监听,适合 UI / 全局音)
    ///   - VolumeMul / Pitch = 临时覆盖
    ///
    /// Editor Preview 期间也会播放(走 LevelEditorPlayer.TriggerOne → OnTrigger 路径);
    /// 若场景没 AudioSystem,AudioMix.PlaySfx 静默返回,不报错。
    /// </summary>
    [Serializable, SerializeReferenceEditor.SRName("Entry/Play SFX")]
    public class PlaySfxSpawnEntry : SpawnEntry
    {
        [Tooltip("要播放的 SfxCue 资产。空 = 跳过(不报错,方便临时禁用某个时机)。")]
        public SfxCue Cue;

        [Tooltip("勾上 → 在 Position 世界坐标发声(适合 3D 空间音效 / 命中点音效)。\n" +
                 "不勾 → 走 2D 监听(跟随 AudioListener,适合 UI 音 / 全局警告)。")]
        public bool UsePosition = false;

        [Tooltip("发声的世界坐标(只有 UsePosition=true 时生效)。")]
        public Vector2 Position = Vector2.zero;

        [Tooltip("临时音量倍率 [0..1]。会与 SfxCue.DefaultVolume 叠加乘。")]
        [Range(0f, 1f)] public float VolumeMul = 1f;

        [Tooltip("临时音高(1=原始,2=升八度,0.5=降八度)。会与 SfxCue.DefaultPitch 叠加乘。")]
        [Range(0.1f, 3f)] public float Pitch = 1f;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            // Cue 为空时静默跳过(对齐其他 SpawnEntry 子类的"字段缺失不报错"语义)
            if (Cue == null) return;

            AudioMix.PlaySfx(
                cue: Cue,
                position: UsePosition ? Position : (Vector2?)null,
                volumeMul: VolumeMul,
                pitch: Pitch);
        }
    }
}