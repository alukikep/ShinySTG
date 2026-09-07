using System;
using SerializeReferenceEditor;
using UnityEngine;

namespace ShinySTG.Player
{
    // ─────────────────────────────────────────────────────────────────────
    // 内置子机位置模式
    // ─────────────────────────────────────────────────────────────────────
    //
    // 全部基于"以玩家为中心,左右对称扩散"的经典东方式布局。
    // 想加新形态?新建一个 : OptionPositionForm 的类,加 [Serializable, SRName("Form/<你的>")],
    // 就能在 Inspector 下拉里看到。

    /// <summary>
    /// 经典东方对称式:
    ///   - 2 子机:左右紧贴
    ///   - 3 子机:左前 / 右前 / 后(中间偏后)
    ///   - 4 子机:左前 / 右前 / 左后 / 右后(东方标准满级布局)
    /// Focus 时整体朝玩家靠近。
    /// </summary>
    [Serializable, SRName("Form/Touhou Symmetric")]
    public class TouhouSymmetricForm : OptionPositionForm
    {
        public float NormalSide = 0.6f;    // 正常左右偏移(横)
        public float NormalBack = 1.2f;    // 正常后方偏移(纵)
        public float FocusSide  = 0.4f;    // Focus 偏移
        public float FocusBack  = 0.5f;

        public override Vector2 GetOffset(int index, bool focus, int count)
        {
            float side = focus ? FocusSide : NormalSide;
            float back = focus ? FocusBack : NormalBack;

            // 4 子机:左前 / 右前 / 左后 / 右后(经典满级)
            // 3 子机:左前 / 右前 / 后(中央)
            // 2 子机:左前 / 右前
            // 1 子机:左前(单独存在时略偏左,跟其他实现不同避免完全重合)
            switch (count)
            {
                case 1: return new Vector2(-side, back);
                case 2: return index == 0 ? new Vector2(-side, back) : new Vector2(side, back);
                case 3:
                    if (index == 0) return new Vector2(-side, back);
                    if (index == 1) return new Vector2( side, back);
                    return new Vector2(0f, -back * 0.5f); // 中后
                default: // 4+
                    if (index == 0) return new Vector2(-side, back);
                    if (index == 1) return new Vector2( side, back);
                    if (index == 2) return new Vector2(-side, -back);
                    return new Vector2( side, -back);
            }
        }
    }

    /// <summary>
    /// 线性横排:所有子机一字排开,平均分布在左右两侧,Focus 时收拢到玩家身边。
    /// </summary>
    [Serializable, SRName("Form/Linear Row")]
    public class LinearRowForm : OptionPositionForm
    {
        public float Spacing = 0.5f;   // 相邻子机的间距
        public float Forward = 0.5f;   // 整体前推多少

        public override Vector2 GetOffset(int index, bool focus, int count)
        {
            // 在左右两侧按 1, -1, 3, -3 ... 的奇数倍放置
            // 居中且不重叠:0=右+1, 1=左-1, 2=右+3, 3=左-3 ...
            float sideIndex = (index / 2) + 1;       // 0→1, 1→1, 2→2, 3→2
            float sign = (index % 2 == 0) ? 1f : -1f; // 偶数右,奇数左
            float x = sign * sideIndex * Spacing;
            float y = Forward + (focus ? 0f : 0.2f);
            return new Vector2(x, y);
        }
    }

    /// <summary>
    /// 后方一字排:所有子机在玩家身后一字排列(便于躲弹幕)。
    /// </summary>
    [Serializable, SRName("Form/Rear Line")]
    public class RearLineForm : OptionPositionForm
    {
        public float Spacing = 0.5f;
        public override Vector2 GetOffset(int index, bool focus, int count)
        {
            // -3 -1 1 3 ... 对称分布,全部在玩家身后
            float sideIndex = (index / 2) + 1;
            float sign = (index % 2 == 0) ? 1f : -1f;
            float x = sign * sideIndex * Spacing;
            float y = -(focus ? 0.3f : 0.6f);
            return new Vector2(x, y);
        }
    }
}