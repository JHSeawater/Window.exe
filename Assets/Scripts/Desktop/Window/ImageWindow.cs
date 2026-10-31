using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    public class ImageWindow : Window
    {
        [SerializeField] private Sprite primarySprite;

        [Header("Break & Extract Mechanics")]
        [SerializeField] private float breakShakeDuration = 0.25f;
        [SerializeField] private float breakShakeMagnitude = 0.12f;
        [SerializeField] private float debrisFallDuration = 0.50f;
        [SerializeField] private int debrisPieceCount = 7;
        [SerializeField] private Vector2 debrisInwardVelocityRange = new Vector2(-4.6f, -2.4f);
        [SerializeField] private Vector2 debrisVerticalVelocityRange = new Vector2(-2.0f, 2.8f);
        [SerializeField] private float debrisGravity = -22f;
        [SerializeField] private Vector2 debrisAngularVelocityRange = new Vector2(-550f, 550f);

        [Header("Broken Window Overlay")]
        [SerializeField] private Sprite brokenOverlaySprite;
        [SerializeField] private Color brokenOverlayColor = new Color(1f, 1f, 1f, 0.88f);
        [SerializeField] private Vector2 brokenOverlayScale = Vector2.one;
        [SerializeField] private Vector2 brokenOverlayOffset = Vector2.zero;

        [Header("Audio")]
        [SerializeField] private AudioClip breakSoundClip;
        [SerializeField] private float breakSoundVolume = 0.85f;

        [Header("Events")]
        public UnityEvent onBorderBroken;
        public UnityEvent onKeyDroppedToRecycleBin;

        public event Action OnBorderBroken;
        public event Action OnKeyDroppedToRecycleBin;
        public static event Action<ImageWindow> OnAnyKeyDroppedToRecycleBin;

        private Transform primaryRoot;
        private SpriteRenderer image;
        private SpriteRenderer background;
        private SpriteRenderer secondaryBorder;
        private SpriteRenderer secondaryImage;

        private SpriteRenderer brokenOverlayFront;
        private SpriteRenderer brokenOverlayBack;
        private static Sprite defaultBrokenOverlaySprite;

        private class DebrisPiece
        {
            public SpriteRenderer front;
            public SpriteRenderer back;
            public Vector2 localPos;
            public Vector2 velocity;
            public float angle;
            public float angularVelocity;
            public Vector2 size;
        }

        private readonly System.Collections.Generic.List<DebrisPiece> debrisPieces = new System.Collections.Generic.List<DebrisPiece>();
        private SpriteRenderer debrisFront; // 첫 번째 파편 참조 (호환성용)
        private SpriteRenderer debrisBack;

        private Transform dragProxyRoot;
        private SpriteRenderer dragProxyFront;
        private SpriteRenderer dragProxyBack;

        private float shakeTimer;
        private float debrisTimer;
        private int lastSortingOrder;

        private AudioSource audioSource;
        private static AudioClip fallbackBreakClip;

        public Sprite PrimarySprite { get => primarySprite; set => SetPrimarySprite(value); }
        public Sprite GetPrimarySprite() => primarySprite;

        public Sprite BrokenOverlaySprite { get => brokenOverlaySprite; set => SetBrokenOverlaySprite(value); }
        public bool IsBrokenOverlayActive => brokenOverlayFront != null && brokenOverlayFront.gameObject.activeSelf;
        public SpriteRenderer BrokenOverlayFront => brokenOverlayFront;
        public SpriteRenderer BrokenOverlayBack => brokenOverlayBack;

        public AudioClip BreakSoundClip { get => breakSoundClip; set => breakSoundClip = value; }
        public AudioClip LastPlayedBreakClip { get; private set; }
        public int BreakSoundPlayCount { get; private set; }

        public bool IsBorderBroken => IsRightBorderBroken;
        public bool IsShakeActive => shakeTimer > 0f;
        public bool HasActiveDebris => debrisTimer > 0f && debrisPieces.Count > 0 && debrisPieces[0].front != null && debrisPieces[0].front.gameObject.activeSelf;
        public int DebrisPieceCount => debrisPieces.Count;

        public void GetDebrisPositions(System.Collections.Generic.List<Vector2> results)
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < debrisPieces.Count; i++)
            {
                results.Add(debrisPieces[i].localPos);
            }
        }

        public bool IsDraggingImage { get; private set; }
        public bool IsKeyOutsideWindow { get; private set; }
        public bool IsKeyConsumed { get; private set; }
        public Vector2 DraggedKeyWorldPosition => dragProxyRoot != null ? (Vector2)dragProxyRoot.position : (Vector2)transform.position;
        public Rect? OverrideRecycleBinWorldRect { get; set; }

        protected override void InitializePrimary()
        {
            primaryRoot = transform.Find("Primary");
            if (primaryRoot == null)
            {
                primaryRoot = new GameObject("Primary").transform;
                primaryRoot.SetParent(transform, false);
            }

            background = Renderer(primaryRoot, "Background");
            if (background.sprite == null) background.sprite = WindowTitleBar.WhiteSprite;
            background.drawMode = SpriteDrawMode.Sliced;

            image = Renderer(primaryRoot, "Image");
            image.drawMode = SpriteDrawMode.Simple;

            brokenOverlayFront = Renderer(primaryRoot, "BrokenOverlayFront");
            brokenOverlayFront.sortingLayerName = PrimarySortingLayerName;
            brokenOverlayFront.drawMode = SpriteDrawMode.Sliced;
            brokenOverlayFront.gameObject.SetActive(false);

            var secondaryRoot = transform.Find("Secondary");
            if (secondaryRoot != null)
            {
                if (SecondaryRenderer != null)
                {
                    SecondaryRenderer.sprite = WindowTitleBar.WhiteSprite;
                    SecondaryRenderer.color = Color.white;
                    SecondaryRenderer.drawMode = SpriteDrawMode.Sliced;
                    SecondaryRenderer.enabled = true;
                }

                secondaryBorder = Renderer(secondaryRoot, "SecondaryBorder");
                secondaryBorder.sortingLayerName = SecondarySortingLayerName;
                secondaryBorder.drawMode = SpriteDrawMode.Sliced;

                secondaryImage = Renderer(secondaryRoot, "SecondaryImage");
                secondaryImage.sortingLayerName = SecondarySortingLayerName;
                secondaryImage.drawMode = SpriteDrawMode.Simple;

                brokenOverlayBack = Renderer(secondaryRoot, "BrokenOverlayBack");
                brokenOverlayBack.sortingLayerName = SecondarySortingLayerName;
                brokenOverlayBack.drawMode = SpriteDrawMode.Sliced;
                brokenOverlayBack.gameObject.SetActive(false);
            }

            EnsureDebrisPieces(debrisPieceCount);
            EnsureDragProxy();
            SetPrimarySprite(primarySprite);
        }

        private void EnsureDebrisPieces(int count)
        {
            count = Mathf.Max(1, count);
            var secondaryRoot = transform.Find("Secondary");

            while (debrisPieces.Count < count)
            {
                int index = debrisPieces.Count;
                var piece = new DebrisPiece();

                piece.front = Renderer(primaryRoot, "BorderDebrisFront_" + index);
                piece.front.sprite = WindowTitleBar.WhiteSprite;
                piece.front.drawMode = SpriteDrawMode.Sliced;
                piece.front.sortingLayerName = PrimarySortingLayerName;
                piece.front.gameObject.SetActive(false);

                if (secondaryRoot != null)
                {
                    piece.back = Renderer(secondaryRoot, "BorderDebrisBack_" + index);
                    piece.back.sprite = WindowTitleBar.WhiteSprite;
                    piece.back.drawMode = SpriteDrawMode.Sliced;
                    piece.back.sortingLayerName = SecondarySortingLayerName;
                    piece.back.gameObject.SetActive(false);
                }

                debrisPieces.Add(piece);
            }

            if (debrisPieces.Count > 0)
            {
                debrisFront = debrisPieces[0].front;
                debrisBack = debrisPieces[0].back;
            }
        }

        private void EnsureDragProxy()
        {
            if (dragProxyRoot == null)
            {
                var found = transform.Find("DraggedKeyProxy");
                if (found == null)
                {
                    found = new GameObject("DraggedKeyProxy").transform;
                    found.SetParent(transform, false);
                }
                dragProxyRoot = found;
            }

            dragProxyFront = Renderer(dragProxyRoot, "DragKeyFront");
            dragProxyFront.sortingLayerName = PrimarySortingLayerName;
            dragProxyFront.drawMode = SpriteDrawMode.Simple;
            dragProxyFront.sortingOrder = 30000;

            dragProxyBack = Renderer(dragProxyRoot, "DragKeyBack");
            dragProxyBack.sortingLayerName = SecondarySortingLayerName;
            dragProxyBack.drawMode = SpriteDrawMode.Simple;
            dragProxyBack.sortingOrder = 30000;

            if (!IsDraggingImage && !IsKeyOutsideWindow)
                dragProxyRoot.gameObject.SetActive(false);
        }

        private static SpriteRenderer Renderer(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            var renderer = child.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer : child.gameObject.AddComponent<SpriteRenderer>();
        }

        public void SetPrimarySprite(Sprite sprite)
        {
            primarySprite = sprite;
            if (image == null) return;

            bool showInsideWindow = sprite != null && !IsDraggingImage && !IsKeyOutsideWindow && !IsKeyConsumed;
            image.sprite = sprite;
            image.enabled = showInsideWindow;

            if (secondaryImage != null)
            {
                secondaryImage.sprite = sprite;
                secondaryImage.enabled = showInsideWindow;
            }

            if (dragProxyFront != null) dragProxyFront.sprite = sprite;
            if (dragProxyBack != null) dragProxyBack.sprite = sprite;

            ApplySize(size);
        }

        protected override void UpdateBorderLayout(Vector2 value)
        {
            base.UpdateBorderLayout(value);
            if (secondaryBorder != null && BorderRenderer != null)
            {
                secondaryBorder.sprite = BorderRenderer.sprite;
                secondaryBorder.color = BorderRenderer.color;
                secondaryBorder.transform.localPosition = BorderRenderer.transform.localPosition;
                secondaryBorder.size = BorderRenderer.size;
                secondaryBorder.enabled = BorderRenderer.enabled;
            }
        }

        protected override void ApplySize(Vector2 value)
        {
            base.ApplySize(value);
            if (background != null) background.size = value;
            if (SecondaryRenderer != null)
            {
                SecondaryRenderer.sprite = WindowTitleBar.WhiteSprite;
                SecondaryRenderer.size = value;
                SecondaryRenderer.enabled = true;
            }

            if (image != null && image.sprite != null)
            {
                var b = image.sprite.bounds.size;
                var scale = Mathf.Min(value.x / Mathf.Max(0.0001f, b.x), value.y / Mathf.Max(0.0001f, b.y));
                var scaleVec = new Vector3(scale, scale, 1f);
                image.transform.localScale = scaleVec;
                if (secondaryImage != null) secondaryImage.transform.localScale = scaleVec;
                if (dragProxyFront != null) dragProxyFront.transform.localScale = scaleVec;
                if (dragProxyBack != null) dragProxyBack.transform.localScale = scaleVec;
            }

            UpdateBrokenOverlayLayout(value);
        }

        public override void ApplySorting(int order)
        {
            lastSortingOrder = order;
            base.ApplySorting(order);

            if (secondaryBorder != null)
            {
                secondaryBorder.sortingLayerName = SecondarySortingLayerName;
                secondaryBorder.sortingOrder = order - 1;
            }
            if (SecondaryRenderer != null)
            {
                SecondaryRenderer.sortingLayerName = SecondarySortingLayerName;
                SecondaryRenderer.sortingOrder = order;
            }
            if (secondaryImage != null)
            {
                secondaryImage.sortingLayerName = SecondarySortingLayerName;
                secondaryImage.sortingOrder = order + 1;
            }

            if (brokenOverlayFront != null)
            {
                brokenOverlayFront.sortingLayerName = PrimarySortingLayerName;
                brokenOverlayFront.sortingOrder = order + 3;
            }
            if (brokenOverlayBack != null)
            {
                brokenOverlayBack.sortingLayerName = SecondarySortingLayerName;
                brokenOverlayBack.sortingOrder = order + 3;
            }

            for (int i = 0; i < debrisPieces.Count; i++)
            {
                var piece = debrisPieces[i];
                if (piece.front != null)
                {
                    piece.front.sortingLayerName = PrimarySortingLayerName;
                    piece.front.sortingOrder = order + 4;
                }
                if (piece.back != null)
                {
                    piece.back.sortingLayerName = SecondarySortingLayerName;
                    piece.back.sortingOrder = order + 4;
                }
            }

            if (dragProxyFront != null)
            {
                dragProxyFront.sortingLayerName = PrimarySortingLayerName;
                dragProxyFront.sortingOrder = 30000;
            }
            if (dragProxyBack != null)
            {
                dragProxyBack.sortingLayerName = SecondarySortingLayerName;
                dragProxyBack.sortingOrder = 30000;
            }
        }

        public bool BreakRightBorder()
        {
            if (IsBorderBroken) return false;

            // 1. 테두리 가로 두께를 줄이고 왼쪽으로 이동하여 우측 테두리 제거 (전환)
            SetRightBorderBroken(true);

            // 2. 우측 테두리 다중 선분 파편 탈락 연출 시작 (창 안쪽으로 쏟아져 내림)
            debrisTimer = debrisFallDuration;
            EnsureDebrisPieces(debrisPieceCount);

            float totalH = size.y + BarHeight + BorderThickness * 2f;
            float startX = size.x * 0.5f + BorderThickness * 0.5f;
            float pieceH = totalH / debrisPieces.Count;

            for (int i = 0; i < debrisPieces.Count; i++)
            {
                var piece = debrisPieces[i];
                float pieceY = -totalH * 0.5f + (i + 0.5f) * pieceH + BarHeight * 0.5f;
                piece.localPos = new Vector2(startX, pieceY);

                float vx = UnityEngine.Random.Range(debrisInwardVelocityRange.x, debrisInwardVelocityRange.y);
                float vy = UnityEngine.Random.Range(debrisVerticalVelocityRange.x, debrisVerticalVelocityRange.y);
                piece.velocity = new Vector2(vx, vy);
                piece.angle = 0f;
                piece.angularVelocity = UnityEngine.Random.Range(debrisAngularVelocityRange.x, debrisAngularVelocityRange.y);
                piece.size = new Vector2(BorderThickness, pieceH);

                if (piece.front != null)
                {
                    piece.front.color = BorderColor;
                    piece.front.size = piece.size;
                    piece.front.transform.localPosition = piece.localPos;
                    piece.front.transform.localRotation = Quaternion.identity;
                    piece.front.gameObject.SetActive(true);
                }
                if (piece.back != null)
                {
                    piece.back.color = BorderColor;
                    piece.back.size = piece.size;
                    piece.back.transform.localPosition = piece.localPos;
                    piece.back.transform.localRotation = Quaternion.identity;
                    piece.back.gameObject.SetActive(true);
                }
            }

            // 3. 피격 창 흔들림 연출 시작
            shakeTimer = breakShakeDuration;

            // 4. 깨진 유리 오버레이 텍스처 덧씌우기
            ActivateBrokenOverlay();

            // 5. 깨지는 효과음 재생
            PlayBreakSound();

            onBorderBroken?.Invoke();
            OnBorderBroken?.Invoke();
            return true;
        }

        public void ManualTick(float deltaTime)
        {
            if (shakeTimer > 0f)
            {
                shakeTimer = Mathf.Max(0f, shakeTimer - deltaTime);
                if (shakeTimer > 0f)
                {
                    float ratio = shakeTimer / Mathf.Max(0.0001f, breakShakeDuration);
                    float offsetX = Mathf.Sin(shakeTimer * 95f) * breakShakeMagnitude * ratio;
                    float offsetY = Mathf.Cos(shakeTimer * 120f) * breakShakeMagnitude * 0.6f * ratio;
                    var offset = new Vector3(offsetX, offsetY, 0f);
                    if (primaryRoot != null) primaryRoot.localPosition = offset;
                    if (secondaryImage != null) secondaryImage.transform.localPosition = offset;
                }
                else
                {
                    if (primaryRoot != null) primaryRoot.localPosition = Vector3.zero;
                    if (secondaryImage != null) secondaryImage.transform.localPosition = Vector3.zero;
                }
            }

            if (debrisTimer > 0f)
            {
                debrisTimer = Mathf.Max(0f, debrisTimer - deltaTime);
                float alpha = Mathf.Clamp01(debrisTimer / Mathf.Max(0.0001f, debrisFallDuration));
                var c = BorderColor;
                c.a *= alpha;

                for (int i = 0; i < debrisPieces.Count; i++)
                {
                    var piece = debrisPieces[i];
                    piece.velocity.y += debrisGravity * deltaTime;
                    piece.localPos += piece.velocity * deltaTime;
                    piece.angle += piece.angularVelocity * deltaTime;
                    var rot = Quaternion.Euler(0f, 0f, piece.angle);

                    if (piece.front != null)
                    {
                        piece.front.transform.localPosition = piece.localPos;
                        piece.front.transform.localRotation = rot;
                        piece.front.color = c;
                        if (debrisTimer <= 0f) piece.front.gameObject.SetActive(false);
                    }
                    if (piece.back != null)
                    {
                        piece.back.transform.localPosition = piece.localPos;
                        piece.back.transform.localRotation = rot;
                        piece.back.color = c;
                        if (debrisTimer <= 0f) piece.back.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void SetBrokenOverlaySprite(Sprite sprite)
        {
            brokenOverlaySprite = sprite;
            if (IsBrokenOverlayActive)
            {
                Sprite targetSprite = sprite != null ? sprite : GetOrCreateDefaultBrokenSprite();
                if (brokenOverlayFront != null) brokenOverlayFront.sprite = targetSprite;
                if (brokenOverlayBack != null) brokenOverlayBack.sprite = targetSprite;
                UpdateBrokenOverlayLayout(size);
            }
        }

        private void ActivateBrokenOverlay()
        {
            Sprite targetSprite = brokenOverlaySprite != null ? brokenOverlaySprite : GetOrCreateDefaultBrokenSprite();

            if (brokenOverlayFront != null)
            {
                brokenOverlayFront.sprite = targetSprite;
                brokenOverlayFront.gameObject.SetActive(true);
            }
            if (brokenOverlayBack != null)
            {
                brokenOverlayBack.sprite = targetSprite;
                brokenOverlayBack.gameObject.SetActive(true);
            }

            UpdateBrokenOverlayLayout(size);
            ApplySorting(lastSortingOrder);
        }

        private void UpdateBrokenOverlayLayout(Vector2 value)
        {
            if (brokenOverlayFront == null && brokenOverlayBack == null) return;

            Vector2 overlaySize = new Vector2(value.x * brokenOverlayScale.x, value.y * brokenOverlayScale.y);
            Vector3 overlayPos = new Vector3(brokenOverlayOffset.x, brokenOverlayOffset.y, 0f);

            if (brokenOverlayFront != null)
            {
                brokenOverlayFront.color = brokenOverlayColor;
                brokenOverlayFront.size = overlaySize;
                brokenOverlayFront.transform.localPosition = overlayPos;
            }
            if (brokenOverlayBack != null)
            {
                brokenOverlayBack.color = brokenOverlayColor;
                brokenOverlayBack.size = overlaySize;
                brokenOverlayBack.transform.localPosition = overlayPos;
            }
        }

        private static Sprite GetOrCreateDefaultBrokenSprite()
        {
            if (defaultBrokenOverlaySprite != null) return defaultBrokenOverlaySprite;

            const int width = 64;
            const int height = 64;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color transparent = new Color(1f, 1f, 1f, 0f);
            Color crackColor = new Color(1f, 1f, 1f, 0.95f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }

            // 거친 유리 크랙 선들 그리기
            DrawBresenhamLine(tex, 0, height / 2, width - 1, height - 1, crackColor);
            DrawBresenhamLine(tex, 0, height / 2, width - 1, 0, crackColor);
            DrawBresenhamLine(tex, width / 2, 0, width / 2, height - 1, crackColor);
            DrawBresenhamLine(tex, 10, height - 1, width - 1, height / 3, crackColor);
            DrawBresenhamLine(tex, 15, 0, width - 1, height * 2 / 3, crackColor);

            tex.Apply();
            defaultBrokenOverlaySprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            return defaultBrokenOverlaySprite;
        }

        private static void DrawBresenhamLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if (x0 >= 0 && x0 < tex.width && y0 >= 0 && y0 < tex.height)
                    tex.SetPixel(x0, y0, col);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void PlayBreakSound()
        {
            AudioClip clip = breakSoundClip != null ? breakSoundClip : GetFallbackBreakClip();
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

            LastPlayedBreakClip = clip;
            BreakSoundPlayCount++;
            audioSource.PlayOneShot(clip, breakSoundVolume);
        }

        private static AudioClip GetFallbackBreakClip()
        {
            if (fallbackBreakClip != null) return fallbackBreakClip;
            fallbackBreakClip = CreateGlassShatterToneClip("KeyWindowBreak", 0.18f);
            return fallbackBreakClip;
        }

        private static AudioClip CreateGlassShatterToneClip(string clipName, float durationSeconds)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] samples = new float[sampleCount];
            float phase1 = 0f;
            float phase2 = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float freq1 = Mathf.Lerp(1650f, 320f, t);
                float freq2 = Mathf.Lerp(2400f, 180f, t * t);
                phase1 += freq1 / sampleRate;
                phase2 += freq2 / sampleRate;

                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.45f;
                float tone1 = Mathf.Sin(phase1 * Mathf.PI * 2f) * 0.35f;
                float tone2 = Mathf.Sin(phase2 * Mathf.PI * 2f) * 0.20f;
                float envelope = Mathf.Pow(1f - t, 1.8f);

                samples[i] = Mathf.Clamp((noise + tone1 + tone2) * envelope * 0.85f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public Rect GetKeyWorldRect()
        {
            if (IsDraggingImage || IsKeyOutsideWindow)
            {
                if (dragProxyFront != null && dragProxyFront.sprite != null)
                {
                    var b = dragProxyFront.bounds;
                    if (b.size.x > 0.001f && b.size.y > 0.001f)
                        return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                }
                Vector2 p = DraggedKeyWorldPosition;
                return new Rect(p.x - size.x * 0.4f, p.y - size.y * 0.4f, size.x * 0.8f, size.y * 0.8f);
            }

            if (image != null && image.sprite != null)
            {
                var b = image.bounds;
                if (b.size.x > 0.001f && b.size.y > 0.001f)
                    return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
            }

            return Rect.MinMaxRect(
                transform.position.x - size.x * 0.5f,
                transform.position.y - size.y * 0.5f,
                transform.position.x + size.x * 0.5f,
                transform.position.y + size.y * 0.5f
            );
        }

        public override bool Hit(Vector2 point)
        {
            if (IsBorderBroken && !IsKeyConsumed && (IsDraggingImage || IsKeyOutsideWindow) && GetKeyWorldRect().Contains(point))
                return true;
            return base.Hit(point);
        }

        public override ResizeDirection? ResizeAt(Vector2 point)
        {
            if (IsBorderBroken && !IsKeyConsumed && GetKeyWorldRect().Contains(point))
                return null;
            return base.ResizeAt(point);
        }

        public bool BeginKeyDrag(Vector2 worldPoint)
        {
            if (!IsBorderBroken || IsKeyConsumed || primarySprite == null)
                return false;

            Rect keyRect = GetKeyWorldRect();
            bool insideBody = !IsKeyOutsideWindow &&
                              worldPoint.x >= transform.position.x - size.x * 0.5f &&
                              worldPoint.x <= transform.position.x + size.x * 0.5f &&
                              worldPoint.y >= transform.position.y - size.y * 0.5f &&
                              worldPoint.y <= transform.position.y + size.y * 0.5f;

            if (!keyRect.Contains(worldPoint) && !insideBody)
                return false;

            EnsureDragProxy();
            IsDraggingImage = true;

            if (image != null) image.enabled = false;
            if (secondaryImage != null) secondaryImage.enabled = false;

            dragProxyRoot.gameObject.SetActive(true);
            dragProxyRoot.position = new Vector3(worldPoint.x, worldPoint.y, transform.position.z);
            ApplySorting(lastSortingOrder);
            return true;
        }

        public void UpdateKeyDrag(Vector2 worldPoint)
        {
            if (!IsDraggingImage) return;

            if (dragProxyRoot != null)
            {
                dragProxyRoot.position = new Vector3(worldPoint.x, worldPoint.y, transform.position.z);
            }

            if (!OuterBounds.Contains(worldPoint))
            {
                IsKeyOutsideWindow = true;
            }
        }

        public bool EndKeyDrag(Vector2 worldPoint, Vector2? screenPoint = null)
        {
            if (!IsDraggingImage) return false;

            UpdateKeyDrag(worldPoint);
            IsDraggingImage = false;

            if (IsPointOverRecycleBin(worldPoint, screenPoint))
            {
                IsKeyConsumed = true;
                IsKeyOutsideWindow = true;

                if (dragProxyRoot != null)
                    dragProxyRoot.gameObject.SetActive(false);

                if (image != null) image.enabled = false;
                if (secondaryImage != null) secondaryImage.enabled = false;

                Debug.Log("[KeyToTrash] 열쇠 이미지를 쓰레기통에 드롭했습니다! (이벤트 발생)", this);
                onKeyDroppedToRecycleBin?.Invoke();
                OnKeyDroppedToRecycleBin?.Invoke();
                OnAnyKeyDroppedToRecycleBin?.Invoke(this);
                return true;
            }

            if (!OuterBounds.Contains(worldPoint))
            {
                // 창 밖으로 빼낸 상태 유지 (다시 드래그해서 쓰레기통으로 옮길 수 있음)
                IsKeyOutsideWindow = true;
                if (dragProxyRoot != null)
                    dragProxyRoot.gameObject.SetActive(true);
            }
            else
            {
                // 창 내부에서 놓았으면 다시 창 안으로 복귀
                IsKeyOutsideWindow = false;
                if (dragProxyRoot != null)
                    dragProxyRoot.gameObject.SetActive(false);
                if (image != null) image.enabled = primarySprite != null;
                if (secondaryImage != null) secondaryImage.enabled = primarySprite != null;
            }

            return false;
        }

        public bool IsPointOverRecycleBin(Vector2 worldPoint, Vector2? screenPoint = null)
        {
            if (OverrideRecycleBinWorldRect.HasValue)
            {
                return OverrideRecycleBinWorldRect.Value.Contains(worldPoint);
            }

            // 1. 바탕화면 휴지통 아이콘(DesktopIconView) 판정
            var icons = FindObjectsByType<DesktopIconView>();
            Vector3[] corners = new Vector3[4];
            foreach (var icon in icons)
            {
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;

                bool isRecycleIcon =
                    (icon.Item != null && string.Equals(icon.Item.ItemId, "recycle-bin", StringComparison.OrdinalIgnoreCase)) ||
                    icon.name.IndexOf("recycle-bin", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isRecycleIcon) continue;

                var rectTransform = icon.Rect;
                if (rectTransform != null)
                {
                    rectTransform.GetWorldCorners(corners);
                    Rect worldRect = Rect.MinMaxRect(
                        Mathf.Min(corners[0].x, corners[2].x),
                        Mathf.Min(corners[0].y, corners[2].y),
                        Mathf.Max(corners[0].x, corners[2].x),
                        Mathf.Max(corners[0].y, corners[2].y)
                    );
                    if (worldRect.width > 0.0001f && worldRect.height > 0.0001f && worldRect.Contains(worldPoint))
                        return true;

                    if (screenPoint.HasValue)
                    {
                        var canvas = icon.GetComponentInParent<Canvas>();
                        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                            ? canvas.worldCamera
                            : null;
                        if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint.Value, cam))
                            return true;
                    }
                }
            }

            // 2. 열려 있는 휴지통 창("recycle-bin") 판정
            var wm = Manager != null ? Manager : WindowManager.Instance;
            if (wm != null)
            {
                var recycleProc = wm.GetProcess("recycle-bin");
                if (recycleProc != null &&
                    recycleProc.State != WindowState.Minimized &&
                    recycleProc.Instance != null &&
                    recycleProc.Instance.gameObject.activeInHierarchy &&
                    recycleProc.Instance.OuterBounds.Contains(worldPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            ManualTick(Time.unscaledDeltaTime);

            if (!IsBorderBroken || IsKeyConsumed || Mouse.current == null)
                return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            if (!TryGetMouseWorld(screenPos, out Vector2 worldPos))
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame && !IsDraggingImage)
            {
                if (!TitleContains(worldPos) && ButtonAt(worldPos) < 0 && !IsBlockedByHigherWindowControl(worldPos))
                {
                    BeginKeyDrag(worldPos);
                }
            }
            else if (IsDraggingImage && Mouse.current.leftButton.isPressed)
            {
                UpdateKeyDrag(worldPos);
            }
            else if (IsDraggingImage && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndKeyDrag(worldPos, screenPos);
            }
        }

        private bool IsBlockedByHigherWindowControl(Vector2 worldPoint)
        {
            var wm = Manager != null ? Manager : WindowManager.Instance;
            if (wm == null) return false;

            var order = wm.DisplayOrder;
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var proc = order[i];
                if (proc == null || proc.State == WindowState.Minimized || proc.Instance == null || !proc.Instance.gameObject.activeInHierarchy)
                    continue;

                var other = proc.Instance;
                if (ReferenceEquals(other, this))
                    break;

                if (!other.Hit(worldPoint))
                    continue;

                // WINDOW.exe 뷰포트 본문 내부는 투시 영역이므로 드래그를 가로막지 않음 (단, 제목 표시줄/리사이즈 핸들은 제외)
                if (other is WindowExeWindow || string.Equals(proc.Id, "window", StringComparison.OrdinalIgnoreCase) || string.Equals(proc.Id, "WINDOW.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (other.TitleContains(worldPoint) || other.ButtonAt(worldPoint) >= 0 || other.IsPointerOverResizeHandle(worldPoint))
                        return true;
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool TryGetMouseWorld(Vector2 screenPos, out Vector2 worldPos)
        {
            if (DesktopWindowSpace.Instance != null &&
                DesktopWindowSpace.Instance.Valid &&
                DesktopWindowSpace.Instance.TryWorld(screenPos, out worldPos))
            {
                return true;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                worldPos = new Vector2(w.x, w.y);
                return true;
            }

            worldPos = Vector2.zero;
            return false;
        }
    }
}
