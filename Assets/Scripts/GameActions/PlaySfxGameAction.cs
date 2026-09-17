using System;
using SerializeReferenceEditor;
using ShinySTG.Audio;
using UnityEngine;

namespace ShinySTG.GameActions
{
    [Serializable, SRName("Game Action/Play SFX")]
    public sealed class PlaySfxGameAction : GameAction
    {
        [Tooltip("播放后立即继续，不等待音效结束。")]
        public SfxCue Cue;
        public override GameActionRuntime CreateRuntime(GameActionContext context) => new Runtime(Cue, context.Position);
        sealed class Runtime : GameActionRuntime
        {
            readonly SfxCue _cue;
            readonly Vector2 _position;
            public Runtime(SfxCue cue, Vector2 position) { _cue = cue; _position = position; }
            public override bool IsComplete => true;
            public override void Start() => AudioMix.PlaySfx(_cue, position: _position);
        }
    }
}
