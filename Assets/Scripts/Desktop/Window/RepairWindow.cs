using System;
using UnityEngine;
using UnityEngine.Events;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>
    /// 복원 윈도우 (Repair Window):
    /// 이미지 하나를 표시하며, 열쇠 사용 및 향후 상호작용에 따라 이미지를 교체할 수 있는 창입니다.
    /// WindowFront와 WindowBack 듀얼 레이어로 이미지를 렌더링하여 WINDOW.exe 투시와 완벽히 호환됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RepairWindow : Window
    {
        [Header("Repair Images")]
        [Tooltip("복원 창의 초기 1단계 이미지")]
        [SerializeField] private Sprite initialSprite;

        [Tooltip("초기 자물쇠를 WINDOW.exe로 비췄을 때의 이미지. 미할당이면 일반 이미지와 동일하게 표시")]
        [SerializeField] private Sprite initialRevealSprite;

        [Tooltip("열쇠를 사용했을 때 교체되는 2단계 복원 이미지")]
        [SerializeField] private Sprite secondSprite;

        [Tooltip("해제된 자물쇠를 WINDOW.exe로 비췄을 때의 이미지. 미할당이면 일반 이미지와 동일하게 표시")]
        [SerializeField] private Sprite secondRevealSprite;

        [Header("Behavior Settings")]
        [Tooltip("열쇠를 복원 창에 사용했을 때 열쇠를 소모(파괴)할지 여부 (기본값: true)")]
        [SerializeField] private bool consumeKeyOnUse = true;

        [Header("Audio")]
        [SerializeField] private AudioClip keyUsedClip;
        [SerializeField] private float soundVolume = 1f;

        [Header("Events")]
        [SerializeField] private UnityEvent onKeyUsed;
        [SerializeField] private UnityEvent<Sprite> onImageChanged;

        private SpriteRenderer frontImageRenderer;
        private SpriteRenderer backImageRenderer;
        private AudioSource audioSource;
        private Sprite currentSprite;
        private static Sprite fallbackInitialSprite;
        private static Sprite fallbackSecondSprite;
        private static AudioClip fallbackChimeClip;

        public event Action OnKeyUsed;
        public event Action<Sprite> OnImageChanged;

        public bool ConsumeKeyOnUse
        {
            get => consumeKeyOnUse;
            set => consumeKeyOnUse = value;
        }

        public Sprite InitialSprite
        {
            get => initialSprite;
            set
            {
                initialSprite = value;
                if (CurrentStage == 0)
                    ApplySprite(value != null ? value : GetFallbackInitialSprite());
            }
        }

        public Sprite SecondSprite
        {
            get => secondSprite;
            set => secondSprite = value;
        }

        public Sprite CurrentSprite => currentSprite;
        public int CurrentStage { get; private set; } = 0;
        public SpriteRenderer FrontImageRenderer => frontImageRenderer;
        public SpriteRenderer BackImageRenderer => backImageRenderer;

        public static RepairWindow OverrideActiveRepairWindow { get; set; }

        protected override void InitializePrimary()
        {
            SetupImageRenderers();

            Sprite spriteToUse = CurrentStage == 0
                ? (initialSprite != null ? initialSprite : GetFallbackInitialSprite())
                : (currentSprite != null ? currentSprite : secondSprite != null ? secondSprite : GetFallbackSecondSprite());
            ApplySprite(spriteToUse);
        }

        private void SetupImageRenderers()
        {
            Transform primaryTransform = transform.Find("Primary");
            if (primaryTransform == null)
            {
                var primaryGo = new GameObject("Primary");
                primaryGo.transform.SetParent(transform, false);
                primaryGo.transform.localPosition = Vector3.zero;
                primaryTransform = primaryGo.transform;
            }

            Transform frontImg = primaryTransform.Find("RepairImageFront");
            if (frontImg == null)
            {
                var go = new GameObject("RepairImageFront");
                go.transform.SetParent(primaryTransform, false);
                frontImg = go.transform;
            }

            frontImageRenderer = frontImg.GetComponent<SpriteRenderer>();
            if (frontImageRenderer == null)
                frontImageRenderer = frontImg.gameObject.AddComponent<SpriteRenderer>();

            frontImageRenderer.sortingLayerName = PrimarySortingLayerName;
            frontImageRenderer.sortingOrder = 1;

            Transform secondaryTransform = transform.Find("Secondary");
            if (secondaryTransform == null)
            {
                var secGo = new GameObject("Secondary");
                secGo.transform.SetParent(transform, false);
                secGo.transform.localPosition = Vector3.zero;
                secondaryTransform = secGo.transform;
            }

            Transform backImg = secondaryTransform.Find("RepairImageBack");
            if (backImg == null)
            {
                var go = new GameObject("RepairImageBack");
                go.transform.SetParent(secondaryTransform, false);
                backImg = go.transform;
            }

            backImageRenderer = backImg.GetComponent<SpriteRenderer>();
            if (backImageRenderer == null)
                backImageRenderer = backImg.gameObject.AddComponent<SpriteRenderer>();

            backImageRenderer.sortingLayerName = SecondarySortingLayerName;
            backImageRenderer.sortingOrder = 1;
        }

        public void ApplySprite(Sprite sprite)
        {
            currentSprite = sprite;

            if (frontImageRenderer == null || backImageRenderer == null)
                SetupImageRenderers();

            if (frontImageRenderer != null)
            {
                frontImageRenderer.sprite = sprite;
                frontImageRenderer.enabled = sprite != null;
            }

            if (backImageRenderer != null)
            {
                var revealSprite = CurrentStage == 0 && sprite == initialSprite ? initialRevealSprite
                    : CurrentStage == 1 && sprite == secondSprite ? secondRevealSprite : null;
                var backSprite = revealSprite != null ? revealSprite : sprite;
                backImageRenderer.sprite = backSprite;
                backImageRenderer.enabled = backSprite != null;
            }

            UpdateImageLayout(size);
        }

        protected override void ApplySize(Vector2 newSize)
        {
            base.ApplySize(newSize);
            UpdateImageLayout(newSize);
        }

        private void UpdateImageLayout(Vector2 windowSize)
        {
            if (frontImageRenderer == null && backImageRenderer == null)
                return;

            Vector2 targetSize = new Vector2(
                Mathf.Max(0.1f, windowSize.x - 0.2f),
                Mathf.Max(0.1f, windowSize.y - 0.2f)
            );

            // 본문 중심(0, 0)에 맞춤
            Vector3 centerPos = Vector3.zero;

            ConfigureRendererLayout(frontImageRenderer, targetSize, centerPos);
            ConfigureRendererLayout(backImageRenderer, targetSize, centerPos);
        }

        private void ConfigureRendererLayout(SpriteRenderer sr, Vector2 targetSize, Vector3 pos)
        {
            if (sr == null || sr.sprite == null) return;

            sr.transform.localPosition = pos;

            if (sr.sprite.border != Vector4.zero)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = targetSize;
            }
            else
            {
                sr.drawMode = SpriteDrawMode.Simple;
                Vector2 native = sr.sprite.bounds.size;
                if (native.x > 0.0001f && native.y > 0.0001f)
                {
                    float scaleX = targetSize.x / native.x;
                    float scaleY = targetSize.y / native.y;
                    float uniformScale = Mathf.Min(scaleX, scaleY);
                    sr.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
                }
            }
        }

        /// <summary>
        /// 열쇠를 복원 창에 사용합니다.
        /// 초기 단계(Stage 0)일 때 두 번째 이미지(secondSprite)로 교체하고 관련 이벤트를 호출합니다.
        /// </summary>
        public bool TryUseKey()
        {
            if (CurrentStage != 0)
                return false;

            CurrentStage = 1;
            Sprite nextSprite = secondSprite != null ? secondSprite : GetFallbackSecondSprite();
            ApplySprite(nextSprite);

            PlayKeyUsedSound();

            Debug.Log("[RepairWindow] 열쇠가 성공적으로 복원 창에 사용되었습니다! (이미지 교체 완료)", this);
            onKeyUsed?.Invoke();
            OnKeyUsed?.Invoke();
            onImageChanged?.Invoke(nextSprite);
            OnImageChanged?.Invoke(nextSprite);

            return true;
        }

        /// <summary>
        /// 향후 상호작용 확장을 위한 임의의 스프라이트 교체 메서드
        /// </summary>
        public void SetSprite(Sprite newSprite)
        {
            ApplySprite(newSprite);
            onImageChanged?.Invoke(newSprite);
            OnImageChanged?.Invoke(newSprite);
        }

        public void SetStage(int stage, Sprite sprite = null)
        {
            CurrentStage = stage;
            if (sprite != null)
            {
                SetSprite(sprite);
            }
        }

        private void PlayKeyUsedSound()
        {
            AudioClip clip = keyUsedClip != null ? keyUsedClip : GetFallbackChimeClip();
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

            audioSource.PlayOneShot(clip, soundVolume);
        }

        public static Rect? OverrideRepairWindowWorldRect { get; set; }

        public virtual bool CanUseKeyAt(Vector2 worldPos) => CurrentStage == 0 && OuterBounds.Contains(worldPos);

        public static RepairWindow FindRepairWindowAt(Vector2 worldPos, WindowManager wm = null)
        {
            if (OverrideActiveRepairWindow != null && OverrideActiveRepairWindow.gameObject.activeInHierarchy)
            {
                Rect b = OverrideRepairWindowWorldRect.HasValue ? OverrideRepairWindowWorldRect.Value : OverrideActiveRepairWindow.OuterBounds;
                if (b.Contains(worldPos) && (OverrideRepairWindowWorldRect.HasValue || OverrideActiveRepairWindow.CanUseKeyAt(worldPos)))
                    return OverrideActiveRepairWindow;
            }

            if (wm == null) wm = WindowManager.Instance;
            if (wm != null)
            {
                foreach (var proc in wm.ActiveProcesses)
                {
                    if (proc == null || proc.State == WindowState.Minimized || proc.Instance == null)
                        continue;

                    if (proc.Instance is RepairWindow rw && rw.gameObject.activeInHierarchy)
                    {
                        Rect b = OverrideRepairWindowWorldRect.HasValue ? OverrideRepairWindowWorldRect.Value : rw.OuterBounds;
                        if (b.Contains(worldPos) && (OverrideRepairWindowWorldRect.HasValue || rw.CanUseKeyAt(worldPos)))
                            return rw;
                    }
                }
            }

            var activeRepairWindows = FindObjectsByType<RepairWindow>(FindObjectsSortMode.None);
            foreach (var rw in activeRepairWindows)
            {
                if (rw != null && rw.gameObject.activeInHierarchy)
                {
                    Rect b = OverrideRepairWindowWorldRect.HasValue ? OverrideRepairWindowWorldRect.Value : rw.OuterBounds;
                    if (b.Contains(worldPos) && (OverrideRepairWindowWorldRect.HasValue || rw.CanUseKeyAt(worldPos)))
                        return rw;
                }
            }

            return null;
        }

        public static Sprite GetFallbackInitialSprite()
        {
            if (fallbackInitialSprite != null) return fallbackInitialSprite;

            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Color baseColor = new Color(0.24f, 0.28f, 0.35f, 1f);
            Color innerColor = new Color(0.18f, 0.21f, 0.27f, 1f);

            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    bool border = x < 4 || x >= 124 || y < 4 || y >= 124;
                    tex.SetPixel(x, y, border ? baseColor : innerColor);
                }
            }
            tex.Apply();

            fallbackInitialSprite = Sprite.Create(
                tex,
                new Rect(0, 0, 128, 128),
                new Vector2(0.5f, 0.5f),
                100f
            );
            return fallbackInitialSprite;
        }

        public static Sprite GetFallbackSecondSprite()
        {
            if (fallbackSecondSprite != null) return fallbackSecondSprite;

            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Color baseColor = new Color(0.20f, 0.65f, 0.38f, 1f);
            Color innerColor = new Color(0.28f, 0.78f, 0.46f, 1f);

            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    bool border = x < 4 || x >= 124 || y < 4 || y >= 124;
                    tex.SetPixel(x, y, border ? baseColor : innerColor);
                }
            }
            tex.Apply();

            fallbackSecondSprite = Sprite.Create(
                tex,
                new Rect(0, 0, 128, 128),
                new Vector2(0.5f, 0.5f),
                100f
            );
            return fallbackSecondSprite;
        }

        private static AudioClip GetFallbackChimeClip()
        {
            if (fallbackChimeClip != null) return fallbackChimeClip;

            const int sampleRate = 22050;
            const float duration = 0.25f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float freq = Mathf.Lerp(520f, 1040f, t);
                float wave = Mathf.Sin(t * freq * Mathf.PI * 2f);
                float env = 1f - t;
                samples[i] = wave * env * 0.4f;
            }

            fallbackChimeClip = AudioClip.Create("RepairChime", sampleCount, 1, sampleRate, false);
            fallbackChimeClip.SetData(samples, 0);
            return fallbackChimeClip;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            size = new Vector2(5.5f, 4.0f);
            minSize = new Vector2(3.0f, 2.0f);
        }
#endif
    }
}
