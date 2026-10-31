using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WindowExe.Desktop.Desktop.Window;
using W = WindowExe.Desktop.Desktop.Window.Window;
namespace WindowExe.Desktop
{
    public enum DesktopPointerOwner { None, Desktop, Taskbar, Window, Blocked }
    [DefaultExecutionOrder(-200)]
    public sealed class DesktopInputRouter : MonoBehaviour
    {
        public static DesktopInputRouter Instance { get; private set; }
        [SerializeField] private WindowManager manager;
        [SerializeField] private DesktopController desktop;
        [SerializeField] private RectTransform taskbar;
        [SerializeField] private DesktopWindowSpace space;
        private DesktopPointerOwner owner;
        private W window;
        private Vector2 pressScreen, pressWorld, startPosition;
        private WindowGeometryState startGeometry;
        private Rect startBounds;
        private ResizeDirection? resize;
        private int button=-1, syncedFrame=-1, cancelledFrame=-1;
        private bool cancelled, moved, held, titleDrag, waitRelease;
        private Vector2 pendingPress;
        private Mouse pendingMouse;
        private bool hasPendingPress;
        private IDesktopInputSession session;
        private object sessionOwner;
        public bool HasSession => session != null;
        // Legacy world-space controls poll Mouse directly rather than receiving routed UI events.
        public static bool IsWorldInputBlocked => Instance != null &&
            (Instance.session != null || Instance.manager != null && Instance.manager.IsLocked);
        public bool TryBeginSession(object ownerToken, IDesktopInputSession value)
        {
            if (ownerToken == null || value == null || session != null) return false;
            Cancel(); sessionOwner = ownerToken; session = value; return true;
        }
        public bool EndSession(object ownerToken)
        {
            if (!ReferenceEquals(ownerToken, sessionOwner)) return false;
            session = null; sessionOwner = null; window = null; Cancel(); return true;
        }
        public DesktopPointerOwner Owner { get { Sync(); return owner; } }
        public bool IsHeld => held;
        public string Diagnostics => $"Cancelled={cancelled}, Wait={waitRelease}, Held={held}, Press={pressScreen}, Frame={syncedFrame}/{Time.frameCount}, Button={button}, Resize={resize}, Title={titleDrag}, Moved={moved}, Start={startPosition}";
        private void Awake() => Instance=this;
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            InputSystem.onEvent -= CapturePress;
            InputSystem.onEvent += CapturePress;
        }
        private void CapturePress(InputEventPtr input, InputDevice device)
        {
            if((InputState.currentUpdateType & InputUpdateType.Dynamic)==0 || !(device is Mouse mouse) ||
                (!input.IsA<StateEvent>() && !input.IsA<DeltaStateEvent>())) return;
            if(!mouse.leftButton.ReadValueFromEvent(input,out var value) || value<mouse.leftButton.pressPointOrDefault || mouse.leftButton.isPressed) return;
            // Copy the press event before later events in this update can move the device state.
            pendingMouse=mouse;
            pendingPress=mouse.position.ReadValueFromEvent(input,out var position) ? position : mouse.position.ReadValue();
            hasPendingPress=true;
        }
        private void OnDestroy() { if(Instance==this) Instance=null; }
        public void Configure(WindowManager windows, DesktopController surface, RectTransform bar, DesktopWindowSpace coordinates)
        { manager=windows; desktop=surface; taskbar=bar; space=coordinates; }
        public static bool DesktopAllowed(PointerEventData data=null)
        { if(Instance==null||!Instance.isActiveAndEnabled) return true; if(Instance.session!=null) return false; Instance.Sync(); return !Instance.cancelled&&Instance.owner==DesktopPointerOwner.Desktop; }
        public static bool TaskbarAllowed(PointerEventData data=null)
        { if(Instance==null||!Instance.isActiveAndEnabled) return true; if(Instance.session!=null) return false; Instance.Sync(); return !Instance.cancelled&&Instance.owner==DesktopPointerOwner.Taskbar; }
        public static bool WheelAllowed(Vector2 screen)
        {
            if(Instance==null||!Instance.isActiveAndEnabled) return true;
            var r=Instance; r.Sync();
            if(r.session!=null) return false;
            if(r.cancelled&&r.held || r.manager==null || r.manager.IsLocked) return false;
            // Wheel has no PointerDown; use hover unless an existing drag owns the pointer.
            return (!r.held||r.owner==DesktopPointerOwner.Taskbar) && r.OverBar(screen);
        }
        private bool OverBar(Vector2 p) => taskbar!=null&&RectTransformUtility.RectangleContainsScreenPoint(taskbar,p,null);
        private void Update() => Sync();
        private void Sync()
        {
            if(session!=null) { owner=DesktopPointerOwner.Blocked; return; }
            if(syncedFrame==Time.frameCount) return;
            syncedFrame=Time.frameCount;
            var mouse=Mouse.current;
            if(mouse==null) return;
            var p=mouse.position.ReadValue();
            held=mouse.leftButton.isPressed;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
            if(cancelled)
            {
                if(!held) waitRelease=false;
                if(cancelledFrame==Time.frameCount || !mouse.leftButton.wasPressedThisFrame || waitRelease) return;
                
            }
            if(mouse.leftButton.wasPressedThisFrame)
            {
                cancelled=false; moved=false; window=null; button=-1; resize=null; titleDrag=false;
                pressScreen=hasPendingPress&&pendingMouse==mouse?pendingPress:p;
                hasPendingPress=false;
                owner=DesktopPointerOwner.Blocked;
                if(manager==null||manager.IsLocked) return;
                if(OverBar(pressScreen)) owner=DesktopPointerOwner.Taskbar;
                else if(space!=null&&space.Valid&&space.TryWorld(pressScreen,out pressWorld))
                {
                    Physics2D.SyncTransforms();
                    var order=manager.DisplayOrder;
                    for(var i=order.Count-1;i>=0;i--)
                    {
                        var candidate=order[i];
                        if(candidate.State==WindowState.Minimized||candidate.Instance==null||!candidate.Instance.gameObject.activeInHierarchy||!candidate.Instance.Hit(pressWorld)) continue;
                        window=candidate.Instance;
                        if(!manager.TryRequest(window,WindowAction.Activate)||manager.ActiveWindowId!=candidate.InstanceId) { window=null; return; }
                        owner=DesktopPointerOwner.Window;
                        startPosition=window.transform.localPosition; startBounds=window.OuterBounds;
                        startGeometry=window.CaptureGeometryState();
                        button=window.ButtonAt(pressWorld);
                        resize=button<0?window.ResizeAt(pressWorld):null;
                        titleDrag=button<0&&!resize.HasValue&&window.TitleContains(pressWorld)&&!window.IsMaximized;
                        break;
                    }
                    if(window==null && owner!=DesktopPointerOwner.Blocked) owner=DesktopPointerOwner.Desktop;
                    else if(window==null) owner=manager.HasModal?DesktopPointerOwner.Blocked:DesktopPointerOwner.Desktop;
                }
            }
            if(owner!=DesktopPointerOwner.Window||window==null||cancelled) return;
            if(!manager.IsCurrent(window)||!window.gameObject.activeInHierarchy||manager.IsLocked) { Cancel(); return; }
            if(space==null||!space.Valid||!space.TryWorld(p,out var currentWorld)) { Cancel(); return; }
            if(Vector2.Distance(p,pressScreen)>=(EventSystem.current!=null?EventSystem.current.pixelDragThreshold:8)) moved=true;
            if((held||mouse.leftButton.wasReleasedThisFrame)&&moved)
            {
                if(resize.HasValue)
                {
                    var rect=WindowGeometry.Resize(startBounds,resize.Value,currentWorld-pressWorld,window.MinSize,window.MaxSize,space.Bounds,window.BarHeight);
                    window.CurrentInteraction=WindowInteraction.Resizing;
                    window.ResizeFromGeometryState(startGeometry,resize.Value,rect);
                }
                else if(titleDrag) { window.CurrentInteraction=WindowInteraction.Moving; window.MoveTo(startPosition+currentWorld-pressWorld); }
            }
            if(mouse.leftButton.wasReleasedThisFrame)
            {
                var target=window; window.CurrentInteraction=WindowInteraction.None;
                if(button>=0&&!moved&&space.TryWorld(p,out var releaseWorld)&&target.ButtonAt(releaseWorld)==button)
                { if(button==0) target.RequestMinimize(); else if(button==1) target.RequestMaximize(); else target.RequestClose(); }
            }
        }
        public void CancelFor(W target) { if(ReferenceEquals(target,window)) Cancel(); }
        public void FinishFor(W target)
        {
            if(!ReferenceEquals(target,window)) return;
            if(window!=null) window.CurrentInteraction=WindowInteraction.None;
            window=null; button=-1; resize=null; titleDrag=false;
            cancelled=true; cancelledFrame=Time.frameCount;
            waitRelease=Mouse.current!=null&&Mouse.current.leftButton.isPressed;
            owner=DesktopPointerOwner.Blocked;
        }
        public void Cancel(DesktopCancelReason reason = DesktopCancelReason.Explicit)
        {
            if(session!=null) { session.Cancel(reason); return; }
            if(!cancelled&&window!=null&&manager!=null&&manager.IsCurrent(window)&&window.CurrentInteraction!=WindowInteraction.None)
            { window.RestoreGeometryState(startGeometry); window.CurrentInteraction=WindowInteraction.None; }
            desktop?.CancelInteraction();
            cancelledFrame=Time.frameCount;
            cancelled=true; waitRelease=Mouse.current!=null&&Mouse.current.leftButton.isPressed; owner=DesktopPointerOwner.Blocked;
        }
        private void OnApplicationFocus(bool focused) { if(!focused) Cancel(DesktopCancelReason.FocusLost); }
        private void OnDisable() { Cancel(DesktopCancelReason.Disabled); InputSystem.onEvent-=CapturePress; hasPendingPress=false; }
    }
}
