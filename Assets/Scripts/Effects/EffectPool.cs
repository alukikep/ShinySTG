using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinySTG.Effects
{
    /// <summary>按 prefab、所属场景分池；场景卸载同时清理活跃和闲置特效。</summary>
    public sealed class EffectPool : MonoBehaviour
    {
        const int MaxInactivePerPrefab = 64;
        readonly Dictionary<GameObject, Stack<PooledEffect>> _buckets = new();
        static readonly Dictionary<int, EffectPool> _scenes = new();
        Transform _inactiveRoot;
        int _sceneHandle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _scenes.Clear();

        public static PooledEffect Play(GameObject prefab, Vector3 position, Scene scene, Bullet source = null)
        {
            if (prefab == null || !scene.IsValid() || !scene.isLoaded) return null;
            if (!_scenes.TryGetValue(scene.handle, out var pool) || pool == null)
            {
                var root = new GameObject("Effect Pool");
                SceneManager.MoveGameObjectToScene(root, scene);
                pool = root.AddComponent<EffectPool>();
                pool._sceneHandle = scene.handle;
                var inactive = new GameObject("Inactive");
                inactive.transform.SetParent(root.transform, false);
                inactive.SetActive(false);
                pool._inactiveRoot = inactive.transform;
                _scenes[scene.handle] = pool;
            }
            return pool.Rent(prefab, position, source);
        }

        PooledEffect Rent(GameObject prefab, Vector3 position, Bullet source)
        {
            if (!_buckets.TryGetValue(prefab, out var bucket))
                _buckets.Add(prefab, bucket = new Stack<PooledEffect>());
            PooledEffect effect = null;
            while (bucket.Count > 0 && effect == null) effect = bucket.Pop();
            if (effect == null)
            {
                var instance = Instantiate(prefab, _inactiveRoot);
                instance.SetActive(false);
                effect = instance.GetComponent<PooledEffect>();
                if (effect == null) effect = instance.AddComponent<PooledEffect>();
            }
            effect.transform.SetParent(transform, false);
            effect.transform.SetPositionAndRotation(position, prefab.transform.rotation);
            effect.transform.localScale = prefab.transform.localScale;
            effect.Prepare(this, prefab, source);
            effect.gameObject.SetActive(true);
            effect.Play();
            return effect;
        }

        internal void Return(PooledEffect effect, GameObject prefab)
        {
            effect.ResetPlayback();
            effect.gameObject.SetActive(false);
            effect.transform.SetParent(_inactiveRoot, false);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            effect.transform.localScale = Vector3.one;
            if (prefab != null && _buckets.TryGetValue(prefab, out var bucket) && bucket.Count < MaxInactivePerPrefab)
                bucket.Push(effect);
            else Destroy(effect.gameObject);
        }

        void OnDestroy()
        {
            if (_scenes.TryGetValue(_sceneHandle, out var pool) && pool == this)
                _scenes.Remove(_sceneHandle);
        }
    }
}
