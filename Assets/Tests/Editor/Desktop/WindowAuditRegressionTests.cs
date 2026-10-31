using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    public sealed partial class DesktopSceneInteractionTests
    {
        private static void GeometryEqual(Vector2 actual, Vector2 expected, string message)
            => Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.001f), message);

        [UnityTest]
        public IEnumerator QAFixInvalidationConsumesCoalescedClickInTheSameUpdate()
        {
            var manager=WindowManager.Instance; var space=DesktopWindowSpace.Instance;
            var camera=space.Camera; var area=Object.FindAnyObjectByType<DesktopWorkArea>();
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices; var mouse=InputSystem.AddDevice<Mouse>("QACancelledCoalescedMouse");
            module.actionsAsset.devices=new InputDevice[]{mouse};
            var hook=new GameObject("QAEarlySpaceUpdate").AddComponent<QAEarlySpaceUpdate>();
            try
            {
                var w=manager.Open("memo"); var close=TitleButton(w,2);
                yield return Send(mouse,close,false);
                var ran=false;
                hook.Once=()=> { space.Configure(camera,null,manager); space.Configure(camera,area,manager); ran=true; };
                InputSystem.QueueStateEvent(mouse,new MouseState{position=close}.WithButton(MouseButton.Left,true));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=close});
                yield return null; yield return null;
                Assert.That(ran,Is.True);
                Assert.That(manager.GetProcess("memo"),Is.Not.Null,
                    "A down/up in the invalidation frame must not be replayed as a fresh Close click");
                yield return Send(mouse,close,true); yield return Send(mouse,close,false);
                Assert.That(manager.GetProcess("memo"),Is.Null,"The next deliberate click must still work");
            }
            finally
            {
                if(hook!=null) Object.Destroy(hook.gameObject);
                space.Configure(camera,area,manager); manager.ResetWindows();
                module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator QAFixSmallAreaMoveAndCancellationPreserveNormalSize()
        {
            SetResolution(1280,720); for(var f=0;f<4;f++) yield return null;
            var manager=WindowManager.Instance; var space=DesktopWindowSpace.Instance;
            var camera=space.Camera; var cameraSize=camera.orthographicSize;
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices;
            var mouse=InputSystem.AddDevice<Mouse>("QAMoveMouse");
            var keyboard=InputSystem.AddDevice<Keyboard>("QAMoveKeyboard");
            module.actionsAsset.devices=new InputDevice[]{mouse,keyboard};
            try
            {
                for(var mode=0;mode<7;mode++)
                {
                    camera.orthographicSize=cameraSize; yield return null; yield return null; space.Refresh(); manager.ResetWindows();
                    var w=manager.Open("memo"); var normal=w.Size;
                    camera.orthographicSize=1; yield return null; yield return null; space.Refresh();
                    var title=WorldPoint(new Vector2(w.OuterBounds.xMin+.5f,w.OuterBounds.yMax-w.BarHeight/2));
                    yield return Send(mouse,title,false); yield return Send(mouse,title,true);
                    yield return Send(mouse,title+new Vector2(40,-8),true);
                    Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.Moving));
                    if(mode==1) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null; }
                    if(mode==2) DesktopInputRouter.Instance.SendMessage("OnApplicationFocus",false);
                    if(mode==3) manager.SetLocked(true);
                    if(mode==4) manager.Minimize("memo");
                    if(mode==5) { camera.orthographicSize=cameraSize; space.Refresh(); }
                    yield return Send(mouse,title+new Vector2(40,-8),false);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                    manager.SetLocked(false);
                    if(mode==4) manager.Enshow("memo");
                    if(mode==6) { manager.ToggleMaximize("memo"); manager.Minimize("memo"); manager.Enshow("memo"); manager.ToggleMaximize("memo"); }
                    camera.orthographicSize=cameraSize; yield return null; yield return null; space.Refresh();
                    GeometryEqual(w.Size,normal,"Original normal size lost after move/cancel mode "+mode);
                    Assert.That(w.OuterBounds.xMin,Is.GreaterThanOrEqualTo(space.Bounds.xMin-.001f));
                    Assert.That(w.OuterBounds.yMax,Is.LessThanOrEqualTo(space.Bounds.yMax+.001f));
                }
            }
            finally
            {
                camera.orthographicSize=cameraSize; manager.SetLocked(false); space.Refresh(); manager.ResetWindows();
                module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator QAFixPublicRelativeResizePreservesUntouchedNormalAxes()
        {
            var manager=WindowManager.Instance; var space=DesktopWindowSpace.Instance;
            var camera=space.Camera; var cameraSize=camera.orthographicSize;
            try
            {
                foreach(var delta in new[]{new Vector2(0,-.2f),new Vector2(-.2f,0),Vector2.zero})
                {
                    camera.orthographicSize=cameraSize; yield return null; yield return null; space.Refresh(); manager.ResetWindows();
                    var w=manager.Open("memo"); var normal=w.Size;
                    camera.orthographicSize=1; yield return null; yield return null; space.Refresh(); w.ResizeDelta(delta);
                    camera.orthographicSize=cameraSize; yield return null; yield return null; space.Refresh();
                    GeometryEqual(w.Size,normal+delta,"Relative API must use normal geometry, including zero axes");
                    w.Resize(new Vector2(3,2));
                    GeometryEqual(w.Size,new Vector2(3,2),"Absolute resize intentionally sets both axes");
                }
            }
            finally { camera.orthographicSize=cameraSize; space.Refresh(); manager.ResetWindows(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator QAFixAppearanceRefreshPreservesStackAndPublishesMetadata()
        {
            var manager=WindowManager.Instance;
            var memo=manager.Open("memo"); var internet=manager.Open("internet"); var photo=manager.Open("photos");
            manager.FocusWindow("memo");
            var order=manager.DisplayOrder.Select(p=>p.InstanceId).ToArray();
            var size=memo.Size; var position=(Vector2)memo.transform.localPosition;
            var source=Object.FindAnyObjectByType<WindowTaskbarSource>();
            var notifications=0; System.Action changed=()=>notifications++; source.Changed+=changed;
            try
            {
                foreach(var w in new[]{memo,internet})
                {
                    notifications=0;
                    w.ConfigureAppearance("갱신된 한글 제목",photo.TaskbarIcon,GameTypography.Settings.TmpFont);
                    Assert.That(manager.DisplayOrder.Select(p=>p.InstanceId),Is.EqualTo(order));
                    var front=memo.GetComponentsInChildren<Renderer>(true).Where(r=>r.sortingLayerName=="WindowFront").Min(r=>r.sortingOrder);
                    var back=photo.GetComponentsInChildren<Renderer>(true).Where(r=>r.sortingLayerName=="WindowFront").Max(r=>r.sortingOrder);
                    Assert.That(front,Is.GreaterThan(back),"Appearance must not reset the active window to sorting slot zero");
                    Assert.That(notifications,Is.EqualTo(1));
                    Assert.That(source.GetSnapshot().Windows.Single(p=>p.ItemId==w.WindowId).Title,Is.EqualTo("갱신된 한글 제목"));
                    w.InitializeVisuals(); w.InitializeVisuals();
                    Assert.That(manager.DisplayOrder.Select(p=>p.InstanceId),Is.EqualTo(order));
                    Assert.That(memo.GetComponentsInChildren<Renderer>(true).Where(r=>r.sortingLayerName=="WindowFront").Min(r=>r.sortingOrder),Is.EqualTo(front));
                }
                GeometryEqual(memo.Size,size,"Appearance changed geometry");
                GeometryEqual(memo.transform.localPosition,position,"Appearance moved window");
            }
            finally { source.Changed-=changed; manager.ResetWindows(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator QAFixPlaneOnlyChangeUsesIndependentRayProjection()
        {
            var space=DesktopWindowSpace.Instance; var camera=space.Camera;
            var originalOrtho=camera.orthographic;
            var field=typeof(DesktopWindowSpace).GetField("planeZ",BindingFlags.NonPublic|BindingFlags.Instance);
            var originalZ=(float)field.GetValue(space);
            try
            {
                camera.orthographic=false; space.Refresh();
                field.SetValue(space,originalZ+1); space.Refresh();
                var area=Object.FindAnyObjectByType<DesktopWorkArea>(); var canvas=(RectTransform)area.transform;
                var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity); var max=-min;
                foreach(var p in new[]{area.Area.min,new Vector2(area.Area.xMin,area.Area.yMax),area.Area.max,new Vector2(area.Area.xMax,area.Area.yMin)})
                {
                    var screen=RectTransformUtility.WorldToScreenPoint(camera,canvas.TransformPoint(p));
                    var ray=camera.ScreenPointToRay(screen);
                    Assert.That(new Plane(Vector3.forward,new Vector3(0,0,originalZ+1)).Raycast(ray,out var distance),Is.True);
                    var hit=(Vector2)ray.GetPoint(distance); min=Vector2.Min(min,hit); max=Vector2.Max(max,hit);
                }
                GeometryEqual(space.Bounds.min,min,"Plane cache retained old minimum");
                GeometryEqual(space.Bounds.max,max,"Plane cache retained old maximum");
                var count=0; System.Action changed=()=>count++; space.Changed+=changed;
                try { space.Refresh(); space.Refresh(); Assert.That(count,Is.Zero); }
                finally { space.Changed-=changed; }
            }
            finally { field.SetValue(space,originalZ); camera.orthographic=originalOrtho; space.Refresh(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator QAFixInvalidSpaceCancelsHeldInputAndRecoversOnce()
        {
            var manager=WindowManager.Instance; var space=DesktopWindowSpace.Instance;
            var camera=space.Camera; var area=Object.FindAnyObjectByType<DesktopWorkArea>();
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices; var mouse=InputSystem.AddDevice<Mouse>("QAInvalidSpaceMouse");
            module.actionsAsset.devices=new InputDevice[]{mouse};
            var changes=0; System.Action changed=()=>changes++; space.Changed+=changed;
            try
            {
                var w=manager.Open("memo"); var initial=(Vector2)w.transform.localPosition;
                var title=WorldPoint(new Vector2(w.OuterBounds.xMin+.5f,w.OuterBounds.yMax-w.BarHeight/2));
                yield return Send(mouse,title,false); yield return Send(mouse,title,true);
                yield return Send(mouse,title+new Vector2(70,-20),true);
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.Moving));
                changes=0; space.Configure(camera,null,manager);
                Assert.That(space.Valid,Is.False);
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.None));
                GeometryEqual(w.transform.localPosition,initial,"Invalidating space must roll back the gesture");
                Assert.That(space.TryWorld(title,out _),Is.False);
                Assert.That(changes,Is.EqualTo(1));
                space.Refresh(); space.Refresh(); Assert.That(changes,Is.EqualTo(1));
                space.Configure(camera,area,manager); Assert.That(space.Valid,Is.True);
                Assert.That(changes,Is.EqualTo(2));
                yield return Send(mouse,TitleButton(w,2),false);
                Assert.That(manager.GetProcess("memo"),Is.Not.Null,"A cancelled release must not close the recovered window");
            }
            finally
            {
                space.Changed-=changed; space.Configure(camera,area,manager); manager.ResetWindows();
                module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator QAFixPointerResizePreservesLogicalAxesAndRoundTripsAllDirections()
        {
            var manager=WindowManager.Instance; var space=DesktopWindowSpace.Instance;
            var camera=space.Camera; var originalSize=camera.orthographicSize;
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices; var mouse=InputSystem.AddDevice<Mouse>("QAAxisResizeMouse");
            var keyboard=InputSystem.AddDevice<Keyboard>("QAAxisResizeKeyboard");
            module.actionsAsset.devices=new InputDevice[]{mouse,keyboard};
            try
            {
                foreach(ResizeDirection direction in System.Enum.GetValues(typeof(ResizeDirection)))
                {
                    var left=direction==ResizeDirection.Left||direction==ResizeDirection.TopLeft||direction==ResizeDirection.BottomLeft;
                    var right=direction==ResizeDirection.Right||direction==ResizeDirection.TopRight||direction==ResizeDirection.BottomRight;
                    var top=direction==ResizeDirection.Top||direction==ResizeDirection.TopLeft||direction==ResizeDirection.TopRight;
                    var bottom=direction==ResizeDirection.Bottom||direction==ResizeDirection.BottomLeft||direction==ResizeDirection.BottomRight;
                    for(var mode=0;mode<4;mode++)
                    {
                        SetResolution(1280,720); camera.orthographicSize=originalSize;
                        yield return null; yield return null; space.Refresh(); manager.ResetWindows();
                        var w=manager.Open("memo"); var normal=w.Size; var normalPosition=(Vector2)w.transform.localPosition;
                        if(direction==ResizeDirection.Top||direction==ResizeDirection.Bottom)
                        { SetResolution(720,1280); camera.orthographicSize=3; }
                        else camera.orthographicSize=direction==ResizeDirection.Left||direction==ResizeDirection.Right?2:1;
                        yield return null; yield return null; space.Refresh();
                        var start=w.OuterBounds;
                        // The taskbar owns its boundary pixel; press inside the visible handle.
                        var world=new Vector2(left?start.xMin+.04f:right?start.xMax-.04f:start.center.x,
                            top?start.yMax-.04f:bottom?start.yMin+.04f:start.center.y);
                        var delta=new Vector2(left?.2f:right?-.2f:0,top?-.15f:bottom?.15f:0);
                        var press=WorldPoint(world); var end=WorldPoint(world+delta);
                        yield return Send(mouse,press,false); yield return Send(mouse,press,true); yield return Send(mouse,end,true);
                        Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.Resizing),direction+" mode "+mode+" "+Diagnostic());
                        var displayed=w.Size;
                        var expected=normal;
                        var expectedPosition=normalPosition;
                        if(!right) Assert.That(w.OuterBounds.xMax,Is.EqualTo(start.xMax).Within(.001f));
                        if(!left) Assert.That(w.OuterBounds.xMin,Is.EqualTo(start.xMin).Within(.001f));
                        if(!top) Assert.That(w.OuterBounds.yMax,Is.EqualTo(start.yMax).Within(.001f));
                        if(!bottom) Assert.That(w.OuterBounds.yMin,Is.EqualTo(start.yMin).Within(.001f));
                        if(mode==0)
                        {
                            if((left||right)&&Mathf.Abs(displayed.x-start.width)>.00001f)
                            { expected.x=displayed.x; expectedPosition.x=w.transform.localPosition.x; }
                            if((top||bottom)&&Mathf.Abs(displayed.y-(start.height-w.BarHeight))>.00001f)
                            { expected.y=displayed.y; expectedPosition.y=w.transform.localPosition.y; }
                        }
                        if(mode==1) { yield return Send(mouse,press,true); end=press; }
                        if(mode==2) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null; }
                        if(mode==3) { camera.orthographicSize=originalSize; space.Refresh(); }
                        yield return Send(mouse,end,false);
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                        SetResolution(1280,720); camera.orthographicSize=originalSize;
                        yield return null; yield return null; space.Refresh();
                        GeometryEqual(w.Size,expected,"Logical resize axis/round trip "+direction+" mode "+mode);
                        GeometryEqual(w.transform.localPosition,expectedPosition,"Normal position must preserve untouched axes");
                    }
                }
            }
            finally
            {
                camera.orthographicSize=originalSize; space.Refresh(); manager.ResetWindows();
                module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator QAFixAppearanceMinimizedReentrantAndPointerOrderRemainConsistent()
        {
            var manager=WindowManager.Instance; var source=Object.FindAnyObjectByType<WindowTaskbarSource>();
            var memo=manager.Open("memo"); manager.Open("photos"); manager.FocusWindow("memo");
            var id=manager.GetProcess("memo").InstanceId; var normal=memo.Size;
            manager.ToggleMaximize("memo"); manager.Minimize("memo");
            memo.ConfigureAppearance("최소화 중 갱신",memo.TaskbarIcon,GameTypography.Settings.TmpFont);
            Assert.That(manager.GetProcess("memo").State,Is.EqualTo(WindowState.Minimized));
            Assert.That(memo.IsMaximized,Is.True); Assert.That(memo.gameObject.activeSelf,Is.False);
            manager.Enshow("memo"); Assert.That(memo.IsMaximized,Is.True);
            manager.ToggleMaximize("memo"); GeometryEqual(memo.Size,normal,"Appearance lost normal size");
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices; var mouse=InputSystem.AddDevice<Mouse>("QAAppearanceMouse");
            module.actionsAsset.devices=new InputDevice[]{mouse};
            System.Action callback=null;
            try
            {
                var body=WorldPoint(memo.OuterBounds.center);
                yield return Send(mouse,body,false); yield return Send(mouse,body,true); yield return Send(mouse,body,false);
                Assert.That(manager.ActiveWindowId,Is.EqualTo(id));
                var once=false;
                callback=()=>
                {
                    if(once) return; once=true;
                    manager.Close("memo"); manager.Open("memo");
                };
                source.Changed+=callback;
                memo.ConfigureAppearance("다시 열기",memo.TaskbarIcon,GameTypography.Settings.TmpFont);
                var replacement=manager.GetProcess("memo");
                Assert.That(replacement.InstanceId,Is.Not.EqualTo(id));
                var title=replacement.Instance.WindowTitle;
                memo.ConfigureAppearance("오래된 참조",null,GameTypography.Settings.TmpFont);
                Assert.That(replacement.Instance.WindowTitle,Is.EqualTo(title));
            }
            finally
            {
                if(callback!=null) source.Changed-=callback;
                module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse); manager.ResetWindows();
            }
        }

        [UnityTest]
        public IEnumerator QAFixInvalidReferencesRaysAndAreasRecoverWithoutRepeatedNotifications()
        {
            var space=DesktopWindowSpace.Instance; var manager=WindowManager.Instance;
            var camera=space.Camera; var area=Object.FindAnyObjectByType<DesktopWorkArea>(); var barHeight=area.BarHeight;
            var plane=typeof(DesktopWindowSpace).GetField("planeZ",BindingFlags.NonPublic|BindingFlags.Instance);
            var originalZ=(float)plane.GetValue(space);
            var count=0; System.Action changed=()=>count++; space.Changed+=changed;
            try
            {
                for(var mode=0;mode<5;mode++)
                {
                    count=0;
                    if(mode==0) space.Configure(null,area,manager);
                    if(mode==1) space.Configure(camera,null,manager);
                    if(mode==2) plane.SetValue(space,camera.transform.position.z-1);
                    if(mode==3) plane.SetValue(space,float.NaN);
                    if(mode==4) area.SetBarHeight(100000);
                    space.Refresh();
                    Assert.That(space.Valid,Is.False,"Invalid mode "+mode);
                    Assert.That(space.TryWorld(new Vector2(400,300),out _),Is.False);
                    Assert.That(space.Bounds,Is.EqualTo(default(Rect)));
                    Assert.That(count,Is.EqualTo(1));
                    space.Refresh(); space.Refresh(); Assert.That(count,Is.EqualTo(1));
                    plane.SetValue(space,originalZ); area.SetBarHeight(barHeight); space.Configure(camera,area,manager);
                    Assert.That(space.Valid,Is.True); Assert.That(count,Is.EqualTo(2));
                    space.Refresh(); Assert.That(count,Is.EqualTo(2));
                }
            }
            finally
            {
                space.Changed-=changed; plane.SetValue(space,originalZ); area.SetBarHeight(barHeight); space.Configure(camera,area,manager);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator QAFixPlaneChangeCancelsGestureOnTheActualWindowPlane()
        {
            var manager=WindowManager.Instance; var space=DesktopWindowSpace.Instance; var camera=space.Camera;
            var root=manager.transform; var originalRoot=root.position; var originalOrtho=camera.orthographic;
            var field=typeof(DesktopWindowSpace).GetField("planeZ",BindingFlags.NonPublic|BindingFlags.Instance);
            var originalZ=(float)field.GetValue(space);
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices; var mouse=InputSystem.AddDevice<Mouse>("QAPlaneMouse");
            module.actionsAsset.devices=new InputDevice[]{mouse};
            try
            {
                camera.orthographic=false; yield return null; yield return null; space.Refresh();
                var w=manager.Open("memo"); var normal=w.Size;
                var title=camera.WorldToScreenPoint(w.transform.TransformPoint(new Vector3(-w.Size.x/2+.5f,w.Size.y/2+w.BarHeight/2,0)));
                yield return Send(mouse,title,false); yield return Send(mouse,title,true);
                yield return Send(mouse,(Vector2)title+new Vector2(60,-20),true);
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.Moving));
                root.position=new Vector3(originalRoot.x,originalRoot.y,originalZ+1);
                field.SetValue(space,originalZ+1); space.Refresh();
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.None));
                yield return Send(mouse,TitleButton(w,2),false);
                Assert.That(manager.GetProcess("memo"),Is.Not.Null);
                GeometryEqual(w.Size,normal,"Plane cancellation lost general size");
            }
            finally
            {
                root.position=originalRoot; field.SetValue(space,originalZ); camera.orthographic=originalOrtho; space.Refresh();
                module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse); manager.ResetWindows();
            }
        }
    }
}
