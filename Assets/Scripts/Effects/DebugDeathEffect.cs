using UnityEngine;

namespace ShinySTG.Effects
{
    /// <summary>无需贴图的临时死亡表现，复用线段和圆环，不在每次播放时创建对象。</summary>
    public sealed class DebugDeathEffect : PooledEffect
    {
        [SerializeField, Tooltip("线条使用的透明材质。")]
        Material _material;
        [SerializeField, Tooltip("主色。")]
        Color _color = new Color(1f, 0.65f, 0.15f, 1f);
        [SerializeField, Min(0.01f), Tooltip("持续秒数。")]
        float _duration = 0.45f;
        [SerializeField, Min(0.01f), Tooltip("扩散半径，世界单位（根节点缩放为 1 时）。")]
        float _radius = 0.8f;
        [SerializeField, Range(0, 32), Tooltip("向外飞散的火花数量。")]
        int _sparkCount = 12;
        [SerializeField, Tooltip("显示扩散圆环。")]
        bool _showRing = true;
        [SerializeField, Tooltip("显示中心十字闪光。")]
        bool _showFlash = true;
        [SerializeField, Tooltip("渲染排序层名称，需与项目已有层名称一致。")]
        string _sortingLayer = "Default";
        [SerializeField, Tooltip("层内排序，建议高于敌人。")]
        int _sortingOrder = 20;

        LineRenderer _ring;
        LineRenderer[] _sparks;
        LineRenderer[] _flash;
        float _angleOffset;

        LineRenderer CreateLine(string label, int points, bool loop = false)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = _material;
            line.positionCount = points;
            line.loop = loop;
            line.numCapVertices = 2;
            line.sortingLayerName = _sortingLayer;
            line.sortingOrder = _sortingOrder;
            return line;
        }

        protected override void PrepareVisual(Bullet source)
        {
            if (_ring == null)
            {
                _ring = CreateLine("Ring", 48, true);
                _sparks = new LineRenderer[Mathf.Clamp(_sparkCount, 0, 32)];
                for (int i = 0; i < _sparks.Length; i++) _sparks[i] = CreateLine("Spark", 2);
                _flash = new[] { CreateLine("Flash X", 2), CreateLine("Flash Y", 2) };
            }
            _angleOffset = Random.Range(0f, Mathf.PI * 2f);
            TickVisual(0f);
        }

        void Style(LineRenderer line, float width, Color color)
        {
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
        }

        protected override bool TickVisual(float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _duration));
            float expansion = 1f - (1f - t) * (1f - t);
            var fading = _color;
            fading.a *= (1f - t) * (1f - t);
            _ring.enabled = _showRing;
            Style(_ring, Mathf.Lerp(0.07f, 0.015f, t), fading);
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f)
                    * (_radius * Mathf.Lerp(0.1f, 1f, expansion)));
            }
            for (int i = 0; i < _sparks.Length; i++)
            {
                float angle = _angleOffset + i * Mathf.PI * 2f / _sparks.Length;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                float distance = _radius * expansion * (i % 2 == 0 ? 1.2f : 0.8f);
                _sparks[i].enabled = true;
                Style(_sparks[i], 0.045f * (1f - t), fading);
                _sparks[i].SetPosition(0, direction * distance);
                _sparks[i].SetPosition(1, direction * (distance + _radius * 0.22f * (1f - t)));
            }
            float flash = Mathf.Clamp01(1f - t * 4f);
            for (int i = 0; i < _flash.Length; i++)
            {
                var axis = i == 0 ? Vector3.right : Vector3.up;
                _flash[i].enabled = _showFlash && flash > 0f;
                Style(_flash[i], _radius * 0.18f * flash, new Color(1f, 1f, 1f, flash));
                _flash[i].SetPosition(0, -axis * _radius * 0.45f * flash);
                _flash[i].SetPosition(1, axis * _radius * 0.45f * flash);
            }
            return t >= 1f;
        }

        internal override void ResetPlayback()
        {
            base.ResetPlayback();
            _ring.enabled = false;
            foreach (var line in _sparks) line.enabled = false;
            foreach (var line in _flash) line.enabled = false;
            _angleOffset = 0f;
        }
    }
}
