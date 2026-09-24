using System;
using UnityEngine;
using ShinySTG.Items;

public enum BulletClearPresentationMode { Silent, BurstEffect, ConvertToItems }

[Serializable]
public sealed class BulletClearPresentation
{
    public BulletClearPresentationMode Mode = BulletClearPresentationMode.Silent;
    [Min(1)] public int MaxEffectCount = 24;
    [Min(0.1f)] public float EffectGridSize = 1f;
    public GameObject EffectPrefab;
    [Min(1)] public int MaxItemCount = 32;
    [Min(0f)] public float ItemsPerBullet = 0.05f;
    public ItemDefinition Item;
    [Min(0f)] public float ItemScatterRadius = 0.15f;
    [Min(0f)] public float ItemSpeed = 2f;
}
