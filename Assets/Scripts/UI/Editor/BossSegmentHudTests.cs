using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShinySTG.UI.Editor
{
    public sealed class BossSegmentHudTests
    {
        GameObject _go;
        BossHudView _view;
        Image _fill;
        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HUD test", typeof(RectTransform), typeof(BossHudView));
            _view = _go.GetComponent<BossHudView>();
            _fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _fill.transform.SetParent(_go.transform, false);
            var serialized = new SerializedObject(_view);
            serialized.FindProperty("_fill").objectReferenceValue = _fill;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_go);
        RectTransform Segment(int index) => (RectTransform)_fill.transform.GetChild(0).GetChild(index);

        [Test]
        public void FixedBoundariesColorsAndDepletionFollowRightToLeftOrder()
        {
            _view.ConfigureSegments(new[] { .4f, .6f }, new[] { Color.white, Color.red });
            _view.SetHealth(1f, 1, true);
            Assert.That(_fill.enabled, Is.False);
            Assert.That(Segment(0).anchorMin.x, Is.EqualTo(.6f).Within(.0001f));
            Assert.That(Segment(1).GetComponent<Image>().color, Is.EqualTo(Color.red));
            _view.SetHealth(.8f, 1, true);
            Assert.That(Segment(0).anchorMax.x, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(Segment(1).anchorMax.x, Is.EqualTo(.6f).Within(.0001f));
            _view.SetHealth(.6f, 1, true);
            Assert.That(Segment(0).GetComponent<Image>().enabled, Is.False);
            _view.SetHealth(.3f, 1, true);
            Assert.That(Segment(1).anchorMax.x, Is.EqualTo(.3f).Within(.0001f));
        }

        [Test]
        public void SwitchingLayoutsReusesNodesAndClearRestoresLegacyFill()
        {
            _view.ConfigureSegments(new[] { .4f, .6f }, new[] { Color.white, Color.red });
            var first = Segment(0);
            _view.ConfigureSegments(new[] { .2f, .3f, .5f }, new[] { Color.blue, Color.green, Color.yellow });
            _view.SetHealth(1f, 2, true);
            Assert.That(Segment(0), Is.SameAs(first));
            Assert.That(first.GetComponent<Image>().color, Is.EqualTo(Color.blue));
            _view.ConfigureSegments(new[] { 1f }, new[] { Color.white });
            Assert.That(Segment(1).gameObject.activeSelf, Is.False);
            _view.Clear();
            Assert.That(_go.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(_fill.enabled, Is.True);
            _view.SetHealth(.75f, 1, true);
            Assert.That(_fill.fillAmount, Is.EqualTo(.75f));
        }

        [Test]
        public void SegmentSetupIsIdempotentAndUndoRestoresBinding()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            GameplayHudSetup.AddSegments(_view);
            GameplayHudSetup.AddSegments(_view);
            Assert.That(_fill.transform.childCount, Is.EqualTo(1));
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            Undo.PerformUndo();
            Assert.That(_fill.transform.childCount, Is.Zero);
            var serialized = new SerializedObject(_view);
            Assert.That(serialized.FindProperty("_segmentRoot").objectReferenceValue, Is.Null);
        }
    }
}
