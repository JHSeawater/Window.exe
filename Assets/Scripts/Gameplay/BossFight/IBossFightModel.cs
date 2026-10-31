using System;
using System.Collections.Generic;

namespace WindowExe.BossFight
{
    public interface IBossFightModel
    {
        BossFightClock Clock {get;}
        BossRect Body {get;}
        BossPoint Trash {get;}
        BossPoint Boss {get;}
        IReadOnlyList<BossEye> Eyes {get;}
        bool Minimized {get;}
        bool Captured {get;}
        bool CombatLocked {get;}
        bool Invulnerable {get;set;}
        BossOutcome Outcome {get;}
        int Attempt {get;}
        double HiddenRemaining {get;}
        double DownloadPercent {get;}
        event Action Respawned;
        event Action<BossOutcome> Finished;
        void Advance(double inputTime);
        void Advance(double inputTime,BossRect body,BossPoint trash);
        void ResolveBoundary(bool toggle=false,bool dangerousClose=false,bool successDrop=false);
        void DragBoss(BossPoint point);
        void ReturnBoss();
        void Teleport(BossRect body,BossPoint trash);
        bool Respawn();
    }
}
