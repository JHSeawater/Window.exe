using System;
using System.Collections.Generic;

namespace WindowExe.BossFight
{
    public enum BossDrag { None, Move, Resize, Trash, Boss, CloseButton, MinimizeButton, Taskbar }
    public sealed class BossInteraction
    {
        private readonly BossFightSettings settings;
        private BossPoint press,startTrash;
        private BossRect startBody;
        private bool left,right,top,bottom,moved;
        public BossDrag Drag { get; private set; }
        public BossPoint Pointer { get; private set; }
        public BossInteraction(BossFightSettings settings) { this.settings=settings; }
        public void Reset() { Drag=BossDrag.None; moved=false; }
        public void Press(IBossFightModel model,BossPoint point,bool taskbar)
        {
            Reset(); Pointer=press=point; startBody=model.Body; startTrash=model.Trash;
            if(taskbar) { Drag=BossDrag.Taskbar; return; }
            if(model.Minimized || model.CombatLocked || model.Clock.IsPaused) return;
            var b=model.Body;
            int button=ButtonAt(b,point);
            if(button>=0) { Drag=button==2?BossDrag.CloseButton:BossDrag.MinimizeButton; return; }
            double outerTop=b.y-settings.titleHeight;
            double inside=settings.resizeBorder,outside=settings.resizeOuterBorder;
            bool inFrame=point.x>=b.x-outside && point.x<=b.Right+outside &&
                point.y>=outerTop-outside && point.y<=b.Bottom+outside;
            left=inFrame&&point.x<=b.x+inside;
            right=inFrame&&point.x>=b.Right-inside;
            top=inFrame&&point.y<=outerTop+inside;
            bottom=inFrame&&point.y>=b.Bottom-inside;
            if(left||right||top||bottom) { Drag=BossDrag.Resize; return; }
            if(model.Captured && (point-model.Boss).SqrMagnitude<=settings.bossGrabRadius*settings.bossGrabRadius) Drag=BossDrag.Boss;
            else if(b.Contains(point) && (point-model.Trash).SqrMagnitude<=settings.trashGrabRadius*settings.trashGrabRadius) Drag=BossDrag.Trash;
            else if(point.x>=b.x && point.x<=b.Right && point.y>=outerTop && point.y<=b.y) Drag=BossDrag.Move;
            if(Drag==BossDrag.Trash && model is BossPhase1Model first && first.Exposed){first.NotifyFrozenGrab();Drag=BossDrag.None;}
        }
        public int ButtonAt(BossRect b,BossPoint point)
        {
            if(point.y<b.y-settings.titleHeight+settings.resizeBorder || point.y>b.y || point.x>b.Right) return -1;
            if(point.x>=b.Right-26) return 2;
            if(point.x>=b.Right-52) return 0;
            return -1;
        }
        private static double Clamp(double value,double low,double high) => Math.Max(low,Math.Min(high,value));
        public void Preview(IBossFightModel model,BossPoint point,out BossRect body,out BossPoint trash)
        {
            body=model.Body; trash=model.Trash;
            if(model.Minimized || model.Clock.IsPaused || model.CombatLocked) return;
            var d=point-press; var b=startBody; double margin=settings.trashMargin;
            if(Drag==BossDrag.Move)
            {
                body=new BossRect(Clamp(b.x+d.x,Math.Max(0,trash.x+margin-b.width),Math.Min(trash.x-margin,BossFightSpace.Width-b.width)),
                    Clamp(b.y+d.y,Math.Max(settings.titleHeight,trash.y+margin-b.height),Math.Min(trash.y-margin,BossFightSpace.Height-b.height)),b.width,b.height);
            }
            else if(Drag==BossDrag.Resize)
            {
                double l=b.x,r=b.Right,t=b.y,bt=b.Bottom;
                if(left) l=Clamp(l+d.x,Math.Max(0,r-settings.maxWidth),Math.Min(trash.x-margin,r-settings.minWidth));
                if(right) r=Clamp(r+d.x,Math.Max(trash.x+margin,l+settings.minWidth),Math.Min(BossFightSpace.Width,l+settings.maxWidth));
                if(top) t=Clamp(t+d.y,Math.Max(settings.titleHeight,bt-settings.maxHeight),Math.Min(trash.y-margin,bt-settings.minHeight));
                if(bottom) bt=Clamp(bt+d.y,Math.Max(trash.y+margin,t+settings.minHeight),Math.Min(BossFightSpace.Height,t+settings.maxHeight));
                body=new BossRect(l,t,r-l,bt-t);
            }
            else if(Drag==BossDrag.Trash) trash=new BossPoint(Clamp(startTrash.x+d.x,b.x+margin,b.Right-margin),Clamp(startTrash.y+d.y,b.y+margin,b.Bottom-margin));
            if(model is BossPhase1Model first)
            {
                if(Drag==BossDrag.Trash && first.Exposed)trash=model.Trash;
            }
        }
        /// <summary>Break at clamp changes; no diagonal shortcut through an obstacle after one axis stops.</summary>
        public List<double> MotionBreaks(IBossFightModel model,BossPoint to)
        {
            var result=new List<double>{0,1};
            var from=Pointer;
            Preview(model,from,out var a,out var ta); Preview(model,to,out var b,out var tb);
            // Saturated endpoints alone cannot locate the entry into the valid range. Include the unclamped origin offset.
            if(Drag==BossDrag.Move)
            { AddLimits(from.x,to.x,press.x-startBody.x,a.x,b.x,result); AddLimits(from.y,to.y,press.y-startBody.y,a.y,b.y,result); }
            if(Drag==BossDrag.Resize)
            {
                if(left) AddLimits(from.x,to.x,press.x-startBody.x,a.x,b.x,result);
                if(right) AddLimits(from.x,to.x,press.x-startBody.Right,a.Right,b.Right,result);
                if(top) AddLimits(from.y,to.y,press.y-startBody.y,a.y,b.y,result);
                if(bottom) AddLimits(from.y,to.y,press.y-startBody.Bottom,a.Bottom,b.Bottom,result);
            }
            if(Drag==BossDrag.Trash)
            { AddLimits(from.x,to.x,press.x-startTrash.x,ta.x,tb.x,result); AddLimits(from.y,to.y,press.y-startTrash.y,ta.y,tb.y,result); }
            result.Sort(); for(int i=result.Count-1;i>0;i--) if(result[i]==result[i-1]) result.RemoveAt(i);
            return result;
        }
        private static void AddLimits(double from,double to,double offset,double a,double b,List<double> result)
        {
            if(from==to) return;
            foreach(var value in new[]{a,b}) { double t=(value+offset-from)/(to-from); if(t>0 && t<1) result.Add(t); }
        }
        public void MovePointer(IBossFightModel model,BossPoint point)
        {
            Pointer=point; moved|=(point-press).SqrMagnitude>16;
            if(Drag==BossDrag.Boss) model.DragBoss(point);
        }
        public void Release(IBossFightModel model,BossPoint point,bool taskbar,out bool toggle,out bool close,out bool drop)
        {
            toggle=!moved && (Drag==BossDrag.Taskbar&&taskbar || Drag==BossDrag.MinimizeButton&&ButtonAt(model.Body,point)==0);
            close=!moved && Drag==BossDrag.CloseButton && ButtonAt(model.Body,point)==2;
            drop=Drag==BossDrag.Boss;
            Reset();
        }
        public void Cancel(IBossFightModel model)
        {
            if(Drag==BossDrag.Move || Drag==BossDrag.Resize) model.Teleport(startBody,model.Trash);
            else if(Drag==BossDrag.Trash) model.Teleport(model.Body,startTrash);
            if(Drag==BossDrag.Boss) model.ReturnBoss();
            Reset();
        }
    }
}
