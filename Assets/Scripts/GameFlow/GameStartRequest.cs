namespace ShinySTG.GameFlow
{
    /// <summary>每次开局的不可变参数，不携带旧场景对象。</summary>
    public sealed class GameStartRequest
    {
        public CharacterDefinition Character { get; }
        public StageDefinition Stage { get; }

        public GameStartRequest(CharacterDefinition character, StageDefinition stage)
        {
            Character = character;
            Stage = stage;
        }

        public bool Validate(out string error)
        {
            error = null;
            if (Character == null || Character.PlayerPrefab == null)
                error = "角色未配置玩家 prefab。";
            else if (!Character.PlayerPrefab.gameObject.activeSelf || !Character.PlayerPrefab.enabled)
                error = "玩家 prefab 及 Player 组件必须启用。";
            else if (Stage == null || Stage.Level == null)
                error = "未配置首关或关卡时间轴。";
            else if (string.IsNullOrWhiteSpace(Stage.ScenePath))
                error = "关卡场景路径为空。";
            else if (!Finite(Stage.SpawnPosition.x) || !Finite(Stage.SpawnPosition.y) || !Finite(Stage.SpawnPosition.z))
                error = "出生位置必须为有限数值。";
            return error == null;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
