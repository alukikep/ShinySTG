using System;
using System.Reflection;
using NUnit.Framework;
using ShinySTG.GameActions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ShinySTG.EnemyAI.Boss;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ShinySTG.Presentation.SpellDeclaration.Editor
{
    public sealed class SpellDeclarationTests
    {
        GameObject _root;
        SpellDeclarationService _service;
        SpellDeclarationDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _root = SpellDeclarationSetup.BuildView();
            _service = _root.GetComponent<SpellDeclarationService>();
            _definition = ScriptableObject.CreateInstance<SpellDeclarationDefinition>();
            _definition.DisplayName = "Test spell";
            _definition.EnterDuration = .25f;
            _definition.HoldDuration = .5f;
            _definition.ExitDuration = .25f;
        }
        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_root); Object.DestroyImmediate(_definition); }
        void Advance(float dt) => typeof(SpellDeclarationService).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_service, new object[] { dt });

        [Test]
        public void CompletesAtEndAndClearsReusableVisuals()
        {
            var handle = _service.Play(_definition);
            Assert.That(handle.Failure, Is.Null);
            Advance(.5f);
            Assert.That(handle.IsComplete, Is.False);
            Assert.That(_root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(_root.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Test spell"));
            Advance(.5f);
            Assert.That(handle.IsComplete, Is.True);
            Assert.That(handle.IsCancelled, Is.False);
            Assert.That(_root.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(_root.GetComponentInChildren<TMP_Text>().text, Is.Empty);
            Assert.That(_root.transform.Find("Portrait").GetComponent<Image>().sprite, Is.Null);
        }

        [Test]
        public void RejectedAndOldHandlesCannotCancelAnotherPlayback()
        {
            var first = _service.Play(_definition);
            var rejected = _service.Play(_definition);
            Assert.That(rejected.Failure, Is.Not.Null);
            rejected.Cancel();
            Assert.That(first.IsComplete, Is.False);
            first.Cancel();
            var next = _service.Play(_definition);
            first.Cancel();
            Assert.That(next.IsComplete, Is.False);
            _service.enabled = false;
            Assert.That(next.IsCancelled, Is.True);
        }

        [Test]
        public void ZeroDurationCompletesAndInvalidDurationFailsWithoutTakingOwnership()
        {
            _definition.EnterDuration = _definition.HoldDuration = _definition.ExitDuration = 0f;
            Assert.That(_service.Play(_definition).IsComplete, Is.True);
            _definition.HoldDuration = float.NaN;
            Assert.That(_service.Play(_definition).Failure, Is.Not.Null);
            Assert.That(_service.IsPlaying, Is.False);
        }

        [Test]
        public void DisabledViewFailsActivePlayback()
        {
            var handle = _service.Play(_definition);
            _root.GetComponent<SpellDeclarationView>().enabled = false;
            Advance(.1f);
            Assert.That(handle.Failure, Is.Not.Null);
            Assert.That(handle.IsComplete, Is.True);
        }

        [Test]
        public void RunnerWaitsForActualPlaybackAndDisposeCancelsVisual()
        {
            using var runner = new GameActionRunner();
            var sequence = new ActionSequence { Actions = new GameAction[]
                { new PlaySpellDeclarationAction { Declaration = _definition } } };
            var first = runner.Play(sequence, new GameActionContext(null));
            runner.Tick(10f);
            Assert.That(first.IsComplete, Is.False, "动作 Tick 不应重复推进服务时间。");
            Advance(1f);
            runner.Tick(0f);
            Assert.That(first.IsComplete, Is.True);
            Assert.That(first.Failure, Is.Null);
            runner.Play(sequence, new GameActionContext(null));
            runner.Dispose();
            Assert.That(_service.IsPlaying, Is.False);
            Assert.That(_root.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }

        [Test]
        public void CancellingDeclarationScopeRestoresBossDamage()
        {
            var owner = new GameObject("Declaration owner");
            try
            {
                var health = owner.AddComponent<BossHealth>();
                health.Bars = null;
                health.LegacyMaxHp = health.LegacyCurrentHp = 100f;
                using var runner = new GameActionRunner();
                runner.Play(new ActionSequence { Actions = new GameAction[]
                {
                    new InvincibilityScopeAction
                    {
                        ProtectOwner = true, ProtectPlayer = false,
                        Sequence = new ActionSequence { Actions = new GameAction[]
                            { new PlaySpellDeclarationAction { Declaration = _definition } } }
                    }
                } }, new GameActionContext(owner.transform));
                health.TakeDamage(10f);
                Assert.That(health.CurrentHp, Is.EqualTo(100f));
                runner.Dispose();
                health.TakeDamage(10f);
                Assert.That(health.CurrentHp, Is.EqualTo(90f));
                Assert.That(_service.IsPlaying, Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void ShippedPrefabAndSampleHaveUsableReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SpellDeclaration.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                var sample = AssetDatabase.LoadAssetAtPath<SpellDeclarationDefinition>("Assets/SO/Presentation/SpellDeclarationSample.asset");
                Assert.That(sample, Is.Not.Null);
                Assert.That(instance.GetComponent<SpellDeclarationView>().IsReady, Is.True);
                var handle = instance.GetComponent<SpellDeclarationService>().Play(sample);
                Assert.That(handle.Failure, Is.Null);
                handle.Cancel();
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
