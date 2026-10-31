using UnityEngine;
using W = WindowExe.Desktop.Desktop.Window.Window;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop
{
    /// <summary>Optional per-session policy. The default desktop has no policy and keeps its existing behavior.</summary>
    public interface IWindowSessionPolicy
    {
        bool AllowOpen(string contentId, object authority);
        bool AllowRequest(W window, WindowAction action, object authority);
        bool KeepPositionOnMinimize(W window);
    }
    public interface IWindowGeometryConstraint
    {
        bool DeferAreaConstraint { get; }
        bool TryConstrain(W window, ref Vector2 position, ref Vector2 size);
    }
    public enum DesktopCancelReason { Explicit, FocusLost, SpaceChanged, Disabled }
    public interface IDesktopInputSession
    {
        void Cancel(DesktopCancelReason reason);
    }
}
