using UnityEngine;
using TMPro;

namespace WindowExe.Desktop.Desktop.Window
{
    public enum WindowInteraction { None, Moving, Resizing }
    [DisallowMultipleComponent]
    public abstract class Window : MonoBehaviour
    {
        public const string PrimarySortingLayerName = "WindowFront", SecondarySortingLayerName = "WindowBack";
        public const int DefaultSortingOrder = 0;
        [SerializeField] private string windowTitle = "창";
        [SerializeField] private Sprite secondarySprite;
        [SerializeField] private Sprite taskbarIcon;
        [SerializeField] private TMP_FontAsset textFont;
        [SerializeField] private bool showInTaskbar = true, canMinimize = true, isModal;
        [SerializeField] private string ownerWindowId;
        [SerializeField] protected Vector2 size = new Vector2(4, 3);
        [SerializeField] protected Vector2 minSize = new Vector2(2.4f, 1.2f);
        [SerializeField] protected Vector2 maxSize = new Vector2(20, 20);
        [Header("Window Border")]
        [SerializeField] private bool showBorder = true;
        [SerializeField] private float borderThickness = 0.06f;
        [SerializeField] private Color borderColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Sprite borderSprite;
        private SpriteRenderer borderRenderer;
        private SpriteRenderer secondary;
        private WindowTitleBar titleBar;
        private WindowResizeHandle resizeHandle;
        private WindowManager manager;
        private Vector2 restorePosition, restoreSize;
        private bool hasNormalGeometry;
        public Vector2 Size => size;
        public Vector2 MinSize => minSize;
        public Vector2 MaxSize => maxSize;
        public string WindowTitle => windowTitle;
        // Legacy content key. The process InstanceId is the immutable request identity.
        public string WindowId { get; set; }
        public WindowManager Manager => manager;
        public bool IsMaximized { get; private set; }
        public bool ShowInTaskbar => showInTaskbar;
        public bool CanMinimize => canMinimize;
        public bool IsModal => isModal;
        public string OwnerWindowId => ownerWindowId;
        public Sprite TaskbarIcon => taskbarIcon;
        public float BarHeight => titleBar != null ? titleBar.BarHeight : 0.5f;
        public bool ShowBorder => showBorder;
        public float BorderThickness => borderThickness;
        public Color BorderColor => borderColor;
        public SpriteRenderer BorderRenderer => borderRenderer;
        public WindowInteraction CurrentInteraction { get; internal set; }
        public event System.Action<Window> OnCloseRequested;
        public Rect OuterBounds => Rect.MinMaxRect(transform.localPosition.x - size.x / 2, transform.localPosition.y - size.y / 2,
            transform.localPosition.x + size.x / 2, transform.localPosition.y + size.y / 2 + BarHeight);
        protected virtual void Awake() => InitializeVisuals();
        protected virtual void OnDestroy() { if (manager != null) manager.RemovedExternally(this); }
        public void Attach(WindowManager owner, string id) { manager = owner; WindowId = id; }
        public void InitializeVisuals()
        {
            InitializeBorder();
            secondary = Child("Secondary").GetComponent<SpriteRenderer>();
            if (secondary == null) secondary = Child("Secondary").gameObject.AddComponent<SpriteRenderer>();
            secondary.sortingLayerName = SecondarySortingLayerName;
            secondary.drawMode = SpriteDrawMode.Sliced;
            secondary.sprite = secondarySprite;
            secondary.enabled = secondarySprite != null;
            var title = Child("TitleBar");
            titleBar = title.GetComponent<WindowTitleBar>();
            if (titleBar == null) titleBar = title.gameObject.AddComponent<WindowTitleBar>();
            titleBar.Initialize(this);
            var handles = Child("ResizeHandle");
            resizeHandle = handles.GetComponent<WindowResizeHandle>();
            if (resizeHandle == null) resizeHandle = handles.gameObject.AddComponent<WindowResizeHandle>();
            resizeHandle.Initialize(this);
            InitializePrimary();
            ApplySize(size);
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (textFont != null) text.font = textFont;
                GameTypography.Apply(text);
                text.textWrappingMode = text.GetComponentInParent<WindowTitleBar>() != null ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Truncate;
            }
            if (manager != null && manager.IsCurrent(this)) manager.ApplySorting();
            else ApplySorting(DefaultSortingOrder);
        }
        private void InitializeBorder()
        {
            var borderTransform = Child("Border");
            borderRenderer = borderTransform.GetComponent<SpriteRenderer>();
            if (borderRenderer == null) borderRenderer = borderTransform.gameObject.AddComponent<SpriteRenderer>();
            borderRenderer.sortingLayerName = PrimarySortingLayerName;
            borderRenderer.drawMode = SpriteDrawMode.Sliced;
            UpdateBorderAppearance();
        }
        private void UpdateBorderAppearance()
        {
            if (borderRenderer == null) return;
            borderRenderer.sprite = borderSprite != null ? borderSprite : WindowTitleBar.WhiteSprite;
            borderRenderer.color = borderColor;
            borderRenderer.enabled = showBorder;
        }
        private void UpdateBorderLayout(Vector2 value)
        {
            if (borderRenderer == null) return;
            borderRenderer.transform.localPosition = new Vector3(0, BarHeight / 2f, 0);
            borderRenderer.size = new Vector2(value.x + borderThickness * 2f, value.y + BarHeight + borderThickness * 2f);
            borderRenderer.enabled = showBorder;
        }
        public void SetBorder(bool show, float thickness, Color color, Sprite sprite = null)
        {
            showBorder = show;
            borderThickness = thickness;
            borderColor = color;
            if (sprite != null) borderSprite = sprite;
            UpdateBorderAppearance();
            ApplySize(size);
        }
        private Transform Child(string name)
        {
            var child = transform.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(transform, false); }
            return child;
        }
        public void ConfigureAppearance(string title, Sprite icon, TMP_FontAsset font)
        {
            if (manager != null && !manager.IsCurrent(this)) return;
            windowTitle = title; taskbarIcon = icon; textFont = font;
            InitializeVisuals();
            manager?.AppearanceChanged(this);
        }
        public void SetPolicyFields(bool show, bool minimize, bool modal, string owner)
        { showInTaskbar = show; canMinimize = minimize; isModal = modal; ownerWindowId = owner; }
        public void SetWindowTitle(string title)
        { windowTitle = title; if (titleBar != null) titleBar.SetTitle(title); manager?.AppearanceChanged(this); }
        public void SetTaskbarIcon(Sprite icon) { taskbarIcon = icon; manager?.AppearanceChanged(this); }
        public void SetSecondarySprite(Sprite sprite)
        { secondarySprite = sprite; if (secondary != null) { secondary.sprite = sprite; secondary.size=size; secondary.enabled = sprite != null; } }
        public virtual void RequestClose()
        {
            if (manager != null && !manager.IsCurrent(this)) return;
            if (manager != null && !manager.CanRequest(this, WindowAction.Close)) return;
            if (OnCloseRequested != null) OnCloseRequested(this);
            else manager?.TryRequest(this, WindowAction.Close);
        }
        public virtual void RequestMinimize() => manager?.TryRequest(this, WindowAction.Minimize);
        public virtual void RequestMaximize() => manager?.TryRequest(this, WindowAction.Maximize);
        public virtual bool IsPointerOverResizeHandle(Vector2 point) => resizeHandle != null && resizeHandle.IsPointerOverAnyHandle(point);
        public int ButtonAt(Vector2 point) => titleBar != null ? titleBar.ButtonAt(point) : -1;
        public bool TitleContains(Vector2 point) => titleBar != null && titleBar.Contains(point);
        public bool Hit(Vector2 point) => OuterBounds.Contains(point) || IsPointerOverResizeHandle(point);
        public virtual ResizeDirection? ResizeAt(Vector2 point) => resizeHandle != null ? resizeHandle.DirectionAt(point) : null;
        public virtual void MoveTo(Vector2 position)
        {
            if (IsMaximized) return;
            EnsureNormalGeometry();
            restorePosition=position;
            ApplyDisplayGeometry(position,size);
            ConstrainToCurrentArea();
            // Movement commits the reachable position, never the temporary clipped size.
            restorePosition=transform.localPosition;
        }
        public virtual void Translate(Vector2 delta) => MoveTo((Vector2)transform.localPosition + delta);
        public virtual void Resize(Vector2 value)
        {
            if (IsMaximized) return;
            ApplyGeometry(transform.localPosition,ClampNormalSize(value));
            ConstrainToCurrentArea();
        }
        public virtual void ResizeDelta(Vector2 delta)
        {
            if (IsMaximized) return;
            EnsureNormalGeometry();
            var next=restoreSize;
            if (delta.x != 0) next.x=Mathf.Clamp(next.x+delta.x,minSize.x,maxSize.x);
            if (delta.y != 0) next.y=Mathf.Clamp(next.y+delta.y,minSize.y,maxSize.y);
            restoreSize=next;
            ApplyDisplayGeometry(restorePosition,restoreSize);
            ConstrainToCurrentArea();
        }
        public void ApplyGeometry(Vector2 position, Vector2 newSize)
        {
            if (!IsMaximized)
            {
                restorePosition=position; restoreSize=PositiveSize(newSize); hasNormalGeometry=true;
            }
            ApplyDisplayGeometry(position,newSize);
        }
        private void ApplyDisplayGeometry(Vector2 position, Vector2 newSize)
        {
            transform.localPosition = new Vector3(position.x, position.y, transform.localPosition.z);
            size=PositiveSize(newSize);
            ApplySize(size);
        }
        private static Vector2 PositiveSize(Vector2 value) => new Vector2(Mathf.Max(.1f,value.x),Mathf.Max(.1f,value.y));
        private Vector2 ClampNormalSize(Vector2 value) => new Vector2(Mathf.Clamp(value.x,minSize.x,maxSize.x),Mathf.Clamp(value.y,minSize.y,maxSize.y));
        private void EnsureNormalGeometry()
        {
            if (hasNormalGeometry) return;
            restorePosition=transform.localPosition; restoreSize=PositiveSize(size); hasNormalGeometry=true;
        }
        private void ConstrainToCurrentArea()
        {
            var space=DesktopWindowSpace.Instance;
            if (space != null && space.Valid) ConstrainTo(space.Bounds);
        }
        public WindowGeometryState CaptureGeometryState() => new WindowGeometryState(this,
            transform.localPosition,size,restorePosition,restoreSize,hasNormalGeometry);
        public void RestoreGeometryState(WindowGeometryState state)
        {
            if (!ReferenceEquals(state.Owner,this) || manager != null && !manager.IsCurrent(this)) return;
            restorePosition=state.NormalPosition; restoreSize=state.NormalSize; hasNormalGeometry=state.HasNormalGeometry;
            ApplyDisplayGeometry(state.Position,state.Size);
            ConstrainToCurrentArea();
        }
        public void ResizeFromGeometryState(WindowGeometryState start, ResizeDirection direction, Rect outerBounds)
        {
            if (IsMaximized || !ReferenceEquals(start.Owner,this)) return;
            if (manager != null && !manager.IsCurrent(this)) return;
            restorePosition=start.HasNormalGeometry?start.NormalPosition:start.Position;
            restoreSize=start.HasNormalGeometry?start.NormalSize:start.Size;
            hasNormalGeometry=true;
            var nextSize=new Vector2(outerBounds.width,outerBounds.height-BarHeight);
            var nextPosition=new Vector2(outerBounds.center.x,outerBounds.center.y-BarHeight/2);
            var horizontal=direction!=ResizeDirection.Top && direction!=ResizeDirection.Bottom;
            var vertical=direction!=ResizeDirection.Left && direction!=ResizeDirection.Right;
            // Returning to the press geometry restores the original logical axis too.
            if (horizontal && Mathf.Abs(nextSize.x-start.Size.x)>.00001f)
            { restorePosition.x=nextPosition.x; restoreSize.x=nextSize.x; }
            if (vertical && Mathf.Abs(nextSize.y-start.Size.y)>.00001f)
            { restorePosition.y=nextPosition.y; restoreSize.y=nextSize.y; }
            ApplyDisplayGeometry(nextPosition,nextSize);
            ConstrainToCurrentArea();
        }
        public virtual void Maximize()
        {
            if (IsMaximized || DesktopWindowSpace.Instance == null) return;
            EnsureNormalGeometry();
            IsMaximized = true; ConstrainTo(DesktopWindowSpace.Instance.Bounds);
        }
        public virtual void Restore()
        {
            if (!IsMaximized) return;
            IsMaximized = false;
            ApplyDisplayGeometry(restorePosition,restoreSize);
            ConstrainToCurrentArea();
        }
        public virtual void ToggleMaximize() { if (IsMaximized) Restore(); else Maximize(); }
        public void ConstrainTo(Rect bounds)
        {
            if (bounds.width <= 0 || bounds.height <= BarHeight) return;
            EnsureNormalGeometry();
            if (IsMaximized) { ApplyDisplayGeometry(new Vector2(bounds.center.x, bounds.center.y - BarHeight / 2), new Vector2(bounds.width, bounds.height - BarHeight)); return; }
            var clippedSize = new Vector2(Mathf.Min(restoreSize.x, bounds.width), Mathf.Min(restoreSize.y, bounds.height - BarHeight));
            var p = restorePosition;
            p.x = Mathf.Clamp(p.x, bounds.xMin + clippedSize.x / 2, bounds.xMax - clippedSize.x / 2);
            p.y = Mathf.Clamp(p.y, bounds.yMin + clippedSize.y / 2, bounds.yMax - clippedSize.y / 2 - BarHeight);
            ApplyDisplayGeometry(p,clippedSize);
        }
        public virtual void ApplySorting(int order)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var isSecondary = secondary != null && renderer.transform.IsChildOf(secondary.transform);
                renderer.sortingLayerName = isSecondary ? SecondarySortingLayerName : PrimarySortingLayerName;
                var offset = isSecondary
                    ? (renderer.transform == secondary.transform ? 0 : 1)
                    : (renderer == borderRenderer
                        ? -1
                        : (renderer.GetComponentInParent<WindowTitleBar>() != null
                            ? (renderer is SpriteRenderer ? 10 : 11)
                            : (renderer is SpriteRenderer && renderer.name != "Image" ? 0 : 1)));
                renderer.sortingOrder = order + offset;
            }
        }
        protected virtual void ApplySize(Vector2 value)
        {
            if (secondary != null) secondary.size = value;
            titleBar?.UpdateLayout(value);
            resizeHandle?.UpdateLayout(value);
            UpdateBorderLayout(value);
        }
        protected abstract void InitializePrimary();
        protected static Sprite GetFallbackTransparentSprite() => WindowTitleBar.WhiteSprite;
#if UNITY_EDITOR
        protected virtual void OnValidate() { }
        protected virtual void Reset() { }
#endif
    }
}
