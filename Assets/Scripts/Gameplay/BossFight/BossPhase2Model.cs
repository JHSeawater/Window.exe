using System;
using System.Collections.Generic;

namespace WindowExe.BossFight
{
    public enum BossEyeKind { Starting, Scatter, Aimed, Wall, Floating }
    public sealed class BossEye
    {
        public int Id { get; internal set; }
        public BossEyeKind Kind { get; internal set; }
        public BossPoint Position { get; internal set; }
        public BossPoint Velocity { get; internal set; }
        public double Radius { get; internal set; }
    }
    /// <summary>Phase-two simulation. Views and input callbacks cannot advance its clock independently.</summary>
    public sealed class BossPhase2Model : IBossFightModel
    {
        private readonly BossFightSettings settings;
        private readonly List<BossEye> eyes=new List<BossEye>();
        private Random random;
        private int eyeId;
        private double hiddenUntil;
        private bool boundaryHit, boundaryRestore;
        public BossFightClock Clock { get; }
        public BossRect Body { get; private set; }
        public BossPoint Trash { get; private set; }
        public BossPoint Boss { get; private set; }
        public IReadOnlyList<BossEye> Eyes => eyes;
        public BossAttackDirector Attacks { get; private set; }
        public bool Minimized { get; private set; }
        public bool Captured => !Minimized && Body.Contains(Boss,settings.captureInset);
        public BossOutcome Outcome { get; private set; }
        public bool CombatLocked => Outcome!=BossOutcome.None;
        public double DownloadPercent => Clock.DownloadPercent;
        public int Attempt { get; private set; }
        public bool Invulnerable { get; set; }
        public double HiddenRemaining => Minimized?Math.Max(0,hiddenUntil-Clock.CombatTime):0;
        public event Action Respawned;
        public event Action EyeContact;
        public event Action<BossOutcome> Finished;
        public BossPhase2Model(BossFightSettings settings,double inputTime)
        {
            this.settings=settings ?? throw new ArgumentNullException(nameof(settings));
            Clock=new BossFightClock(inputTime,settings.downloadSeconds); Clock.BeginPhase2(inputTime);
            ResetCombat();
        }
        private double RandomBetween(double low,double high) => low+(high-low)*random.NextDouble();
        private double VariedRadius(double radius,bool wall=false)
        {
            // Stable per-spawn size without consuming the attack/placement random stream.
            uint hash=unchecked((uint)settings.randomSeed*747796405u+(uint)(eyeId+1)*2891336453u);
            hash=(hash^(hash>>16))*2246822519u;hash^=hash>>13;
            double result=radius*(1+settings.EyeSizeVariation*(hash/(double)uint.MaxValue*2-1));
            return wall?Math.Max(result,settings.wallSpacing*.5+.25):result;
        }
        private void ResetCombat()
        {
            Attempt++; random=new Random(settings.randomSeed); eyeId=0; eyes.Clear();
            Body=settings.phase2Window; Trash=settings.phase2Trash; Boss=settings.phase2Boss;
            Minimized=false; boundaryHit=boundaryRestore=false; hiddenUntil=0;
            Attacks=new BossAttackDirector(settings,Clock.CombatTime);
            Attacks.Fire+=SpawnAttack;
            Attacks.Accelerate+=ratio=>{foreach(var eye in eyes)eye.Velocity*=ratio;};
            Attacks.Sway+=()=>{foreach(var eye in eyes)if(eye.Kind==BossEyeKind.Scatter)eye.Velocity=new BossPoint(eye.Velocity.x,-eye.Velocity.y);};
            Attacks.WallExited+=()=>eyes.RemoveAll(e=>e.Kind==BossEyeKind.Wall);
            for(int i=0;i<7;i++)
            {
                double radius=VariedRadius(27);
                BossPoint point=default,velocity=default; bool found=false;
                for(int tries=0;tries<10000;tries++)
                {
                    point=new BossPoint(RandomBetween(510,1770),RandomBetween(60,BossFightSpace.Height-60));
                    velocity=new BossPoint(-RandomBetween(83,128),RandomBetween(-15,15));
                    if(BossCollision.Overlaps(Body.Frame(settings.titleHeight),point,192)) continue;
                    if(Math.Abs(point.y-Body.Center.y)<=225 && point.x-Body.Center.x<1170) continue;
                    var startingFrame=Body.Frame(settings.titleHeight).Inset(settings.eyeInset);
                    if(BossCollision.Sweep(startingFrame,startingFrame,point,point+velocity*7,radius,out _)) continue;
                    bool crowded=false;
                    foreach(var eye in eyes) if((eye.Position-point).SqrMagnitude<165*165) { crowded=true; break; }
                    if(crowded) continue; found=true; break;
                }
                if(!found) throw new InvalidOperationException("Starting eye placement has no safe sample.");
                AddEye(BossEyeKind.Starting,point,velocity,radius);
            }
            foreach(var eye in settings.phase2FloatingEyes??Array.Empty<BossStaticEye>())
                AddEye(BossEyeKind.Floating,eye.center,default,VariedRadius(eye.radius));
        }
        internal BossEye AddEye(BossEyeKind kind,BossPoint position,BossPoint velocity,double radius)
        {
            var eye=new BossEye {Id=++eyeId,Kind=kind,Position=position,Velocity=velocity,Radius=radius}; eyes.Add(eye); return eye;
        }
        internal void ClearEyes() => eyes.Clear();
        private void Scatter()
        {
            double radius=RandomBetween(22,32);
            AddEye(BossEyeKind.Scatter,new BossPoint(BossFightSpace.Width+radius+1,RandomBetween(40,BossFightSpace.Height-40)),
                new BossPoint(-RandomBetween(143,218),RandomBetween(-23,23))*Attacks.SpeedMultiplier,radius);
        }
        private void SpawnAttack(BossEyeKind kind,int index)
        {
            if(kind==BossEyeKind.Scatter) {Scatter();return;}
            if(kind==BossEyeKind.Wall)
            {
                var speed=(index==0?settings.firstWallSpeed:settings.wallSpeed)*Attacks.SpeedMultiplier;
                for(double y=0;y<=BossFightSpace.Height+settings.wallRadius;y+=settings.wallSpacing)
                    AddEye(kind,new BossPoint(BossFightSpace.Width+settings.MaximumWallRadius+1,y),new BossPoint(-speed,0),VariedRadius(settings.wallRadius,true));
                return;
            }
            BossPoint target=Body.Center;
            double aimedRadius=VariedRadius(settings.aimedRadius);
            bool top=target.y+aimedRadius>=settings.aimedMinimumDistance;
            bool bottom=BossFightSpace.Height+aimedRadius-target.y>=settings.aimedMinimumDistance;
            double yStart=top && (!bottom || random.Next(2)==0)?-aimedRadius-1:BossFightSpace.Height+aimedRadius+1;
            var point=new BossPoint(Math.Max(0,Math.Min(BossFightSpace.Width,target.x+RandomBetween(-settings.aimedHorizontalRange,settings.aimedHorizontalRange))),yStart);
            var direction=target-point;
            AddEye(BossEyeKind.Aimed,point,direction*(settings.aimedSpeed*Attacks.SpeedMultiplier/direction.Magnitude),aimedRadius);
        }
        private bool Near(BossPoint trash) => (trash-settings.phase2Boss).SqrMagnitude<=settings.directRange*settings.directRange;
        private void SynchronizeAttacks() => Attacks.Synchronize(Captured,Near(Trash),Trash.x);

