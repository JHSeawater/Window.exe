using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossCompatibilityTests
    {
        private GameObject root, template;
        private WindowManager manager;
        private sealed class Policy : IWindowSessionPolicy
        {
            public object token;
            public bool AllowOpen(string id, object authority) => ReferenceEquals(token, authority);
            public bool AllowRequest(Window window, WindowAction action, object authority) => ReferenceEquals(token, authority) && action != WindowAction.Maximize;
            public bool KeepPositionOnMinimize(Window window) => true;
        }
        private sealed class Constraint : IWindowGeometryConstraint
        {
            public bool DeferAreaConstraint => true;
            public bool reject;
            public bool TryConstrain(Window w, ref Vector2 position, ref Vector2 size)
            {
                if (reject) return false;
                position = Vector2.zero; size = new Vector2(3, 2); return true;
            }
        }
        [SetUp] public void SetUp()
        {
            root = new GameObject("Boss compatibility test"); manager = root.AddComponent<WindowManager>();
            template = new GameObject("Passive template"); template.SetActive(false);
            var prefab = template.AddComponent<WindowExeWindow>();
            manager.RegisterPrefab("window", prefab); manager.RegisterPrefab("boss-fight-window", prefab);
        }
        [TearDown] public void TearDown()
        { if (root != null) Object.DestroyImmediate(root); if (template != null) Object.DestroyImmediate(template); }
        [Test] public void SessionPolicyCoversExistingOpenAndDirectRequestsAndNormalModeReturns()
        {
            var original = manager.Open("window"); var token = new object(); var p = new Policy { token = token };
            Assert.That(manager.TryInstallSessionPolicy(token, p), Is.True);
            Assert.That(manager.Open("window"), Is.Null);
            Assert.That(manager.Open("boss-fight-window"), Is.Null);
            var boss = manager.Open("boss-fight-window", token);
            Assert.That(boss, Is.Not.SameAs(original));
            Assert.That(manager.TryRequest(boss, WindowAction.Close), Is.False);
            Assert.That(manager.TryRequest(boss, WindowAction.Maximize, token), Is.False);
            Assert.That(manager.TryRequest(boss, WindowAction.Minimize, token), Is.True);
            Assert.That(manager.RemoveSessionPolicy(new object()), Is.False);
            Assert.That(manager.RemoveSessionPolicy(token), Is.True);
            Assert.That(manager.Open("window"), Is.SameAs(original));
            Assert.That(manager.TryRequest(boss, WindowAction.Close), Is.True);
        }
        [Test] public void GeometryConstraintCoversMoveResizeApplyRestoreAndAreaConstrain()
        {
            var w = manager.Open("boss-fight-window"); var constraint = new Constraint(); w.GeometryConstraint = constraint;
            w.MoveTo(new Vector2(100, 100)); AssertGeometry(w);
            var start = w.CaptureGeometryState();
            w.Resize(new Vector2(200, 200)); AssertGeometry(w);
            w.ApplyGeometry(new Vector2(-100, -100), new Vector2(300, 300)); AssertGeometry(w);
            w.ResizeFromGeometryState(start, ResizeDirection.Right, new Rect(20, 20, 100, 100)); AssertGeometry(w);
            w.RestoreGeometryState(start); AssertGeometry(w);
            w.ConstrainTo(new Rect(-.1f, -.1f, .2f, .2f)); AssertGeometry(w);
            constraint.reject = true; w.ApplyGeometry(Vector2.one, Vector2.one); AssertGeometry(w);
            Assert.That(w.CaptureGeometryState().NormalSize, Is.EqualTo(w.Size));
        }
        private static void AssertGeometry(Window w)
        { Assert.That((Vector2)w.transform.localPosition, Is.EqualTo(Vector2.zero)); Assert.That(w.Size, Is.EqualTo(new Vector2(3, 2))); }
        [Test] public void ImmutableProjectionRemainsUsableAfterTheLiveCameraChanges()
        {
            var camera = root.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10); camera.pixelRect = new Rect(0, 0, 1920, 1080);
            var old = DesktopSpaceSnapshot.Capture(1, new Rect(-10, -5, 20, 10), 0, camera);
            var screen = new Vector2(1300, 700);
            var expected = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            camera.transform.position = new Vector3(20, 0, -10); camera.orthographicSize = 8;
            Assert.That(old.TryWorld(screen, out var point), Is.True);
            Assert.That(Vector2.Distance(point, expected), Is.LessThan(.0001f));
        }
        [Test] public void StaleCrtOwnerCannotChangeOrReleaseNewOwner()
        {
            var go = new GameObject("CRT ownership", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.RawImage)); go.transform.SetParent(root.transform);
            var effect = go.AddComponent<CRTOverlay>();
            var previous = effect.AcquireControl(); Assert.That(previous, Is.Not.Null);
            var next = previous.Transfer(); Assert.That(next.IsCurrent, Is.True);
            Assert.That(previous.ResetVisuals(), Is.False); Assert.That(previous.SetLevel(0), Is.False); Assert.That(previous.Release(), Is.False);
            Assert.That(effect.AcquireControl(), Is.Null); Assert.That(next.Release(), Is.True);
            Assert.That(effect.AcquireControl(), Is.Not.Null);
        }
    }
}
