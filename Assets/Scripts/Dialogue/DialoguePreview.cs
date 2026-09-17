using UnityEngine;

namespace ShinySTG.Dialogue
{
    /// <summary>独立场景试播入口，正式关卡由动作宿主调用 DialogueService.Play。</summary>
    [AddComponentMenu("ShinySTG/Dialogue/Dialogue Preview")]
    public sealed class DialoguePreview : MonoBehaviour
    {
        [SerializeField, Tooltip("用于试播的服务。")]
        DialogueService _service;
        [SerializeField, Tooltip("试播的对话资产。")]
        DialogueDefinition _dialogue;
        [SerializeField, Tooltip("进入 Play Mode 后自动试播。")]
        bool _playOnStart = true;
        DialogueHandle _handle;

        void Start() { if (_playOnStart) Play(); }

        [ContextMenu("Play Dialogue (Play Mode)")]
        public void Play()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (_service == null) { Debug.LogError("[Dialogue] 试播未配置服务。", this); return; }
            if (_handle != null && !_handle.IsComplete) return;
            _handle = _service.Play(_dialogue);
        }

        void OnDisable() { _handle?.Cancel(); _handle = null; }
    }
}
