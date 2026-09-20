using NUnit.Framework;
using ShinySTG.Level;
using ShinySTG.Level.SpawnEntries;
using UnityEngine;

namespace ShinySTG.GameFlow.Editor
{
    public sealed class CompleteLevelEntryTests
    {
        [Test]
        public void CompletionStopsLaterEntriesAndResetAllowsReplay()
        {
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var counter = new CounterEntry();
                definition.Entries = new SpawnEntry[] { new CompleteLevelEntry { TriggerTime = 1f }, counter };
                var runtime = new LevelRuntime(definition);
                runtime.Tick(1f);
                Assert.IsTrue(runtime.CompletionRequested);
                Assert.Zero(counter.Count);
                runtime.Tick(10f);
                Assert.AreEqual(1f, runtime.Elapsed);
                runtime.Reset(false);
                Assert.IsFalse(runtime.CompletionRequested);
                runtime.Tick(0.5f);
                Assert.AreEqual(1, counter.Count);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void CompletionWaitsForBlockerRegisteredInSameTick()
        {
            var definition = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var blocker = new Blocker();
                definition.Entries = new SpawnEntry[] { new ProcessEntry { Process = blocker }, new CompleteLevelEntry() };
                var runtime = new LevelRuntime(definition);
                runtime.Tick(0.1f);
                Assert.IsTrue(runtime.CompletionRequested);
                Assert.IsTrue(runtime.IsTimelineBlocked);
                runtime.Tick(1f);
                Assert.AreEqual(1, blocker.Ticks);
                Assert.IsTrue(runtime.IsTimelineBlocked);
                blocker.Done = true;
                runtime.Tick(1f);
                Assert.IsFalse(runtime.IsTimelineBlocked);
                Assert.AreEqual(0.1f, runtime.Elapsed);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        sealed class CounterEntry : SpawnEntry
        {
            public int Count;
            public override void OnTrigger(LevelRuntime runtime, LevelDefinition def) => Count++;
        }
        sealed class ProcessEntry : SpawnEntry
        {
            public Blocker Process;
            public override void OnTrigger(LevelRuntime runtime, LevelDefinition def) => runtime.AddTimelineProcess(Process);
        }
        sealed class Blocker : ILevelTimelineProcess
        {
            public bool Done;
            public int Ticks;
            public bool IsComplete => Done;
            public bool BlocksTimeline => !Done;
            public void Tick(float dt) => Ticks++;
            public void Dispose() { }
        }
    }
}
