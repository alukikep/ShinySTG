using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Level.SpawnEntries
{
    /// <summary>
    /// 同一时间点批量生成一组敌人,沿 X 轴等距铺开(经典 STG 小怪阵)。
    /// 不预设为特定阵型 —— 给中心点 + 间距 + prefab 数组即可,后续若要 V 字 / 圆弧,
    /// 新建子类 CurvedWaveSpawnEntry 覆盖 OnTrigger,不动本类。
    /// </summary>
    [Serializable, SRName("敌人生成/Wave")]
    public class WaveSpawnEntry : SpawnEntry
    {
        [Tooltip("这一波包含的预制体(数组里可重复,允许同 prefab 出现多次)。")]
        public GameObject[] Prefabs;

        [Tooltip("每只敌人沿 X 方向的像素间隔(Unity 单位)。")]
        public float SpacingX = 0.5f;

        [Tooltip("整波中心点(世界坐标)。N 只敌人会以此为中心对称铺开。")]
        public Vector2 CenterPosition = Vector2.zero;

        [Tooltip("每只敌人的初始朝向(度)。同 SimpleSpawnEntry 语义。")]
        public float InitialRotation = 0f;

        public override void OnTrigger(LevelRuntime runtime, LevelDefinition def)
        {
            if (Prefabs == null || Prefabs.Length == 0)
            {
                Debug.LogWarning("[Level] WaveSpawnEntry 缺少 Prefabs,已跳过。", def);
                return;
            }
            // 检查至少有一个有效 prefab
            int validCount = 0;
            for (int i = 0; i < Prefabs.Length; i++)
                if (Prefabs[i] != null) validCount++;
            if (validCount == 0)
            {
                Debug.LogWarning("[Level] WaveSpawnEntry Prefabs 全是空槽,已跳过。", def);
                return;
            }

            int count = Prefabs.Length;
            // 以 CenterPosition 为中心点对称铺:第一只 x = center - (count-1)/2 * spacing
            float startX = CenterPosition.x - (count - 1) * 0.5f * SpacingX;
            for (int i = 0; i < count; i++)
            {
                var prefab = Prefabs[i];
                if (prefab == null) continue;

                var go = UnityEngine.Object.Instantiate(prefab);
                go.SetActive(false);
                go.transform.position = new Vector3(startX + i * SpacingX, CenterPosition.y, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, InitialRotation);
                go.SetActive(true);

                runtime.Track(go);
            }
        }
    }
}