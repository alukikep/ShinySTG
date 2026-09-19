using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShinySTG.Effects.Editor
{
    public sealed class EffectPoolTests
    {
        GameObject _prefab;
        GameObject _bulletObject;
        GameObject _poolObject;

        [TearDown]
        public void TearDown()
        {
            if (_poolObject != null) Object.DestroyImmediate(_poolObject);
            if (_bulletObject != null) Object.DestroyImmediate(_bulletObject);
            if (_prefab != null) Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void RepeatedReleaseDoesNotRentSameInstanceTwice()
        {
            _prefab = new GameObject("effect prefab");
            _prefab.SetActive(false);
            var scene = SceneManager.GetActiveScene();
            var first = EffectPool.Play(_prefab, Vector3.one, scene);
            _poolObject = first.transform.parent.gameObject;
            uint version = first.PlaybackVersion;
            Assert.IsTrue(first.IsPlaybackActive(version));
            first.Release();
            Assert.IsFalse(first.IsPlaybackActive(version));
            first.Release();
            var reused = EffectPool.Play(_prefab, Vector3.right, scene);
            var simultaneous = EffectPool.Play(_prefab, Vector3.left, scene);
            Assert.AreSame(first, reused);
            Assert.IsFalse(first.IsPlaybackActive(version));
            Assert.IsTrue(reused.IsPlaybackActive(reused.PlaybackVersion));
            Assert.AreNotSame(reused, simultaneous);
            Assert.AreEqual(Vector3.right, reused.transform.position);
        }

        [Test]
        public void SnapshotSurvivesSourceResetAndReuseCopiesNewProperties()
        {
            _prefab = new GameObject("afterimage prefab", typeof(SpriteRenderer), typeof(BulletAfterimage));
            _prefab.SetActive(false);
            _bulletObject = new GameObject("bullet", typeof(SpriteRenderer), typeof(Bullet));
            var bullet = _bulletObject.GetComponent<Bullet>();
            var renderer = _bulletObject.GetComponent<SpriteRenderer>();
            bullet.Renderer = renderer;
            bullet.ResetForPool();
            renderer.color = Color.red;
            renderer.flipX = true;
            var property = Shader.PropertyToID("_TintColor");
            var block = new MaterialPropertyBlock();
            block.SetColor(property, Color.cyan);
            renderer.SetPropertyBlock(block);
            var effect = EffectPool.Play(_prefab, Vector3.zero, _bulletObject.scene, bullet);
            _poolObject = effect.transform.parent.gameObject;
            var snapshot = effect.GetComponent<SpriteRenderer>();
            bullet.ResetForPool();
            renderer.color = Color.green;
            Assert.AreEqual(Color.red, snapshot.color);
            snapshot.GetPropertyBlock(block);
            Assert.AreEqual(Color.cyan, block.GetColor(property));
            effect.Release();
            renderer.flipX = false;
            var reused = EffectPool.Play(_prefab, Vector3.zero, _bulletObject.scene, bullet);
            Assert.AreSame(effect, reused);
            Assert.AreEqual(Color.green, snapshot.color);
            Assert.IsFalse(snapshot.flipX);
            snapshot.GetPropertyBlock(block);
            Assert.IsFalse(block.HasColor(property));
        }
    }
}
