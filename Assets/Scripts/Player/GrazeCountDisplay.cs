using UnityEngine;

namespace ShinySTG.Player
{
    /// <summary>
    /// 临时调试组件:在屏幕左上角 OnGUI 显示 Lives / GrazeCount,
    /// 并提供一个按钮直接调 PlayerHealth.DebugTriggerGraze(),
    /// 用于在 Play Mode 中不依赖敌人发射也能验证擦弹计数是否生效。
    ///
    /// 用法:挂到 Player GameObject 上即可。验证完成后删除本脚本 + Remove Component。
    /// </summary>
    public class GrazeCountDisplay : MonoBehaviour
    {
        PlayerHealth _health;

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        void OnGUI()
        {
            if (_health == null) return;

            // 显示 Lives / GrazeCount(左上角固定位置)
            GUI.Box(new Rect(10, 10, 220, 70), "Player Stats");
            GUI.Label(new Rect(20, 35, 200, 20),
                "Lives: " + _health.Lives + "   Power: " + _health.PowerLevel);
            GUI.Label(new Rect(20, 55, 200, 20),
                "GrazeCount: " + _health.GrazeCount);

            // 调试按钮:点一下触发一次擦弹累加(不依赖敌人发射)
            if (GUI.Button(new Rect(10, 90, 220, 30), "Debug: Trigger Graze +1"))
            {
                _health.DebugTriggerGraze();
            }
        }
    }
}
