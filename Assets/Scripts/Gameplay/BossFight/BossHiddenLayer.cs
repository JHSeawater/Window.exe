using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WindowExe.Desktop.Desktop.Window;
using Object=UnityEngine.Object;

namespace WindowExe.BossFight
{
    /// <summary>Dedicated camera/renderer/texture/material. Never uses the desktop sorting-layer capture.</summary>
    public sealed class BossHiddenLayer : IDisposable
    {
        private readonly GameObject root;
        private readonly Camera camera;
        private readonly RenderTexture texture;
        private readonly Material material,previousMaterial;
        private readonly BossDesktopSession session;
        private readonly int layer;
        private readonly List<(Transform transform,BossPoint point,Vector2 size)> geometry=new List<(Transform,BossPoint,Vector2)>();
        private readonly Dictionary<Camera,(int before,int assigned)> cameraMasks=new Dictionary<Camera,(int,int)>();
        private readonly List<(EyeTracker tracker,SpriteRenderer white,SpriteRenderer pupil,BossStaticEye state)> trackers=new List<(EyeTracker,SpriteRenderer,SpriteRenderer,BossStaticEye)>();
        private readonly List<Sprite> ownedSprites=new List<Sprite>();
        private readonly float pupilAspect;
        private readonly bool authoredEye;
        private double lastTime;
        public RenderTexture Texture=>texture;
        public Camera Camera=>camera;
        public BossHiddenLayer(BossDesktopSession session,BossFightSettings settings,Sprite circle)
        {
            this.session=session;layer=settings.hiddenLayer;
            if(LayerMask.LayerToName(layer)!="BossHidden")throw new InvalidOperationException("BossHidden layer has not been prepared.");
            root=new GameObject("Boss hidden world");root.layer=layer;
            texture=new RenderTexture(1920,1032,24,RenderTextureFormat.ARGB32){name="Boss hidden layer (Runtime)",filterMode=FilterMode.Bilinear};texture.Create();
            var cameraObject=new GameObject("Boss hidden camera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
            camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.055f,.065f);
            camera.cullingMask=1<<layer;camera.targetTexture=texture;camera.depth=-50;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            var data=cameraObject.AddComponent<UniversalAdditionalCameraData>();data.SetRenderer(settings.hiddenRendererIndex);data.renderPostProcessing=false;
            material=new Material(settings.hiddenViewShader){name="Boss hidden view (Runtime)"};material.SetTexture("_BossHiddenTexture",texture);
            previousMaterial=session.View.Body.sharedMaterial;session.View.Body.sharedMaterial=material;
            var hiddenBackground=FullRectangle(settings.phase1HiddenBackgroundSprite,null);
            if(hiddenBackground!=null)
            {
                var imageSize=hiddenBackground.rect.size;
                float fill=Mathf.Max(1920/imageSize.x,1032/imageSize.y);
                Sprite("Hidden background",hiddenBackground,new BossPoint(960,516),imageSize*fill,Color.white,0);
            }
            authoredEye=settings.phase1EyeSprite!=null;
            var eyeSprite=FullRectangle(settings.phase1EyeSprite,circle);
            var pupilSprite=FullRectangle(settings.phase1PupilSprite,circle);
            pupilAspect=pupilSprite.rect.height/pupilSprite.rect.width;
            for(int x=0;x<1920;x+=60)Sprite("Grid",WindowTitleBar.WhiteSprite,new BossPoint(x,516),new Vector2(1,1032),new Color(.14f,.17f,.19f,.45f),1);
            for(int y=0;y<1032;y+=60)Sprite("Grid",WindowTitleBar.WhiteSprite,new BossPoint(960,y),new Vector2(1920,1),new Color(.14f,.17f,.19f,.45f),1);
            foreach(var state in settings.phase1.ActiveEyes)
            {
                var eyeSize=AspectSize(eyeSprite,(float)(state.radius*2));
                var pupilSize=new Vector2(eyeSize.x*.375f,eyeSize.x*.375f*pupilAspect);
                var white=Sprite("Eye",eyeSprite,state.center,eyeSize,authoredEye?Color.white:new Color(.7f,.62f,.56f),4);
                var pupil=Sprite("Pupil",pupilSprite,state.center,pupilSize,settings.phase1PupilSprite!=null?Color.white:new Color(.08f,.01f,.015f),5);
                pupil.transform.SetParent(white.transform,false);geometry.RemoveAt(geometry.Count-1);
                var tracker=white.gameObject.AddComponent<EyeTracker>();tracker.enabled=false;tracker.PupilTransform=pupil.transform;
                tracker.TargetWindowId=BossFightWindow.ContentId;trackers.Add((tracker,white,pupil,state));
            }
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;
            foreach(var other in Camera.allCameras)Exclude(other);
        }
        private static Vector2 AspectSize(Sprite sprite,float width)=>new Vector2(width,width*sprite.rect.height/sprite.rect.width);
        private Sprite FullRectangle(Sprite source,Sprite fallback)
        {
            if(source==null)return fallback;
            // Sliced sizing needs a full rectangle; leave the shared source/import settings untouched.
            var sprite=UnityEngine.Sprite.Create(source.texture,source.rect,Vector2.one*.5f,source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            sprite.name="Boss phase 1 "+source.texture.name+" (Runtime)";ownedSprites.Add(sprite);return sprite;
        }
        private SpriteRenderer Sprite(string name,Sprite sprite,BossPoint point,Vector2 size,Color color,int order)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.layer=layer;go.transform.SetParent(root.transform,false);
            var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.drawMode=SpriteDrawMode.Sliced;r.color=color;r.sortingOrder=order;
            geometry.Add((go.transform,point,size));return r;
        }
        private void BeforeCamera(ScriptableRenderContext context,Camera other)=>Exclude(other);
        private void Exclude(Camera other)
        {
            if(other==null || other==camera || cameraMasks.ContainsKey(other))return;
            int before=other.cullingMask,assigned=before&~(1<<layer);cameraMasks.Add(other,(before,assigned));other.cullingMask=assigned;
        }
        public void Render(BossPhase1Model model)
        {
            var space=session.Space;if(!space.Valid)return;
            camera.enabled=!model.Minimized;
            camera.transform.position=new Vector3(space.TopLeft.x+960*space.Scale,space.TopLeft.y-516*space.Scale,space.Snapshot.PlaneZ-10);
            camera.orthographicSize=516*space.Scale;camera.aspect=1920f/1032;
            foreach(var item in geometry)
            {item.transform.position=space.ToWorld(item.point.Vector);item.transform.GetComponent<SpriteRenderer>().size=item.size*space.Scale;}
            foreach(var item in trackers)
            {
                var eyeSize=model.EyeShape.Size(item.state.radius);
                item.white.size=eyeSize*space.Scale;
                item.pupil.size=new Vector2(eyeSize.x*.375f,eyeSize.x*.375f*pupilAspect)*space.Scale;
                item.tracker.MaxRadius=eyeSize.x/24*space.Scale;item.tracker.JitterAmount=eyeSize.x/640*space.Scale;item.tracker.OverrideHasTarget=!model.Minimized;
                item.tracker.OverrideTargetPosition=space.ToWorld(model.Body.Center);item.tracker.ManualTick((float)Math.Max(0,model.Clock.CombatTime-lastTime));
            }
            lastTime=model.Clock.CombatTime;
            material.SetVector("_BossWorldRect",new Vector4(space.TopLeft.x,space.TopLeft.y-1032*space.Scale,1920*space.Scale,1032*space.Scale));
        }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            foreach(var item in cameraMasks)if(item.Key!=null && item.Key.cullingMask==item.Value.assigned)item.Key.cullingMask=item.Value.before;
            if(session.View!=null && session.View.Body.sharedMaterial==material)session.View.Body.sharedMaterial=previousMaterial;
            if(camera!=null){camera.enabled=false;camera.targetTexture=null;}if(texture!=null)texture.Release();Object.Destroy(texture);Object.Destroy(material);Object.Destroy(root);
            foreach(var sprite in ownedSprites)Object.Destroy(sprite);ownedSprites.Clear();
        }
    }
}
