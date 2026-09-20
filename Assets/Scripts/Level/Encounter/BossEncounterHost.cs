using UnityEngine;

namespace ShinySTG.Level.Encounter
{
    /// <summary>场景直接放置 Boss 时驱动遭遇；宿主独立于 Boss，允许销毁后继续收尾。</summary>
    public sealed class BossEncounterHost : MonoBehaviour
    {
        BossEncounterRuntime _runtime;
        public void Initialize(BossEncounterRuntime runtime) => _runtime = runtime;
        void Update()
        {
            if (_runtime == null) return;
            _runtime.Tick(Time.deltaTime);
            if (!_runtime.IsComplete) return;
            _runtime.Dispose();
            _runtime = null;
            Destroy(gameObject);
        }
        void OnDestroy() => _runtime?.Dispose();
    }
}
