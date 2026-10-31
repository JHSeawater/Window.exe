using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossReloadTests
    {
        private bool previousEnabled;
        private EnterPlayModeOptions previousOptions;
        private string previousScene;
        private BossDesktopSession session;
        [SetUp] public void SetUp()
        {
            previousEnabled=EditorSettings.enterPlayModeOptionsEnabled; previousOptions=EditorSettings.enterPlayModeOptions;
            previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            for(var i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty,Is.False,"Save scenes before validation.");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(EditorApplication.isPlaying) { session?.Exit(BossCleanupCause.SceneUnloading); yield return new ExitPlayMode(); }
            EditorSettings.enterPlayModeOptions=previousOptions;
            EditorSettings.enterPlayModeOptionsEnabled=previousEnabled;
            if(!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
        }
        [UnityTest] public IEnumerator DesktopPolicyAndCrtOwnershipDoNotSurviveANewPlaySession()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            CRTControlLease previousLease=null;
            string previousId=null;
            for(var play=0;play<2;play++)
            {
                yield return new EnterPlayMode(false); yield return null;
                var manager=WindowManager.Instance; var router=DesktopInputRouter.Instance;
                Assert.That(manager.SessionPolicy,Is.Null); Assert.That(router.HasSession,Is.False);
                if(previousLease!=null) Assert.That(previousLease.IsCurrent,Is.False);
                var desktop=Object.FindAnyObjectByType<DesktopController>(); var taskbar=Object.FindAnyObjectByType<TaskbarController>();
                var template=new GameObject("Reload boss template"); template.SetActive(false);
                var prefab=template.AddComponent<BossFightWindow>();
                session=new BossDesktopSession(manager,router,DesktopWindowSpace.Instance,desktop,taskbar);
                Assert.That(session.TryEnter(prefab,new BossRect(100,100,320,220),out var error),Is.True,error);
                Assert.That(session.SessionId,Is.Not.EqualTo(previousId)); previousId=session.SessionId;
                previousLease=CRTOverlay.Instance.AcquireControl(); Assert.That(previousLease,Is.Not.Null);
                previousLease.SetLevel(3);
                // Deliberately leave the session active; scene/manager lifetime must release its policy.
                yield return new ExitPlayMode();
            }
        }
        [UnityTest] public IEnumerator InputProbeReleasesSubscriptionsAndRestoresFrameSettingsAcrossPlay()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/BossInputValidation.unity");
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            var originalFps=Application.targetFrameRate; var originalVsync=QualitySettings.vSyncCount;
            BossInputSource previousSource=null;
            for(var play=0;play<2;play++)
            {
                yield return new EnterPlayMode(false); yield return null;
                var probe=Object.FindAnyObjectByType<BossInputProbe>();
                Assert.That(probe.Source,Is.Not.Null);
                Assert.That(probe.Source,Is.Not.SameAs(previousSource)); previousSource=probe.Source;
                Assert.That(Application.targetFrameRate,Is.EqualTo(60));
                yield return new ExitPlayMode();
                if(probe!=null) Assert.That(probe.Source,Is.Null);
                Assert.That(Application.targetFrameRate,Is.EqualTo(originalFps));
                Assert.That(QualitySettings.vSyncCount,Is.EqualTo(originalVsync));
            }
        }
        [UnityTest] public IEnumerator FullControllerRecreatesSessionClockAndHiddenResourcesWithoutReloads()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/BossFightFullValidation.unity");
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            string previousId=null;
            for(int play=0;play<2;play++)
            {
                yield return new EnterPlayMode(false);yield return null;yield return null;
                var controller=Object.FindFirstObjectByType<BossFightController>();Assert.That(controller.IsActive,Is.True);
                Assert.That(controller.Session.SessionId,Is.Not.EqualTo(previousId));previousId=controller.Session.SessionId;
                Assert.That(controller.Model,Is.TypeOf<BossPhase1Model>());Assert.That(controller.Model.DownloadPercent,Is.InRange(10,11));
                Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(x=>x.name=="Boss hidden layer (Runtime)"),Is.EqualTo(1));
                yield return new ExitPlayMode();
                Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(x=>x.name=="Boss hidden layer (Runtime)"),Is.Zero);
            }
        }
    }
}
