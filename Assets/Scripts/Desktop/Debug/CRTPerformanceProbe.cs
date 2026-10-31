#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop
{
    // Opt-in Development Player harness. No object or profiling work in normal games.
    public sealed class CRTPerformanceProbe : MonoBehaviour
    {
        [Serializable] private sealed class Metrics { public double median, p95, max; }
        [Serializable] private sealed class Segment
        {
            public string scenario; public int repeat, frames, materialCount, gpuFrames;
            public Metrics frame, cpuMain, cpuRender, gpu;
            public long maxCRTAllocated, maxFrameAllocated;
            public bool gpuAvailable, frameBudgetPass, crtAllocationPass;
        }
        [Serializable] private sealed class Report
        {
            public string cpu, gpu, api, unity, os;
            public int width, height, originalVSync, originalFrameLimit;
            public bool frameTimingEnabled, development, gcRecorderAvailable, complete;
            public double warmupSeconds = 10, measurementSeconds = 30;
            public Segment[] segments;
        }
        private struct Sample { public double frame, main, render, gpu; public long crtGC, frameGC; }
        private readonly List<Sample> samples = new List<Sample>(100000);
        private readonly List<Segment> results = new List<Segment>();
        // Read older completed frames: the newest CPU sample can precede its GPU query.
        private readonly FrameTiming[] timings = new FrameTiming[8];
        private ProfilerRecorder gc;
        private CRTOverlay effect;
        private string output;
        private int scenario, repeat, oldVSync, oldLimit;
        private bool oldBackground, measuring;
        private double started;
        private ulong lastTimestamp;
        private Report report;
        private static readonly string[] Names = { "Off", "Full-Level0", "Full-Level3", "Full-Spike" };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "--crt-profile");
            if (index < 0 || index + 1 >= args.Length || !Debug.isDebugBuild) return;
            new GameObject("CRT Performance Probe").AddComponent<CRTPerformanceProbe>().output = args[index + 1];
        }
        private void Start()
        {
            effect = CRTOverlay.Instance;
            if (effect == null || string.IsNullOrEmpty(output)) { enabled = false; return; }
            Directory.CreateDirectory(output);
            oldVSync = QualitySettings.vSyncCount; oldLimit = Application.targetFrameRate; oldBackground = Application.runInBackground;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Application.runInBackground = true;
            WindowManager.Instance.Open("memo"); WindowManager.Instance.Open("photos");
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            report = new Report { cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(),
                os = SystemInfo.operatingSystem, unity = Application.unityVersion, width = Screen.width, height = Screen.height,
                originalVSync = oldVSync, originalFrameLimit = oldLimit, development = Debug.isDebugBuild, gcRecorderAvailable = gc.Valid, frameTimingEnabled = FrameTimingManager.IsFeatureEnabled() };
            File.WriteAllText(Path.Combine(output, "environment.json"), JsonUtility.ToJson(report, true));
            Configure();
        }
        private void Configure()
        {
            effect.ResetVisuals(); effect.transitionTime = 0;
            effect.SetQuality(scenario == 0 ? CRTQuality.Off : CRTQuality.Full);
            if (scenario >= 2) effect.SetLevel(3);
            if (scenario == 3) effect.Spike(100);
            samples.Clear(); lastTimestamp = 0; measuring = false; started = Time.realtimeSinceStartupAsDouble;
            File.WriteAllText(Path.Combine(output, "progress.json"), JsonUtility.ToJson(new Segment { scenario = Names[scenario], repeat = repeat + 1 }));
        }
        private void LateUpdate()
        {
            if (effect == null || report == null) return;
            FrameTimingManager.CaptureFrameTimings();
            var elapsed = Time.realtimeSinceStartupAsDouble - started;
            if (!measuring && elapsed >= 10) { measuring = true; started = Time.realtimeSinceStartupAsDouble; samples.Clear(); return; }
            if (!measuring) return;
            var count = FrameTimingManager.GetLatestTimings((uint)timings.Length, timings);
            var available = count > 0;
            var t = available ? timings[count - 1] : default;
            if ((!available || t.frameStartTimestamp == 0 || t.frameStartTimestamp != lastTimestamp) && samples.Count < samples.Capacity)
            {
                lastTimestamp = t.frameStartTimestamp;
                samples.Add(new Sample { frame = available && t.cpuFrameTime > 0 ? t.cpuFrameTime : Time.unscaledDeltaTime * 1000,
                    main = available ? t.cpuMainThreadFrameTime : 0, render = available ? t.cpuRenderThreadFrameTime : 0,
                    gpu = available ? t.gpuFrameTime : 0, crtGC = effect.LastUpdateAllocatedBytes, frameGC = gc.Valid ? gc.LastValue : -1 });
            }
            if (elapsed < 30) return;
            SaveSegment();
            repeat++; if (repeat == 3) { repeat = 0; scenario++; }
            if (scenario < Names.Length) { Configure(); return; }
            report.complete = true; report.segments = results.ToArray();
            File.WriteAllText(Path.Combine(output, "complete.json"), JsonUtility.ToJson(report, true));
            Debug.Log("CRT performance capture complete: " + output);
            Application.Quit(); enabled = false;
        }
        private static Metrics Summarize(IEnumerable<double> values)
        {
            var a = values.Where(x => x > 0 && !double.IsNaN(x) && !double.IsInfinity(x)).OrderBy(x => x).ToArray();
            return a.Length == 0 ? new Metrics() : new Metrics { median = a[a.Length / 2], p95 = a[(int)Math.Ceiling(a.Length * .95) - 1], max = a[a.Length - 1] };
        }
        private void SaveSegment()
        {
            var result = new Segment { scenario = Names[scenario], repeat = repeat + 1, frames = samples.Count,
                materialCount = Resources.FindObjectsOfTypeAll<Material>().Count(x => x.name == "CRTOverlay (Runtime)"),
                frame = Summarize(samples.Select(x => x.frame)), cpuMain = Summarize(samples.Select(x => x.main)),
                cpuRender = Summarize(samples.Select(x => x.render)), gpu = Summarize(samples.Select(x => x.gpu)),
                maxCRTAllocated = samples.Count == 0 ? -1 : samples.Max(x => x.crtGC), maxFrameAllocated = samples.Count == 0 ? -1 : samples.Max(x => x.frameGC) };
            result.gpuFrames = samples.Count(x => x.gpu > 0);
            result.gpuAvailable = samples.Count > 0 && result.gpuFrames == samples.Count;
            result.frameBudgetPass = result.frames > 0 && result.frame.p95 <= 16.67;
            result.crtAllocationPass = result.frames > 0 && result.maxCRTAllocated == 0;
            results.Add(result);
            var name = Names[scenario] + "-" + (repeat + 1);
            File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(result, true));
            using (var writer = new StreamWriter(Path.Combine(output, name + ".csv")))
            {
                writer.WriteLine("frameMs,cpuMainMs,cpuRenderMs,gpuMs,crtAllocatedBytes,frameAllocatedBytes");
                foreach (var s in samples) writer.WriteLine(string.Join(",", s.frame.ToString("R", CultureInfo.InvariantCulture), s.main.ToString("R", CultureInfo.InvariantCulture), s.render.ToString("R", CultureInfo.InvariantCulture), s.gpu.ToString("R", CultureInfo.InvariantCulture), s.crtGC, s.frameGC));
            }
        }
        private void OnDestroy()
        {
            gc.Dispose();
            if (report == null) return;
            QualitySettings.vSyncCount = oldVSync; Application.targetFrameRate = oldLimit; Application.runInBackground = oldBackground;
        }
    }
}
#endif
