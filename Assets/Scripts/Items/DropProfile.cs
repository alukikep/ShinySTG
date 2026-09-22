using System;
using UnityEngine;

namespace ShinySTG.Items
{
    [CreateAssetMenu(menuName = "STG/Items/Drop Profile")]
    public sealed class DropProfile : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("生成的道具定义。")]
            public ItemDefinition Item;
            [Min(0), Tooltip("生成实体的数量。")]
            public int Count = 1;
            [Range(0f, 1f), Tooltip("该条目在一次掉落中的命中概率；1 为必定掉落。命中后生成全部数量。")]
            public float DropChance = 1f;
        }

        [Tooltip("可同时生成多种道具；每个条目独立按概率抽样，空项和非正数量被忽略。")]
        public Entry[] Entries;
        [Tooltip("撒出中心方向，90 度为向上。")]
        public float DirectionDegrees = 90f;
        [Range(0f, 360f), Tooltip("撒出扇形角度。")]
        public float SpreadDegrees = 120f;
        [Tooltip("初始速度范围，运行时自动排序并限制非负。")]
        public Vector2 SpeedRange = new Vector2(2f, 4f);
        [Min(0f), Tooltip("出生位置的随机散布半径。")]
        public float SpawnRadius = 0.15f;
    }
}
