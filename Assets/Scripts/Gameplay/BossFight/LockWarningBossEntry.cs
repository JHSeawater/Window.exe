using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.BossFight
{
    /// <summary>Unlock confirmation owns input for three active seconds, then hands off in the same scene.</summary>
    public sealed class LockWarningBossEntry : MonoBehaviour, IDesktopInputSession
    {
        public const double DelaySeconds = 3;
        [SerializeField] private BossFightController fight;
        private WindowManager manager;
        private DesktopInputRouter router;
        private readonly HashSet<LockWarningWindow> warnings = new HashSet<LockWarningWindow>();
        private LockWarningWindow pendingWindow;
        private BossEntryBackdrop backdrop;
        private Coroutine capture;
        private Canvas crtCanvas;
        private bool crtWasEnabled, ownsInput, paused, focused;
        private double elapsed;
        private int resumedFrame = -1;
        public bool IsPending => pendingWindow != null;
        public double PendingSeconds => elapsed;
        public string LastError { get; private set; }
        internal bool ObserveNativeApplicationState { get; set; } = true;
        public void Configure(BossFightController controller) => fight = controller;

        private void OnEnable() { if (Application.isPlaying) Bind(); }
        private void Start() => Bind();
        private void Bind()
        {
            router = DesktopInputRouter.Instance;
            if (manager != null) return;
            manager = WindowManager.Instance;
            focused = Application.isFocused;
            if (manager == null) return;
            manager.OnProcessOpened += OnOpened;
            foreach (var process in manager.ActiveProcesses) OnOpened(process);
        }
        private void OnOpened(WindowProcess process)
        {
            if (process.Instance is LockWarningWindow warning && warnings.Add(warning))
                warning.BossEntryRequested += RequestEntry;
        }
        private void RequestEntry(LockWarningWindow warning)
        {
            if (IsPending || fight == null || fight.IsActive || manager == null || router == null ||
                warning.CurrentStage != 1 || !manager.IsCurrent(warning) || manager.IsLocked ||
                manager.SessionPolicy != null || router.HasSession) return;
            if (!router.TryBeginSession(this, this)) return;
            ownsInput = true; manager.SetLocked(true);
            pendingWindow = warning; elapsed = 0; LastError = null;
            capture = StartCoroutine(CaptureDesktop());
        }
        private IEnumerator CaptureDesktop()
        {
            // Capture before the CRT overlay, so the frozen screen receives the effect exactly once.
            // Key use has finished before confirmation; end-of-frame also flushes UI and key destruction.
            crtCanvas = CRTOverlay.Instance != null ? CRTOverlay.Instance.GetComponentInParent<Canvas>() : null;
            crtWasEnabled = crtCanvas != null && crtCanvas.enabled;
            if (crtCanvas != null) crtCanvas.enabled = false;
            yield return new WaitForEndOfFrame();
            Texture2D snapshot = null;
            try
            {
                snapshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (snapshot == null || DesktopWindowSpace.Instance?.Camera == null)
                    throw new InvalidOperationException("Desktop snapshot unavailable.");
                var taskbar = FindAnyObjectByType<TaskbarController>()?.GetComponentInParent<Canvas>();
                backdrop = new BossEntryBackdrop(snapshot, DesktopWindowSpace.Instance.Camera, taskbar,
                    pendingWindow.FrontImageRenderer.sharedMaterial);
                snapshot = null;
            }
            catch (Exception error) { LastError = error.Message; }
            finally
            {
                if (snapshot != null) Destroy(snapshot);
                RestoreCrt(); capture = null;
            }
            if (backdrop == null) FailEntry(LastError);
        }
        private void RestoreCrt()
        {
            if (crtCanvas != null) crtCanvas.enabled = crtWasEnabled;
            crtCanvas = null;
        }
        private void Update()
        {
            // The first unscaled delta after suspension can include the entire time away.
            if (Time.frameCount != resumedFrame) AdvanceWait(Time.unscaledDeltaTime);
        }
        internal void AdvanceWait(double seconds)
        {
            if (!ownsInput) return;
            if (pendingWindow == null || fight == null || !manager.IsCurrent(pendingWindow))
            { FailEntry("Unlock window or boss controller removed."); return; }
            backdrop?.FitCamera();
            if (!focused || paused) return;
            elapsed += Math.Max(0, seconds);
            if (elapsed < DelaySeconds || backdrop == null) return;
            ReleaseInput();
            if (fight.BeginBossFight(new BossFightEntryContext { RequestId = Guid.NewGuid().ToString("N"), Backdrop = backdrop }, out var error))
            { backdrop = null; pendingWindow = null; }
            else FailEntry(error);
        }
        internal void ObserveFocus(bool value)
        {
            if (value && !focused) resumedFrame = Time.frameCount;
            focused = value;
        }
        internal void ObservePause(bool value)
        {
            if (!value && paused) resumedFrame = Time.frameCount;
            paused = value;
        }
        private void OnApplicationFocus(bool value) { if (ObserveNativeApplicationState) ObserveFocus(value); }
        private void OnApplicationPause(bool value) { if (ObserveNativeApplicationState) ObservePause(value); }
        public void Cancel(DesktopCancelReason reason) { } // Escape/focus loss cannot dismiss the accepted confirmation.
        private void ReleaseInput()
        {
            if (!ownsInput) return;
            ownsInput = false;
            router?.EndSession(this); manager?.SetLocked(false);
        }
        private void FailEntry(string error)
        {
            LastError = error;
            if (capture != null) { StopCoroutine(capture); capture = null; }
            RestoreCrt(); backdrop?.Dispose(); backdrop = null;
            pendingWindow = null; ReleaseInput();
            Debug.LogWarning("Boss entry cancelled: " + error, this);
        }
        private void OnDisable()
        {
            if (manager != null) manager.OnProcessOpened -= OnOpened;
            foreach (var warning in warnings) if (warning != null) warning.BossEntryRequested -= RequestEntry;
            warnings.Clear();
            if (capture != null) { StopCoroutine(capture); capture = null; }
            RestoreCrt(); backdrop?.Dispose(); backdrop = null; pendingWindow = null; ReleaseInput();
            manager = null; router = null;
        }
    }
}
