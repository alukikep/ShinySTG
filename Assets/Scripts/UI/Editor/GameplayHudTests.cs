using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ShinySTG.Player;

namespace ShinySTG.UI.Editor
{
    public sealed class GameplayHudTests
    {
        [Test]
        public void LifeNotificationsObserveNewValueAndKeepPreHitEventOrder()
        {
            var go = new GameObject("HUD health test");
            try
            {
                var health = go.AddComponent<PlayerHealth>();
                health.ReviveInvincibleDuration = 0f;
                // Edit Mode 不自动执行普通 MonoBehaviour.Awake；初始无敌保持为零。
                health.AddLife(-int.MaxValue);
                health.AddLife(2);
                var observed = new List<string>();
                health.OnLifeLost += () => observed.Add("before:" + health.Lives);
                health.OnLivesChanged += value =>
                {
                    Assert.That(health.Lives, Is.EqualTo(value));
                    observed.Add("after:" + value);
                };
                health.OnAllLivesLost += () => observed.Add("dead");
                health.TakeHit();
                health.TakeHit();
                health.TakeHit();
                health.AddLife();
                CollectionAssert.AreEqual(new[] { "before:2", "after:1", "before:1", "after:0", "dead", "after:1" }, observed);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LifeOverflowDoesNotWrapOrNotifyWithoutChange()
        {
            var go = new GameObject("HUD life overflow test");
            try
            {
                var health = go.AddComponent<PlayerHealth>();
                health.AddLife(int.MaxValue);
                int notifications = 0;
                health.OnLivesChanged += _ => notifications++;
                health.AddLife();
                Assert.That(health.Lives, Is.EqualTo(int.MaxValue));
                Assert.That(notifications, Is.Zero);
                health.AddLife(int.MinValue);
                Assert.That(health.Lives, Is.Zero);
                Assert.That(notifications, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(0f, 0f)]
        [TestCase(.5f, .5f)]
        [TestCase(1f, 1f)]
        public void NestedUiEditPreservesAnchorsAndExactRootPosition(float pivotX, float pivotY)
        {
            var root = new GameObject("HUD root", typeof(RectTransform));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(1600, 900);
                var parent = new GameObject("panel", typeof(RectTransform)).GetComponent<RectTransform>();
                parent.SetParent(rootRect, false);
                parent.pivot = new Vector2(pivotX, pivotY);
                parent.sizeDelta = new Vector2(400, 700);
                parent.anchoredPosition = new Vector2(150, 50);
                var child = new GameObject("row", typeof(RectTransform)).GetComponent<RectTransform>();
                child.SetParent(parent, false);
                child.anchorMin = new Vector2(.1f, .2f);
                child.anchorMax = new Vector2(.8f, .9f);
                Rect wanted = new Rect(500, 250, 300, 70);
                GameplayHudLayoutWindow.ApplyRect(rootRect, child, wanted);
                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                Vector3 bottom = rootRect.InverseTransformPoint(corners[0]);
                Vector3 top = rootRect.InverseTransformPoint(corners[2]);
                Assert.That(bottom.x - rootRect.rect.xMin, Is.EqualTo(wanted.xMin).Within(.001f));
                Assert.That(rootRect.rect.yMax - top.y, Is.EqualTo(wanted.yMin).Within(.001f));
                Assert.That(top.x - bottom.x, Is.EqualTo(wanted.width).Within(.001f));
                Assert.That(top.y - bottom.y, Is.EqualTo(wanted.height).Within(.001f));
                Assert.That(child.anchorMin, Is.EqualTo(new Vector2(.1f, .2f)));
                Assert.That(child.anchorMax, Is.EqualTo(new Vector2(.8f, .9f)));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
