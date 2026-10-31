using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WindowExe.Desktop.Tests
{
    public sealed class CRTVisualStateTests
    {
        private CRTPreset[] levels;
        private CRTVisualState state;
        [SetUp] public void SetUp() { levels = CRTVisualState.DefaultLevels(); state = new CRTVisualState(levels, new CRTPreset(.45f, .6f, .8f, .3f, .75f, .35f)); }
        private static float[] Values(CRTPreset p) => new[] { p.scanline, p.grain, p.streak, p.streakDensity, p.vignette, p.tint };
        private static void Equal(CRTPreset actual, CRTPreset expected)
        { Assert.That(Values(actual), Is.EqualTo(Values(expected)).Within(.00001)); }
        [Test] public void AllAxesArriveAtTheRequestedTimeAndRepeatedTargetDoesNotRestart()
        {
            state.SetLevel(3, 1); state.Advance(.25);
            Equal(state.Base, CRTPreset.Lerp(levels[0], levels[3], .25f));
            state.SetLevel(3, 1); state.Advance(.75); Equal(state.Base, levels[3]);
            state.Advance(1); Equal(state.Base, levels[3]);
        }
        [Test] public void RetargetUsesCurrentBaseEvenDuringSpike()
        {
            state.SetLevel(3, 1); state.Advance(.5); var midpoint = state.Base;
            state.Spike(2, .2f); state.SetLevel(1, 1); state.Advance(.5);
            Equal(state.Base, CRTPreset.Lerp(midpoint, levels[1], .5f));
            Assert.That(state.Weight, Is.EqualTo(1));
            state.Advance(2); Equal(state.Display, levels[1]);
        }
        [Test] public void SpikeUsesComponentMaximumAndReturnsToLatestBase()
        {
            state.Spike(.3f, .2f); var peak = state.Display;
            state.SetLevel(3, 0); state.Advance(.29);
            Equal(state.Display, CRTPreset.Max(levels[3], peak));
            state.Advance(.11); Assert.That(state.Weight, Is.EqualTo(.5f).Within(.0001));
            Equal(state.Display, CRTPreset.Lerp(levels[3], CRTPreset.Max(levels[3], peak), .5f));
            state.Advance(.11); Equal(state.Display, levels[3]);
        }
        [Test] public void HoldingSpikeExtendsButNeverShortensAndReleaseRetriggersImmediately()
        {
            state.Spike(.3f, .2f); state.Advance(.1); state.Spike(.01f, .2f);
            state.Advance(.19); Assert.That(state.Weight, Is.EqualTo(1));
            state.Spike(.5f, .2f); state.Advance(.6);
            Assert.That(state.Weight, Is.EqualTo(.5f).Within(.0001));
            state.Spike(.3f, .2f); Assert.That(state.Weight, Is.EqualTo(1));
            state.Advance(.31); Assert.That(state.Weight, Is.LessThan(1).And.GreaterThan(0));
            state.Advance(.2); Assert.That(state.Weight, Is.Zero);
        }
        [Test] public void NonpositiveDurationsAreImmediateAndInvalidRequestsLeaveStateIntact()
        {
            state.SetLevel(99, -1); Equal(state.Base, levels[3]);
            state.SetLevel(-10, 0); Equal(state.Base, levels[0]);
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            { state.SetLevel(3, value); state.Spike(value, .2f); state.Spike(.3f, value); state.Advance(value); }
            state.Spike(0, .2f); state.Spike(-1, .2f);
            Equal(state.Display, levels[0]); Assert.That(state.Time, Is.Zero);
            state.Spike(.1f, 0); state.Advance(.11); Assert.That(state.Weight, Is.Zero);
        }
        [Test] public void ResetAndSnapCancelTemporaryStateWithoutReplayingIt()
        {
            state.Spike(100, 2); state.SetLevel(2, 5); state.Advance(.4); state.Snap(2);
            Equal(state.Display, levels[2]); Assert.That(state.Weight, Is.Zero); Assert.That(state.Time, Is.EqualTo(.4));
            state.Reset(); Equal(state.Display, levels[0]); Assert.That(state.Time, Is.Zero);
        }
        [Test] public void ConfigurationIsCopiedClampedAndRejectsNonFinitePresets()
        {
            levels[0] = new CRTPreset(-2, 9, 9, 9, -2, 9);
            var copied = new CRTVisualState(levels, default); levels[0] = default;
            Equal(copied.Base, new CRTPreset(0, 1, 1, .3f, 0, 1));
            Assert.Throws<ArgumentException>(() => new CRTVisualState(null, default));
            Assert.Throws<ArgumentException>(() => new CRTVisualState(new CRTPreset[3], default));
            levels[2].grain = float.NaN;
            Assert.Throws<ArgumentException>(() => new CRTVisualState(levels, default));
        }
        [Test] public void HorizontalProfileReducesBaseButPreservesSpikeAndOtherAxes()
        {
            state.SetLevel(2,0);var expected=levels[2];expected.scanline*=.35f;expected.streak*=.2f;expected.streakDensity*=.25f;
            Equal(state.DisplayWithBaseHorizontal(.35f,.2f,.25f),expected);Equal(state.Display,levels[2]);
            state.Spike(.3f,.2f);Equal(state.DisplayWithBaseHorizontal(.35f,.2f,.25f),state.Display);
            state.Advance(.6);Equal(state.DisplayWithBaseHorizontal(.35f,.2f,.25f),expected);
        }
    }

    public sealed class CRTOverlayLifetimeTests
    {
        private float timeScale;
        private DesktopInputTestScope scope;
        [UnitySetUp] public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            yield return new EnterPlayMode(); scope = new DesktopInputTestScope(); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() { Time.timeScale = timeScale; scope?.Dispose(); scope = null; if (Application.isPlaying) yield return new ExitPlayMode(); }
        private static CRTOverlay CreateInactive()
        {
            var go = new GameObject("CRT test", typeof(RectTransform)); go.SetActive(false);
            var image = go.AddComponent<RawImage>(); image.enabled = false;
            var effect = go.AddComponent<CRTOverlay>();
            effect.baseMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Data/Desktop/Effects/CRTOverlay.mat");
            return effect;
        }
        [UnityTest] public IEnumerator HorizontalProfileIsOwnedAndClearsOnTransferAndRelease()
        {
            var effect=CRTOverlay.Instance;effect.transitionTime=0;effect.ResetVisuals();effect.SetLevel(2);
            var baseline=effect.Displayed;var source=EditorJsonUtility.ToJson(effect.baseMaterial);
            var previous=effect.AcquireControl();Assert.That(previous.SetBaseHorizontalNoise(.35f,.2f,.25f),Is.True);
            Assert.That(effect.RuntimeMaterial.GetFloat("_StreakStrength"),Is.EqualTo(baseline.streak*.2f).Within(.00001));
            Assert.That(effect.Displayed.scanline,Is.EqualTo(baseline.scanline*.35f).Within(.00001));
            Assert.That(effect.Displayed.streakDensity,Is.EqualTo(baseline.streakDensity*.25f).Within(.00001));
            Assert.That(effect.Displayed.grain,Is.EqualTo(baseline.grain));Assert.That(effect.Displayed.tint,Is.EqualTo(baseline.tint));
            Assert.That(previous.SetBaseHorizontalNoise(float.NaN,0,0),Is.False);
            effect.Spike(.05f);Assert.That(effect.Displayed.streak,Is.EqualTo(effect.spike.streak).Within(.00001));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(effect.Displayed.streak,Is.EqualTo(baseline.streak*.2f).Within(.00001));
            var next=previous.Transfer();Assert.That(effect.Displayed.streak,Is.EqualTo(baseline.streak).Within(.00001));
            Assert.That(previous.SetBaseHorizontalNoise(0,0,0),Is.False);
            Assert.That(next.SetBaseHorizontalNoise(.5f,.5f,.5f),Is.True);Assert.That(previous.Release(),Is.False);
            Assert.That(effect.Displayed.streak,Is.EqualTo(baseline.streak*.5f).Within(.00001));
            Assert.That(next.Release(),Is.True);Assert.That(effect.Displayed.streak,Is.EqualTo(baseline.streak).Within(.00001));
            Assert.That(EditorJsonUtility.ToJson(effect.baseMaterial),Is.EqualTo(source));
        }
        [UnityTest] public IEnumerator DisableAtEveryPhaseHidesAndReusesTheOwnedMaterial()
        {
            var effect = CRTOverlay.Instance; Assert.That(effect, Is.Not.Null);
            var original = effect.baseMaterial; var originalJson = EditorJsonUtility.ToJson(original);
            var owned = effect.RuntimeMaterial; var image = effect.GetComponent<RawImage>();
            for (var phase = 0; phase < 3; phase++)
                for (var objectDisable = 0; objectDisable < 2; objectDisable++)
                {
                    effect.ResetVisuals(); effect.transitionTime = 1; effect.SetLevel(2);
                    if (phase > 0) effect.Spike(phase == 1 ? 1 : .01f);
                    if (phase == 2) yield return new WaitForSecondsRealtime(.06f);
                    if (objectDisable == 1) effect.gameObject.SetActive(false); else effect.enabled = false;
                    Assert.That(image.enabled, Is.False); Assert.That(CRTOverlay.Instance, Is.Null);
                    var clock = effect.EffectTime; effect.SetLevel(3); effect.Spike(10);
                    yield return null; Assert.That(effect.EffectTime, Is.EqualTo(clock));
                    if (objectDisable == 1) effect.gameObject.SetActive(true); else effect.enabled = true;
                    Assert.That(image.enabled, Is.True); Assert.That(image.raycastTarget, Is.False);
                    Assert.That(effect.Displayed.grain, Is.EqualTo(effect.levels[3].grain).Within(.00001));
                    Assert.That(effect.RuntimeMaterial, Is.SameAs(owned));
                    Assert.That(owned.GetVector("_Resolution"), Is.EqualTo(new Vector4(Screen.width, Screen.height, 0, 0)));
                }
            Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(originalJson));
            effect.enabled = false; effect.ResetVisuals(); Assert.That(image.enabled, Is.False);
            effect.enabled = true; Assert.That(effect.TargetLevel, Is.Zero);
        }
        [UnityTest] public IEnumerator OffKeepsClockAndLatestRequestsWhileTimeScaleIsZero()
        {
            var effect = CRTOverlay.Instance; var image = effect.GetComponent<RawImage>();
            Time.timeScale = 0; effect.ResetVisuals(); effect.SetQuality(CRTQuality.Off); effect.transitionTime = .03f; effect.SetLevel(3);
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(image.enabled, Is.False); Assert.That(effect.EffectTime, Is.GreaterThan(0));
            effect.SetQuality(CRTQuality.Simple);
            Assert.That(image.enabled, Is.True); Assert.That(effect.RuntimeMaterial.GetFloat("_Simple"), Is.EqualTo(1));
            Assert.That(effect.Displayed.grain, Is.EqualTo(effect.levels[3].grain).Within(.00001));
            Assert.That(effect.RuntimeMaterial.GetFloat("_EffectTime"), Is.EqualTo((float)effect.EffectTime));
            effect.SetQuality(CRTQuality.Full); Assert.That(effect.RuntimeMaterial.GetFloat("_Simple"), Is.Zero);
        }
        [UnityTest] public IEnumerator InitiallyDisabledDuplicateDestroyAndInactiveCallsAreSafe()
        {
            var owner = CRTOverlay.Instance; var duplicate = CreateInactive(); duplicate.gameObject.SetActive(true);
            Assert.That(CRTOverlay.Instance, Is.SameAs(owner)); Assert.That(duplicate.GetComponent<RawImage>().enabled, Is.False);
            Assert.That(duplicate.RuntimeMaterial, Is.Null); Object.Destroy(duplicate.gameObject); yield return null;
            owner.enabled = false;
            var fresh = CreateInactive(); fresh.enabled = false; fresh.SetLevel(2); fresh.Spike(2); fresh.gameObject.SetActive(true);
            Assert.That(fresh.GetComponent<RawImage>().enabled, Is.False); Assert.That(fresh.RuntimeMaterial, Is.Null);
            fresh.enabled = true; Assert.That(fresh.TargetLevel, Is.EqualTo(2));
            Assert.That(fresh.Displayed.grain, Is.EqualTo(fresh.levels[2].grain));
            var owned = fresh.RuntimeMaterial; Object.Destroy(fresh.gameObject); yield return null; yield return null;
            Assert.That(owned == null, Is.True); Assert.That(CRTOverlay.Instance, Is.Null);
            Assert.DoesNotThrow(() => { fresh.SetLevel(3); fresh.Spike(); fresh.ResetVisuals(); fresh.SetQuality(CRTQuality.Full); });
            owner.enabled = true; Assert.That(CRTOverlay.Instance, Is.SameAs(owner));
        }
        [UnityTest] public IEnumerator InvalidSettingsFailHiddenOnceWithoutOwningTheInstance()
        {
            CRTOverlay.Instance.enabled = false;
            for (var mode = 0; mode < 8; mode++)
            {
                var effect = CreateInactive(); Material material = null;
                if (mode == 0) effect.baseMaterial = null;
                if (mode == 1) effect.levels = null;
                if (mode == 2) effect.levels = new CRTPreset[0];
                if (mode == 3) effect.levels = new CRTPreset[3];
                if (mode == 4) effect.levels[0].tint = float.NaN;
                if (mode == 5) effect.transitionTime = float.PositiveInfinity;
                if (mode == 6) { material = new Material(Shader.Find("UI/Default")); effect.baseMaterial = material; }
                if (mode == 7) { material = new Material(effect.baseMaterial); material.SetFloat("_RollSpeed", float.NaN); effect.baseMaterial = material; }
                LogAssert.Expect(LogType.Warning, new Regex("CRTOverlay:")); effect.gameObject.SetActive(true);
                Assert.That(effect.GetComponent<RawImage>().enabled, Is.False); Assert.That(CRTOverlay.Instance, Is.Null);
                effect.SetLevel(3); effect.Spike(); effect.enabled = false; effect.enabled = true;
                yield return null; Assert.That(effect.RuntimeMaterial, Is.Null);
                Object.Destroy(effect.gameObject); if (material != null) Object.Destroy(material);
                yield return null;
            }
        }
    }
}
