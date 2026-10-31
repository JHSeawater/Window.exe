using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using WindowExe.Desktop;

namespace WindowExe.BossFight
{
    /// <summary>Explicit development-only validation surface. Never installed by the game bootstrap.</summary>
    public sealed class BossInputProbe : MonoBehaviour
    {
        [Serializable] public sealed class Record
        { public BossInputSample sample; public string disposition; public double committed; }
        [Serializable] public sealed class Frame
        { public double collection, cutoff, interval; public int injectedDelayMs; }
        [Serializable] public sealed class Report
        {
            public string mouse, keyboard;
            public double holdback, started, ended, maxResponse;
            public int presses, releases, spaces, escapes, cancellations;
            public BossInputMetrics metrics;
            public List<Record> records;
            public List<BossInputSample> initialStates;
            public List<Frame> frames;
            public int targetFps;
            public bool hitchProfile;
        }
        public double holdback = .05;
        private BossInputSource source;
        private Text label;
        private RectTransform pointer;
        private readonly List<Record> records = new List<Record>();
        private Report report;
        private double nextReport;
        private int targetFps=60, oldFps, oldVsync, nextHitch=100, injectedDelay;
        private bool hitchProfile, settingsCaptured, started;
        private double lastFrame, nextHitchTime;
        private readonly List<Frame> frames=new List<Frame>();
        public BossInputSource Source => source;
        private void Start()
        {
            started=true;
            Activate();
        }
        private void OnEnable() { if(started && Application.isPlaying) Activate(); }
        private void Activate()
        {
            if(source!=null) return;
            oldFps=Application.targetFrameRate; oldVsync=QualitySettings.vSyncCount; settingsCaptured=true;
            Begin();
        }
        public void Begin()
        {
            source?.Dispose(); records.Clear(); frames.Clear();
            QualitySettings.vSyncCount=0; Application.targetFrameRate=targetFps;
            source = new BossInputSource();
            report = new Report { mouse = source.Mouse?.description.ToString(), keyboard = source.Keyboard?.description.ToString(),
                holdback = holdback, started = source.CollectionTime, metrics = source.Timeline.Metrics, records = records,
                initialStates=new List<BossInputSample>(source.InitialStates),frames=frames,targetFps=targetFps,hitchProfile=hitchProfile };
            source.Timeline.Observed += (s, reason, committed) => { if (records.Count < 50000) records.Add(new Record { sample = s, disposition = reason, committed = committed }); };
            source.Timeline.Intent += OnIntent;
            if (label == null) CreateDisplay();
            lastFrame=Time.realtimeSinceStartupAsDouble; nextHitchTime=lastFrame+1; nextHitch=100;
        }
        private void Update()
        {
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(keyboard!=null)
            {
                if(keyboard.f1Key.wasPressedThisFrame) ChangeProfile(30,false);
                else if(keyboard.f2Key.wasPressedThisFrame) ChangeProfile(60,false);
                else if(keyboard.f3Key.wasPressedThisFrame) ChangeProfile(60,true);
            }
            injectedDelay=0;
            if(hitchProfile && Application.isFocused && Time.realtimeSinceStartupAsDouble>=nextHitchTime)
            {
                injectedDelay=nextHitch;
                // Explicit test-only main-thread stalls; never installed in the combat scene.
                System.Threading.Thread.Sleep(injectedDelay);
                nextHitch=nextHitch==100?250:100;
                nextHitchTime=Time.realtimeSinceStartupAsDouble+1;
            }
        }
        private void ChangeProfile(int fps, bool hitches)
        {
            SavePlayerReport(); targetFps=fps; hitchProfile=hitches; Begin();
        }
        private void SavePlayerReport()
        {
            if(Application.isEditor || report==null) return;
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            Save(Path.Combine(folder,$"input-probe-{targetFps}{(hitchProfile?"-hitch":"")}.json"));
            Save(Path.Combine(folder,"input-probe.json"));
        }
        private void CreateDisplay()
        {
            var canvas = new GameObject("Input validation", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(canvas.transform, false);
            var rect = bg.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(.035f, .055f, .08f);
            var text = new GameObject("Status", typeof(RectTransform), typeof(Text)); text.transform.SetParent(bg.transform, false);
            label = text.GetComponent<Text>(); GameTypography.Apply(label); label.fontSize = 24; label.color = Color.white;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = new Vector2(35, 35); label.rectTransform.offsetMax = new Vector2(-35, -35);
            var marker = new GameObject("Pointer", typeof(RectTransform), typeof(Image)); marker.transform.SetParent(canvas.transform, false);
            pointer = marker.GetComponent<RectTransform>(); pointer.anchorMin = pointer.anchorMax = Vector2.zero; pointer.sizeDelta = new Vector2(18, 18);
            marker.GetComponent<Image>().color = new Color(.2f, .9f, .7f); marker.GetComponent<Image>().raycastTarget = false;
        }
        private void OnIntent(BossInputIntent intent)
        {
            report.maxResponse = Math.Max(report.maxResponse, UnityEngine.InputSystem.LowLevel.InputState.currentTime - intent.Time);
            switch (intent.Kind)
            {
                case BossInputKind.Press: report.presses++; break;
                case BossInputKind.Release: report.releases++; break;
                case BossInputKind.Space: report.spaces++; break;
                case BossInputKind.Escape: report.escapes++; break;
                case BossInputKind.Cancel: report.cancellations++; break;
            }
            if (pointer != null && intent.Kind == BossInputKind.Position) pointer.anchoredPosition = intent.Position;
        }
        private void LateUpdate()
        {
            if (source == null) return;
            source.Timeline.Advance(source.CollectionTime - holdback);
            var now=Time.realtimeSinceStartupAsDouble;
            if(frames.Count<50000) frames.Add(new Frame { collection=source.CollectionTime,cutoff=source.Timeline.CommittedTime,interval=now-lastFrame,injectedDelayMs=injectedDelay });
            lastFrame=now;
            var m = source.Timeline.Metrics;
            if (!Application.isEditor && Time.realtimeSinceStartupAsDouble >= nextReport)
            {
                nextReport = Time.realtimeSinceStartupAsDouble + 2;
                SavePlayerReport();
            }
            label.text = "보스전 입력 검증 · 0a\nF1: 30fps / F2: 60fps / F3: 긴 프레임\n" +
                $"현재 {targetFps}fps{(hitchProfile?" + 100/250ms 정지":"")} (모드 변경 시 횟수 초기화)\n\n" +
                $"누름 {report.presses}    해제 {report.releases}\nSpace {report.spaces}    Escape {report.escapes}\n" +
                $"표본 {m.samples}    늦은 입력 {m.late}    취소 {report.cancellations}\n수신 지연 최대 {m.maxDelay * 1000:F2} ms\n" +
                $"입력 보류 {holdback * 1000:F0} ms\n\n초록 점은 수집된 포인터 위치입니다.";
        }
        public string Save(string path)
        {
            report.ended = UnityEngine.InputSystem.LowLevel.InputState.currentTime;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var json = JsonUtility.ToJson(report, true); File.WriteAllText(path, json); return json;
        }
        private void OnApplicationQuit() => SavePlayerReport();
        private void OnDisable()
        {
            SavePlayerReport();
            source?.Dispose(); source = null;
            if(settingsCaptured) { Application.targetFrameRate=oldFps; QualitySettings.vSyncCount=oldVsync; }
            settingsCaptured=false;
        }
    }
}
