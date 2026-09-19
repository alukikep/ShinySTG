using System;
using SerializeReferenceEditor;
using ShinySTG.Presentation.SpellDeclaration;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Play Spell Declaration")]
    public sealed class PlaySpellDeclarationAction : GameAction
    {
        [Tooltip("符卡名称、立绘、音效与宣言动画配置。场景需有唯一启用的宣言服务。")]
        public SpellDeclarationDefinition Declaration;
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Declaration);

        sealed class Runtime : GameActionRuntime
        {
            readonly SpellDeclarationDefinition _definition;
            SpellDeclarationHandle _handle;
            public Runtime(SpellDeclarationDefinition definition) => _definition = definition;
            public override void Start()
            {
                SpellDeclarationService service = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<SpellDeclarationService>())
                {
                    if (!candidate.isActiveAndEnabled) continue;
                    if (service != null) throw new InvalidOperationException("[Spell Declaration] 存在多个启用的宣言服务。");
                    service = candidate;
                }
                if (service == null) throw new InvalidOperationException("[Spell Declaration] 缺少启用的宣言服务。");
                _handle = service.Play(_definition);
            }
            public override bool IsComplete
            {
                get
                {
                    if (_handle?.Failure != null) throw _handle.Failure;
                    if (_handle != null && _handle.IsCancelled)
                        throw new OperationCanceledException("[Spell Declaration] 宣言被外部取消。");
                    return _handle != null && _handle.IsComplete;
                }
            }
            public override void Dispose() { _handle?.Cancel(); _handle = null; }
        }
    }
}
