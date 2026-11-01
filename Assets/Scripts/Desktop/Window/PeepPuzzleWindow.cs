using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WindowExe.Desktop.Desktop.Window
{
    public class PeepPuzzleWindow : Window
    {
        [Header("Primary (Front Normal View)")]
        [SerializeField] private Sprite primarySprite;

        [Header("WINDOW.exe Target")]
        [SerializeField] private string targetWindowId = "window";

        [Header("Puzzle Timers (Seconds)")]
        [SerializeField] private float dangerMaxTime = 3.0f;
        [SerializeField] private float watcherMaxUnseenTime = 15.0f;

        [Header("Game Over / Scene Transition")]
        [SerializeField] private bool loadGameOverScene = true;
        [SerializeField] private string gameOverSceneName = "YouAreDie";

        [Header("Puzzle Objects (Secondary 하위 오브젝트)")]
        [SerializeField] private List<PeepPuzzleObject> clues = new List<PeepPuzzleObject>();
        [SerializeField] private List<PeepPuzzleObject> dangerObjects = new List<PeepPuzzleObject>();
        [SerializeField] private List<PeepPuzzleObject> watcherObjects = new List<PeepPuzzleObject>();
        [SerializeField] private PeepPuzzleObject escapeObject;

        [Header("Audio (선택적 수동 할당 - 없으면 미재생)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clueCollectedSound;
        [SerializeField] private AudioClip puzzleClearedSound;
        [SerializeField] private AudioClip gameOverSound;

        [Header("Events (확장성)")]
        public UnityEvent onPuzzleCleared;
        public UnityEvent<string> onGameOver;

        public event Action OnCleared;
        public event Action<string> OnGameOverTriggered;

        private SpriteRenderer primaryImage;
        private SpriteRenderer primaryBackground;

        private readonly Dictionary<PeepPuzzleObject, float> dangerTimers = new Dictionary<PeepPuzzleObject, float>();
        private readonly Dictionary<PeepPuzzleObject, float> watcherTimers = new Dictionary<PeepPuzzleObject, float>();

        private float currentDangerTimer;
        private float currentWatcherTimer;
        private bool isCleared;
        private bool isGameOver;

        public float DangerTimer => currentDangerTimer;
        public float WatcherTimer => currentWatcherTimer;
        public float DangerMaxTime { get => dangerMaxTime; set => dangerMaxTime = value; }
        public float WatcherMaxUnseenTime { get => watcherMaxUnseenTime; set => watcherMaxUnseenTime = value; }
        public bool LoadGameOverScene { get => loadGameOverScene; set => loadGameOverScene = value; }
        public string GameOverSceneName { get => gameOverSceneName; set => gameOverSceneName = value; }
        public bool IsCleared => isCleared;
        public bool IsGameOver => isGameOver;
        public string TargetWindowId { get => targetWindowId; set => targetWindowId = value; }

        public IReadOnlyList<PeepPuzzleObject> Clues => clues;
        public IReadOnlyList<PeepPuzzleObject> DangerObjects => dangerObjects;
        public IReadOnlyList<PeepPuzzleObject> WatcherObjects => watcherObjects;
        public PeepPuzzleObject DangerObject => dangerObjects != null && dangerObjects.Count > 0 ? dangerObjects[0] : null;
        public PeepPuzzleObject WatcherObject => watcherObjects != null && watcherObjects.Count > 0 ? watcherObjects[0] : null;
        public PeepPuzzleObject EscapeObject => escapeObject;

        // 단위 테스트 및 외부 제어를 위한 오버라이드 뷰포트
        public Rect? OverridePeepViewport { get; set; }

        public bool AreAllCluesCollected
        {
            get
            {
                if (clues == null || clues.Count == 0) return true;
                for (var i = 0; i < clues.Count; i++)
                {
                    if (clues[i] != null && !clues[i].IsCollected) return false;
                }
                return true;
            }
        }

        protected override void InitializePrimary()
        {
            var primary = transform.Find("Primary");
            if (primary == null)
            {
                primary = new GameObject("Primary").transform;
                primary.SetParent(transform, false);
            }

            primaryBackground = GetOrAddRenderer(primary, "Background");
            if (primaryBackground.sprite == null) primaryBackground.sprite = WindowTitleBar.WhiteSprite;
            primaryBackground.drawMode = SpriteDrawMode.Sliced;

            primaryImage = GetOrAddRenderer(primary, "Image");
            primaryImage.drawMode = SpriteDrawMode.Simple;
            SetPrimarySprite(primarySprite);

            // 크기 조절 핸들 비활성화
            var handles = transform.Find("ResizeHandle");
            if (handles != null) handles.gameObject.SetActive(false);

            ResolveMissingPuzzleObjects();
            ApplySecondarySorting();
            UpdateEscapeLockState();
        }

        private void ResolveMissingPuzzleObjects()
        {
            var secondary = transform.Find("Secondary");
            if (secondary == null) return;

            // Danger 오브젝트 자동 복구
            if (dangerObjects == null || dangerObjects.Count == 0 || (dangerObjects.Count > 0 && dangerObjects[0] == null))
            {
                dangerObjects = new List<PeepPuzzleObject>();
                foreach (var obj in secondary.GetComponentsInChildren<PeepPuzzleObject>(true))
                {
                    if (obj != null && (obj.ObjectType == PeepObjectType.Danger || obj.name.IndexOf("danger", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        dangerObjects.Add(obj);
                    }
                }
            }

            // Watcher 오브젝트 자동 복구
            if (watcherObjects == null || watcherObjects.Count == 0 || (watcherObjects.Count > 0 && watcherObjects[0] == null))
            {
                watcherObjects = new List<PeepPuzzleObject>();
                foreach (var obj in secondary.GetComponentsInChildren<PeepPuzzleObject>(true))
                {
                    if (obj != null && (obj.ObjectType == PeepObjectType.Watcher || obj.name.IndexOf("watcher", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        watcherObjects.Add(obj);
                    }
                }
            }
        }

        public void SetupPuzzleObjects(
            IEnumerable<PeepPuzzleObject> clueList,
            IEnumerable<PeepPuzzleObject> dangerList,
            IEnumerable<PeepPuzzleObject> watcherList,
            PeepPuzzleObject escape)
        {
            clues = clueList != null ? new List<PeepPuzzleObject>(clueList) : new List<PeepPuzzleObject>();
            dangerObjects = dangerList != null ? new List<PeepPuzzleObject>(dangerList) : new List<PeepPuzzleObject>();
            watcherObjects = watcherList != null ? new List<PeepPuzzleObject>(watcherList) : new List<PeepPuzzleObject>();
            escapeObject = escape;

            dangerTimers.Clear();
            watcherTimers.Clear();

            if (clues != null)
            {
                foreach (var clue in clues)
                {
                    if (clue != null)
                    {
                        clue.onClicked.RemoveListener(UpdateEscapeLockState);
                        clue.onClicked.AddListener(UpdateEscapeLockState);
                    }
                }
            }

            ApplySecondarySorting();
            UpdateEscapeLockState();
        }

        public void SetupPuzzleObjects(
            IEnumerable<PeepPuzzleObject> clueList,
            PeepPuzzleObject danger,
            PeepPuzzleObject watcher,
            PeepPuzzleObject escape)
        {
            var dList = danger != null ? new[] { danger } : Array.Empty<PeepPuzzleObject>();
            var wList = watcher != null ? new[] { watcher } : Array.Empty<PeepPuzzleObject>();
            SetupPuzzleObjects(clueList, dList, wList, escape);
        }

        protected override void OnDestroy()
        {
            if (clues != null)
            {
                foreach (var clue in clues)
                {
                    if (clue != null) clue.onClicked.RemoveListener(UpdateEscapeLockState);
                }
            }
            base.OnDestroy();
        }

        public void SetPrimarySprite(Sprite sprite)
        {
            primarySprite = sprite;
            if (primaryImage == null) return;
            primaryImage.sprite = sprite;
            primaryImage.enabled = sprite != null;
            ApplySize(size);
        }

        protected override void ApplySize(Vector2 value)
        {
            base.ApplySize(value);
            if (primaryBackground != null) primaryBackground.size = value;
            if (primaryImage != null && primaryImage.sprite != null)
            {
                var b = primaryImage.sprite.bounds.size;
                var scale = Mathf.Min(value.x / b.x, value.y / b.y);
                primaryImage.transform.localScale = new Vector3(scale, scale, 1);
            }
        }

        public override void ApplySorting(int order)
        {
            base.ApplySorting(order);
            ApplySecondarySorting();
        }

        private void ApplySecondarySorting()
        {
            var secondaryTransform = transform.Find("Secondary");
            if (secondaryTransform == null) return;

            foreach (var r in secondaryTransform.GetComponentsInChildren<Renderer>(true))
            {
                r.sortingLayerName = SecondarySortingLayerName;
            }
        }

        // --- 크기 조절 및 최대화 무효화 (고정 크기 창) ---
        public override ResizeDirection? ResizeAt(Vector2 point) => null;
        public override bool IsPointerOverResizeHandle(Vector2 point) => false;
        public override void Maximize() { }
        public override void ToggleMaximize() { }
        public override void Resize(Vector2 value) { }
        public override void ResizeDelta(Vector2 delta) { }

        private void Update()
        {
            if (isCleared || isGameOver) return;

            // 창 드래그 중(DesktopInputRouter.IsWorldInputBlocked)이어도 WINDOW.exe로 비추는 동안 타이머는 흘러야 함
            ManualTick(Time.deltaTime);

            if (!DesktopInputRouter.IsWorldInputBlocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ProcessMouseClick();
            }
        }

        public void ManualTick(float deltaTime)
        {
            if (isCleared || isGameOver) return;

            var hasViewport = GetPeepViewport(out var peepViewport);

            // 1. 봐서는 안 되는 오브젝트들 판정 (하나라도 3초 이상 노출되면 게임오버)
            var maxDanger = 0f;
            if (dangerObjects != null && dangerObjects.Count > 0)
            {
                for (var i = 0; i < dangerObjects.Count; i++)
                {
                    var danger = dangerObjects[i];
                    if (danger == null || !danger.gameObject.activeInHierarchy) continue;

                    dangerTimers.TryGetValue(danger, out var timer);
                    if (hasViewport && IsObjectInViewport(danger, peepViewport))
                    {
                        timer += deltaTime;
                        dangerTimers[danger] = timer;
                        if (timer > maxDanger) maxDanger = timer;

                        if (timer >= dangerMaxTime)
                        {
                            currentDangerTimer = timer;
                            TriggerGameOver("위험 대상을 너무 오래 보았습니다.");
                            return;
                        }
                    }
                    else
                    {
                        // 시야에서 벗어나면 해당 오브젝트 누적 타이머 즉시 0으로 리셋
                        dangerTimers[danger] = 0f;
                    }
                }
            }
            currentDangerTimer = maxDanger;

            // 2. 주기적으로 봐야 하는 오브젝트들 판정 (하나라도 10초 이상 미관측되면 게임오버)
            var maxWatcher = 0f;
            if (watcherObjects != null && watcherObjects.Count > 0)
            {
                for (var i = 0; i < watcherObjects.Count; i++)
                {
                    var watcher = watcherObjects[i];
                    if (watcher == null || !watcher.gameObject.activeInHierarchy) continue;

                    watcherTimers.TryGetValue(watcher, out var timer);
                    if (hasViewport && IsObjectInViewport(watcher, peepViewport))
                    {
                        // 시야에 들어와 있으면 타이머 0으로 리셋 유지
                        timer = 0f;
                        watcherTimers[watcher] = 0f;
                    }
                    else
                    {
                        // 윈도우를 통해 보지 못하고 있는 시간 누적
                        timer += deltaTime;
                        watcherTimers[watcher] = timer;
                        if (timer > maxWatcher) maxWatcher = timer;
                        if (timer >= watcherMaxUnseenTime)
                        {
                            currentWatcherTimer = timer;
                            TriggerGameOver("감시 대상을 놓쳤습니다.");
                            return;
                        }
                    }
                }
            }
            currentWatcherTimer = maxWatcher;
        }

        private void ProcessMouseClick()
        {
            if (!GetPeepViewport(out var peepViewport)) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 mouseWorld;

            if (DesktopWindowSpace.Instance != null && DesktopWindowSpace.Instance.Valid)
            {
                if (!DesktopWindowSpace.Instance.TryWorld(mouseScreen, out mouseWorld)) return;
            }
            else
            {
                var cam = Camera.main;
                if (cam == null) return;
                var worldPoint3 = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
                mouseWorld = new Vector2(worldPoint3.x, worldPoint3.y);
            }

            if (!peepViewport.Contains(mouseWorld)) return;

            HandlePeepClick(mouseWorld);
        }

        public bool HandlePeepClick(Vector2 worldPoint)
        {
            if (isCleared || isGameOver) return false;
            if (!GetPeepViewport(out var viewport) || !viewport.Contains(worldPoint)) return false;

            // 1. 단서 클릭 확인
            if (clues != null)
            {
                for (var i = 0; i < clues.Count; i++)
                {
                    var clue = clues[i];
                    if (clue != null && !clue.IsCollected && clue.ContainsPoint(worldPoint))
                    {
                        if (clue.Click())
                        {
                            PlaySound(clue.GetClickSound() != null ? clue.GetClickSound() : clueCollectedSound);
                            UpdateEscapeLockState();
                            return true;
                        }
                    }
                }
            }

            // 2. 탈출 오브젝트 클릭 확인
            if (escapeObject != null && escapeObject.IsUnlocked && escapeObject.ContainsPoint(worldPoint))
            {
                if (escapeObject.Click())
                {
                    PlaySound(escapeObject.GetClickSound() != null ? escapeObject.GetClickSound() : puzzleClearedSound);
                    OnPuzzleCleared();
                    return true;
                }
            }

            return false;
        }

        public void UpdateEscapeLockState()
        {
            if (escapeObject != null)
            {
                escapeObject.SetUnlocked(AreAllCluesCollected);
            }
            else
            {
                var secondaryTransform = transform.Find("Secondary");
                var unusedEscape = secondaryTransform != null ? secondaryTransform.Find("Escape") : null;
                if (unusedEscape != null && unusedEscape.gameObject.activeSelf)
                {
                    unusedEscape.gameObject.SetActive(false);
                }

                if (!isCleared && !isGameOver && clues != null && clues.Count > 0 && AreAllCluesCollected)
                {
                    isCleared = true;
                    onPuzzleCleared?.Invoke();
                    OnCleared?.Invoke();
                }
            }
        }

        public bool GetPeepViewport(out Rect viewport)
        {
            if (OverridePeepViewport.HasValue)
            {
                viewport = OverridePeepViewport.Value;
                return true;
            }

            viewport = default;
            var wm = Manager != null ? Manager : WindowManager.Instance;
            Window targetWindow = null;

            if (wm != null)
            {
                // 1순위: 지정된 targetWindowId로 프로세스 검색
                if (!string.IsNullOrEmpty(targetWindowId))
                {
                    var proc = wm.GetProcess(targetWindowId);
                    if (proc != null && !ReferenceEquals(proc.Instance, this) && proc.State != WindowState.Minimized && proc.Instance != null && proc.Instance.gameObject.activeInHierarchy)
                    {
                        targetWindow = proc.Instance;
                    }
                }

                // 2순위: "window", "WINDOW.exe" 프로세스 또는 WindowExeWindow 인스턴스 검색
                if (targetWindow == null)
                {
                    foreach (var proc in wm.ActiveProcesses)
                    {
                        if (proc == null || ReferenceEquals(proc.Instance, this) || proc.State == WindowState.Minimized || proc.Instance == null || !proc.Instance.gameObject.activeInHierarchy)
                            continue;

                        if (string.Equals(proc.Id, "window", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(proc.Id, "WINDOW.exe", StringComparison.OrdinalIgnoreCase) ||
                            proc.Instance is WindowExeWindow)
                        {
                            targetWindow = proc.Instance;
                            break;
                        }
                    }
                }
            }

            // 3순위: 씬 전체에서 WindowExeWindow 탐색 (Fallback)
            if (targetWindow == null)
            {
                var exeWindows = UnityEngine.Object.FindObjectsByType<WindowExeWindow>(FindObjectsSortMode.None);
                for (var i = 0; i < exeWindows.Length; i++)
                {
                    var candidate = exeWindows[i];
                    if (candidate != null && !ReferenceEquals(candidate, this) && candidate.gameObject.activeInHierarchy)
                    {
                        targetWindow = candidate;
                        break;
                    }
                }
            }

            if (targetWindow == null) return false;

            viewport = GetWindowWorldRect(targetWindow);
            return true;
        }

        private static Rect GetWindowWorldRect(Window window)
        {
            if (window == null) return default;
            Rect local = Rect.MinMaxRect(
                -window.Size.x * 0.5f,
                -window.Size.y * 0.5f,
                window.Size.x * 0.5f,
                window.Size.y * 0.5f + window.BarHeight
            );
            return TransformRectToWorld(local, window.transform);
        }

        private static Rect TransformRectToWorld(Rect rect, Transform t)
        {
            if (t == null) return rect;
            Vector3 a = t.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0f));
            Vector3 b = t.TransformPoint(new Vector3(rect.xMax, rect.yMin, 0f));
            Vector3 c = t.TransformPoint(new Vector3(rect.xMax, rect.yMax, 0f));
            Vector3 d = t.TransformPoint(new Vector3(rect.xMin, rect.yMax, 0f));

            float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static bool IsObjectInViewport(PeepPuzzleObject obj, Rect viewport)
        {
            if (obj == null) return false;
            // 1. 오브젝트의 월드 위치(중심)가 뷰포트 안에 들어있는 경우
            if (viewport.Contains(obj.transform.position)) return true;

            // 2. 오브젝트의 월드 바운즈와 뷰포트가 겹치는 경우
            var bounds = obj.WorldBounds;
            var objRect = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
            return viewport.Overlaps(objRect);
        }

        private void OnPuzzleCleared()
        {
            if (isCleared || isGameOver) return;
            isCleared = true;

            onPuzzleCleared?.Invoke();
            OnCleared?.Invoke();

            // 클리어 후 창 자동 닫기
            RequestClose();
        }

        private void TriggerGameOver(string reason)
        {
            if (isCleared || isGameOver) return;
            isGameOver = true;

            Debug.LogWarning($"<color=red><b>[GAME OVER]</b></color> {reason}");
            PlaySound(gameOverSound);

            onGameOver?.Invoke(reason);
            OnGameOverTriggered?.Invoke(reason);

            if (loadGameOverScene && Application.isPlaying && !string.IsNullOrEmpty(gameOverSceneName))
            {
                try
                {
                    SceneManager.LoadScene(gameOverSceneName);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PeepPuzzleWindow] Failed to load game over scene '{gameOverSceneName}': {e.Message}. Trying fallback 'Assets/Scenes/YouAreDie.unity'...");
                    SceneManager.LoadScene("Assets/Scenes/YouAreDie.unity");
                }
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        public void ResetPuzzle()
        {
            dangerTimers.Clear();
            watcherTimers.Clear();
            currentDangerTimer = 0f;
            currentWatcherTimer = 0f;
            isCleared = false;
            isGameOver = false;

            if (clues != null)
            {
                for (var i = 0; i < clues.Count; i++) clues[i]?.ResetState();
            }
            if (dangerObjects != null)
            {
                for (var i = 0; i < dangerObjects.Count; i++) dangerObjects[i]?.ResetState();
            }
            if (watcherObjects != null)
            {
                for (var i = 0; i < watcherObjects.Count; i++) watcherObjects[i]?.ResetState();
            }
            escapeObject?.ResetState();
            UpdateEscapeLockState();
        }

        private static SpriteRenderer GetOrAddRenderer(Transform parent, string name)
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
    }
}
