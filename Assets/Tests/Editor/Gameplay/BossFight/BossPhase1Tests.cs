using NUnit.Framework;
using UnityEngine;

namespace WindowExe.BossFight.Tests
{
    public sealed class BossPhase1Tests
    {
        private BossFightSettings settings;
        private BossPhaseDefinition definition;
        [SetUp] public void SetUp(){settings=ScriptableObject.CreateInstance<BossFightSettings>();definition=ScriptableObject.CreateInstance<BossPhaseDefinition>();settings.phase1EyeSprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/눈깔풀.png");}
        [TearDown] public void TearDown(){Object.DestroyImmediate(settings);Object.DestroyImmediate(definition);}
        private BossPhase1Model Model()=>new BossPhase1Model(settings,definition,0);
        [Test] public void AuthoredRouteRunsWithoutExposureAndEndsWithFirstDropOnly()
        {
            var model=Model();int dropped=0;model.FirstDrop+=()=>dropped++;
            var route=BossPhase1RouteTests.Route(definition);
            for(int i=1;i<route.Count;i++)
            {model.Advance(i*.2,route[i].body,route[i].trash);model.ResolveBoundary();Assert.That(model.Corruption,Is.Zero,"step "+i);Assert.That(model.Attempt,Is.EqualTo(1));}
            Assert.That(model.Captured,Is.True);model.DragBoss(model.Trash);model.ResolveBoundary(successDrop:true);
            model.ResolveBoundary(successDrop:true);Assert.That(dropped,Is.EqualTo(1));Assert.That(model.CombatLocked,Is.True);
            Assert.That(model.Outcome,Is.EqualTo(BossOutcome.None));Assert.That(model.Clock.DownloadStarted,Is.False);
        }
        [Test] public void SeveralEyesShareExposureAndRequireCompleteExit()
        {
            definition.eyes=new[]{new BossStaticEye(600,700,50),new BossStaticEye(600,700,50)};var model=Model();
            var exposed=new BossRect(550,600,100,240);model.Teleport(exposed,new BossPoint(600,720));
            model.Advance(.599);model.ResolveBoundary();Assert.That(model.Corruption,Is.Zero);
            model.Advance(.6);model.ResolveBoundary();Assert.That(model.Corruption,Is.EqualTo(1));
            model.Advance(2);model.ResolveBoundary();Assert.That(model.Corruption,Is.EqualTo(1));
            model.Teleport(definition.initialWindow,definition.initialTrash);model.Teleport(exposed,new BossPoint(600,720));
            model.Advance(2.6);model.ResolveBoundary();Assert.That(model.Corruption,Is.EqualTo(2));
        }
        [Test] public void GapInsideOneLongFrameResetsExposureInsteadOfSummingBothEyes()
        {
            definition.initialWindow=new BossRect(0,300,100,240);definition.initialTrash=new BossPoint(50,420);
            definition.eyes=new[]{new BossStaticEye(220,420,10),new BossStaticEye(420,420,10)};
            var model=Model();model.Advance(2,new BossRect(600,300,100,240),model.Trash);model.ResolveBoundary();
            Assert.That(model.Corruption,Is.Zero);Assert.That(model.Exposure,Is.Zero);
        }
        [Test] public void FrozenTrashCannotMoveThroughExposureAndMinimizeResetsOnlyExposure()
        {
            definition.eyes=new[]{new BossStaticEye(600,700,50)};var model=Model();
            model.Teleport(new BossRect(500,600,240,240),new BossPoint(600,720));
            model.Advance(.4,model.Body,new BossPoint(680,720));Assert.That(model.Trash.x,Is.EqualTo(600));
            model.ResolveBoundary(toggle:true);Assert.That(model.Exposure,Is.Zero);
            model.Advance(1);model.ResolveBoundary(toggle:true);Assert.That(model.Corruption,Is.Zero);
            model.Advance(1.6);model.ResolveBoundary();Assert.That(model.Corruption,Is.EqualTo(1));
        }
        [Test] public void ThreeSeparateExposuresRespawnAndRestorePhaseOneDownload()
        {
            definition.eyes=new[]{new BossStaticEye(600,700,50)};var model=Model();
            for(int i=0;i<3;i++)
            {
                model.Teleport(new BossRect(550,600,100,240),new BossPoint(600,720));model.Advance(i+1);model.ResolveBoundary();
                if(i<2)model.Teleport(definition.initialWindow,definition.initialTrash);
            }
            Assert.That(model.Attempt,Is.EqualTo(2));Assert.That(model.Corruption,Is.Zero);
            // The segment after the exact third hit continues in the fresh attempt.
            Assert.That(model.DownloadPercent,Is.InRange(10,10.2));Assert.That(model.Body.x,Is.EqualTo(definition.initialWindow.x));
        }
        [Test] public void UnlimitedFirstPhaseDownloadCapsAndDebugPauseDoesNotStopCombat()
        {
            var model=Model();model.Advance(90);Assert.That(model.DownloadPercent,Is.EqualTo(30));model.Advance(1000);Assert.That(model.DownloadPercent,Is.EqualTo(30));
            Assert.That(model.Outcome,Is.EqualTo(BossOutcome.None));model.Respawn();Assert.That(model.DownloadPercent,Is.EqualTo(10));
            model.Clock.SetDownloadDebugPause(true,1000);model.Advance(1010);
            Assert.That(model.DownloadPercent,Is.EqualTo(10));Assert.That(model.Clock.CombatTime,Is.EqualTo(1010));
        }
        [Test] public void CornerResizeReachesNinetySquareWithOppositeAnchorFixed()
        {
            definition.initialTrash=new BossPoint(140,650);var model=Model();var input=new BossInteraction(settings);
            input.Press(model,new BossPoint(430,840),false);input.Preview(model,new BossPoint(190,690),out var body,out var trash);
            Assert.That(body.width,Is.EqualTo(90));Assert.That(body.height,Is.EqualTo(90));
            Assert.That(body.x,Is.EqualTo(100));Assert.That(body.y,Is.EqualTo(600));Assert.That(body.Contains(trash,39),Is.True);
        }
        [TestCase(false)] [TestCase(true)] public void SingleAxisCanShrinkToNinetyWithoutLengtheningOtherAxis(bool vertical)
        {
            definition.initialWindow=new BossRect(100,600,vertical?90:240,vertical?240:90);
            definition.initialTrash=new BossPoint(145,645);var model=Model();var input=new BossInteraction(settings);
            var press=vertical?new BossPoint(145,840):new BossPoint(340,645);
            var target=vertical?new BossPoint(145,690):new BossPoint(190,645);
            input.Press(model,press,false);input.Preview(model,target,out var body,out var trash);
            Assert.That(body.width,Is.EqualTo(90));Assert.That(body.height,Is.EqualTo(90));
            Assert.That(body.x,Is.EqualTo(100));Assert.That(body.y,Is.EqualTo(600));Assert.That(body.Contains(trash,39),Is.True);
        }
        [Test] public void WideWindowMovesTrashFartherPerDragThanSmallSquare()
        {
            var model=Model();var input=new BossInteraction(settings);
            model.Teleport(new BossRect(175,675,90,90),definition.initialTrash);
            input.Press(model,model.Trash,false);input.Preview(model,new BossPoint(470,720),out _,out var small);
            model.Teleport(new BossRect(100,675,330,90),definition.initialTrash);
            input.Press(model,model.Trash,false);input.Preview(model,new BossPoint(470,720),out _,out var wide);
            Assert.That(small.x,Is.EqualTo(226));Assert.That(wide.x,Is.EqualTo(391));
        }
    }
}
