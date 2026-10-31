using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>
    /// 폴더 윈도우 (Folder Window):
    /// 폴더 내부에 실행 가능한 앱 아이콘을 포함하며, 더블클릭 시 지정된 대상 창(복원 윈도우 등)을 실행합니다.
    /// 휴지통 창(recycle-bin)의 기본 컴포넌트로 동작합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FolderWindow : Window
    {
        [Header("Executable App Item")]
        [Tooltip("더블클릭 시 실행할 윈도우 ID (기본값: 'repair')")]
        [SerializeField] private string targetExecutableWindowId = "repair";

        [Tooltip("폴더 내부 앱 아이콘의 표시 이름")]
        [SerializeField] private string executableItemName = "복원.exe";

        [Tooltip("앱 아이콘 스프라이트 (미할당 시 기본 레트로 실행 파일 아이콘 사용)")]
        [SerializeField] private Sprite executableItemIcon;

        [Header("App Icon Layout")]
        [Tooltip("폴더 내부 기준 아이콘의 로컬 좌표")]
        [SerializeField] private Vector2 iconLocalPosition = new Vector2(-1.4f, 0.5f);

        [Tooltip("아이콘 표시 크기")]
        [SerializeField] private Vector2 iconVisualSize = new Vector2(0.9f, 0.9f);

        [Header("Double Click Settings")]
        [Tooltip("더블클릭 인식 제한 시간 (초 단위)")]
        [SerializeField] private float doubleClickThreshold = 0.35f;

        [Header("Audio & Events")]
        [SerializeField] private AudioClip executeSound;
        [SerializeField] private UnityEvent onExecuted;

        private Transform primaryAppIconRoot;
        private Transform backAppIconRoot;
        private SpriteRenderer primaryIconSr;
        private SpriteRenderer backIconSr;
        private TextMeshPro primaryLabel;
        private TextMeshPro backLabel;
        private AudioSource audioSource;
        private float lastClickTime;
        private static Sprite fallbackAppIconSprite;
        private static AudioClip fallbackExecuteClip;

        public event Action OnExecuted;

        public string TargetExecutableWindowId
        {
            get => targetExecutableWindowId;
            set => targetExecutableWindowId = value;
        }

        public string ExecutableItemName
        {
            get => executableItemName;
            set
            {
                executableItemName = value;
                if (primaryLabel != null) primaryLabel.text = value;
                if (backLabel != null) backLabel.text = value;
            }
        }

        public Sprite ExecutableItemIcon
        {
            get => executableItemIcon;
            set
            {
                executableItemIcon = value;
                Sprite toUse = value != null ? value : GetFallbackAppIconSprite();
                if (primaryIconSr != null) primaryIconSr.sprite = toUse;
                if (backIconSr != null) backIconSr.sprite = toUse;
            }
        }

        public float DoubleClickThreshold
        {
            get => doubleClickThreshold;
            set => doubleClickThreshold = Mathf.Max(0.05f, value);
        }

        protected override void InitializePrimary()
        {
            SetupAppIconComponents();
            ApplyIconLayout();
        }

        private void SetupAppIconComponents()
        {
            Transform primaryTransform = transform.Find("Primary");
            if (primaryTransform == null)
            {
                var primaryGo = new GameObject("Primary");
                primaryGo.transform.SetParent(transform, false);
                primaryGo.transform.localPosition = Vector3.zero;
                primaryTransform = primaryGo.transform;
            }

            Transform secondaryTransform = transform.Find("Secondary");
            if (secondaryTransform == null)
            {
                var secGo = new GameObject("Secondary");
                secGo.transform.SetParent(transform, false);
                secGo.transform.localPosition = Vector3.zero;
                secondaryTransform = secGo.transform;
            }

            // 1. Primary App Icon Root
            if (primaryAppIconRoot == null)
            {
                Transform existing = primaryTransform.Find("AppIcon_Repair");
                if (existing == null)
                {
                    var go = new GameObject("AppIcon_Repair");
                    go.transform.SetParent(primaryTransform, false);
                    primaryAppIconRoot = go.transform;
                }
                else
                {
                    primaryAppIconRoot = existing;
                }
            }

            // 2. Secondary App Icon Root (WindowBack)
            if (backAppIconRoot == null)
            {
                Transform existing = secondaryTransform.Find("AppIcon_RepairBack");
                if (existing == null)
                {
                    var go = new GameObject("AppIcon_RepairBack");
                    go.transform.SetParent(secondaryTransform, false);
                    backAppIconRoot = go.transform;
                }
                else
                {
                    backAppIconRoot = existing;
                }
            }

            // Primary Icon Sprite
            Transform iconT = primaryAppIconRoot.Find("Icon");
            if (iconT == null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(primaryAppIconRoot, false);
                iconT = iconGo.transform;
            }
            primaryIconSr = iconT.GetComponent<SpriteRenderer>();
            if (primaryIconSr == null) primaryIconSr = iconT.gameObject.AddComponent<SpriteRenderer>();
            primaryIconSr.sortingLayerName = PrimarySortingLayerName;
            primaryIconSr.sortingOrder = 2;

            // Secondary Icon Sprite
            Transform backIconT = backAppIconRoot.Find("Icon");
            if (backIconT == null)
            {
                var backIconGo = new GameObject("Icon");
                backIconGo.transform.SetParent(backAppIconRoot, false);
                backIconT = backIconGo.transform;
            }
            backIconSr = backIconT.GetComponent<SpriteRenderer>();
            if (backIconSr == null) backIconSr = backIconT.gameObject.AddComponent<SpriteRenderer>();
            backIconSr.sortingLayerName = SecondarySortingLayerName;
            backIconSr.sortingOrder = 2;

            // Primary Label Text
            Transform labelT = primaryAppIconRoot.Find("Label");
            if (labelT == null)
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(primaryAppIconRoot, false);
                labelT = labelGo.transform;
            }
            primaryLabel = labelT.GetComponent<TextMeshPro>();
            if (primaryLabel == null) primaryLabel = labelT.gameObject.AddComponent<TextMeshPro>();
            GameTypography.Apply(primaryLabel);
            primaryLabel.alignment = TextAlignmentOptions.Center;
            primaryLabel.fontSize = 2.4f;
            primaryLabel.color = Color.black;
            primaryLabel.text = executableItemName;
            var primaryLabelRenderer = primaryLabel.GetComponent<Renderer>();
            if (primaryLabelRenderer != null)
            {
                primaryLabelRenderer.sortingLayerName = PrimarySortingLayerName;
                primaryLabelRenderer.sortingOrder = 3;
            }

            // Secondary Label Text
            Transform backLabelT = backAppIconRoot.Find("Label");
            if (backLabelT == null)
            {
                var backLabelGo = new GameObject("Label");
                backLabelGo.transform.SetParent(backAppIconRoot, false);
                backLabelT = backLabelGo.transform;
            }
            backLabel = backLabelT.GetComponent<TextMeshPro>();
            if (backLabel == null) backLabel = backLabelT.gameObject.AddComponent<TextMeshPro>();
            GameTypography.Apply(backLabel);
            backLabel.alignment = TextAlignmentOptions.Center;
            backLabel.fontSize = 2.4f;
            backLabel.color = Color.black;
            backLabel.text = executableItemName;
            var backLabelRenderer = backLabel.GetComponent<Renderer>();
            if (backLabelRenderer != null)
            {
                backLabelRenderer.sortingLayerName = SecondarySortingLayerName;
                backLabelRenderer.sortingOrder = 3;
            }

            Sprite spriteToUse = executableItemIcon != null ? executableItemIcon : GetFallbackAppIconSprite();
            primaryIconSr.sprite = spriteToUse;
            backIconSr.sprite = spriteToUse;
        }

        private void ApplyIconLayout()
        {
            if (primaryAppIconRoot != null)
                primaryAppIconRoot.localPosition = new Vector3(iconLocalPosition.x, iconLocalPosition.y, 0f);

            if (backAppIconRoot != null)
                backAppIconRoot.localPosition = new Vector3(iconLocalPosition.x, iconLocalPosition.y, 0f);

            ConfigureIconSubparts(primaryIconSr, primaryLabel);
            ConfigureIconSubparts(backIconSr, backLabel);
        }

        private void ConfigureIconSubparts(SpriteRenderer sr, TextMeshPro label)
        {
            if (sr != null && sr.sprite != null)
            {
                sr.transform.localPosition = Vector3.zero;
                Vector2 native = sr.sprite.bounds.size;
                if (native.x > 0.0001f && native.y > 0.0001f)
                {
                    sr.transform.localScale = new Vector3(
                        iconVisualSize.x / native.x,
                        iconVisualSize.y / native.y,
                        1f
                    );
                }
            }

            if (label != null)
            {
                label.rectTransform.localPosition = new Vector3(0f, -iconVisualSize.y * 0.65f, 0f);
                label.rectTransform.sizeDelta = new Vector2(1.8f, 0.45f);
            }
        }

        public Rect GetAppIconWorldRect()
        {
            Vector3 worldCenter = transform.TransformPoint(new Vector3(iconLocalPosition.x, iconLocalPosition.y - 0.1f, 0f));
            float width = Mathf.Max(iconVisualSize.x, 1.4f);
            float height = iconVisualSize.y + 0.5f;

            return new Rect(
                worldCenter.x - width * 0.5f,
                worldCenter.y - height * 0.5f,
                width,
                height
            );
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (!TryGetMouseWorld(out Vector2 mouseWorld))
                return;

            if (GetAppIconWorldRect().Contains(mouseWorld))
            {
                float now = Time.unscaledTime;
                if (now - lastClickTime <= doubleClickThreshold && now - lastClickTime > 0.001f)
                {
                    ExecuteTargetApp();
                    lastClickTime = 0f;
                }
                else
                {
                    lastClickTime = now;
                }
            }
        }

        private bool TryGetMouseWorld(out Vector2 mouseWorld)
        {
            mouseWorld = Vector2.zero;
            if (Mouse.current == null) return false;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            if (DesktopWindowSpace.Instance != null &&
                DesktopWindowSpace.Instance.Valid &&
                DesktopWindowSpace.Instance.TryWorld(screenPos, out Vector2 world2D))
            {
                mouseWorld = world2D;
                return true;
            }

            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            mouseWorld = new Vector2(w.x, w.y);
            return true;
        }

        /// <summary>
        /// 폴더 내 앱 아이콘을 실행하여 대상 창(RepairWindow)을 엽니다.
        /// </summary>
        public bool ExecuteTargetApp()
        {
            var wm = Manager != null ? Manager : WindowManager.Instance;
            if (wm != null)
            {
                wm.Open(targetExecutableWindowId);
            }

            PlayExecuteSound();

            Debug.Log($"[FolderWindow] '{executableItemName}' 실행! -> '{targetExecutableWindowId}' 창을 엽니다.", this);
            onExecuted?.Invoke();
            OnExecuted?.Invoke();
            return true;
        }

        /// <summary>
        /// 테스트 또는 스크립트에서 더블클릭을 직접 시뮬레이션할 때 호출
        /// </summary>
        public bool SimulateDoubleClick()
        {
            return ExecuteTargetApp();
        }

        private void PlayExecuteSound()
        {
            AudioClip clip = executeSound != null ? executeSound : GetFallbackExecuteClip();
            if (clip == null) return;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                }
            }

            audioSource.PlayOneShot(clip, 0.8f);
        }

        private static Sprite GetFallbackAppIconSprite()
        {
            if (fallbackAppIconSprite != null) return fallbackAppIconSprite;

            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color bodyColor = new Color(0.85f, 0.88f, 0.92f, 1f);
            Color headerColor = new Color(0.18f, 0.35f, 0.75f, 1f);
            Color borderColor = new Color(0.12f, 0.12f, 0.12f, 1f);

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    if (x < 2 || x >= 30 || y < 2 || y >= 30)
                        tex.SetPixel(x, y, borderColor);
                    else if (y >= 24)
                        tex.SetPixel(x, y, headerColor);
                    else
                        tex.SetPixel(x, y, bodyColor);
                }
            }
            tex.Apply();

            fallbackAppIconSprite = Sprite.Create(
                tex,
                new Rect(0, 0, 32, 32),
                new Vector2(0.5f, 0.5f),
                32f
            );
            return fallbackAppIconSprite;
        }

        private static AudioClip GetFallbackExecuteClip()
        {
            if (fallbackExecuteClip != null) return fallbackExecuteClip;

            const int sampleRate = 22050;
            const float duration = 0.08f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float freq = Mathf.Lerp(650f, 980f, t);
                samples[i] = Mathf.Sin(t * freq * Mathf.PI * 2f) * (1f - t) * 0.3f;
            }

            fallbackExecuteClip = AudioClip.Create("AppExecute", sampleCount, 1, sampleRate, false);
            fallbackExecuteClip.SetData(samples, 0);
            return fallbackExecuteClip;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            size = new Vector2(5.5f, 3.8f);
            minSize = new Vector2(3.0f, 2.0f);
        }
#endif
    }
}

