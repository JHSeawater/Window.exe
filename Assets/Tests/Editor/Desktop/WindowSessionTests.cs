using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using WindowExe.Desktop.Desktop.Window;
namespace WindowExe.Desktop.Tests
{
    public sealed class WindowSessionTests
    {
        private bool originalEnabled;
        private EnterPlayModeOptions originalOptions;
        [SetUp] public void SetUp()
        { originalEnabled=EditorSettings.enterPlayModeOptionsEnabled; originalOptions=EditorSettings.enterPlayModeOptions; }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(EditorApplication.isPlaying) yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptions=originalOptions;
            EditorSettings.enterPlayModeOptionsEnabled=originalEnabled;
        }
        [UnityTest] public IEnumerator SessionsResetWithDomainAndSceneReloadDisabled()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            yield return new EnterPlayMode(false);
            var scope=new DesktopInputTestScope();
            var manager=WindowManager.Instance;
            Assert.That(manager,Is.Not.Null); manager.Open("memo"); manager.SetLocked(true);
            var id=manager.GetProcess("memo").InstanceId;
            scope.Dispose();
            yield return new ExitPlayMode();
            yield return new EnterPlayMode(false);
            scope=new DesktopInputTestScope();
            manager=WindowManager.Instance;
            Assert.That(manager,Is.Not.Null); Assert.That(manager.IsLocked,Is.False); Assert.That(manager.ActiveProcesses,Is.Empty);
            manager.Open("memo"); Assert.That(manager.GetProcess("memo").InstanceId,Is.Not.EqualTo(id));
            var bar=Object.FindAnyObjectByType<TaskbarController>(); Assert.That(bar.Buttons.Count,Is.EqualTo(1));
            scope.Dispose();
            yield return new ExitPlayMode();
        }
    }
}
