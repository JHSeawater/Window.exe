using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.BossFight.Tests
{
    public sealed class LockWarningBossEntryTests
    {
        private string previousScene;
        private LockWarningBossEntry entry;
        private BossFightController fight;
        private WindowManager manager;
        private Mouse mouse;
        private InputSettings originalSettings, testSettings;
        private HideFlags originalFlags;
        private bool originalBackground;

        [UnitySetUp] public IEnumerator SetUp()
        {
            System.IO.Directory.CreateDirectory("Builds/LockBossEntry_20261101");
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, Is.False);
            previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            yield return new EnterPlayMode(); yield return null;
            originalSettings=InputSystem.settings;originalFlags=originalSettings.hideFlags;originalBackground=Application.runInBackground;
            testSettings=Object.Instantiate(originalSettings);testSettings.hideFlags=HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            if(originalFlags==HideFlags.HideAndDontSave)originalSettings.hideFlags=HideFlags.DontSave;
            InputSystem.settings=testSettings;Application.runInBackground=true;
            mouse=InputSystem.AddDevice<Mouse>("Lock warning test mouse");mouse.MakeCurrent();
            double readyDeadline=Time.realtimeSinceStartupAsDouble+3;
            while(WindowManager.Instance==null && Time.realtimeSinceStartupAsDouble<readyDeadline)yield return null;
            manager=WindowManager.Instance;Assert.That(manager,Is.Not.Null,"Desktop initialization must finish before input tests.");
            entry=Object.FindAnyObjectByType<LockWarningBossEntry>();Assert.That(entry,Is.Not.Null);
            fight=entry.GetComponent<BossFightController>();
            entry.ObserveNativeApplicationState=false;entry.ObserveFocus(false);
            fight.ObserveNativeApplicationState=false;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(entry!=null)entry.enabled=false;
            if(fight!=null)Object.Destroy(fight.gameObject);
            yield return null;
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(originalSettings!=null){InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;}
            Application.runInBackground=originalBackground;
            if(testSettings!=null)Object.DestroyImmediate(testSettings);
            yield return new ExitPlayMode();
            if(!string.IsNullOrEmpty(previousScene))EditorSceneManager.OpenScene(previousScene);
        }
        private IEnumerator Click(LockWarningWindow warning, Vector2 uv)
        {
            var screen=(Vector2)Camera.main.WorldToScreenPoint(warning.ImageToWorld(uv));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return null;yield return null;
        }
        [UnityTest] public IEnumerator ConfirmClosesLockAndStartsUnlockEntry() { yield return RunButtons(false); }
        [UnityTest] public IEnumerator CloseClosesLockAndStartsUnlockEntry() { yield return RunButtons(true); }
        private IEnumerator RunButtons(bool close)
        {
            var warning=manager.Open(LockWarningWindow.ContentId) as LockWarningWindow;yield return null;yield return null;
            Assert.That(warning,Is.Not.Null);
            yield return Click(warning,close?new Vector2(.89f,.89f):new Vector2(.49f,.31f));
            Assert.That(manager.GetProcess(LockWarningWindow.ContentId),Is.Null);
            Assert.That(entry.IsPending || fight.IsActive,Is.False);
            warning=manager.Open(LockWarningWindow.ContentId) as LockWarningWindow;yield return null;yield return null;
            Assert.That(warning.TryUseKey(),Is.True);yield return null;
            Assert.That(entry.IsPending || fight.IsActive,Is.False,"Key use alone must not start the fight.");
            var unlocked=warning.CurrentSprite;
            yield return Click(warning,close?new Vector2(.885f,.805f):new Vector2(.465f,.23f));
            yield return new WaitForEndOfFrame();yield return null;
            Assert.That(entry.IsPending,Is.True);
            Assert.That(manager.IsLocked && DesktopInputRouter.Instance.HasSession,Is.True);
            var frozen=GameObject.Find("Frozen Unlock desktop");Assert.That(frozen,Is.Not.Null);
            var pixels=frozen.GetComponent<SpriteRenderer>().sprite.texture;
            Assert.That(pixels.width,Is.EqualTo(Screen.width));
            Assert.That(DesktopInputRouter.IsWorldInputBlocked,Is.True);
            if(!close)System.IO.File.WriteAllBytes("Builds/LockBossEntry_20261101/frozen-desktop.png",pixels.EncodeToPNG());
            warning.RequestClose();entry.AdvanceWait(20);
            Assert.That(entry.PendingSeconds,Is.EqualTo(0),"Focus loss must pause the delay.");
            entry.ObserveFocus(true);entry.ObservePause(true);entry.AdvanceWait(20);
            Assert.That(entry.PendingSeconds,Is.EqualTo(0));entry.ObservePause(false);
            entry.AdvanceWait(2.99);
            Assert.That(fight.IsActive,Is.False);Assert.That(warning.CurrentSprite,Is.SameAs(unlocked));
            entry.AdvanceWait(.011);
            Assert.That(entry.LastError,Is.Null);Assert.That(fight.IsEntering,Is.True);
            Assert.That(entry.IsPending,Is.False);Assert.That(manager.IsLocked,Is.False);
            Assert.That(GameObject.Find("Frozen Unlock desktop"),Is.SameAs(frozen));
            Assert.That(frozen.GetComponent<SpriteRenderer>().sortingLayerName,Is.EqualTo("Background"));
            var session=fight.Session.SessionId;warning.RequestClose();Assert.That(fight.Session.SessionId,Is.EqualTo(session));
            fight.ObserveFocus(true);
            var rules=AssetDatabase.LoadAssetAtPath<BossFightSettings>("Assets/Data/Gameplay/BossFight/BossFightSettings.asset");
            double warningEnd=rules.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds;
            bool downloadShot=false,eyesShot=false,warningShot=false,blackShot=false,terminalShot=false;
            double deadline=Time.realtimeSinceStartupAsDouble+22;
            while(fight.IsEntering && Time.realtimeSinceStartupAsDouble<deadline)
            {
                double age=fight.Model.Clock.CombatTime;
                Assert.That(GameObject.Find("Frozen Unlock desktop")!=null,Is.EqualTo(age<warningEnd));
                if(!close)
                {
                    if(!downloadShot && age>.1){ScreenCapture.CaptureScreenshot("Builds/LockBossEntry_20261101/downloading.png");downloadShot=true;}
                    if(!eyesShot && age>3.4){ScreenCapture.CaptureScreenshot("Builds/LockBossEntry_20261101/eyes.png");eyesShot=true;}
                    if(!warningShot && age>rules.EntryPreludeSeconds+.2){ScreenCapture.CaptureScreenshot("Builds/LockBossEntry_20261101/one-more.png");warningShot=true;}
                    if(!blackShot && age>warningEnd+.2){ScreenCapture.CaptureScreenshot("Builds/LockBossEntry_20261101/black-delay.png");blackShot=true;}
                    if(!terminalShot && age>warningEnd+1.3){ScreenCapture.CaptureScreenshot("Builds/LockBossEntry_20261101/terminal.png");terminalShot=true;}
                }
                yield return null;
            }
            Assert.That(fight.IsEntering,Is.False);
            Assert.That(GameObject.Find("Frozen Unlock desktop"),Is.Null);
            Assert.That(Object.FindAnyObjectByType<TaskbarController>().GetComponentInParent<Canvas>().enabled,Is.True);
        }
        [UnityTest] public IEnumerator FailedEntryRestoresUnlockAndInput()
        {
            var warning=manager.Open(LockWarningWindow.ContentId) as LockWarningWindow;yield return null;
            warning.TryUseKey();warning.RequestClose();
            yield return new WaitForEndOfFrame();yield return null;
            fight.ConfigureFullFight(null,null);
            entry.ObserveFocus(true);entry.AdvanceWait(3);
            Assert.That(entry.LastError,Is.Not.Null);
            Assert.That(entry.IsPending || fight.IsActive || manager.IsLocked || DesktopInputRouter.Instance.HasSession,Is.False);
            Assert.That(manager.IsCurrent(warning) && warning.gameObject.activeInHierarchy,Is.True);
            Assert.That(warning.CurrentStage,Is.EqualTo(1));
            Assert.That(GameObject.Find("Frozen Unlock desktop"),Is.Null);
            Assert.That(Object.FindAnyObjectByType<TaskbarController>().GetComponentInParent<Canvas>().enabled,Is.True);
            Assert.That(CRTOverlay.Instance.GetComponentInParent<Canvas>().enabled,Is.True);
        }
    }
}
