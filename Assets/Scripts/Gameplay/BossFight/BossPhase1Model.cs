using System;
using System.Collections.Generic;

namespace WindowExe.BossFight
{
    public sealed class BossPhase1Model : IBossFightModel
    {
        private readonly BossFightSettings settings;
        private readonly List<BossEye> eyes=new List<BossEye>();
        private double hiddenUntil,downloadElapsed,downloadAtDrop,dropTime;
        private bool scored,pendingCorruption,pendingRestore;
        private double frozenGrabAt=double.NegativeInfinity;
        public BossPhaseDefinition Definition {get;}
        public BossEyeSilhouette EyeShape {get;}
        public BossFightClock Clock {get;}
        public BossRect Body {get;private set;}
        public BossPoint Trash {get;private set;}
        public BossPoint Boss {get;private set;}
        public IReadOnlyList<BossEye> Eyes=>eyes;
        public bool Minimized {get;private set;}
        public bool Captured=>!Minimized && Body.Contains(Boss,settings.captureInset);
        public bool CombatLocked {get;private set;}
        public bool Invulnerable {get;set;}
        public BossOutcome Outcome=>BossOutcome.None;
        public int Attempt {get;private set;}
        public int Corruption {get;private set;}
        public double Exposure {get;private set;}
        public bool Exposed=>!Minimized && Overlapping(0);
        public bool NearEye=>!Minimized && Overlapping(Definition.warningDistance);
        public double HiddenRemaining=>Minimized?Math.Max(0,hiddenUntil-Clock.CombatTime):0;
        public double DownloadPercent=>CombatLocked?downloadAtDrop:10+20*Math.Min(1,downloadElapsed/90);
        public double TransitionAge=>CombatLocked?Clock.CombatTime-dropTime:0;
        public double FrozenGrabAge=>Clock.CombatTime-frozenGrabAt;
        public void NotifyFrozenGrab()=>frozenGrabAt=Clock.CombatTime;
        public event Action Respawned,Corrupted,FirstDrop;
        public event Action<BossOutcome> Finished {add{} remove{}}
        public BossPhase1Model(BossFightSettings settings,BossPhaseDefinition definition,double inputTime)
        {this.settings=settings;Definition=definition??throw new ArgumentNullException(nameof(definition));EyeShape=new BossEyeSilhouette(settings.phase1EyeSprite);Clock=new BossFightClock(inputTime);Reset();}
        private void Reset()
        {
            Attempt++;Body=Definition.initialWindow;Trash=Definition.initialTrash;Boss=Definition.boss;
            Corruption=0;Exposure=0;scored=pendingCorruption=pendingRestore=Minimized=CombatLocked=false;
            frozenGrabAt=double.NegativeInfinity;
            downloadElapsed=0;eyes.Clear();int id=0;
            foreach(var eye in Definition.ActiveEyes)eyes.Add(new BossEye{Id=++id,Kind=BossEyeKind.Starting,Position=eye.center,Radius=eye.radius});
        }
        private bool Overlapping(double margin)
        {foreach(var eye in eyes)if(EyeShape.Overlaps(Body,eye.Position,eye.Radius,margin))return true;return false;}
        private void ClearExposure(){Exposure=0;scored=pendingCorruption=false;}
        private void AdvanceClock(double time)
        {double elapsed=Clock.Advance(time);if(!Clock.DownloadDebugPaused && !CombatLocked)downloadElapsed+=elapsed;}
        private bool AddCorruption()
        {
            pendingCorruption=false;scored=true;
            if(Invulnerable)return false;
            Corruption++;Corrupted?.Invoke();
            if(Corruption<Definition.corruptionLimit)return false;
            Respawn();return true;
        }
        public void Advance(double inputTime)=>Advance(inputTime,Body,Trash);
        public void Advance(double inputTime,BossRect destination,BossPoint trash)
        {
            if(inputTime<Clock.InputTime)throw new ArgumentOutOfRangeException(nameof(inputTime));
            if(Clock.IsPaused || CombatLocked){AdvanceClock(inputTime);return;}
            if(Minimized)
            {
                double restoreAt=Clock.InputTime+HiddenRemaining;
                if(inputTime<=restoreAt){AdvanceClock(inputTime);pendingRestore=inputTime==restoreAt;return;}
                AdvanceClock(restoreAt);Minimized=false;ClearExposure();
                // Restoration resumes exposure at its destination, with no imaginary travel path.
                Advance(inputTime,Body,Trash);return;
            }
            double start=Clock.InputTime,seconds=inputTime-start;
            var from=Body;var oldTrash=Trash;var intervals=new List<BossInterval>();
            foreach(var eye in eyes)EyeShape.Intervals(from,destination,eye.Position,eye.Radius,intervals);
            BossCollision.Union(intervals);
            bool frozen=intervals.Count>0;
            double previousEnd=0;
            foreach(var interval in intervals)
            {
                if(interval.Start>previousEnd)ClearExposure();
                double duration=(interval.End-interval.Start)*seconds;
                if(!scored && Exposure+duration>=Definition.exposureSeconds)
                {
                    double at=start+interval.Start*seconds+Definition.exposureSeconds-Exposure;
                    Exposure=Definition.exposureSeconds;
                    if(at>=inputTime)pendingCorruption=true;
                    else
                    {
                        AdvanceClock(at);
                        if(AddCorruption()){Advance(inputTime,Body,Trash);return;}
                    }
                }
                else if(!scored)Exposure+=duration;
                previousEnd=interval.End;
            }
            if(intervals.Count==0 || previousEnd<1)ClearExposure();
            Body=destination;Trash=frozen?oldTrash:trash;AdvanceClock(inputTime);
        }
        public void ResolveBoundary(bool toggle=false,bool dangerousClose=false,bool successDrop=false)
        {
            if(CombatLocked || Clock.IsPaused)return;
            if(dangerousClose){Respawn();return;}
            bool hidden=Minimized;
            if(toggle && !hidden){Minimized=true;hiddenUntil=Clock.CombatTime+settings.minimizeSeconds;ClearExposure();ReturnBoss();}
            else if(hidden && (toggle || pendingRestore)){Minimized=false;ClearExposure();}
            pendingRestore=false;
            if(pendingCorruption && !Minimized && AddCorruption())return;
            if(successDrop && Captured && (Boss-Trash).SqrMagnitude<=settings.dropRadius*settings.dropRadius)
            {downloadAtDrop=DownloadPercent;dropTime=Clock.CombatTime;CombatLocked=true;FirstDrop?.Invoke();}
        }
        public void DragBoss(BossPoint point)
        {
            if(!Captured || CombatLocked || Clock.IsPaused)return;
            Boss=new BossPoint(Math.Max(Body.x+settings.captureInset,Math.Min(Body.Right-settings.captureInset,point.x)),
                Math.Max(Body.y+settings.captureInset,Math.Min(Body.Bottom-settings.captureInset,point.y)));
        }
        public void ReturnBoss()=>Boss=Definition.boss;
        public void Teleport(BossRect body,BossPoint trash)
        {Body=body;Trash=trash;if(!Clock.IsPaused && !Exposed)ClearExposure();}
        public bool Respawn(){if(CombatLocked)return false;Reset();Respawned?.Invoke();return true;}
    }
}
