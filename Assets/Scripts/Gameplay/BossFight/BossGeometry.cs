using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowExe.BossFight
{
    /// <summary>Reference pixels, origin at the top left. Simulation geometry stays in double precision.</summary>
    [Serializable]
    public struct BossRect
    {
        public double x, y, width, height;
        public double Right => x + width;
        public double Bottom => y + height;
        public Vector2 Center => new Vector2((float)(x + width * .5), (float)(y + height * .5));
        public BossRect(double x, double y, double width, double height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
        public bool Contains(Vector2 p, double inset = 0) => p.x >= x + inset && p.x <= Right - inset && p.y >= y + inset && p.y <= Bottom - inset;
        public bool Contains(BossPoint p, double inset = 0) => p.x >= x + inset && p.x <= Right - inset && p.y >= y + inset && p.y <= Bottom - inset;
        public BossRect Inset(double value) => new BossRect(x + value, y + value, width - 2 * value, height - 2 * value);
        public BossRect Frame(double titleHeight) => new BossRect(x, y - titleHeight, width, height + titleHeight);
        public static BossRect Lerp(BossRect a, BossRect b, double t) => new BossRect(
            a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t,
            a.width + (b.width - a.width) * t, a.height + (b.height - a.height) * t);
    }

    public readonly struct BossInterval
    {
        public readonly double Start, End;
        public BossInterval(double start, double end) { Start = start; End = end; }
    }

    /// <summary>Exact piecewise quadratic distance for linearly moving circle and rectangle edges.</summary>
    public static class BossCollision
    {
        public static bool Overlaps(BossRect rect, Vector2 center, double radius)
            => Overlaps(rect,(BossPoint)center,radius);
        public static bool Overlaps(BossRect rect, BossPoint center, double radius)
        {
            double dx = Math.Max(rect.x - center.x, Math.Max(0, center.x - rect.Right));
            double dy = Math.Max(rect.y - center.y, Math.Max(0, center.y - rect.Bottom));
            return dx * dx + dy * dy <= radius * radius;
        }
        public static bool Sweep(BossRect from, BossRect to, Vector2 eyeFrom, Vector2 eyeTo, double radius, out double first)
            => Sweep(from,to,(BossPoint)eyeFrom,(BossPoint)eyeTo,radius,out first);
        public static bool Sweep(BossRect from, BossRect to, BossPoint eyeFrom, BossPoint eyeTo, double radius, out double first)
        {
            var intervals = new List<BossInterval>(5);
            Intervals(from, to, eyeFrom, eyeTo, radius, intervals);
            first = intervals.Count == 0 ? double.PositiveInfinity : intervals[0].Start;
            return intervals.Count > 0;
        }
        public static void Intervals(BossRect from, BossRect to, Vector2 eyeFrom, Vector2 eyeTo, double radius, List<BossInterval> result)
            => Intervals(from,to,(BossPoint)eyeFrom,(BossPoint)eyeTo,radius,result);
        public static void Intervals(BossRect from, BossRect to, BossPoint eyeFrom, BossPoint eyeTo, double radius, List<BossInterval> result)
        {
            double ex = eyeFrom.x, ey = eyeFrom.y, vx = (double)eyeTo.x - ex, vy = (double)eyeTo.y - ey;
            var cuts = new List<double>(6) { 0, 1 };
            AddCrossing(from.x - ex, to.x - from.x - vx, cuts);
            AddCrossing(from.Right - ex, to.Right - from.Right - vx, cuts);
            AddCrossing(from.y - ey, to.y - from.y - vy, cuts);
            AddCrossing(from.Bottom - ey, to.Bottom - from.Bottom - vy, cuts);
            cuts.Sort();
            for (int i = 1; i < cuts.Count; i++)
            {
                double l = cuts[i - 1], r = cuts[i];
                if (r <= l) continue;
                double mid = (l + r) * .5;
                Axis(from.x - ex, to.x - from.x - vx, from.Right - ex, to.Right - from.Right - vx, mid, out double ax, out double bx);
                Axis(from.y - ey, to.y - from.y - vy, from.Bottom - ey, to.Bottom - from.Bottom - vy, mid, out double ay, out double by);
                Quadratic(ax * ax + ay * ay, 2 * (ax * bx + ay * by), bx * bx + by * by - radius * radius, l, r, result);
            }
        }
        private static void AddCrossing(double b, double a, List<double> values)
        {
            if (a == 0) return;
            double t = -b / a;
            if (t > 0 && t < 1) values.Add(t);
        }
        private static void Axis(double lowB, double lowA, double highB, double highA, double t, out double a, out double b)
        {
            if (lowB + lowA * t > 0) { a = lowA; b = lowB; }
            else if (highB + highA * t < 0) { a = highA; b = highB; }
            else { a = b = 0; }
        }
        private static void Quadratic(double a, double b, double c, double l, double r, List<BossInterval> result)
        {
            if (a == 0)
            {
                if (b == 0) { if (c <= 0) Append(l, r, result); return; }
                double root = -c / b;
                if (b > 0) r = Math.Min(r, root); else l = Math.Max(l, root);
            }
            else
            {
                double discriminant = b * b - 4 * a * c;
                if (discriminant < 0) return;
                double sqrt = Math.Sqrt(discriminant);
                // Stable roots avoid cancellation for near-boundary contacts.
                double q = -.5 * (b + (b >= 0 ? sqrt : -sqrt));
                double p = q == 0 ? -b / (2 * a) : q / a;
                double s = q == 0 ? p : c / q;
                l = Math.Max(l, Math.Min(p, s)); r = Math.Min(r, Math.Max(p, s));
            }
            if (l <= r) Append(l, r, result);
        }
        private static void Append(double l, double r, List<BossInterval> result)
        {
            if (result.Count > 0 && l >= result[result.Count - 1].Start && l <= result[result.Count - 1].End)
            {
                var last = result[result.Count - 1];
                result[result.Count - 1] = new BossInterval(last.Start, Math.Max(last.End, r));
            }
            else result.Add(new BossInterval(l, r));
        }
        public static void Union(List<BossInterval> intervals)
        {
            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
            int write = 0;
            for (int read = 0; read < intervals.Count; read++)
            {
                var next = intervals[read];
                if (write > 0 && next.Start <= intervals[write - 1].End)
                {
                    var previous = intervals[write - 1];
                    intervals[write - 1] = new BossInterval(previous.Start, Math.Max(previous.End, next.End));
                }
                else intervals[write++] = next;
            }
            if (write < intervals.Count) intervals.RemoveRange(write, intervals.Count - write);
        }
    }
}
