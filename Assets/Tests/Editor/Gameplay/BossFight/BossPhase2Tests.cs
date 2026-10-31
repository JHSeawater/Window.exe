using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossPhase2Tests
    {
        private BossFightSettings settings;
        [SetUp] public void SetUp() => settings=ScriptableObject.CreateInstance<BossFightSettings>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);
        [Test] public void FirstSevenEyesHaveSafeSpacingAndNoImmediateDeath()
        {
            var model=new BossPhase2Model(settings,0);
            Assert.That(model.Eyes.Count(x=>x.Kind==BossEyeKind.Starting),Is.EqualTo(7));
            Assert.That(model.Eyes.Count(x=>x.Kind==BossEyeKind.Floating),Is.EqualTo(4));
            for(int i=0;i<7;i++)
            {
                Assert.That(BossCollision.Overlaps(model.Body.Frame(36),model.Eyes[i].Position,165),Is.False);
                for(int j=i+1;j<7;j++) Assert.That((model.Eyes[i].Position-model.Eyes[j].Position).Magnitude,Is.GreaterThanOrEqualTo(165));
            }
            model.Advance(6.99);model.ResolveBoundary();Assert.That(model.Attempt,Is.EqualTo(1));
        }
        [Test] public void RepeatedDeathsRestoreCombatButNeverDownload()
        {
            var model=new BossPhase2Model(settings,0) {Invulnerable=true};
            for(int i=1;i<=3;i++)
            {
                model.Advance(i*5);model.ResolveBoundary(dangerousClose:true);
                Assert.That(model.Body.x,Is.EqualTo(settings.phase2Window.x));
                Assert.That(model.Trash.x,Is.EqualTo(settings.phase2Trash.x));
                Assert.That(model.Eyes.Count(x=>x.Kind==BossEyeKind.Starting),Is.EqualTo(7));
                Assert.That(model.Eyes.Count(x=>x.Kind==BossEyeKind.Floating),Is.EqualTo(4));
            }
            Assert.That(model.Attempt,Is.EqualTo(4));Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(15));
        }
        [Test] public void VariedSizesAndFloatingPlacementRepeatAcrossRespawnWithoutChangingAttackRandomness()
        {
            var model=new BossPhase2Model(settings,0){Invulnerable=true};
            var starting=model.Eyes.Where(x=>x.Kind==BossEyeKind.Starting).ToArray();
            Assert.That(starting.Select(x=>x.Radius).Distinct().Count(),Is.EqualTo(7));
            Assert.That(starting.All(x=>x.Radius>=27*(1-settings.EyeSizeVariation) && x.Radius<=27*(1+settings.EyeSizeVariation)),Is.True);
            var floating=model.Eyes.Where(x=>x.Kind==BossEyeKind.Floating).Select(x=>(x.Id,x.Position,x.Radius)).ToArray();
            model.Advance(40);model.ResolveBoundary();
            foreach(var entry in floating)
            {
                var eye=model.Eyes.Single(x=>x.Id==entry.Id);
                Assert.That((eye.Position-entry.Position).Magnitude,Is.Zero);Assert.That(eye.Velocity.Magnitude,Is.Zero);
            }
            model.Respawn();
            foreach(var entry in floating)
            {
                var eye=model.Eyes.Single(x=>x.Id==entry.Id);
                Assert.That((eye.Position-entry.Position).Magnitude,Is.Zero);Assert.That(eye.Radius,Is.EqualTo(entry.Radius));
            }
            var elapsed=model.Clock.DownloadElapsed;Assert.That(elapsed,Is.EqualTo(40));
            settings.phase2EyeSizeVariation=0;
            var uniform=new BossPhase2Model(settings,0){Invulnerable=true};
            for(int i=0;i<7;i++)Assert.That((uniform.Eyes[i].Velocity-model.Eyes[i].Velocity).Magnitude,Is.Zero);
        }
        [Test] public void CentralFloatingEyeBlocksDirectWindowSweepButLeavesUpperAndLowerRoutes()
        {
            var model=new BossPhase2Model(settings,0);
            var eye=model.Eyes.Single(x=>x.Kind==BossEyeKind.Floating && x.Position.x==960 && x.Position.y==530);
            Assert.That(eye.Velocity.Magnitude,Is.Zero);
            var start=settings.phase2Window.Frame(settings.titleHeight).Inset(settings.eyeInset);
            var end=new BossRect(1500,settings.phase2Window.y,settings.phase2Window.width,settings.phase2Window.height).Frame(settings.titleHeight).Inset(settings.eyeInset);
            Assert.That(BossCollision.Sweep(start,end,eye.Position,eye.Position,eye.Radius,out _),Is.True);
            foreach(var y in new[]{100d,760d})
            {
                var above=new BossRect(105,y,300,210).Frame(settings.titleHeight).Inset(settings.eyeInset);
                var across=new BossRect(1500,y,300,210).Frame(settings.titleHeight).Inset(settings.eyeInset);
                Assert.That(BossCollision.Sweep(above,across,eye.Position,eye.Position,eye.Radius,out _),Is.False);
            }
        }
        [Test] public void FastWindowPassThroughFloatingEyeRespawnsAndPreservesDownload()
        {
            var model=new BossPhase2Model(settings,0);var floating=model.Eyes.First(x=>x.Kind==BossEyeKind.Floating);
            var position=floating.Position;var radius=floating.Radius;model.ClearEyes();model.AddEye(BossEyeKind.Floating,position,default,radius);
            var end=new BossRect(1100,230,300,210);
            Assert.That(BossCollision.Overlaps(end.Frame(36),position,radius),Is.False);
            model.Advance(1,end,new BossPoint(1180,335));model.ResolveBoundary();
            Assert.That(model.Attempt,Is.EqualTo(2));Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(1).Within(1e-9));
        }
        [Test] public void ExactMinimizeBeatsContactButRestoreChecksOverlap()
        {
            var model=new BossPhase2Model(settings,0);model.ClearEyes();
            model.AddEye(BossEyeKind.Aimed,new BossPoint(422,500),new BossPoint(-10,0),10);
            model.Advance(1);model.ResolveBoundary(toggle:true);
            Assert.That(model.Attempt,Is.EqualTo(1));Assert.That(model.Minimized,Is.True);
            model.Advance(1.001);model.ResolveBoundary(toggle:true);
            Assert.That(model.Attempt,Is.EqualTo(2));Assert.That(model.Minimized,Is.False);
            Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(1.001).Within(1e-10));
        }
        [Test] public void EarlierContactIsNotCancelledByLaterMinimize()
        {
            var model=new BossPhase2Model(settings,0);model.ClearEyes();
            model.AddEye(BossEyeKind.Aimed,new BossPoint(422,500),new BossPoint(-10,0),10);
            model.Advance(1.0001);model.ResolveBoundary(toggle:true);
            Assert.That(model.Attempt,Is.EqualTo(2));
        }
        [Test] public void DeadlineWinsOverCloseHitAndDropOnlyOnce()
        {
            settings.downloadSeconds=1;
            var model=new BossPhase2Model(settings,0);model.ClearEyes();int finished=0,respawns=0;
            model.Finished+=_=>finished++;model.Respawned+=()=>respawns++;
            model.AddEye(BossEyeKind.Aimed,new BossPoint(422,500),new BossPoint(-10,0),10);
            model.Advance(1);model.ResolveBoundary(true,true,true);model.Advance(2);model.ResolveBoundary(true,true,true);
            Assert.That(model.Outcome,Is.EqualTo(BossOutcome.BadEnding));Assert.That(finished,Is.EqualTo(1));Assert.That(respawns,Is.Zero);
            Assert.That(model.Clock.DownloadPercent,Is.EqualTo(100));Assert.That(model.Respawn(),Is.False);
        }
        [Test] public void ForcedAndManualRestoreAtSameTimeRestoreOnce()
        {
            var model=new BossPhase2Model(settings,0);model.ResolveBoundary(toggle:true);
            model.Advance(4.5);model.ResolveBoundary(toggle:true);
            Assert.That(model.Minimized,Is.False);Assert.That(model.HiddenRemaining,Is.Zero);Assert.That(model.Attempt,Is.EqualTo(1));
        }
        [Test] public void TeleportDoesNotSweepButDestinationStillHits()
        {
            var model=new BossPhase2Model(settings,0);model.ClearEyes();
            model.AddEye(BossEyeKind.Aimed,new BossPoint(800,500),new BossPoint(0,0),10);
            model.Teleport(new BossRect(1100,435,300,210),new BossPoint(1180,540));model.ResolveBoundary();
            Assert.That(model.Attempt,Is.EqualTo(1));
            model.Teleport(new BossRect(700,435,300,210),new BossPoint(780,540));model.ResolveBoundary();
            Assert.That(model.Attempt,Is.EqualTo(2));
        }
        [Test] public void PauseDefersDestinationHitUntilAllReasonsClear()
        {
            var model=new BossPhase2Model(settings,0);model.ClearEyes();
            model.AddEye(BossEyeKind.Aimed,new BossPoint(800,500),new BossPoint(0,0),10);
            model.SetPause(BossPauseReason.FocusLost,true,1);
            model.SetPause(BossPauseReason.SpaceUnavailable,true,2);
            model.Teleport(new BossRect(700,435,300,210),new BossPoint(780,540));model.ResolveBoundary();
            Assert.That(model.Attempt,Is.EqualTo(1));
            model.SetPause(BossPauseReason.FocusLost,false,20);model.ResolveBoundary();Assert.That(model.Attempt,Is.EqualTo(1));
            model.SetPause(BossPauseReason.SpaceUnavailable,false,30);model.ResolveBoundary();
            Assert.That(model.Attempt,Is.EqualTo(2));Assert.That(model.Clock.DownloadElapsed,Is.EqualTo(1));
        }
        [Test] public void CaptureAndDropFinishAndFreezeTheDownload()
        {
            var model=new BossPhase2Model(settings,0);model.ClearEyes();
            model.Teleport(new BossRect(1500,400,400,300),new BossPoint(1700,540));
            Assert.That(model.Captured,Is.True);model.DragBoss(model.Trash);model.ResolveBoundary(successDrop:true);
            Assert.That(model.Outcome,Is.EqualTo(BossOutcome.Success));model.Advance(100);
            Assert.That(model.Clock.DownloadElapsed,Is.Zero);
        }
        [Test] public void WindowAndTrashClampsPreserveMarginAndSplitCornerMotion()
        {
            var model=new BossPhase2Model(settings,0);var interaction=new BossInteraction(settings);
            interaction.Press(model,new BossPoint(160,420),false);
            Assert.That(interaction.Drag,Is.EqualTo(BossDrag.Move));
            interaction.Preview(model,new BossPoint(2000,0),out var body,out var trash);
            Assert.That(body.Contains(trash,39),Is.True);Assert.That(body.y,Is.GreaterThanOrEqualTo(36));
            var breaks=interaction.MotionBreaks(model,new BossPoint(2000,0));Assert.That(breaks.Count,Is.GreaterThan(2));
            interaction.Press(model,model.Trash,false);interaction.Preview(model,new BossPoint(2000,2000),out body,out trash);
            Assert.That(body.Contains(trash,39),Is.True);Assert.That(body.x,Is.EqualTo(model.Body.x));
        }
        [Test] public void WiderOuterResizeKeepsTitleButtonsAndContentTargetsSeparate()
        {
            var model=new BossPhase2Model(settings,0);model.ClearEyes();
            var body=new BossRect(200,400,300,240);model.Teleport(body,new BossPoint(350,520));
            var interaction=new BossInteraction(settings);
            interaction.Press(model,new BossPoint(175.9,520),false);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.None));
            interaction.Press(model,new BossPoint(350,339.9),false);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.None));
            interaction.Press(model,new BossPoint(212,520),false);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.None));
            interaction.Press(model,new BossPoint(220,382),false);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.Move));
            interaction.Press(model,new BossPoint(487,382),false);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.CloseButton));
            interaction.Press(model,new BossPoint(461,382),false);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.MinimizeButton));
            interaction.Press(model,model.Trash,false);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.Trash));
            interaction.Press(model,new BossPoint(180,520),true);Assert.That(interaction.Drag,Is.EqualTo(BossDrag.Taskbar));
        }
    }
}
