using NUnit.Framework;
using ShinySTG.Level.Encounter;
using UnityEditor;
using UnityEngine;

namespace ShinySTG.EnemyAI.Boss.Editor
{
    public sealed class BossInspectorTests
    {
        [Test]
        public void RepairPreservesExistingReferenceAndSupportsUndo()
        {
            var health = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                health.Bars = new[]
                {
                    new BossHealth.HealthBar { Id = "original" },
                    new BossHealth.HealthBar { Id = "original" },
                    new BossHealth.HealthBar { Id = "" }
                };
                Undo.IncrementCurrentGroup();
                var serialized = new SerializedObject(health);
                serialized.Update();
                Assert.That(BossInspectorFields.RepairIds(serialized), Is.True);
                serialized.ApplyModifiedProperties();
                Undo.FlushUndoRecordObjects();
                Assert.That(health.Bars[0].Id, Is.EqualTo("original"));
                Assert.That(health.Bars[1].Id, Is.Not.EqualTo("original"));
                Assert.That(health.Bars[2].Id, Is.Not.Empty);
                Assert.That(health.Bars[1].Id, Is.Not.EqualTo(health.Bars[2].Id));
                serialized.Update();
                Assert.That(BossInspectorFields.RepairIds(serialized), Is.False);
                Undo.PerformUndo();
                Assert.That(health.Bars[1].Id, Is.EqualTo("original"));
                Assert.That(health.Bars[2].Id, Is.Empty);
            }
            finally { Object.DestroyImmediate(health); }
        }
    }
}
