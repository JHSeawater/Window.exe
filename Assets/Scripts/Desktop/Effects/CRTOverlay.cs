using UnityEngine;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public enum CRTQuality { Full, Simple, Off }

    [DisallowMultipleComponent, RequireComponent(typeof(RawImage))]
    public sealed class CRTOverlay : MonoBehaviour
    {
        public static CRTOverlay Instance { get; private set; }
        public Material baseMaterial;
        public CRTPreset[] levels = CRTVisualState.DefaultLevels();
        public CRTPreset spike = new CRTPreset(.38f, .032f, .65f, .16f, .60f, .25f);
        public float transitionTime = 1f, returnTime = .2f;
        [SerializeField] private CRTQuality quality = CRTQuality.Full;
        private RawImage image;
        private Material ownedMaterial;
        private CRTVisualState state;
        private int requestedLevel, session = -1;
        private static int generation;
        private long controlGeneration;
        private bool controlOwned;
        private float baseScanlineScale = 1, baseStreakScale = 1, baseDensityScale = 1;
        public CRTControlLease AcquireControl()
        {
            EnsureSession();
            if (controlOwned) return null;
            controlOwned = true; return new CRTControlLease(this, ++controlGeneration);
        }
        internal bool IsControlCurrent(long token) => session == generation && controlOwned && token == controlGeneration;
        internal CRTControlLease TransferControl(long token)
        {
            if (!IsControlCurrent(token)) return null;
            ClearBaseHorizontalNoise(); return new CRTControlLease(this, ++controlGeneration);
        }
        internal bool ReleaseControl(long token)
        { if (!IsControlCurrent(token)) return false; controlOwned = false; controlGeneration++; ClearBaseHorizontalNoise(); return true; }
        internal bool SetBaseHorizontalNoise(long token, float scanlineScale, float streakScale, float densityScale)
        {
            if (!IsControlCurrent(token) || !CRTVisualState.Finite(scanlineScale) || !CRTVisualState.Finite(streakScale) || !CRTVisualState.Finite(densityScale)) return false;
            baseScanlineScale = Mathf.Clamp01(scanlineScale); baseStreakScale = Mathf.Clamp01(streakScale); baseDensityScale = Mathf.Clamp01(densityScale);
            Apply(); return true;
        }
        private void ClearBaseHorizontalNoise()
        { baseScanlineScale = baseStreakScale = baseDensityScale = 1; Apply(); }
        private bool failed, reported;
        private int width, height;
        private static readonly int[] Properties = {
            Shader.PropertyToID("_ScanlineStrength"), Shader.PropertyToID("_GrainStrength"),
            Shader.PropertyToID("_StreakStrength"), Shader.PropertyToID("_StreakDensity"),
            Shader.PropertyToID("_VignetteStrength"), Shader.PropertyToID("_TintAmount") };
        private static readonly int TimeId = Shader.PropertyToID("_EffectTime"), ResolutionId = Shader.PropertyToID("_Resolution"), SimpleId = Shader.PropertyToID("_Simple");
        public Material RuntimeMaterial => ownedMaterial;
        public CRTPreset Displayed => state == null ? default : state.DisplayWithBaseHorizontal(baseScanlineScale, baseStreakScale, baseDensityScale);
        public double EffectTime => state?.Time ?? 0;
        public int TargetLevel => requestedLevel;
        public CRTQuality Quality => quality;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        public long LastUpdateAllocatedBytes { get; private set; }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginSession() { Instance = null; unchecked { generation++; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebindSession()
        {
            foreach (var effect in FindObjectsByType<CRTOverlay>(FindObjectsInactive.Include))
            {
                effect.EnsureSession();
                if (effect.isActiveAndEnabled) effect.Activate();
            }
        }
        private void Awake() { if (Application.isPlaying) { Hide(); EnsureSession(); } }
        private void OnEnable() { if (Application.isPlaying) Activate(); }
        private void EnsureSession()
        {
            if (session == generation) return;
            Hide(); ReleaseMaterial(); state = null; requestedLevel = 0;
            controlOwned = false; controlGeneration++;
            baseScanlineScale = baseStreakScale = baseDensityScale = 1;
            session = generation; failed = reported = false;
        }
        private void Hide()
        {
            if (image == null) image = GetComponent<RawImage>();
            if (image != null) { image.enabled = false; image.raycastTarget = false; }
        }
        private void Activate()
        {
            EnsureSession(); Hide();
            if (Instance != null && Instance != this) return;
            if (!ValidateSettings(out var reason)) { Fail(reason); return; }
            failed = false;
            if (state == null) state = new CRTVisualState(levels, spike);
            state.Snap(requestedLevel);
            if (ownedMaterial == null) ownedMaterial = new Material(baseMaterial) { name = "CRTOverlay (Runtime)", hideFlags = HideFlags.DontSave };
            image.material = ownedMaterial;
            Instance = this;
            Apply();
        }
        private bool ValidateSettings(out string reason)
        {
            reason = null;
            if (baseMaterial == null || baseMaterial.shader == null || !baseMaterial.shader.isSupported) reason = "A supported CRT material is required.";
            else if (levels == null || levels.Length != 4) reason = "Exactly four level presets are required.";
            else if (!CRTVisualState.Finite(transitionTime) || !CRTVisualState.Finite(returnTime) || !spike.IsFinite) reason = "Non-finite preset or transition duration.";
            if (reason != null) return false;
            foreach (var preset in levels) if (!preset.IsFinite) { reason = "Non-finite level preset."; return false; }
            foreach (var property in Properties) if (!baseMaterial.HasProperty(property)) { reason = "Missing CRT shader property."; return false; }
            foreach (var property in new[] { "_EffectTime", "_Resolution", "_Simple", "_ScanlinePeriod", "_StreakSpeed", "_RollStrength", "_RollSpeed", "_TintColor" })
                if (!baseMaterial.HasProperty(property)) { reason = "Missing CRT shader property: " + property; return false; }
            foreach (var property in new[] { "_ScanlinePeriod", "_StreakSpeed", "_RollStrength", "_RollSpeed" })
                if (!CRTVisualState.Finite(baseMaterial.GetFloat(property))) { reason = "Non-finite material setting: " + property; return false; }
            var tint = baseMaterial.GetColor("_TintColor");
            if (!CRTVisualState.Finite(tint.r) || !CRTVisualState.Finite(tint.g) || !CRTVisualState.Finite(tint.b) || !CRTVisualState.Finite(tint.a)) { reason = "Non-finite tint color."; return false; }
            return true;
        }
        private void Fail(string reason)
        {
            Hide(); failed = true;
            if (Instance == this) Instance = null;
            if (!reported) { reported = true; Debug.LogWarning("CRTOverlay: " + reason, this); }
        }
        public void SetLevel(int level)
        {
            if (this == null || failed) return;
            EnsureSession(); requestedLevel = Mathf.Clamp(level, 0, 3);
            if (!isActiveAndEnabled || Instance != this || state == null) return;
            if (!CRTVisualState.Finite(transitionTime)) { Fail("Non-finite transition duration."); return; }
            state.SetLevel(requestedLevel, transitionTime); Apply();
        }
        public void Spike(float seconds = .3f)
        {
            if (this == null || !isActiveAndEnabled || failed || Instance != this || state == null || !CRTVisualState.Finite(seconds) || seconds <= 0) return;
            if (!CRTVisualState.Finite(returnTime)) { Fail("Non-finite return duration."); return; }
            state.Spike(seconds, returnTime); Apply();
        }
        public void SetQuality(CRTQuality value)
        {
            if (this == null) return;
            quality = value == CRTQuality.Simple || value == CRTQuality.Off ? value : CRTQuality.Full;
            if (Application.isPlaying && isActiveAndEnabled && Instance == this && !failed) Apply();
        }
        public void ResetVisuals()
        {
            if (this == null) return;
            requestedLevel = 0; state?.Reset();
            if (isActiveAndEnabled && Instance == this && !failed) Apply();
            else Hide();
        }
        private void Update()
        {
            if (Instance != this || failed || state == null) return;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            var allocated = System.GC.GetAllocatedBytesForCurrentThread();
#endif
            state.Advance(Time.unscaledDeltaTime); Apply();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            LastUpdateAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - allocated;
#endif
        }
        private void Apply()
        {
            if (ownedMaterial == null || image == null) return;
            var p = Displayed;
            ownedMaterial.SetFloat(Properties[0], p.scanline); ownedMaterial.SetFloat(Properties[1], p.grain);
            ownedMaterial.SetFloat(Properties[2], p.streak); ownedMaterial.SetFloat(Properties[3], p.streakDensity);
            ownedMaterial.SetFloat(Properties[4], p.vignette); ownedMaterial.SetFloat(Properties[5], p.tint);
            ownedMaterial.SetFloat(TimeId, (float)state.Time);
            ownedMaterial.SetFloat(SimpleId, quality == CRTQuality.Simple ? 1 : 0);
            if (width != Screen.width || height != Screen.height || width == 0)
            { width = Screen.width; height = Screen.height; ownedMaterial.SetVector(ResolutionId, new Vector4(width, height, 0, 0)); }
            image.raycastTarget = false;
            image.enabled = isActiveAndEnabled && Instance == this && quality != CRTQuality.Off;
        }
        private void OnDisable()
        {
            Hide(); state?.Snap(requestedLevel);
            if (Instance == this) Instance = null;
        }
        private void ReleaseMaterial()
        {
            if (image != null && image.material == ownedMaterial) image.material = null;
            if (ownedMaterial != null)
            { if (Application.isPlaying) Destroy(ownedMaterial); else DestroyImmediate(ownedMaterial); }
            ownedMaterial = null; width = height = 0;
        }
        private void OnDestroy() { Hide(); if (Instance == this) Instance = null; ReleaseMaterial(); }
    }
}
