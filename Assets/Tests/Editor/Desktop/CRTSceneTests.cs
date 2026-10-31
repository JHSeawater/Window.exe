using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using WindowExe.Desktop.Desktop.Window;
using Object = UnityEngine.Object;

namespace WindowExe.Desktop.Tests
{
    public sealed partial class DesktopSceneInteractionTests
    {
        [UnityTest] public IEnumerator CRTActualOutputAndPointerRoutingSurviveEveryLevelAndQuality()
        {
            var effect = CRTOverlay.Instance; Assert.That(effect, Is.Not.Null);
            var canvas = effect.GetComponentInParent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.GreaterThan(Object.FindAnyObjectByType<TaskbarController>().GetComponentInParent<Canvas>().sortingOrder));
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Null);
            var mouse = InputSystem.AddDevice<Mouse>("CRT Input Test");
            var module = Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var previous = module.actionsAsset.devices; module.actionsAsset.devices = new InputDevice[] { mouse };
            var manager = WindowManager.Instance;
            var folder = "Builds/DesktopValidation/CRTOverlay"; Directory.CreateDirectory(folder);
            try
            {
                foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1024, 768) })
                {
                    SetResolution(resolution.x, resolution.y); for (var f = 0; f < 4; f++) yield return null;
                    var memo = manager.Open("memo"); manager.Open("photos"); memo.MoveTo(new Vector2(-1, -1));
                    effect.SetQuality(CRTQuality.Off);
                    var path = Path.GetFullPath($"{folder}/off-{resolution.x}x{resolution.y}.png");
                    ScreenCapture.CaptureScreenshot(path); for (var f = 0; f < 5; f++) yield return null;
                    var baseline = new Texture2D(2, 2); baseline.LoadImage(File.ReadAllBytes(path));
                    try
                    {
                        effect.transitionTime = 0;
                        for (var level = 0; level < 5; level++)
                        {
                            effect.SetQuality(CRTQuality.Full); effect.SetLevel(Mathf.Min(level, 3));
                            if (level == 4) effect.Spike(100);
                            yield return null;
                            Assert.That(effect.RuntimeMaterial.GetVector("_Resolution"), Is.EqualTo(new Vector4(resolution.x, resolution.y, 0, 0)));
                            path = Path.GetFullPath($"{folder}/level-{level}-{resolution.x}x{resolution.y}.png");
                            ScreenCapture.CaptureScreenshot(path); for (var f = 0; f < 5; f++) yield return null;
                            var capture = new Texture2D(2, 2); capture.LoadImage(File.ReadAllBytes(path));
                            try
                            {
                                Assert.That(capture.width, Is.EqualTo(resolution.x)); Assert.That(capture.height, Is.EqualTo(resolution.y));
                                var difference = 0f; var points = 0;
                                for (var y = 10; y < capture.height; y += 47)
                                    for (var x = 10; x < capture.width; x += 47)
                                    { var a = capture.GetPixel(x, y); var b = baseline.GetPixel(x, y); difference += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b); points++; Assert.That(a.r > .95f && a.b > .95f && a.g < .05f, Is.False, "Shader error pink"); }
                                Assert.That(difference / points, Is.GreaterThan(.005f), "Overlay must actually change captured pixels.");
                            }
                            finally { Object.Destroy(capture); }
                            manager.TryRequest(memo, WindowAction.Activate);
                            var title = WorldPoint(new Vector2(memo.OuterBounds.center.x, memo.OuterBounds.yMax - .12f));
                            yield return Send(mouse, title, false); yield return Send(mouse, title, true);
                            yield return Send(mouse, title + new Vector2(20, 0), true); yield return Send(mouse, title + new Vector2(20, 0), false);
                            Assert.That(manager.ActiveWindowId, Is.EqualTo(manager.GetProcess("memo").InstanceId));
                            Assert.That(effect.GetComponent<RawImage>().raycastTarget, Is.False);
                            effect.ResetVisuals();
                        }
                        effect.SetQuality(CRTQuality.Simple); yield return null;
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath($"{folder}/simple-{resolution.x}x{resolution.y}.png"));
                        for (var f = 0; f < 5; f++) yield return null;
                    }
                    finally { Object.Destroy(baseline); }
                    manager.ResetWindows();
                }
            }
            finally { module.actionsAsset.devices = previous; InputSystem.RemoveDevice(mouse); }
        }
    }
}
