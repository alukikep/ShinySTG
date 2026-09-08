using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>
    /// Boss 出场条目 —— 当前为留壳版本,只负责 "生成 boss prefab 到指定位置"。
    /// 后续要接 BossController.EnterPhase / 多阶段 / OnDefeated 信号时,
    /// 在本类里覆盖 OnTrigger 的 boss 启动部分即可,不动 LevelController。
    ///
    /// 用法:
    ///   - BossPrefab 上必须挂 BossController + BossHealth + BossShotCounter(BossController 的 RequireComponent 不在文件里,需手动配齐)。
    ///   - 本类当前只调 Instantiate + 摆位,不主动 Start BossController
    ///     (因为 BossController 已有 Start 自己跑第一阶段)。
    ///   - 留给未来:接 OnBossDefeated → LevelController.OnBossDefeated → 后续 SpawnEntry 可订阅。
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
            // 留口:未来可在此订阅 BossHealth.OnDeath → LevelController.NotifyBossDefeated(go)
        }
    }
}