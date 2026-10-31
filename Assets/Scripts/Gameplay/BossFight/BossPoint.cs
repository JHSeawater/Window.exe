using System;
using UnityEngine;

namespace WindowExe.BossFight
{
    [Serializable] public struct BossPoint
    {
        public double x,y;
        public BossPoint(double x,double y) { this.x=x; this.y=y; }
        public double SqrMagnitude => x*x+y*y;
        public double Magnitude => Math.Sqrt(SqrMagnitude);
        public Vector2 Vector => new Vector2((float)x,(float)y);
        public static implicit operator BossPoint(Vector2 p) => new BossPoint(p.x,p.y);
        public static BossPoint operator +(BossPoint a,BossPoint b) => new BossPoint(a.x+b.x,a.y+b.y);
        public static BossPoint operator -(BossPoint a,BossPoint b) => new BossPoint(a.x-b.x,a.y-b.y);
        public static BossPoint operator *(BossPoint a,double k) => new BossPoint(a.x*k,a.y*k);
        public static BossPoint Lerp(BossPoint a,BossPoint b,double t) => a+(b-a)*t;
    }
}
