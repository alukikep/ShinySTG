using UnityEngine;

namespace ShinySTG.Effects
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BulletAfterimage : PooledEffect
    {
        [SerializeField, Min(0.01f), Tooltip("命中残影持续秒数。")]
        float _duration = 0.08f;
        [SerializeField, Min(0f), Tooltip("沿命中时运动方向继续移动的世界距离。")]
        float _distance = 0.3f;
        [SerializeField, Tooltip("横轴为生命进度，纵轴为移动进度。")]
        AnimationCurve _movement = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        [SerializeField, Tooltip("横轴为生命进度，纵轴为透明度倍率。")]
        AnimationCurve _opacity = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        static readonly int EffectOpacity = Shader.PropertyToID("_EffectOpacity");
        SpriteRenderer _renderer;
        MaterialPropertyBlock _properties;
        Vector3 _origin;
        Vector3 _direction;
        Color _color;
        float _initialOpacity;
        bool _usesEffectOpacity;

        protected override void PrepareVisual(Bullet source)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_properties == null) _properties = new MaterialPropertyBlock();
            _properties.Clear();
            var visual = source != null ? source.Renderer : null;
            _renderer.enabled = visual != null && visual.enabled && visual.gameObject.activeInHierarchy;
            if (visual == null) return;
            transform.SetPositionAndRotation(visual.transform.position, visual.transform.rotation);
            transform.localScale = visual.transform.lossyScale;
            gameObject.layer = visual.gameObject.layer;
            _renderer.sprite = visual.sprite;
            _renderer.sharedMaterial = visual.sharedMaterial;
            _renderer.color = _color = visual.color;
            _renderer.flipX = visual.flipX;
            _renderer.flipY = visual.flipY;
            _renderer.drawMode = visual.drawMode;
            _renderer.size = visual.size;
            _renderer.tileMode = visual.tileMode;
            _renderer.maskInteraction = visual.maskInteraction;
            _renderer.spriteSortPoint = visual.spriteSortPoint;
            _renderer.sortingLayerID = visual.sortingLayerID;
            _renderer.sortingOrder = visual.sortingOrder;
            visual.GetPropertyBlock(_properties);
            _usesEffectOpacity = visual.sharedMaterial != null && visual.sharedMaterial.HasProperty(EffectOpacity);
            _initialOpacity = _properties.HasFloat(EffectOpacity) ? _properties.GetFloat(EffectOpacity)
                : _usesEffectOpacity ? visual.sharedMaterial.GetFloat(EffectOpacity) : 1f;
            _renderer.SetPropertyBlock(_properties);
            _origin = transform.position;
            _direction = new Vector3(Mathf.Cos(source.SteerAngle), Mathf.Sin(source.SteerAngle), 0f)
                * (source.Speed < 0f ? -1f : 1f);
        }

        protected override bool TickVisual(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _duration));
            transform.position = _origin + _direction * (_distance * (_movement != null ? _movement.Evaluate(progress) : progress));
            float alpha = Mathf.Clamp01(_opacity != null ? _opacity.Evaluate(progress) : 1f - progress);
            if (_usesEffectOpacity)
            {
                _properties.SetFloat(EffectOpacity, _initialOpacity * alpha);
                _renderer.SetPropertyBlock(_properties);
            }
            else _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
            return progress >= 1f;
        }

        internal override void ResetPlayback()
        {
            base.ResetPlayback();
            _renderer.sprite = null;
            _renderer.sharedMaterial = null;
            _renderer.color = Color.white;
            _renderer.SetPropertyBlock(null);
            _properties.Clear();
            _origin = _direction = Vector3.zero;
            _color = Color.white;
            _initialOpacity = 1f;
            _usesEffectOpacity = false;
        }
    }
}
