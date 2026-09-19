using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI
{
    /// <summary>只负责显示；不保存或结算玩家资源。</summary>
    public sealed class GameplayHudView : MonoBehaviour
    {
        [SerializeField, Tooltip("分数文本，完整保留 long 精度。")]
        TMP_Text _score;
        [SerializeField, Tooltip("备用残机数量文本。")]
        TMP_Text _lives;
        [SerializeField, Tooltip("Bomb 库存文本。")]
        TMP_Text _bombs;
        [SerializeField, Tooltip("火力文本。")]
        TMP_Text _power;
        [SerializeField, Tooltip("擦弹文本。")]
        TMP_Text _graze;
        [SerializeField, Tooltip("固定数量的备用残机图标槽。")]
        Image[] _lifeIcons;
        [SerializeField, Tooltip("固定数量的 Bomb 图标槽。")]
        Image[] _bombIcons;
        [SerializeField, Tooltip("可用残机颜色。")]
        Color _lifeColor = new Color(1f, 0.48f, 0.7f);
        [SerializeField, Tooltip("可用 Bomb 颜色。")]
        Color _bombColor = new Color(0.4f, 0.9f, 0.8f);
        [SerializeField, Tooltip("空图标槽颜色。")]
        Color _emptyColor = new Color(0.2f, 0.23f, 0.32f);

        public void SetScore(long value) => SetText(_score, value.ToString("N0", CultureInfo.InvariantCulture));
        public void SetLives(int totalLives)
        {
            int spare = totalLives > 0 ? totalLives - 1 : 0;
            SetText(_lives, spare.ToString(CultureInfo.InvariantCulture));
            SetIcons(_lifeIcons, spare, _lifeColor);
        }
        public void SetBombs(int value)
        {
            SetText(_bombs, value.ToString(CultureInfo.InvariantCulture));
            SetIcons(_bombIcons, value, _bombColor);
        }
        public void SetPower(int units, int maximum) => SetText(_power,
            (units / 100m).ToString("F2", CultureInfo.InvariantCulture) + " / " +
            maximum.ToString("F2", CultureInfo.InvariantCulture));
        public void SetGraze(int value) => SetText(_graze, value.ToString("N0", CultureInfo.InvariantCulture));

        public void Clear()
        {
            SetText(_score, "—");
            SetText(_lives, "—");
            SetText(_bombs, "—");
            SetText(_power, "—");
            SetText(_graze, "—");
            SetIcons(_lifeIcons, 0, _lifeColor);
            SetIcons(_bombIcons, 0, _bombColor);
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }

        void SetIcons(Image[] icons, int count, Color color)
        {
            if (icons == null) return;
            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null) icons[i].color = i < count ? color : _emptyColor;
        }
    }
}
