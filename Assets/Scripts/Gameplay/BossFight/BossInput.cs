using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WindowExe.BossFight
{
    [Flags]
    public enum BossInputFields { None=0,Position=1,Left=2,Space=4,Escape=8,Invulnerable=16,Restart=32,Skip=64,PauseDownload=128 }
    public enum BossInputKind { Position,Press,Release,Space,Escape,Cancel,Invulnerable,Restart,Skip,PauseDownload }
    public enum BossTimeKind { SourceEvent, ObservedBoundary, Scheduled }

    [Serializable]
    public struct BossInputSample
    {
        public double time, received;
        public long sequence;
        public int deviceId, eventId, batch;
        public BossInputFields fields;
        public Vector2 position;
        public bool left,space,escape,invulnerable,restart,skip,pauseDownload;
    }

    public readonly struct BossInputIntent
    {
        public readonly double Time;
        public readonly double SourceTime;
        public readonly BossTimeKind TimeKind;
        public readonly long Sequence;
        public readonly int DeviceId;
        public readonly BossInputKind Kind;
        public readonly Vector2 Position;
        public BossInputIntent(BossInputSample sample, BossInputKind kind, Vector2 position, BossTimeKind timeKind = BossTimeKind.SourceEvent)
        { SourceTime = sample.time; TimeKind = timeKind; Time = timeKind == BossTimeKind.ObservedBoundary ? sample.received : sample.time;
            Sequence = sample.sequence; DeviceId = sample.deviceId; Kind = kind; Position = position; }
    }

    [Serializable]
    public sealed class BossInputMetrics
    {
        public int samples, applied, late, beforeBaseline, invalid, consumed, intents;
        public double observedFrom, observedTo, maxDelay, delaySum;
        public int below1ms, below8ms, below16ms, below33ms, above33ms;
        public double MeanDelay => samples == 0 ? 0 : delaySum / samples;
        public void Observe(BossInputSample sample)
        {
            if (samples++ == 0) observedFrom = sample.received;
            observedTo = sample.received;
            var delay = Math.Max(0, sample.received - sample.time);
            delaySum += delay; maxDelay = Math.Max(maxDelay, delay);
            if (delay < .001) below1ms++;
            else if (delay < .008) below8ms++;
            else if (delay < .016) below16ms++;
            else if (delay < .033) below33ms++;
            else above33ms++;
        }
    }

    /// <summary>Copied input state, independent of device frame flags and combat attempts.</summary>
    public sealed class BossInputTimeline
    {
        private sealed class DeviceState
        {
            public Vector2 position;
            public bool left,space,escape,invulnerable,restart,skip,pauseDownload;
            public BossInputFields consumed;
            public double baseline;
        }
        private readonly Dictionary<int, DeviceState> states = new Dictionary<int, DeviceState>();
        private readonly List<BossInputSample> pending = new List<BossInputSample>();
        private readonly List<BossInputSample> batch = new List<BossInputSample>();
        public BossInputMetrics Metrics { get; } = new BossInputMetrics();
        public double CommittedTime { get; private set; }
        public bool Suppressed { get; private set; }
        public event Action<BossInputIntent> Intent;
        public event Action<BossInputSample, string, double> Observed;
        public BossInputTimeline(double startTime) { CommittedTime = startTime; }
        public void Baseline(int deviceId,double time,Vector2 position,bool left,bool space,bool escape,bool invulnerable=false,bool restart=false,bool skip=false,bool pauseDownload=false)
        {
            states[deviceId] = new DeviceState { position = position, left = left, space = space, escape = escape,
                invulnerable=invulnerable,restart=restart,skip=skip,pauseDownload=pauseDownload,
                baseline = time, consumed = (left ? BossInputFields.Left : 0) | (space ? BossInputFields.Space : 0) | (escape ? BossInputFields.Escape : 0) |
                    (invulnerable?BossInputFields.Invulnerable:0)|(restart?BossInputFields.Restart:0)|(skip?BossInputFields.Skip:0)|(pauseDownload?BossInputFields.PauseDownload:0) };
        }
        public void SetSuppressed(bool value)
        {
            Suppressed = value;
            ConsumeHeld();
        }
        public void ConsumeHeld()
        {
            foreach (var s in states.Values)
                s.consumed |= (s.left ? BossInputFields.Left : 0) | (s.space ? BossInputFields.Space : 0) | (s.escape ? BossInputFields.Escape : 0) |
                    (s.invulnerable?BossInputFields.Invulnerable:0)|(s.restart?BossInputFields.Restart:0)|(s.skip?BossInputFields.Skip:0)|(s.pauseDownload?BossInputFields.PauseDownload:0);
        }
        public void Receive(BossInputSample sample)
        {
            if (!Finite(sample.time) || !Finite(sample.received) ||
                ((sample.fields & BossInputFields.Position) != 0 && (!Finite(sample.position.x) || !Finite(sample.position.y))))
            { Metrics.invalid++; Observed?.Invoke(sample, "invalid", CommittedTime); return; }
            Metrics.Observe(sample);
            if (!states.TryGetValue(sample.deviceId, out var state))
            { Metrics.invalid++; Observed?.Invoke(sample, "unbound-device", CommittedTime); return; }
            if (sample.time < state.baseline)
            { Metrics.beforeBaseline++; Observed?.Invoke(sample, "before-baseline", CommittedTime); return; }
            if (sample.time < CommittedTime)
            {
                Metrics.late++;
                // A lost release must not leave a stuck drag. It is cancellation, never a new drop.
                var released = state.left && (sample.fields & BossInputFields.Left) != 0 && !sample.left;
                SyncRelease(state, sample);
                Observed?.Invoke(sample, "late-discard", CommittedTime);
                if (released)
                {
                    Metrics.intents++;
                    Intent?.Invoke(new BossInputIntent(sample, BossInputKind.Cancel, state.position, BossTimeKind.ObservedBoundary));
                }
                return;
            }
            pending.Add(sample);
        }
        public void Advance(double cutoff)
        {
            if (!Finite(cutoff) || cutoff < CommittedTime) return;
            pending.Sort(Compare);
            batch.Clear();
            int count = 0;
            while (count < pending.Count && pending[count].time <= cutoff) batch.Add(pending[count++]);
            if (count > 0) pending.RemoveRange(0, count);
            foreach (var sample in batch)
            {
                CommittedTime = sample.time;
                Apply(sample);
            }
            CommittedTime = cutoff;
            batch.Clear();
        }
        public bool TryPeek(double cutoff,out BossInputSample sample)
        {
            pending.Sort(Compare);
            if(pending.Count>0 && pending[0].time<=cutoff) { sample=pending[0]; return true; }
            sample=default; return false;
        }
        public bool PreviewPosition(double time,int deviceId,out Vector2 position)
        {
            position=default;
            pending.Sort(Compare);
            foreach(var sample in pending)
                if(sample.time==time && sample.deviceId==deviceId && (sample.fields&BossInputFields.Position)!=0)
                { position=sample.position; return true; }
            return false;
        }
        public void Clear() { pending.Clear(); batch.Clear(); states.Clear(); Intent = null; Observed = null; }
        private static int Compare(BossInputSample a, BossInputSample b)
        { int c = a.time.CompareTo(b.time); return c != 0 ? c : a.sequence.CompareTo(b.sequence); }
        private void Apply(BossInputSample sample)
        {
            if (!states.TryGetValue(sample.deviceId, out var s)) return;
            Metrics.applied++;
            bool position = (sample.fields & BossInputFields.Position) != 0 && sample.position != s.position;
            if ((sample.fields & BossInputFields.Position) != 0) s.position = sample.position;
            // Position precedes the button edge within the same physical sample.
            if (position && !Suppressed) Emit(sample, BossInputKind.Position, s.position);
            Edge(sample, s, BossInputFields.Left, sample.left, ref s.left, BossInputKind.Press, BossInputKind.Release);
            Edge(sample, s, BossInputFields.Space, sample.space, ref s.space, BossInputKind.Space, null);
            Edge(sample, s, BossInputFields.Escape, sample.escape, ref s.escape, BossInputKind.Escape, null);
            Edge(sample,s,BossInputFields.Invulnerable,sample.invulnerable,ref s.invulnerable,BossInputKind.Invulnerable,null);
            Edge(sample,s,BossInputFields.Restart,sample.restart,ref s.restart,BossInputKind.Restart,null);
            Edge(sample,s,BossInputFields.Skip,sample.skip,ref s.skip,BossInputKind.Skip,null);
            Edge(sample,s,BossInputFields.PauseDownload,sample.pauseDownload,ref s.pauseDownload,BossInputKind.PauseDownload,null);
            Observed?.Invoke(sample, Suppressed ? "suppressed-sync" : "applied", CommittedTime);
        }
        private void Edge(BossInputSample sample, DeviceState s, BossInputFields flag, bool next, ref bool current, BossInputKind down, BossInputKind? up)
        {
            if ((sample.fields & flag) == 0) return;
            bool changed = current != next;
            current = next;
            bool consumed = (s.consumed & flag) != 0 || Suppressed;
            if (!next) s.consumed &= ~flag;
            else if (Suppressed) s.consumed |= flag;
            if (!changed) return;
            if (consumed) { Metrics.consumed++; return; }
            if (next) Emit(sample, down, s.position);
            else if (up.HasValue) Emit(sample, up.Value, s.position);
        }
        private void Emit(BossInputSample sample, BossInputKind kind, Vector2 position)
        { Metrics.intents++; Intent?.Invoke(new BossInputIntent(sample, kind, position)); }
        private static void SyncRelease(DeviceState s, BossInputSample sample)
        {
            if ((sample.fields & BossInputFields.Left) != 0 && !sample.left) { s.left = false; s.consumed &= ~BossInputFields.Left; }
            if ((sample.fields & BossInputFields.Space) != 0 && !sample.space) { s.space = false; s.consumed &= ~BossInputFields.Space; }
            if ((sample.fields & BossInputFields.Escape) != 0 && !sample.escape) { s.escape = false; s.consumed &= ~BossInputFields.Escape; }
            if((sample.fields&BossInputFields.Invulnerable)!=0 && !sample.invulnerable){s.invulnerable=false;s.consumed&=~BossInputFields.Invulnerable;}
            if((sample.fields&BossInputFields.Restart)!=0 && !sample.restart){s.restart=false;s.consumed&=~BossInputFields.Restart;}
            if((sample.fields&BossInputFields.Skip)!=0 && !sample.skip){s.skip=false;s.consumed&=~BossInputFields.Skip;}
            if((sample.fields&BossInputFields.PauseDownload)!=0 && !sample.pauseDownload){s.pauseDownload=false;s.consumed&=~BossInputFields.PauseDownload;}
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Copies source events without marking them handled; temporarily disables pre-callback mouse merging.</summary>
    public sealed class BossInputSource : IDisposable
    {
        private readonly BossInputCollectionLease collectionLease;
        public readonly BossInputTimeline Timeline;
        public double CollectionTime { get; private set; }
        public int Batch { get; private set; }
        public readonly Mouse Mouse;
        public readonly Keyboard Keyboard;
        public IReadOnlyList<BossInputSample> InitialStates { get; }
        private long sequence;
        private bool disposed;
        public BossInputSource(Mouse mouse = null, Keyboard keyboard = null)
        {
            collectionLease=new BossInputCollectionLease();
            Mouse = mouse ?? UnityEngine.InputSystem.Mouse.current;
            Keyboard = keyboard ?? UnityEngine.InputSystem.Keyboard.current;
            CollectionTime = InputState.currentTime;
            Timeline = new BossInputTimeline(CollectionTime);
            var initial=new List<BossInputSample>();
            if (Mouse != null) initial.Add(new BossInputSample {deviceId=Mouse.deviceId,time=CollectionTime,received=CollectionTime,
                fields=BossInputFields.Position|BossInputFields.Left,position=Mouse.position.ReadValue(),left=Mouse.leftButton.isPressed});
            if (Keyboard != null) initial.Add(new BossInputSample {deviceId=Keyboard.deviceId,time=CollectionTime,received=CollectionTime,
                fields=BossInputFields.Space|BossInputFields.Escape|BossInputFields.Invulnerable|BossInputFields.Restart|BossInputFields.Skip|BossInputFields.PauseDownload,
                space=Keyboard.spaceKey.isPressed,escape=Keyboard.escapeKey.isPressed,invulnerable=Keyboard.f7Key.isPressed,restart=Keyboard.f8Key.isPressed,skip=Keyboard.f9Key.isPressed,pauseDownload=Keyboard.f10Key.isPressed});
            InitialStates=initial.AsReadOnly();
            foreach(var state in initial) Timeline.Baseline(state.deviceId,state.time,state.position,state.left,state.space,state.escape,state.invulnerable,state.restart,state.skip,state.pauseDownload);
            InputSystem.onEvent += OnEvent;
            InputSystem.onAfterUpdate += AfterUpdate;
        }
        private void OnEvent(InputEventPtr e, InputDevice device)
        {
            if (disposed || (InputState.currentUpdateType & InputUpdateType.Dynamic) == 0 ||
                (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>()) || (device != Mouse && device != Keyboard)) return;
            var sample = new BossInputSample { time = e.time, received = InputState.currentTime, sequence = ++sequence,
                eventId = e.id, deviceId = device.deviceId, batch = Batch + 1 };
            if (device == Mouse)
            {
                if (Mouse.position.ReadValueFromEvent(e, out var p)) { sample.fields |= BossInputFields.Position; sample.position = p; }
                if (Mouse.leftButton.ReadValueFromEvent(e, out var left)) { sample.fields |= BossInputFields.Left; sample.left = left >= Mouse.leftButton.pressPointOrDefault; }
            }
            else
            {
                if (Keyboard.spaceKey.ReadValueFromEvent(e, out var space)) { sample.fields |= BossInputFields.Space; sample.space = space >= Keyboard.spaceKey.pressPointOrDefault; }
                if (Keyboard.escapeKey.ReadValueFromEvent(e, out var escape)) { sample.fields |= BossInputFields.Escape; sample.escape = escape >= Keyboard.escapeKey.pressPointOrDefault; }
                if(Keyboard.f7Key.ReadValueFromEvent(e,out var invulnerable)){sample.fields|=BossInputFields.Invulnerable;sample.invulnerable=invulnerable>=Keyboard.f7Key.pressPointOrDefault;}
                if(Keyboard.f8Key.ReadValueFromEvent(e,out var restart)){sample.fields|=BossInputFields.Restart;sample.restart=restart>=Keyboard.f8Key.pressPointOrDefault;}
                if(Keyboard.f9Key.ReadValueFromEvent(e,out var skip)){sample.fields|=BossInputFields.Skip;sample.skip=skip>=Keyboard.f9Key.pressPointOrDefault;}
                if(Keyboard.f10Key.ReadValueFromEvent(e,out var pauseDownload)){sample.fields|=BossInputFields.PauseDownload;sample.pauseDownload=pauseDownload>=Keyboard.f10Key.pressPointOrDefault;}
            }
            if (sample.fields != BossInputFields.None) Timeline.Receive(sample);
        }
        private void AfterUpdate()
        {
            if (disposed || (InputState.currentUpdateType & InputUpdateType.Dynamic) == 0) return;
            CollectionTime = InputState.currentTime; Batch++;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            InputSystem.onEvent -= OnEvent;
            InputSystem.onAfterUpdate -= AfterUpdate;
            Timeline.Clear();
            collectionLease.Dispose();
        }
    }
}
