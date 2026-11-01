using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using WindowExe.Desktop;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossControllerTests
    {
        private string previousScene;
        private BossFightController controller;
        private BossFightSettings rules;
        private GameObject template;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings originalSettings,testSettings;
        private HideFlags originalFlags;
        private bool originalBackground;
        [UnitySetUp] public IEnumerator SetUp()
        {
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,Is.False);
            previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");yield return new EnterPlayMode();yield return null;
            originalSettings=InputSystem.settings;originalFlags=originalSettings.hideFlags;originalBackground=Application.runInBackground;
            testSettings=Object.Instantiate(originalSettings);testSettings.hideFlags=HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            if(originalFlags==HideFlags.HideAndDontSave)originalSettings.hideFlags=HideFlags.DontSave;
            InputSystem.settings=testSettings;Application.runInBackground=true;
            mouse=InputSystem.AddDevice<Mouse>("Boss controller mouse");keyboard=InputSystem.AddDevice<Keyboard>("Boss controller keyboard");
            mouse.MakeCurrent();keyboard.MakeCurrent();
            rules=ScriptableObject.CreateInstance<BossFightSettings>();
            template=new GameObject("Boss template");template.SetActive(false);var prefab=template.AddComponent<BossFightWindow>();
            controller=new GameObject("Boss controller test").AddComponent<BossFightController>();controller.ObserveNativeApplicationState=false;controller.Configure(rules,prefab);
            Assert.That(controller.BeginPhase2(out var error),Is.True,error);controller.Model.Invulnerable=true;
            controller.ObserveFocus(true);yield return Settle();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(controller!=null)Object.Destroy(controller.gameObject);
            if(template!=null)Object.Destroy(template);if(rules!=null)Object.Destroy(rules);yield return null;
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(originalSettings!=null){InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;}
            Application.runInBackground=originalBackground;if(testSettings!=null)Object.DestroyImmediate(testSettings);
            yield return new ExitPlayMode();if(!string.IsNullOrEmpty(previousScene))EditorSceneManager.OpenScene(previousScene);
        }
        private static IEnumerator Settle(){yield return new WaitForSecondsRealtime(.12f);yield return null;}
        private Vector2 Screen(BossPoint point)
        {var world=controller.Session.Space.ToWorld(point.Vector);return Camera.main.WorldToScreenPoint(new Vector3(world.x,world.y,controller.Session.Space.Snapshot.PlaneZ));}
        private IEnumerator Pointer(BossPoint point,bool held)
        {var state=new MouseState{position=Screen(point)};if(held)state=state.WithButton(MouseButton.Left);InputSystem.QueueStateEvent(mouse,state);yield return Settle();}
        private IEnumerator Space(bool held)
        {InputSystem.QueueStateEvent(keyboard,held?new KeyboardState(Key.Space):new KeyboardState());yield return Settle();}
        private IEnumerator KeyPress(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Settle();InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Settle();}
        private IEnumerator BeginFirstPhase(bool entry=false,bool eyeWindows=false,bool downloading=false)
        {
            Object.Destroy(controller.gameObject);yield return null;
            rules.phase1=UnityEditor.AssetDatabase.LoadAssetAtPath<BossPhaseDefinition>("Assets/Data/Gameplay/BossFight/Phase1.asset");
            rules.hiddenViewShader=Shader.Find("WINDOW.exe/BossHiddenView");
            var art=UnityEditor.AssetDatabase.LoadAssetAtPath<BossFightSettings>("Assets/Data/Gameplay/BossFight/BossFightSettings.asset");
            rules.phase1EyeSprite=art.phase1EyeSprite;rules.phase1PupilSprite=art.phase1PupilSprite;
            rules.eyeSprite=art.eyeSprite;rules.pupilSprite=art.pupilSprite;
            rules.phase2BackgroundSprite=art.phase2BackgroundSprite;rules.phase2PupilDiameterRatio=art.phase2PupilDiameterRatio;
            rules.phase1HiddenBackgroundSprite=art.phase1HiddenBackgroundSprite;
            rules.phase1ErrorSprite=art.phase1ErrorSprite;
            rules.phase1IntroSprite=entry?art.phase1IntroSprite:null;
            rules.phase1DownloadSprite=downloading?art.phase1DownloadSprite:null;
            if(eyeWindows)
            {rules.phase1EntryEyeFrames=art.phase1EntryEyeFrames;rules.phase1EntryFrameSeconds=art.phase1EntryFrameSeconds;rules.phase1EntryErrorSprite=art.phase1EntryErrorSprite;}
            rules.phaseTransitionDosSprite=art.phaseTransitionDosSprite;
            mouse.MakeCurrent();keyboard.MakeCurrent();
            controller=new GameObject("Full boss controller test").AddComponent<BossFightController>();controller.ObserveNativeApplicationState=false;controller.ConfigureFullFight(rules,template.GetComponent<BossFightWindow>());
            Assert.That(controller.BeginBossFight(out var error),Is.True,error);controller.ObserveFocus(true);yield return Settle();
        }
        [UnityTest] public IEnumerator PhaseOneDownloadingBarStartsBeforeAnyEyeWindow()
        {
            yield return BeginFirstPhase(true,true,true);var first=(BossPhase1Model)controller.Model;
            var world=GameObject.Find("Boss fight world").transform;
            var dialog=world.Find("Boss entry downloading");
            Assert.That(dialog,Is.Not.Null);Assert.That(rules.EntryDownloadDuration,Is.EqualTo(3));
            Assert.That(rules.EntryPreludeSeconds,Is.EqualTo(10.82).Within(.00001));
            var panel=dialog.Find("Downloading window").GetComponent<SpriteRenderer>();
            Assert.That(panel.sprite.texture,Is.SameAs(rules.phase1DownloadSprite.texture));
            Assert.That(Vector2.Distance(panel.bounds.center,controller.Session.Space.ToWorld(new BossPoint(960,516).Vector)),Is.LessThan(.0001f));
            var blocks=dialog.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Entry download block ")).ToArray();
            Assert.That(blocks.Length,Is.EqualTo(20));
            var taskbar=Object.FindAnyObjectByType<TaskbarController>().transform.Find("Boss download");
            var taskBlocks=taskbar.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(x=>x.name.StartsWith("Block ")).ToArray();
            Assert.That(taskBlocks.Length,Is.EqualTo(20));
            var active=taskBlocks[0].color;var idle=taskBlocks[19].color;
            Assert.That(blocks.All(x=>x.color==idle),Is.True);
            Assert.That(blocks[0].size.x/blocks[0].size.y,Is.EqualTo(taskBlocks[0].rectTransform.rect.width/taskBlocks[0].rectTransform.rect.height).Within(.0001));
            controller.ObserveFocus(false);yield return Settle();double paused=first.Clock.CombatTime;
            var colors=blocks.Select(x=>x.color).ToArray();
            yield return new WaitForSecondsRealtime(.2f);yield return Settle();
            Assert.That(first.Clock.CombatTime,Is.EqualTo(paused));Assert.That(blocks.Select(x=>x.color).ToArray(),Is.EqualTo(colors));controller.ObserveFocus(true);
            int mostLit=0;bool captured=false;double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(first.Clock.CombatTime<rules.EntryDownloadDuration && Time.realtimeSinceStartupAsDouble<deadline)
            {
                int lit=blocks.Count(x=>x!=null && x.color==active);Assert.That(lit,Is.GreaterThanOrEqualTo(mostLit));mostLit=lit;
                Assert.That(world.GetComponentsInChildren<SpriteRenderer>(true).Any(x=>x.name.StartsWith("Boss entry eye window ")),Is.False);
                Assert.That(world.Find("Boss entry warning").GetComponent<SpriteRenderer>().enabled,Is.False);
                Assert.That(first.DownloadPercent,Is.EqualTo(10));Assert.That(controller.Session.View.Body.forceRenderingOff,Is.True);
                if(!captured && lit==1){ScreenCapture.CaptureScreenshot("Builds/BossFight/EntryPreview/entry-downloading.png");captured=true;}
                yield return null;
            }
            Assert.That(dialog!=null && dialog.gameObject.activeSelf,Is.True);Assert.That(mostLit,Is.EqualTo(1));
            Assert.That(first.Clock.CombatTime,Is.InRange(2.9,3.3));
            var eyes=world.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Boss entry eye window ") && x.enabled).ToArray();
            Assert.That(eyes,Is.Not.Empty);Assert.That(eyes.Min(x=>x.sortingOrder),Is.GreaterThan(dialog.GetComponentsInChildren<SpriteRenderer>().Max(x=>x.sortingOrder)));
            Assert.That(blocks.Count(x=>x.color==active),Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot("Builds/BossFight/EntryPreview/entry-downloading-with-eyes.png");yield return null;
        }
        [UnityTest] public IEnumerator PhaseOneEntryEyesFinishBeforeOrderedErrorsAndWarning()
        {
            yield return BeginFirstPhase(true,true,true);var first=(BossPhase1Model)controller.Model;
            var world=GameObject.Find("Boss fight world").transform;
            var panel=world.Find("Boss entry warning").GetComponent<SpriteRenderer>();
            var terminal=world.Find("Phase 1 terminal").GetComponent<TMPro.TextMeshPro>();
            double eyesDeadline=Time.realtimeSinceStartupAsDouble+4;
            while(!world.GetComponentsInChildren<SpriteRenderer>().Any(x=>x.name.StartsWith("Boss entry eye window ")) && Time.realtimeSinceStartupAsDouble<eyesDeadline)yield return null;
            var windows=world.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Boss entry eye window ")).OrderBy(x=>x.sortingOrder).ToArray();
            Assert.That(windows.Length,Is.EqualTo(15));Assert.That(rules.EntryEyesDuration,Is.EqualTo(5.56).Within(.00001));
            Assert.That(panel.enabled,Is.False);Assert.That(terminal.gameObject.activeSelf,Is.False);
            Assert.That(windows.Any(a=>windows.Any(b=>a!=b && a.bounds.Intersects(b.bounds))),Is.True);
            foreach(var window in windows)
            {
                var bounds=controller.Session.Space.Snapshot.Bounds;
                Assert.That(window.bounds.min.x,Is.GreaterThanOrEqualTo(bounds.min.x-.001f));
                Assert.That(window.bounds.max.x,Is.LessThanOrEqualTo(bounds.max.x+.001f));
                Assert.That(window.bounds.min.y,Is.GreaterThanOrEqualTo(bounds.min.y-.001f));
                Assert.That(window.bounds.max.y,Is.LessThanOrEqualTo(bounds.max.y+.001f));
            }
            controller.ObserveFocus(false);yield return Settle();double pausedAt=first.Clock.CombatTime;
            var pausedFrames=windows.Select(x=>x.sprite.rect).ToArray();
            yield return new WaitForSecondsRealtime(.2f);yield return Settle();
            Assert.That(first.Clock.CombatTime,Is.EqualTo(pausedAt));
            Assert.That(windows.Select(x=>x.sprite.rect).ToArray(),Is.EqualTo(pausedFrames));controller.ObserveFocus(true);
            int mostWindows=0,mostErrors=0;var frames=new System.Collections.Generic.HashSet<Rect>();
            bool eyesCapture=false,errorCapture=false;double deadline=Time.realtimeSinceStartupAsDouble+16;
            while(!panel.enabled && Time.realtimeSinceStartupAsDouble<deadline)
            {
                int visible=0,errors=0;double age=first.Clock.CombatTime;
                for(int i=0;i<windows.Length;i++)
                {
                    var window=windows[i];if(!window.enabled)continue;visible++;
                    if(age<rules.EntryDownloadDuration+(BossFightSettings.EntryWindowCount-1)*BossFightSettings.EntryWindowInterval+2-.001)
                        Assert.That(window.sprite.rect,Is.EqualTo(rules.phase1EntryEyeFrames[0].rect),"All windows hold red for two seconds after the last window appears.");
                    if(age<rules.EntryErrorsStart)
                        Assert.That(window.sprite.rect,Is.EqualTo(windows[0].sprite.rect),"All GIF windows must play the same frame together.");
                    if(i>0)Assert.That(windows[i-1].enabled,Is.True,"Windows must appear in order.");
                    if(window.sprite.texture==rules.phase1EntryErrorSprite.texture)
                    {
                        errors++;Assert.That(age,Is.GreaterThanOrEqualTo(rules.EntryErrorsStart-.001),"All GIFs must finish before errors begin.");
                        if(i>0)Assert.That(windows[i-1].sprite.texture,Is.SameAs(rules.phase1EntryErrorSprite.texture),"Errors must follow creation order.");
                    }
                    else frames.Add(window.sprite.rect);
                }
                mostWindows=Mathf.Max(mostWindows,visible);mostErrors=Mathf.Max(mostErrors,errors);
                Assert.That(first.DownloadPercent,Is.EqualTo(10));Assert.That(first.Corruption,Is.Zero);
                Assert.That(controller.Session.View.Body.forceRenderingOff,Is.True);
                Assert.That(terminal.gameObject.activeSelf,Is.False);
                if(!eyesCapture && age>=rules.EntryDownloadDuration+1.5 && errors==0){ScreenCapture.CaptureScreenshot("Builds/BossFight/EntryPreview/entry-eyes.png");eyesCapture=true;}
                if(!errorCapture && errors==15){ScreenCapture.CaptureScreenshot("Builds/BossFight/EntryPreview/entry-errors.png");errorCapture=true;}
                yield return null;
            }
            Assert.That(panel.enabled,Is.True);Assert.That(mostWindows,Is.EqualTo(15));Assert.That(mostErrors,Is.EqualTo(15));Assert.That(frames.Count,Is.GreaterThan(1));
            yield return null;
            Assert.That(windows.All(x=>x!=null && x.enabled && x.sprite.texture==rules.phase1EntryErrorSprite.texture),Is.True,"All error windows remain behind One_more.");
            Assert.That(panel.sortingOrder,Is.GreaterThan(windows.Max(x=>x.sortingOrder)));
            var retainedDownload=world.Find("Boss entry downloading");
            Assert.That(retainedDownload!=null && retainedDownload.gameObject.activeSelf,Is.True);
            Assert.That(panel.sortingOrder,Is.GreaterThan(retainedDownload.GetComponentsInChildren<SpriteRenderer>().Max(x=>x.sortingOrder)));
            ScreenCapture.CaptureScreenshot("Builds/BossFight/EntryPreview/entry-one-more-over-errors.png");
            while(!terminal.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(terminal.gameObject.activeSelf,Is.True);Assert.That(panel.enabled,Is.False);
            yield return null;
            Assert.That(world.GetComponentsInChildren<SpriteRenderer>(true).Any(x=>x.name.StartsWith("Boss entry eye window ")),Is.False);
            Assert.That(world.Find("Boss entry downloading"),Is.Null);
            Assert.That(world.Find("WINDOW.exe").GetComponent<SpriteRenderer>().enabled,Is.True);
            while(controller.IsEntering && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(controller.IsEntering,Is.False);Assert.That(controller.Session.View.Body.forceRenderingOff,Is.False);
        }
        [UnityTest] public IEnumerator PhaseOneEntryShowsWarningThenTerminalBeforeAcceptingInput()
        {
            yield return BeginFirstPhase(true);var first=(BossPhase1Model)controller.Model;
            var world=GameObject.Find("Boss fight world").transform;
            var panel=world.Find("Boss entry warning").GetComponent<SpriteRenderer>();
            var terminal=world.Find("Phase 1 terminal").GetComponent<TMPro.TextMeshPro>();
            Assert.That(controller.IsEntering,Is.True);Assert.That(panel.enabled,Is.True);
            Assert.That(panel.sprite.texture,Is.SameAs(rules.phase1IntroSprite.texture));
            Assert.That(Vector2.Distance(panel.bounds.center,controller.Session.Space.ToWorld(new BossPoint(960,516).Vector)),Is.LessThan(.0001f));
            Assert.That(terminal.gameObject.activeSelf,Is.False);Assert.That(controller.Session.View.Body.forceRenderingOff,Is.True);
            Assert.That(world.Find("WINDOW.exe").GetComponent<SpriteRenderer>().enabled,Is.False);
            var body=first.Body;var trash=first.Trash;
            yield return Pointer(new BossPoint(body.x+20,body.y-18),true);
            yield return Pointer(new BossPoint(body.x+120,body.y-18),false);
            Assert.That(first.Body.x,Is.EqualTo(body.x));Assert.That(first.Trash.x,Is.EqualTo(trash.x));
            controller.ObserveFocus(false);yield return Settle();double pausedAt=first.Clock.CombatTime;
            yield return new WaitForSecondsRealtime(.2f);yield return Settle();
            Assert.That(first.Clock.CombatTime,Is.EqualTo(pausedAt));controller.ObserveFocus(true);
            double deadline=Time.realtimeSinceStartupAsDouble+6;
            while(panel.enabled && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(controller.IsEntering,Is.True);Assert.That(panel.enabled,Is.False);
            double commandWaitStartedAt=first.Clock.CombatTime;
            Assert.That(terminal.gameObject.activeSelf,Is.False,"Commands wait one second after One_more disappears.");
            Assert.That(world.Find("WINDOW.exe").GetComponent<SpriteRenderer>().enabled,Is.True);
            while(!terminal.gameObject.activeSelf && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(first.Clock.CombatTime-commandWaitStartedAt,Is.InRange(.8,1.2));
            Assert.That(terminal.gameObject.activeSelf,Is.True);Assert.That(terminal.text,Is.Not.Empty);
            Assert.That(world.Find("WINDOW.exe").GetComponent<SpriteRenderer>().enabled,Is.True,"The boss icon appears with the first command line.");
            Assert.That(world.Find("Recycle bin").GetComponent<SpriteRenderer>().enabled,Is.False);
            Assert.That(controller.Session.View.Body.forceRenderingOff,Is.True);
            double terminalStartedAt=first.Clock.CombatTime;
            yield return Space(true);
            while(controller.IsEntering && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(controller.IsEntering,Is.False);
            Assert.That(first.Clock.CombatTime-terminalStartedAt,Is.InRange(1.8,2.3));
            Assert.That(first.DownloadPercent,Is.EqualTo(10).Within(.03));Assert.That(first.Corruption,Is.Zero);
            Assert.That(controller.Session.View.Body.forceRenderingOff,Is.False);
            Assert.That(world.Find("WINDOW.exe").GetComponent<SpriteRenderer>().enabled,Is.True);
            Assert.That(world.Find("Recycle bin").GetComponent<SpriteRenderer>().enabled,Is.True);
            yield return Settle();Assert.That(first.Minimized,Is.False,"A Space held through entry must remain consumed.");
            yield return Space(false);yield return Space(true);Assert.That(first.Minimized,Is.True);
        }
        [UnityTest] public IEnumerator PhaseOneAuthoredPupilTracksWindowWhileEyeRootStaysFixedAndPauses()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;first.Invulnerable=true;
            Assert.That(rules.phase1EyeSprite,Is.Not.Null);Assert.That(rules.phase1PupilSprite,Is.Not.Null);
            var tracker=GameObject.Find("Boss hidden world").GetComponentsInChildren<WindowExe.Desktop.Desktop.Window.EyeTracker>()[0];
            var eye=tracker.GetComponent<SpriteRenderer>();var pupil=tracker.PupilTransform.GetComponent<SpriteRenderer>();
            Assert.That(eye.sprite.texture,Is.SameAs(rules.phase1EyeSprite.texture));
            Assert.That(pupil.sprite.texture,Is.SameAs(rules.phase1PupilSprite.texture));
            Assert.That(pupil.transform.parent,Is.SameAs(eye.transform));Assert.That(tracker.enabled,Is.False);
            var anchor=eye.transform.position;
            first.Teleport(new BossRect(450,440,150,160),new BossPoint(525,520));yield return Settle();yield return Settle();
            Assert.That(pupil.transform.localPosition.x,Is.LessThan(0));
            first.Teleport(new BossRect(640,440,150,160),new BossPoint(715,520));yield return Settle();yield return Settle();
            Assert.That(pupil.transform.localPosition.x,Is.GreaterThan(0));
            Assert.That(eye.transform.position,Is.EqualTo(anchor));
            Assert.That(((Vector2)pupil.transform.localPosition).magnitude,Is.LessThanOrEqualTo(tracker.MaxRadius+.0001f));
            Assert.That(pupil.sortingOrder,Is.GreaterThan(eye.sortingOrder));
            controller.ObserveFocus(false);yield return Settle();var paused=pupil.transform.localPosition;
            yield return Settle();Assert.That(pupil.transform.localPosition,Is.EqualTo(paused));controller.ObserveFocus(true);
        }
        [UnityTest] public IEnumerator PhaseTwoShowsSpacebarStackOnceWithoutBackgroundMessages()
        {
            Object.Destroy(controller.gameObject);yield return null;
            var art=UnityEditor.AssetDatabase.LoadAssetAtPath<BossFightSettings>("Assets/Data/Gameplay/BossFight/BossFightSettings.asset");
            rules.phase2SpacebarSprite=art.phase2SpacebarSprite;rules.eyeSprite=art.eyeSprite;rules.pupilSprite=art.pupilSprite;
            rules.phase2BackgroundSprite=art.phase2BackgroundSprite;
            Assert.That(rules.phase2SpacebarSprite,Is.Not.Null);
            controller=new GameObject("Phase two Spacebar presentation test").AddComponent<BossFightController>();
            controller.ObserveNativeApplicationState=false;controller.Configure(rules,template.GetComponent<BossFightWindow>());
            Assert.That(controller.BeginPhase2(out var error),Is.True,error);controller.Model.Invulnerable=true;controller.ObserveFocus(true);yield return Settle();
            var model=controller.Model;var world=GameObject.Find("Boss fight world").transform;
            Assert.That(world.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(x=>x.text.Contains("벽이 몰려오면") || x.text.Contains("오래는 못 숨어") || x.text.Contains("다운로드가 끝나면") || x.text.Contains("창을 닫지 마")),Is.False);
            Assert.That(world.GetComponentsInChildren<SpriteRenderer>(true).Any(x=>x.name.StartsWith("Phase 2 Spacebar hint ")),Is.False);
            double deadline=Time.realtimeSinceStartupAsDouble+8;SpriteRenderer[] hints;
            do
            {
                yield return null;
                hints=world.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Phase 2 Spacebar hint ") && x.enabled).OrderBy(x=>x.sortingOrder).ToArray();
            }while(hints.Length<3 && Time.realtimeSinceStartupAsDouble<deadline);
            Assert.That(hints.Length,Is.EqualTo(3));
            for(int i=0;i<hints.Length;i++)
            {
                Assert.That(hints[i].sprite.texture,Is.SameAs(rules.phase2SpacebarSprite.texture));
                if(i==0)continue;
                Assert.That(hints[i].transform.position.x,Is.GreaterThan(hints[i-1].transform.position.x));
                Assert.That(hints[i].transform.position.y,Is.LessThan(hints[i-1].transform.position.y));
                Assert.That(hints[i].bounds.Intersects(hints[i-1].bounds),Is.True);
            }
            ScreenCapture.CaptureScreenshot("Builds/BossFight/phase2-spacebar-hint.png");yield return null;
            yield return Space(true);yield return Space(false);Assert.That(model.Minimized,Is.True,"Hint images must not block Space.");
            controller.ObserveFocus(false);yield return Settle();double paused=model.Clock.CombatTime;
            yield return new WaitForSecondsRealtime(.25f);yield return Settle();
            Assert.That(model.Clock.CombatTime,Is.EqualTo(paused));Assert.That(hints.All(x=>x!=null && x.enabled),Is.True);
            controller.ObserveFocus(true);
            while(world.GetComponentsInChildren<SpriteRenderer>(true).Any(x=>x.name.StartsWith("Phase 2 Spacebar hint ")) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(world.GetComponentsInChildren<SpriteRenderer>(true).Any(x=>x.name.StartsWith("Phase 2 Spacebar hint ")),Is.False);
            Assert.That(model.Clock.CombatTime,Is.InRange(2.9,3.4));
            yield return KeyPress(Key.F8);yield return new WaitForSecondsRealtime(1.2f);yield return Settle();
            Assert.That(model.Attempt,Is.EqualTo(2));
            Assert.That(world.GetComponentsInChildren<SpriteRenderer>(true).Any(x=>x.name.StartsWith("Phase 2 Spacebar hint ")),Is.False,"Respawning must not replay the phase-entry hint.");
        }
        [UnityTest] public IEnumerator PhaseTwoAuthoredEyesStayCenteredTrackPauseAndRespawn()
        {
            Object.Destroy(controller.gameObject);yield return null;
            var art=UnityEditor.AssetDatabase.LoadAssetAtPath<BossFightSettings>("Assets/Data/Gameplay/BossFight/BossFightSettings.asset");
            rules.eyeSprite=art.eyeSprite;rules.pupilSprite=art.pupilSprite;
            rules.phase2BackgroundSprite=art.phase2BackgroundSprite;rules.phase2PupilDiameterRatio=art.phase2PupilDiameterRatio;
            Assert.That(rules.eyeSprite,Is.Not.Null);Assert.That(rules.pupilSprite,Is.Not.Null);
            controller=new GameObject("Authored phase two controller").AddComponent<BossFightController>();
            controller.ObserveNativeApplicationState=false;controller.Configure(rules,template.GetComponent<BossFightWindow>());
            Assert.That(controller.BeginPhase2(out var error),Is.True,error);controller.Model.Invulnerable=true;controller.ObserveFocus(true);yield return Settle();
            var model=controller.Model;var state=model.Eyes[0];
            var eye=GameObject.Find("Boss fight world").transform.Find("Eye "+state.Id).GetComponent<SpriteRenderer>();
            var tracker=eye.GetComponent<WindowExe.Desktop.Desktop.Window.EyeTracker>();var pupil=tracker.PupilTransform.GetComponent<SpriteRenderer>();
            var background=GameObject.Find("Boss fight world").transform.Find("Background").GetComponent<SpriteRenderer>();
            Assert.That(background.sprite.texture,Is.SameAs(art.phase1HiddenBackgroundSprite.texture));
            Assert.That(background.color,Is.EqualTo(Color.white));
            Assert.That(background.size.x/background.size.y,Is.EqualTo(background.sprite.rect.width/background.sprite.rect.height).Within(.0001));
            Assert.That(background.bounds.min.x,Is.LessThanOrEqualTo(controller.Session.Space.Snapshot.Bounds.min.x+.0001));
            Assert.That(background.bounds.max.x,Is.GreaterThanOrEqualTo(controller.Session.Space.Snapshot.Bounds.max.x-.0001));
            Assert.That(background.bounds.min.y,Is.LessThanOrEqualTo(controller.Session.Space.Snapshot.Bounds.min.y+.0001));
            Assert.That(background.bounds.max.y,Is.GreaterThanOrEqualTo(controller.Session.Space.Snapshot.Bounds.max.y-.0001));
            var backgroundPosition=background.transform.position;
            Assert.That(controller.Session.View.Body.color,Is.EqualTo(rules.safeDesktop));
            Assert.That(eye.sprite.texture,Is.SameAs(rules.eyeSprite.texture));Assert.That(pupil.sprite.texture,Is.SameAs(rules.pupilSprite.texture));
            Assert.That(eye.color,Is.EqualTo(Color.white));Assert.That(pupil.color,Is.EqualTo(Color.white));
            Assert.That(eye.sprite.pivot,Is.EqualTo(eye.sprite.rect.size*.5f));Assert.That(pupil.sprite.pivot,Is.EqualTo(pupil.sprite.rect.size*.5f));
            Assert.That(pupil.size.x/pupil.size.y,Is.EqualTo(rules.pupilSprite.rect.width/rules.pupilSprite.rect.height).Within(.0001));
            Assert.That(Mathf.Max(pupil.size.x,pupil.size.y)/Mathf.Max(eye.size.x,eye.size.y),Is.EqualTo(.55f).Within(.0001));
            Assert.That(tracker.enabled,Is.False);Assert.That(pupil.transform.parent,Is.SameAs(eye.transform));
            Assert.That(Vector2.Distance(eye.bounds.center,controller.Session.Space.ToWorld(state.Position.Vector)),Is.LessThan(.0001f));
            Assert.That(pupil.transform.localPosition.x,Is.LessThan(0));
            model.Teleport(new BossRect(1770,435,140,210),new BossPoint(1840,540));yield return Settle();yield return Settle();
            Assert.That(background.transform.position,Is.EqualTo(backgroundPosition));
            Assert.That(pupil.transform.localPosition.x,Is.GreaterThan(0));Assert.That(((Vector2)pupil.transform.localPosition).magnitude,Is.LessThanOrEqualTo(tracker.MaxRadius+.0001));
            controller.ObserveFocus(false);yield return Settle();var anchor=eye.transform.position;var paused=pupil.transform.localPosition;
            yield return Settle();Assert.That(eye.transform.position,Is.EqualTo(anchor));Assert.That(pupil.transform.localPosition,Is.EqualTo(paused));
            controller.ObserveFocus(true);yield return KeyPress(Key.F8);
            Assert.That(eye==null,Is.True);Assert.That(model.Attempt,Is.EqualTo(2));
            var eyes=GameObject.Find("Boss fight world").GetComponentsInChildren<WindowExe.Desktop.Desktop.Window.EyeTracker>();
            Assert.That(eyes.Length,Is.EqualTo(7+rules.phase2FloatingEyes.Length));Assert.That(eyes.All(x=>x.GetComponent<SpriteRenderer>().sprite.texture==rules.eyeSprite.texture),Is.True);
            yield return Space(true);yield return Space(false);Assert.That(model.Minimized,Is.True);
            Assert.That(background.enabled,Is.True);Assert.That(background.transform.position,Is.EqualTo(backgroundPosition));
            yield return Space(true);yield return Space(false);Assert.That(model.Minimized,Is.False);
            Assert.That(controller.Session.View.Body.color,Is.EqualTo(rules.safeDesktop));
            yield return new WaitForSecondsRealtime(.65f);
            var folder=System.IO.Path.GetFullPath("Builds/BossFight");System.IO.Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"phase2-varied-floating-eyes.png"));yield return Settle();
        }
        [UnityTest] public IEnumerator PhaseOneCorruptionDoesNotInterruptHeldResize()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;
            first.Teleport(new BossRect(100,500,330,240),new BossPoint(220,620));yield return Settle();
            yield return Pointer(new BossPoint(430,620),true);
            yield return Pointer(new BossPoint(600,620),true);
            Assert.That(first.Exposed,Is.True);
            yield return new WaitForSecondsRealtime(.7f);yield return Settle();
            Assert.That(first.Corruption,Is.EqualTo(1));Assert.That(first.Attempt,Is.EqualTo(1));
            yield return Pointer(new BossPoint(650,620),true);
            Assert.That(first.Body.width,Is.EqualTo(550).Within(.05),"Corruption must not drop a window resize before its size limit.");
            yield return Pointer(new BossPoint(650,620),false);
        }
        [UnityTest] public IEnumerator PhaseOneRawResizeShrinksToSmallSquareWithoutAxisTradeoff()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;
            first.Teleport(new BossRect(175,675,240,90),new BossPoint(220,720));yield return Settle();
            yield return Pointer(new BossPoint(415,720),true);yield return Pointer(new BossPoint(265,720),true);
            Assert.That(first.Body.width,Is.EqualTo(90).Within(.05));Assert.That(first.Body.height,Is.EqualTo(90).Within(.05));
            yield return Pointer(new BossPoint(265,720),false);
            first.Teleport(new BossRect(175,600,90,240),new BossPoint(220,645));yield return Settle();
            yield return Pointer(new BossPoint(220,840),true);yield return Pointer(new BossPoint(220,690),true);
            Assert.That(first.Body.width,Is.EqualTo(90).Within(.05));Assert.That(first.Body.height,Is.EqualTo(90).Within(.05));
            yield return Pointer(new BossPoint(220,690),false);
        }
        [UnityTest] public IEnumerator PhaseOneWindowMoveKeepsTrashFixedUntilSeparateDrag()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;
            var originalTrash=first.Trash;
            yield return Pointer(new BossPoint(150,582),true);yield return Pointer(new BossPoint(170,582),true);
            yield return Pointer(new BossPoint(170,582),false);
            Assert.That(first.Body.x,Is.EqualTo(120).Within(.05));
            Assert.That((first.Trash-originalTrash).Magnitude,Is.LessThan(.05),"Window movement must not carry the bin.");
            var window=first.Body;
            yield return Pointer(first.Trash,true);yield return Pointer(new BossPoint(250,720),true);
            yield return Pointer(new BossPoint(250,720),false);
            Assert.That(first.Trash.x,Is.EqualTo(250).Within(.05));Assert.That(first.Body.x,Is.EqualTo(window.x));
            Assert.That(first.Body.y,Is.EqualTo(window.y));Assert.That(first.Body.width,Is.EqualTo(window.width));
        }
        [UnityTest] public IEnumerator NarrowWindowTopEdgeResizesAboveBothButtons()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;
            foreach(double x in new[]{252d,226d})
            {
                first.Teleport(new BossRect(175,675,90,240),new BossPoint(220,720));yield return Settle();
                var reference=new Vector2((float)x,639);var world=controller.Session.Space.ToWorld(reference);
                Assert.That(controller.Session.View.ButtonAt(world),Is.EqualTo(-1));
                yield return Pointer(new BossPoint(x,639),true);yield return Pointer(new BossPoint(x,619),true);
                Assert.That(first.Body.y,Is.EqualTo(655).Within(.05));Assert.That(first.Body.height,Is.EqualTo(260).Within(.05));
                yield return Pointer(new BossPoint(x,619),false);
                Assert.That(first.Attempt,Is.EqualTo(1));Assert.That(first.Minimized,Is.False);
            }
        }
        [UnityTest] public IEnumerator WiderOuterResizeTargetsWorkInAllDirectionsInBothPhases()
        {
            yield return BeginFirstPhase();
            for(int phase=0;phase<2;phase++)
            {
                if(phase==1)yield return KeyPress(Key.F9);
                controller.Model.Invulnerable=true;
                for(int direction=0;direction<8;direction++)
                {
                    var model=controller.Model;var body=new BossRect(200,400,300,240);var trash=new BossPoint(350,520);
                    model.Teleport(body,trash);yield return Settle();
                    bool left=direction==0||direction==4||direction==6,right=direction==1||direction==5||direction==7;
                    bool top=direction==2||direction==4||direction==5,bottom=direction==3||direction==6||direction==7;
                    var point=new BossPoint(left?180:right?520:350,top?344:bottom?660:502);
                    var delta=new BossPoint(left?-20:right?20:0,top?-20:bottom?20:0);
                    var world=controller.Session.Space.ToWorld(point.Vector);
                    Assert.That(controller.Session.View.ResizeAt(world),Is.EqualTo((WindowExe.Desktop.Desktop.Window.ResizeDirection)direction));
                    Assert.That(controller.Session.View.Hit(world),Is.True);
                    yield return Pointer(point,true);yield return Pointer(point+delta,true);yield return Pointer(point+delta,false);
                    Assert.That(model.Body.x,Is.EqualTo(left?180:200).Within(.05),"left anchor "+phase+"/"+direction);
                    Assert.That(model.Body.y,Is.EqualTo(top?380:400).Within(.05),"top anchor "+phase+"/"+direction);
                    Assert.That(model.Body.width,Is.EqualTo(left||right?320:300).Within(.05));
                    Assert.That(model.Body.height,Is.EqualTo(top||bottom?260:240).Within(.05));
                    Assert.That((model.Trash-trash).Magnitude,Is.LessThan(.05));
                }
            }
        }
        [UnityTest] public IEnumerator PhaseOneCorruptionDoesNotInterruptHeldWindowMove()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;
            first.Teleport(new BossRect(400,500,330,240),new BossPoint(500,620));yield return Settle();
            yield return Pointer(new BossPoint(420,482),true);
            yield return Pointer(new BossPoint(430,482),true);
            Assert.That(first.Exposed,Is.True);
            yield return new WaitForSecondsRealtime(.7f);yield return Settle();
            Assert.That(first.Corruption,Is.EqualTo(1));Assert.That(first.Attempt,Is.EqualTo(1));
            yield return Pointer(new BossPoint(440,482),true);
            Assert.That(first.Body.x,Is.EqualTo(420).Within(.05),"Corruption must leave the held title drag usable.");
            yield return Pointer(new BossPoint(440,482),false);
        }
        [UnityTest] public IEnumerator PhaseOneCorruptionStillDropsTrashUntilFreshPress()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;
            yield return Pointer(first.Trash,true);yield return Pointer(new BossPoint(260,720),true);
            Assert.That(first.Trash.x,Is.EqualTo(260).Within(.05));
            // Introduce exposure while the bin is held, then clear it after the penalty.
            first.Teleport(new BossRect(100,500,500,340),first.Trash);
            yield return new WaitForSecondsRealtime(.8f);yield return Settle();
            Assert.That(first.Corruption,Is.EqualTo(1));
            first.Teleport(rules.phase1.initialWindow,first.Trash);yield return Settle();
            yield return Pointer(new BossPoint(280,720),true);
            Assert.That(first.Trash.x,Is.EqualTo(260).Within(.05),"The previous held drag must remain consumed.");
            yield return Pointer(new BossPoint(280,720),false);
            yield return Pointer(first.Trash,true);yield return Pointer(new BossPoint(300,720),true);
            Assert.That(first.Trash.x,Is.EqualTo(300).Within(.05));yield return Pointer(first.Trash,false);
        }
        [UnityTest] public IEnumerator FullRouteThroughRawInputTransitionsToFreshPhaseTwoAndReleasesHiddenResources()
        {
            int cameraMask=Camera.main.cullingMask;yield return BeginFirstPhase();
            Assert.That(Camera.main.cullingMask&(1<<rules.hiddenLayer),Is.Zero);
            var texture=controller.Session.View.Body.sharedMaterial.GetTexture("_BossHiddenTexture") as RenderTexture;
            Assert.That(texture,Is.Not.Null);Assert.That(texture.IsCreated(),Is.True);Assert.That(texture.depth,Is.GreaterThan(0));
            Assert.That(GameObject.Find("Boss hidden world").GetComponentsInChildren<SpriteRenderer>().Any(x=>x.name=="Floor"),Is.False);
            var first=(BossPhase1Model)controller.Model;var route=BossPhase1RouteTests.Route(rules.phase1);
            for(int i=1;i<route.Count;i++)
            {
                var before=route[i-1];var after=route[i];var b=before.body;var a=after.body;BossPoint press,release;
                if((before.trash-after.trash).SqrMagnitude>0){press=before.trash;release=after.trash;}
                else if(b.width==a.width && b.height==a.height){press=new BossPoint(b.x+20,b.y-18);release=press+new BossPoint(a.x-b.x,a.y-b.y);}
                else if(b.x!=a.x){press=new BossPoint(b.x,b.y+b.height*.5);release=new BossPoint(a.x,press.y);}
                else if(b.Right!=a.Right){press=new BossPoint(b.Right,b.y+b.height*.5);release=new BossPoint(a.Right,press.y);}
                else if(b.y!=a.y){press=new BossPoint(b.x+b.width*.5,b.y-36);release=new BossPoint(press.x,a.y-36);}
                else{press=new BossPoint(b.x+b.width*.5,b.Bottom);release=new BossPoint(press.x,a.Bottom);}
                double now=InputState.currentTime;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=Screen(press)}.WithButton(MouseButton.Left),now+.001);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=Screen(release)}.WithButton(MouseButton.Left),now+.006);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=Screen(release)},now+.011);
                yield return Settle();
                Assert.That(first.Body.x,Is.EqualTo(a.x).Within(.05),"body x step "+i);
                Assert.That(first.Body.y,Is.EqualTo(a.y).Within(.05),"body y step "+i);
                Assert.That(first.Body.width,Is.EqualTo(a.width).Within(.05),"width step "+i);
                Assert.That(first.Body.height,Is.EqualTo(a.height).Within(.05),"height step "+i);
                Assert.That((first.Trash-after.trash).Magnitude,Is.LessThan(.05),"trash step "+i+" before="+JsonUtility.ToJson(before.trash)+" expected="+JsonUtility.ToJson(after.trash)+" actual="+JsonUtility.ToJson(first.Trash)+" body="+JsonUtility.ToJson(first.Body)+" captured="+first.Captured+" boss="+JsonUtility.ToJson(first.Boss)+" exposed="+first.Exposed+" corruption="+first.Corruption+" pause="+first.Clock.PauseReasons+" metrics="+JsonUtility.ToJson(controller.InputSource.Timeline.Metrics));
            }
            yield return Settle();Assert.That(first.Corruption,Is.Zero);Assert.That(first.Attempt,Is.EqualTo(1));
            Assert.That(first.Captured,Is.True,$"body={JsonUtility.ToJson(first.Body)} boss={JsonUtility.ToJson(first.Boss)} hidden={first.Minimized} locked={first.CombatLocked}");Assert.That((first.Trash-route[route.Count-1].trash).Magnitude,Is.LessThan(.05));
            yield return Pointer(first.Boss,true);yield return Pointer(first.Trash,true);yield return Pointer(first.Trash,false);
            Assert.That(first.CombatLocked,Is.True);Assert.That(controller.Session.Outcome,Is.EqualTo(BossOutcome.None));
            yield return new WaitForSecondsRealtime(2.25f);yield return Settle();
            Assert.That(controller.Model,Is.TypeOf<BossPhase2Model>(),$"active={controller.IsActive}, pause={first.Clock.PauseReasons}, transition age={first.TransitionAge}, input={first.Clock.InputTime}");Assert.That(controller.Model.DownloadPercent,Is.InRange(30,31));
            Assert.That(controller.Model.Attempt,Is.EqualTo(1));Assert.That(controller.Model.Eyes.Count,Is.EqualTo(7+rules.phase2FloatingEyes.Length));
            Assert.That(Camera.main.cullingMask,Is.EqualTo(cameraMask));
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(x=>x.name=="Boss hidden layer (Runtime)"),Is.Zero);
            Assert.That(Resources.FindObjectsOfTypeAll<Sprite>().Count(x=>x.name.StartsWith("Boss phase 1 ") && x.name.EndsWith(" (Runtime)")),Is.Zero);
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(x=>x.name=="Boss hidden camera"),Is.False);
        }
        [UnityTest] public IEnumerator FirstDropTransitionContinuesWithoutFurtherInput()
        {
            yield return BeginFirstPhase();var first=(BossPhase1Model)controller.Model;
            Assert.That(rules.phase1HiddenBackgroundSprite,Is.Not.Null);
            var background=GameObject.Find("Boss hidden world").transform.Find("Hidden background").GetComponent<SpriteRenderer>();
            Assert.That(background.enabled,Is.True);
            Assert.That(background.sprite.texture,Is.SameAs(rules.phase1HiddenBackgroundSprite.texture));
            Assert.That(background.gameObject.layer,Is.EqualTo(rules.hiddenLayer));
            Assert.That(Camera.main.cullingMask&(1<<rules.hiddenLayer),Is.Zero,"Hidden artwork must never draw directly outside the window.");
            Assert.That(background.size.x/background.size.y,Is.EqualTo(background.sprite.rect.width/background.sprite.rect.height).Within(.0001));
            Assert.That(background.size.x/controller.Session.Space.Scale,Is.GreaterThanOrEqualTo(1920-.0001f));
            Assert.That(background.size.y/controller.Session.Space.Scale,Is.GreaterThanOrEqualTo(1032-.0001f));
            Assert.That(background.transform.parent.GetComponentsInChildren<TMPro.TMP_Text>(true),Is.Empty);
            yield return KeyPress(Key.Space);
            Assert.That(first.Minimized,Is.True);
            Assert.That(GameObject.Find("Boss hidden camera").GetComponent<Camera>().enabled,Is.False);
            yield return KeyPress(Key.Space);
            Assert.That(first.Minimized,Is.False);
            first.Teleport(new BossRect(1500,650,340,180),new BossPoint(1610,720));yield return Settle();
            yield return Pointer(first.Boss,true);yield return Pointer(first.Trash,true);yield return Pointer(first.Trash,false);
            Assert.That(first.CombatLocked,Is.True);
            Assert.That(rules.phaseTransitionDosSprite,Is.Not.Null);
            int mostDosWindows=0;double deadline=Time.realtimeSinceStartupAsDouble+4;
            while(controller.Model==first && Time.realtimeSinceStartupAsDouble<deadline)
            {
                var windows=GameObject.Find("Boss fight world").GetComponentsInChildren<SpriteRenderer>()
                    .Where(x=>x.name.StartsWith("Transition DOS window ") && x.enabled).OrderBy(x=>x.sortingOrder).ToArray();
                if(first.TransitionAge<1.5)Assert.That(windows,Is.Empty,"DOS windows must wait until the error image ends.");
                mostDosWindows=Mathf.Max(mostDosWindows,windows.Length);
                for(int i=0;i<windows.Length;i++)
                {
                    Assert.That(windows[i].sprite.texture,Is.SameAs(rules.phaseTransitionDosSprite.texture));
                    Assert.That(windows[i].size.x/windows[i].size.y,Is.EqualTo(1728f/910).Within(.0001));
                    int number=int.Parse(windows[i].name.Substring("Transition DOS window ".Length));
                    if(number==1 || number==5)
                    {
                        var center=number==1?new BossPoint(690,330):new BossPoint(1230,702);
                        Assert.That(Vector2.Distance(windows[i].transform.position,controller.Session.Space.ToWorld(center.Vector)),Is.LessThan(.0001f));
                        continue;
                    }
                    var previous=windows.First(x=>x.name=="Transition DOS window "+(number-1));
                    float direction=number<=4?1:-1;
                    Assert.That((windows[i].transform.position.x-previous.transform.position.x)*direction,Is.GreaterThan(0));
                    Assert.That((windows[i].transform.position.y-previous.transform.position.y)*direction,Is.LessThan(0));
                    Assert.That(windows[i].bounds.Intersects(previous.bounds),Is.True,"Each staircase must overlap in its own direction.");
                }
                yield return null;
            }
            yield return Settle();
            Assert.That(mostDosWindows,Is.EqualTo(8));
            Assert.That(GameObject.Find("Boss fight world").GetComponentsInChildren<SpriteRenderer>(true).Any(x=>x.name.StartsWith("Transition DOS window ")),Is.False);
            Assert.That(controller.Model,Is.TypeOf<BossPhase2Model>(),$"active={controller.IsActive}, pause={first.Clock.PauseReasons}, transition age={first.TransitionAge}, stopped={first.Clock.Stopped}");
            Assert.That(background==null,Is.True,"Hidden background resources must be released on entering phase two.");
            Assert.That(GameObject.Find("Boss fight world").transform.Find("Background").GetComponent<SpriteRenderer>().sprite.texture,Is.SameAs(rules.phase2BackgroundSprite.texture));
            Assert.That(controller.Session.View.Body.color,Is.EqualTo(rules.safeDesktop));
            var eyes=GameObject.Find("Boss fight world").GetComponentsInChildren<WindowExe.Desktop.Desktop.Window.EyeTracker>();
            Assert.That(eyes.Length,Is.EqualTo(7+rules.phase2FloatingEyes.Length));
            Assert.That(eyes.All(x=>x.GetComponent<SpriteRenderer>().sprite.texture==rules.eyeSprite.texture && x.PupilTransform.GetComponent<SpriteRenderer>().sprite.texture==rules.pupilSprite.texture),Is.True);
        }
        [UnityTest] public IEnumerator HorizontalNoiseIsReducedInBothPhasesAndRestoredForEnding()
        {
            var effect=CRTOverlay.Instance;effect.transitionTime=0;effect.ResetVisuals();effect.SetLevel(2);
            void AssertReduced(int level)
            {
                Assert.That(effect.Displayed.scanline,Is.EqualTo(effect.levels[level].scanline*rules.crtBaseScanlineScale).Within(.00001));
                Assert.That(effect.Displayed.streak,Is.EqualTo(effect.levels[level].streak*rules.crtBaseStreakScale).Within(.00001));
                Assert.That(effect.Displayed.streakDensity,Is.EqualTo(effect.levels[level].streakDensity*rules.crtBaseStreakDensityScale).Within(.00001));
                Assert.That(effect.Displayed.grain,Is.EqualTo(effect.levels[level].grain).Within(.00001));
                Assert.That(effect.RuntimeMaterial.GetFloat("_StreakStrength"),Is.EqualTo(effect.Displayed.streak).Within(.00001));
            }
            AssertReduced(2);
            yield return BeginFirstPhase();AssertReduced(0);
            var folder=System.IO.Path.GetFullPath("Builds/BossFight");System.IO.Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"reduced-horizontal-verified-phase1.png"));yield return Settle();
            effect.Spike(.05f);Assert.That(effect.Displayed.streak,Is.EqualTo(effect.spike.streak).Within(.00001));
            yield return new WaitForSecondsRealtime(.35f);AssertReduced(0);
            yield return KeyPress(Key.F9);yield return new WaitForSecondsRealtime(.65f);AssertReduced(2);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"reduced-horizontal-verified-phase2.png"));yield return Settle();
            var model=controller.Model;model.Teleport(new BossRect(1500,400,400,300),new BossPoint(1700,540));model.DragBoss(model.Trash);model.ResolveBoundary(successDrop:true);
            yield return Settle();
            var id=controller.Session.SessionId;var ending=controller.TransferCrtToEnding(id);Assert.That(ending,Is.Not.Null);
            Assert.That(effect.Displayed.streak,Is.EqualTo(effect.levels[2].streak).Within(.00001));
            Assert.That(ending.SetBaseHorizontalNoise(.5f,.5f,.5f),Is.True);Assert.That(controller.CompleteEnding(id),Is.True);
            Assert.That(effect.Displayed.streak,Is.EqualTo(effect.levels[2].streak*.5f).Within(.00001));
            Assert.That(ending.Release(),Is.True);Assert.That(effect.Displayed.streak,Is.EqualTo(effect.levels[2].streak).Within(.00001));
        }
        [UnityTest] public IEnumerator DebugKeysPreserveDownloadAcrossSkipAndRestart()
        {
            yield return BeginFirstPhase();yield return KeyPress(Key.F7);yield return KeyPress(Key.F10);
            Assert.That(controller.Model.Invulnerable,Is.True);Assert.That(controller.Model.Clock.DownloadDebugPaused,Is.True);
            yield return KeyPress(Key.F9);Assert.That(controller.Model,Is.TypeOf<BossPhase2Model>());
            Assert.That(controller.Model.Invulnerable,Is.True);Assert.That(controller.Model.DownloadPercent,Is.EqualTo(30));
            int attempt=controller.Model.Attempt;yield return KeyPress(Key.F8);
            Assert.That(controller.Model.Attempt,Is.EqualTo(attempt+1));Assert.That(controller.Model.DownloadPercent,Is.EqualTo(30));
            yield return KeyPress(Key.F10);Assert.That(controller.Model.Clock.DownloadDebugPaused,Is.False);
        }
        [UnityTest] public IEnumerator DeadlineSignalsOnceAndDebugRestartCannotReviveIt()
        {
            Object.Destroy(controller.gameObject);yield return null;rules.downloadSeconds=.4;
            mouse.MakeCurrent();keyboard.MakeCurrent();controller=new GameObject("Deadline controller").AddComponent<BossFightController>();
            controller.ObserveNativeApplicationState=false;controller.Configure(rules,template.GetComponent<BossFightWindow>());int results=0;controller.BadEndingRequested+=_=>results++;
            Assert.That(controller.BeginPhase2(out var error),Is.True,error);controller.ObserveFocus(true);
            yield return new WaitForSecondsRealtime(.7f);yield return Settle();
            Assert.That(controller.Model.Outcome,Is.EqualTo(BossOutcome.BadEnding));Assert.That(results,Is.EqualTo(1));
            int attempt=controller.Model.Attempt;yield return KeyPress(Key.F8);yield return Space(true);
            Assert.That(controller.Model.Attempt,Is.EqualTo(attempt));Assert.That(controller.Model.DownloadPercent,Is.EqualTo(100));Assert.That(results,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator DeadlineMovieUsesBadClipLocksCleanupAndDisposesOnExit()
        {
            rules.normalEndingVideo=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>("Assets/Art/Screen/Normal_Ending.mp4");
            rules.badEndingVideo=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>("Assets/Art/Screen/Bad_Ending.mp4");
            Assert.That(rules.normalEndingVideo,Is.Not.Null);Assert.That(rules.badEndingVideo,Is.Not.Null);
            var model=controller.Model;model.Clock.Advance(model.Clock.InputTime+rules.downloadSeconds+1);model.ResolveBoundary();
            var surface=GameObject.Find("Boss success blackout");Assert.That(surface,Is.Not.Null);
            var player=surface.GetComponentInChildren<UnityEngine.Video.VideoPlayer>();Assert.That(player.clip,Is.SameAs(rules.badEndingVideo));
            Assert.That(player.isLooping,Is.False);Assert.That(controller.CompleteEnding(controller.Session.SessionId),Is.False);
            double deadline=Time.realtimeSinceStartupAsDouble+8;
            while((!player.isPrepared || player.frame<0) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(player.isPrepared,Is.True);Assert.That(player.frame,Is.GreaterThanOrEqualTo(0));
            Assert.That(model.Outcome,Is.EqualTo(BossOutcome.BadEnding));Assert.That(model.Respawn(),Is.False);
            controller.enabled=false;yield return null;
            Assert.That(player==null,Is.True);Assert.That(surface==null,Is.True);
        }
        [UnityTest] public IEnumerator EntryRequestOwnsCrtAndCannotBeRepeatedAfterEnding()
        {
            Object.Destroy(controller.gameObject);yield return null;
            controller=new GameObject("Entry context controller").AddComponent<BossFightController>();controller.ObserveNativeApplicationState=false;controller.Configure(rules,template.GetComponent<BossFightWindow>());
            var incoming=CRTOverlay.Instance.AcquireControl();var context=new BossFightEntryContext{RequestId="prelude-1",StartAtPhase2=true,CrtControl=incoming};
            Assert.That(controller.BeginBossFight(context,out var error),Is.True,error);Assert.That(incoming.IsCurrent,Is.False);
            var model=controller.Model;model.Invulnerable=true;model.Teleport(new BossRect(1500,400,400,300),new BossPoint(1700,540));model.DragBoss(model.Trash);model.ResolveBoundary(successDrop:true);
            yield return Settle();
            string id=controller.Session.SessionId;Assert.That(controller.CompleteEnding(id),Is.True);
            Assert.That(controller.BeginBossFight(context,out error),Is.False);Assert.That(controller.IsActive,Is.False);
        }
        [UnityTest] public IEnumerator MoveMinimizeAndOldReleaseThenFreshClosePreserveDownload()
        {
            var model=controller.Model;yield return Pointer(new BossPoint(160,417),true);
            yield return Pointer(new BossPoint(180,417),true);double moved=model.Body.x;
            Assert.That(moved,Is.EqualTo(125).Within(.01),"pause="+model.Clock.PauseReasons+" metrics="+JsonUtility.ToJson(controller.InputSource.Timeline.Metrics));yield return Space(true);
            Assert.That(model.Minimized,Is.True);Assert.That(model.Body.x,Is.EqualTo(moved));
            yield return Pointer(new BossPoint(180,417),false);yield return Space(false);yield return Space(true);
            Assert.That(model.Minimized,Is.False);Assert.That(model.Body.x,Is.EqualTo(moved));yield return Space(false);
            int attempt=model.Attempt;double elapsed=model.Clock.DownloadElapsed;
            var close=new BossPoint(model.Body.Right-13,model.Body.y-18);
            yield return Pointer(close,true);yield return Pointer(close,false);
            Assert.That(model.Attempt,Is.EqualTo(attempt+1));Assert.That(model.Clock.DownloadElapsed,Is.GreaterThan(elapsed));
            Assert.That(model.Body.x,Is.EqualTo(rules.phase2Window.x));
            Assert.That(controller.Session.View.gameObject.activeSelf,Is.True);
        }
        [UnityTest] public IEnumerator ResizeAndTrashKeepCurrentStateWhenSpaceEndsDrag()
        {
            var model=controller.Model;
            yield return Pointer(new BossPoint(model.Body.Right,500),true);yield return Pointer(new BossPoint(600,500),true);
            Assert.That(model.Body.Right,Is.EqualTo(600).Within(.01));double width=model.Body.width;
            yield return Space(true);yield return Pointer(new BossPoint(600,500),false);yield return Space(false);yield return Space(true);yield return Space(false);
            Assert.That(model.Body.width,Is.EqualTo(width));
            yield return Pointer(model.Trash,true);yield return Pointer(new BossPoint(400,540),true);
            double trashX=model.Trash.x;Assert.That(trashX,Is.EqualTo(400).Within(.01));
            yield return Space(true);yield return Pointer(new BossPoint(400,540),false);
            Assert.That(model.Trash.x,Is.EqualTo(trashX));Assert.That(model.Outcome,Is.EqualTo(BossOutcome.None));
        }
        [UnityTest] public IEnumerator CapturedBossDropNotifiesOnceAndEndingOwnershipSurvives()
        {
            rules.phase2SuccessSound=UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/깨진삐.mp3");
            rules.entryErrorSound=UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/에러(띵).mp3");
            var model=controller.Model;int results=0;BossFightResult result=null;
            controller.RestoreCancelled+=r=>{results++;result=r;};
            model.Teleport(new BossRect(1500,400,400,300),new BossPoint(1700,540));yield return Settle();
            yield return Pointer(model.Boss,true);yield return Pointer(model.Trash,true);yield return Pointer(model.Trash,false);
            Assert.That(model.Outcome,Is.EqualTo(BossOutcome.Success));Assert.That(results,Is.Zero);
            Assert.That(controller.SuccessPresentationComplete,Is.False);
            Assert.That(controller.CompleteEnding(controller.Session.SessionId),Is.False);
            Assert.That(controller.TransferCrtToEnding(controller.Session.SessionId),Is.Null);
            Assert.That(controller.AllowEnding(controller.Session.SessionId,new[]{"memo"}),Is.False);
            double frozen=model.Clock.CombatTime,download=model.Clock.DownloadElapsed;int attempt=model.Attempt;
            controller.ObserveFocus(false);yield return Settle();yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.SuccessPresentationComplete,Is.False);Assert.That(results,Is.Zero);
            controller.ObserveFocus(true);yield return Settle();yield return KeyPress(Key.F8);yield return Space(true);
            double deadline=Time.realtimeSinceStartupAsDouble+rules.phase2SuccessSound.length+2;
            while(!controller.SuccessPresentationComplete && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(controller.SuccessPresentationComplete,Is.True);Assert.That(results,Is.EqualTo(1));Assert.That(result.SessionId,Is.EqualTo(controller.Session.SessionId));
            Assert.That(model.Clock.CombatTime,Is.EqualTo(frozen));Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(download));Assert.That(model.Attempt,Is.EqualTo(attempt));
            double elapsed=model.Clock.DownloadElapsed;yield return Space(true);yield return Settle();
            Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(elapsed));Assert.That(results,Is.EqualTo(1));
            Assert.That(controller.AllowEnding("old session",new[]{"memo"}),Is.False);
            Assert.That(controller.AllowEnding(result.SessionId,new[]{"memo"}),Is.True);
            var lease=controller.TransferCrtToEnding(result.SessionId);Assert.That(lease,Is.Not.Null);lease.SetLevel(1);
            Assert.That(controller.CompleteEnding(result.SessionId),Is.True);Assert.That(controller.IsActive,Is.False);
            Assert.That(lease.SetLevel(3),Is.True);lease.Release();
        }
        [UnityTest] public IEnumerator SpaceDuringBossDragReturnsBossAndConsumesTheOldDrop()
        {
            var model=controller.Model;int success=0;controller.RestoreCancelled+=_=>success++;
            model.Teleport(new BossRect(1500,400,400,300),new BossPoint(1700,540));yield return Settle();
            yield return Pointer(model.Boss,true);yield return Pointer(model.Trash,true);yield return Space(true);
            Assert.That(model.Minimized,Is.True);Assert.That((model.Boss-rules.phase2Boss).Magnitude,Is.Zero);
            yield return Pointer(model.Trash,false);yield return Space(false);yield return Space(true);
            Assert.That(model.Minimized,Is.False);Assert.That(success,Is.Zero);Assert.That(model.Outcome,Is.EqualTo(BossOutcome.None));
        }
        [UnityTest] public IEnumerator FocusPauseRestoresDragAndDefersDownloadUntilResume()
        {
            var model=controller.Model;yield return Pointer(new BossPoint(160,417),true);yield return Pointer(new BossPoint(180,417),true);
            controller.ObserveFocus(false);yield return Settle();
            Assert.That(model.Clock.IsPaused,Is.True);Assert.That(model.Body.x,Is.EqualTo(rules.phase2Window.x));
            double elapsed=model.Clock.DownloadElapsed;yield return new WaitForSecondsRealtime(.25f);Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(elapsed));
            yield return Pointer(new BossPoint(180,417),false);controller.ObserveFocus(true);yield return Settle();
            Assert.That(model.Clock.IsPaused,Is.False);Assert.That(model.Clock.DownloadElapsed-elapsed,Is.LessThan(.25));
        }
    }
}
