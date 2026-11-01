using UnityEngine;

namespace WindowExe.BossFight
{
    [CreateAssetMenu(menuName="WINDOW.exe/Boss Fight Settings")]
    public sealed class BossFightSettings : ScriptableObject
    {
        public int randomSeed=117;
        public bool enableDebugControls=true;
        [Range(0,1)] public float crtBaseScanlineScale=.35f,crtBaseStreakScale=.2f,crtBaseStreakDensityScale=.25f;
        public BossRect phase2Window=new BossRect(105,435,300,210);
        public BossPoint phase2Trash=new BossPoint(180,540), phase2Boss=new BossPoint(1782,525);
        public double downloadSeconds=240, minimizeSeconds=4.5;
        public double trashMargin=39, trashGrabRadius=42, bossGrabRadius=51, captureInset=30, dropRadius=63;
        public double minWidth=90,minHeight=90,maxWidth=600,maxHeight=510;
        public double eyeInset=3, titleHeight=36, resizeBorder=9, resizeOuterBorder=24;
        public double wallWarningSeconds=1.8, wallAttackPauseSeconds=2.5, wallSpeed=248, firstWallSpeed=186;
        public double wallRadius=27, wallSpacing=51, aimedSpeed=278, aimedRadius=27, aimedMinimumDistance=390;
        public double aimedHorizontalRange=240, directRange=570, directInterval=3.5;
        public double scatterInterval=1.1, aimedInterval=.45, scatterSwayInterval=.6;
        public double[] attackDurations={2.2,4.5,1.6,.9,2,3.5,1.8};
        public Sprite trashSprite, bossSprite, eyeSprite, pupilSprite;
        [Range(.1f,.65f)] public float phase2PupilDiameterRatio=.55f;
        public Sprite phase2BackgroundSprite;
        [Range(0,.2f)] public float phase2EyeSizeVariation=.12f;
        public BossStaticEye[] phase2FloatingEyes={
            new BossStaticEye(790,310,34),new BossStaticEye(1120,720,30),new BossStaticEye(1450,330,38),new BossStaticEye(960,530,70)};
        public Sprite phase2SpacebarSprite;
        public const double Phase2HintDelay=1,Phase2HintSeconds=2,Phase2HintInterval=.08;
        public double EyeSizeVariation=>System.Math.Max(0,System.Math.Min(.2,phase2EyeSizeVariation));
        public double MaximumWallRadius=>System.Math.Max(wallRadius*(1+EyeSizeVariation),wallSpacing*.5+.25);
        public Sprite phase1EyeSprite, phase1PupilSprite;
        public Sprite phase1HiddenBackgroundSprite;
        public Sprite phase1ErrorSprite;
        public Sprite phase1IntroSprite;
        public Sprite phase1DownloadSprite;
        public AudioClip entryDownloadSound,entryEyeSound,entryErrorSound;
        public AudioClip phase1Music,phase2Music;
        public AudioClip phase1EyeContactSound,phase2EyeContactSound;
        public AudioClip phase1DropSound;
        [Min(0f)] public float phase1DropGain=.8f,transitionDosGain=.15f;
        public AudioClip phase2SuccessSound;
        public UnityEngine.Video.VideoClip normalEndingVideo,badEndingVideo;
        public string endingReturnScene="StartScene";
        [Min(0f)] public float phase2SuccessGain=.8f,phase2SuccessErrorGain=.08f;
        public const int SuccessErrorCount=8;
        public const double SuccessErrorInterval=.08;
        [Range(0f,1f)] public float musicVolume=.7f;
        [Range(0f,1f)] public float entrySoundVolume=.7f;
        [Min(0f)] public float entryDownloadGain=8f;
        [Min(0f)] public float entryErrorGain=.3f;
        public const double EntryDownloadSeconds=3;
        public double EntryDownloadDuration=>phase1DownloadSprite!=null?EntryDownloadSeconds:0;
        public Sprite[] phase1EntryEyeFrames=System.Array.Empty<Sprite>();
        public double[] phase1EntryFrameSeconds=System.Array.Empty<double>();
        public Sprite phase1EntryErrorSprite;
        public const int EntryWindowCount=15;
        public const double EntryWindowInterval=.06,EntryErrorInterval=.08,EntryErrorHold=.3,EntryInitialHoldSeconds=2;
        public const double EntryWarningSeconds=1.5,EntryTerminalDelay=1,EntryTerminalSeconds=2;
        public bool HasEntryWindows=>phase1EntryErrorSprite!=null && phase1EntryEyeFrames!=null && phase1EntryEyeFrames.Length>0 && phase1EntryFrameSeconds!=null && phase1EntryFrameSeconds.Length==phase1EntryEyeFrames.Length;
        public double EntryEyesDuration
        {
            get {double seconds=0;for(int i=0;i<phase1EntryFrameSeconds.Length;i++)seconds+=EntryFrameDuration(i);return seconds;}
        }
        public double EntryFrameDuration(int frame)=>frame==0?EntryInitialHoldSeconds:System.Math.Max(.01,phase1EntryFrameSeconds[frame]);
        public double EntryErrorsStart=>EntryDownloadDuration+(EntryWindowCount-1)*EntryWindowInterval+EntryEyesDuration;
        public double EntryPreludeSeconds=>HasEntryWindows?EntryErrorsStart+(EntryWindowCount-1)*EntryErrorInterval+EntryErrorHold:EntryDownloadDuration;
        public Sprite phaseTransitionDosSprite;
        public BossPhaseDefinition phase1;
        public Shader hiddenViewShader;
        public int hiddenLayer=8,hiddenRendererIndex=1;
        public Color phase2Background=new Color(.23f,.025f,.045f),safeDesktop=new Color(.025f,.18f,.42f);
    }
}
