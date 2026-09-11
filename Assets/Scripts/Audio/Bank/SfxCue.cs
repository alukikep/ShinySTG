using System;
using System.Collections.Generic;
using UnityEngine;
using SerializeReferenceEditor;  // [SerializeReference] + SRName(SfxRule 多态下拉)

namespace ShinySTG.Audio
{
    /// <summary>
    /// 单个音效配置(短促的 SFX,如"子弹命中"、"敌人死亡"、"UI 点击")。
    ///
    /// ★ 设计要点 ★
    ///   - 一个 Cue = 一组 AudioClip + 处理规则 + 路由参数。完全数据驱动,Inspector 配置。
    ///   - 调用方不感知这些细节,只通过 AudioMix.PlaySfx(cue) 触发,由 SfxRouter 内部按规则处理。
    ///   - 多态规则走 SerializeReference(SRName 下拉) —— 与项目既有 FireExtension / EnemyAction
    ///     / BossPhase / BossSignal / OptionPositionForm 同套路。
    ///
    /// 字段速查:
    ///   - Clips           : 候选 clip 数组(单 clip 也合法;配合 RandomPickRule 才有意义)
    ///   - DefaultVolume   : 默认音量倍率 [0..1](可被调用方 volumeMul 临时覆盖)
    ///   - DefaultPitch    : 默认 pitch(可被调用方 pitch 临时覆盖)
    ///   - Loop            : 是否循环(用于"机枪持续声"、"引擎轰鸣"这类长音)
    ///   - Bus             : 路由到哪个总线(Master/Bgm/Sfx/UI/...);空 = Master
    ///   - Priority        : 路由优先级(数值越大越优先,池满时低优先级先丢;默认 128)
    ///   - MaxVoices       : 同 cue 同时最多 voice 数,0 = 不限;超出时丢最老
    ///   - Cooldown        : 同 cue 全局最小间隔(秒);0 = 无冷却(防"哒哒哒哒"一片)
    ///   - Rules           : 处理规则数组(随机抽 clip / pitch 抖动 / ...),按数组顺序串行执行
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Audio/SFX Cue", fileName = "NewSfxCue", order = 100)]
    public class SfxCue : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("候选 clip 数组。单 clip 也合法;配合 Rule/Random Pick 才有意义。\n" +
                 "数组为空时该 cue 不播放(但不报错),方便临时禁用某个音效。")]
        public AudioClip[] Clips;

        [Header("Playback")]
        [Range(0f, 1f)]
        [Tooltip("默认音量倍率 [0..1]。调用方可用 volumeMul 临时覆盖(叠加乘)。")]
        public float DefaultVolume = 1f;

        [Tooltip("默认 pitch(1 = 原始音高,2 = 升八度,0.5 = 降八度)。调用方可用 pitch 临时覆盖。")]
        public float DefaultPitch = 1f;

        [Tooltip("是否循环(用于「机枪持续声」「引擎轰鸣」这类长音)。\n" +
                 "循环音调用方需自己调 AudioMix.StopLooped(handle) 停止。")]
        public bool Loop = false;

        [Header("Routing")]
        [Tooltip("路由到哪个总线(Master/Bgm/Sfx/UI/...)。空 = Master(默认走 Master 不走任何 AudioMixer Group)。")]
        public AudioBus Bus;

        [Range(0, 255)]
        [Tooltip("路由优先级(数值越大越优先,池满时低优先级先丢)。\n" +
                 "默认 128。建议:UI=200,玩家射击=160,敌人爆炸=140,环境音=80。")]
        public int Priority = 128;

        [Header("Throttling")]
        [Tooltip("同 cue 同时最多 voice 数。0 = 不限。\n" +
                 "STG 高弹量场景必备 —— 默认 4 可防止「哒哒哒哒」一片。\n" +
                 "超出时丢最老的 voice(保持最新一次播放)。")]
        public int MaxVoices = 4;

        [Tooltip("同 cue 全局最小播放间隔(秒)。0 = 无冷却。\n" +
                 "适合「密集小事件」(子弹命中、连击)防止声音糊成一片。")]
        public float Cooldown = 0f;

        [Header("Rules (多态处理管道,按数组顺序串行)")]
        [Tooltip("处理规则数组(随机抽 clip / pitch 抖动 / ...)。\n" +
                 "按数组顺序串行执行 —— 与 FireExtension 的 Pipeline 模型同款。\n" +
                 "每个规则是一个普通 C# 类(SRName 下拉选),不是 SO —— 与 EnemyAction 同款。")]
        [SerializeReference]
        public SfxRule[] Rules;

        /// <summary>空 cue(无 clip)直接跳过,避免 NullRef。供 SfxRouter 调用前先检查。</summary>
        public bool IsEmpty => Clips == null || Clips.Length == 0 || Array.TrueForAll(Clips, c => c == null);
    }
}
