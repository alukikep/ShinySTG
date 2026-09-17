using System;
using SerializeReferenceEditor;
using ShinySTG.Dialogue;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Play Dialogue")]
    public sealed class PlayDialogueAction : GameAction
    {
        [Tooltip("要播放的对话资产。场景中需有唯一启用的 DialogueService。")]
        public DialogueDefinition Dialogue;
        [Tooltip("对话期间阻止玩家移动、低速操作与主炮/子机射击。")]
        public bool LockPlayerControls = true;

        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(this);

        sealed class Runtime : GameActionRuntime
        {
            readonly PlayDialogueAction _config;
            DialogueHandle _handle;

            public Runtime(PlayDialogueAction config) => _config = config;

            public override void Start()
            {
                DialogueService service = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<DialogueService>())
                {
                    if (!candidate.isActiveAndEnabled) continue;
                    if (service != null)
                        throw new InvalidOperationException("[Dialogue] 场景中存在多个启用的 DialogueService。");
                    service = candidate;
                }
                if (service == null)
                    throw new InvalidOperationException("[Dialogue] 场景中缺少启用的 DialogueService。");
                _handle = service.Play(_config.Dialogue, _config.LockPlayerControls);
            }

            public override bool IsComplete
            {
                get
                {
                    if (_handle?.Failure != null) throw _handle.Failure;
                    if (_handle != null && _handle.IsCancelled)
                        throw new OperationCanceledException("[Dialogue] 对话被外部取消，终止后续动作。");
                    return _handle != null && _handle.IsComplete;
                }
            }

            public override void Dispose()
            {
                _handle?.Cancel();
                _handle = null;
            }
        }
    }
}
