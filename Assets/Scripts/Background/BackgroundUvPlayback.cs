using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShinySTG.Background
{
    /// <summary>循环布景拥有的 UV 状态；仅为背景雾材质写入每槽属性块。</summary>
    internal sealed class BackgroundUvPlayback
    {
        static readonly int SpeedId = Shader.PropertyToID("_BackgroundUvSpeed");
        static readonly int OffsetId = Shader.PropertyToID("_BackgroundUvOffset");
        readonly List<Slot> _slots = new List<Slot>();
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        struct Slot
        {
            public MeshRenderer Renderer;
            public Material Material;
            public int Index;
            public MaterialPropertyBlock Original;
        }

        public BackgroundUvPlayback(Transform root)
        {
            int layer = LayerMask.NameToLayer("Background3D");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.layer != layer) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || material.shader == null
                        || material.shader.name != "ShinySTG/Background/Distance Fog Unlit") continue;
                    var original = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, i);
                    _slots.Add(new Slot { Renderer = renderer, Material = material, Index = i,
                        Original = original.isEmpty ? null : original });
                }
            }
        }

        public void Apply(double elapsed)
        {
            foreach (var slot in _slots)
            {
                if (slot.Renderer == null || slot.Material == null) continue;
                var speed = slot.Material.GetVector(SpeedId);
                _block.Clear();
                slot.Renderer.GetPropertyBlock(_block, slot.Index);
                // 没有每槽覆盖时保留已有 Renderer 级属性；本系统独占 UV 偏移。
                if (_block.isEmpty) slot.Renderer.GetPropertyBlock(_block);
                _block.SetVector(OffsetId, new Vector4(Phase(speed.x, elapsed), Phase(speed.y, elapsed), 0f, 0f));
                slot.Renderer.SetPropertyBlock(_block, slot.Index);
            }
        }

        static float Phase(float speed, double elapsed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed)) return 0f;
            double value = speed * elapsed;
            return (float)(value - Math.Floor(value));
        }

        public void Restore()
        {
            foreach (var slot in _slots)
                if (slot.Renderer != null) slot.Renderer.SetPropertyBlock(slot.Original, slot.Index);
        }
    }
}
