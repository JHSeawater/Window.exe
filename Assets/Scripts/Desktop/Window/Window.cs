using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    public enum WindowInteraction
    {
        None,
        Moving,
        Resizing
    }

    /// <summary>
    /// 순수 2D(SpriteRenderer) 기반 가상 데스크톱 창 부모 클래스
    /// Canvas 없이 2D 월드에서 위치 이동, 크기 조절, 상단 메뉴 바(드래그 이동), 우하단 리사이즈 핸들을 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class Window : MonoBehaviour
    {
        public const string PrimarySortingLayerName = "WindowFront";
        public const string SecondarySortingLayerName = "WindowBack";
        public const int DefaultSortingOrder = 0;

        [Header("Window Title")]
        [SerializeField] private string windowTitle = "창";

        [Header("Secondary (Back Clue)")]
        [SerializeField] private Sprite secondarySprite;

        [Header("Window Size")]
        [SerializeField] protected Vector2 size = new Vector2(4f, 3f);
        [SerializeField] protected Vector2 minSize = new Vector2(1.5f, 1.2f);
        [SerializeField] protected Vector2 maxSize = new Vector2(20f, 20f);

        // 내부 컴포넌트 (자동 생성 및 탐색)
        private SpriteRenderer _secondaryRenderer;
        private WindowTitleBar _titleBar;
        private WindowResizeHandle _resizeHandle;

        private static Sprite _fallbackTransparentSprite;

        // 최대화 이전 위치/크기 기억
        private Vector2 _preMaximizePos;
        private Vector2 _preMaximizeSize;

        public Vector2 Size => size;
        public Vector2 MinSize => minSize;
        public Vector2 MaxSize => maxSize;
        public string WindowTitle => windowTitle;
        public string WindowId { get; set; }
        public bool IsMaximized { get; private set; }
        public WindowInteraction CurrentInteraction { get; internal set; } = WindowInteraction.None;

        /// <summary>
        /// 닫기 요청 시 발생하는 이벤트 (위험한 X 등 게임 규칙 가로채기 지원)
        /// </summary>
        public event System.Action<Window> OnCloseRequested;

        /// <summary>
        /// 마우스 위치가 리사이즈 핸들 영역 위에 있는지 여부 (모서리 판정 우선권 제공)
        /// </summary>
        public bool IsPointerOverResizeHandle(Vector2 mouseWorldPos)
        {
            if (_resizeHandle == null) return false;
            return _resizeHandle.IsPointerOverAnyHandle(mouseWorldPos);
        }

        /// <summary>
        /// 닫기 요청 (X 버튼 클릭 시 호출)
        /// </summary>
        public virtual void RequestClose()
        {
            if (OnCloseRequested != null)
            {
                OnCloseRequested.Invoke(this);
                return;
            }

            if (!string.IsNullOrEmpty(WindowId) && WindowManager.Instance != null)
            {
                WindowManager.Instance.Close(WindowId);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 최소화 요청 (_ 버튼 클릭 시 호출)
        /// </summary>
        public virtual void RequestMinimize()
        {
            if (!string.IsNullOrEmpty(WindowId) && WindowManager.Instance != null)
            {
                WindowManager.Instance.Minimize(WindowId);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 최대화 / 복원 토글 요청 (□ 버튼 클릭 시 호출)
        /// </summary>
        public virtual void RequestMaximize()
        {
            if (!string.IsNullOrEmpty(WindowId) && WindowManager.Instance != null)
            {
                WindowManager.Instance.ToggleMaximize(WindowId);
            }
            else
            {
                ToggleMaximize();
            }
        }

        /// <summary>
        /// 창 최대화: 카메라 화면 전체 크기에 맞춤
        /// </summary>
        public virtual void Maximize()
        {
            if (IsMaximized) return;

            _preMaximizePos = transform.localPosition;
            _preMaximizeSize = size;

            Camera cam = Camera.main;
            if (cam != null)
            {
                float camH = cam.orthographicSize * 2f;
                float camW = camH * cam.aspect;
                float barH = _titleBar != null ? _titleBar.BarHeight : 0.5f;

                Vector3 camPos = cam.transform.position;
                MoveTo(new Vector2(camPos.x, camPos.y - barH * 0.5f));
                Resize(new Vector2(camW, camH - barH));
            }

            IsMaximized = true;
        }

        /// <summary>
        /// 창 최대화 해제: 이전 위치와 크기로 복원
        /// </summary>
        public virtual void Restore()
        {
            if (!IsMaximized) return;

            MoveTo(_preMaximizePos);
            Resize(_preMaximizeSize);

            IsMaximized = false;
        }

        /// <summary>
        /// 최대화 / 복원 토글
        /// </summary>
        public virtual void ToggleMaximize()
        {
            if (IsMaximized) Restore();
            else Maximize();
        }

        protected virtual void Awake()
        {
            SetupSecondaryRenderer();
            SetupTitleBar();
            SetupResizeHandle();

            ApplySize(size);
            SetSecondarySprite(secondarySprite);
            InitializePrimary();
        }

        /// <summary>
        /// 위치 이동: 지정된 로컬 좌표로 이동
        /// </summary>
        public virtual void MoveTo(Vector2 position)
        {
            Vector3 currentPos = transform.localPosition;
            transform.localPosition = new Vector3(position.x, position.y, currentPos.z);
        }

        /// <summary>
        /// 위치 이동: 상대적 델타 이동 (드래그 시 사용)
        /// </summary>
        public virtual void Translate(Vector2 delta)
        {
            transform.localPosition += new Vector3(delta.x, delta.y, 0f);
        }

        /// <summary>
        /// 크기 조절: 목표 크기로 설정 (최소/최대 크기 클램핑)
        /// </summary>
        public virtual void Resize(Vector2 newSize)
        {
            size = new Vector2(
                Mathf.Clamp(newSize.x, minSize.x, maxSize.x),
                Mathf.Clamp(newSize.y, minSize.y, maxSize.y)
            );

            ApplySize(size);
        }

        /// <summary>
        /// 크기 조절: 상대적 델타 크기 변경
        /// </summary>
        public virtual void ResizeDelta(Vector2 deltaSize)
        {
            Resize(size + deltaSize);
        }

        /// <summary>
        /// 창 제목 텍스트 변경
        /// </summary>
        public void SetWindowTitle(string title)
        {
            windowTitle = title;
            if (_titleBar != null)
            {
                _titleBar.SetTitle(title);
            }
        }

        /// <summary>
        /// Secondary(WindowBack)에 스프라이트 주입
        /// null일 경우 에러 출력 후 1x1 투명 스프라이트를 fallback으로 적용합니다.
        /// </summary>
        public void SetSecondarySprite(Sprite sprite)
        {
            if (_secondaryRenderer == null)
            {
                SetupSecondaryRenderer();
            }

            if (sprite == null)
            {
                Debug.LogError($"[Window] '{gameObject.name}': Secondary 스프라이트가 null입니다! 투명 스프라이트로 대체합니다.", this);
                _secondaryRenderer.sprite = GetFallbackTransparentSprite();
                return;
            }

            secondarySprite = sprite;
            _secondaryRenderer.sprite = sprite;
        }

        /// <summary>
        /// 창 크기가 변경될 때 Secondary, 상단 메뉴 바, 우하단 리사이즈 핸들에 크기를 적용하는 메서드
        /// </summary>
        protected virtual void ApplySize(Vector2 newSize)
        {
            if (_secondaryRenderer != null)
            {
                _secondaryRenderer.drawMode = SpriteDrawMode.Sliced;
                _secondaryRenderer.size = newSize;
            }

            if (_titleBar != null)
            {
                _titleBar.UpdateLayout(newSize);
            }

            if (_resizeHandle != null)
            {
                _resizeHandle.UpdateLayout(newSize);
            }
        }

        /// <summary>
        /// Secondary SpriteRenderer 자동 탐색 또는 자동 생성
        /// </summary>
        protected void SetupSecondaryRenderer()
        {
            if (_secondaryRenderer != null) return;

            Transform secondaryTransform = transform.Find("Secondary");
            if (secondaryTransform == null)
            {
                var secondaryGo = new GameObject("Secondary");
                secondaryGo.transform.SetParent(transform, false);
                secondaryGo.transform.localPosition = Vector3.zero;
                secondaryGo.transform.localScale = Vector3.one;
                secondaryTransform = secondaryGo.transform;
            }

            _secondaryRenderer = secondaryTransform.GetComponent<SpriteRenderer>();
            if (_secondaryRenderer == null)
            {
                _secondaryRenderer = secondaryTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            _secondaryRenderer.sortingLayerName = SecondarySortingLayerName;
            _secondaryRenderer.sortingOrder = DefaultSortingOrder;
            _secondaryRenderer.drawMode = SpriteDrawMode.Sliced;
            _secondaryRenderer.size = size;
        }

        /// <summary>
        /// 상단 메뉴 바(타이틀 바) 자동 탐색 또는 자동 생성
        /// </summary>
        protected void SetupTitleBar()
        {
            if (_titleBar != null) return;

            Transform barTransform = transform.Find("TitleBar");
            if (barTransform == null)
            {
                var barGo = new GameObject("TitleBar");
                barGo.transform.SetParent(transform, false);
                barTransform = barGo.transform;
            }

            _titleBar = barTransform.GetComponent<WindowTitleBar>();
            if (_titleBar == null)
            {
                _titleBar = barTransform.gameObject.AddComponent<WindowTitleBar>();
            }

            _titleBar.SetTitle(string.IsNullOrEmpty(windowTitle) ? gameObject.name : windowTitle);
        }

        /// <summary>
        /// 우하단 리사이즈 핸들 자동 탐색 또는 자동 생성
        /// </summary>
        protected void SetupResizeHandle()
        {
            if (_resizeHandle != null) return;

            Transform handleTransform = transform.Find("ResizeHandle");
            if (handleTransform == null)
            {
                var handleGo = new GameObject("ResizeHandle");
                handleGo.transform.SetParent(transform, false);
                handleTransform = handleGo.transform;
            }

            _resizeHandle = handleTransform.GetComponent<WindowResizeHandle>();
            if (_resizeHandle == null)
            {
                _resizeHandle = handleTransform.gameObject.AddComponent<WindowResizeHandle>();
            }
        }

        protected static Sprite GetFallbackTransparentSprite()
        {
            if (_fallbackTransparentSprite != null) return _fallbackTransparentSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Transparent_Fallback_Texture"
            };
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();

            _fallbackTransparentSprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                100f
            );
            _fallbackTransparentSprite.name = "Transparent_Fallback_Sprite";
            return _fallbackTransparentSprite;
        }

        /// <summary>
        /// 파생 클래스에서 Primary(WindowFront) 영역을 초기화하는 훅
        /// </summary>
        protected abstract void InitializePrimary();

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            SetupSecondaryRenderer();
            SetupTitleBar();
            SetupResizeHandle();

            ApplySize(size);
            if (secondarySprite != null && _secondaryRenderer != null)
            {
                _secondaryRenderer.sprite = secondarySprite;
            }

            if (_titleBar != null)
            {
                _titleBar.SetTitle(string.IsNullOrEmpty(windowTitle) ? gameObject.name : windowTitle);
            }
        }

        protected virtual void Reset()
        {
            SetupSecondaryRenderer();
            SetupTitleBar();
            SetupResizeHandle();
            ApplySize(size);
        }
#endif
    }
}
