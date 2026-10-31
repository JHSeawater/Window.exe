using System;
using System.Collections.Generic;

namespace WindowExe.BossFight
{
    [Flags] internal enum BossAttackDue { None=0, Stage=1, Shot=2, Direct=4, Warning=8, Rest=16, WallExit=32, Sway=64 }

    /// <summary>Scheduled deadlines remain unchanged between state transitions; no per-frame countdown drift.</summary>
    public sealed class BossAttackDirector
    {
        private sealed class Timer
        {
            private double deadline,remaining=double.PositiveInfinity;
            private bool running;
            public double Left(double now)=>running?Math.Max(0,deadline-now):remaining;
            public void Set(double seconds,double now,bool run=true)
            {remaining=seconds;running=run;deadline=now+seconds;}
            public void Run(bool value,double now)
            {if(value==running)return;remaining=Left(now);running=value;if(value)deadline=now+remaining;}
            public void Due(double now){remaining=0;deadline=now;}
        }
        private readonly BossFightSettings settings;
        private readonly Queue<int> walls=new Queue<int>();
        private readonly Timer stageTimer=new Timer(),shotTimer=new Timer(),directTimer=new Timer(),
            warningTimer=new Timer(),restTimer=new Timer(),wallTimer=new Timer(),swayTimer=new Timer();
        private double now;
        private int stage,shots,warningIndex;
        private bool captured,near,wallPresent;
        public int ConsumedWalls {get;private set;}
        public int WallsLaunched {get;private set;}
        public int PendingWalls=>walls.Count;
        public int Cycle {get;private set;}
        public double SpeedMultiplier=>1+Math.Min(Cycle,5)*.05;
        public bool Warning {get;private set;}
        public double WarningRemaining=>Warning?warningTimer.Left(now):0;
        public bool WallPresent=>wallPresent;
        public double NormalRemaining=>stageTimer.Left(now);
        public double DirectRemaining=>directTimer.Left(now);
        public bool AttacksPaused=>captured || Warning || restTimer.Left(now)>0;
        internal event Action<BossEyeKind,int> Fire;
        internal event Action<double> Accelerate;
        internal event Action WallExited,Sway;
        public BossAttackDirector(BossFightSettings settings,double combatTime=0)
        {
            this.settings=settings;now=combatTime;stageTimer.Set(settings.attackDurations[0],now);
            directTimer.Set(settings.directInterval,now,false);restTimer.Set(0,now,false);
            swayTimer.Set(settings.scatterSwayInterval,now);
        }
        internal double ThresholdX(int index)=>settings.phase2Trash.x+(settings.phase2Boss.x-90-settings.phase2Trash.x)*(index==0?.3:.7);
        private void RefreshRunning()
        {stageTimer.Run(!AttacksPaused,now);shotTimer.Run(!AttacksPaused,now);directTimer.Run(!AttacksPaused && near,now);}
        internal void Synchronize(bool isCaptured,bool isNear,double trashX)
        {
            captured=isCaptured;near=isNear;
            for(int i=0;i<2;i++)if((ConsumedWalls&(1<<i))==0 && trashX>=ThresholdX(i))
            {ConsumedWalls|=1<<i;if(!captured)walls.Enqueue(i);}
            if(captured){walls.Clear();Warning=false;}
            if(!captured && !Warning && restTimer.Left(now)<=0 && !wallPresent && walls.Count>0)
            {warningIndex=walls.Dequeue();Warning=true;warningTimer.Set(settings.wallWarningSeconds,now);}
            RefreshRunning();
        }
        internal double NextDelay(out BossAttackDue due)
        {
            double next=double.PositiveInfinity;var result=BossAttackDue.None;
            void Consider(Timer timer,BossAttackDue kind)
            {var value=timer.Left(now);if(value<next){next=value;result=kind;}else if(value==next)result|=kind;}
            Consider(swayTimer,BossAttackDue.Sway);
            if(Warning)Consider(warningTimer,BossAttackDue.Warning);
            if(restTimer.Left(now)>0)Consider(restTimer,BossAttackDue.Rest);
            if(wallPresent)Consider(wallTimer,BossAttackDue.WallExit);
            if(!AttacksPaused)
            {Consider(stageTimer,BossAttackDue.Stage);Consider(shotTimer,BossAttackDue.Shot);if(near)Consider(directTimer,BossAttackDue.Direct);}
            due=result;return next;
        }
        internal void AdvanceTo(double combatTime,BossAttackDue due)
        {
            now=combatTime;
            // The model selected these exact scheduled timestamps before advancing the common clock.
            void Mark(Timer timer,BossAttackDue kind){if((due&kind)!=0)timer.Due(now);}
            Mark(stageTimer,BossAttackDue.Stage);Mark(shotTimer,BossAttackDue.Shot);Mark(directTimer,BossAttackDue.Direct);
            Mark(warningTimer,BossAttackDue.Warning);Mark(restTimer,BossAttackDue.Rest);Mark(wallTimer,BossAttackDue.WallExit);Mark(swayTimer,BossAttackDue.Sway);
            if(wallPresent && wallTimer.Left(now)==0){wallPresent=false;WallExited?.Invoke();}
        }
        internal void Resolve()
        {
            if(swayTimer.Left(now)==0){swayTimer.Set(settings.scatterSwayInterval,now);Sway?.Invoke();}
            if(Warning && warningTimer.Left(now)==0 && !captured)
            {
                Warning=false;restTimer.Set(settings.wallAttackPauseSeconds,now);WallsLaunched++;wallPresent=true;
                double speed=(warningIndex==0?settings.firstWallSpeed:settings.wallSpeed)*SpeedMultiplier;
                wallTimer.Set((BossFightSpace.Width+2*settings.MaximumWallRadius+1)/speed,now);
                Fire?.Invoke(BossEyeKind.Wall,warningIndex);
            }
            RefreshRunning();
            if(AttacksPaused)return;
            for(int guard=0;guard<4;guard++)
            {
                if(shotTimer.Left(now)==0)
                {
                    Fire?.Invoke(stage==3?BossEyeKind.Aimed:BossEyeKind.Scatter,0);shots++;
                    shotTimer.Set(stage==3 && shots>=3?double.PositiveInfinity:stage==3?settings.aimedInterval:settings.scatterInterval,now);
                }
                if(stageTimer.Left(now)>0)break;
                stage++;
                if(stage>=settings.attackDurations.Length)
                {
                    var before=SpeedMultiplier;stage=0;Cycle++;var ratio=SpeedMultiplier/before;
                    if(wallPresent)wallTimer.Set(wallTimer.Left(now)/ratio,now);
                    Accelerate?.Invoke(ratio);
                }
                stageTimer.Set(settings.attackDurations[stage],now);shots=0;
                shotTimer.Set(stage==1 || stage==3 || stage==5?0:double.PositiveInfinity,now);
            }
            if(near && directTimer.Left(now)==0){Fire?.Invoke(BossEyeKind.Aimed,0);directTimer.Set(settings.directInterval,now);}
        }
    }
}
