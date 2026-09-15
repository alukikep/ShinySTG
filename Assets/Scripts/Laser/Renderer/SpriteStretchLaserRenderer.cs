using UnityEngine;

namespace ShinySTG.Laser
{
    /// <summary>
    /// 默认 Renderer:用 Body Sprite 按 VisualWidth + CurrentLength 拉伸。
    /// 头贴图(可选)显示在激光起点;激光本体跟随长度缩放 X。
    ///
    /// 视觉表现:
    ///   - Warning   → 显示一个固定长度(预警长度,可配)的细条,WarningColor。
    ///   - Expanding → 本体从 0 长度增长,BodyColor;头贴图常驻。
    ///   - Active    → 本体全长,BodyColor;头贴图亮色(HeadColor)。
    ///   - Shrinking → 本体收缩,BodyColor。
    ///   - Dead      → GameObject SetActive(false),Renderer 不再绘制。
    ///
    /// 子物体结构(prefab 美术可挂):
    ///   LaserEntity (根)
    ///     ├─ SpriteStretchLaserRenderer (本类)
    ///     ├─ Body      (SpriteRenderer,按 CurrentLength 拉伸 X,不动旋转)
    ///     ├─ Head      (SpriteRenderer,在原点不动)
    ///     └─ Warning   (SpriteRenderer,Warning 期显示一条全长的预警线)
    /// 子类用 GetComponentsInChildren 抓(包含 inactive),与 Bullet.Renderer 抓取方式一致。
    /// </summary>
    public class SpriteStretchLaserRenderer : LaserRendererBase
    {
        [Tooltip("Body 子物体名(默认 \"Body\")。SpriteRenderer 在该子物体上,按 CurrentLength 拉伸 X。")]
        public string BodyChildName = "Body";

        [Tooltip("Head 子物体名(默认 \"Head\",可空 = 不画头)。")]
        public string HeadChildName = "Head";

        [Tooltip("Warning 子物体名(默认 \"Warning\",可空 = 不画预警线)。")]
        public string WarningChildName = "Warning";

        [Tooltip("Warning 期的预警线长度(世界单位)。默认 = 整长 MaxLength。\n" +
                 "调小可让预警线比最终激光短(经典东方行为:预警只显示激光前段)。")]
        public float WarningLineLength = -1f; // < 0 = 用 Data.MaxLength

        SpriteRenderer _body, _head, _warning;
        Color _bodyBaseColor = Color.white;
        Color _headBaseColor = Color.white;

        public override void OnLaserInit(LaserEntity laser)
        {
            var data = laser.Data;
            // 抓子 Renderer(包含 inactive,允许 Head/Warning 在初始时 SetActive(false) 但存在)
            _body = FindChild<Renderer>(BodyChildName) as SpriteRenderer
                    ?? FindComponentInChildren<SpriteRenderer>(BodyChildName);
            _head = FindComponentInChildren<SpriteRenderer>(HeadChildName);
            _warning = FindComponentInChildren<SpriteRenderer>(WarningChildName);

            if (_body != null && data != null && data.Sprite != null) _body.sprite = data.Sprite;
            if (_head != null && data != null && data.HeadSprite != null) _head.sprite = data.HeadSprite;
            if (_warning != null && _body != null && data != null && data.Sprite != null) _warning.sprite = data.Sprite;

            if (_body != null)
            {
                _bodyBaseColor = _body.color;
            }
            if (_head != null) _headBaseColor = _head.color;

            ApplyInitColors(laser);
            ApplyStateVisuals(laser);
        }

        public override void OnLaserTick(LaserEntity laser)
        {
            ApplyStateVisuals(laser);
        }

        void ApplyInitColors(LaserEntity laser)
        {
            var data = laser.Data;
            if (data == null) return;
            if (_body != null) _body.color = data.BodyColor;
            if (_head != null) _head.color = data.HeadColor;
            if (_warning != null) _warning.color = data.WarningColor;
        }

        void ApplyStateVisuals(LaserEntity laser)
        {
            var data = laser.Data;
            float visualWidth = laser.VisualWidth;
            float length = laser.CurrentLength;

            // ─── Body 拉伸 + 显隐 ───
            if (_body != null)
            {
                if (laser.State == LaserState.Warning || laser.State == LaserState.Dead)
                {
                    // Warning 期不显示本体(本体由 Warning 子物体显示)
                    _body.gameObject.SetActive(false);
                }
                else
                {
                    if (!_body.gameObject.activeSelf) _body.gameObject.SetActive(true);
                    // X 缩放 = 当前长度(以 prefab 基准 _bodyBaseLocalX 为 1)
                    // Y 缩放保持基准,视觉宽度由 SpriteRenderer drawMode=Sliced 或 size 控制,
                    // 这里走最简的 transform.localScale.x = length 拉伸。
                    Vector3 ls = _body.transform.localScale;
                    // ★ FIX:直接用 length(世界长度),不再乘 _bodyBaseLocalX。
                    //   旧实现 bug:OnLaserInit 从 _body.transform.localScale.x 读 _bodyBaseLocalX,
                    //   但这个值会被前一次发射的 Tick 污染(Shrinking 末期接近0),
                    //   导致池复用时每次读到的 baseLocalX 都比上一次小,Active 期 ls.x 越来越短。
                    //   修复后代码直接用世界长度,prefab 里 Body 的 localScale.x 应保持 1.0(美术不需改)。
                    ls.x = Mathf.Max(0.0001f, length);
                    ls.y = visualWidth;
                    _body.transform.localScale = ls;
                    // 朝向:跟激光方向(根 transform 已 LateUpdate 设置过 z 旋转,本体不转)
                }
            }

            // ─── Warning 细线:Warning 期显示,长度 = WarningLineLength(默认 MaxLength) ───
            if (_warning != null)
            {
                bool showWarning = laser.State == LaserState.Warning;
                if (_warning.gameObject.activeSelf != showWarning)
                    _warning.gameObject.SetActive(showWarning);
                if (showWarning)
                {
                    Vector3 ls = _warning.transform.localScale;
                    float warnLen = WarningLineLength >= 0f
                        ? WarningLineLength
                        : (data != null ? data.MaxLength : length);
                    ls.x = Mathf.Max(0.0001f, warnLen);
                    ls.y = Mathf.Max(0.0001f, visualWidth * 0.4f); // 预警线更细
                    _warning.transform.localScale = ls;
                }
            }
        }

        // ─── 子物体查找 helpers ───
        T FindChild<T>(string name) where T : Component
        {
            if (string.IsNullOrEmpty(name)) return null;
            var t = transform.Find(name);
            return t != null ? t.GetComponent<T>() : null;
        }

        T FindComponentInChildren<T>(string name) where T : Component
        {
            if (string.IsNullOrEmpty(name)) return null;
            var all = GetComponentsInChildren<T>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.name == name) return all[i];
            }
            return null;
        }
    }
}
