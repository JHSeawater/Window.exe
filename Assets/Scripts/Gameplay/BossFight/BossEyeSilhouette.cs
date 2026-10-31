using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowExe.BossFight
{
    /// <summary>Fixed authored eye outline, normalized by sprite width. No texture readback or physics simulation.</summary>
    public sealed class BossEyeSilhouette
    {
        private readonly List<BossPoint[]> contours=new List<BossPoint[]>();
        private readonly double aspect;
        private readonly BossRect bounds;
        public bool UsesArtwork=>contours.Count>0;

        public BossEyeSilhouette(Sprite sprite):this(ReadOutline(sprite),sprite!=null?sprite.rect.height/sprite.rect.width:1){}
        internal BossEyeSilhouette(BossPoint[][] outlines,double aspectRatio)
        {
            aspect=aspectRatio;
            double left=double.PositiveInfinity,top=left,right=double.NegativeInfinity,bottom=right;
            foreach(var points in outlines)
            {
                if(points.Length<3)continue;
                var contour=(BossPoint[])points.Clone();contours.Add(contour);
                foreach(var p in contour){left=Math.Min(left,p.x);right=Math.Max(right,p.x);top=Math.Min(top,p.y);bottom=Math.Max(bottom,p.y);}
            }
            if(UsesArtwork)bounds=new BossRect(left,top,right-left,bottom-top);
        }
        private static BossPoint[][] ReadOutline(Sprite sprite)
        {
            if(sprite==null)return Array.Empty<BossPoint[]>(); // Isolated fixtures may omit artwork.
            var result=new List<BossPoint[]>();var shift=sprite.pivot-sprite.rect.size*.5f;
            for(int shape=0;shape<sprite.GetPhysicsShapeCount();shape++)
            {
                var points=new List<Vector2>();sprite.GetPhysicsShape(shape,points);if(points.Count<3)continue;
                var contour=new BossPoint[points.Count];
                for(int i=0;i<points.Count;i++)contour[i]=new BossPoint((points[i].x*sprite.pixelsPerUnit+shift.x)/sprite.rect.width,
                    -(points[i].y*sprite.pixelsPerUnit+shift.y)/sprite.rect.width);
                result.Add(contour);
            }
            if(result.Count==0)throw new InvalidOperationException("Phase-one eye artwork requires an imported physics outline.");
            return result.ToArray();
        }

        public Vector2 Size(double radius)=>new Vector2((float)(radius*2),(float)(radius*2*aspect));
        private static BossRect Local(BossRect body,BossPoint center,double diameter)=>new BossRect(
            (body.x-center.x)/diameter,(body.y-center.y)/diameter,body.width/diameter,body.height/diameter);
        private static bool Intersects(BossRect a,BossRect b)=>a.x<=b.Right && a.Right>=b.x && a.y<=b.Bottom && a.Bottom>=b.y;
        private static BossPoint Center(BossRect rect)=>new BossPoint(rect.x+rect.width*.5,rect.y+rect.height*.5);
        private bool Contains(BossPoint p)
        {
            bool inside=false;
            foreach(var contour in contours)
                for(int i=0,j=contour.Length-1;i<contour.Length;j=i++)
                {
                    var a=contour[i];var b=contour[j];
                    if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                }
            return inside;
        }
        public bool Overlaps(BossRect body,BossPoint center,double radius,double warningMargin=0)
        {
            if(!UsesArtwork)return BossCollision.Overlaps(body,center,radius+warningMargin);
            var local=Local(body.Inset(-warningMargin),center,radius*2);
            if(!Intersects(local,bounds))return false;
            if(Contains(Center(local)))return true;
            foreach(var contour in contours)
                for(int i=0,j=contour.Length-1;i<contour.Length;j=i++)
                    if(Segment(local,local,contour[j],contour[i],out _,out _))return true;
            return false;
        }
        public void Intervals(BossRect from,BossRect to,BossPoint center,double radius,List<BossInterval> result)
        {
            if(!UsesArtwork){BossCollision.Intervals(from,to,center,center,radius,result);return;}
            var a=Local(from,center,radius*2);var b=Local(to,center,radius*2);
            var swept=new BossRect(Math.Min(a.x,b.x),Math.Min(a.y,b.y),0,0);
            swept.width=Math.Max(a.Right,b.Right)-swept.x;swept.height=Math.Max(a.Bottom,b.Bottom)-swept.y;
            if(!Intersects(swept,bounds))return;
            var contacts=new List<BossInterval>();
            foreach(var contour in contours)
                for(int i=0,j=contour.Length-1;i<contour.Length;j=i++)
                    if(Segment(a,b,contour[j],contour[i],out double enter,out double exit))contacts.Add(new BossInterval(enter,exit));
            BossCollision.Union(contacts);
            double cursor=0;
            foreach(var contact in contacts)
            {
                if(contact.Start>cursor && Contains(Center(BossRect.Lerp(a,b,(cursor+contact.Start)*.5))))
                    result.Add(new BossInterval(cursor,contact.Start));
                result.Add(contact);cursor=contact.End;
            }
            if(cursor<1 && Contains(Center(BossRect.Lerp(a,b,(cursor+1)*.5))))result.Add(new BossInterval(cursor,1));
        }
        // A rectangle with linearly changing edges against a fixed segment is convex in time.
        // Clip its projections on x, y and the segment normal; no movement subdivisions are needed.
        private static bool Segment(BossRect from,BossRect to,BossPoint a,BossPoint b,out double enter,out double exit)
        {
            enter=0;exit=1;
            return Axis(from,to,1,0,Math.Min(a.x,b.x),Math.Max(a.x,b.x),ref enter,ref exit)
                && Axis(from,to,0,1,Math.Min(a.y,b.y),Math.Max(a.y,b.y),ref enter,ref exit)
                && Axis(from,to,a.y-b.y,b.x-a.x,(a.y-b.y)*a.x+(b.x-a.x)*a.y,
                    (a.y-b.y)*a.x+(b.x-a.x)*a.y,ref enter,ref exit);
        }
        private static bool Axis(BossRect from,BossRect to,double x,double y,double low,double high,ref double enter,ref double exit)
        {
            double minA=x*(x>=0?from.x:from.Right)+y*(y>=0?from.y:from.Bottom);
            double maxA=x*(x>=0?from.Right:from.x)+y*(y>=0?from.Bottom:from.y);
            double minB=x*(x>=0?to.x:to.Right)+y*(y>=0?to.y:to.Bottom);
            double maxB=x*(x>=0?to.Right:to.x)+y*(y>=0?to.Bottom:to.y);
            return Clip(maxA-low,maxB-maxA,ref enter,ref exit) && Clip(high-minA,minA-minB,ref enter,ref exit);
        }
        private static bool Clip(double start,double delta,ref double enter,ref double exit)
        {
            if(delta==0)return start>=0;
            double crossing=-start/delta;
            if(delta>0)enter=Math.Max(enter,crossing);else exit=Math.Min(exit,crossing);
            return enter<=exit;
        }
    }
}
