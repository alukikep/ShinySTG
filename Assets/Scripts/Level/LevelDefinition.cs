using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Level
{
    /// <summary>
    /// 一份可复用的关卡配置(类比 BehaviorFlow / FirePattern)。
    /// 把"什么时间点生成什么敌人"封装成 SO 资产,LevelController 直接拖一份即可。
    ///
    /// 工作流:
    ///   1. 右键 Project → Create → STG → Level,创建 .asset
    ///   2. 在 Entries 数组里点 +,SR 下拉选 Simple / Wave / Boss / (未来:Conditional / Repeat / ...)
    ///   3. 把 .asset 拖到 LevelController.Definition
    /// </summary>
    [CreateAssetMenu(menuName = "STG/Level", fileName = "Level")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Timeline")]
        [Tooltip("时间轴条目。点击 + 号,通过 SR 下拉选具体类型(Entry/Simple / Entry/Wave / Entry/Boss / ...)。\n" +
                 "运行时按 TriggerTime 升序触发,时间相同按数组顺序。")]
        [SerializeReference, SR]
        public SpawnEntry[] Entries;

        [Header("Optional")]
        [Tooltip("关卡总时长(秒)。<=0 = 不限时(Duration 到也不会自动结束)。\n" +
                 "运行时供 UI / 时间缩放系统读,不强制逻辑。")]
        public float Duration = 60f;

        [Tooltip("关卡专用的 BulletPool(可选)。为空时 LevelController 会在 BeginLevel 自动 FindObjectOfType。\n" +
                 "用法:Boss 关卡挂专属弹 prefab 时配一个,普通关卡留空走场景默认池。")]
        public BulletPool Pool;

        [Header("Audio")]
        [Tooltip("勾上后,BeginLevel 时若场景里有 AudioSystem,会按 AudioBinding 自动切歌(" +
                 "OnLevelStart → AudioBinding.Playlist,OnBossSpawned → AudioBinding.BossMusic," +
                 "OnBossDefeated → AudioBinding.DefeatMusic)。\n" +
                 "取消勾选 = 此关卡不参与自动切歌(BGM 由调用方手动控制,比如过场关 / 静音关)。")]
        public bool AutoSwitchBgm = true;

        [Tooltip("此关卡对应的 LevelAudioBinding。AudioEventHub 按它切歌。空 = 不切 BGM(可与 AutoSwitchBgm 配合,实现『允许自动切但本关不切』)。\n" +
                 "关卡编辑器工具栏有「Create AudioBinding」一键创建并自动反引用。")]
        public ShinySTG.Audio.LevelAudioBinding AudioBinding;
    }
}