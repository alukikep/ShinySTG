using System;
using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 子机位置模式(多态)。跟项目 MoveBehaviour / EnemyAction 一样用 [SerializeReference] + [SRName],
    /// 可以在 Inspector 下拉选不同形态,无需改代码即可扩展。
    ///
    /// 每种实现代表"子机相对玩家本体的偏移",用 Vector2 = 本地坐标(右 +X, 上 +Y,玩家默认朝上)。
    /// 子机编号:0 = 左 / 1 = 右 / 2 = 左后 / 3 = 右后(顺序由 OptionPositionForm 自己决定)。
    ///
    /// Focus 模式(FocusHeld)由 PlayerOptions 决定:大多数实现直接让偏移更近(贴近玩家),
    /// 这跟东方系列的子机行为一致。
    /// </summary>
    [Serializable]
    public abstract class OptionPositionForm
    {
        /// <summary>PlayerOptions 每帧调用一次,返回该编号 option 的本地偏移。</summary>
        /// <param name="index">子机编号(0..Count-1)。</param>
        /// <param name="focus">是否处于 Focus(集中)模式。</param>
        /// <param name="count">当前活跃子机总数(便于实现"动态分散"逻辑)。</param>
        public abstract Vector2 GetOffset(int index, bool focus, int count);

        /// 可选:仅在 PlayerOptions 进入时调用一次(适合做"初始化位置"动画之类)。
        public virtual void OnEnter(PlayerOptions owner) { }
        /// 可选:PlayerOptions 销毁或切换形态时调用。
        public virtual void OnExit(PlayerOptions owner) { }
    }
}