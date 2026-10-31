using System;
using System.Collections.Generic;

namespace WindowExe.BossFight
{
    [Flags] public enum BossPauseReason { None=0, FocusLost=1, ApplicationPaused=2, SpaceUnavailable=4 }

    /// <summary>One input clock and one cumulative download. Combat respawns have no reset operation here.</summary>
    public sealed class BossFightClock
    {
        public double InputTime { get; private set; }
        public double CombatTime { get; private set; }
        public double DownloadElapsed { get; private set; }
        public double DownloadDuration { get; }
        public double DownloadRemaining => Math.Max(0,DownloadDuration-DownloadElapsed);
        public double DownloadPercent => 30+70*DownloadElapsed/DownloadDuration;
        public bool DownloadStarted { get; private set; }
        public bool DownloadExpired => DownloadStarted && DownloadElapsed>=DownloadDuration;
        public bool DownloadDebugPaused { get; private set; }
        public BossPauseReason PauseReasons { get; private set; }
        public bool Stopped { get; private set; }
        public double? DownloadExpiredAtInputTime { get; private set; }
        public bool IsPaused => PauseReasons!=BossPauseReason.None;
        public double NextDownloadInputTime => !DownloadStarted || IsPaused || DownloadDebugPaused || Stopped
            ? double.PositiveInfinity : DownloadExpiredAtInputTime ?? InputTime+DownloadRemaining;
        public BossFightClock(double inputTime, double downloadDuration=240)
        {
            RequireFinite(inputTime);
            RequireFinite(downloadDuration);
            if(downloadDuration<=0) throw new ArgumentOutOfRangeException(nameof(downloadDuration));
            InputTime=inputTime; DownloadDuration=downloadDuration;
        }
        public double Advance(double inputTime)
        {
            RequireFinite(inputTime);
            if(inputTime<InputTime) throw new ArgumentOutOfRangeException(nameof(inputTime),"A committed clock cannot rewind.");
            var delta=inputTime-InputTime;
            var previousInputTime=InputTime;
            InputTime=inputTime;
            if(Stopped || IsPaused || DownloadExpired) return 0;
            if(DownloadStarted && !DownloadDebugPaused)
            {
                // The executor resolves the terminal candidate at this boundary before later input.
                bool expires=inputTime>=previousInputTime+DownloadRemaining;
                if(expires) DownloadExpiredAtInputTime=previousInputTime+DownloadRemaining;
                delta=Math.Min(delta,DownloadRemaining);
                DownloadElapsed=expires?DownloadDuration:Math.Min(DownloadDuration,DownloadElapsed+delta);
            }
            CombatTime+=delta;
            return delta;
        }
        public bool BeginPhase2(double inputTime)
        {
            Advance(inputTime);
            if(DownloadStarted || Stopped) return false;
            DownloadStarted=true; return true;
        }
        public void SetPause(BossPauseReason reason, bool active, double observedTime)
        {
            Advance(observedTime);
            if(active) PauseReasons|=reason; else PauseReasons&=~reason;
        }
        public void SetDownloadDebugPause(bool value, double inputTime)
        { Advance(inputTime); DownloadDebugPaused=value; }
        public void Stop(double inputTime) { Advance(inputTime); Stopped=true; }
        private static void RequireFinite(double value)
        { if(double.IsNaN(value)||double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
    }

    public enum BossDecisionKind { DownloadExpired, DangerousClose, Minimize, EyeHit, SuccessDrop }

    /// <summary>Only valid candidates enter arbitration. Distinct source times are never rounded together.</summary>
    public readonly struct BossDecision
    {
        public readonly double Time;
        public readonly long Sequence;
        public readonly BossDecisionKind Kind;
        public readonly BossTimeKind TimeKind;
        public readonly string SessionId;
        public readonly int Attempt, Interaction;
        public BossDecision(double time, long sequence, BossDecisionKind kind, BossTimeKind timeKind,
            string sessionId, int attempt, int interaction)
        {
            if(double.IsNaN(time)||double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            Time=time; Sequence=sequence; Kind=kind; TimeKind=timeKind; SessionId=sessionId; Attempt=attempt; Interaction=interaction;
        }
        public bool IsCurrent(string sessionId, int attempt) => SessionId==sessionId && Attempt==attempt;
    }
    public sealed class BossDecisionOrder : IComparer<BossDecision>
    {
        public static readonly BossDecisionOrder Instance=new BossDecisionOrder();
        public int Compare(BossDecision a, BossDecision b)
        {
            var time=a.Time.CompareTo(b.Time); if(time!=0) return time;
            var priority=a.Kind.CompareTo(b.Kind); return priority!=0?priority:a.Sequence.CompareTo(b.Sequence);
        }
    }
}
