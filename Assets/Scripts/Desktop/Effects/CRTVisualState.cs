using System;
using UnityEngine;

namespace WindowExe.Desktop
{
    [Serializable]
    public struct CRTPreset
    {
        [Range(0, 1)] public float scanline, grain, streak;
        [Range(0, .3f)] public float streakDensity;
        [Range(0, 1)] public float vignette, tint;
        public CRTPreset(float scanline, float grain, float streak, float density, float vignette, float tint)
        { this.scanline = scanline; this.grain = grain; this.streak = streak; streakDensity = density; this.vignette = vignette; this.tint = tint; }
        public bool IsFinite => CRTVisualState.Finite(scanline) && CRTVisualState.Finite(grain) && CRTVisualState.Finite(streak) && CRTVisualState.Finite(streakDensity) && CRTVisualState.Finite(vignette) && CRTVisualState.Finite(tint);
        public CRTPreset Clamped => new CRTPreset(Mathf.Clamp01(scanline), Mathf.Clamp01(grain), Mathf.Clamp01(streak), Mathf.Clamp(streakDensity, 0, .3f), Mathf.Clamp01(vignette), Mathf.Clamp01(tint));
        public static CRTPreset Lerp(CRTPreset a, CRTPreset b, float t) => new CRTPreset(Mathf.Lerp(a.scanline, b.scanline, t), Mathf.Lerp(a.grain, b.grain, t), Mathf.Lerp(a.streak, b.streak, t), Mathf.Lerp(a.streakDensity, b.streakDensity, t), Mathf.Lerp(a.vignette, b.vignette, t), Mathf.Lerp(a.tint, b.tint, t));
        public static CRTPreset Max(CRTPreset a, CRTPreset b) => new CRTPreset(Mathf.Max(a.scanline, b.scanline), Mathf.Max(a.grain, b.grain), Mathf.Max(a.streak, b.streak), Mathf.Max(a.streakDensity, b.streakDensity), Mathf.Max(a.vignette, b.vignette), Mathf.Max(a.tint, b.tint));
    }

    public sealed class CRTVisualState
    {
        private readonly CRTPreset[] levels;
        private readonly CRTPreset peak;
        private CRTPreset start;
        private double transitionStart, duration, holdEnd = double.NegativeInfinity, release;
        public int TargetLevel { get; private set; }
        public double Time { get; private set; }
        public CRTPreset Base { get; private set; }
        public float Weight => Time < holdEnd ? 1 : release > 0 ? Mathf.Clamp01(1 - (float)((Time - holdEnd) / release)) : 0;
        public CRTPreset Display => CRTPreset.Lerp(Base, CRTPreset.Max(Base, peak), Weight);
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static CRTPreset[] DefaultLevels() => new[] {
            new CRTPreset(.16f, .035f, .18f, .02f, .22f, 0),
            new CRTPreset(.24f, .09f, .35f, .05f, .35f, .06f),
            new CRTPreset(.30f, .15f, .48f, .10f, .48f, .15f),
            new CRTPreset(.36f, .22f, .60f, .20f, .60f, .25f) };
        public CRTVisualState(CRTPreset[] presets, CRTPreset spike)
        {
            if (presets == null || presets.Length != 4 || !spike.IsFinite) throw new ArgumentException("Four finite CRT presets are required.");
            levels = new CRTPreset[4];
            for (var i = 0; i < 4; i++) { if (!presets[i].IsFinite) throw new ArgumentException("Non-finite preset."); levels[i] = presets[i].Clamped; }
            peak = spike.Clamped; Reset();
        }
        public void SetLevel(int level, float seconds)
        {
            if (!Finite(seconds)) return;
            level = Mathf.Clamp(level, 0, 3);
            if (level == TargetLevel) return;
            start = Base; transitionStart = Time; duration = Math.Max(0, seconds); TargetLevel = level;
            if (duration == 0) Base = levels[level];
        }
        public void Spike(float seconds, float returnSeconds)
        {
            if (!Finite(seconds) || !Finite(returnSeconds) || seconds <= 0) return;
            holdEnd = Time < holdEnd ? Math.Max(holdEnd, Time + seconds) : Time + seconds;
            release = Math.Max(0, returnSeconds);
        }
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
            Time += seconds;
            Base = duration <= 0 ? levels[TargetLevel] : CRTPreset.Lerp(start, levels[TargetLevel], (float)((Time - transitionStart) / duration));
        }
        public void Snap(int level)
        {
            TargetLevel = Mathf.Clamp(level, 0, 3); Base = start = levels[TargetLevel];
            duration = 0; holdEnd = double.NegativeInfinity; release = 0;
        }
        public void Reset() { Time = 0; Snap(0); }
    }
}
