using UnityEngine;

namespace WindowExe.Desktop
{
    public static class DesktopLayout
    {
        public static Rect FromPoints(Vector2 a, Vector2 b) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        public static Vector2 ClampPoint(Vector2 point, Rect area) =>
            new Vector2(Mathf.Clamp(point.x, area.xMin, area.xMax), Mathf.Clamp(point.y, area.yMin, area.yMax));

        public static bool Intersects(Rect a, Rect b) =>
            a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0 && a.Overlaps(b);

        public static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(
            Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        public static Rect BoundsInParent(RectTransform item, RectTransform parent)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            for (var i = 1; i < corners.Length; i++)
            {
                Vector2 point = parent.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Vector2 ClampOffset(Rect item, Rect area)
        {
            var x = item.width > area.width ? area.center.x - item.center.x
                : Mathf.Clamp(item.xMin, area.xMin, area.xMax - item.width) - item.xMin;
            var y = item.height > area.height ? area.center.y - item.center.y
                : Mathf.Clamp(item.yMin, area.yMin, area.yMax - item.height) - item.yMin;
            return new Vector2(x, y);
        }

        public static bool Fits(Rect item, Rect area)
        {
            const float tolerance = 0.05f;
            return item.xMin >= area.xMin - tolerance && item.yMin >= area.yMin - tolerance
                && item.xMax <= area.xMax + tolerance && item.yMax <= area.yMax + tolerance;
        }
    }
}
