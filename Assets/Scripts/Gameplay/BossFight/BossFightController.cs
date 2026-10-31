using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.BossFight
{
    public sealed class BossFightEntryContext
    {
        public string RequestId;
        public bool StartAtPhase2;
        public CRTControlLease CrtControl;
        // Ownership transfers to the fight only after entry succeeds.
        public BossEntryBackdrop Backdrop;
    }
    public sealed class BossFightResult
    {
        public string SessionId { get; internal set; }
        public BossOutcome Outcome { get; internal set; }
        public BossRect Body { get; internal set; }
        public BossPoint Trash { get; internal set; }
        public BossPoint Boss { get; internal set; }
        public double DownloadPercent { get; internal set; }
    }
    [DefaultExecutionOrder(500)]
    public sealed class BossFightController : MonoBehaviour
    {
        [SerializeField] private BossFightSettings settings;
        [SerializeField] private BossFightWindow windowPrefab;
        [SerializeField] private bool startPhase2ForValidation;
        [SerializeField] private bool startFullFightForValidation;
        [SerializeField] private double inputHoldback=.05;
        private sealed class Boundary
        {
            public double time; public long sequence; public BossPauseReason reason; public bool active,cancel,space;
            public DesktopSpaceSnapshot snapshot;
        }
        private readonly List<Boundary> boundaries=new List<Boundary>();
        private readonly List<BossInputIntent> intents=new List<BossInputIntent>();
        private readonly HashSet<string> acceptedRequests=new HashSet<string>();
        private long boundarySequence;
        private BossInputSource input;
        private BossInteraction interaction;
        private BossDesktopSession session;
        private BossFightPresentation presentation;
        private TaskbarController taskbar;
        private CRTControlLease crt;
        private bool cleaning;
        private bool entering;
        private double entryStarted;
        public bool IsEntering => IsActive && entering;
        private double EntryAge => Math.Max(0,Model.Clock.CombatTime-entryStarted);
        private double transitionStarted=-1;
        private Rect taskbarHit;
        public IBossFightModel Model { get; private set; }
        public BossDesktopSession Session => session;
        internal bool ObserveNativeApplicationState {get;set;}=true;
        internal BossInputSource InputSource=>input;
        public bool IsActive => session!=null && !session.IsClosed;
        public event Action<BossFightResult> RestoreCancelled;
        public event Action<BossFightResult> BadEndingRequested;
        public void Configure(BossFightSettings rules,BossFightWindow prefab,bool autoPhase2=false)
        { settings=rules; windowPrefab=prefab; startPhase2ForValidation=autoPhase2; }
        public void ConfigureFullFight(BossFightSettings rules,BossFightWindow prefab,bool autoStart=false)
        {Configure(rules,prefab);startFullFightForValidation=autoStart;}
        private void OnEnable()
        { if(Application.isPlaying && (startPhase2ForValidation || startFullFightForValidation)) StartCoroutine(StartValidation()); }
        private IEnumerator StartValidation()
        {
            yield return null;
            if(!Begin(startPhase2ForValidation,out var error)) Debug.LogError("Boss validation entry failed: "+error,this);
        }
        public bool BeginPhase2(out string error)=>Begin(true,out error);
        public bool BeginBossFight(out string error)=>Begin(false,out error);
        public bool BeginBossFight(BossFightEntryContext context,out string error)
        {
            error=null;
            if(context==null || string.IsNullOrEmpty(context.RequestId) || acceptedRequests.Contains(context.RequestId))
            {error="Missing or previously consumed entry request.";return false;}
            if(!Begin(context.StartAtPhase2,out error,context.CrtControl,context.Backdrop))return false;
            acceptedRequests.Add(context.RequestId);return true;
        }
        private bool Begin(bool phase2,out string error,CRTControlLease incomingCrt=null,BossEntryBackdrop backdrop=null)
        {
            error=null;
            if(IsActive || settings==null || windowPrefab==null) { error="Already active or missing boss settings/prefab."; return false; }
            if(incomingCrt!=null && !incomingCrt.IsCurrent){error="Expired CRT entry ownership.";return false;}
            if(!phase2 && (settings.phase1==null || settings.hiddenViewShader==null)){error="Missing phase-one definition or hidden-layer shader.";return false;}
            var manager=WindowManager.Instance; var router=DesktopInputRouter.Instance; var space=DesktopWindowSpace.Instance;
            var desktop=FindAnyObjectByType<DesktopController>(); taskbar=FindAnyObjectByType<TaskbarController>();
            session=new BossDesktopSession(manager,router,space,desktop,taskbar);
            if(!session.TryEnter(windowPrefab,phase2?settings.phase2Window:settings.phase1.initialWindow,out error)) {session=null;return false;}
            try
            {
                session.View.ConfigureResizePadding(settings.resizeBorder,settings.resizeOuterBorder);
                input=new BossInputSource(); interaction=new BossInteraction(settings);
                Model=phase2?(IBossFightModel)new BossPhase2Model(settings,input.CollectionTime):new BossPhase1Model(settings,settings.phase1,input.CollectionTime);
                entering=!phase2 && settings.phase1IntroSprite!=null;entryStarted=Model.Clock.CombatTime;
                input.Timeline.SetSuppressed(entering);
                Model.Respawned+=OnRespawn; Model.Finished+=OnFinished;
                if(Model is BossPhase1Model first){first.Corrupted+=OnCorrupted;first.FirstDrop+=OnFirstDrop;}
                session.Cancelled+=OnCancel; session.SpaceChanging+=OnSpaceChanging; session.Removed+=OnRemoved;
                input.Timeline.Intent+=OnIntent;
                session.View.SetTaskbarIcon(settings.bossSprite);
                presentation=new BossFightPresentation(session,settings,taskbar,backdrop);
                crt=incomingCrt!=null?incomingCrt.Transfer():CRTOverlay.Instance?.AcquireControl();
                crt?.SetBaseHorizontalNoise(settings.crtBaseScanlineScale,settings.crtBaseStreakScale,settings.crtBaseStreakDensityScale);
                crt?.SetLevel(phase2?2:0);
                CacheTaskbarHit(); Present();
                if(!Application.isFocused) boundaries.Add(new Boundary {time=input.CollectionTime,reason=BossPauseReason.FocusLost,active=true});
                return true;
            }
            catch(Exception e)
            {
                error=e.Message; session.AbortEntryPreparation(); Cleanup(BossCleanupCause.EntryFailed); return false;
            }
        }
        private void CacheTaskbarHit()
        {
            taskbarHit=default; Canvas.ForceUpdateCanvases();
            var snapshot=session.GetSnapshot();
            if(snapshot.Windows.Count==0 || !taskbar.Buttons.TryGetValue(snapshot.Windows[0].WindowId,out var button)) return;
            var corners=new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
            var canvas=button.GetComponentInParent<Canvas>(); var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]); var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            taskbarHit=Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        private void OnIntent(BossInputIntent intent)
        {
            if(intent.TimeKind==BossTimeKind.ObservedBoundary)
                boundaries.Add(new Boundary {time=intent.Time,sequence=++boundarySequence,cancel=true});
            else intents.Add(intent);
        }
        private void OnCancel(DesktopCancelReason reason)
        {
            if(reason==DesktopCancelReason.Explicit)
                boundaries.Add(new Boundary {time=InputState.currentTime,sequence=++boundarySequence,cancel=true});
        }
        private void OnSpaceChanging(DesktopSpaceSnapshot snapshot)
            =>boundaries.Add(new Boundary {time=InputState.currentTime,sequence=++boundarySequence,space=true,snapshot=snapshot});
        private void OnApplicationFocus(bool focused)
        {if(ObserveNativeApplicationState) ObserveFocus(focused);}
        internal void ObserveFocus(bool focused)
        { if(IsActive) boundaries.Add(new Boundary {time=InputState.currentTime,sequence=++boundarySequence,reason=BossPauseReason.FocusLost,active=!focused}); }
        private void OnApplicationPause(bool paused)
        { if(ObserveNativeApplicationState && IsActive) boundaries.Add(new Boundary {time=InputState.currentTime,sequence=++boundarySequence,reason=BossPauseReason.ApplicationPaused,active=paused}); }
        private void LateUpdate()
        {
            if(!IsActive || input==null) return;
            double cutoff=Math.Max(Model.Clock.InputTime,input.CollectionTime-inputHoldback);
            boundaries.Sort((a,b)=>{int c=a.time.CompareTo(b.time);return c!=0?c:a.sequence.CompareTo(b.sequence);});
            while(IsActive)
            {
                bool hasInput=input.Timeline.TryPeek(cutoff,out var sample);
                bool hasBoundary=boundaries.Count>0 && boundaries[0].time<=cutoff;
                if(!hasInput && !hasBoundary) break;
                if(hasBoundary && (!hasInput || boundaries[0].time<=sample.time))
                {
                    var boundary=boundaries[0]; boundaries.RemoveAt(0); ApplyBoundary(boundary); continue;
                }
                AdvanceToSample(sample.time);
                intents.Clear(); input.Timeline.Advance(sample.time);
                bool toggle=false,close=false,drop=false,skip=false;
                foreach(var intent in intents)
                {
                    if(!IsActive || entering || Model.CombatLocked || Model.Clock.IsPaused) break;
                    session.Space.TryReference(intent.Position,out var reference);
                    BossPoint point=reference;
                    switch(intent.Kind)
                    {
                        case BossInputKind.Position:
                            interaction.Preview(Model,point,out var positionBody,out var positionTrash);
                            Model.Advance(intent.Time,positionBody,positionTrash);
                            interaction.MovePointer(Model,point); break;
                        case BossInputKind.Press: interaction.Press(Model,point,taskbarHit.Contains(intent.Position)); break;
                        case BossInputKind.Release:
                            interaction.Release(Model,point,taskbarHit.Contains(intent.Position),out var t,out var x,out var d);
                            toggle|=t;close|=x;drop|=d;break;
                        case BossInputKind.Space: toggle=true; break;
                        case BossInputKind.Escape: interaction.Cancel(Model); input.Timeline.ConsumeHeld(); break;
                        case BossInputKind.Invulnerable: if(settings.enableDebugControls)Model.Invulnerable=!Model.Invulnerable;break;
                        case BossInputKind.Restart: if(settings.enableDebugControls)close=true;break;
                        case BossInputKind.Skip: if(settings.enableDebugControls)skip=true;break;
                        case BossInputKind.PauseDownload: if(settings.enableDebugControls)Model.Clock.SetDownloadDebugPause(!Model.Clock.DownloadDebugPaused,intent.Time);break;
                    }
                }
                bool wasHidden=Model.Minimized; int attempt=Model.Attempt;
                Model.ResolveBoundary(toggle,close,drop);
                if(drop && !Model.CombatLocked) Model.ReturnBoss();
                if(skip && Model is BossPhase1Model && Model.Outcome==BossOutcome.None)EnterSecondPhase();
                if(wasHidden!=Model.Minimized || attempt!=Model.Attempt)
                {interaction.Reset();input.Timeline.ConsumeHeld();}
            }
            if(!IsActive) return;
            AdvanceModel(cutoff); Model.ResolveBoundary();
            input.Timeline.Advance(cutoff); intents.Clear();
            if(transitionStarted>=0 && Model.Clock.CombatTime-transitionStarted>=2) EnterSecondPhase();
            Present();
        }
        private void AdvanceModel(double time)
        {
            if(!entering){Model.Advance(time);return;}
            // The entry uses the pausable combat clock, without advancing phase-one damage or download.
            Model.Clock.Advance(time);
            if(EntryAge<settings.EntryPreludeSeconds+BossFightSettings.EntryWarningSeconds+BossFightSettings.EntryTerminalDelay+BossFightSettings.EntryTerminalSeconds)return;
            entering=false;interaction.Reset();
            input.Timeline.SetSuppressed(Model.Clock.IsPaused || Model.CombatLocked);
        }
        private void AdvanceToSample(double time)
        {
            if(entering){AdvanceModel(time);return;}
            if(input.Mouse==null || !input.Timeline.PreviewPosition(time,input.Mouse.deviceId,out var screen) ||
                !session.Space.TryReference(screen,out var reference)) {Model.Advance(time);return;}
            BossPoint to=reference,from=interaction.Pointer;
            var fractions=interaction.MotionBreaks(Model,to);
            double start=Model.Clock.InputTime; int attempt=Model.Attempt;
            for(int i=1;i<fractions.Count;i++)
            {
                var point=BossPoint.Lerp(from,to,fractions[i]);
                interaction.Preview(Model,point,out var body,out var trash);
                Model.Advance(start+(time-start)*fractions[i],body,trash);
                if(i<fractions.Count-1) Model.ResolveBoundary();
                if(Model.Attempt!=attempt || Model.Outcome!=BossOutcome.None) {Model.Advance(time);break;}
            }
        }
        private void ApplyBoundary(Boundary boundary)
        {
            double time=Math.Max(Model.Clock.InputTime,boundary.time);
            AdvanceModel(time);
            if(boundary.space)
            {
                Model.Clock.SetPause(BossPauseReason.SpaceUnavailable,true,time);
                interaction.Cancel(Model); input.Timeline.ConsumeHeld();
                bool valid=session.CommitSpace(boundary.snapshot);
                Model.Clock.SetPause(BossPauseReason.SpaceUnavailable,!valid,time);
                if(!Model.Clock.IsPaused) Model.Teleport(Model.Body,Model.Trash);
                if(valid) CacheTaskbarHit();
            }
            else if(boundary.cancel) {interaction.Cancel(Model);input.Timeline.ConsumeHeld();}
            else
            {
                // Pause before teleport cancellation so its destination cannot cause damage while suspended.
                Model.Clock.SetPause(boundary.reason,boundary.active,time);
                interaction.Cancel(Model);input.Timeline.ConsumeHeld();
                if(!Model.Clock.IsPaused) Model.Teleport(Model.Body,Model.Trash);
            }
            input.Timeline.SetSuppressed(entering || Model.Clock.IsPaused || Model.CombatLocked);
            Model.ResolveBoundary();
        }
        private void OnRespawn() {interaction.Reset();input?.Timeline.ConsumeHeld();if(Model is BossPhase1Model)crt?.SetLevel(0);}
        private void OnCorrupted()
        {
            // Exposure drops the recycle bin, not the window controls needed to escape it.
            // A terminal corruption still clears every interaction through OnRespawn.
            if(interaction.Drag==BossDrag.Trash)
            {interaction.Reset();input?.Timeline.ConsumeHeld();}
            crt?.SetLevel(((BossPhase1Model)Model).Corruption);crt?.Spike();
        }
        private void OnFirstDrop()
        {transitionStarted=Model.Clock.CombatTime;interaction.Reset();input.Timeline.SetSuppressed(true);presentation?.ShowTransition(true);}
        private void EnterSecondPhase()
        {
            var first=Model as BossPhase1Model;if(first==null)return;
            entering=false;
            first.Corrupted-=OnCorrupted;first.FirstDrop-=OnFirstDrop;first.Respawned-=OnRespawn;first.Finished-=OnFinished;
            var time=Model.Clock.InputTime;var pauses=Model.Clock.PauseReasons;
            Model=new BossPhase2Model(settings,time){Invulnerable=first.Invulnerable};Model.Clock.SetPause(pauses,true,time);
            Model.Clock.SetDownloadDebugPause(first.Clock.DownloadDebugPaused,time);
            Model.Respawned+=OnRespawn;Model.Finished+=OnFinished;transitionStarted=-1;
            interaction.Reset();input.Timeline.ConsumeHeld();input.Timeline.SetSuppressed(Model.Clock.IsPaused);
            presentation?.ShowTransition(false);crt?.SetLevel(2);crt?.Spike(.35f);
        }
        private void OnFinished(BossOutcome outcome)
        {
            session.SetOutcome(outcome); interaction.Reset();input.Timeline.SetSuppressed(true);
            var result=new BossFightResult {SessionId=session.SessionId,Outcome=outcome,Body=Model.Body,Trash=Model.Trash,Boss=Model.Boss,DownloadPercent=Model.DownloadPercent};
            if(outcome==BossOutcome.Success) RestoreCancelled?.Invoke(result); else BadEndingRequested?.Invoke(result);
        }
        private void Present()
        {
            if(session.Space.Valid)
            {
                session.SetBody(Model.Body);
                var process=WindowManager.Instance?.GetProcess(BossFightWindow.ContentId);
                if(process!=null && (process.State==WindowState.Minimized)!=Model.Minimized)
                    session.Request(Model.Minimized?WindowAction.Minimize:WindowAction.Restore);
                presentation?.Render(Model,taskbar,entering?EntryAge:-1);
            }
        }
        public bool AllowEnding(string sessionId,IEnumerable<string> contentIds) => IsActive && session.AllowEnding(sessionId,contentIds);
        public CRTControlLease TransferCrtToEnding(string sessionId) => IsActive && session.SessionId==sessionId && Model.Outcome!=BossOutcome.None ? crt?.Transfer() : null;
        public bool CompleteEnding(string sessionId)
        {
            if(!IsActive || sessionId!=session.SessionId || Model.Outcome==BossOutcome.None) return false;
            Cleanup(BossCleanupCause.EndingFinished);return true;
        }
        private void OnRemoved(BossCleanupCause cause) => Cleanup(cause);
        private void OnDisable() {if(IsActive) Cleanup(BossCleanupCause.SceneUnloading);}
        private void Cleanup(BossCleanupCause cause)
        {
            if(cleaning) return; cleaning=true;
            input?.Dispose();input=null;
            if(Model!=null) {Model.Respawned-=OnRespawn;Model.Finished-=OnFinished;}
            if(Model is BossPhase1Model first){first.Corrupted-=OnCorrupted;first.FirstDrop-=OnFirstDrop;}
            if(session!=null)
            {
                session.Cancelled-=OnCancel;session.SpaceChanging-=OnSpaceChanging;session.Removed-=OnRemoved;
                session.Exit(cause,cause==BossCleanupCause.EndingFinished);
            }
            presentation?.Dispose();presentation=null;crt?.Release();crt=null;session=null;
            boundaries.Clear();intents.Clear();transitionStarted=-1;entering=false;cleaning=false;
        }
    }
}
