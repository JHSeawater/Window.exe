using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;
using Object=UnityEngine.Object;

namespace WindowExe.BossFight
{
    /// <summary>Session-owned provisional visuals. Simulation remains authoritative.</summary>
    public sealed class BossFightPresentation : IDisposable
    {
        private sealed class EyeView { public GameObject root; public SpriteRenderer white,pupil; public EyeTracker tracker; }
        private readonly BossFightSettings settings;
        private readonly BossDesktopSession session;
        private readonly GameObject root,downloadRoot;
        private readonly SpriteRenderer background,trash,boss,warning;
        private readonly List<TextMeshPro> writing=new List<TextMeshPro>();
        private readonly List<BossPoint> writingPositions=new List<BossPoint>();
        private readonly Dictionary<int,EyeView> eyes=new Dictionary<int,EyeView>();
        private readonly Dictionary<(Sprite,bool),Sprite> rectangularSprites=new Dictionary<(Sprite,bool),Sprite>();
        private readonly Texture2D circleTexture;
        private readonly Sprite circle;
        private readonly Text percent;
        private const int DownloadBlockCount=20;
        private const float DownloadBlockWidth=7,DownloadBlockHeight=20,DownloadBlockPitch=9;
        private static readonly Color DownloadActiveColor=new Color(.25f,.7f,.6f),DownloadIdleColor=new Color(.12f,.15f,.18f);
        private readonly Image[] blocks=new Image[DownloadBlockCount];
        private readonly Image hiddenBar;
        private readonly TextMeshPro bossName,warningText;
        private readonly SpriteRenderer errorPanel;
        private readonly TextMeshPro errorText;
        private readonly SpriteRenderer entryPanel;
        private readonly Renderer[] windowRenderers;
        private bool entryWindowHidden;
        private BossEntryBackdrop entryBackdrop;
        private SpriteRenderer[] transitionDosWindows;
        private SpriteRenderer[] entryEyeWindows;
        private GameObject entryDownloadRoot;
        private SpriteRenderer entryDownloadPanel,entryDownloadBorder,entryDownloadTrack;
        private SpriteRenderer[] entryDownloadBlocks;
        private SpriteRenderer[] phase2SpacebarWindows;
        private IBossFightModel hintedPhase2;
        private double phase2StartedAt;
        private static readonly BossPoint[] entryWindowPositions={
            new BossPoint(340,260),new BossPoint(990,300),new BossPoint(1530,260),new BossPoint(610,560),new BossPoint(1300,580),
            new BossPoint(360,780),new BossPoint(1580,780),new BossPoint(920,740),new BossPoint(720,290),new BossPoint(1200,430),
            new BossPoint(280,510),new BossPoint(1640,500),new BossPoint(960,220),new BossPoint(650,820),new BossPoint(1280,820)};
        private readonly TextMeshPro debugText;
        private readonly TextMeshPro phase1Terminal;
        private readonly TMP_SpriteAsset terminalSymbols;
        private readonly Texture2D terminalSymbolTexture;
        private readonly Material terminalSymbolMaterial;
        private BossPhase1Model terminalModel;
        private double terminalStartedAt;
        private int terminalAttempt;
        private int terminalPrintedLines=-1,terminalFirstVisible;
        private readonly SpriteRenderer[] exposureBorder=new SpriteRenderer[4];
        private readonly SpriteRenderer exposureBar;
        private BossHiddenLayer hidden;
        private IBossFightModel renderedModel;
        private IReadOnlyList<BossEye> transitionEyes;
        private double successShownAt=-1,returnStarted;
        private BossPoint lastBoss,returnFrom,visualBoss;
        private bool returning;
        private double lastTime;
        private int attempt=-1;
        public BossFightPresentation(BossDesktopSession session,BossFightSettings settings,TaskbarController taskbar,BossEntryBackdrop backdrop=null)
        {
            this.session=session; this.settings=settings;
            entryBackdrop=backdrop;
            entryBackdrop?.ShowBehindEntry();
            windowRenderers=session.View.GetComponentsInChildren<Renderer>(true);
            root=new GameObject("Boss fight world");
            circleTexture=new Texture2D(64,64,TextureFormat.RGBA32,false) {name="Boss eye circle (Runtime)",filterMode=FilterMode.Bilinear};
            var colors=new Color[64*64];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {float distance=new Vector2(x-31.5f,y-31.5f).magnitude; colors[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-distance));}
            circleTexture.SetPixels(colors); circleTexture.Apply();
            circle=UnityEngine.Sprite.Create(circleTexture,new Rect(0,0,64,64),Vector2.one*.5f,64,0,SpriteMeshType.FullRect);
            background=Sprite("Background",WindowTitleBar.WhiteSprite,settings.phase2Background,"Background",1);
            if(settings.phase1!=null)
            {
                var terminalObject=new GameObject("Phase 1 terminal",typeof(TextMeshPro));terminalObject.transform.SetParent(root.transform,false);
                phase1Terminal=terminalObject.GetComponent<TextMeshPro>();GameTypography.Apply(phase1Terminal);
                phase1Terminal.fontSize=34;phase1Terminal.color=new Color(.68f,.74f,.7f);
                phase1Terminal.alignment=TextAlignmentOptions.TopLeft;phase1Terminal.textWrappingMode=TextWrappingModes.Normal;
                phase1Terminal.overflowMode=TextOverflowModes.Truncate;
                phase1Terminal.richText=false;phase1Terminal.parseCtrlCharacters=false;phase1Terminal.lineSpacing=4;
                // NeoDunggeunmo has no U+2192 glyph. Supply a local pixel arrow without changing the source text or shared font.
                terminalSymbolTexture=new Texture2D(12,12,TextureFormat.RGBA32,false){name="Boss terminal arrow (Runtime)",filterMode=FilterMode.Point};
                var arrowPixels=new Color[144];
                for(int x=1;x<=10;x++)arrowPixels[5*12+x]=Color.white;
                for(int i=0;i<=3;i++){arrowPixels[(5-i)*12+10-i]=Color.white;arrowPixels[(5+i)*12+10-i]=Color.white;}
                terminalSymbolTexture.SetPixels(arrowPixels);terminalSymbolTexture.Apply();
                terminalSymbolMaterial=new Material(background.sharedMaterial){name="Boss terminal arrow (Runtime)",mainTexture=terminalSymbolTexture};
                terminalSymbols=ScriptableObject.CreateInstance<TMP_SpriteAsset>();terminalSymbols.name="Boss terminal symbols (Runtime)";
                terminalSymbols.spriteSheet=terminalSymbolTexture;
                var glyph=new TMP_SpriteGlyph(0,new UnityEngine.TextCore.GlyphMetrics(12,12,0,10,12),new UnityEngine.TextCore.GlyphRect(0,0,12,12),1,0);
                terminalSymbols.spriteGlyphTable.Add(glyph);terminalSymbols.spriteCharacterTable.Add(new TMP_SpriteCharacter(0x2192,terminalSymbols,glyph){name="Right arrow"});
                terminalSymbols.UpdateLookupTables();terminalSymbols.material=terminalSymbolMaterial;
                phase1Terminal.spriteAsset=terminalSymbols;phase1Terminal.tintAllSprites=true;
                phase1Terminal.rectTransform.pivot=new Vector2(0,1);phase1Terminal.rectTransform.sizeDelta=new Vector2(1856/9f,968/9f);
                var terminalRenderer=phase1Terminal.GetComponent<MeshRenderer>();terminalRenderer.sortingLayerName="Background";terminalRenderer.sortingOrder=2;
                terminalObject.SetActive(false);
            }
            warning=Sprite("Wall warning",WindowTitleBar.WhiteSprite,new Color(1,.02f,.04f,.5f));
            if(settings.phase1IntroSprite!=null)
            {entryPanel=Sprite("Boss entry warning",settings.phase1IntroSprite,Color.white,centered:true);entryPanel.enabled=false;}
            if(settings.phase1ErrorSprite!=null)
                errorPanel=Sprite("File in use error",settings.phase1ErrorSprite,Color.white);
            else
            {
                errorPanel=Sprite("File in use error",WindowTitleBar.WhiteSprite,new Color(.62f,.65f,.68f));
                errorText=Writing("삭제할 수 없습니다: 파일이 사용 중입니다.",new BossPoint(960,516),new Color(.05f,.05f,.05f),28);
            }
            for(int i=0;i<4;i++)exposureBorder[i]=Sprite("Exposure border",WindowTitleBar.WhiteSprite,Color.red);
            exposureBar=Sprite("Exposure time",WindowTitleBar.WhiteSprite,Color.red);
            ShowTransition(false);
            trash=Sprite("Recycle bin",settings.trashSprite!=null?settings.trashSprite:WindowTitleBar.WhiteSprite,Color.white);
            boss=Sprite("WINDOW.exe",settings.bossSprite!=null?settings.bossSprite:circle,new Color(1,.3f,.3f));
            bossName=Writing("WINDOW.exe",new BossPoint(1782,590),Color.white,28);
            debugText=Writing("",new BossPoint(1640,35),new Color(1,.85f,.2f),22);
            warningText=Writing("!!",new BossPoint(1875,470),Color.red,66);
            downloadRoot=new GameObject("Boss download",typeof(RectTransform)); downloadRoot.transform.SetParent(taskbar.transform,false);
            var dr=(RectTransform)downloadRoot.transform; dr.anchorMin=dr.anchorMax=new Vector2(1,.5f); dr.pivot=new Vector2(1,.5f);
            dr.anchoredPosition=new Vector2(-130,0); dr.sizeDelta=new Vector2(260,32);
            for(int i=0;i<DownloadBlockCount;i++)
            {
                var go=new GameObject("Block "+i,typeof(RectTransform),typeof(Image)); go.transform.SetParent(dr,false);
                var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=new Vector2(0,.5f); rt.pivot=new Vector2(0,.5f);
                rt.anchoredPosition=new Vector2(i*DownloadBlockPitch,0); rt.sizeDelta=new Vector2(DownloadBlockWidth,DownloadBlockHeight);
                blocks[i]=go.GetComponent<Image>(); blocks[i].raycastTarget=false;
            }
            var label=new GameObject("Percent",typeof(RectTransform),typeof(Text)); label.transform.SetParent(dr,false);
            percent=label.GetComponent<Text>(); GameTypography.Apply(percent); percent.fontSize=18; percent.alignment=TextAnchor.MiddleRight; percent.raycastTarget=false;
            percent.rectTransform.anchorMin=Vector2.zero; percent.rectTransform.anchorMax=Vector2.one; percent.rectTransform.offsetMin=percent.rectTransform.offsetMax=Vector2.zero;
            var bar=new GameObject("Minimize remaining",typeof(RectTransform),typeof(Image)); bar.transform.SetParent(dr,false);
            hiddenBar=bar.GetComponent<Image>(); hiddenBar.raycastTarget=false; hiddenBar.color=new Color(.9f,.5f,.2f);
        }
        private SpriteRenderer Sprite(string name,Sprite image,Color color,string layer="WindowFront",int order=3,bool centered=false)
        {
            var go=new GameObject(name,typeof(SpriteRenderer)); go.transform.SetParent(root.transform,false);
            var renderer=go.GetComponent<SpriteRenderer>(); renderer.sprite=Rectangle(image,centered); renderer.color=color;
            renderer.drawMode=SpriteDrawMode.Sliced; renderer.sortingLayerName=layer; renderer.sortingOrder=order; return renderer;
        }
        private Sprite Rectangle(Sprite image,bool centered=false)
        {
            if(image!=circle && image!=WindowTitleBar.WhiteSprite)
            {
                var key=(image,centered);
                if(!rectangularSprites.TryGetValue(key,out var rectangle))
                {
                    var pivot=centered?Vector2.one*.5f:new Vector2(image.pivot.x/image.rect.width,image.pivot.y/image.rect.height);
                    rectangle=UnityEngine.Sprite.Create(image.texture,image.rect,pivot,image.pixelsPerUnit,0,SpriteMeshType.FullRect,image.border);
                    rectangularSprites.Add(key,rectangle);
                }
                image=rectangle;
            }
            return image;
        }
        private TextMeshPro Writing(string text,BossPoint position,Color color,float pixels)
        {
            var go=new GameObject("Writing",typeof(TextMeshPro)); go.transform.SetParent(root.transform,false);
            var label=go.GetComponent<TextMeshPro>(); GameTypography.Apply(label); label.text=text; label.color=color;
            label.alignment=TextAlignmentOptions.Center; label.textWrappingMode=TextWrappingModes.NoWrap;
            label.fontSize=pixels; label.rectTransform.sizeDelta=new Vector2(10,1); label.GetComponent<MeshRenderer>().sortingLayerName="WindowFront";
            writing.Add(label); writingPositions.Add(position); return label;
        }
        private void Place(SpriteRenderer renderer,BossPoint point,double width,double height,int order)
        {
            renderer.transform.position=session.Space.ToWorld(point.Vector); renderer.size=new Vector2((float)width,(float)height)*session.Space.Scale;
            renderer.sortingOrder=order;
        }
        public void ShowTransition(bool value){errorPanel.enabled=value;if(errorText!=null)errorText.gameObject.SetActive(value);}
        public void Render(IBossFightModel model,TaskbarController taskbar,double entryAge=-1)
        {
            if(entryAge<0 || entryAge>=settings.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds)
            {entryBackdrop?.Dispose();entryBackdrop=null;}
            else entryBackdrop?.FitCamera();
            var first=model as BossPhase1Model;
            if(first==null || !first.CombatLocked || first.TransitionAge<1.5) ClearTransitionDosWindows();
            if(!session.Space.Valid || session.View==null) return;
            var attacks=(model as BossPhase2Model)?.Attacks;
            var scale=session.Space.Scale; int order=session.View.Body.sortingOrder;
            if(first!=null){if(hidden==null)hidden=new BossHiddenLayer(session,settings,circle);hidden.Render(first);}
            else if(hidden!=null){hidden.Dispose();hidden=null;}
            session.View.Body.color=first==null?settings.safeDesktop:first.Exposed?new Color(1,.6f,.6f):Color.white;
            bool authoredBackground=first==null && settings.phase2BackgroundSprite!=null;
            background.sprite=authoredBackground?Rectangle(settings.phase2BackgroundSprite,true):WindowTitleBar.WhiteSprite;
            background.color=first==null?(authoredBackground?Color.white:settings.phase2Background):new Color(.035f,.045f,.055f);
            if(first!=null && first.TransitionAge>=1.5)background.color=Color.Lerp(settings.phase2Background,Color.red,(float)(.5+.5*Math.Sin(first.TransitionAge*60)));
            Place(background,new BossPoint(960,516),1920,1032,1);
            background.transform.position=session.Space.Snapshot.Bounds.center;
            background.size=session.Space.Snapshot.Bounds.size;
            if(authoredBackground)
            {
                var imageSize=background.sprite.rect.size;var boundsSize=session.Space.Snapshot.Bounds.size;
                background.size=imageSize*Mathf.Max(boundsSize.x/imageSize.x,boundsSize.y/imageSize.y);
            }
            RenderTerminal(first,entryAge);
            bool flash=((int)(model.Clock.CombatTime*7)&1)==0;
            warning.enabled=attacks!=null && attacks.Warning && flash;
            Place(warning,new BossPoint(1905,516),30,1032,order+15);
            warningText.gameObject.SetActive(attacks!=null && attacks.Warning && flash);
            Place(trash,model.Trash,62,62,order+2); trash.enabled=!model.Minimized;
            var home=first!=null?first.Definition.boss:settings.phase2Boss;
            if(renderedModel!=model || attempt!=model.Attempt)returning=false;
            else if((model.Boss-lastBoss).SqrMagnitude>1 && (model.Boss-home).SqrMagnitude<.001 && (lastBoss-home).SqrMagnitude>1)
            {returning=true;returnFrom=visualBoss;returnStarted=model.Clock.CombatTime;}
            if((model.Boss-home).SqrMagnitude>1)returning=false;
            visualBoss=model.Boss;
            if(returning){double t=Math.Min(1,(model.Clock.CombatTime-returnStarted)/.22);visualBoss=BossPoint.Lerp(returnFrom,home,t*t*(3-2*t));if(t>=1)returning=false;}
            lastBoss=model.Boss;
            Place(boss,visualBoss,78,78,order+4); boss.color=model.Captured?new Color(1,.8f,.35f):new Color(1,.28f+(float)(Math.Sin(model.Clock.CombatTime*4)*.1),.28f);
            for(int i=0;i<writing.Count;i++)
            {
                var text=writing[i]; var position=text==bossName?visualBoss+new BossPoint(0,65):writingPositions[i];
                if(text!=warningText && text!=errorText)text.gameObject.SetActive(text==bossName || first==null);
                text.transform.position=session.Space.ToWorld(position.Vector);
                text.transform.localScale=Vector3.one*(scale*9);
                text.GetComponent<MeshRenderer>().sortingOrder=text==errorText?order+31:text==warningText?order+16:text==bossName?order+5:order-1;
            }
            debugText.gameObject.SetActive(model.Invulnerable || model.Clock.DownloadDebugPaused);
            debugText.text=(model.Invulnerable?"무적":"")+(model.Invulnerable && model.Clock.DownloadDebugPaused?" · ":"")+(model.Clock.DownloadDebugPaused?"다운로드 정지":"");
            if(first!=null && first.FrozenGrabAge<.18 && !model.Minimized)
                session.View.transform.position+=Vector3.right*(float)(Math.Sin(first.FrozenGrabAge*100)*(1-first.FrozenGrabAge/.18)*3*scale);
            ShowTransition(first!=null && first.CombatLocked && first.TransitionAge<1.5);
            if(settings.phase1ErrorSprite!=null)
            {
                var rect=errorPanel.sprite.rect;
                double factor=Math.Min(960/rect.width,520/rect.height),width=rect.width*factor,height=rect.height*factor;
                var pivot=errorPanel.sprite.pivot;
                Place(errorPanel,new BossPoint(960+(pivot.x/rect.width-.5)*width,516-(pivot.y/rect.height-.5)*height),width,height,order+30);
            }
            else Place(errorPanel,new BossPoint(960,516),720,130,order+30);
            RenderTransitionDosWindows(first,order);
            bossName.text=((int)(model.Clock.CombatTime*3)%13)==0?"W1ND0W.exe":((int)(model.Clock.CombatTime*3)%17)==0?"WIN▒OW.exe":"WINDOW.exe";
            for(int i=0;i<4;i++)
            {
                exposureBorder[i].enabled=first!=null && !model.Minimized && first.NearEye;
                exposureBorder[i].color=first!=null && first.Exposed?(flash?Color.red:new Color(.5f,.02f,.02f)):new Color(1,.5f,.1f);
            }
            var body=model.Body;
            Place(exposureBorder[0],new BossPoint(body.x,body.y+body.height*.5),3,body.height,order+6);
            Place(exposureBorder[1],new BossPoint(body.Right,body.y+body.height*.5),3,body.height,order+6);
            Place(exposureBorder[2],new BossPoint(body.x+body.width*.5,body.y),body.width,3,order+6);
            Place(exposureBorder[3],new BossPoint(body.x+body.width*.5,body.Bottom),body.width,3,order+6);
            exposureBar.enabled=first!=null && !model.Minimized && first.Exposed;
            if(first!=null){double width=body.width*Math.Min(1,first.Exposure/first.Definition.exposureSeconds);Place(exposureBar,new BossPoint(body.x+width*.5,body.Bottom-3),width,4,order+7);}
            if(attempt!=model.Attempt || renderedModel!=model)
            { foreach(var eye in eyes.Values) Object.Destroy(eye.root); eyes.Clear(); attempt=model.Attempt; lastTime=model.Clock.CombatTime; }
            renderedModel=model;
            var alive=new HashSet<int>();
            IReadOnlyList<BossEye> visibleEyes=model.Eyes;
            if(first!=null)
            {
                if(first.TransitionAge>=1.5){if(transitionEyes==null)transitionEyes=new BossPhase2Model(settings,0).Eyes;visibleEyes=transitionEyes;}
                else visibleEyes=Array.Empty<BossEye>();
            }
            foreach(var state in visibleEyes)
            {
                alive.Add(state.Id);
                if(!eyes.TryGetValue(state.Id,out var view))
                {
                    var white=Sprite("Eye "+state.Id,settings.eyeSprite!=null?settings.eyeSprite:circle,settings.eyeSprite!=null?Color.white:new Color(.9f,.82f,.75f),centered:true);
                    var pupil=Sprite("Pupil",settings.pupilSprite!=null?settings.pupilSprite:circle,settings.pupilSprite!=null?Color.white:new Color(.08f,.01f,.015f),centered:true);
                    pupil.transform.SetParent(white.transform,false);
                    var tracker=white.gameObject.AddComponent<EyeTracker>(); tracker.enabled=false; tracker.PupilTransform=pupil.transform;
                    tracker.TargetWindowId=BossFightWindow.ContentId; tracker.AddMicroJitter=true;
                    view=new EyeView {root=white.gameObject,white=white,pupil=pupil,tracker=tracker}; eyes.Add(state.Id,view);
                }
                var eyeSize=FitEye(view.white.sprite,state.Radius*2);
                Place(view.white,state.Position,eyeSize.x,eyeSize.y,order+12);
                view.pupil.size=FitEye(view.pupil.sprite,state.Radius*2*settings.phase2PupilDiameterRatio)*scale; view.pupil.sortingOrder=order+13;
                view.tracker.MaxRadius=(float)state.Radius*.35f*scale; view.tracker.JitterAmount=scale*.6f;
                view.tracker.OverrideHasTarget=!model.Minimized;
                view.tracker.OverrideTargetPosition=session.Space.ToWorld(model.Body.Center);
                view.tracker.ManualTick((float)Math.Max(0,model.Clock.CombatTime-lastTime));
            }
            foreach(var id in new List<int>(eyes.Keys)) if(!alive.Contains(id)) {Object.Destroy(eyes[id].root); eyes.Remove(id);}
            lastTime=model.Clock.CombatTime;
            var value=model.DownloadPercent; int count=(int)Math.Floor(value/5);
            var lit=value>=80?new Color(flash?.9f:.5f,.18f,.15f):DownloadActiveColor;
            for(int i=0;i<DownloadBlockCount;i++) blocks[i].color=i<count?lit:DownloadIdleColor;
            percent.text=model.Outcome==BossOutcome.Success?"다운로드 취소됨":$"{Math.Floor(value):0}%";
            if(first!=null && value>=30 && flash)percent.text="계산 중…";
            if(model.Outcome==BossOutcome.Success)foreach(var block in blocks)block.enabled=false;
            if(model.Outcome==BossOutcome.Success)
            {if(successShownAt<0)successShownAt=Time.realtimeSinceStartupAsDouble;percent.enabled=Time.realtimeSinceStartupAsDouble-successShownAt<1.5;}
            hiddenBar.gameObject.SetActive(model.Minimized);
            if(model.Minimized && taskbar.Buttons.TryGetValue(session.GetSnapshot().Windows[0].WindowId,out var button))
            {
                var rt=hiddenBar.rectTransform; rt.SetParent(button.transform,false); rt.anchorMin=rt.anchorMax=new Vector2(0,0);rt.pivot=Vector2.zero;
                rt.anchoredPosition=new Vector2(3,1);rt.sizeDelta=new Vector2((((RectTransform)button.transform).rect.width-6)*(float)(model.HiddenRemaining/settings.minimizeSeconds),3);
                hiddenBar.color=model.HiddenRemaining<.9 && ((int)(model.Clock.CombatTime*8)&1)==0?Color.red:new Color(.9f,.5f,.2f);
            }
            bool entering=entryAge>=0;
            if(entryWindowHidden!=entering)
            {
                foreach(var renderer in windowRenderers)if(renderer!=null)renderer.forceRenderingOff=entering;
                entryWindowHidden=entering;
            }
            downloadRoot.SetActive(!entering);
            bool showBoss=!entering || entryAge>=settings.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds;
            boss.enabled=showBoss;
            if(entering)
            {
                trash.enabled=exposureBar.enabled=false;
                foreach(var edge in exposureBorder)edge.enabled=false;
                bossName.gameObject.SetActive(showBoss);debugText.gameObject.SetActive(false);
            }
            if(entryPanel!=null)
            {
                entryPanel.enabled=entering && entryAge>=settings.EntryPreludeSeconds && entryAge<settings.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds;
                var rect=entryPanel.sprite.rect;double factor=Math.Min(1280/rect.width,800/rect.height);
                Place(entryPanel,new BossPoint(960,516),rect.width*factor,rect.height*factor,order+41+BossFightSettings.EntryWindowCount);
            }
            RenderEntryWindows(entryAge,order);
            RenderEntryDownload(entryAge,order);
            RenderPhase2Spacebar(model,order);
        }
        private void RenderEntryDownload(double age,int order)
        {
            if(age<0 || age>=settings.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds || settings.phase1DownloadSprite==null)
            {ClearEntryDownload();return;}
            if(entryDownloadRoot==null)
            {
                entryDownloadRoot=new GameObject("Boss entry downloading");entryDownloadRoot.transform.SetParent(root.transform,false);
                entryDownloadPanel=Sprite("Downloading window",settings.phase1DownloadSprite,Color.white,centered:true);
                entryDownloadBorder=Sprite("Entry download border",WindowTitleBar.WhiteSprite,new Color(.65f,.67f,.67f));
                entryDownloadTrack=Sprite("Entry download track",WindowTitleBar.WhiteSprite,new Color(.35f,.37f,.4f));
                entryDownloadPanel.transform.SetParent(entryDownloadRoot.transform,false);
                entryDownloadBorder.transform.SetParent(entryDownloadRoot.transform,false);
                entryDownloadTrack.transform.SetParent(entryDownloadRoot.transform,false);
                entryDownloadBlocks=new SpriteRenderer[DownloadBlockCount];
                for(int i=0;i<DownloadBlockCount;i++)
                {
                    entryDownloadBlocks[i]=Sprite("Entry download block "+i,WindowTitleBar.WhiteSprite,DownloadIdleColor);
                    entryDownloadBlocks[i].transform.SetParent(entryDownloadRoot.transform,false);
                }
            }
            var rect=settings.phase1DownloadSprite.rect;double factor=Math.Min(1280/rect.width,700/rect.height);
            Place(entryDownloadPanel,new BossPoint(960,516),rect.width*factor,rect.height*factor,order+30);
            const double magnification=3;double trackWidth=DownloadBlockCount*DownloadBlockPitch*magnification;
            Place(entryDownloadBorder,new BossPoint(960,612),trackWidth+6,DownloadBlockHeight*magnification+6,order+31);
            Place(entryDownloadTrack,new BossPoint(960,612),trackWidth,DownloadBlockHeight*magnification,order+32);
            int count=age>=.5?1:0;
            for(int i=0;i<DownloadBlockCount;i++)
            {
                entryDownloadBlocks[i].color=i<count?DownloadActiveColor:DownloadIdleColor;
                Place(entryDownloadBlocks[i],new BossPoint(960+(i-(DownloadBlockCount-1)*.5)*DownloadBlockPitch*magnification,612),DownloadBlockWidth*magnification,DownloadBlockHeight*magnification,order+33);
            }
        }
        private void ClearEntryDownload()
        {
            if(entryDownloadRoot==null)return;
            entryDownloadRoot.SetActive(false);Object.Destroy(entryDownloadRoot);entryDownloadRoot=null;entryDownloadBlocks=null;
        }
        private void RenderPhase2Spacebar(IBossFightModel model,int order)
        {
            if(!(model is BossPhase2Model)){ClearPhase2Spacebar();return;}
            if(hintedPhase2!=model){hintedPhase2=model;phase2StartedAt=model.Clock.CombatTime;}
            double age=model.Clock.CombatTime-phase2StartedAt-BossFightSettings.Phase2HintDelay;
            if(settings.phase2SpacebarSprite==null || age<0 || age>=BossFightSettings.Phase2HintSeconds || model.Outcome!=BossOutcome.None)
            {ClearPhase2Spacebar();return;}
            if(phase2SpacebarWindows==null)
            {
                phase2SpacebarWindows=new SpriteRenderer[3];
                for(int i=0;i<3;i++)phase2SpacebarWindows[i]=Sprite("Phase 2 Spacebar hint "+(i+1),settings.phase2SpacebarSprite,Color.white,centered:true);
            }
            var rect=settings.phase2SpacebarSprite.rect;double factor=Math.Min(800/rect.width,300/rect.height);
            for(int i=0;i<3;i++)
            {
                phase2SpacebarWindows[i].enabled=age>=i*BossFightSettings.Phase2HintInterval;
                Place(phase2SpacebarWindows[i],new BossPoint(860+i*60,170+i*35),rect.width*factor,rect.height*factor,order+60+i);
            }
        }
        private void ClearPhase2Spacebar()
        {
            if(phase2SpacebarWindows==null)return;
            foreach(var window in phase2SpacebarWindows){window.gameObject.SetActive(false);Object.Destroy(window.gameObject);}
            phase2SpacebarWindows=null;
        }
        private void RenderEntryWindows(double age,int order)
        {
            if(age<settings.EntryDownloadDuration || !settings.HasEntryWindows || age>=settings.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds)
            {ClearEntryWindows();return;}
            if(entryEyeWindows==null)
            {
                entryEyeWindows=new SpriteRenderer[BossFightSettings.EntryWindowCount];
                for(int i=0;i<entryEyeWindows.Length;i++)
                    entryEyeWindows[i]=Sprite("Boss entry eye window "+(i+1),settings.phase1EntryEyeFrames[0],Color.white,centered:true);
            }
            for(int i=0;i<entryEyeWindows.Length;i++)
            {
                var window=entryEyeWindows[i];
                double elapsed=age-settings.EntryDownloadDuration-(BossFightSettings.EntryWindowCount-1)*BossFightSettings.EntryWindowInterval;
                window.enabled=age-settings.EntryDownloadDuration>=i*BossFightSettings.EntryWindowInterval;
                Sprite image=settings.phase1EntryErrorSprite;
                if(age<settings.EntryErrorsStart+i*BossFightSettings.EntryErrorInterval)
                {
                    int frame=0;
                    while(frame<settings.phase1EntryEyeFrames.Length-1 && elapsed>=settings.EntryFrameDuration(frame))
                    {elapsed-=settings.EntryFrameDuration(frame);frame++;}
                    image=settings.phase1EntryEyeFrames[frame];
                }
                window.sprite=Rectangle(image,true);
                var rect=image.rect;double factor=Math.Min(560/rect.width,420/rect.height);
                Place(window,entryWindowPositions[i],rect.width*factor,rect.height*factor,order+41+i);
            }
        }
        private void ClearEntryWindows()
        {
            if(entryEyeWindows==null)return;
            foreach(var window in entryEyeWindows){window.gameObject.SetActive(false);Object.Destroy(window.gameObject);}
            entryEyeWindows=null;
        }
        private void RenderTransitionDosWindows(BossPhase1Model first,int order)
        {
            if(first==null || !first.CombatLocked || first.TransitionAge<1.5 || first.TransitionAge>=2 || settings.phaseTransitionDosSprite==null)
            {ClearTransitionDosWindows();return;}
            if(transitionDosWindows==null)
            {
                transitionDosWindows=new SpriteRenderer[8];
                for(int i=0;i<transitionDosWindows.Length;i++)
                    transitionDosWindows[i]=Sprite("Transition DOS window "+(i+1),settings.phaseTransitionDosSprite,Color.white,centered:true);
            }
            var rect=settings.phaseTransitionDosSprite.rect;
            double factor=Math.Min(1000/rect.width,600/rect.height);
            for(int i=0;i<transitionDosWindows.Length;i++)
            {
                var window=transitionDosWindows[i];
                int step=i%4;
                window.enabled=first.TransitionAge-1.5>=step*.08;
                var position=i<4?new BossPoint(690+step*60,330+step*44):new BossPoint(1230-step*60,702-step*44);
                Place(window,position,rect.width*factor,rect.height*factor,order+32+i);
            }
        }
        private void ClearTransitionDosWindows()
        {
            if(transitionDosWindows==null)return;
            foreach(var window in transitionDosWindows)
            {window.gameObject.SetActive(false);Object.Destroy(window.gameObject);}
            transitionDosWindows=null;
        }
        private static Vector2 FitEye(Sprite sprite,double diameter)
        {
            var size=sprite.rect.size;
            return size*((float)diameter/Mathf.Max(size.x,size.y));
        }
        private void RenderTerminal(BossPhase1Model first,double entryAge)
        {
            if(phase1Terminal==null)return;
            if(entryAge>=0 && entryAge<settings.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds+BossFightSettings.EntryTerminalDelay){phase1Terminal.gameObject.SetActive(false);terminalModel=null;return;}
            phase1Terminal.gameObject.SetActive(first!=null);
            if(first==null)return;
            var lines=first.Definition.terminalLines??Array.Empty<string>();
            if(terminalModel!=first || terminalAttempt!=first.Attempt)
            {
                terminalModel=first;terminalAttempt=first.Attempt;terminalStartedAt=first.Clock.CombatTime;
                terminalPrintedLines=-1;terminalFirstVisible=0;
            }
            double interval=Math.Max(.05,first.Definition.terminalLineSeconds);
            int printed=Math.Min(lines.Length,1+(int)Math.Floor(Math.Max(0,first.Clock.CombatTime-terminalStartedAt)/interval));
            if(printed!=terminalPrintedLines)
            {
                terminalPrintedLines=printed;
                var rect=phase1Terminal.rectTransform.rect;
                phase1Terminal.text=string.Join("\n",lines,terminalFirstVisible,printed-terminalFirstVisible);
                // Keep whole log entries: wrapping may use several screen rows for one emitted entry.
                while(terminalFirstVisible<printed-1 && phase1Terminal.GetPreferredValues(phase1Terminal.text,rect.width,Mathf.Infinity).y>rect.height)
                {
                    terminalFirstVisible++;
                    phase1Terminal.text=string.Join("\n",lines,terminalFirstVisible,printed-terminalFirstVisible);
                }
            }
            phase1Terminal.transform.position=session.Space.ToWorld(new BossPoint(32,28).Vector);
            phase1Terminal.transform.localScale=Vector3.one*(session.Space.Scale*9);
        }
        public void Dispose()
        {
            entryBackdrop?.Dispose();entryBackdrop=null;
            if(entryWindowHidden)foreach(var renderer in windowRenderers)if(renderer!=null)renderer.forceRenderingOff=false;
            ClearTransitionDosWindows();
            ClearEntryWindows();
            ClearEntryDownload();
            ClearPhase2Spacebar();
            hidden?.Dispose();hidden=null;
            foreach(var sprite in rectangularSprites.Values)Object.Destroy(sprite);rectangularSprites.Clear();
            if(hiddenBar!=null) Object.Destroy(hiddenBar.gameObject);
            Object.Destroy(downloadRoot); Object.Destroy(root); Object.Destroy(circle); Object.Destroy(circleTexture);
            Object.Destroy(terminalSymbols);Object.Destroy(terminalSymbolMaterial);Object.Destroy(terminalSymbolTexture);
        }
    }
}
