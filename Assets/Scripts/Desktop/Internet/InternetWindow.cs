using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    public sealed class InternetWindow : Window
    {
        public enum InternetStep
        {
            LockScreen,     // 1단계: 4자리 암호 잠금 화면
            NetworkPuzzle,  // 2단계: 와이파이 하노이 탑 네트워크 퍼즐
            SearchHistory,  // 3단계: 검색 기록 화면
            Result404,      // 4단계: 404 결과 화면
            ResultGun       // 4단계: 총 결과 화면
        }

        public enum InternetPhase
        {
            NetworkPuzzle,
            SearchHistory,
            Result404,
            ResultGun
        }

        private enum HanoiSoundType
        {
            Select,
            Move,
            Error,
            Connected,
            Shot,
            Ricochet
        }

        public const int HanoiPegCount = 3;
        public const int HanoiDiskCount = 3;

        [Header("Step 1 - Passcode Lock")]
        [SerializeField] private string passcode = "1971";
        [SerializeField] private float errorMessageDuration = 1.8f;
        [SerializeField] private Color passcodeDotColor = Color.black;

        [Header("Wi-Fi")]
        [SerializeField] private global::WindowExe.Desktop.WifiConnection wifi;

        [Header("Browser Background")]
        [SerializeField] private Sprite browserBackgroundSprite;
        [SerializeField] private Color browserBackgroundTint = Color.white;
        [SerializeField] private bool artworkIncludesTitleBar = true;

        [Header("Search History")]
        [SerializeField] private string firstHistory = "오래된 컴퓨터 수리 방법";
        [SerializeField] private string secondHistory = "삭제한 파일 복구하는 법";
        [SerializeField] private string scrambledHistory = "#%@!_E?R?O?R";

        [Header("Result Window IDs")]
        [SerializeField] private string firstResultId = "history-first";
        [SerializeField] private string secondResultId = "history-second";
        [SerializeField] private string gunFileResultId = "gun-file";

        [Header("WINDOW.exe Reveal")]
        [SerializeField] private string requiredRevealWindowId = "WINDOW.exe";
        [SerializeField, Min(0f)] private float requiredOverlapWidth = 0.3f;
        [SerializeField, Min(0f)] private float requiredOverlapHeight = 0.15f;

        [Header("Phase 3 - 404 Result")]
        [SerializeField] private Sprite notFound404Sprite;
        [SerializeField] private Color notFound404Tint = Color.white;
        [SerializeField] private Vector2 notFound404Size = new Vector2(7f, 4.2f);
        [SerializeField] private Vector2 notFound404Offset = Vector2.zero;

        [Header("Phase 3 - Gun Result")]
        [SerializeField] private Sprite gunResultSprite;
        [SerializeField] private Color gunResultTint = Color.white;
        [SerializeField] private Vector2 gunResultSize = new Vector2(7f, 4.2f);
        [SerializeField] private Vector2 gunResultOffset = Vector2.zero;

        [Header("Phase 3 - Gun Shooting")]
        [SerializeField] private string targetKeyWindowId = "key-image";
        [SerializeField] private Vector2 gunMuzzleNormalizedOffset = new Vector2(-0.38f, 0.24f);
        [SerializeField] private AudioClip gunShotClip;
        [SerializeField] private AudioClip gunRicochetClip;
        [SerializeField] private float bulletTrailDuration = 0.20f;
        [SerializeField] private float bulletTrailThickness = 0.045f;
        [SerializeField] private Color bulletTrailColor = new Color(1f, 0.95f, 0.45f, 0.95f);
        [Tooltip("총구가 WINDOW.exe 뷰포트 안에 있을 때에만 총 클릭 시 발사가 가능한지 여부 (기본: true)")]
        [SerializeField] private bool requireMuzzleInWindowExeToShoot = true;

        [Header("Offline Network UI")]
        [SerializeField] private Sprite offlineIconSprite;
        [SerializeField] private Color offlineIconTint = Color.white;
        [SerializeField] private Vector2 offlineIconSize = new Vector2(0.85f, 0.85f);
        [SerializeField] private Vector2 offlineIconOffset = Vector2.zero;

        [Header("Hanoi Network Puzzle")]
        [SerializeField] private Color blindDiskColor = new Color(0.50f, 0.53f, 0.58f, 1f);
        [SerializeField] private Color[] revealedDiskColors =
        {
            new Color(0.90f, 0.26f, 0.26f, 1f), // 원반 1 (가장 넓음, 맨 아래)
            new Color(0.95f, 0.72f, 0.18f, 1f), // 원반 2 (중간)
            new Color(0.24f, 0.62f, 0.96f, 1f)  // 원반 3 (가장 좁음, 맨 위)
        };
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.12f;
        [SerializeField, Min(0.005f)] private float shakeMagnitude = 0.06f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip diskSelectClip;
        [SerializeField] private AudioClip diskMoveClip;
        [SerializeField] private AudioClip diskErrorClip;
        [SerializeField] private AudioClip networkConnectedClip;

        [Header("Font")]
        [SerializeField] private TMP_FontAsset koreanFont;

        private static Sprite whitePixel;
        private static Sprite circleSprite;
        private static AudioClip fallbackSelectClip;
        private static AudioClip fallbackMoveClip;
        private static AudioClip fallbackErrorClip;
        private static AudioClip fallbackConnectedClip;
        private static AudioClip fallbackGunShotClip;
        private static AudioClip fallbackGunRicochetClip;

        // Step 1 - 비밀번호 잠금 전역 유지 상태
        private static bool isPasswordUnlocked = false;

        public static bool IsPasswordUnlocked
        {
            get => isPasswordUnlocked;
            set => isPasswordUnlocked = value;
        }

        public static void ResetPasswordState()
        {
            isPasswordUnlocked = false;
        }

        // 정답 1971에 맞춘 WindowBack 투시 동그라미 직경 (1: 엄청작음, 9: 엄청큼, 7: 조금큼, 1: 엄청작음)
        private static readonly float[] PasscodeDotDiameters = { 0.10f, 0.44f, 0.26f, 0.10f };

        public const int KeypadButtonCount = 11;
        private static readonly string[] KeypadLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0" };

        private Transform contentRoot;
        private Transform backContentRoot;
        private WindowTitleBar ieTitleBar;

        // Step 1 암호 잠금 화면 - WindowFront & WindowBack
        private SpriteRenderer lockDialogBorderFront;
        private SpriteRenderer lockDialogFillFront;
        private SpriteRenderer lockDialogBorderBack;
        private SpriteRenderer lockDialogFillBack;

        private SpriteRenderer lockInputBorderFront;
        private SpriteRenderer lockInputFillFront;
        private SpriteRenderer lockInputBorderBack;
        private SpriteRenderer lockInputFillBack;

        private readonly SpriteRenderer[] passcodeBackDots = new SpriteRenderer[4];

        private readonly SpriteRenderer[] keypadButtonBordersFront = new SpriteRenderer[KeypadButtonCount];
        private readonly SpriteRenderer[] keypadButtonFillsFront = new SpriteRenderer[KeypadButtonCount];
        private readonly SpriteRenderer[] keypadButtonBordersBack = new SpriteRenderer[KeypadButtonCount];
        private readonly SpriteRenderer[] keypadButtonFillsBack = new SpriteRenderer[KeypadButtonCount];
        private readonly TextMeshPro[] keypadButtonLabelsFront = new TextMeshPro[KeypadButtonCount];
        private readonly TextMeshPro[] keypadButtonLabelsBack = new TextMeshPro[KeypadButtonCount];
        private readonly Rect[] keypadButtonRects = new Rect[KeypadButtonCount];

        private TextMeshPro lockTitleLabel;
        private TextMeshPro backLockTitleLabel;
        private TextMeshPro lockGuideLabel;
        private TextMeshPro backLockGuideLabel;
        private TextMeshPro lockErrorLabel;
        private TextMeshPro backLockErrorLabel;
        private TextMeshPro lockInputMaskLabel;
        private TextMeshPro backLockInputMaskLabel;

        private string currentPasscodeInput = "";
        private float errorMessageTimer = 0f;

        // WindowFront / WindowBack 렌더러
        private SpriteRenderer artwork;
        private SpriteRenderer frontSearchBorder;
        private SpriteRenderer frontSearchFill;
        private SpriteRenderer frontArrowFill;
        private SpriteRenderer frontListBorder;
        private SpriteRenderer frontListFill;
        private readonly SpriteRenderer[] frontRowBackgrounds = new SpriteRenderer[3];
        private SpriteRenderer frontThirdRowMask;

        private SpriteRenderer searchBorder;
        private SpriteRenderer searchFill;
        private SpriteRenderer arrowFill;
        private SpriteRenderer listBorder;
        private SpriteRenderer listFill;
        private readonly SpriteRenderer[] rowBackgrounds = new SpriteRenderer[3];

        // Phase 3 결과 화면 - WindowFront & WindowBack
        private SpriteRenderer notFound404Front;
        private SpriteRenderer notFound404Back;
        private SpriteRenderer gunResultFront;
        private SpriteRenderer gunResultBack;

        // Phase 1 하노이 탑 - WindowFront (평상시 보이지만 WINDOW.exe에는 가려짐)
        private SpriteRenderer offlineIconFront;
        private SpriteRenderer hanoiBackdropFront;
        private SpriteRenderer pegBaseFront;
        private readonly SpriteRenderer[] pegPolesFront = new SpriteRenderer[HanoiPegCount];
        private readonly SpriteRenderer[] diskFrontBorders = new SpriteRenderer[HanoiDiskCount];
        private readonly SpriteRenderer[] diskFrontRenderers = new SpriteRenderer[HanoiDiskCount];

        // Phase 1 하노이 탑 - WindowBack (WINDOW.exe를 통해서만 보임)
        private SpriteRenderer backArtwork;
        private SpriteRenderer offlineIconBack;
        private SpriteRenderer hanoiBackdropBack;
        private SpriteRenderer pegBaseBack;
        private readonly SpriteRenderer[] pegPolesBack = new SpriteRenderer[HanoiPegCount];
        private readonly SpriteRenderer[] diskBackBorders = new SpriteRenderer[HanoiDiskCount];
        private readonly SpriteRenderer[] diskBackRenderers = new SpriteRenderer[HanoiDiskCount];

        private TextMeshPro heading;
        private TextMeshPro searchLabel;
        private TextMeshPro arrowLabel;
        private TextMeshPro backHeading;
        private TextMeshPro backSearchLabel;
        private TextMeshPro backArrowLabel;
        private TextMeshPro offlineLabel;
        private TextMeshPro backOfflineLabel;
        private TextMeshPro glitchRed;
        private TextMeshPro glitchBlue;

        private readonly TextMeshPro[] rowLabels = new TextMeshPro[3];
        private readonly TextMeshPro[] backRowLabels = new TextMeshPro[3];
        private readonly Rect[] rowRects = new Rect[3];
        private readonly Rect[] pegRects = new Rect[HanoiPegCount];

        private Rect searchRect;
        private Rect thirdRowRevealLocalRect;
        private Rect hanoiBoardLocalRect;

        // 하노이 탑 상태: 각 막대의 바닥(0)부터 맨 위(Count-1)까지 원반 인덱스(0, 1, 2) 저장
        // 원반 인덱스 d의 숫자는 d + 1 (1, 2, 3)이며, 자신의 아래에는 자신보다 작은 숫자의 원반만 올 수 있음
        private readonly List<int>[] pegs =
        {
            new List<int>(HanoiDiskCount),
            new List<int>(HanoiDiskCount),
            new List<int>(HanoiDiskCount)
        };

        private readonly Vector2[] diskBasePositions = new Vector2[HanoiDiskCount];
        private readonly float[] diskBackWidths = new float[HanoiDiskCount];
        private readonly float[] diskShakeTimers = new float[HanoiDiskCount];
        private readonly float[] diskShakeMagnitudes = new float[HanoiDiskCount];

        private float currentBlindDiskWidth = 1f;
        private float currentDiskHeight = 0.25f;
        private int selectedPeg = -1;
        private int lastSortingOrder;

        private bool initialized;
        private bool initializing;
        private bool hanoiInitialized;
        private bool localConnected;
        private bool dropdownOpen;
        private bool decoded;
        private bool lastOnline;
        private float gunShakeTimer;
        private InternetPhase phase3Result = InternetPhase.SearchHistory;

        private string selectedText = "검색 기록을 선택하세요";

        public event Action<ImageWindow> OnGunFired;

        public bool IsDecoded => decoded;
        public bool DropdownOpen => dropdownOpen;
        public bool IsGunShaking => gunShakeTimer > 0f;
        public string TargetKeyWindowId { get => targetKeyWindowId; set => targetKeyWindowId = value; }
        public AudioClip GunShotClip { get => gunShotClip; set => gunShotClip = value; }
        public AudioClip GunRicochetClip { get => gunRicochetClip; set => gunRicochetClip = value; }
        public bool LastShotWasRicochet { get; private set; }
        public Vector2 LastTracerStart { get; private set; }
        public Vector2 LastTracerEnd { get; private set; }
        public Vector2? LastRicochetEnd { get; private set; }
        public bool RequireMuzzleInWindowExeToShoot { get => requireMuzzleInWindowExeToShoot; set => requireMuzzleInWindowExeToShoot = value; }

        public string Passcode { get => passcode; set => passcode = value; }
        public string CurrentPasscodeInput => currentPasscodeInput;
        public bool IsPasscodeErrorShowing => errorMessageTimer > 0f;

        public InternetStep CurrentStep
        {
            get
            {
                if (!IsPasswordUnlocked) return InternetStep.LockScreen;
                if (!IsOnline) return InternetStep.NetworkPuzzle;
                if (phase3Result == InternetPhase.Result404) return InternetStep.Result404;
                if (phase3Result == InternetPhase.ResultGun) return InternetStep.ResultGun;
                return InternetStep.SearchHistory;
            }
        }

        public InternetPhase CurrentPhase
        {
            get
            {
                if (!IsPasswordUnlocked || !IsOnline) return InternetPhase.NetworkPuzzle;
                if (phase3Result == InternetPhase.Result404 || phase3Result == InternetPhase.ResultGun)
                    return phase3Result;
                return InternetPhase.SearchHistory;
            }
        }
        public int SelectedPeg => selectedPeg;
        public AudioClip LastPlayedClip { get; private set; }
        public int SoundPlayCount { get; private set; }
        public Sprite OfflineIconSprite
        {
            get => offlineIconSprite;
            set => SetOfflineIconSprite(value);
        }

        public void SetOfflineIconSprite(Sprite sprite)
        {
            offlineIconSprite = sprite;
            ApplyOfflineIcon();
            if (initialized && ReferencesReady())
            {
                ApplySize(size);
                RefreshUI();
            }
        }

        public Sprite NotFound404Sprite
        {
            get => notFound404Sprite;
            set => SetNotFound404Sprite(value);
        }

        public void SetNotFound404Sprite(Sprite sprite)
        {
            notFound404Sprite = sprite;
            ApplyPhase3Sprites();
            if (initialized && ReferencesReady())
            {
                ApplySize(size);
                RefreshUI();
            }
        }

        public Sprite GunResultSprite
        {
            get => gunResultSprite;
            set => SetGunResultSprite(value);
        }

        public void SetGunResultSprite(Sprite sprite)
        {
            gunResultSprite = sprite;
            ApplyPhase3Sprites();
            if (initialized && ReferencesReady())
            {
                ApplySize(size);
                RefreshUI();
            }
        }

        public Vector2 GetNotFound404FrontSize() => notFound404Front != null ? (Vector2)notFound404Front.transform.localScale : Vector2.zero;
        public Vector2 GetGunResultFrontSize() => gunResultFront != null ? (Vector2)gunResultFront.transform.localScale : Vector2.zero;

        public bool IsOnline
        {
            get
            {
                var connection = GetWifiConnection();
                return connection != null ? connection.IsConnected : localConnected;
            }
        }

        public void SetWifi(global::WindowExe.Desktop.WifiConnection connection)
        {
            wifi = connection;
        }

        private global::WindowExe.Desktop.WifiConnection GetWifiConnection()
        {
            return wifi != null
                ? wifi
                : global::WindowExe.Desktop.WifiConnection.Instance;
        }

        public static int GetDiskNumber(int diskIndex) => diskIndex + 1;

        public IReadOnlyList<int> GetPegDiskNumbers(int pegIndex)
        {
            if (pegIndex < 0 || pegIndex >= HanoiPegCount)
                return Array.Empty<int>();

            var list = pegs[pegIndex];
            var numbers = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
                numbers[i] = GetDiskNumber(list[i]);
            return numbers;
        }

        public Vector2 GetDiskFrontSize(int diskIndex)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount || diskFrontRenderers[diskIndex] == null)
                return Vector2.zero;
            return diskFrontRenderers[diskIndex].transform.localScale;
        }

        public Vector2 GetDiskBackSize(int diskIndex)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount || diskBackRenderers[diskIndex] == null)
                return Vector2.zero;
            return diskBackRenderers[diskIndex].transform.localScale;
        }

        public Color GetDiskFrontColor(int diskIndex)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount || diskFrontRenderers[diskIndex] == null)
                return Color.clear;
            return diskFrontRenderers[diskIndex].color;
        }

        public Color GetDiskBackColor(int diskIndex)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount || diskBackRenderers[diskIndex] == null)
                return Color.clear;
            return diskBackRenderers[diskIndex].color;
        }

        public string GetDiskFrontSortingLayer(int diskIndex)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount || diskFrontRenderers[diskIndex] == null)
                return string.Empty;
            return diskFrontRenderers[diskIndex].sortingLayerName;
        }

        public string GetDiskBackSortingLayer(int diskIndex)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount || diskBackRenderers[diskIndex] == null)
                return string.Empty;
            return diskBackRenderers[diskIndex].sortingLayerName;
        }

        public Vector2 GetDiskPosition(int diskIndex)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount || diskFrontRenderers[diskIndex] == null)
                return Vector2.zero;
            return diskFrontRenderers[diskIndex].transform.localPosition;
        }

        public bool IsDiskShaking(int diskIndex)
        {
            return diskIndex >= 0 && diskIndex < HanoiDiskCount && diskShakeTimers[diskIndex] > 0f;
        }

        public void SetDropdownOpen(bool open)
        {
            dropdownOpen = open;
            if (initialized && ReferencesReady())
            {
                RefreshUI();
                if (IsOnline && dropdownOpen && !decoded)
                    TryRevealThirdHistory();
            }
        }

        public string GetFrontRowText(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= 3 || rowLabels[rowIndex] == null)
                return string.Empty;
            return rowLabels[rowIndex].text;
        }

        public string GetBackRowText(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= 3 || backRowLabels[rowIndex] == null)
                return string.Empty;
            return backRowLabels[rowIndex].text;
        }

        public string GetFrontRowSortingLayer(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= 3 || rowLabels[rowIndex] == null)
                return string.Empty;
            var renderer = rowLabels[rowIndex].GetComponent<Renderer>();
            return renderer != null ? renderer.sortingLayerName : string.Empty;
        }

        public string GetBackRowSortingLayer(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= 3 || backRowLabels[rowIndex] == null)
                return string.Empty;
            var renderer = backRowLabels[rowIndex].GetComponent<Renderer>();
            return renderer != null ? renderer.sortingLayerName : string.Empty;
        }

        public Rect GetSearchRowWorldRect(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= 3)
                return default;
            return WorldRect(rowRects[rowIndex], transform);
        }

        protected override void InitializePrimary()
        {
            if (initializing) return;

            if (!hanoiInitialized)
            {
                ResetHanoiState();
                hanoiInitialized = true;
            }

            if (initialized && ReferencesReady())
            {
                ApplyArtwork();
                ApplyOfflineIcon();
                ApplyPhase3Sprites();
                HideDefaultArtwork();
                ApplySize(size);
                RefreshUI();
                ApplyLayerOrders(GetBaseSortingOrder());
                return;
            }

            initializing = true;

            try
            {
                minSize = new Vector2(
                    Mathf.Max(minSize.x, 7.2f),
                    Mathf.Max(minSize.y, 4f)
                );

                size = new Vector2(
                    Mathf.Max(size.x, minSize.x),
                    Mathf.Max(size.y, minSize.y)
                );

                contentRoot = transform.Find("Primary");
                if (contentRoot == null)
                {
                    var root = new GameObject("Primary");
                    root.transform.SetParent(transform, false);
                    contentRoot = root.transform;
                }

                var secondaryRoot = transform.Find("Secondary");
                if (secondaryRoot == null)
                {
                    var sec = new GameObject("Secondary");
                    sec.transform.SetParent(transform, false);
                    secondaryRoot = sec.transform;
                }

                backContentRoot = secondaryRoot.Find("IE_BackContent");
                if (backContentRoot == null)
                {
                    var backRoot = new GameObject("IE_BackContent");
                    backRoot.transform.SetParent(secondaryRoot, false);
                    backContentRoot = backRoot.transform;
                }

                // WindowFront 브라우저 배경 및 검색/드롭다운 UI 패널
                artwork = CreatePanel(contentRoot, "IE_BrowserArtwork", Color.white, PrimarySortingLayerName);
                frontSearchBorder = CreatePanel(contentRoot, "IE_SearchBorder", new Color(.35f, .35f, .35f), PrimarySortingLayerName);
                frontSearchFill = CreatePanel(contentRoot, "IE_SearchFill", Color.white, PrimarySortingLayerName);
                frontArrowFill = CreatePanel(contentRoot, "IE_ArrowFill", new Color(.80f, .80f, .80f), PrimarySortingLayerName);
                frontListBorder = CreatePanel(contentRoot, "IE_ListBorder", new Color(.35f, .35f, .35f), PrimarySortingLayerName);
                frontListFill = CreatePanel(contentRoot, "IE_ListFill", Color.white, PrimarySortingLayerName);

                for (int i = 0; i < 3; i++)
                {
                    frontRowBackgrounds[i] = CreatePanel(
                        contentRoot,
                        "IE_RowBackground_" + i,
                        i % 2 == 0 ? Color.white : new Color(.94f, .95f, .97f),
                        PrimarySortingLayerName
                    );
                }

                frontThirdRowMask = CreatePanel(contentRoot, "IE_FrontThirdRowMask", Color.white, PrimarySortingLayerName);

                // WindowBack 브라우저 배경 및 검색/드롭다운 UI 패널
                backArtwork = CreatePanel(backContentRoot, "IE_BackBrowserArtwork", Color.white, SecondarySortingLayerName);
                searchBorder = CreatePanel(backContentRoot, "IE_SearchBorder", new Color(.35f, .35f, .35f), SecondarySortingLayerName);
                searchFill = CreatePanel(backContentRoot, "IE_SearchFill", Color.white, SecondarySortingLayerName);
                arrowFill = CreatePanel(backContentRoot, "IE_ArrowFill", new Color(.80f, .80f, .80f), SecondarySortingLayerName);
                listBorder = CreatePanel(backContentRoot, "IE_ListBorder", new Color(.35f, .35f, .35f), SecondarySortingLayerName);
                listFill = CreatePanel(backContentRoot, "IE_ListFill", Color.white, SecondarySortingLayerName);

                for (int i = 0; i < 3; i++)
                {
                    rowBackgrounds[i] = CreatePanel(
                        backContentRoot,
                        "IE_RowBackground_" + i,
                        i % 2 == 0 ? Color.white : new Color(.94f, .95f, .97f),
                        SecondarySortingLayerName
                    );
                }

                // Phase 3 결과 화면 - WindowFront & WindowBack (수동 할당 이미지 덧씌움)
                notFound404Front = CreatePanel(contentRoot, "IE_NotFound404Front", notFound404Tint, PrimarySortingLayerName);
                notFound404Back = CreatePanel(backContentRoot, "IE_NotFound404Back", notFound404Tint, SecondarySortingLayerName);
                gunResultFront = CreatePanel(contentRoot, "IE_GunResultFront", gunResultTint, PrimarySortingLayerName);
                gunResultBack = CreatePanel(backContentRoot, "IE_GunResultBack", gunResultTint, SecondarySortingLayerName);

                // Phase 1 하노이 탑 - WindowFront (동일 크기, 동일 색상 블라인드)
                offlineIconFront = CreatePanel(contentRoot, "IE_OfflineIconFront", offlineIconTint, PrimarySortingLayerName);
                hanoiBackdropFront = CreatePanel(contentRoot, "IE_HanoiBackdropFront", Color.white, PrimarySortingLayerName);
                pegBaseFront = CreatePanel(contentRoot, "IE_HanoiBaseFront", new Color(0.28f, 0.30f, 0.34f), PrimarySortingLayerName);

                for (int p = 0; p < HanoiPegCount; p++)
                {
                    pegPolesFront[p] = CreatePanel(
                        contentRoot,
                        "IE_HanoiPegFront_" + p,
                        new Color(0.36f, 0.39f, 0.44f),
                        PrimarySortingLayerName
                    );
                }

                for (int d = 0; d < HanoiDiskCount; d++)
                {
                    diskFrontBorders[d] = CreatePanel(
                        contentRoot,
                        "IE_HanoiDiskFrontBorder_" + d,
                        new Color(0.22f, 0.24f, 0.27f),
                        PrimarySortingLayerName
                    );
                    diskFrontRenderers[d] = CreatePanel(
                        contentRoot,
                        "IE_HanoiDiskFront_" + d,
                        blindDiskColor,
                        PrimarySortingLayerName
                    );
                }

                // Phase 1 하노이 탑 - WindowBack (WINDOW.exe 투시용: 서로 다른 크기와 색상)
                offlineIconBack = CreatePanel(backContentRoot, "IE_OfflineIconBack", offlineIconTint, SecondarySortingLayerName);
                hanoiBackdropBack = CreatePanel(backContentRoot, "IE_HanoiBackdropBack", new Color(0.95f, 0.97f, 1f), SecondarySortingLayerName);
                pegBaseBack = CreatePanel(backContentRoot, "IE_HanoiBaseBack", new Color(0.28f, 0.30f, 0.34f), SecondarySortingLayerName);

                for (int p = 0; p < HanoiPegCount; p++)
                {
                    pegPolesBack[p] = CreatePanel(
                        backContentRoot,
                        "IE_HanoiPegBack_" + p,
                        new Color(0.36f, 0.39f, 0.44f),
                        SecondarySortingLayerName
                    );
                }

                for (int d = 0; d < HanoiDiskCount; d++)
                {
                    diskBackBorders[d] = CreatePanel(
                        backContentRoot,
                        "IE_HanoiDiskBackBorder_" + d,
                        new Color(0.15f, 0.17f, 0.20f),
                        SecondarySortingLayerName
                    );
                    diskBackRenderers[d] = CreatePanel(
                        backContentRoot,
                        "IE_HanoiDiskBack_" + d,
                        GetRevealedColorConfig(d),
                        SecondarySortingLayerName
                    );
                }

                // Phase 2 텍스트는 WindowFront와 WindowBack 양쪽에서 모두 렌더링
                heading = CreateLabel(
                    contentRoot,
                    "IE_Heading",
                    "검색",
                    3.1f,
                    new Color(.2f, .2f, .2f),
                    TextAlignmentOptions.Center,
                    PrimarySortingLayerName
                );

                searchLabel = CreateLabel(
                    contentRoot,
                    "IE_SearchLabel",
                    selectedText,
                    2.2f,
                    new Color(.12f, .12f, .12f),
                    TextAlignmentOptions.MidlineLeft,
                    PrimarySortingLayerName
                );

                arrowLabel = CreateLabel(
                    contentRoot,
                    "IE_ArrowLabel",
                    "▼",
                    2.1f,
                    Color.black,
                    TextAlignmentOptions.Center,
                    PrimarySortingLayerName
                );

                backHeading = CreateLabel(
                    backContentRoot,
                    "IE_BackHeading",
                    "검색",
                    3.1f,
                    new Color(.2f, .2f, .2f),
                    TextAlignmentOptions.Center,
                    SecondarySortingLayerName
                );

                backSearchLabel = CreateLabel(
                    backContentRoot,
                    "IE_BackSearchLabel",
                    selectedText,
                    2.2f,
                    new Color(.12f, .12f, .12f),
                    TextAlignmentOptions.MidlineLeft,
                    SecondarySortingLayerName
                );

                backArrowLabel = CreateLabel(
                    backContentRoot,
                    "IE_BackArrowLabel",
                    "▼",
                    2.1f,
                    Color.black,
                    TextAlignmentOptions.Center,
                    SecondarySortingLayerName
                );

                const string offlineGuideText = "네트워크 연결이 끊어졌습니다. 원반을 세 번째 막대로 옮겨 연결하세요.";

                offlineLabel = CreateLabel(
                    contentRoot,
                    "IE_Offline",
                    offlineGuideText,
                    2.1f,
                    new Color(.20f, .20f, .20f),
                    TextAlignmentOptions.Center,
                    PrimarySortingLayerName
                );
                offlineLabel.textWrappingMode = TextWrappingModes.Normal;

                backOfflineLabel = CreateLabel(
                    backContentRoot,
                    "IE_BackOffline",
                    offlineGuideText,
                    2.1f,
                    new Color(.15f, .22f, .35f),
                    TextAlignmentOptions.Center,
                    SecondarySortingLayerName
                );
                backOfflineLabel.textWrappingMode = TextWrappingModes.Normal;

                for (int i = 0; i < 3; i++)
                {
                    rowLabels[i] = CreateLabel(
                        contentRoot,
                        "IE_RowText_" + i,
                        "",
                        2.1f,
                        new Color(.14f, .14f, .14f),
                        TextAlignmentOptions.MidlineLeft,
                        PrimarySortingLayerName
                    );

                    backRowLabels[i] = CreateLabel(
                        backContentRoot,
                        "IE_BackRowText_" + i,
                        "",
                        2.1f,
                        new Color(.14f, .14f, .14f),
                        TextAlignmentOptions.MidlineLeft,
                        SecondarySortingLayerName
                    );
                }

                glitchRed = CreateLabel(
                    contentRoot,
                    "IE_GlitchRed",
                    "",
                    2.1f,
                    new Color(.85f, .14f, .20f, .36f),
                    TextAlignmentOptions.MidlineLeft,
                    PrimarySortingLayerName
                );

                glitchBlue = CreateLabel(
                    contentRoot,
                    "IE_GlitchBlue",
                    "",
                    2.1f,
                    new Color(.12f, .53f, .87f, .36f),
                    TextAlignmentOptions.MidlineLeft,
                    PrimarySortingLayerName
                );

                // Step 1: 잠금 화면 UI - WindowFront & WindowBack
                Color dialogBorderColor = new Color(0.35f, 0.35f, 0.35f);
                Color dialogFillColor = new Color(0.86f, 0.87f, 0.90f);
                lockDialogBorderFront = CreatePanel(contentRoot, "IE_LockDialogBorderFront", dialogBorderColor, PrimarySortingLayerName);
                lockDialogFillFront = CreatePanel(contentRoot, "IE_LockDialogFillFront", dialogFillColor, PrimarySortingLayerName);
                lockDialogBorderBack = CreatePanel(backContentRoot, "IE_LockDialogBorderBack", dialogBorderColor, SecondarySortingLayerName);
                lockDialogFillBack = CreatePanel(backContentRoot, "IE_LockDialogFillBack", dialogFillColor, SecondarySortingLayerName);

                Color inputBorderColor = new Color(0.30f, 0.30f, 0.30f);
                Color inputFillColor = Color.white;
                lockInputBorderFront = CreatePanel(contentRoot, "IE_LockInputBorderFront", inputBorderColor, PrimarySortingLayerName);
                lockInputFillFront = CreatePanel(contentRoot, "IE_LockInputFillFront", inputFillColor, PrimarySortingLayerName);
                lockInputBorderBack = CreatePanel(backContentRoot, "IE_LockInputBorderBack", inputBorderColor, SecondarySortingLayerName);
                lockInputFillBack = CreatePanel(backContentRoot, "IE_LockInputFillBack", inputFillColor, SecondarySortingLayerName);

                // WindowBack 투시용 4자리 동그라미 (스프라이트는 CircleSprite(), 색상은 검은색)
                for (int d = 0; d < 4; d++)
                {
                    passcodeBackDots[d] = CreatePanel(backContentRoot, "IE_PasscodeBackDot_" + d, passcodeDotColor, SecondarySortingLayerName);
                    passcodeBackDots[d].sprite = CircleSprite();
                }

                // 온스크린 키패드 (11개 버튼) - WindowFront & WindowBack
                Color keypadBorderColor = new Color(0.40f, 0.40f, 0.40f);
                Color keypadFillColor = new Color(0.80f, 0.81f, 0.84f);
                for (int b = 0; b < KeypadButtonCount; b++)
                {
                    keypadButtonBordersFront[b] = CreatePanel(contentRoot, "IE_KeypadBorderFront_" + b, keypadBorderColor, PrimarySortingLayerName);
                    keypadButtonFillsFront[b] = CreatePanel(contentRoot, "IE_KeypadFillFront_" + b, keypadFillColor, PrimarySortingLayerName);
                    keypadButtonBordersBack[b] = CreatePanel(backContentRoot, "IE_KeypadBorderBack_" + b, keypadBorderColor, SecondarySortingLayerName);
                    keypadButtonFillsBack[b] = CreatePanel(backContentRoot, "IE_KeypadFillBack_" + b, keypadFillColor, SecondarySortingLayerName);

                    keypadButtonLabelsFront[b] = CreateLabel(
                        contentRoot,
                        "IE_KeypadLabelFront_" + b,
                        KeypadLabels[b],
                        2.2f,
                        new Color(0.15f, 0.15f, 0.15f),
                        TextAlignmentOptions.Center,
                        PrimarySortingLayerName
                    );

                    keypadButtonLabelsBack[b] = CreateLabel(
                        backContentRoot,
                        "IE_KeypadLabelBack_" + b,
                        KeypadLabels[b],
                        2.2f,
                        new Color(0.15f, 0.15f, 0.15f),
                        TextAlignmentOptions.Center,
                        SecondarySortingLayerName
                    );
                }

                // 잠금 화면 텍스트 라벨들
                lockTitleLabel = CreateLabel(
                    contentRoot,
                    "IE_LockTitleFront",
                    "네트워크 접속 암호",
                    2.4f,
                    new Color(0.12f, 0.12f, 0.12f),
                    TextAlignmentOptions.Center,
                    PrimarySortingLayerName
                );

                backLockTitleLabel = CreateLabel(
                    backContentRoot,
                    "IE_LockTitleBack",
                    "네트워크 접속 암호",
                    2.4f,
                    new Color(0.12f, 0.12f, 0.12f),
                    TextAlignmentOptions.Center,
                    SecondarySortingLayerName
                );

                const string lockGuideText = "인터넷에 접속하려면 4자리 암호를 입력하십시오.";
                lockGuideLabel = CreateLabel(
                    contentRoot,
                    "IE_LockGuideFront",
                    lockGuideText,
                    1.9f,
                    new Color(0.25f, 0.25f, 0.25f),
                    TextAlignmentOptions.Center,
                    PrimarySortingLayerName
                );

                backLockGuideLabel = CreateLabel(
                    backContentRoot,
                    "IE_LockGuideBack",
                    lockGuideText,
                    1.9f,
                    new Color(0.25f, 0.25f, 0.25f),
                    TextAlignmentOptions.Center,
                    SecondarySortingLayerName
                );

                lockErrorLabel = CreateLabel(
                    contentRoot,
                    "IE_LockErrorFront",
                    "",
                    1.9f,
                    new Color(0.85f, 0.15f, 0.15f),
                    TextAlignmentOptions.Center,
                    PrimarySortingLayerName
                );

                backLockErrorLabel = CreateLabel(
                    backContentRoot,
                    "IE_LockErrorBack",
                    "",
                    1.9f,
                    new Color(0.85f, 0.15f, 0.15f),
                    TextAlignmentOptions.Center,
                    SecondarySortingLayerName
                );

                lockInputMaskLabel = CreateLabel(
                    contentRoot,
                    "IE_LockInputMaskFront",
                    "",
                    2.6f,
                    new Color(0.10f, 0.10f, 0.10f),
                    TextAlignmentOptions.Center,
                    PrimarySortingLayerName
                );

                backLockInputMaskLabel = CreateLabel(
                    backContentRoot,
                    "IE_LockInputMaskBack",
                    "",
                    2.6f,
                    new Color(0.10f, 0.10f, 0.10f),
                    TextAlignmentOptions.Center,
                    SecondarySortingLayerName
                );

                ieTitleBar = GetComponentInChildren<WindowTitleBar>(true);

                ApplyArtwork();
                ApplyOfflineIcon();
                ApplyPhase3Sprites();
                HideDefaultArtwork();

                // Secondary 기본 스프라이트도 브라우저 배경으로 동기화
                Sprite bgSprite = browserBackgroundSprite != null ? browserBackgroundSprite : WhitePixel();
                SetSecondarySprite(bgSprite);
                SetSecondaryColor(browserBackgroundSprite != null ? browserBackgroundTint : Color.white);

                var oldFooter = contentRoot.Find("IE_Footer");
                if (oldFooter != null)
                    oldFooter.gameObject.SetActive(false);

                initialized = ReferencesReady();

                if (!initialized)
                {
                    Debug.LogError(
                        "[InternetWindow] UI 초기화 실패: 생성된 컴포넌트가 누락되었습니다.",
                        this
                    );
                    return;
                }

                ApplySize(size);

                lastOnline = IsOnline;
                RefreshUI();
                ApplyLayerOrders(GetBaseSortingOrder());
            }
            finally
            {
                initializing = false;
            }
        }

        public void ResetHanoiState()
        {
            for (int p = 0; p < HanoiPegCount; p++)
                pegs[p].Clear();

            // 첫 번째 막대에 바닥부터 0(숫자 1, 가장 넓음), 1(숫자 2, 중간), 2(숫자 3, 가장 좁음) 적재
            for (int d = 0; d < HanoiDiskCount; d++)
            {
                pegs[0].Add(d);
                diskShakeTimers[d] = 0f;
                diskShakeMagnitudes[d] = 0f;
            }

            selectedPeg = -1;

            if (initialized && ReferencesReady())
            {
                ApplySize(size);
                RefreshUI();
            }
        }

        private Color GetRevealedColorConfig(int diskIndex)
        {
            if (revealedDiskColors != null && diskIndex >= 0 && diskIndex < revealedDiskColors.Length)
                return revealedDiskColors[diskIndex];

            return diskIndex switch
            {
                0 => new Color(0.90f, 0.26f, 0.26f, 1f),
                1 => new Color(0.95f, 0.72f, 0.18f, 1f),
                _ => new Color(0.24f, 0.62f, 0.96f, 1f)
            };
        }

        private void ApplyArtwork()
        {
            Sprite targetSprite = browserBackgroundSprite != null
                ? browserBackgroundSprite
                : WhitePixel();

            Color targetColor = browserBackgroundSprite != null
                ? browserBackgroundTint
                : Color.white;

            if (artwork != null)
            {
                artwork.sprite = targetSprite;
                artwork.color = targetColor;
            }

            if (backArtwork != null)
            {
                backArtwork.sprite = targetSprite;
                backArtwork.color = targetColor;
            }

            SetSecondarySprite(targetSprite);
            SetSecondaryColor(targetColor);

            if (browserBackgroundSprite == null)
            {
                Debug.LogWarning(
                    "[InternetWindow] Browser Background Sprite가 비어 있습니다. 인터넷 프리팹 Inspector에 IE 이미지를 연결하세요.",
                    this
                );
            }
        }

        private void ApplyOfflineIcon()
        {
            bool hasSprite = offlineIconSprite != null;
            Sprite spriteToUse = hasSprite ? offlineIconSprite : WhitePixel();

            if (offlineIconFront != null)
            {
                offlineIconFront.sprite = spriteToUse;
                offlineIconFront.color = offlineIconTint;
                offlineIconFront.enabled = hasSprite;
            }

            if (offlineIconBack != null)
            {
                offlineIconBack.sprite = spriteToUse;
                offlineIconBack.color = offlineIconTint;
                offlineIconBack.enabled = hasSprite;
            }
        }

        private void ApplyPhase3Sprites()
        {
            bool has404 = notFound404Sprite != null;
            Sprite sprite404 = has404 ? notFound404Sprite : WhitePixel();

            if (notFound404Front != null)
            {
                notFound404Front.sprite = sprite404;
                notFound404Front.color = notFound404Tint;
                notFound404Front.enabled = has404;
            }

            if (notFound404Back != null)
            {
                notFound404Back.sprite = sprite404;
                notFound404Back.color = notFound404Tint;
                notFound404Back.enabled = has404;
            }

            bool hasGun = gunResultSprite != null;
            Sprite spriteGun = hasGun ? gunResultSprite : WhitePixel();

            if (gunResultFront != null)
            {
                gunResultFront.sprite = spriteGun;
                gunResultFront.color = gunResultTint;
                gunResultFront.enabled = hasGun;
            }

            if (gunResultBack != null)
            {
                gunResultBack.sprite = spriteGun;
                gunResultBack.color = gunResultTint;
                gunResultBack.enabled = hasGun;
            }
        }

        private void HideDefaultArtwork()
        {
            if (contentRoot != null)
            {
                foreach (var sprite in contentRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sprite == artwork ||
                        sprite.gameObject.name.StartsWith("IE_", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    sprite.enabled = false;
                }
            }

            if (ieTitleBar == null)
                ieTitleBar = GetComponentInChildren<WindowTitleBar>(true);

            if (ieTitleBar != null)
            {
                bool show = !(artworkIncludesTitleBar && browserBackgroundSprite != null);

                var barSprite = ieTitleBar.GetComponent<SpriteRenderer>();
                if (barSprite != null)
                    barSprite.enabled = show;

                foreach (var label in ieTitleBar.GetComponentsInChildren<TextMeshPro>(true))
                {
                    label.enabled = show;
                }
            }
        }

        protected override void ApplySize(Vector2 newSize)
        {
            base.ApplySize(newSize);

            if (!initialized || !ReferencesReady()) return;

            float bar = BarHeight;
            float fullHeight = newSize.y + bar;
            float top = newSize.y * .5f + bar;
            float width = newSize.x;

            PlaceSprite(artwork, 0f, bar * .5f, width, fullHeight);
            PlaceSprite(backArtwork, 0f, bar * .5f, width, fullHeight);

            // Phase 2: 검색 기록 UI 배치
            float headingY = top - fullHeight * .435f;
            float searchY = top - fullHeight * .555f;

            float searchWidth = width * .59f;
            float fieldHeight = Mathf.Max(.30f, fullHeight * .068f);
            float rowHeight = Mathf.Max(.29f, fullHeight * .065f);

            PlaceText(heading, 0f, headingY, width * .65f, fullHeight * .085f);
            PlaceText(backHeading, 0f, headingY, width * .65f, fullHeight * .085f);

            searchRect = CenterRect(0f, searchY, searchWidth, fieldHeight);

            PlaceSprite(frontSearchBorder, 0f, searchY, searchWidth, fieldHeight);
            PlaceSprite(frontSearchFill, -.10f, searchY, searchWidth - .24f, fieldHeight - .05f);
            PlaceSprite(frontArrowFill, searchWidth * .5f - .14f, searchY, .25f, fieldHeight - .05f);

            PlaceSprite(searchBorder, 0f, searchY, searchWidth, fieldHeight);
            PlaceSprite(searchFill, -.10f, searchY, searchWidth - .24f, fieldHeight - .05f);
            PlaceSprite(arrowFill, searchWidth * .5f - .14f, searchY, .25f, fieldHeight - .05f);

            PlaceText(searchLabel, -.12f, searchY, searchWidth - .49f, fieldHeight * .87f);
            PlaceText(arrowLabel, searchWidth * .5f - .14f, searchY, .25f, fieldHeight * .87f);
            PlaceText(backSearchLabel, -.12f, searchY, searchWidth - .49f, fieldHeight * .87f);
            PlaceText(backArrowLabel, searchWidth * .5f - .14f, searchY, .25f, fieldHeight * .87f);

            float firstRowY = searchY - (fieldHeight + rowHeight) * .5f - .012f;
            float listY = firstRowY - rowHeight;
            float listHeight = rowHeight * 3f + .04f;

            PlaceSprite(frontListBorder, 0f, listY, searchWidth, listHeight);
            PlaceSprite(frontListFill, 0f, listY, searchWidth - .04f, listHeight - .04f);

            PlaceSprite(listBorder, 0f, listY, searchWidth, listHeight);
            PlaceSprite(listFill, 0f, listY, searchWidth - .04f, listHeight - .04f);

            for (int i = 0; i < 3; i++)
            {
                float y = firstRowY - rowHeight * i;
                rowRects[i] = CenterRect(0f, y, searchWidth, rowHeight);

                PlaceSprite(frontRowBackgrounds[i], 0f, y, searchWidth - .06f, rowHeight - .03f);
                PlaceSprite(rowBackgrounds[i], 0f, y, searchWidth - .06f, rowHeight - .03f);
                PlaceText(rowLabels[i], 0f, y, searchWidth - .30f, rowHeight * .88f);
                PlaceText(backRowLabels[i], 0f, y, searchWidth - .30f, rowHeight * .88f);
            }

            PlaceSprite(frontThirdRowMask, 0f, rowRects[2].center.y, searchWidth - .06f, rowHeight - .03f);

            float textBoxWidth = searchWidth - .30f;
            float textLeftX = -textBoxWidth * .5f;
            float targetTextWidth = Mathf.Min(textBoxWidth * .65f, 2.5f);
            float targetTextHeight = rowHeight * .82f;
            thirdRowRevealLocalRect = new Rect(
                textLeftX,
                rowRects[2].center.y - targetTextHeight * .5f,
                targetTextWidth,
                targetTextHeight
            );

            PlaceText(
                glitchRed,
                .025f,
                rowRects[2].center.y + .012f,
                searchWidth - .30f,
                rowHeight * .88f
            );

            PlaceText(
                glitchBlue,
                -.025f,
                rowRects[2].center.y - .012f,
                searchWidth - .30f,
                rowHeight * .88f
            );

            // Phase 3: 결과 이미지 (404 / 총) 배치
            float contentCenterY = bar * 0.5f;
            PlaceSprite(notFound404Front, notFound404Offset.x, contentCenterY + notFound404Offset.y, notFound404Size.x, notFound404Size.y);
            PlaceSprite(notFound404Back, notFound404Offset.x, contentCenterY + notFound404Offset.y, notFound404Size.x, notFound404Size.y);
            PlaceSprite(gunResultFront, gunResultOffset.x, contentCenterY + gunResultOffset.y, gunResultSize.x, gunResultSize.y);
            PlaceSprite(gunResultBack, gunResultOffset.x, contentCenterY + gunResultOffset.y, gunResultSize.x, gunResultSize.y);

            // Phase 1: 네트워크 연결 하노이 탑 퍼즐 배치
            LayoutHanoiElements(width, fullHeight, top);

            // Step 1: 암호 잠금 화면 UI 배치
            LayoutLockElements(width, fullHeight, top);
        }

        private void LayoutLockElements(float width, float fullHeight, float top)
        {
            float dialogWidth = Mathf.Min(width * 0.62f, 4.2f);
            float dialogHeight = Mathf.Min(fullHeight * 0.72f, 3.2f);
            float dialogCenterY = top - fullHeight * 0.54f;

            // 다이얼로그 테두리 및 채우기
            PlaceSprite(lockDialogBorderFront, 0f, dialogCenterY, dialogWidth, dialogHeight);
            PlaceSprite(lockDialogFillFront, 0f, dialogCenterY, dialogWidth - 0.04f, dialogHeight - 0.04f);
            PlaceSprite(lockDialogBorderBack, 0f, dialogCenterY, dialogWidth, dialogHeight);
            PlaceSprite(lockDialogFillBack, 0f, dialogCenterY, dialogWidth - 0.04f, dialogHeight - 0.04f);

            // 타이틀 및 가이드 라벨
            float titleY = dialogCenterY + dialogHeight * 0.38f;
            PlaceText(lockTitleLabel, 0f, titleY, dialogWidth * 0.90f, dialogHeight * 0.12f);
            PlaceText(backLockTitleLabel, 0f, titleY, dialogWidth * 0.90f, dialogHeight * 0.12f);

            float guideY = dialogCenterY + dialogHeight * 0.27f;
            PlaceText(lockGuideLabel, 0f, guideY, dialogWidth * 0.92f, dialogHeight * 0.10f);
            PlaceText(backLockGuideLabel, 0f, guideY, dialogWidth * 0.92f, dialogHeight * 0.10f);

            // 오답 에러 라벨 (비밀번호 입력칸 위쪽)
            float errorY = dialogCenterY + dialogHeight * 0.16f;
            PlaceText(lockErrorLabel, 0f, errorY, dialogWidth * 0.90f, dialogHeight * 0.09f);
            PlaceText(backLockErrorLabel, 0f, errorY, dialogWidth * 0.90f, dialogHeight * 0.09f);

            // 비밀번호 입력창 (테두리, 채우기, 마스크 텍스트)
            float inputY = dialogCenterY + dialogHeight * 0.05f;
            float inputWidth = dialogWidth * 0.58f;
            float inputHeight = 0.44f;

            PlaceSprite(lockInputBorderFront, 0f, inputY, inputWidth, inputHeight);
            PlaceSprite(lockInputFillFront, 0f, inputY, inputWidth - 0.04f, inputHeight - 0.04f);
            PlaceSprite(lockInputBorderBack, 0f, inputY, inputWidth, inputHeight);
            PlaceSprite(lockInputFillBack, 0f, inputY, inputWidth - 0.04f, inputHeight - 0.04f);

            PlaceText(lockInputMaskLabel, 0f, inputY, inputWidth - 0.08f, inputHeight * 0.88f);
            PlaceText(backLockInputMaskLabel, 0f, inputY, inputWidth - 0.08f, inputHeight * 0.88f);

            // WindowBack 투시용 4자리 동그라미 (각 자릿수 위치에 배치)
            float slotSpacing = inputWidth / 4f;
            float firstSlotX = -inputWidth * 0.5f + slotSpacing * 0.5f;
            for (int d = 0; d < 4; d++)
            {
                float dotX = firstSlotX + d * slotSpacing;
                float dotDiameter = PasscodeDotDiameters[d];
                PlaceSprite(passcodeBackDots[d], dotX, inputY, dotDiameter, dotDiameter);
            }

            // 온스크린 키패드: 3열 4행
            // row 0: 1, 2, 3
            // row 1: 4, 5, 6
            // row 2: 7, 8, 9
            // row 3: C, 0
            float keypadTopY = dialogCenterY - dialogHeight * 0.09f;
            float btnWidth = 0.52f;
            float btnHeight = 0.28f;
            float spacingX = 0.58f;
            float spacingY = 0.33f;

            for (int b = 0; b < KeypadButtonCount; b++)
            {
                int row = b / 3;
                int col = b % 3;

                float btnX = (col - 1) * spacingX;
                float btnY = keypadTopY - row * spacingY;

                keypadButtonRects[b] = CenterRect(btnX, btnY, btnWidth, btnHeight);

                PlaceSprite(keypadButtonBordersFront[b], btnX, btnY, btnWidth, btnHeight);
                PlaceSprite(keypadButtonFillsFront[b], btnX, btnY, btnWidth - 0.03f, btnHeight - 0.03f);
                PlaceSprite(keypadButtonBordersBack[b], btnX, btnY, btnWidth, btnHeight);
                PlaceSprite(keypadButtonFillsBack[b], btnX, btnY, btnWidth - 0.03f, btnHeight - 0.03f);

                PlaceText(keypadButtonLabelsFront[b], btnX, btnY, btnWidth, btnHeight * 0.88f);
                PlaceText(keypadButtonLabelsBack[b], btnX, btnY, btnWidth, btnHeight * 0.88f);
            }
        }

        private void LayoutHanoiElements(float width, float fullHeight, float top)
        {
            float iconY = top - fullHeight * 0.345f + offlineIconOffset.y;
            float iconX = offlineIconOffset.x;
            PlaceSprite(offlineIconFront, iconX, iconY, offlineIconSize.x, offlineIconSize.y);
            PlaceSprite(offlineIconBack, iconX, iconY, offlineIconSize.x, offlineIconSize.y);

            float guideY = top - fullHeight * 0.440f;
            PlaceText(offlineLabel, 0f, guideY, width * 0.74f, fullHeight * 0.09f);
            PlaceText(backOfflineLabel, 0f, guideY, width * 0.74f, fullHeight * 0.09f);

            float boardCenterY = top - fullHeight * 0.655f;
            float boardWidth = width * 0.68f;
            float boardHeight = fullHeight * 0.36f;

            hanoiBoardLocalRect = CenterRect(0f, boardCenterY, boardWidth, boardHeight);

            PlaceSprite(hanoiBackdropFront, 0f, boardCenterY, boardWidth, boardHeight);
            PlaceSprite(hanoiBackdropBack, 0f, boardCenterY, boardWidth, boardHeight);

            float baseY = top - fullHeight * 0.805f;
            float baseWidth = boardWidth * 0.90f;
            float baseThickness = Mathf.Max(0.08f, fullHeight * 0.016f);

            PlaceSprite(pegBaseFront, 0f, baseY, baseWidth, baseThickness);
            PlaceSprite(pegBaseBack, 0f, baseY, baseWidth, baseThickness);

            float pegSpacing = boardWidth * 0.30f;
            float poleHeight = fullHeight * 0.22f;
            float poleCenterY = baseY + poleHeight * 0.5f;
            float poleWidth = Mathf.Max(0.09f, width * 0.014f);

            for (int p = 0; p < HanoiPegCount; p++)
            {
                float pegX = (p - 1) * pegSpacing;
                PlaceSprite(pegPolesFront[p], pegX, poleCenterY, poleWidth, poleHeight);
                PlaceSprite(pegPolesBack[p], pegX, poleCenterY, poleWidth, poleHeight);

                Color poleColor = selectedPeg == p
                    ? new Color(0.20f, 0.48f, 0.82f, 1f)
                    : new Color(0.36f, 0.39f, 0.44f, 1f);

                pegPolesFront[p].color = poleColor;
                pegPolesBack[p].color = poleColor;

                pegRects[p] = CenterRect(
                    pegX,
                    poleCenterY + fullHeight * 0.02f,
                    pegSpacing * 0.92f,
                    poleHeight + fullHeight * 0.12f
                );
            }

            currentDiskHeight = Mathf.Max(0.20f, poleHeight * 0.21f);
            float diskStepY = currentDiskHeight * 1.15f;
            float firstSlotY = baseY + baseThickness * 0.5f + currentDiskHeight * 0.5f + 0.02f;
            float liftedY = baseY + poleHeight + currentDiskHeight * 0.60f;

            // WindowFront의 원반은 모두 동일한 크기(중간 폭)와 동일한 색상
            currentBlindDiskWidth = pegSpacing * 0.60f;

            // WindowBack의 원반은 숫자와 순서에 따라 서로 다른 크기(대/중/소)
            diskBackWidths[0] = pegSpacing * 0.84f; // 원반 1 (가장 넓음)
            diskBackWidths[1] = pegSpacing * 0.60f; // 원반 2 (중간)
            diskBackWidths[2] = pegSpacing * 0.36f; // 원반 3 (가장 좁음)

            for (int p = 0; p < HanoiPegCount; p++)
            {
                float pegX = (p - 1) * pegSpacing;
                var list = pegs[p];

                for (int slot = 0; slot < list.Count; slot++)
                {
                    int diskIndex = list[slot];
                    bool isSelectedTop = p == selectedPeg && slot == list.Count - 1;
                    float diskY = isSelectedTop
                        ? Mathf.Max(liftedY, firstSlotY + slot * diskStepY + currentDiskHeight * 0.85f)
                        : firstSlotY + slot * diskStepY;

                    diskBasePositions[diskIndex] = new Vector2(pegX, diskY);
                }
            }

            ApplyDiskVisualsAndPositions();
        }

        private void ApplyDiskVisualsAndPositions()
        {
            const float borderPadding = 0.045f;

            for (int d = 0; d < HanoiDiskCount; d++)
            {
                Vector2 pos = diskBasePositions[d];

                if (diskShakeTimers[d] > 0f)
                {
                    float ratio = Mathf.Clamp01(diskShakeTimers[d] / Mathf.Max(0.001f, shakeDuration));
                    float phase = Time.unscaledTime * 120f + (d + 1) * 1.3f;
                    Vector2 offset = new Vector2(
                        Mathf.Sin(phase),
                        Mathf.Cos(phase * 1.37f) * 0.55f
                    ) * (diskShakeMagnitudes[d] * ratio);

                    pos += offset;
                }

                if (diskFrontRenderers[d] != null)
                    diskFrontRenderers[d].color = blindDiskColor;

                if (diskBackRenderers[d] != null)
                    diskBackRenderers[d].color = GetRevealedColorConfig(d);

                PlaceSprite(
                    diskFrontBorders[d],
                    pos.x,
                    pos.y,
                    currentBlindDiskWidth + borderPadding,
                    currentDiskHeight + borderPadding
                );
                PlaceSprite(
                    diskFrontRenderers[d],
                    pos.x,
                    pos.y,
                    currentBlindDiskWidth,
                    currentDiskHeight
                );

                float backWidth = diskBackWidths[d] > 0f ? diskBackWidths[d] : currentBlindDiskWidth;
                PlaceSprite(
                    diskBackBorders[d],
                    pos.x,
                    pos.y,
                    backWidth + borderPadding,
                    currentDiskHeight + borderPadding
                );
                PlaceSprite(
                    diskBackRenderers[d],
                    pos.x,
                    pos.y,
                    backWidth,
                    currentDiskHeight
                );
            }
        }

        public override void ApplySorting(int order)
        {
            base.ApplySorting(order);
            lastSortingOrder = order;
            if (initialized && ReferencesReady())
                ApplyLayerOrders(order);
        }

        // 부모 Window.ApplySorting()이 렌더링 순서를 덮어쓰므로
        // 인터넷 창의 내부 UI 순서를 보정
        private void LateUpdate()
        {
            if (!initialized || !ReferencesReady()) return;
            ApplyLayerOrders(GetBaseSortingOrder());
        }

        private int GetBaseSortingOrder()
        {
            var secondary = transform.Find("Secondary");
            var secondaryRenderer = secondary != null
                ? secondary.GetComponent<SpriteRenderer>()
                : null;

            return secondaryRenderer != null
                ? secondaryRenderer.sortingOrder
                : lastSortingOrder;
        }

        private void ApplyLayerOrders(int baseOrder)
        {
            // WindowFront: 브라우저 이미지가 가장 뒤
            SetLayer(artwork, PrimarySortingLayerName, baseOrder + 0);

            // WindowFront: Phase 1 하노이 탑 (평상시 보임, WINDOW.exe에 의해 가려짐)
            SetLayer(hanoiBackdropFront, PrimarySortingLayerName, baseOrder + 1);
            SetLayer(pegBaseFront, PrimarySortingLayerName, baseOrder + 2);
            for (int p = 0; p < HanoiPegCount; p++)
                SetLayer(pegPolesFront[p], PrimarySortingLayerName, baseOrder + 3);
            for (int d = 0; d < HanoiDiskCount; d++)
            {
                SetLayer(diskFrontBorders[d], PrimarySortingLayerName, baseOrder + 4);
                SetLayer(diskFrontRenderers[d], PrimarySortingLayerName, baseOrder + 5);
            }
            SetLayer(offlineIconFront, PrimarySortingLayerName, baseOrder + 6);
            SetLayer(offlineLabel, PrimarySortingLayerName, baseOrder + 6);

            // WindowBack: 브라우저 배경 및 Phase 1 하노이 탑 (WINDOW.exe를 통해서만 보임)
            SetLayer(backArtwork, SecondarySortingLayerName, baseOrder + 0);
            SetLayer(hanoiBackdropBack, SecondarySortingLayerName, baseOrder + 1);
            SetLayer(pegBaseBack, SecondarySortingLayerName, baseOrder + 2);
            for (int p = 0; p < HanoiPegCount; p++)
                SetLayer(pegPolesBack[p], SecondarySortingLayerName, baseOrder + 3);
            for (int d = 0; d < HanoiDiskCount; d++)
            {
                SetLayer(diskBackBorders[d], SecondarySortingLayerName, baseOrder + 4);
                SetLayer(diskBackRenderers[d], SecondarySortingLayerName, baseOrder + 5);
            }
            SetLayer(offlineIconBack, SecondarySortingLayerName, baseOrder + 6);
            SetLayer(backOfflineLabel, SecondarySortingLayerName, baseOrder + 6);

            // Phase 2: WindowFront 검색창 및 드롭다운 패널
            SetLayer(frontSearchBorder, PrimarySortingLayerName, baseOrder + 2);
            SetLayer(frontSearchFill, PrimarySortingLayerName, baseOrder + 3);
            SetLayer(frontArrowFill, PrimarySortingLayerName, baseOrder + 4);

            SetLayer(frontListBorder, PrimarySortingLayerName, baseOrder + 6);
            SetLayer(frontListFill, PrimarySortingLayerName, baseOrder + 7);

            for (int i = 0; i < 3; i++)
                SetLayer(frontRowBackgrounds[i], PrimarySortingLayerName, baseOrder + 8);

            // Phase 2: WindowBack 검색창 및 드롭다운 패널
            SetLayer(searchBorder, SecondarySortingLayerName, baseOrder + 2);
            SetLayer(searchFill, SecondarySortingLayerName, baseOrder + 3);
            SetLayer(arrowFill, SecondarySortingLayerName, baseOrder + 4);

            SetLayer(listBorder, SecondarySortingLayerName, baseOrder + 6);
            SetLayer(listFill, SecondarySortingLayerName, baseOrder + 7);

            for (int i = 0; i < 3; i++)
                SetLayer(rowBackgrounds[i], SecondarySortingLayerName, baseOrder + 8);

            // Phase 2: 텍스트는 WindowFront와 WindowBack 양쪽에서 모두 렌더링
            SetLayer(heading, PrimarySortingLayerName, baseOrder + 5);
            SetLayer(searchLabel, PrimarySortingLayerName, baseOrder + 5);
            SetLayer(arrowLabel, PrimarySortingLayerName, baseOrder + 5);

            SetLayer(backHeading, SecondarySortingLayerName, baseOrder + 5);
            SetLayer(backSearchLabel, SecondarySortingLayerName, baseOrder + 5);
            SetLayer(backArrowLabel, SecondarySortingLayerName, baseOrder + 5);

            // 해독 전 평상시 시야에서 WindowBack의 "총" 글자가 비치지 않도록 가리는 WindowFront 가림막
            SetLayer(frontThirdRowMask, PrimarySortingLayerName, baseOrder + 8);

            for (int i = 0; i < 3; i++)
            {
                SetLayer(rowLabels[i], PrimarySortingLayerName, baseOrder + 9);
                SetLayer(backRowLabels[i], SecondarySortingLayerName, baseOrder + 9);
            }

            SetLayer(glitchRed, PrimarySortingLayerName, baseOrder + 10);
            SetLayer(glitchBlue, PrimarySortingLayerName, baseOrder + 10);

            // Phase 3: 결과 이미지 (404 / 총) 렌더러 레이어
            SetLayer(notFound404Front, PrimarySortingLayerName, baseOrder + 2);
            SetLayer(notFound404Back, SecondarySortingLayerName, baseOrder + 2);
            SetLayer(gunResultFront, PrimarySortingLayerName, baseOrder + 2);
            SetLayer(gunResultBack, SecondarySortingLayerName, baseOrder + 2);

            // Step 1: 암호 잠금 화면 (WindowFront & WindowBack)
            SetLayer(lockDialogBorderFront, PrimarySortingLayerName, baseOrder + 1);
            SetLayer(lockDialogFillFront, PrimarySortingLayerName, baseOrder + 2);
            SetLayer(lockDialogBorderBack, SecondarySortingLayerName, baseOrder + 1);
            SetLayer(lockDialogFillBack, SecondarySortingLayerName, baseOrder + 2);

            SetLayer(lockTitleLabel, PrimarySortingLayerName, baseOrder + 4);
            SetLayer(backLockTitleLabel, SecondarySortingLayerName, baseOrder + 4);
            SetLayer(lockGuideLabel, PrimarySortingLayerName, baseOrder + 4);
            SetLayer(backLockGuideLabel, SecondarySortingLayerName, baseOrder + 4);
            SetLayer(lockErrorLabel, PrimarySortingLayerName, baseOrder + 4);
            SetLayer(backLockErrorLabel, SecondarySortingLayerName, baseOrder + 4);

            SetLayer(lockInputBorderFront, PrimarySortingLayerName, baseOrder + 3);
            SetLayer(lockInputFillFront, PrimarySortingLayerName, baseOrder + 4);
            SetLayer(lockInputBorderBack, SecondarySortingLayerName, baseOrder + 3);
            SetLayer(lockInputFillBack, SecondarySortingLayerName, baseOrder + 4);

            SetLayer(lockInputMaskLabel, PrimarySortingLayerName, baseOrder + 6);
            SetLayer(backLockInputMaskLabel, SecondarySortingLayerName, baseOrder + 6);

            for (int d = 0; d < 4; d++)
                SetLayer(passcodeBackDots[d], SecondarySortingLayerName, baseOrder + 6);

            for (int b = 0; b < KeypadButtonCount; b++)
            {
                SetLayer(keypadButtonBordersFront[b], PrimarySortingLayerName, baseOrder + 3);
                SetLayer(keypadButtonFillsFront[b], PrimarySortingLayerName, baseOrder + 4);
                SetLayer(keypadButtonLabelsFront[b], PrimarySortingLayerName, baseOrder + 5);

                SetLayer(keypadButtonBordersBack[b], SecondarySortingLayerName, baseOrder + 3);
                SetLayer(keypadButtonFillsBack[b], SecondarySortingLayerName, baseOrder + 4);
                SetLayer(keypadButtonLabelsBack[b], SecondarySortingLayerName, baseOrder + 5);
            }
        }

        private static void SetLayer(Renderer renderer, string sortingLayer, int order)
        {
            if (renderer == null) return;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
        }

        private static void SetLayer(TextMeshPro text, string sortingLayer, int order)
        {
            if (text == null) return;
            Renderer renderer = text.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
        }

        private bool ReferencesReady()
        {
            if (contentRoot == null ||
                backContentRoot == null ||
                artwork == null ||
                backArtwork == null ||
                notFound404Front == null ||
                notFound404Back == null ||
                gunResultFront == null ||
                gunResultBack == null ||
                frontThirdRowMask == null ||
                offlineIconFront == null ||
                offlineIconBack == null ||
                hanoiBackdropFront == null ||
                hanoiBackdropBack == null ||
                pegBaseFront == null ||
                pegBaseBack == null ||
                frontSearchBorder == null ||
                frontSearchFill == null ||
                frontArrowFill == null ||
                frontListBorder == null ||
                frontListFill == null ||
                searchBorder == null ||
                searchFill == null ||
                arrowFill == null ||
                listBorder == null ||
                listFill == null ||
                heading == null ||
                searchLabel == null ||
                arrowLabel == null ||
                backHeading == null ||
                backSearchLabel == null ||
                backArrowLabel == null ||
                offlineLabel == null ||
                backOfflineLabel == null ||
                glitchRed == null ||
                glitchBlue == null ||
                lockDialogBorderFront == null ||
                lockDialogFillFront == null ||
                lockDialogBorderBack == null ||
                lockDialogFillBack == null ||
                lockInputBorderFront == null ||
                lockInputFillFront == null ||
                lockInputBorderBack == null ||
                lockInputFillBack == null ||
                lockTitleLabel == null ||
                backLockTitleLabel == null ||
                lockGuideLabel == null ||
                backLockGuideLabel == null ||
                lockErrorLabel == null ||
                backLockErrorLabel == null ||
                lockInputMaskLabel == null ||
                backLockInputMaskLabel == null)
            {
                return false;
            }

            for (int d = 0; d < 4; d++)
            {
                if (passcodeBackDots[d] == null)
                    return false;
            }

            for (int b = 0; b < KeypadButtonCount; b++)
            {
                if (keypadButtonBordersFront[b] == null ||
                    keypadButtonFillsFront[b] == null ||
                    keypadButtonBordersBack[b] == null ||
                    keypadButtonFillsBack[b] == null ||
                    keypadButtonLabelsFront[b] == null ||
                    keypadButtonLabelsBack[b] == null)
                {
                    return false;
                }
            }

            for (int p = 0; p < HanoiPegCount; p++)
            {
                if (pegPolesFront[p] == null || pegPolesBack[p] == null)
                    return false;
            }

            for (int d = 0; d < HanoiDiskCount; d++)
            {
                if (diskFrontBorders[d] == null ||
                    diskFrontRenderers[d] == null ||
                    diskBackBorders[d] == null ||
                    diskBackRenderers[d] == null)
                {
                    return false;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                if (frontRowBackgrounds[i] == null ||
                    rowBackgrounds[i] == null ||
                    rowLabels[i] == null ||
                    backRowLabels[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void Update()
        {
            if (!initialized || !ReferencesReady()) return;

            bool online = IsOnline;

            if (online != lastOnline)
            {
                lastOnline = online;

                if (!online)
                {
                    dropdownOpen = false;
                    phase3Result = InternetPhase.SearchHistory;
                    if (pegs[2].Count == HanoiDiskCount)
                        ResetHanoiState();
                }

                RefreshUI();
            }

            ManualTick(Time.unscaledDeltaTime);

            if (errorMessageTimer > 0f)
            {
                errorMessageTimer -= Time.unscaledDeltaTime;
                if (errorMessageTimer <= 0f)
                {
                    errorMessageTimer = 0f;
                    RefreshUI();
                }
            }

            if (CurrentStep == InternetStep.LockScreen)
            {
                HandleKeyboardInputForPasscode();
            }

            if (online && dropdownOpen && !decoded)
            {
                TryRevealThirdHistory();

                if (!decoded)
                {
                    Vector3 p = glitchRed.transform.localPosition;
                    glitchRed.transform.localPosition = new Vector3(
                        .025f + Mathf.Sin(Time.unscaledTime * 24f) * .012f,
                        p.y,
                        p.z
                    );
                }
            }

            if (Mouse.current == null ||
                !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            PointerLeftPressed(Mouse.current.position.ReadValue());
        }

        public void PointerLeftPressed(Vector2 position)
        {
            if (!initialized || !ReferencesReady()) return;
            bool online = IsOnline;
            if (!TryGetPointerWorldPosition(position, out Vector3 mouseWorld)) return;

            Vector2 local = transform.InverseTransformPoint(mouseWorld);

            if (online && phase3Result == InternetPhase.ResultGun)
            {
                if (!TitleContains(mouseWorld) &&
                    ButtonAt(mouseWorld) < 0 &&
                    !IsCoveredByBlockingWindowForGun(mouseWorld) &&
                    GetGunWorldRect().Contains(mouseWorld))
                {
                    TryShootGun();
                }
                return;
            }

            if (IsCoveredByFrontWindow(mouseWorld))
                return;

            // Step 1: 잠금 화면에서는 온스크린 키패드 클릭 처리
            if (CurrentStep == InternetStep.LockScreen)
            {
                for (int b = 0; b < KeypadButtonCount; b++)
                {
                    if (keypadButtonRects[b].Contains(local))
                    {
                        string btnLabel = KeypadLabels[b];
                        if (btnLabel == "C")
                        {
                            ClearPasscode();
                        }
                        else if (btnLabel.Length == 1 && char.IsDigit(btnLabel[0]))
                        {
                            InputPasscodeDigit(btnLabel[0]);
                        }
                        return;
                    }
                }
                return;
            }

            if (!online)
            {
                // Phase 1: WINDOW.exe 창이 겹쳐진 상태에서는 하노이 탑 조작 불가
                if (IsHanoiBlockedByWindowExe())
                    return;

                for (int p = 0; p < HanoiPegCount; p++)
                {
                    if (pegRects[p].Contains(local))
                    {
                        TryClickPeg(p);
                        return;
                    }
                }

                return;
            }

            // Phase 3: 404 결과 화면에서는 검색창/드롭다운 조작 없음
            if (phase3Result == InternetPhase.Result404)
                return;

            // Phase 2: 검색 기록 화면
            if (dropdownOpen)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (rowRects[i].Contains(local))
                    {
                        ChooseHistory(i);
                        return;
                    }
                }
            }

            if (searchRect.Contains(local))
            {
                dropdownOpen = !dropdownOpen;
                RefreshUI();
            }
            else if (dropdownOpen)
            {
                dropdownOpen = false;
                RefreshUI();
            }
        }

        private bool TryGetMouseWorldPosition(out Vector3 mouseWorld)
        {
            mouseWorld = Vector3.zero;
            if (Mouse.current == null) return false;

            return TryGetPointerWorldPosition(Mouse.current.position.ReadValue(), out mouseWorld);
        }

        private bool TryGetPointerWorldPosition(Vector2 screenPos, out Vector3 mouseWorld)
        {
            mouseWorld = Vector3.zero;
            if (DesktopWindowSpace.Instance != null &&
                DesktopWindowSpace.Instance.Valid &&
                DesktopWindowSpace.Instance.TryWorld(screenPos, out Vector2 world2D))
            {
                mouseWorld = new Vector3(world2D.x, world2D.y, transform.position.z);
                return true;
            }

            Camera cam = Camera.main;
            if (cam == null) return false;

            mouseWorld = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z)
            );
            return true;
        }

        public bool TryClickPeg(int pegIndex)
        {
            if (pegIndex < 0 || pegIndex >= HanoiPegCount)
                return false;

            if (IsOnline || IsHanoiBlockedByWindowExe())
                return false;

            // 1. 아직 선택된 막대가 없을 때: 클릭한 막대의 맨 위 원반을 위로 살짝 띄움
            if (selectedPeg < 0)
            {
                if (pegs[pegIndex].Count == 0)
                    return false;

                selectedPeg = pegIndex;
                PlayHanoiSound(HanoiSoundType.Select);
                ApplySize(size);
                RefreshUI();
                return true;
            }

            // 2. 같은 막대를 다시 클릭했을 때: 선택 취소
            if (selectedPeg == pegIndex)
            {
                selectedPeg = -1;
                PlayHanoiSound(HanoiSoundType.Select);
                ApplySize(size);
                RefreshUI();
                return true;
            }

            // 3. 다른 막대를 클릭했을 때: 이동 가능 여부(자신의 아래에는 자신보다 작은 숫자의 원반만 허용) 검사
            int fromPeg = selectedPeg;
            if (pegs[fromPeg].Count == 0)
            {
                selectedPeg = -1;
                return false;
            }

            int movingDisk = pegs[fromPeg][pegs[fromPeg].Count - 1];

            if (!CanMoveDisk(fromPeg, pegIndex))
            {
                // 규칙 위반: 이동 불가, 원반 흔들림 + 오류 사운드
                selectedPeg = -1;
                StartDiskShake(movingDisk, shakeDuration * 1.25f, shakeMagnitude * 1.35f);
                PlayHanoiSound(HanoiSoundType.Error);
                ApplySize(size);
                RefreshUI();
                return false;
            }

            // 규칙 통과: 다른 막대의 맨 위로 순간이동 + 조금 떨리는 이펙트 + 사운드
            pegs[fromPeg].RemoveAt(pegs[fromPeg].Count - 1);
            pegs[pegIndex].Add(movingDisk);
            selectedPeg = -1;

            StartDiskShake(movingDisk, shakeDuration, shakeMagnitude);

            bool solved = pegs[HanoiPegCount - 1].Count == HanoiDiskCount;
            if (solved)
            {
                PlayHanoiSound(HanoiSoundType.Connected);
                CompleteNetworkPuzzle();
            }
            else
            {
                PlayHanoiSound(HanoiSoundType.Move);
                ApplySize(size);
                RefreshUI();
            }

            return true;
        }

        public bool CanMoveDisk(int fromPeg, int toPeg)
        {
            if (fromPeg < 0 || fromPeg >= HanoiPegCount ||
                toPeg < 0 || toPeg >= HanoiPegCount ||
                fromPeg == toPeg)
                return false;

            if (pegs[fromPeg].Count == 0)
                return false;

            if (pegs[toPeg].Count == 0)
                return true;

            int movingDisk = pegs[fromPeg][pegs[fromPeg].Count - 1];
            int targetTopDisk = pegs[toPeg][pegs[toPeg].Count - 1];

            // 자신의 아래(targetTopDisk)에는 자신(movingDisk)보다 작은 숫자의 원반만 올 수 있음
            return GetDiskNumber(targetTopDisk) < GetDiskNumber(movingDisk);
        }

        private void CompleteNetworkPuzzle()
        {
            localConnected = true;
            var connection = GetWifiConnection();
            if (connection != null)
                connection.SetConnected(true);

            lastOnline = IsOnline;
            ApplySize(size);
            RefreshUI();
        }

        private void StartDiskShake(int diskIndex, float duration, float magnitude)
        {
            if (diskIndex < 0 || diskIndex >= HanoiDiskCount) return;
            diskShakeTimers[diskIndex] = Mathf.Max(0.01f, duration);
            diskShakeMagnitudes[diskIndex] = Mathf.Max(0.005f, magnitude);
        }

        private void PlayHanoiSound(HanoiSoundType soundType)
        {
            AudioClip clip = soundType switch
            {
                HanoiSoundType.Select => diskSelectClip != null ? diskSelectClip : GetFallbackClip(HanoiSoundType.Select),
                HanoiSoundType.Move => diskMoveClip != null ? diskMoveClip : GetFallbackClip(HanoiSoundType.Move),
                HanoiSoundType.Error => diskErrorClip != null ? diskErrorClip : GetFallbackClip(HanoiSoundType.Error),
                HanoiSoundType.Connected => networkConnectedClip != null ? networkConnectedClip : GetFallbackClip(HanoiSoundType.Connected),
                HanoiSoundType.Shot => gunShotClip != null ? gunShotClip : GetFallbackClip(HanoiSoundType.Shot),
                HanoiSoundType.Ricochet => gunRicochetClip != null ? gunRicochetClip : GetFallbackClip(HanoiSoundType.Ricochet),
                _ => null
            };

            if (clip == null) return;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                }
            }

            LastPlayedClip = clip;
            SoundPlayCount++;
            audioSource.PlayOneShot(clip);
        }

        private static AudioClip GetFallbackClip(HanoiSoundType type)
        {
            return type switch
            {
                HanoiSoundType.Select => fallbackSelectClip != null
                    ? fallbackSelectClip
                    : (fallbackSelectClip = CreateRetroToneClip("HanoiSelect", 0.045f, 740f, 920f, 0.22f)),
                HanoiSoundType.Move => fallbackMoveClip != null
                    ? fallbackMoveClip
                    : (fallbackMoveClip = CreateRetroToneClip("HanoiMove", 0.065f, 320f, 180f, 0.28f)),
                HanoiSoundType.Error => fallbackErrorClip != null
                    ? fallbackErrorClip
                    : (fallbackErrorClip = CreateRetroToneClip("HanoiError", 0.12f, 165f, 130f, 0.30f)),
                HanoiSoundType.Connected => fallbackConnectedClip != null
                    ? fallbackConnectedClip
                    : (fallbackConnectedClip = CreateRetroToneClip("HanoiConnected", 0.18f, 523.25f, 1046.5f, 0.26f)),
                HanoiSoundType.Shot => fallbackGunShotClip != null
                    ? fallbackGunShotClip
                    : (fallbackGunShotClip = CreateRetroToneClip("GunShot", 0.14f, 440f, 75f, 0.36f)),
                HanoiSoundType.Ricochet => fallbackGunRicochetClip != null
                    ? fallbackGunRicochetClip
                    : (fallbackGunRicochetClip = CreateRetroToneClip("GunRicochet", 0.16f, 1950f, 620f, 0.35f)),
                _ => null
            };
        }

        private static AudioClip CreateRetroToneClip(
            string clipName,
            float durationSeconds,
            float startFreq,
            float endFreq,
            float volume)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += freq / sampleRate;
                float wave = Mathf.Sin(phase * Mathf.PI * 2f) >= 0f ? 1f : -1f;
                float envelope = 1f - t;
                samples[i] = wave * volume * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public bool IsHanoiBlockedByWindowExe()
        {
            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null) return false;

            Rect myBounds = GetWindowWorldRect(this);

            foreach (var process in manager.ActiveProcesses)
            {
                if (process == null || process.State == WindowState.Minimized)
                    continue;

                Window other = process.Instance;
                if (other == null || other == this || !other.gameObject.activeInHierarchy)
                    continue;

                if (!IsRevealWindowProcess(process))
                    continue;

                Rect otherBounds = GetWindowWorldRect(other);
                if (myBounds.Overlaps(otherBounds))
                    return true;
            }

            return false;
        }

        private bool IsRevealWindowProcess(WindowProcess process)
        {
            if (process == null || process.Instance == null)
                return false;

            if (!string.IsNullOrEmpty(requiredRevealWindowId) &&
                string.Equals(process.Id, requiredRevealWindowId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(process.Id, "window", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(process.Id, "WINDOW.exe", StringComparison.OrdinalIgnoreCase))
                return true;

            return process.Instance is WindowExeWindow;
        }

        private static Rect GetWindowWorldRect(Window window)
        {
            Rect local = Rect.MinMaxRect(
                -window.Size.x * 0.5f,
                -window.Size.y * 0.5f,
                window.Size.x * 0.5f,
                window.Size.y * 0.5f + window.BarHeight
            );
            return WorldRect(local, window.transform);
        }

        public void ChooseHistory(int index)
        {
            if (index == 2 && !decoded)
                return;

            dropdownOpen = false;

            selectedText = index == 0
                ? firstHistory
                : index == 1
                    ? secondHistory
                    : "총";

            if (index == 0 || index == 1)
            {
                phase3Result = InternetPhase.Result404;
            }
            else if (index == 2)
            {
                phase3Result = InternetPhase.ResultGun;
            }

            RefreshUI();
        }

        public Rect GetGunWorldRect()
        {
            Rect local = CenterRect(gunResultOffset.x, gunResultOffset.y, gunResultSize.x, gunResultSize.y);
            return WorldRect(local, transform);
        }

        public Vector2 GetGunMuzzleWorldPosition()
        {
            Vector2 localMuzzle = new Vector2(
                gunResultOffset.x + gunResultSize.x * gunMuzzleNormalizedOffset.x,
                gunResultOffset.y + gunResultSize.y * gunMuzzleNormalizedOffset.y
            );
            Vector3 world = transform.TransformPoint(new Vector3(localMuzzle.x, localMuzzle.y, 0f));
            return new Vector2(world.x, world.y);
        }

        public Rect GetGunMuzzleWorldRect()
        {
            Vector2 localMuzzle = new Vector2(
                gunResultOffset.x + gunResultSize.x * gunMuzzleNormalizedOffset.x,
                gunResultOffset.y + gunResultSize.y * gunMuzzleNormalizedOffset.y
            );
            Rect local = CenterRect(
                localMuzzle.x,
                localMuzzle.y,
                Mathf.Max(0.4f, gunResultSize.x * 0.25f),
                Mathf.Max(0.4f, gunResultSize.y * 0.35f)
            );
            return WorldRect(local, transform);
        }

        public bool CanShootKeyWindow(out ImageWindow targetKeyWindow)
        {
            targetKeyWindow = null;

            if (CurrentPhase != InternetPhase.ResultGun)
                return false;

            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null)
                return false;

            // 1. 대상 열쇠 창(ImageWindow) 탐색
            if (!string.IsNullOrEmpty(targetKeyWindowId))
            {
                var keyProc = manager.GetProcess(targetKeyWindowId);
                if (keyProc != null &&
                    keyProc.State != WindowState.Minimized &&
                    keyProc.Instance is ImageWindow img &&
                    img.gameObject.activeInHierarchy)
                {
                    targetKeyWindow = img;
                }
            }

            if (targetKeyWindow == null)
            {
                foreach (var proc in manager.ActiveProcesses)
                {
                    if (proc != null &&
                        proc.State != WindowState.Minimized &&
                        proc.Instance is ImageWindow img &&
                        img.gameObject.activeInHierarchy)
                    {
                        targetKeyWindow = img;
                        break;
                    }
                }
            }

            if (targetKeyWindow == null)
                return false;

            Vector2 muzzlePos = GetGunMuzzleWorldPosition();
            Rect muzzleRect = GetGunMuzzleWorldRect();
            Rect keyBounds = GetWindowWorldRect(targetKeyWindow);

            // 2. 방향 조건: 총은 항상 왼쪽을 향하므로 총구 좌표의 왼쪽에 열쇠 창 바운드가 존재해야 함
            if (keyBounds.xMin >= muzzlePos.x || keyBounds.center.x >= muzzlePos.x)
                return false;

            // 3. 중첩 조건: 총구 좌표가 WINDOW.exe 안에 포함되고, 열쇠 창도 WINDOW.exe와 겹쳐 있어야 함
            foreach (var process in manager.ActiveProcesses)
            {
                if (process == null || process.State == WindowState.Minimized || !IsRevealWindowProcess(process))
                    continue;

                Window windowExe = process.Instance;
                if (windowExe == null || windowExe == this || !windowExe.gameObject.activeInHierarchy)
                    continue;

                Rect windowExeBounds = GetWindowWorldRect(windowExe);
                bool muzzleCovered = windowExeBounds.Contains(muzzlePos);
                bool keyCovered = windowExeBounds.Overlaps(keyBounds);

                if (muzzleCovered && keyCovered)
                    return true;
            }

            return false;
        }

        public bool IsMuzzleInsideWindowExe(Vector2 muzzlePos)
        {
            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null) return false;

            foreach (var process in manager.ActiveProcesses)
            {
                if (process == null || process.State == WindowState.Minimized || !IsRevealWindowProcess(process))
                    continue;

                Window windowExe = process.Instance;
                if (windowExe == null || windowExe == this || !windowExe.gameObject.activeInHierarchy)
                    continue;

                Rect windowExeBounds = GetWindowWorldRect(windowExe);
                if (windowExeBounds.Contains(muzzlePos))
                    return true;
            }

            return false;
        }

        public bool TryShootGun()
        {
            if (CurrentPhase != InternetPhase.ResultGun)
                return false;

            Vector2 muzzlePos = GetGunMuzzleWorldPosition();

            // 총구가 WINDOW.exe 안에 덮여 있어야만 발사 가능 옵션 적용 (밖에서는 클릭해도 불발/무반응)
            if (requireMuzzleInWindowExeToShoot && !IsMuzzleInsideWindowExe(muzzlePos))
                return false;

            gunShakeTimer = shakeDuration * 1.5f;
            UpdateGunShakePosition();
            PlayHanoiSound(HanoiSoundType.Shot);

            LastTracerStart = muzzlePos;
            LastRicochetEnd = null;

            bool canShoot = CanShootKeyWindow(out ImageWindow targetKeyWindow) && targetKeyWindow != null;

            if (canShoot)
            {
                // 성공 관통: 총구 -> 열쇠 창 양쪽 벽을 완전히 관통
                float leftEdgeX = targetKeyWindow.transform.position.x - targetKeyWindow.Size.x * 0.5f;
                Vector2 exitPoint = new Vector2(leftEdgeX - 1.2f, muzzlePos.y);
                LastTracerEnd = exitPoint;
                LastShotWasRicochet = false;

                SpawnBulletTracer(muzzlePos, exitPoint, bulletTrailDuration, bulletTrailColor);

                targetKeyWindow.BreakBothBorders();
                OnGunFired?.Invoke(targetKeyWindow);
                return true;
            }
            else
            {
                // 실패 또는 비관통 상태: 총구 전방(왼쪽)에 열쇠 창이 존재하는지 확인
                ImageWindow candidate = FindKeyWindowInFront(muzzlePos);
                if (candidate != null)
                {
                    // 열쇠 창의 우측 테두리 표면에 맞고 도탄
                    float rightEdgeX = candidate.transform.position.x + candidate.Size.x * 0.5f;
                    Vector2 hitPoint = new Vector2(rightEdgeX, muzzlePos.y);
                    Vector2 ricochetEnd = hitPoint + new Vector2(3.0f, 2.0f);

                    LastTracerEnd = hitPoint;
                    LastRicochetEnd = ricochetEnd;
                    LastShotWasRicochet = true;

                    // 1) 총구 -> 충돌 지점 직선 궤적
                    SpawnBulletTracer(muzzlePos, hitPoint, bulletTrailDuration, bulletTrailColor);
                    // 2) 충돌 지점 -> 튕겨나가는 도탄 궤적
                    SpawnBulletTracer(hitPoint, ricochetEnd, bulletTrailDuration, bulletTrailColor);

                    PlayHanoiSound(HanoiSoundType.Ricochet);
                }
                else
                {
                    // 전방에 대상 창이 없으면 화면 밖으로 허공 사격
                    Vector2 missEnd = new Vector2(muzzlePos.x - 8.0f, muzzlePos.y);
                    LastTracerEnd = missEnd;
                    LastShotWasRicochet = false;

                    SpawnBulletTracer(muzzlePos, missEnd, bulletTrailDuration, bulletTrailColor);
                }

                return false;
            }
        }

        private ImageWindow FindKeyWindowInFront(Vector2 muzzlePos)
        {
            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null) return null;

            if (!string.IsNullOrEmpty(targetKeyWindowId))
            {
                var proc = manager.GetProcess(targetKeyWindowId);
                if (proc != null && proc.State != WindowState.Minimized && proc.Instance is ImageWindow img && img.gameObject.activeInHierarchy)
                {
                    Rect b = GetWindowWorldRect(img);
                    if (b.xMin < muzzlePos.x) return img;
                }
            }

            foreach (var proc in manager.ActiveProcesses)
            {
                if (proc != null && proc.State != WindowState.Minimized && proc.Instance is ImageWindow img && img.gameObject.activeInHierarchy)
                {
                    Rect b = GetWindowWorldRect(img);
                    if (b.xMin < muzzlePos.x) return img;
                }
            }

            return null;
        }

        public void SpawnBulletTracer(Vector2 start, Vector2 end, float duration, Color color)
        {
            float dist = Vector2.Distance(start, end);
            if (dist < 0.001f) return;

            var tracerGo = new GameObject("BulletTracer");
            tracerGo.transform.position = (Vector3)(start + end) * 0.5f;

            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            tracerGo.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Front (WindowFront)
            var frontGo = new GameObject("TracerFront");
            frontGo.transform.SetParent(tracerGo.transform, false);
            var frontSr = frontGo.AddComponent<SpriteRenderer>();
            frontSr.sprite = WindowTitleBar.WhiteSprite;
            frontSr.sortingLayerName = PrimarySortingLayerName;
            frontSr.sortingOrder = 32000;
            frontSr.drawMode = SpriteDrawMode.Sliced;
            frontSr.size = new Vector2(dist, bulletTrailThickness);
            frontSr.color = color;

            // Back (WindowBack)
            var backGo = new GameObject("TracerBack");
            backGo.transform.SetParent(tracerGo.transform, false);
            var backSr = backGo.AddComponent<SpriteRenderer>();
            backSr.sprite = WindowTitleBar.WhiteSprite;
            backSr.sortingLayerName = SecondarySortingLayerName;
            backSr.sortingOrder = 32000;
            backSr.drawMode = SpriteDrawMode.Sliced;
            backSr.size = new Vector2(dist, bulletTrailThickness);
            backSr.color = color;

            var fading = tracerGo.AddComponent<TracerFader>();
            fading.Initialize(frontSr, backSr, color, duration);
        }

        private class TracerFader : MonoBehaviour
        {
            private SpriteRenderer front;
            private SpriteRenderer back;
            private Color baseColor;
            private float maxDuration;
            private float timer;

            public void Initialize(SpriteRenderer f, SpriteRenderer b, Color col, float dur)
            {
                front = f;
                back = b;
                baseColor = col;
                maxDuration = Mathf.Max(0.01f, dur);
                timer = maxDuration;
            }

            private void Update()
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                float alpha = timer / maxDuration;
                Color c = baseColor;
                c.a = baseColor.a * alpha;
                if (front != null) front.color = c;
                if (back != null) back.color = c;
            }
        }

        private void UpdateGunShakePosition()
        {
            if (gunResultFront == null || gunResultBack == null)
                return;

            float bar = BarHeight;
            float fullHeight = size.y + bar;
            float top = size.y * .5f + bar;
            float contentCenterY = (top - bar - size.y * .5f) * .5f;

            Vector3 basePos = new Vector3(gunResultOffset.x, contentCenterY + gunResultOffset.y, 0f);
            if (gunShakeTimer > 0f)
            {
                float ratio = gunShakeTimer / Mathf.Max(0.0001f, shakeDuration * 1.5f);
                float recoilX = Mathf.Abs(Mathf.Sin(gunShakeTimer * 85f)) * shakeMagnitude * 1.6f * ratio;
                float recoilY = Mathf.Cos(gunShakeTimer * 110f) * shakeMagnitude * 0.6f * ratio;
                Vector3 shaken = basePos + new Vector3(recoilX, recoilY, 0f);
                gunResultFront.transform.localPosition = shaken;
                gunResultBack.transform.localPosition = shaken;
            }
            else
            {
                gunResultFront.transform.localPosition = basePos;
                gunResultBack.transform.localPosition = basePos;
            }
        }

        public void ManualTick(float deltaTime)
        {
            bool shakingUpdated = false;
            for (int d = 0; d < HanoiDiskCount; d++)
            {
                if (diskShakeTimers[d] > 0f)
                {
                    diskShakeTimers[d] = Mathf.Max(0f, diskShakeTimers[d] - deltaTime);
                    shakingUpdated = true;
                }
            }

            if (shakingUpdated)
                ApplyDiskVisualsAndPositions();

            if (gunShakeTimer > 0f)
            {
                gunShakeTimer = Mathf.Max(0f, gunShakeTimer - deltaTime);
                UpdateGunShakePosition();
            }
        }

        private bool IsCoveredByBlockingWindowForGun(Vector3 point)
        {
            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null) return false;

            var displayOrder = manager.DisplayOrder;
            int myOrderIndex = -1;
            for (int i = 0; i < displayOrder.Count; i++)
            {
                if (displayOrder[i] != null && displayOrder[i].Instance == this)
                {
                    myOrderIndex = i;
                    break;
                }
            }

            if (myOrderIndex >= 0)
            {
                for (int i = myOrderIndex + 1; i < displayOrder.Count; i++)
                {
                    var process = displayOrder[i];
                    if (process == null || process.State == WindowState.Minimized)
                        continue;

                    Window other = process.Instance;
                    if (other == null || other == this || !other.gameObject.activeInHierarchy)
                        continue;

                    if (IsRevealWindowProcess(process))
                    {
                        if (other.TitleContains(point) || other.ButtonAt(point) >= 0 || other.IsPointerOverResizeHandle(point))
                            return true;
                        continue;
                    }

                    Rect bounds = Rect.MinMaxRect(
                        -other.Size.x * .5f,
                        -other.Size.y * .5f,
                        other.Size.x * .5f,
                        other.Size.y * .5f + other.BarHeight
                    );

                    if (bounds.Contains(other.transform.InverseTransformPoint(point)))
                        return true;
                }
            }

            return false;
        }

        private static Rect GetViewportWorldRect(Window window)
        {
            Rect local = CenterRect(0f, 0f, window.Size.x, window.Size.y);
            return WorldRect(local, window.transform);
        }

        public bool TryRevealThirdHistory()
        {
            if (decoded) return true;
            if (!IsOnline || !dropdownOpen) return false;

            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null) return false;

            Rect rowWorld = WorldRect(rowRects[2], transform);
            Rect textWorld = WorldRect(thirdRowRevealLocalRect, transform);

            foreach (var process in manager.ActiveProcesses)
            {
                if (process == null || process.State == WindowState.Minimized || !IsRevealWindowProcess(process))
                    continue;

                Window other = process.Instance;

                if (other == null ||
                    other == this ||
                    !other.gameObject.activeInHierarchy)
                    continue;

                Rect otherViewport = GetViewportWorldRect(other);

                // 세로 방향: 3번째 검색 결과 행이 WINDOW.exe 뷰포트 안에 온전히 들어와야 함
                bool coversVertically =
                    otherViewport.yMin <= textWorld.yMin + 0.02f &&
                    otherViewport.yMax >= textWorld.yMax - 0.02f;

                if (!coversVertically)
                    continue;

                // 가로 방향: 3번째 검색 결과 텍스트 영역 전체를 덮거나, 행 폭의 85% 이상을 완전히 가려야 함
                bool coversTextHorizontally =
                    otherViewport.xMin <= textWorld.xMin + 0.02f &&
                    otherViewport.xMax >= textWorld.xMax - 0.02f;

                float dx =
                    Mathf.Min(rowWorld.xMax, otherViewport.xMax) -
                    Mathf.Max(rowWorld.xMin, otherViewport.xMin);

                float requiredWidth = Mathf.Min(rowWorld.width, otherViewport.width) * 0.85f;
                bool coversRowHorizontally = dx >= Mathf.Max(requiredOverlapWidth, requiredWidth);

                if (!coversTextHorizontally && !coversRowHorizontally)
                    continue;

                decoded = true;
                RefreshUI();
                return true;
            }

            return false;
        }

        private bool IsCoveredByFrontWindow(Vector3 point)
        {
            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null) return false;

            var displayOrder = manager.DisplayOrder;
            int myOrderIndex = -1;
            for (int i = 0; i < displayOrder.Count; i++)
            {
                if (displayOrder[i] != null && displayOrder[i].Instance == this)
                {
                    myOrderIndex = i;
                    break;
                }
            }

            if (myOrderIndex >= 0)
            {
                for (int i = myOrderIndex + 1; i < displayOrder.Count; i++)
                {
                    var process = displayOrder[i];
                    if (process == null || process.State == WindowState.Minimized)
                        continue;

                    Window other = process.Instance;
                    if (other == null || other == this || !other.gameObject.activeInHierarchy)
                        continue;

                    Rect bounds = Rect.MinMaxRect(
                        -other.Size.x * .5f,
                        -other.Size.y * .5f,
                        other.Size.x * .5f,
                        other.Size.y * .5f + other.BarHeight
                    );

                    if (bounds.Contains(other.transform.InverseTransformPoint(point)))
                        return true;
                }

                return false;
            }

            foreach (var process in manager.ActiveProcesses)
            {
                Window other = process.Instance;

                if (other == null ||
                    other == this ||
                    !other.gameObject.activeInHierarchy)
                    continue;

                if (other.transform.parent != transform.parent ||
                    other.transform.GetSiblingIndex() <= transform.GetSiblingIndex())
                    continue;

                Rect bounds = Rect.MinMaxRect(
                    -other.Size.x * .5f,
                    -other.Size.y * .5f,
                    other.Size.x * .5f,
                    other.Size.y * .5f + other.BarHeight
                );

                if (bounds.Contains(other.transform.InverseTransformPoint(point)))
                    return true;
            }

            return false;
        }

        private void RefreshUI()
        {
            if (!initialized || !ReferencesReady()) return;

            bool isLockScreen = !IsPasswordUnlocked;
            bool online = IsOnline;
            bool isPhase3_404 = !isLockScreen && online && phase3Result == InternetPhase.Result404;
            bool isPhase3_Gun = !isLockScreen && online && phase3Result == InternetPhase.ResultGun;
            bool isPhase3 = isPhase3_404 || isPhase3_Gun;

            bool showHanoi = !isLockScreen && !online;
            bool showOfflineIcon = showHanoi && offlineIconSprite != null;

            // 브라우저 배경: WindowFront(artwork)와 WindowBack(backArtwork) 모두 항상 활성화하여
            // Primary와 Secondary 양쪽에서 동일한 메인 화면 렌더링 유지
            artwork.gameObject.SetActive(true);
            backArtwork.gameObject.SetActive(true);

            // Step 1: 잠금 화면 UI (WindowFront & WindowBack)
            lockDialogBorderFront.gameObject.SetActive(isLockScreen);
            lockDialogFillFront.gameObject.SetActive(isLockScreen);
            lockDialogBorderBack.gameObject.SetActive(isLockScreen);
            lockDialogFillBack.gameObject.SetActive(isLockScreen);

            lockTitleLabel.gameObject.SetActive(isLockScreen);
            backLockTitleLabel.gameObject.SetActive(isLockScreen);
            lockGuideLabel.gameObject.SetActive(isLockScreen);
            backLockGuideLabel.gameObject.SetActive(isLockScreen);
            lockErrorLabel.gameObject.SetActive(isLockScreen);
            backLockErrorLabel.gameObject.SetActive(isLockScreen);

            string errorMsg = errorMessageTimer > 0f ? "암호가 올바르지 않습니다." : "";
            lockErrorLabel.text = errorMsg;
            backLockErrorLabel.text = errorMsg;

            lockInputBorderFront.gameObject.SetActive(isLockScreen);
            lockInputFillFront.gameObject.SetActive(isLockScreen);
            lockInputBorderBack.gameObject.SetActive(isLockScreen);
            lockInputFillBack.gameObject.SetActive(isLockScreen);

            lockInputMaskLabel.gameObject.SetActive(isLockScreen);
            backLockInputMaskLabel.gameObject.SetActive(isLockScreen);

            // 마스킹 텍스트: 입력된 글자 수만큼 * 표시 (예: "*   *   *   *")
            string maskText = "";
            for (int i = 0; i < currentPasscodeInput.Length; i++)
            {
                maskText += (i > 0 ? "   *" : "*");
            }
            lockInputMaskLabel.text = maskText;
            backLockInputMaskLabel.text = maskText;

            // WindowBack 투시 동그라미 4개 (WindowBack에만 활성화)
            for (int d = 0; d < 4; d++)
            {
                passcodeBackDots[d].gameObject.SetActive(isLockScreen);
                passcodeBackDots[d].color = passcodeDotColor;
            }

            // 온스크린 키패드
            for (int b = 0; b < KeypadButtonCount; b++)
            {
                keypadButtonBordersFront[b].gameObject.SetActive(isLockScreen);
                keypadButtonFillsFront[b].gameObject.SetActive(isLockScreen);
                keypadButtonBordersBack[b].gameObject.SetActive(isLockScreen);
                keypadButtonFillsBack[b].gameObject.SetActive(isLockScreen);
                keypadButtonLabelsFront[b].gameObject.SetActive(isLockScreen);
                keypadButtonLabelsBack[b].gameObject.SetActive(isLockScreen);
            }

            // Phase 1: 네트워크 연결 하노이 탑 UI
            offlineIconFront.gameObject.SetActive(showHanoi);
            offlineIconBack.gameObject.SetActive(showHanoi);
            offlineIconFront.enabled = showOfflineIcon;
            offlineIconBack.enabled = showOfflineIcon;

            hanoiBackdropFront.gameObject.SetActive(showHanoi);
            hanoiBackdropBack.gameObject.SetActive(showHanoi);
            pegBaseFront.gameObject.SetActive(showHanoi);
            pegBaseBack.gameObject.SetActive(showHanoi);
            offlineLabel.gameObject.SetActive(showHanoi);
            backOfflineLabel.gameObject.SetActive(showHanoi);

            for (int p = 0; p < HanoiPegCount; p++)
            {
                pegPolesFront[p].gameObject.SetActive(showHanoi);
                pegPolesBack[p].gameObject.SetActive(showHanoi);
            }

            for (int d = 0; d < HanoiDiskCount; d++)
            {
                diskFrontBorders[d].gameObject.SetActive(showHanoi);
                diskFrontRenderers[d].gameObject.SetActive(showHanoi);
                diskBackBorders[d].gameObject.SetActive(showHanoi);
                diskBackRenderers[d].gameObject.SetActive(showHanoi);
            }

            // Phase 2: 검색 기록 UI (비-텍스트 요소와 텍스트를 WindowFront와 WindowBack 양쪽에 모두 렌더링)
            bool showSearch = !isLockScreen && online && !isPhase3;
            heading.gameObject.SetActive(showSearch);
            backHeading.gameObject.SetActive(showSearch);
            frontSearchBorder.gameObject.SetActive(showSearch);
            frontSearchFill.gameObject.SetActive(showSearch);
            frontArrowFill.gameObject.SetActive(showSearch);
            searchBorder.gameObject.SetActive(showSearch);
            searchFill.gameObject.SetActive(showSearch);
            arrowFill.gameObject.SetActive(showSearch);
            searchLabel.gameObject.SetActive(showSearch);
            arrowLabel.gameObject.SetActive(showSearch);
            backSearchLabel.gameObject.SetActive(showSearch);
            backArrowLabel.gameObject.SetActive(showSearch);

            string arrowText = dropdownOpen ? "▲" : "▼";
            searchLabel.text = selectedText;
            arrowLabel.text = arrowText;
            backSearchLabel.text = selectedText;
            backArrowLabel.text = arrowText;

            bool showDropdown = showSearch && dropdownOpen;

            frontListBorder.gameObject.SetActive(showDropdown);
            frontListFill.gameObject.SetActive(showDropdown);
            listBorder.gameObject.SetActive(showDropdown);
            listFill.gameObject.SetActive(showDropdown);

            for (int i = 0; i < 3; i++)
            {
                frontRowBackgrounds[i].gameObject.SetActive(showDropdown);
                rowBackgrounds[i].gameObject.SetActive(showDropdown);
                rowLabels[i].gameObject.SetActive(showDropdown);
                backRowLabels[i].gameObject.SetActive(showDropdown);
            }

            string row0Text = "◷  " + firstHistory;
            string row1Text = "◷  " + secondHistory;
            const string decodedRow2Text = "◷  총";

            rowLabels[0].text = row0Text;
            rowLabels[1].text = row1Text;
            backRowLabels[0].text = row0Text;
            backRowLabels[1].text = row1Text;

            // WindowBack의 3번째 행은 항상 진짜 검색 기록("◷  총")을 렌더링
            backRowLabels[2].text = decodedRow2Text;
            backRowLabels[2].color = new Color(.14f, .14f, .14f);

            // WindowFront의 3번째 행은 WINDOW.exe로 완전히 가려 해독되면 WindowBack과 동일한 내용으로 전환
            rowLabels[2].text = decoded
                ? backRowLabels[2].text
                : "◷  " + scrambledHistory;

            rowLabels[2].color = decoded
                ? backRowLabels[2].color
                : new Color(.4f, .4f, .4f);

            frontThirdRowMask.gameObject.SetActive(showDropdown && !decoded);
            glitchRed.gameObject.SetActive(showDropdown && !decoded);
            glitchBlue.gameObject.SetActive(showDropdown && !decoded);

            glitchRed.text = "◷  " + scrambledHistory;
            glitchBlue.text = "◷  " + scrambledHistory;

            // Phase 3: 결과 이미지 (404 / 총) 렌더러
            bool show404 = isPhase3_404;
            bool show404Sprite = show404 && notFound404Sprite != null;
            notFound404Front.gameObject.SetActive(show404);
            notFound404Back.gameObject.SetActive(show404);
            notFound404Front.enabled = show404Sprite;
            notFound404Back.enabled = show404Sprite;

            bool showGun = isPhase3_Gun;
            bool showGunSprite = showGun && gunResultSprite != null;
            gunResultFront.gameObject.SetActive(showGun);
            gunResultBack.gameObject.SetActive(showGun);
            gunResultFront.enabled = showGunSprite;
            gunResultBack.enabled = showGunSprite;
        }

        private static SpriteRenderer CreatePanel(
            Transform parent,
            string name,
            Color color,
            string sortingLayer)
        {
            Transform child = parent.Find(name);

            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = child.gameObject.AddComponent<SpriteRenderer>();

            renderer.sprite = WhitePixel();
            renderer.color = color;
            renderer.sortingLayerName = sortingLayer;

            return renderer;
        }

        private TextMeshPro CreateLabel(
            Transform parent,
            string name,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            string sortingLayer)
        {
            Transform child = parent.Find(name);

            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            var label = child.GetComponent<TextMeshPro>();
            if (label == null)
                label = child.gameObject.AddComponent<TextMeshPro>();

            if (koreanFont != null)
                label.font = koreanFont;

            GameTypography.Apply(label);

            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.sortingLayerID = SortingLayer.NameToID(sortingLayer);
            return label;
        }

        private static void PlaceSprite(
            SpriteRenderer renderer,
            float x,
            float y,
            float w,
            float h)
        {
            if (renderer == null || renderer.sprite == null)
                return;

            renderer.drawMode = SpriteDrawMode.Simple;

            Vector2 native = renderer.sprite.bounds.size;

            renderer.transform.localPosition = new Vector3(x, y, 0f);
            renderer.transform.localScale = new Vector3(
                w / Mathf.Max(.0001f, native.x),
                h / Mathf.Max(.0001f, native.y),
                1f
            );
        }

        private static void PlaceText(
            TextMeshPro label,
            float x,
            float y,
            float w,
            float h)
        {
            if (label == null) return;

            label.rectTransform.localPosition = new Vector3(x, y, 0f);
            label.rectTransform.sizeDelta = new Vector2(w, h);
        }

        private static Rect CenterRect(
            float x,
            float y,
            float w,
            float h)
        {
            return new Rect(
                x - w * .5f,
                y - h * .5f,
                w,
                h
            );
        }

        private static Rect WorldRect(Rect rect, Transform t)
        {
            Vector3 a = t.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0f));
            Vector3 b = t.TransformPoint(new Vector3(rect.xMax, rect.yMin, 0f));
            Vector3 c = t.TransformPoint(new Vector3(rect.xMax, rect.yMax, 0f));
            Vector3 d = t.TransformPoint(new Vector3(rect.xMin, rect.yMax, 0f));

            return Rect.MinMaxRect(
                Mathf.Min(a.x, b.x, c.x, d.x),
                Mathf.Min(a.y, b.y, c.y, d.y),
                Mathf.Max(a.x, b.x, c.x, d.x),
                Mathf.Max(a.y, b.y, c.y, d.y)
            );
        }

        private static Sprite WhitePixel()
        {
            if (whitePixel != null) return whitePixel;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            whitePixel = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(.5f, .5f),
                1f
            );

            return whitePixel;
        }

        private static Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;

            const int texSize = 64;
            var texture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            float center = (texSize - 1) * 0.5f;
            float radius = texSize * 0.48f;

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            circleSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texSize, texSize),
                new Vector2(0.5f, 0.5f),
                texSize
            );

            return circleSprite;
        }

        private void HandleKeyboardInputForPasscode()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // 숫자 키 (상단 숫자 0~9)
            if (kb.digit1Key.wasPressedThisFrame) InputPasscodeDigit('1');
            else if (kb.digit2Key.wasPressedThisFrame) InputPasscodeDigit('2');
            else if (kb.digit3Key.wasPressedThisFrame) InputPasscodeDigit('3');
            else if (kb.digit4Key.wasPressedThisFrame) InputPasscodeDigit('4');
            else if (kb.digit5Key.wasPressedThisFrame) InputPasscodeDigit('5');
            else if (kb.digit6Key.wasPressedThisFrame) InputPasscodeDigit('6');
            else if (kb.digit7Key.wasPressedThisFrame) InputPasscodeDigit('7');
            else if (kb.digit8Key.wasPressedThisFrame) InputPasscodeDigit('8');
            else if (kb.digit9Key.wasPressedThisFrame) InputPasscodeDigit('9');
            else if (kb.digit0Key.wasPressedThisFrame) InputPasscodeDigit('0');
            // 넘패드 숫자 키 0~9
            else if (kb.numpad1Key.wasPressedThisFrame) InputPasscodeDigit('1');
            else if (kb.numpad2Key.wasPressedThisFrame) InputPasscodeDigit('2');
            else if (kb.numpad3Key.wasPressedThisFrame) InputPasscodeDigit('3');
            else if (kb.numpad4Key.wasPressedThisFrame) InputPasscodeDigit('4');
            else if (kb.numpad5Key.wasPressedThisFrame) InputPasscodeDigit('5');
            else if (kb.numpad6Key.wasPressedThisFrame) InputPasscodeDigit('6');
            else if (kb.numpad7Key.wasPressedThisFrame) InputPasscodeDigit('7');
            else if (kb.numpad8Key.wasPressedThisFrame) InputPasscodeDigit('8');
            else if (kb.numpad9Key.wasPressedThisFrame) InputPasscodeDigit('9');
            else if (kb.numpad0Key.wasPressedThisFrame) InputPasscodeDigit('0');
            // 백스페이스 및 Delete
            else if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame)
            {
                DeleteLastPasscodeDigit();
            }
        }

        public void InputPasscodeDigit(char digit)
        {
            if (IsPasswordUnlocked || currentPasscodeInput.Length >= 4) return;
            currentPasscodeInput += digit;
            PlayHanoiSound(HanoiSoundType.Select);

            if (currentPasscodeInput.Length == 4)
            {
                ValidatePasscode();
            }
            else
            {
                RefreshUI();
            }
        }

        public void DeleteLastPasscodeDigit()
        {
            if (IsPasswordUnlocked || currentPasscodeInput.Length == 0) return;
            currentPasscodeInput = currentPasscodeInput.Substring(0, currentPasscodeInput.Length - 1);
            PlayHanoiSound(HanoiSoundType.Select);
            RefreshUI();
        }

        public void ClearPasscode()
        {
            if (IsPasswordUnlocked || currentPasscodeInput.Length == 0) return;
            currentPasscodeInput = "";
            PlayHanoiSound(HanoiSoundType.Select);
            RefreshUI();
        }

        public bool ValidatePasscode()
        {
            if (currentPasscodeInput == passcode)
            {
                isPasswordUnlocked = true;
                errorMessageTimer = 0f;
                PlayHanoiSound(HanoiSoundType.Connected);
                RefreshUI();
                return true;
            }
            else
            {
                currentPasscodeInput = "";
                errorMessageTimer = errorMessageDuration;
                PlayHanoiSound(HanoiSoundType.Error);
                RefreshUI();
                return false;
            }
        }

        public Vector2 GetPasscodeDotSize(int index)
        {
            if (index < 0 || index >= 4 || passcodeBackDots[index] == null)
                return Vector2.zero;
            return passcodeBackDots[index].transform.localScale;
        }

        public Color GetPasscodeDotColor(int index)
        {
            if (index < 0 || index >= 4 || passcodeBackDots[index] == null)
                return Color.clear;
            return passcodeBackDots[index].color;
        }

        public Rect GetPasscodeBackDotWorldRect(int index)
        {
            if (index < 0 || index >= 4 || passcodeBackDots[index] == null)
                return default;
            var pos = passcodeBackDots[index].transform.position;
            var scale = passcodeBackDots[index].transform.lossyScale;
            return CenterRect(pos.x, pos.y, scale.x, scale.y);
        }

        public Rect GetKeypadButtonWorldRect(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= KeypadButtonCount)
                return default;
            return WorldRect(keypadButtonRects[buttonIndex], transform);
        }

        public void SimulatePasscodeInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            for (int i = 0; i < input.Length; i++)
            {
                InputPasscodeDigit(input[i]);
            }
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();

            size = new Vector2(10f, 5.1f);
            minSize = new Vector2(7.2f, 4f);
            maxSize = new Vector2(20f, 20f);
        }
#endif
    }
}
