using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossEyeSilhouetteTests
    {
        private static Sprite Artwork()=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/눈깔풀.png");
        private BossEyeSilhouette Concave()
        {
            return new BossEyeSilhouette(new[]{new[]{new BossPoint(-.5,-.5),new BossPoint(.5,-.5),new BossPoint(.5,0),
                new BossPoint(0,0),new BossPoint(0,.5),new BossPoint(-.5,.5)}},1);
        }
        [Test] public void EmptyPartOfOldCircleAndTransparentImageCornerAreSafe()
        {
            var shape=new BossEyeSilhouette(Artwork());var center=new BossPoint(620,520);
            var below=new BossRect(575,600,90,90);
            Assert.That(BossCollision.Overlaps(below,center,125),Is.True,"This was inside the old gaze circle.");
            Assert.That(shape.Overlaps(below,center,125),Is.False);
            Assert.That(shape.Overlaps(new BossRect(495,437,6,6),center,125),Is.False,"Transparent artwork corners must not be solid.");
            Assert.That(shape.Overlaps(new BossRect(600,500,40,40),center,125),Is.True);
        }
        [Test] public void ConcaveEmptySpaceIsSafeAndInsideContactHasOneContinuousInterval()
        {
            var shape=Concave();var center=new BossPoint(500,500);
            Assert.That(shape.Overlaps(new BossRect(510,510,10,10),center,50),Is.False);
            var intervals=new List<BossInterval>();shape.Intervals(new BossRect(430,520,10,10),new BossRect(530,520,10,10),center,50,intervals);BossCollision.Union(intervals);
            Assert.That(intervals,Has.Count.EqualTo(1));Assert.That(intervals[0].Start,Is.EqualTo(.1).Within(1e-9));Assert.That(intervals[0].End,Is.EqualTo(.7).Within(1e-9));
        }
        [Test] public void ResizeDetectsFirstContactAtTheMovingEdge()
        {
            var shape=Concave();var intervals=new List<BossInterval>();
            shape.Intervals(new BossRect(530,520,10,10),new BossRect(470,520,70,10),new BossPoint(500,500),50,intervals);BossCollision.Union(intervals);
            Assert.That(intervals,Has.Count.EqualTo(1));Assert.That(intervals[0].Start,Is.EqualTo(.5).Within(1e-9));Assert.That(intervals[0].End,Is.EqualTo(1));
        }
        [Test] public void FastPassAcrossArtworkIsDetectedAlthoughBothEndpointsAreSafe()
        {
            var shape=new BossEyeSilhouette(Artwork());var center=new BossPoint(620,520);
            var from=new BossRect(300,480,90,90);var to=new BossRect(820,480,90,90);
            Assert.That(shape.Overlaps(from,center,125),Is.False);Assert.That(shape.Overlaps(to,center,125),Is.False);
            var intervals=new List<BossInterval>();shape.Intervals(from,to,center,125,intervals);BossCollision.Union(intervals);
            Assert.That(intervals,Has.Count.EqualTo(1));Assert.That(intervals[0].Start,Is.InRange(0.01,.5));Assert.That(intervals[0].End,Is.InRange(.5,.99));
        }
        [Test] public void ActualArtworkContactUsesExistingExposureWhileOldCircleOnlySpaceDoesNot()
        {
            var settings=ScriptableObject.CreateInstance<BossFightSettings>();var phase=ScriptableObject.CreateInstance<BossPhaseDefinition>();
            try
            {
                settings.phase1EyeSprite=Artwork();phase.eyes=new[]{new BossStaticEye(620,520,125)};
                var model=new BossPhase1Model(settings,phase,0);model.Teleport(new BossRect(575,600,90,90),new BossPoint(620,645));
                model.Advance(1);model.ResolveBoundary();Assert.That(model.Exposure,Is.Zero);Assert.That(model.Corruption,Is.Zero);
                model.Teleport(new BossRect(575,475,90,90),new BossPoint(620,520));
                model.Advance(1.6);model.ResolveBoundary();Assert.That(model.Corruption,Is.EqualTo(1));
                Assert.That(model.EyeShape.Size(125),Is.EqualTo(new Vector2(250,250*2f/3)));
            }
            finally{Object.DestroyImmediate(settings);Object.DestroyImmediate(phase);}
        }
        [Test] public void SplittingCombinedMovementAndResizePreservesArtworkContactTimes()
        {
            var shape=new BossEyeSilhouette(Artwork());var center=new BossPoint(620,520);
            var from=new BossRect(300,300,90,90);var to=new BossRect(750,550,180,240);
            var whole=new List<BossInterval>();var split=new List<BossInterval>();
            shape.Intervals(from,to,center,125,whole);BossCollision.Union(whole);
            for(int i=0;i<64;i++)
            {
                var part=new List<BossInterval>();shape.Intervals(BossRect.Lerp(from,to,i/64d),BossRect.Lerp(from,to,(i+1)/64d),center,125,part);
                foreach(var interval in part)split.Add(new BossInterval((i+interval.Start)/64,(i+interval.End)/64));
            }
            BossCollision.Union(split);Assert.That(whole,Is.Not.Empty);Assert.That(split.Count,Is.EqualTo(whole.Count));
            for(int i=0;i<whole.Count;i++){Assert.That(split[i].Start,Is.EqualTo(whole[i].Start).Within(1e-10));Assert.That(split[i].End,Is.EqualTo(whole[i].End).Within(1e-10));}
        }
    }
}
