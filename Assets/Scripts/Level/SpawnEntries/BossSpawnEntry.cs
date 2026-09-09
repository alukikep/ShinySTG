using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>
    /// Boss 出场条目:在指定时间点于指定位置实例化 boss prefab,并触发关卡级 OnBossSpawned 事件。
    /// 死亡收尾的职责已下沉到 Boss 子树:
    ///   - BossHealth.OnDeath → Boss 总控.HandleDeath → BossController.Stop()
    ///     → phase.OnExit + LevelController.NotifyBossDefeated + Destroy。
    /// 本类不再订阅任何 boss 内部事件,保持 SpawnEntry 子类"只管生成"的语义。
    ///
    /// 用法:
    ///   - BossPrefab 上挂 Boss 总控即可,`[RequireComponent]` 自动加挂 BossHealth + BossHitbox + BossShotCounter + BossController,无需手填。
    ///   - BossController.Start 会自动跑第一阶段,本类不主动驱动。
    /// </summary>
    [Serializable, SRName("Entry/Boss")]
    public class BossSpawnEntry : SpawnEntry
    {
        [Tooltip("Boss prefab。要求挂 BossController + BossHealth + BossShotCounter。")]
        public GameObject BossPrefab;

        [Tooltip("生成位置(世界坐标)。")]
        public Vector2 SpawnPosition = Vector2.zero;

        [Tooltip("初始朝向(度)。多数 Boss 朝下(180)或默认朝玩家。")]
        public float InitialRotation = 0f;

        [Tooltip("留口子:boss 生成后是否立刻 disable 一帧再 enable,避免 Awake 顺序问题。默认 false。")]
        public bool DeferEnable = false;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (BossPrefab == null)
            {
                Debug.LogWarning("[Level] BossSpawnEntry 缺少 BossPrefab,已跳过。", def);
                return;
            }

            GameObject go;
            if (DeferEnable)
            {
                // 防止外部脚本依赖 BossController.Awake 已跑(目前没必要,留口子)
                go = UnityEngine.Object.Instantiate(BossPrefab);
                go.SetActive(false);
                go.transform.position = SpawnPosition;
                go.transform.rotation = Quaternion.Euler(0f, 0f, InitialRotation);
                go.SetActive(true);
            }
            else
            {
                go = UnityEngine.Object.Instantiate(BossPrefab, SpawnPosition,
                                                    Quaternion.Euler(0f, 0f, InitialRotation));
            }

            runtime.Track(go);

            // 广播 OnBossSpawned:对齐 §9 "事件发送权集中在 Controller" 的协作边界。
            // UI / 计分 / 音效系统订阅 LevelController.OnBossSpawned 即可知道 boss 何时入场。
            LevelController.Instance?.NotifyBossSpawned(go);
        }
    }
}