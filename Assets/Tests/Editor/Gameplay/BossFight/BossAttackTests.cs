using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossAttackTests
    {
        private BossFightSettings settings;
        [SetUp] public void SetUp()=>settings=ScriptableObject.CreateInstance<BossFightSettings>();
        [TearDown] public void TearDown()=>UnityEngine.Object.DestroyImmediate(settings);
        private BossPhase2Model Model()=>new BossPhase2Model(settings,0){Invulnerable=true};
        private void Advance(BossPhase2Model model,double time){model.Advance(time);model.ResolveBoundary();}
        [Test] public void SequenceWaitsScattersAndAimsAtScheduledTimes()
        {
            var model=Model();var fired=new List<(BossEyeKind kind,double time)>();
            model.Attacks.Fire+=(kind,_)=>fired.Add((kind,model.Clock.InputTime));
            Advance(model,2.199);Assert.That(fired,Is.Empty);
            Advance(model,2.2);Assert.That(fired.Count,Is.EqualTo(1));
            Advance(model,9.21);
            Assert.That(fired.Count(x=>x.kind==BossEyeKind.Scatter),Is.EqualTo(5));
            Assert.That(fired.Count(x=>x.kind==BossEyeKind.Aimed),Is.EqualTo(3));
            Assert.That(fired.First(x=>x.kind==BossEyeKind.Aimed).time,Is.EqualTo(8.3).Within(1e-9));
            foreach(var eye in model.Eyes.Where(x=>x.Kind==BossEyeKind.Aimed))Assert.That(eye.Velocity.Magnitude,Is.EqualTo(278).Within(1e-9));
        }
        [Test] public void CaptureInsideMotionPausesAtCrossingNotFrameEnd()
        {
            settings.phase2Boss=new BossPoint(600,525);
            var one=Model();var split=Model();var start=one.Body;var end=new BossRect(start.x,start.y,600,start.height);
            one.Advance(2,end,one.Trash);one.ResolveBoundary();
            for(int i=1;i<=20;i++){split.Advance(i*.1,BossRect.Lerp(start,end,i/20.0),split.Trash);split.ResolveBoundary();}
            Assert.That(one.Captured,Is.True);
            Assert.That(one.Attacks.NormalRemaining,Is.EqualTo(.7).Within(1e-9));
            Assert.That(split.Attacks.NormalRemaining,Is.EqualTo(one.Attacks.NormalRemaining).Within(1e-9));
            Advance(one,20);Assert.That(one.Attacks.NormalRemaining,Is.EqualTo(.7).Within(1e-9));
        }
        [Test] public void ThresholdsQueueInOrderAndNextWaitsUntilWallExit()
        {
            var model=Model();model.Teleport(new BossRect(900,400,500,300),new BossPoint(1300,540));
            Assert.That(model.Attacks.Warning,Is.True);Assert.That(model.Attacks.PendingWalls,Is.EqualTo(1));
            Advance(model,1.8);Assert.That(model.Attacks.WallsLaunched,Is.EqualTo(1));
            Assert.That(model.Eyes.Count(x=>x.Kind==BossEyeKind.Wall),Is.EqualTo(21));
            Advance(model,4.31);Assert.That(model.Attacks.Warning,Is.False);Assert.That(model.Attacks.PendingWalls,Is.EqualTo(1));
            Advance(model,12.5);Assert.That(model.Attacks.Warning,Is.True);Assert.That(model.Attacks.PendingWalls,Is.Zero);
            Assert.That(model.Eyes.Any(x=>x.Kind==BossEyeKind.Wall),Is.False);
        }
        [Test] public void CaptureAtLaunchCancelsWarningAndQueueButConsumesThresholds()
        {
            var model=Model();model.Teleport(new BossRect(900,400,500,300),new BossPoint(1300,540));
            model.Advance(1.8);
            model.Teleport(new BossRect(1500,400,400,300),new BossPoint(1700,540));model.ResolveBoundary();
            Assert.That(model.Attacks.WallsLaunched,Is.Zero);Assert.That(model.Attacks.Warning,Is.False);
            Assert.That(model.Attacks.PendingWalls,Is.Zero);Assert.That(model.Attacks.ConsumedWalls,Is.EqualTo(3));
            model.Teleport(settings.phase2Window,settings.phase2Trash);Advance(model,10);
            Assert.That(model.Attacks.WallsLaunched,Is.Zero);
        }
        [Test] public void VariedWallEyesStayGaplessAndDoNotClearBeforeLargestRadiusExits()
        {
            var model=Model();model.Teleport(new BossRect(500,400,400,300),new BossPoint(700,540));Advance(model,1.8);
            var wall=model.Eyes.Where(x=>x.Kind==BossEyeKind.Wall).OrderBy(x=>x.Position.y).ToArray();
            Assert.That(wall.Select(x=>x.Radius).Distinct().Count(),Is.GreaterThan(1));
            Assert.That(wall[0].Position.y-wall[0].Radius,Is.LessThanOrEqualTo(0));
            Assert.That(wall[wall.Length-1].Position.y+wall[wall.Length-1].Radius,Is.GreaterThanOrEqualTo(BossFightSpace.Height));
            for(int i=1;i<wall.Length;i++)Assert.That(wall[i].Position.y-wall[i-1].Position.y,Is.LessThanOrEqualTo(wall[i].Radius+wall[i-1].Radius));
            Advance(model,1.8+(BossFightSpace.Width+2*settings.MaximumWallRadius+1)/settings.firstWallSpeed-1e-6);
            Assert.That(model.Attacks.WallPresent,Is.True);
            foreach(var eye in wall)Assert.That(eye.Position.x+eye.Radius,Is.LessThan(.001));
            Advance(model,12.5);Assert.That(model.Attacks.WallPresent,Is.False);
        }
        [Test] public void CaptureKeepsAlreadyFiredWallAndDownloadRunning()
        {
            var model=Model();model.Teleport(new BossRect(500,400,400,300),new BossPoint(700,540));Advance(model,1.8);
            model.Teleport(new BossRect(1500,400,400,300),new BossPoint(1700,540));
            var wall=model.Eyes.First(x=>x.Kind==BossEyeKind.Wall);double x=wall.Position.x;Advance(model,2.8);
            Assert.That(wall.Position.x,Is.LessThan(x));Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(2.8));
            Assert.That(model.Attacks.WallsLaunched,Is.EqualTo(1));Assert.That(model.Attacks.ConsumedWalls,Is.EqualTo(3));
        }
        [Test] public void WallRestDoesNotSpendNormalAttackTimeOnItsLastSegment()
        {
            var model=Model();Advance(model,1);
            double remaining=model.Attacks.NormalRemaining;
            model.Teleport(new BossRect(500,400,400,300),new BossPoint(700,540));
            Advance(model,5.3);Assert.That(model.Attacks.NormalRemaining,Is.EqualTo(remaining).Within(1e-9));
            Advance(model,5.8);Assert.That(model.Attacks.NormalRemaining,Is.EqualTo(remaining-.5).Within(1e-9));
        }
        [Test] public void DownloadDeadlineStopsEveryAttackAndRespawnResetsWallChancesOnly()
        {
            settings.downloadSeconds=8;var model=Model();model.Teleport(new BossRect(500,400,400,300),new BossPoint(700,540));
            Advance(model,2);model.Respawn();Assert.That(model.Attacks.ConsumedWalls,Is.Zero);Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(2));
            Advance(model,8);int count=model.Eyes.Count;Advance(model,20);
            Assert.That(model.Outcome,Is.EqualTo(BossOutcome.BadEnding));Assert.That(model.Eyes.Count,Is.EqualTo(count));
        }
        [Test] public void LongAndShortFramesProduceSameAttackState()
        {
            var one=Model();var many=Model();Advance(one,80);
            for(int i=1;i<=800;i++)Advance(many,i*.1);
            Assert.That(one.Attacks.Cycle,Is.EqualTo(many.Attacks.Cycle));Assert.That(one.Attacks.SpeedMultiplier,Is.EqualTo(1.2).Within(1e-9));
            Assert.That(one.Eyes.Count,Is.EqualTo(many.Eyes.Count));
            for(int i=0;i<one.Eyes.Count;i++)
            {Assert.That(one.Eyes[i].Id,Is.EqualTo(many.Eyes[i].Id));Assert.That((one.Eyes[i].Position-many.Eyes[i].Position).Magnitude,Is.LessThan(.00001));}
        }
    }
}
