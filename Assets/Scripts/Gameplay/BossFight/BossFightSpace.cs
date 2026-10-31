using UnityEngine;
using WindowExe.Desktop;

namespace WindowExe.BossFight
{
    public sealed class BossFightSpace
    {
        public const double Width = 1920, Height = 1032, TitleHeight = 36;
        public DesktopSpaceSnapshot Snapshot { get; }
        public float Scale { get; }
        public Vector2 TopLeft { get; }
        public bool Valid => Snapshot.Valid && Scale > 0 && !float.IsNaN(Scale) && !float.IsInfinity(Scale);
        public BossFightSpace(DesktopSpaceSnapshot snapshot)
        {
            Snapshot = snapshot;
            Scale = snapshot.Valid ? Mathf.Min(snapshot.Bounds.width / (float)Width, snapshot.Bounds.height / (float)Height) : 0;
            TopLeft = new Vector2(snapshot.Bounds.center.x - (float)Width * Scale / 2, snapshot.Bounds.center.y + (float)Height * Scale / 2);
        }
        public Vector2 ToWorld(Vector2 reference) => TopLeft + new Vector2(reference.x, -reference.y) * Scale;
        public Vector2 ToReference(Vector2 world) => new Vector2((world.x - TopLeft.x) / Scale, (TopLeft.y - world.y) / Scale);
        public bool TryReference(Vector2 screen, out Vector2 point)
        { point=default; if(!Valid || !Snapshot.TryWorld(screen,out var world)) return false; point=ToReference(world); return true; }
        public bool Fits(BossRect body) => Valid && body.width >= 90 && body.height >= 90 && body.width <= 600 && body.height <= 510 &&
            body.x >= 0 && body.y >= TitleHeight && body.Right <= Width && body.Bottom <= Height;
    }
}
