using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;
using Object = UnityEngine.Object;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossDesktopSessionTests
    {
        private string previousScene;
        private GameObject template;
        private BossFightWindow prefab;
        private BossDesktopSession session;
        private WindowManager manager;
        private DesktopController desktop;
        private DesktopWindowSpace space;
        private TaskbarController taskbar;
        private DesktopInputRouter router;
        private static readonly BossRect Initial = new BossRect(100, 100, 320, 220);

        [UnitySetUp] public IEnumerator SetUp()
        {
            for(var i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, Is.False, "Save scene changes before validation.");
            previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            yield return new EnterPlayMode();
            yield return null;
            manager=WindowManager.Instance; router=DesktopInputRouter.Instance; space=DesktopWindowSpace.Instance;
            desktop=Object.FindAnyObjectByType<DesktopController>(); taskbar=Object.FindAnyObjectByType<TaskbarController>();
            Assert.That(space.Valid, Is.True);
            template=new GameObject("Boss session template"); template.SetActive(false);
            prefab=template.AddComponent<BossFightWindow>();
            session=new BossDesktopSession(manager,router,space,desktop,taskbar);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            session?.Exit(BossCleanupCause.SceneUnloading);
            if(template!=null) Object.Destroy(template);
            yield return new ExitPlayMode();
            if(!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
        }
        [UnityTest] public IEnumerator EntryUsesSeparateWindowAndMinimizeKeepsCurrentBody()
        {
            var original=manager.Open("window"); Assert.That(original, Is.Not.Null);
            Assert.That(session.TryEnter(prefab,Initial,out var error), Is.True, error);
            Assert.That(session.View, Is.Not.SameAs(original));
            Assert.That(session.View.IsBound, Is.True);
            Assert.That(manager.GetProcess("window").State, Is.EqualTo(WindowState.Minimized));
            Assert.That(desktop.enabled, Is.False); Assert.That(router.HasSession, Is.True);
            Assert.That(manager.Open("window"), Is.Null);
            Assert.That(manager.TryRequest(session.View,WindowAction.Close), Is.False);
            Assert.That(manager.TryRequest(session.View,WindowAction.Maximize), Is.False);
            var moved=new BossRect(400,250,90,90); Assert.That(session.SetBody(moved), Is.True);
            var position=session.View.transform.position;
            Assert.That(session.Request(WindowAction.Minimize), Is.True);
            Assert.That(session.View.transform.position, Is.EqualTo(position));
            Assert.That(session.Request(WindowAction.Restore), Is.True);
            Assert.That(session.View.transform.position, Is.EqualTo(position));
            Assert.That(session.View.BarHeight/session.Space.Scale, Is.EqualTo(36).Within(.001));
            Physics2D.SyncTransforms();
            var titleY=(float)moved.y-18;
            Assert.That(session.View.ButtonAt(session.Space.ToWorld(new Vector2((float)moved.Right-13,titleY))), Is.EqualTo(2));
            Assert.That(session.View.ButtonAt(session.Space.ToWorld(new Vector2((float)moved.Right-39,titleY))), Is.EqualTo(0));
            Assert.That(taskbar.Buttons.Count, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator FailedOpenObserverRollsBackDesktopWindowsAndPolicy()
        {
            var original=manager.Open("window"); var active=manager.ActiveWindowId;
            var provider=taskbar.Source;
            var iconStates=desktop.Icons.ToDictionary(x=>x.gameObject,x=>x.gameObject.activeSelf);
            Action<WindowProcess> fail=p=> { if(p.Id==BossFightWindow.ContentId) throw new InvalidOperationException("Injected entry failure"); };
            manager.OnProcessOpened+=fail;
            try { Assert.That(session.TryEnter(prefab,Initial,out var error), Is.False); Assert.That(error, Does.Contain("Injected")); }
            finally { manager.OnProcessOpened-=fail; }
            Assert.That(session.IsClosed, Is.True); Assert.That(session.Outcome, Is.EqualTo(BossOutcome.None));
            Assert.That(manager.GetProcess(BossFightWindow.ContentId), Is.Null);
            Assert.That(manager.GetProcess("window").Instance, Is.SameAs(original));
            Assert.That(manager.GetProcess("window").State, Is.EqualTo(WindowState.Normal));
            Assert.That(manager.ActiveWindowId, Is.EqualTo(active));
            Assert.That(manager.SessionPolicy, Is.Null); Assert.That(router.HasSession, Is.False);
            Assert.That(taskbar.Source, Is.SameAs(provider)); Assert.That(desktop.enabled, Is.True);
            foreach(var icon in iconStates) Assert.That(icon.Key.activeSelf, Is.EqualTo(icon.Value));
            Assert.That(manager.Open("window"), Is.SameAs(original));
            yield return null;
        }
        [UnityTest] public IEnumerator EndingAllowsOnlyItsWindowsAndCleanupDoesNotRestoreOldDesktop()
        {
            var oldProvider=taskbar.Source;
            manager.Open("window"); manager.RegisterPrefab("test-ending",manager.GetRegisteredPrefab("window"));
            Assert.That(session.TryEnter(prefab,Initial,out var error), Is.True, error);
            Assert.That(session.SetOutcome(BossOutcome.Success), Is.True);
            Assert.That(session.SetOutcome(BossOutcome.BadEnding), Is.False);
            Assert.That(manager.IsLocked, Is.False);
            Assert.That(session.AllowEnding("old-session",new[]{"test-ending"}), Is.False);
            Assert.That(session.Exit(BossCleanupCause.EndingFinished), Is.False);
            Assert.That(session.AllowEnding(session.SessionId,new[]{"test-ending"}), Is.True);
            Assert.That(router.HasSession, Is.False);
            var ending=manager.Open("test-ending"); Assert.That(ending, Is.Not.Null);
            Assert.That(manager.TryRequest(ending,WindowAction.Minimize), Is.True);
            Assert.That(manager.TryRequest(ending,WindowAction.Restore), Is.True);
            Assert.That(manager.Open("window"), Is.Null);
            Assert.That(session.Exit(BossCleanupCause.EndingFinished,true), Is.True);
            Assert.That(session.Exit(BossCleanupCause.SceneUnloading), Is.True);
            Assert.That(session.Outcome, Is.EqualTo(BossOutcome.Success));
            Assert.That(taskbar.Source, Is.Null); Assert.That(taskbar.Source, Is.Not.SameAs(oldProvider));
            Assert.That(manager.GetProcess("window").State, Is.EqualTo(WindowState.Minimized));
            Assert.That(desktop.Icons.All(x=>!x.gameObject.activeSelf), Is.True);
            Assert.That(manager.IsCurrent(ending), Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator SpaceChangeDefersGeometryUntilExplicitCommit()
        {
            Assert.That(session.TryEnter(prefab,Initial,out var error), Is.True, error);
            var position=session.View.transform.position; var generation=session.Space.Snapshot.Generation;
            DesktopSpaceSnapshot pending=default;
            session.SpaceChanging+=snapshot=>pending=snapshot;
            space.Camera.transform.position+=Vector3.right*2;
            var workArea=Object.FindAnyObjectByType<DesktopWorkArea>();
            workArea.SetBarHeight(workArea.BarHeight+100);
            space.Refresh();
            Assert.That(pending.Generation, Is.GreaterThan(generation));
            Assert.That(session.Space.Snapshot.Generation, Is.EqualTo(generation));
            Assert.That(session.View.transform.position, Is.EqualTo(position));
            var expected=new BossFightSpace(pending).ToWorld(Initial.Center);
            Assert.That(Vector2.Distance(expected,position), Is.GreaterThan(.001));
            Assert.That(session.CommitSpace(pending), Is.True);
            Assert.That(Vector2.Distance(session.View.transform.position,expected), Is.LessThan(.001));
            Assert.That(session.Body.x, Is.EqualTo(Initial.x));
            Assert.That(session.CommitSpace(default), Is.False);
            Assert.That(session.SetBody(Initial), Is.False);
            Assert.That(session.CommitSpace(space.Snapshot), Is.True);
            yield return null;
        }
    }
}
