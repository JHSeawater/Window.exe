using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossInputTests
    {
        private BossInputTimeline timeline;
        private List<BossInputIntent> intents;
        [SetUp] public void SetUp()
        {
            timeline = new BossInputTimeline(0);
            timeline.Baseline(1, 0, new Vector2(100, 200), false, false, false);
            intents = new List<BossInputIntent>(); timeline.Intent += intents.Add;
        }
        private void Send(double t, BossInputFields fields, bool left = false, bool space = false, Vector2 position = default, long seq = 0)
        { timeline.Receive(new BossInputSample { time = t, received = t + .002, sequence = seq, deviceId = 1, fields = fields, left = left, space = space, position = position }); }
        [Test] public void PartialPositionPreservesHeldButtonAndButtonOnlyPreservesPosition()
        {
            Send(.1, BossInputFields.Left, true);
            Send(.2, BossInputFields.Position, position: new Vector2(140, 240));
            Send(.3, BossInputFields.Left, true);
            Send(.4, BossInputFields.Left);
            timeline.Advance(.5);
            Assert.That(intents.ConvertAll(x => x.Kind), Is.EqualTo(new[] { BossInputKind.Press, BossInputKind.Position, BossInputKind.Release }));
            Assert.That(intents[2].Position, Is.EqualTo(new Vector2(140, 240)));
        }
        [Test] public void HeldAtEntryAndRespawnRequiresReleaseAndFreshPress()
        {
            timeline.Baseline(1, 0, Vector2.zero, true, true, false);
            Send(.1, BossInputFields.Left | BossInputFields.Space, true, true);
            Send(.2, BossInputFields.Left | BossInputFields.Space);
            Send(.3, BossInputFields.Left | BossInputFields.Space, true, true);
            timeline.Advance(.3); timeline.ConsumeHeld();
            Send(.4, BossInputFields.Left | BossInputFields.Space, true, true);
            Send(.5, BossInputFields.Left | BossInputFields.Space);
            Send(.6, BossInputFields.Space, space: true);
            timeline.Advance(.7);
            Assert.That(intents.ConvertAll(x => x.Kind), Is.EqualTo(new[] { BossInputKind.Press, BossInputKind.Space, BossInputKind.Space }));
        }
        [Test] public void ReplaySortsCopiedSourceTimeAndSequenceWithoutRoundingDistinctTimes()
        {
            Send(.1000001, BossInputFields.Left, seq: 3);
            Send(.1, BossInputFields.Position, position: Vector2.one, seq: 1);
            Send(.1, BossInputFields.Left, true, seq: 2);
            timeline.Advance(.1);
            Assert.That(intents.Count, Is.EqualTo(2)); Assert.That(intents[1].Kind, Is.EqualTo(BossInputKind.Press));
            timeline.Advance(.2); Assert.That(intents[2].Kind, Is.EqualTo(BossInputKind.Release));
            Assert.That(intents[2].Time, Is.GreaterThan(intents[1].Time));
        }
        [Test] public void LateReleaseCancelsAndNeverCreatesDropOrChangesCommittedClock()
        {
            Send(.1, BossInputFields.Left, true); timeline.Advance(.5);
            timeline.Receive(new BossInputSample { time=.2, received=.55, sequence=2, deviceId=1, fields=BossInputFields.Left });
            Assert.That(intents[intents.Count - 1].Kind, Is.EqualTo(BossInputKind.Cancel));
            Assert.That(intents[intents.Count - 1].TimeKind, Is.EqualTo(BossTimeKind.ObservedBoundary));
            Assert.That(intents[intents.Count - 1].Time, Is.EqualTo(.55));
            Assert.That(intents[intents.Count - 1].SourceTime, Is.EqualTo(.2));
            Assert.That(timeline.CommittedTime, Is.EqualTo(.5)); Assert.That(timeline.Metrics.late, Is.EqualTo(1));
            Send(.6, BossInputFields.Left, true); timeline.Advance(.6);
            Assert.That(intents[intents.Count - 1].Kind, Is.EqualTo(BossInputKind.Press));
        }
        [Test] public void PauseConsumesInputButSynchronizesReleases()
        {
            Send(.1, BossInputFields.Left, true); timeline.Advance(.1); timeline.SetSuppressed(true);
            Send(.2, BossInputFields.Left); Send(.3, BossInputFields.Space, space: true); timeline.Advance(.4);
            timeline.SetSuppressed(false);
            Send(.5, BossInputFields.Space); Send(.6, BossInputFields.Left, true); timeline.Advance(.7);
            Assert.That(intents.ConvertAll(x => x.Kind), Is.EqualTo(new[] { BossInputKind.Press, BossInputKind.Press }));
        }
        [Test] public void NanAndPrebindEventsCannotGenerateIntents()
        {
            timeline.Baseline(1, .1, Vector2.one, false, false, false);
            Send(.05, BossInputFields.Left, true); Send(double.NaN, BossInputFields.Left, true); timeline.Advance(1);
            Assert.That(intents, Is.Empty); Assert.That(timeline.Metrics.beforeBaseline, Is.EqualTo(1)); Assert.That(timeline.Metrics.invalid, Is.EqualTo(1));
        }
    }
}
