using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;
namespace WindowExe.Desktop.Tests
{
    public sealed class WindowGeometryTests
    {
        [TestCase(ResizeDirection.Left)] [TestCase(ResizeDirection.Right)]
        [TestCase(ResizeDirection.Top)] [TestCase(ResizeDirection.Bottom)]
        [TestCase(ResizeDirection.TopLeft)] [TestCase(ResizeDirection.TopRight)]
        [TestCase(ResizeDirection.BottomLeft)] [TestCase(ResizeDirection.BottomRight)]
        public void ResizeKeepsOppositeEdgesAtBothSizeLimits(ResizeDirection direction)
        {
            var start=Rect.MinMaxRect(-2,-2,2,2.5f);
            var area=Rect.MinMaxRect(-6,-5,6,5);
            foreach(var delta in new[]{new Vector2(-100,-100),new Vector2(100,100),new Vector2(.2f,.3f)})
            {
                var r=WindowGeometry.Resize(start,direction,delta,new Vector2(2,1),new Vector2(6,6),area,.5f);
                Assert.That(r.width,Is.InRange(2,6)); Assert.That(r.height,Is.InRange(1.5f,6.5f));
                Assert.That(r.xMin,Is.GreaterThanOrEqualTo(area.xMin)); Assert.That(r.xMax,Is.LessThanOrEqualTo(area.xMax));
                Assert.That(r.yMin,Is.GreaterThanOrEqualTo(area.yMin)); Assert.That(r.yMax,Is.LessThanOrEqualTo(area.yMax));
                bool left=direction==ResizeDirection.Left||direction==ResizeDirection.TopLeft||direction==ResizeDirection.BottomLeft;
                bool right=direction==ResizeDirection.Right||direction==ResizeDirection.TopRight||direction==ResizeDirection.BottomRight;
                bool top=direction==ResizeDirection.Top||direction==ResizeDirection.TopLeft||direction==ResizeDirection.TopRight;
                bool bottom=direction==ResizeDirection.Bottom||direction==ResizeDirection.BottomLeft||direction==ResizeDirection.BottomRight;
                if(!right) Assert.That(r.xMax,Is.EqualTo(start.xMax).Within(.00001));
                if(!left) Assert.That(r.xMin,Is.EqualTo(start.xMin).Within(.00001));
                if(!top) Assert.That(r.yMax,Is.EqualTo(start.yMax).Within(.00001));
                if(!bottom) Assert.That(r.yMin,Is.EqualTo(start.yMin).Within(.00001));
            }
        }
    }
}
