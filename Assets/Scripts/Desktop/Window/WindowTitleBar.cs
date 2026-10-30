using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>
    /// 순수 2D 기반 창 상단 메뉴 바 (타이틀 바)
    /// 드래그를 통해 창 전체를 이동시키며, 창 제목과 더미 버튼을 표시합니다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class WindowTitleBar : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private float barHeight = 0.5f;
        [SerializeField] private Color barColor = new Color(0.12f, 0.25f, 0.55f, 1f); // 윈도우풍 네이비 블루
        [SerializeField] private Color textColor = Color.white;

        [Header("Title Text Layout")]
        [SerializeField] private float titlePosX = -0.6f;
        [SerializeField] private float titleLeftMargin = 0.1f;

        private Window _window;
        private BoxCollider2D _collider;
        private SpriteRenderer _backgroundRenderer;
        private TextMeshPro _titleText;
        private TextMeshPro _buttonsText;

        // 드래그 상태
        private bool _isDragging;
        private Vector2 _dragStartMousePos;
        private Vector3 _dragStartWindowPos;

        public float BarHeight => barHeight;

        private void Awake()
        {
            _window = GetComponentInParent<Window>();
            _collider = GetComponent<BoxCollider2D>();
            SetupComponents();
        }

        private void Update()
        {
            HandleDrag();
        }

        /// <summary>
        /// 창 크기에 맞춰 메뉴 바의 크기와 위치를 상단으로 갱신합니다.
        /// </summary>
        public void UpdateLayout(Vector2 windowSize)
        {
            if (_window == null) _window = GetComponentInParent<Window>();
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            SetupComponents();

            // 메뉴 바를 창 본체 바로 상단에 배치
            float posY = (windowSize.y * 0.5f) + (barHeight * 0.5f);
            transform.localPosition = new Vector3(0f, posY, 0f);

            // 콜라이더 및 배경 크기 설정
            Vector2 barSize = new Vector2(windowSize.x, barHeight);
            _collider.size = barSize;

            if (_backgroundRenderer != null)
            {
                _backgroundRenderer.drawMode = SpriteDrawMode.Sliced;
                _backgroundRenderer.size = barSize;
            }

            // 제목 텍스트 위치 및 마진
            if (_titleText != null)
            {
                _titleText.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.5f, windowSize.x - 1.2f), barHeight);
                _titleText.rectTransform.localPosition = new Vector3(titlePosX, 0f, 0f);
                _titleText.margin = new Vector4(titleLeftMargin, _titleText.margin.y, _titleText.margin.z, _titleText.margin.w);
            }

            // 우측 버튼 위치 (우측 정렬)
            if (_buttonsText != null)
            {
                _buttonsText.rectTransform.sizeDelta = new Vector2(1.2f, barHeight);
                _buttonsText.rectTransform.localPosition = new Vector3(windowSize.x * 0.5f - 0.65f, 0f, 0f);
            }
        }

        public void SetTitle(string title)
        {
            if (_titleText == null) SetupComponents();
            if (_titleText != null) _titleText.text = title;
        }

        private void SetupComponents()
        {
            // 1. 배경 SpriteRenderer
            if (_backgroundRenderer == null)
            {
                _backgroundRenderer = GetComponent<SpriteRenderer>();
                if (_backgroundRenderer == null)
                {
                    _backgroundRenderer = gameObject.AddComponent<SpriteRenderer>();
                }

                _backgroundRenderer.sprite = GetDefaultBarSprite();
                _backgroundRenderer.color = barColor;
                _backgroundRenderer.sortingLayerName = Window.PrimarySortingLayerName;
                _backgroundRenderer.sortingOrder = 10; // 본체보다 위에 표시
            }

            // 2. 제목 텍스트
            if (_titleText == null)
            {
                Transform textT = transform.Find("TitleText");
                if (textT == null)
                {
                    var go = new GameObject("TitleText");
                    go.transform.SetParent(transform, false);
                    textT = go.transform;
                }

                _titleText = textT.GetComponent<TextMeshPro>();
                if (_titleText == null) _titleText = textT.gameObject.AddComponent<TextMeshPro>();

                _titleText.sortingLayerID = SortingLayer.NameToID(Window.PrimarySortingLayerName);
                _titleText.sortingOrder = 11;
                _titleText.fontSize = 2.5f;
                _titleText.color = textColor;
                _titleText.alignment = TextAlignmentOptions.MidlineLeft;
                _titleText.margin = new Vector4(titleLeftMargin, 0f, 0f, 0f);
                _titleText.rectTransform.localPosition = new Vector3(titlePosX, 0f, 0f);
                _titleText.text = _window != null ? _window.gameObject.name : "WINDOW.exe";
            }

            // 3. 우측 더미 버튼 (- □ X)
            if (_buttonsText == null)
            {
                Transform btnT = transform.Find("ButtonsText");
                if (btnT == null)
                {
                    var go = new GameObject("ButtonsText");
                    go.transform.SetParent(transform, false);
                    btnT = go.transform;
                }

                _buttonsText = btnT.GetComponent<TextMeshPro>();
                if (_buttonsText == null) _buttonsText = btnT.gameObject.AddComponent<TextMeshPro>();

                _buttonsText.sortingLayerID = SortingLayer.NameToID(Window.PrimarySortingLayerName);
                _buttonsText.sortingOrder = 11;
                _buttonsText.fontSize = 2.2f;
                _buttonsText.color = textColor;
                _buttonsText.alignment = TextAlignmentOptions.MidlineRight;
                _buttonsText.text = " _  □  ✕ ";
            }
        }

        private void HandleDrag()
        {
            Vector2 mousePos = GetMouseWorldPosition();
            bool isDown = WasMouseButtonPressedThisFrame();
            bool isPressed = IsMouseButtonPressed();

            if (isDown)
            {
                // 모서리/리사이즈 판정이 최우선: 마우스가 리사이즈 핸들 위에 있거나 리사이즈 중이면 메뉴바 드래그 무시
                if (_window != null && (_window.CurrentInteraction == WindowInteraction.Resizing || _window.IsPointerOverResizeHandle(mousePos)))
                {
                    return;
                }

                if (_collider != null && _collider.OverlapPoint(mousePos))
                {
                    // 창 포커스 처리 (맨 앞으로 가져오기)
                    if (_window != null && !string.IsNullOrEmpty(_window.WindowId) && WindowManager.Instance != null)
                    {
                        WindowManager.Instance.FocusWindow(_window.WindowId);
                    }

                    // 우측 3개 버튼 클릭 판정 (_ □ ✕)
                    if (CheckButtonsClick(mousePos))
                    {
                        return;
                    }

                    _isDragging = true;
                    if (_window != null) _window.CurrentInteraction = WindowInteraction.Moving;
                    _dragStartMousePos = mousePos;
                    if (_window != null)
                    {
                        _dragStartWindowPos = _window.transform.localPosition;
                    }
                }
            }
            else if (_isDragging)
            {
                if (isPressed && _window != null)
                {
                    Vector2 delta = mousePos - _dragStartMousePos;
                    _window.MoveTo((Vector2)_dragStartWindowPos + delta);
                }
                else
                {
                    _isDragging = false;
                    if (_window != null && _window.CurrentInteraction == WindowInteraction.Moving)
                    {
                        _window.CurrentInteraction = WindowInteraction.None;
                    }
                }
            }
        }

        private bool CheckButtonsClick(Vector2 mouseWorldPos)
        {
            if (_buttonsText == null || _window == null) return false;

            Vector3 localMouse = transform.InverseTransformPoint(mouseWorldPos);
            float btnWidth = 1.2f;
            float btnCenterX = (_window.Size.x * 0.5f) - (btnWidth * 0.5f) - 0.05f;

            float minX = btnCenterX - (btnWidth * 0.5f);
            float maxX = btnCenterX + (btnWidth * 0.5f);

            if (localMouse.x >= minX && localMouse.x <= maxX && Mathf.Abs(localMouse.y) <= barHeight * 0.5f)
            {
                float relativeX = (localMouse.x - minX) / btnWidth;

                if (relativeX < 0.33f)
                {
                    // 최소화 (_)
                    _window.RequestMinimize();
                }
                else if (relativeX < 0.66f)
                {
                    // 최대화 / 복원 (□)
                    _window.RequestMaximize();
                }
                else
                {
                    // 닫기 (✕)
                    _window.RequestClose();
                }
                return true;
            }

            return false;
        }

        private static Vector2 GetMouseWorldPosition()
        {
            Vector2 screenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue();
            }
#else
            screenPos = Input.mousePosition;
#endif
            Camera cam = Camera.main;
            if (cam == null) return Vector2.zero;
            return cam.ScreenToWorldPoint(screenPos);
        }

        private static bool IsMouseButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }

        private static bool WasMouseButtonPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static Sprite _defaultBarSprite;
        private static Sprite GetDefaultBarSprite()
        {
            if (_defaultBarSprite != null) return _defaultBarSprite;

            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _defaultBarSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            return _defaultBarSprite;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SetupComponents();
            if (_window == null) _window = GetComponentInParent<Window>();
            if (_window != null)
            {
                UpdateLayout(_window.Size);
            }
        }
#endif
    }
}

