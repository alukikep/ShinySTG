using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShinySTG.Background.Editor
{
    public sealed class BackgroundImagePlaybackTests
    {
        static readonly Type PlaybackType = typeof(StageBackgroundController).Assembly
            .GetType("ShinySTG.Background.BackgroundImagePlayback", true);
        static readonly Type SnapshotType = PlaybackType.GetNestedType("Snapshot", BindingFlags.NonPublic);

        static object Snapshot(BackgroundImageDefinition image) => SnapshotType.GetMethod("Capture").Invoke(null, new object[] { image });
        static object Call(object state, string name, params object[] args) => PlaybackType.GetMethod(name).Invoke(state, args);
        static T Read<T>(object state, string name) => (T)PlaybackType.GetProperty(name).GetValue(state);

        [Test]
        public void CameraDrawsLowerThenGeometryThenUpperThenFadeAndReleasesBuffers()
        {
            var root = new GameObject("background rendering test");
            var cameraObject = new GameObject("background test camera");
            var geometry = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var target = new RenderTexture(64, 64, 24);
            var pixels = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var image = ScriptableObject.CreateInstance<BackgroundImageDefinition>();
            var material = new Material(Shader.Find("Unlit/Color"));
            var previous = RenderTexture.active;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(StageBackgroundController);
            try
            {
                var controller = root.AddComponent<StageBackgroundController>();
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.cullingMask = 1 << 30;
                geometry.layer = 30;
                geometry.transform.position = new Vector3(0f, 0f, 3f);
                material.color = Color.green;
                geometry.GetComponent<Renderer>().sharedMaterial = material;
                type.GetField("_backgroundCamera", flags).SetValue(controller, camera);
                Assert.IsTrue((bool)type.GetMethod("EnsureImageRenderer", flags).Invoke(controller, null));
                image.Sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f);
                image.Tint = Color.red;
                var lower = type.GetField("_lowerImage", flags).GetValue(controller);
                var upper = type.GetField("_upperImage", flags).GetValue(controller);
                Call(lower, "Set", Snapshot(image), 0f, 0f);
                Action capture = () =>
                {
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                    pixels.Apply();
                };
                capture();
                Assert.That(pixels.GetPixel(2, 2).r, Is.GreaterThan(0.9f), "Lower image must survive camera clear.");
                Assert.That(pixels.GetPixel(32, 32).g, Is.GreaterThan(0.9f), "Geometry must cover lower image.");
                image.Tint = Color.blue;
                Call(upper, "Set", Snapshot(image), 0f, 0f);
                capture();
                Assert.That(pixels.GetPixel(32, 32).b, Is.GreaterThan(0.9f), "Upper image must cover geometry.");
                type.GetField("_transitionColor", flags).SetValue(controller, Color.white);
                type.GetMethod("SetFade", flags).Invoke(controller, new object[] { 1f });
                capture();
                Assert.That(pixels.GetPixel(32, 32).r, Is.GreaterThan(0.9f));
                Assert.That(pixels.GetPixel(32, 32).g, Is.GreaterThan(0.9f));
                type.GetMethod("SetFade", flags).Invoke(controller, new object[] { 0.5f });
                capture();
                Assert.That(pixels.GetPixel(32, 32).r, Is.InRange(0.4f, 0.8f), "White flash must blend at intermediate alpha.");
                type.GetMethod("SetFade", flags).Invoke(controller, new object[] { 0f });
                capture();
                Assert.That(pixels.GetPixel(32, 32).r, Is.LessThan(0.1f), "Completed flash must reveal the upper image.");
                type.GetMethod("ReleaseImageRenderer", flags).Invoke(controller, null);
                Assert.AreEqual(0, camera.commandBufferCount);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(geometry);
                UnityEngine.Object.DestroyImmediate(material);
                if (image.Sprite != null) UnityEngine.Object.DestroyImmediate(image.Sprite); UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SpriteSubRectangleUsesItsOwnAspectAndAtlasRegion()
        {
            var image = ScriptableObject.CreateInstance<BackgroundImageDefinition>();
            var texture = new Texture2D(256, 256);
            image.Sprite = Sprite.Create(texture, new Rect(64, 32, 128, 64), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
            var state = Activator.CreateInstance(PlaybackType, true);
            try
            {
                var snapshot = Snapshot(image);
                Call(state, "Set", snapshot, 0f, 0f);
                Assert.AreEqual(new Vector4(0.5f, 0.25f, 0.25f, 0.125f), SnapshotType.GetField("Region").GetValue(snapshot));
                Assert.AreEqual(new Vector4(0.5f, 1f, 0.25f, 0f), (Vector4)Call(state, "UvTransform", 1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image.Sprite);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ImmediateApplySnapshotsConfigAndOldHandleCannotCancelReplacement()
        {
            var image = ScriptableObject.CreateInstance<BackgroundImageDefinition>();
            image.Sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f);
            var state = Activator.CreateInstance(PlaybackType, true);
            try
            {
                var first = (BackgroundPlaybackHandle)Call(state, "Set", Snapshot(image), 0f, 2f);
                Call(state, "Advance", 1f);
                Assert.AreEqual(0.5f, Read<float>(state, "Opacity"));
                var second = (BackgroundPlaybackHandle)Call(state, "Set", Snapshot(image), 0f, 0f);
                Assert.AreEqual(BackgroundPlaybackStatus.Cancelled, first.Status);
                Assert.AreEqual(BackgroundPlaybackStatus.Completed, second.Status);
                first.Cancel();
                UnityEngine.Object.DestroyImmediate(image.Sprite); image.Sprite = null;
                Call(state, "Advance", 0f);
                Assert.AreEqual(1f, Read<float>(state, "Opacity"));
                Assert.AreEqual(BackgroundPlaybackStatus.Completed, second.Status);
                var hide = (BackgroundPlaybackHandle)Call(state, "Set", null, 1f, 9f);
                Call(state, "Advance", 1f);
                Assert.AreEqual(BackgroundPlaybackStatus.Completed, hide.Status);
                Assert.IsNull(Read<object>(state, "Image"));
            }
            finally { if (image.Sprite != null) UnityEngine.Object.DestroyImmediate(image.Sprite); UnityEngine.Object.DestroyImmediate(image); }
        }

        [Test]
        public void CancelKeepsVisibleImageAndClearResetsScroll()
        {
            var image = ScriptableObject.CreateInstance<BackgroundImageDefinition>();
            image.Sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f);
            image.ScrollSpeed = Vector2.one;
            var state = Activator.CreateInstance(PlaybackType, true);
            try
            {
                Call(state, "Set", Snapshot(image), 0f, 0f);
                var hide = (BackgroundPlaybackHandle)Call(state, "Set", null, 2f, 0f);
                Call(state, "Advance", 1f);
                hide.Cancel();
                Call(state, "Advance", 5f);
                Assert.AreEqual(0.5f, Read<float>(state, "Opacity"));
                Assert.IsNotNull(Read<object>(state, "Image"));
                Call(state, "Clear");
                Assert.IsNull(Read<object>(state, "Image"));
                Assert.AreEqual(Vector2.zero, Read<Vector2>(state, "Scroll"));
            }
            finally { if (image.Sprite != null) UnityEngine.Object.DestroyImmediate(image.Sprite); UnityEngine.Object.DestroyImmediate(image); }
        }

        [Test]
        public void ReplacementConsumesLongFrameAndCoverCropsWithoutStretching()
        {
            var image = ScriptableObject.CreateInstance<BackgroundImageDefinition>();
            var texture = new Texture2D(200, 100);
            image.Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
            var state = Activator.CreateInstance(PlaybackType, true);
            try
            {
                Call(state, "Set", Snapshot(image), 0f, 0f);
                var handle = (BackgroundPlaybackHandle)Call(state, "Set", Snapshot(image), 1f, 2f);
                Call(state, "Advance", 3f);
                Assert.AreEqual(BackgroundPlaybackStatus.Completed, handle.Status);
                Assert.AreEqual(new Vector4(0.5f, 1f, 0.25f, 0f), (Vector4)Call(state, "UvTransform", 1f));
            }
            finally
            {
                if (image.Sprite != null) UnityEngine.Object.DestroyImmediate(image.Sprite); UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
