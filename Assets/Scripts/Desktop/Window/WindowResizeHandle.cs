using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    public enum ResizeDirection
    {
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// 창 테두리 및 4개 꼭짓점(총 8방향) 리사이즈를 총괄하는 컨트롤러
    /// 실제 OS 창처럼 가장자리나 꼭짓점을 마우스로 잡고 끌면 반대편이 고정된 채 자연스럽게 크기가 조절됩니다.
    /// </summary>
    public class WindowResizeHandle : MonoBehaviour
    {
        [Header("Border Settings")]
        [SerializeField] private float borderThickness = 0.25f; // 가장자리 감지 두께
        [SerializeField] private float cornerSize = 0.45f;      // 꼭짓점 감지 크기

        private Window _window;
        private WindowTitleBar _titleBar;

        // 8개 개별 핸들
        private SingleResizeHandle[] _handles;

        private void Awake()
        {
            _window = GetComponentInParent<Window>();
            _titleBar = _window != null ? _window.GetComponentInChildren<WindowTitleBar>(true) : null;
            CreateHandlesIfNeeded();
        }

        /// <summary>
        /// 창 크기와 메뉴 바 높이에 맞춰 8개 테두리/꼭짓점 핸들의 위치와 콜라이더 크기를 재배치합니다.
        /// </summary>
        public void UpdateLayout(Vector2 windowSize)
        {
            if (_window == null) _window = GetComponentInParent<Window>();
            if (_titleBar == null && _window != null) _titleBar = _window.GetComponentInChildren<WindowTitleBar>(true);

            CreateHandlesIfNeeded();

            float barHeight = _titleBar != null ? _titleBar.BarHeight : 0f;
            float totalH = windowSize.y + barHeight;

            float leftX = -windowSize.x * 0.5f;
            float rightX = windowSize.x * 0.5f;
            float bottomY = -windowSize.y * 0.5f;
            float topY = windowSize.y * 0.5f + barHeight;
            float centerY = (bottomY + topY) * 0.5f;

            float edgeWidth = Mathf.Max(0.1f, windowSize.x - cornerSize * 2f);
            float edgeHeight = Mathf.Max(0.1f, totalH - cornerSize * 2f);

            foreach (var handle in _handles)
            {
                if (handle == null) continue;

                Vector3 pos = Vector3.zero;
                Vector2 colSize = Vector2.zero;

                switch (handle.Direction)
                {
                    case ResizeDirection.Left:
                        pos = new Vector3(leftX, centerY, 0f);
                        colSize = new Vector2(borderThickness, edgeHeight);
                        break;
                    case ResizeDirection.Right:
                        pos = new Vector3(rightX, centerY, 0f);
                        colSize = new Vector2(borderThickness, edgeHeight);
                        break;
                    case ResizeDirection.Top:
                        pos = new Vector3(0f, topY, 0f);
                        colSize = new Vector2(edgeWidth, borderThickness);
                        break;
                    case ResizeDirection.Bottom:
                        pos = new Vector3(0f, bottomY, 0f);
                        colSize = new Vector2(edgeWidth, borderThickness);
                        break;
                    case ResizeDirection.TopLeft:
                        pos = new Vector3(leftX, topY, 0f);
                        colSize = new Vector2(cornerSize, cornerSize);
                        break;
                    case ResizeDirection.TopRight:
                        pos = new Vector3(rightX, topY, 0f);
                        colSize = new Vector2(cornerSize, cornerSize);
                        break;
                    case ResizeDirection.BottomLeft:
                        pos = new Vector3(leftX, bottomY, 0f);
                        colSize = new Vector2(cornerSize, cornerSize);
                        break;
                    case ResizeDirection.BottomRight:
                        pos = new Vector3(rightX, bottomY, 0f);
                        colSize = new Vector2(cornerSize, cornerSize);
                        break;
                }

                handle.transform.localPosition = pos;
                handle.Collider.size = colSize;
            }
        }

        private void CreateHandlesIfNeeded()
        {
            if (_handles != null && _handles.Length == 8) return;

            _handles = new SingleResizeHandle[8];
            var directions = (ResizeDirection[])System.Enum.GetValues(typeof(ResizeDirection));

            for (int i = 0; i < directions.Length; i++)
            {
                var dir = directions[i];
                string handleName = $"Handle_{dir}";
                Transform childT = transform.Find(handleName);

                if (childT == null)
                {
                    var go = new GameObject(handleName);
                    go.transform.SetParent(transform, false);
                    childT = go.transform;
                }

                var handle = childT.GetComponent<SingleResizeHandle>();
                if (handle == null)
                {
                    handle = childT.gameObject.AddComponent<SingleResizeHandle>();
                }

                handle.Init(_window, this, dir);
                _handles[i] = handle;
            }
        }

        public float GetBarHeight()
        {
            if (_titleBar == null && _window != null)
            {
                _titleBar = _window.GetComponentInChildren<WindowTitleBar>(true);
            }
            return _titleBar != null ? _titleBar.BarHeight : 0f;
        }

        /// <summary>
        /// 마우스 위치가 8개 리사이즈 핸들 중 어느 하나에라도 겹쳐 있는지 검사
        /// </summary>
        public bool IsPointerOverAnyHandle(Vector2 mouseWorldPos)
        {
            CreateHandlesIfNeeded();
            if (_handles == null) return false;

            for (int i = 0; i < _handles.Length; i++)
            {
                if (_handles[i] != null && _handles[i].Collider != null && _handles[i].Collider.OverlapPoint(mouseWorldPos))
                {
                    return true;
                }
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_window == null) _window = GetComponentInParent<Window>();
            if (_window != null)
            {
                UpdateLayout(_window.Size);
            }
        }
#endif
    }

    /// <summary>
    /// 8방향 각각의 개별 리사이즈 핸들러
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class SingleResizeHandle : MonoBehaviour
    {
        [SerializeField] private ResizeDirection direction;

        private Window _window;
        private WindowResizeHandle _parentController;
        private BoxCollider2D _collider;

        private bool _isResizing;
        private Vector2 _startMousePos;
        private Vector2 _startSize;
        private Vector3 _startWindowPos;

        public ResizeDirection Direction => direction;
        public BoxCollider2D Collider => _collider != null ? _collider : (_collider = GetComponent<BoxCollider2D>());

        public void Init(Window window, WindowResizeHandle controller, ResizeDirection dir)
        {
            _window = window;
            _parentController = controller;
            direction = dir;
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
        }

        private void Awake()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            if (_window == null) _window = GetComponentInParent<Window>();
            if (_parentController == null) _parentController = GetComponentInParent<WindowResizeHandle>();
        }

        private void Update()
        {
            HandleResizeInput();
        }

        private void HandleResizeInput()
        {
            Vector2 mousePos = GetMouseWorldPosition();
            bool isDown = WasMouseButtonPressedThisFrame();
            bool isPressed = IsMouseButtonPressed();

            if (isDown)
            {
                // 메뉴바 드래그 중이면 리사이즈 시작 방지
                if (_window != null && _window.CurrentInteraction == WindowInteraction.Moving)
                {
                    return;
                }

                if (Collider != null && Collider.OverlapPoint(mousePos))
                {
                    _isResizing = true;
                    if (_window != null) _window.CurrentInteraction = WindowInteraction.Resizing;
                    _startMousePos = mousePos;
                    if (_window != null)
                    {
                        _startSize = _window.Size;
                        _startWindowPos = _window.transform.localPosition;
                    }
                }
            }
            else if (_isResizing)
            {
                if (isPressed && _window != null && _parentController != null)
                {
                    Vector2 delta = mousePos - _startMousePos;
                    Apply8WayResize(delta);
                }
                else
                {
                    _isResizing = false;
                    if (_window != null && _window.CurrentInteraction == WindowInteraction.Resizing)
                    {
                        _window.CurrentInteraction = WindowInteraction.None;
                    }
                }
            }
        }

        private void Apply8WayResize(Vector2 delta)
        {
            float barHeight = _parentController.GetBarHeight();

            // 시작 당시의 외곽 경계
            float startL = _startWindowPos.x - _startSize.x * 0.5f;
            float startR = _startWindowPos.x + _startSize.x * 0.5f;
            float startB = _startWindowPos.y - _startSize.y * 0.5f;
            float startT = _startWindowPos.y + _startSize.y * 0.5f + barHeight;

            float newL = startL;
            float newR = startR;
            float newB = startB;
            float newT = startT;

            // 1. 방향별 마우스 델타 반영
            switch (direction)
            {
                case ResizeDirection.Left:
                    newL = startL + delta.x;
                    break;
                case ResizeDirection.Right:
                    newR = startR + delta.x;
                    break;
                case ResizeDirection.Top:
                    newT = startT + delta.y;
                    break;
                case ResizeDirection.Bottom:
                    newB = startB + delta.y;
                    break;
                case ResizeDirection.TopLeft:
                    newL = startL + delta.x;
                    newT = startT + delta.y;
                    break;
                case ResizeDirection.TopRight:
                    newR = startR + delta.x;
                    newT = startT + delta.y;
                    break;
                case ResizeDirection.BottomLeft:
                    newL = startL + delta.x;
                    newB = startB + delta.y;
                    break;
                case ResizeDirection.BottomRight:
                    newR = startR + delta.x;
                    newB = startB + delta.y;
                    break;
            }

            // 2. 가로 크기 클램핑
            float minW = _window.MinSize.x;
            float maxW = _window.MaxSize.x;
            float currentW = newR - newL;

            if (direction == ResizeDirection.Left || direction == ResizeDirection.TopLeft || direction == ResizeDirection.BottomLeft)
            {
                // 좌측을 잡고 드래그 -> 우측(newR) 고정
                if (currentW < minW) newL = newR - minW;
                else if (currentW > maxW) newL = newR - maxW;
            }
            else if (direction == ResizeDirection.Right || direction == ResizeDirection.TopRight || direction == ResizeDirection.BottomRight)
            {
                // 우측을 잡고 드래그 -> 좌측(newL) 고정
                if (currentW < minW) newR = newL + minW;
                else if (currentW > maxW) newR = newL + maxW;
            }

            // 3. 세로 크기 클램핑 (H는 창 본체 높이 = 전체 높이 - 메뉴바)
            float minH = _window.MinSize.y;
            float maxH = _window.MaxSize.y;
            float currentH = newT - newB - barHeight;

            if (direction == ResizeDirection.Top || direction == ResizeDirection.TopLeft || direction == ResizeDirection.TopRight)
            {
                // 상단을 잡고 드래그 -> 하단(newB) 고정
                if (currentH < minH) newT = newB + barHeight + minH;
                else if (currentH > maxH) newT = newB + barHeight + maxH;
            }
            else if (direction == ResizeDirection.Bottom || direction == ResizeDirection.BottomLeft || direction == ResizeDirection.BottomRight)
            {
                // 하단을 잡고 드래그 -> 상단(newT) 고정
                if (currentH < minH) newB = newT - barHeight - minH;
                else if (currentH > maxH) newB = newT - barHeight - maxH;
            }

            // 4. 최종 크기 및 위치 반영
            float finalW = newR - newL;
            float finalH = newT - newB - barHeight;

            float newCenterX = (newL + newR) * 0.5f;
            float newCenterY = newB + (finalH * 0.5f);

            _window.MoveTo(new Vector2(newCenterX, newCenterY));
            _window.Resize(new Vector2(finalW, finalH));
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
    }
}
