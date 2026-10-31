namespace WindowExe.Desktop
{
    /// <summary>Only the current owner may change the shared effect; stale cleanup cannot erase an ending.</summary>
    public sealed class CRTControlLease
    {
        private readonly CRTOverlay effect;
        private readonly long generation;
        internal CRTControlLease(CRTOverlay effect, long generation) { this.effect = effect; this.generation = generation; }
        public bool IsCurrent => effect != null && effect.IsControlCurrent(generation);
        public bool SetLevel(int value) { if (!IsCurrent) return false; effect.SetLevel(value); return true; }
        public bool Spike(float seconds = .3f) { if (!IsCurrent) return false; effect.Spike(seconds); return true; }
        public bool ResetVisuals() { if (!IsCurrent) return false; effect.ResetVisuals(); return true; }
        public bool SetBaseHorizontalNoise(float scanlineScale, float streakScale, float densityScale)
            => effect != null && effect.SetBaseHorizontalNoise(generation, scanlineScale, streakScale, densityScale);
        public CRTControlLease Transfer() => IsCurrent ? effect.TransferControl(generation) : null;
        public bool Release() => effect != null && effect.ReleaseControl(generation);
    }
}
