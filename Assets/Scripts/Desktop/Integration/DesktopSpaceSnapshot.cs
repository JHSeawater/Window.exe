using UnityEngine;

namespace WindowExe.Desktop
{
    /// <summary>Immutable projection retained for input collected before a camera/work-area change.</summary>
    public readonly struct DesktopSpaceSnapshot
    {
        public readonly int Generation;
        public readonly bool Valid;
        public readonly Rect Bounds, PixelRect;
        public readonly float PlaneZ;
        private readonly Matrix4x4 cameraToWorld, inverseProjection;
        public DesktopSpaceSnapshot(int generation, bool valid, Rect bounds, Rect pixels, float planeZ, Matrix4x4 camera, Matrix4x4 projection)
        { Generation = generation; Valid = valid; Bounds = bounds; PixelRect = pixels; PlaneZ = planeZ; cameraToWorld = camera; inverseProjection = projection.inverse; }
        public static DesktopSpaceSnapshot Capture(int generation, Rect bounds, float planeZ, Camera camera)
        {
            // ScreenPointToRay uses the integer render dimensions, even when pixelRect is fractional.
            var pixels = camera.pixelRect;
            pixels.size = new Vector2(camera.pixelWidth, camera.pixelHeight);
            return new DesktopSpaceSnapshot(generation, true, bounds, pixels, planeZ, camera.cameraToWorldMatrix, camera.projectionMatrix);
        }
        public bool TryWorld(Vector2 screen, out Vector2 world)
        {
            world = default;
            if (!Valid || PixelRect.width <= 0 || PixelRect.height <= 0) return false;
            float x = (screen.x - PixelRect.x) / PixelRect.width * 2 - 1;
            float y = (screen.y - PixelRect.y) / PixelRect.height * 2 - 1;
            var near = inverseProjection * new Vector4(x, y, -1, 1);
            var far = inverseProjection * new Vector4(x, y, 1, 1);
            if (Mathf.Abs(near.w) < 1e-9f || Mathf.Abs(far.w) < 1e-9f) return false;
            var a = cameraToWorld.MultiplyPoint3x4(new Vector3(near.x, near.y, near.z) / near.w);
            var b = cameraToWorld.MultiplyPoint3x4(new Vector3(far.x, far.y, far.z) / far.w);
            var d = b - a;
            if (Mathf.Abs(d.z) < 1e-9f) return false;
            float t = (PlaneZ - a.z) / d.z;
            if (t < 0) return false;
            var p = a + d * t;
            if (float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y)) return false;
            world = new Vector2(p.x, p.y); return true;
        }
    }
}
