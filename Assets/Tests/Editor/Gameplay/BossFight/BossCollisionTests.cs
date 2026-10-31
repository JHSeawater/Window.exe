using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossCollisionTests
    {
        [Test] public void FastWindowCrossingHitsBeforeEndpointDespiteBothEndpointsBeingClear()
        {
            var a = new BossRect(0, 0, 10, 10); var b = new BossRect(100, 0, 10, 10); var eye = new Vector2(50, 5);
            Assert.That(BossCollision.Overlaps(a, eye, 2), Is.False); Assert.That(BossCollision.Overlaps(b, eye, 2), Is.False);
            Assert.That(BossCollision.Sweep(a, b, eye, eye, 2, out double hit), Is.True);
            Assert.That(hit, Is.EqualTo(.38).Within(1e-12));
        }
        [Test] public void SimultaneousEyeAndResizeUseRelativeMotion()
        {
            var a = new BossRect(0, 0, 10, 10); var b = new BossRect(0, 0, 50, 10);
            Assert.That(BossCollision.Sweep(a, b, new Vector2(100, 5), new Vector2(0, 5), 2, out double hit), Is.True);
            Assert.That(hit, Is.EqualTo(88.0 / 140).Within(1e-12));
        }
        [Test] public void SweptBoundingBoxOverlapIsNotACollision()
        {
            Assert.That(BossCollision.Sweep(new BossRect(0, 0, 10, 10), new BossRect(100, 100, 10, 10), new Vector2(10, 90), new Vector2(10, 90), 2, out _), Is.False);
        }
        [Test] public void CornerTangentAndBoundaryTimesAreAccurate()
        {
            var r = new BossRect(10, 10, 10, 10);
            Assert.That(BossCollision.Sweep(r, r, new Vector2(0, 5), new Vector2(30, 5), 5, out double t), Is.True);
            Assert.That(t, Is.EqualTo(1.0 / 3).Within(1e-12));
            Assert.That(BossCollision.Sweep(r, r, new Vector2(0, 4.99f), new Vector2(30, 4.99f), 5, out _), Is.False);
        }
        [Test] public void UnionPreservesEveryRealGapAndRemovesDoubleExposure()
        {
            var spans = new List<BossInterval> { new BossInterval(.6, 1), new BossInterval(0, .4), new BossInterval(.1, .3) };
            BossCollision.Union(spans);
            Assert.That(spans.Count, Is.EqualTo(2)); Assert.That(spans[0].End, Is.EqualTo(.4)); Assert.That(spans[1].Start, Is.EqualTo(.6));
        }
        [Test] public void SplittingTimeDoesNotChangeCollisionMoment()
        {
            var a = new BossRect(0, 0, 10, 10); var b = new BossRect(100, 50, 10, 10); var e = new Vector2(70, 35);
            Assert.That(BossCollision.Sweep(a, b, e, e, 4, out double whole), Is.True);
            double found = double.PositiveInfinity;
            for (int i = 0; i < 60; i++)
                if (BossCollision.Sweep(BossRect.Lerp(a, b, i / 60.0), BossRect.Lerp(a, b, (i + 1) / 60.0), e, e, 4, out double local))
                { found = (i + local) / 60; break; }
            Assert.That(found, Is.EqualTo(whole).Within(1e-10));
        }
        [Test] public void UnsortedEyesDoNotDiscardAnEarlierExposureInterval()
        {
            var spans = new List<BossInterval>();
            var a = new BossRect(0, 0, 10, 10); var b = new BossRect(100, 0, 10, 10);
            BossCollision.Intervals(a, b, new Vector2(85, 5), new Vector2(85, 5), 1, spans);
            BossCollision.Intervals(a, b, new Vector2(25, 5), new Vector2(25, 5), 1, spans);
            BossCollision.Union(spans);
            Assert.That(spans.Count, Is.EqualTo(2));
            Assert.That(spans[0].Start, Is.EqualTo(.14).Within(1e-12));
            Assert.That(spans[1].Start, Is.EqualTo(.74).Within(1e-12));
        }
    }
}
