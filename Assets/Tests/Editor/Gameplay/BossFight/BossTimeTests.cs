using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossTimeTests
    {
        [Test] public void Phase2BeginsAtThirtyAndRepeatedEntryNeverRestoresTime()
        {
            var clock=new BossFightClock(1000);
            Assert.That(clock.BeginPhase2(1005),Is.True);
            Assert.That(clock.DownloadPercent,Is.EqualTo(30));
            clock.Advance(1065);
            Assert.That(clock.BeginPhase2(1065),Is.False);
            Assert.That(clock.DownloadElapsed,Is.EqualTo(60));
            Assert.That(clock.DownloadRemaining,Is.EqualTo(180));
            Assert.That(clock.DownloadPercent,Is.EqualTo(47.5));
            Assert.That(clock.NextDownloadInputTime,Is.EqualTo(1245));
        }
        [Test] public void AllPauseReasonsMustClearWithoutCatchupOrDownloadReset()
        {
            var clock=new BossFightClock(0); clock.BeginPhase2(0);
            clock.SetPause(BossPauseReason.FocusLost,true,10);
            clock.SetPause(BossPauseReason.SpaceUnavailable,true,20);
            clock.SetPause(BossPauseReason.FocusLost,false,30);
            Assert.That(clock.IsPaused,Is.True);
            Assert.That(double.IsPositiveInfinity(clock.NextDownloadInputTime),Is.True);
            clock.SetPause(BossPauseReason.SpaceUnavailable,false,100);
            clock.Advance(105);
            Assert.That(clock.CombatTime,Is.EqualTo(15));
            Assert.That(clock.DownloadElapsed,Is.EqualTo(15));
        }
        [Test] public void DebugDownloadPauseDoesNotPauseCombatAndExpiryIsExact()
        {
            var clock=new BossFightClock(0,10); clock.BeginPhase2(0);
            clock.SetDownloadDebugPause(true,2); clock.SetDownloadDebugPause(false,12);
            Assert.That(clock.CombatTime,Is.EqualTo(12)); Assert.That(clock.DownloadElapsed,Is.EqualTo(2));
            clock.Advance(19.9999999); Assert.That(clock.DownloadExpired,Is.False);
            clock.Advance(20); Assert.That(clock.DownloadExpired,Is.True);
            Assert.That(clock.DownloadPercent,Is.EqualTo(100));
            var combat=clock.CombatTime;
            clock.Advance(200); Assert.That(clock.CombatTime,Is.EqualTo(combat));
            Assert.That(clock.DownloadExpiredAtInputTime,Is.EqualTo(20));
            Assert.That(clock.BeginPhase2(200),Is.False);
        }
        [Test] public void LongFramePreservesScheduledExpiryInsteadOfObservationTime()
        {
            var clock=new BossFightClock(100,10); clock.BeginPhase2(100);
            clock.Advance(125);
            Assert.That(clock.DownloadExpiredAtInputTime,Is.EqualTo(110));
            Assert.That(clock.NextDownloadInputTime,Is.EqualTo(110));
            Assert.That(clock.CombatTime,Is.EqualTo(10));
        }
        [Test] public void StopAndInvalidTimesCannotResumeOrRewindTheClock()
        {
            var clock=new BossFightClock(10); clock.BeginPhase2(10); clock.Stop(20);
            clock.SetPause(BossPauseReason.FocusLost,true,25); clock.SetPause(BossPauseReason.FocusLost,false,30);
            clock.Advance(40);
            Assert.That(clock.DownloadElapsed,Is.EqualTo(10));
            Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Advance(39));
            Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Advance(double.NaN));
            Assert.That(clock.InputTime,Is.EqualTo(40));
        }
        private static BossDecision Candidate(double time, BossDecisionKind kind, long sequence=1)
            =>new BossDecision(time,sequence,kind,BossTimeKind.SourceEvent,"session",2,5);
        [Test] public void EqualTimePriorityIsIndependentOfCollectionOrder()
        {
            var random=new Random(42);
            for(int repeat=0;repeat<100;repeat++)
            {
                var candidates=new List<BossDecision>();
                foreach(BossDecisionKind kind in Enum.GetValues(typeof(BossDecisionKind))) candidates.Add(Candidate(5,kind));
                for(int i=candidates.Count-1;i>0;i--) { int j=random.Next(i+1); var tmp=candidates[i]; candidates[i]=candidates[j]; candidates[j]=tmp; }
                candidates.Sort(BossDecisionOrder.Instance);
                Assert.That(candidates.ConvertAll(x=>x.Kind),Is.EqualTo(new[]{BossDecisionKind.DownloadExpired,
                    BossDecisionKind.DangerousClose,BossDecisionKind.Minimize,BossDecisionKind.EyeHit,BossDecisionKind.SuccessDrop}));
            }
        }
        [Test] public void AdjacentSourceTimesAreNotGroupedAndOldAttemptIsRejected()
        {
            var before=Candidate(1-1e-7,BossDecisionKind.SuccessDrop);
            var expiry=Candidate(1,BossDecisionKind.DownloadExpired);
            var after=Candidate(1+1e-7,BossDecisionKind.Minimize);
            Assert.That(BossDecisionOrder.Instance.Compare(before,expiry),Is.LessThan(0));
            Assert.That(BossDecisionOrder.Instance.Compare(after,expiry),Is.GreaterThan(0));
            Assert.That(before.IsCurrent("session",3),Is.False);
            Assert.That(before.IsCurrent("previous",2),Is.False);
            Assert.That(before.IsCurrent("session",2),Is.True);
        }
    }
}
