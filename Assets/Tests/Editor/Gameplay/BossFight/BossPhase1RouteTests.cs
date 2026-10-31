using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossPhase1RouteTests
    {
        internal readonly struct Step
        {internal readonly BossRect body;internal readonly BossPoint trash;internal Step(BossRect b,BossPoint t){body=b;trash=t;}}
        internal static List<Step> Route(BossPhaseDefinition phase)=>BuildRoute(phase,false);
        internal static List<Step> SmallRoute(BossPhaseDefinition phase)=>BuildRoute(phase,true);
        private static List<Step> BuildRoute(BossPhaseDefinition phase,bool small)
        {
            var steps=new List<Step>();var body=phase.initialWindow;var trash=phase.initialTrash;bool reachedBoss=false;steps.Add(new Step(body,trash));
            void Geometry(double l,double t,double r,double b){body=new BossRect(l,t,r-l,b-t);steps.Add(new Step(body,trash));}
            void Shape(double width,double height)
            {
                double l=trash.x-width/2,r=trash.x+width/2,t=trash.y-height/2,b=trash.y+height/2;
                // Shrink first, then expand, using one physical edge per action.
                if(width<body.width){Geometry(body.x,body.y,r,body.Bottom);Geometry(l,body.y,body.Right,body.Bottom);}
                if(height<body.height){Geometry(body.x,body.y,body.Right,b);Geometry(body.x,t,body.Right,body.Bottom);}
                if(width>body.width){Geometry(l,body.y,body.Right,body.Bottom);Geometry(body.x,body.y,r,body.Bottom);}
                if(height>body.height){Geometry(body.x,t,body.Right,body.Bottom);Geometry(body.x,body.y,body.Right,b);}
            }
            void MoveTo(double x,double y)
            {
                if(reachedBoss)return;
                var target=new BossPoint(x,y);
                for(int guard=0;guard<2000 && (target-trash).SqrMagnitude>0;guard++)
                {
                    var delta=target-trash;
                    double allowance=(delta.x!=0?body.width:body.height)*.5-39;
                    double length=delta.Magnitude;delta*=Math.Min(Math.Min(40,allowance),length)/length;
                    Geometry(body.x+delta.x,body.y+delta.y,body.Right+delta.x,body.Bottom+delta.y);
                    // Once captured, the nearby boss has input priority over the bin. Start the final drop now.
                    // Keep this raw-input route inside capture, away from camera projection roundoff at exactly 30px.
                    if(body.Contains(phase.boss,32)){reachedBoss=true;return;}
                    trash+=delta;steps.Add(new Step(body,trash));
                }
                Assert.That((target-trash).Magnitude,Is.LessThan(1e-8));
            }
            void Choose(double w,double h){if(!small)Shape(w,h);}
            if(small)Shape(90,90);else Shape(160,100);
            MoveTo(800,720);Choose(100,160);MoveTo(800,580);Choose(100,100);MoveTo(860,580);
            Choose(160,100);MoveTo(1160,580);Choose(100,100);MoveTo(1210,580);
            Choose(100,160);MoveTo(1210,300);Choose(160,100);MoveTo(1580,300);
            // Keep the final horizontal approach clear of the exact 30px capture boundary.
            // Once captured, the boss correctly wins a nearby bin grab; that must end this route.
            Choose(100,160);MoveTo(1580,724);Choose(160,100);MoveTo(1720,724);MoveTo(1720,740);
            return steps;
        }
        [TestCase(true,false)] [TestCase(false,false)] [TestCase(true,true)] [TestCase(false,true)]
        public void OpenFieldSupportsLongAndUnchangedSmallWindowRoutes(bool rightSideEyes,bool small)
        {
            var phase=ScriptableObject.CreateInstance<BossPhaseDefinition>();var settings=ScriptableObject.CreateInstance<BossFightSettings>();
            phase.includeRightSideEyes=rightSideEyes;
            settings.phase1EyeSprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/눈깔풀.png");
            try
            {
                var model=new BossPhase1Model(settings,phase,0);var route=small?SmallRoute(phase):Route(phase);bool reachedSmall=false;
                for(int i=1;i<route.Count;i++)
                {
                    var previous=route[i-1];var step=route[i];var b=step.body;
                    Assert.That(b.Contains(previous.trash,39),Is.True,"fixed bin during window move "+i);
                    Assert.That(b.Contains(step.trash,39),Is.True,"bin margin "+i);
                    Assert.That(b.width,Is.InRange(90,600));Assert.That(b.height,Is.InRange(90,510));
                    Assert.That(b.x>=0 && b.y>=36 && b.Right<=1920 && b.Bottom<=1032,Is.True,"frame "+i);
                    if(small && b.width==90 && b.height==90)reachedSmall=true;
                    if(small && reachedSmall){Assert.That(b.width,Is.EqualTo(90));Assert.That(b.height,Is.EqualTo(90));}
                    foreach(var eye in phase.ActiveEyes)Assert.That(BossCollision.Sweep(previous.body,b,eye.center,eye.center,eye.radius,out _),Is.False,"eye "+eye.center.x+","+eye.center.y+" step "+i);
                    model.Advance(i*.1,b,step.trash);model.ResolveBoundary();
                }
                Assert.That(model.Corruption,Is.Zero);Assert.That(model.Attempt,Is.EqualTo(1));Assert.That(model.Captured,Is.True);
                model.DragBoss(model.Trash);model.ResolveBoundary(successDrop:true);Assert.That(model.CombatLocked,Is.True);
                if(small)Assert.That(route.Count,Is.GreaterThan(Route(phase).Count));
            }
            finally{UnityEngine.Object.DestroyImmediate(settings);UnityEngine.Object.DestroyImmediate(phase);}
        }
        [Test] public void TrashCanCrossFormerFloorBoundaryInsideSafeWindow()
        {
            var phase=ScriptableObject.CreateInstance<BossPhaseDefinition>();var settings=ScriptableObject.CreateInstance<BossFightSettings>();
            try
            {
                var model=new BossPhase1Model(settings,phase,0);
                model.Teleport(new BossRect(300,660,300,120),new BossPoint(450,720));var input=new BossInteraction(settings);
                input.Press(model,model.Trash,false);input.Preview(model,new BossPoint(550,705),out var body,out var trash);
                model.Advance(.1,body,trash);model.ResolveBoundary();Assert.That(model.Trash.x,Is.EqualTo(550));Assert.That(model.Trash.y,Is.EqualTo(705));
                model.Teleport(new BossRect(100,300,330,240),new BossPoint(220,420));
                input.Press(model,model.Trash,false);input.Preview(model,new BossPoint(300,420),out body,out trash);
                model.Advance(.2,body,trash);Assert.That(model.Trash.x,Is.EqualTo(300),"The old map had no floor here.");
            }
            finally{UnityEngine.Object.DestroyImmediate(settings);UnityEngine.Object.DestroyImmediate(phase);}
        }
    }
}
