using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossInputSourceTests
    {
        [UnityTest] public IEnumerator RawPartialEventsPreserveStateAndSubscriptionsAreReleased()
        {
            yield return new EnterPlayMode();
            var mouse = InputSystem.AddDevice<Mouse>("Boss partial mouse");
            var keyboard = InputSystem.AddDevice<Keyboard>("Boss partial keyboard");
            var original = InputSystem.settings;
            var originalBackground = Application.runInBackground;
            Application.runInBackground = true;
            var originalFlags = original.hideFlags;
            var settings = Object.Instantiate(original);
            settings.hideFlags = HideFlags.HideAndDontSave;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            if (originalFlags == HideFlags.HideAndDontSave) original.hideFlags = HideFlags.DontSave;
            InputSystem.settings = settings;
            bool originalMerging=settings.disableRedundantEventsMerging;
            var source = new BossInputSource(mouse, keyboard);
            Assert.That(settings.disableRedundantEventsMerging,Is.True);
            var seen = new List<BossInputIntent>(); source.Timeline.Intent += seen.Add;
            var diagnostics = new List<string>();
            source.Timeline.Observed += (s, reason, committed) => diagnostics.Add($"{s.time:R}/{s.received:R} {s.fields} left={s.left} space={s.space} {reason} cutoff={committed:R}");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(100, 200) }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                InputSystem.QueueDeltaStateEvent(mouse.position, new Vector2(150, 250));
                yield return null; yield return null;
                InputSystem.QueueDeltaStateEvent(mouse.position, new Vector2(200, 300));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(200, 300) });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                source.Timeline.Advance(source.CollectionTime);
                Assert.That(seen.FindAll(x => x.Kind == BossInputKind.Press).Count, Is.EqualTo(1), string.Join("\n", diagnostics) + "\n" + JsonUtility.ToJson(source.Timeline.Metrics));
                Assert.That(seen.FindAll(x => x.Kind == BossInputKind.Release).Count, Is.EqualTo(1));
                Assert.That(seen.FindAll(x => x.Kind == BossInputKind.Space).Count, Is.EqualTo(1));
                Assert.That(seen.Find(x => x.Kind == BossInputKind.Release).Position, Is.EqualTo(new Vector2(200, 300)));
                Assert.That(source.Timeline.Metrics.late, Is.Zero);
                int samples = source.Timeline.Metrics.samples;
                source.Dispose();
                Assert.That(settings.disableRedundantEventsMerging,Is.EqualTo(originalMerging));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.one }); yield return null;
                Assert.That(source.Timeline.Metrics.samples, Is.EqualTo(samples));
            }
            finally
            {
                source.Dispose(); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = original; original.hideFlags = originalFlags;
                Application.runInBackground = originalBackground;
                Object.DestroyImmediate(settings);
            }
            yield return new ExitPlayMode();
        }
    }
}