        // Exact affine boundaries divide capture, distance and wall-threshold changes inside one input segment.
        private double NextMotionBoundary(BossRect origin,BossRect destination,BossPoint initialTrash,BossPoint targetTrash,double start,double end,double now)
        {
            double next=end;
            void Add(double fraction)
            {if(fraction<=0 || fraction>=1 || double.IsNaN(fraction))return;double t=start+(end-start)*fraction;if(t>now && t<next)next=t;}
            void Root(double from,double to) {if(from!=to)Add(-from/(to-from));}
            Root(Boss.x-origin.x-settings.captureInset,Boss.x-destination.x-settings.captureInset);
            Root(origin.Right-settings.captureInset-Boss.x,destination.Right-settings.captureInset-Boss.x);
            Root(Boss.y-origin.y-settings.captureInset,Boss.y-destination.y-settings.captureInset);
            Root(origin.Bottom-settings.captureInset-Boss.y,destination.Bottom-settings.captureInset-Boss.y);
            for(int i=0;i<2;i++)if((Attacks.ConsumedWalls&(1<<i))==0)Root(initialTrash.x-Attacks.ThresholdX(i),targetTrash.x-Attacks.ThresholdX(i));
            var offset=initialTrash-settings.phase2Boss;var delta=targetTrash-initialTrash;
            double a=delta.SqrMagnitude,b=2*(offset.x*delta.x+offset.y*delta.y),c=offset.SqrMagnitude-settings.directRange*settings.directRange;
            double discriminant=b*b-4*a*c;
            if(a>0 && discriminant>=0){var root=Math.Sqrt(discriminant);Add((-b-root)/(2*a));Add((-b+root)/(2*a));}
            return next;
        }
        private bool Overlapping()
        {
            if(Minimized || Invulnerable) return false;
            var frame=Body.Frame(settings.titleHeight).Inset(settings.eyeInset);
            foreach(var eye in eyes) if(BossCollision.Overlaps(frame,eye.Position,eye.Radius)) return true;
            return false;
        }
        private double FirstHit(BossRect destination,double seconds)
        {
            if(Minimized || Invulnerable) return double.PositiveInfinity;
            var start=Body.Frame(settings.titleHeight).Inset(settings.eyeInset);
            var end=destination.Frame(settings.titleHeight).Inset(settings.eyeInset);
            double first=double.PositiveInfinity;
            foreach(var eye in eyes)
                if(BossCollision.Sweep(start,end,eye.Position,eye.Position+eye.Velocity*seconds,eye.Radius,out var t)) first=Math.Min(first,t);
            return first;
        }
        private void MoveWorld(double seconds,BossRect body,BossPoint trash)
        {
            foreach(var eye in eyes) eye.Position+=eye.Velocity*seconds;
            Body=body; Trash=trash;
            for(int i=eyes.Count-1;i>=0;i--)
            {
                var e=eyes[i];
                if(e.Position.x+e.Radius<0 || e.Position.y+e.Radius<0 || e.Position.y-e.Radius>BossFightSpace.Height) eyes.RemoveAt(i);
            }
            Clock.Advance(Clock.InputTime+seconds);
        }
        /// <summary>One accepted linear movement segment. Endpoint hits wait for same-time input arbitration.</summary>
        public void Advance(double inputTime,BossRect destination,BossPoint trash)
        {
            if(inputTime<Clock.InputTime) throw new ArgumentOutOfRangeException(nameof(inputTime));
            if(Outcome!=BossOutcome.None || Clock.IsPaused) { Clock.Advance(inputTime); return; }
            if(inputTime==Clock.InputTime)
            {
                if(!Minimized)
                {boundaryHit|=!double.IsPositiveInfinity(FirstHit(destination,0));Body=destination;Trash=trash;}
                SynchronizeAttacks(); return;
            }
            var origin=Body; var originalTrash=Trash; var start=Clock.InputTime;
            int schedulingSteps=0;
            while(Clock.InputTime<inputTime && Outcome==BossOutcome.None)
            {
                if(++schedulingSteps>4096) throw new InvalidOperationException("Combat scheduling did not advance; no gameplay result was fabricated.");
                double motionEnd=NextMotionBoundary(origin,destination,originalTrash,trash,start,inputTime,Clock.InputTime);
                double midpoint=(Clock.InputTime+motionEnd)*.5;
                double middleFraction=(midpoint-start)/(inputTime-start);
                var middleBody=Minimized?Body:BossRect.Lerp(origin,destination,middleFraction);
                var middleTrash=Minimized?Trash:BossPoint.Lerp(originalTrash,trash,middleFraction);
                Attacks.Synchronize(!Minimized && middleBody.Contains(Boss,settings.captureInset),Near(middleTrash),middleTrash.x);
                double attackAt=Clock.InputTime+Attacks.NextDelay(out var attackDue);
                double stepEnd=motionEnd;
                double restoreAt=Minimized?Clock.InputTime+Math.Max(0,hiddenUntil-Clock.CombatTime):double.PositiveInfinity;
                stepEnd=Math.Min(stepEnd,Clock.NextDownloadInputTime);
                stepEnd=Math.Min(stepEnd,restoreAt);
                stepEnd=Math.Min(stepEnd,attackAt);
                bool restoreDue=stepEnd==restoreAt;
                double seconds=stepEnd-Clock.InputTime;
                double fraction=inputTime==start?1:(stepEnd-start)/(inputTime-start);
                var end=Minimized?Body:BossRect.Lerp(origin,destination,fraction);
                var trashEnd=Minimized?Trash:BossPoint.Lerp(originalTrash,trash,fraction);
                double hit=FirstHit(end,seconds);
                if(hit<1 || hit==1 && stepEnd<inputTime)
                {
                    double elapsed=seconds*hit;
                    var contact=BossRect.Lerp(Body,end,hit); var contactTrash=BossPoint.Lerp(Trash,trashEnd,hit);
                    MoveWorld(elapsed,contact,contactTrash);
                    if(Clock.DownloadExpired) { Finish(BossOutcome.BadEnding); break; }
                    HitEye();
                    origin=destination=Body; originalTrash=trash=Trash; start=Clock.InputTime;
                    continue;
                }
                MoveWorld(seconds,end,trashEnd);
                Attacks.AdvanceTo(Clock.CombatTime,stepEnd==attackAt?attackDue:BossAttackDue.None);
                if(Clock.DownloadExpired) { Finish(BossOutcome.BadEnding); break; }
                boundaryHit|=hit==1;
                if(Minimized && restoreDue)
                {
                    if(stepEnd==inputTime) boundaryRestore=true;
                    else
                    {
                        Minimized=false;
                        if(Overlapping())
                        {HitEye();origin=destination=Body;originalTrash=trash=Trash;start=Clock.InputTime;continue;}
                    }
                }
                SynchronizeAttacks();
                if(stepEnd==inputTime) break;
                Attacks.Resolve();
            }
        }
        public void Advance(double inputTime) => Advance(inputTime,Body,Trash);
        public void ResolveBoundary(bool toggle=false,bool dangerousClose=false,bool successDrop=false)
        {
            if(Outcome!=BossOutcome.None || Clock.IsPaused) { boundaryHit=boundaryRestore=false; return; }
            if(Clock.DownloadExpired) { Finish(BossOutcome.BadEnding); return; }
            if(dangerousClose) { Respawn(); return; }
            bool wasHidden=Minimized;
            if(toggle && !wasHidden)
            { Minimized=true; hiddenUntil=Clock.CombatTime+settings.minimizeSeconds; Boss=settings.phase2Boss; }
            else if(wasHidden && (toggle||boundaryRestore)) Minimized=false;
            bool hit=wasHidden ? !Minimized && Overlapping() : !Minimized && (boundaryHit||Overlapping());
            boundaryHit=boundaryRestore=false;
            if(hit) { HitEye(); return; }
            if(successDrop && !Minimized && Captured && (Boss-Trash).SqrMagnitude<=settings.dropRadius*settings.dropRadius) Finish(BossOutcome.Success);
            if(Outcome==BossOutcome.None) {SynchronizeAttacks();Attacks.Resolve();}
        }
        public void DragBoss(BossPoint point)
        {
            if(!Captured || Outcome!=BossOutcome.None || Clock.IsPaused) return;
            Boss=new BossPoint(Math.Max(Body.x+settings.captureInset,Math.Min(Body.Right-settings.captureInset,point.x)),
                Math.Max(Body.y+settings.captureInset,Math.Min(Body.Bottom-settings.captureInset,point.y)));
        }
        public void ReturnBoss() {Boss=settings.phase2Boss;SynchronizeAttacks();}
        public void Teleport(BossRect body,BossPoint trash)
        { Body=body; Trash=trash; boundaryHit=false; SynchronizeAttacks(); if(!Clock.IsPaused) boundaryHit=Overlapping(); }
        public void SetPause(BossPauseReason reason,bool value,double observedTime)
        {
            Advance(observedTime); ResolveBoundary();
            Clock.SetPause(reason,value,observedTime);
            if(!Clock.IsPaused) boundaryHit=Overlapping();
        }
        private void HitEye(){EyeContact?.Invoke();Respawn();}
        public bool Respawn()
        {
            if(Outcome!=BossOutcome.None || Clock.DownloadExpired) return false;
            ResetCombat(); Respawned?.Invoke(); return true;
        }
        private void Finish(BossOutcome outcome)
        {
            if(Outcome!=BossOutcome.None) return;
            Outcome=outcome; Clock.Stop(Clock.InputTime); boundaryHit=boundaryRestore=false; Finished?.Invoke(outcome);
        }
    }
}
