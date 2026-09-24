using System;
using NUnit.Framework;
using UnityEngine;

namespace ShinySTG.EnemyAI.Editor
{
    public sealed class EnemyDeathTests
    {
        GameObject _owner;
        EnemyHealth _health;
        Action<EnemyHealth> _globalHandler;
        int _globalDeaths;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Enemy death test");
            _health = _owner.AddComponent<EnemyHealth>();
            // EditMode does not guarantee MonoBehaviour.Awake invocation.
            _health.SendMessage("Awake");
            _globalHandler = health => { if (health == _health) _globalDeaths++; };
            _globalDeaths = 0;
            EnemyHealth.OnAnyDeath += _globalHandler;
        }

        [TearDown]
        public void TearDown()
        {
            EnemyHealth.OnAnyDeath -= _globalHandler;
            UnityEngine.Object.DestroyImmediate(_owner);
        }

        [Test]
        public void ForcedDeathBypassesInvincibilityAndNotifiesOnceWithoutDamage()
        {
            int deaths = 0, damageEvents = 0;
            _health.OnDeath += () => { deaths++; Assert.That(_health.IsDead, Is.True); };
            _health.OnDamaged += _ => damageEvents++;
            _health.AddInvincibility("test");
            _health.Kill();
            _health.Kill();
            _health.TakeDamage(100f);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(_globalDeaths, Is.EqualTo(1));
            Assert.That(damageEvents, Is.Zero);
        }

        [Test]
        public void ReentrantDamageCallbackCannotDuplicateDeathNotification()
        {
            _health.OnDamaged += _ => _health.TakeDamage(100f);
            _health.TakeDamage(0.5f);
            _health.Kill();
            Assert.That(_health.IsDead, Is.True);
            Assert.That(_globalDeaths, Is.EqualTo(1));
        }

        [Test]
        public void DisabledEnemyCannotBeKilled()
        {
            _owner.SetActive(false);
            _health.Kill();
            Assert.That(_health.IsDead, Is.False);
            Assert.That(_globalDeaths, Is.Zero);
        }
    }
}
