using System;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;
namespace WindowExe.Desktop
{
    [DefaultExecutionOrder(-500)]
    public sealed class DesktopWindowSpace : MonoBehaviour
    {
        public static DesktopWindowSpace Instance { get; private set; }
        [SerializeField] private Camera viewCamera;
        [SerializeField] private DesktopWorkArea workArea;
        [SerializeField] private WindowManager manager;
        [SerializeField] private float planeZ;
        public Camera Camera => viewCamera;
        public Rect Bounds { get; private set; }
        public bool Valid { get; private set; }
        public event Action Changed;
        public event Action<DesktopSpaceSnapshot> Changing;
        public DesktopSpaceSnapshot Snapshot { get; private set; }
        private int generation;
        private Matrix4x4 projection, cameraMatrix, canvasMatrix;
        private Rect lastArea, viewport;
        private float lastPlaneZ;
        private Camera lastCamera;
        private DesktopWorkArea lastWorkArea;
        private WindowManager lastManager;
        private bool refreshing;
        private void Awake() { Instance=this; Refresh(); }
        private void OnDestroy() { if(Instance==this) Instance=null; }
        public void Configure(Camera camera, DesktopWorkArea area, WindowManager windows)
        { viewCamera=camera; workArea=area; manager=windows; Refresh(); }
        private void Update() => Refresh();
        public bool TryWorld(Vector2 screen, out Vector2 world)
        {
            world=default;
            if(!Valid || !ConfigurationMatches(lastCamera,lastWorkArea,lastManager,projection,cameraMatrix,canvasMatrix,lastArea,viewport,lastPlaneZ)) return false;
            return TryProject(lastCamera,lastPlaneZ,screen,out world);
        }
        private static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
        private static bool TryProject(Camera camera, float z, Vector2 screen, out Vector2 world)
        {
            world=default;
            if(camera==null||!Finite(z)||!Finite(screen.x)||!Finite(screen.y)) return false;
            var ray=camera.ScreenPointToRay(screen);
            var plane=new Plane(Vector3.forward,new Vector3(0,0,z));
            if(!plane.Raycast(ray,out var distance)) return false;
            var hit=ray.GetPoint(distance);
            if(!Finite(hit.x)||!Finite(hit.y)||!Finite(distance)) return false;
            world=new Vector2(hit.x,hit.y); return true;
        }
        private bool ConfigurationMatches(Camera camera, DesktopWorkArea area, WindowManager windows,
            Matrix4x4 p, Matrix4x4 c, Matrix4x4 canvas, Rect local, Rect pixels, float z)
        {
            return camera!=null&&area!=null&&viewCamera==camera&&workArea==area&&manager==windows
                &&planeZ==z&&viewCamera.projectionMatrix==p&&viewCamera.transform.localToWorldMatrix==c
                &&area.transform.localToWorldMatrix==canvas&&area.Area==local&&viewCamera.pixelRect==pixels;
        }
        private void Invalidate()
        {
            if(!Valid) return;
            var invalid = new DesktopSpaceSnapshot(++generation, false, default, default, planeZ, Matrix4x4.identity, Matrix4x4.identity);
            Changing?.Invoke(invalid);
            // Invalidate before rollback so it cannot constrain against obsolete bounds.
            Valid=false; Bounds=default;
            Snapshot=invalid;
            DesktopInputRouter.Instance?.Cancel(DesktopCancelReason.SpaceChanged);
            Changed?.Invoke();
        }
        public void Refresh()
        {
            if(refreshing) return;
            refreshing=true;
            try
            {
                if(viewCamera==null||workArea==null) { Invalidate(); return; }
                Canvas.ForceUpdateCanvases();
                workArea.Apply();
                var camera=viewCamera; var area=workArea; var windows=manager;
                var canvas=area.transform as RectTransform;
                if(canvas==null) { Invalidate(); return; }
                var local=area.Area; var pixels=camera.pixelRect; var z=planeZ;
                var nextProjection=camera.projectionMatrix;
                var nextCamera=camera.transform.localToWorldMatrix;
                var nextCanvas=canvas.localToWorldMatrix;
                if(Valid&&ConfigurationMatches(lastCamera,lastWorkArea,lastManager,projection,cameraMatrix,canvasMatrix,lastArea,viewport,lastPlaneZ)) return;
                if(local.width<=0||local.height<=0||pixels.width<=0||pixels.height<=0||!Finite(z)) { Invalidate(); return; }
                var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
                var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                foreach(var point in new[]{local.min,new Vector2(local.xMin,local.yMax),local.max,new Vector2(local.xMax,local.yMin)})
                {
                    var screen=RectTransformUtility.WorldToScreenPoint(camera,canvas.TransformPoint(point));
                    if(!TryProject(camera,z,screen,out var world)) { Invalidate(); return; }
                    min=Vector2.Min(min,world); max=Vector2.Max(max,world);
                }
                var bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
                if(!Finite(bounds.width)||!Finite(bounds.height)||bounds.width<=0||bounds.height<=0) { Invalidate(); return; }
                var nextSnapshot = DesktopSpaceSnapshot.Capture(++generation, bounds, z, camera);
                Changing?.Invoke(nextSnapshot);
                DesktopInputRouter.Instance?.Cancel(DesktopCancelReason.SpaceChanged);
                // Cancellation may invoke observers; never publish a mixed configuration.
                if(!ConfigurationMatches(camera,area,windows,nextProjection,nextCamera,nextCanvas,local,pixels,z)) { Invalidate(); return; }
                Bounds=bounds; projection=nextProjection; cameraMatrix=nextCamera; canvasMatrix=nextCanvas;
                lastArea=local; viewport=pixels; lastPlaneZ=z;
                lastCamera=camera; lastWorkArea=area; lastManager=windows; Valid=true;
                Snapshot=nextSnapshot;
                windows?.ConstrainAll(bounds);
                Changed?.Invoke();
            }
            finally { refreshing=false; }
        }
    }
    public static class WindowGeometry
    {
        public static Rect Resize(Rect start, ResizeDirection direction, Vector2 delta, Vector2 min, Vector2 max, Rect area, float bar)
        {
            var l=start.xMin; var r=start.xMax; var b=start.yMin; var t=start.yMax;
            var left=direction==ResizeDirection.Left||direction==ResizeDirection.TopLeft||direction==ResizeDirection.BottomLeft;
            var right=direction==ResizeDirection.Right||direction==ResizeDirection.TopRight||direction==ResizeDirection.BottomRight;
            var top=direction==ResizeDirection.Top||direction==ResizeDirection.TopLeft||direction==ResizeDirection.TopRight;
            var bottom=direction==ResizeDirection.Bottom||direction==ResizeDirection.BottomLeft||direction==ResizeDirection.BottomRight;
            var minW=Mathf.Min(min.x,area.width); var minH=Mathf.Min(min.y+bar,area.height);
            if(left) l=Mathf.Clamp(l+delta.x,Mathf.Max(area.xMin,r-max.x),r-minW);
            if(right) r=Mathf.Clamp(r+delta.x,l+minW,Mathf.Min(area.xMax,l+max.x));
            if(bottom) b=Mathf.Clamp(b+delta.y,Mathf.Max(area.yMin,t-max.y-bar),t-minH);
            if(top) t=Mathf.Clamp(t+delta.y,b+minH,Mathf.Min(area.yMax,b+max.y+bar));
            return Rect.MinMaxRect(l,b,r,t);
        }
    }
}
