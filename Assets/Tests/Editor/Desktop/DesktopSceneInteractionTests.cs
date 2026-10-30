using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace WindowExe.Desktop.Tests
{
    public sealed class DesktopSceneInteractionTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private EditorWindow gameView;
        private PropertyInfo sizeIndex;
        private object sizeGroup;
        private int previousSize;
        private readonly List<int> addedSizes = new List<int>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, Is.False,
                    "Save user scene changes before testing the shared desktop scene.");
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            yield return new EnterPlayMode();
            var assembly = typeof(Editor).Assembly;
            var viewType = assembly.GetType("UnityEditor.GameView");
            gameView = EditorWindow.GetWindow(viewType);
            sizeIndex = viewType.GetProperty("selectedSizeIndex", Flags);
            previousSize = (int)sizeIndex.GetValue(gameView);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = sizesType.BaseType.GetProperty("instance", Flags).GetValue(null);
            sizeGroup = sizesType.GetProperty("currentGroup", Flags).GetValue(sizes);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gameView != null && sizeIndex != null)
            {
                sizeIndex.SetValue(gameView, previousSize);
                for (var i = addedSizes.Count - 1; i >= 0; i--)
                    sizeGroup.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(sizeGroup, new object[] { addedSizes[i] });
                gameView.Repaint();
            }
            addedSizes.Clear();
            yield return new ExitPlayMode();
        }

        private void SetResolution(int width, int height)
        {
            // Unity exposes fixed Game View sizes through Editor-only internal types.
            var assembly = typeof(Editor).Assembly;
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, Flags, null,
                new[] { Enum.Parse(modeType, "FixedResolution"), (object)width, height, "Desktop region validation" }, null);
            var groupType = sizeGroup.GetType();
            groupType.GetMethod("AddCustomSize", Flags).Invoke(sizeGroup, new[] { size });
            var index = (int)groupType.GetMethod("GetTotalCount", Flags).Invoke(sizeGroup, null) - 1;
            addedSizes.Add(index);
            sizeIndex.SetValue(gameView, index);
            gameView.Repaint();
        }

        [UnityTest]
        public IEnumerator SharedSceneSelectsAndMovesGroupAt720pAnd1080p()
        {
            var desktop = Object.FindAnyObjectByType<DesktopController>();
            var box = desktop.GetComponentInChildren<DesktopSelectionBox>(true);
            Assert.That(box, Is.Not.Null, "The shared scene must contain its serialized selection view.");
            var events = Object.FindAnyObjectByType<EventSystem>();
            var module = events.GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("SharedDesktopValidationMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            var positions = new Dictionary<DesktopIconView, Vector3>();
            foreach (var icon in desktop.Icons) positions.Add(icon, icon.Rect.localPosition);
            var opened = 0;
            desktop.OpenRequested += _ => opened++;
            try
            {
                foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                {
                    SetResolution(resolution.x, resolution.y);
                    for (var frame = 0; frame < 5; frame++) yield return null;
                    Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(resolution));
                    foreach (var entry in positions) entry.Key.Rect.localPosition = entry.Value;
                    desktop.ClearSelection();
                    var first = desktop.Icons[0];
                    var second = desktop.Icons[1];
                    var bounds = DesktopLayout.Union(DesktopLayout.BoundsInParent(first.Rect, desktop.IconArea),
                        DesktopLayout.BoundsInParent(second.Rect, desktop.IconArea));
                    var start = ScreenPoint(desktop, new Vector2(bounds.xMin - 8, bounds.yMax + 8));
                    var end = ScreenPoint(desktop, new Vector2(bounds.xMax + 8, bounds.yMin - 8));
                    yield return Send(mouse, start, false);
                    yield return Send(mouse, start, true);
                    yield return Send(mouse, end, true);
                    Assert.That(desktop.IsSelecting, Is.True);
                    Assert.That(box.Visible, Is.True);
                    Assert.That(desktop.SelectedIcons, Is.EquivalentTo(new[] { first, second }));
                    var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/DesktopValidation",
                        $"region-{resolution.x}x{resolution.y}.png"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    ScreenCapture.CaptureScreenshot(path);
                    for (var frame = 0; frame < 5; frame++) yield return null;
                    yield return Send(mouse, end, false);
                    Assert.That(box.Visible, Is.False);
                    Assert.That(desktop.SelectedIcons.Count, Is.EqualTo(2));
                    var firstStart = first.Rect.localPosition;
                    var secondStart = second.Rect.localPosition;
                    var press = RectTransformUtility.WorldToScreenPoint(null, first.Rect.TransformPoint(first.Rect.rect.center));
                    Vector2 movement = new Vector2(220, -30);
                    var target = press + ScreenPoint(desktop, movement) - ScreenPoint(desktop, Vector2.zero);
                    yield return Send(mouse, press, false);
                    yield return Send(mouse, press, true);
                    yield return Send(mouse, target, true);
                    yield return Send(mouse, target, false);
                    Assert.That(Vector2.Distance(first.Rect.localPosition - firstStart, movement), Is.LessThan(0.1));
                    Assert.That(Vector2.Distance(second.Rect.localPosition - secondStart, movement), Is.LessThan(0.1));
                    Assert.That(opened, Is.Zero);
                    Assert.That(File.Exists(path), Is.True, "Selection screenshot was not written.");
                }
            }
            finally
            {
                desktop.CancelInteraction();
                foreach (var entry in positions)
                    if (entry.Key != null) entry.Key.Rect.localPosition = entry.Value;
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        private static Vector2 ScreenPoint(DesktopController desktop, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(null, desktop.IconArea.TransformPoint(local));

        private static IEnumerator Send(Mouse mouse, Vector2 point, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, pressed));
            yield return null;
            yield return null;
        }
    }
}
