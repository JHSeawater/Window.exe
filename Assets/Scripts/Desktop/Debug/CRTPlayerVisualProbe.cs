#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop
{
    public sealed class CRTPlayerVisualProbe : MonoBehaviour
    {
        [Serializable] private sealed class Capture
        { public string mode; public int width, height, pinkPixels; public float meanDifference, taskbarDifference; public bool visible, raycast; }
        [Serializable] private sealed class Report { public string shader, api; public bool shaderSupported; public Capture[] captures; }
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "--crt-capture");
            if (!Debug.isDebugBuild || index < 0 || index + 1 >= args.Length) return;
            new GameObject("CRT Player Visual Probe").AddComponent<CRTPlayerVisualProbe>().output = args[index + 1];
        }
        private IEnumerator Start()
        {
            var effect = CRTOverlay.Instance;
            if (effect == null || string.IsNullOrEmpty(output)) yield break;
            Application.runInBackground = true; Directory.CreateDirectory(output);
            WindowManager.Instance.Open("memo").MoveTo(new Vector2(-3, 0));
            WindowManager.Instance.Open("photos").MoveTo(new Vector2(3, 0));
            var captures = new List<Capture>(); Texture2D baseline = null;
            for (var mode = -1; mode < 7; mode++)
            {
                effect.ResetVisuals(); effect.transitionTime = 0;
                effect.SetLevel(Mathf.Clamp(mode, 0, 3));
                effect.SetQuality(mode < 0 || mode == 6 ? CRTQuality.Off : mode == 5 ? CRTQuality.Simple : CRTQuality.Full);
                if (mode == 4) effect.Spike(10);
                for (var f = 0; f < 5; f++) yield return null;
                yield return new WaitForEndOfFrame();
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                var record = new Capture { mode = mode == -1 ? "Off-before" : mode == 6 ? "Off-after" : mode == 5 ? "Simple" : mode == 4 ? "Spike" : "Level" + mode,
                    width = texture.width, height = texture.height, visible = effect.GetComponent<UnityEngine.UI.RawImage>().enabled,
                    raycast = effect.GetComponent<UnityEngine.UI.RawImage>().raycastTarget };
                var count = 0; var barCount = 0;
                for (var y = 4; y < texture.height; y += 23)
                    for (var x = 4; x < texture.width; x += 23)
                    {
                        var pixel = texture.GetPixel(x, y);
                        if (pixel.r > .95f && pixel.b > .95f && pixel.g < .05f) record.pinkPixels++;
                        if (baseline == null) continue;
                        var original = baseline.GetPixel(x, y);
                        var difference = Mathf.Abs(pixel.r - original.r) + Mathf.Abs(pixel.g - original.g) + Mathf.Abs(pixel.b - original.b);
                        record.meanDifference += difference; count++;
                        if (y < texture.height * .045f) { record.taskbarDifference += difference; barCount++; }
                    }
                if (count > 0) record.meanDifference /= count;
                if (barCount > 0) record.taskbarDifference /= barCount;
                File.WriteAllBytes(Path.Combine(output, record.mode + ".png"), texture.EncodeToPNG());
                captures.Add(record);
                if (baseline == null) baseline = texture; else Destroy(texture);
            }
            var report = new Report { shader = effect.baseMaterial.shader.name, shaderSupported = effect.baseMaterial.shader.isSupported,
                api = SystemInfo.graphicsDeviceType.ToString(), captures = captures.ToArray() };
            File.WriteAllText(Path.Combine(output, "complete.json"), JsonUtility.ToJson(report, true));
            Destroy(baseline); Application.Quit();
        }
    }
}
#endif
