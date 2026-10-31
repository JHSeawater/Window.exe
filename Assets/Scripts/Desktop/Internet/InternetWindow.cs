using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    public sealed class InternetWindow : Window
    {
        public enum InternetPhase
        {
            NetworkPuzzle,
            SearchHistory
        }

        private enum HanoiSoundType
        {
            Select,
            Move,
            Error,
            Connected
        }

        public const int HanoiPegCount = 3;
        public const int HanoiDiskCount = 3;

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
        private static AudioClip fallbackSelectClip;
        private static AudioClip fallbackMoveClip;
        private static AudioClip fallbackErrorClip;
        private static AudioClip fallbackConnectedClip;

        private Transform contentRoot;
        private Transform backContentRoot;
        private WindowTitleBar ieTitleBar;

        // WindowFront (Primary) 렌더러
        private SpriteRenderer artwork;
        private SpriteRenderer searchBorder;
        private SpriteRenderer searchFill;
        private SpriteRenderer arrowFill;
        private SpriteRenderer listBorder;
        private SpriteRenderer listFill;
        private readonly SpriteRenderer[] rowBackgrounds = new SpriteRenderer[3];

        // Phase 1 하노이 탑 - WindowFront (평상시 보이지만 WINDOW.exe에는 가려짐)
        private SpriteRenderer hanoiBackdropFront;
        private SpriteRenderer pegBaseFront;
        private readonly SpriteRenderer[] pegPolesFront = new SpriteRenderer[HanoiPegCount];
        private readonly SpriteRenderer[] diskFrontBorders = new SpriteRenderer[HanoiDiskCount];
        private readonly SpriteRenderer[] diskFrontRenderers = new SpriteRenderer[HanoiDiskCount];

        // Phase 1 하노이 탑 - WindowBack (WINDOW.exe를 통해서만 보임)
        private SpriteRenderer backArtwork;
        private SpriteRenderer hanoiBackdropBack;
        private SpriteRenderer pegBaseBack;
        private readonly SpriteRenderer[] pegPolesBack = new SpriteRenderer[HanoiPegCount];
        private readonly SpriteRenderer[] diskBackBorders = new SpriteRenderer[HanoiDiskCount];
        private readonly SpriteRenderer[] diskBackRenderers = new SpriteRenderer[HanoiDiskCount];

        private TextMeshPro heading;
        private TextMeshPro searchLabel;
        private TextMeshPro arrowLabel;
        private TextMeshPro offlineLabel;
        private TextMeshPro backOfflineLabel;
        private TextMeshPro glitchRed;
        private TextMeshPro glitchBlue;

        private readonly TextMeshPro[] rowLabels = new TextMeshPro[3];
        private readonly Rect[] rowRects = new Rect[3];
        private readonly Rect[] pegRects = new Rect[HanoiPegCount];

        private Rect searchRect;
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

        private string selectedText = "검색 기록을 선택하세요";

        public bool IsDecoded => decoded;
        public bool DropdownOpen => dropdownOpen;
        public InternetPhase CurrentPhase => IsOnline ? InternetPhase.SearchHistory : InternetPhase.NetworkPuzzle;
        public int SelectedPeg => selectedPeg;
        public AudioClip LastPlayedClip { get; private set; }
        public int SoundPlayCount { get; private set; }

        public bool IsOnline
        {
            get
            {
                var connection = GetWifiConnection();
                return connection != null ? connection.IsConnected : localConnected;
            }
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

                // WindowFront 기본 브라우저 배경 및 Phase 2 검색 UI
                artwork = CreatePanel(contentRoot, "IE_BrowserArtwork", Color.white, PrimarySortingLayerName);
                searchBorder = CreatePanel(contentRoot, "IE_SearchBorder", new Color(.35f, .35f, .35f), PrimarySortingLayerName);
                searchFill = CreatePanel(contentRoot, "IE_SearchFill", Color.white, PrimarySortingLayerName);
                arrowFill = CreatePanel(contentRoot, "IE_ArrowFill", new Color(.80f, .80f, .80f), PrimarySortingLayerName);
                listBorder = CreatePanel(contentRoot, "IE_ListBorder", new Color(.35f, .35f, .35f), PrimarySortingLayerName);
                listFill = CreatePanel(contentRoot, "IE_ListFill", Color.white, PrimarySortingLayerName);

                for (int i = 0; i < 3; i++)
                {
                    rowBackgrounds[i] = CreatePanel(
                        contentRoot,
                        "IE_RowBackground_" + i,
                        i % 2 == 0 ? Color.white : new Color(.94f, .95f, .97f),
                        PrimarySortingLayerName
                    );
                }

                // Phase 1 하노이 탑 - WindowFront (동일 크기, 동일 색상 블라인드)
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
                backArtwork = CreatePanel(backContentRoot, "IE_BackBrowserArtwork", Color.white, SecondarySortingLayerName);
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

                ieTitleBar = GetComponentInChildren<WindowTitleBar>(true);

                ApplyArtwork();
                HideDefaultArtwork();

                // 기존 Secondary 배경 제거
                SetSecondarySprite(null);

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

            if (browserBackgroundSprite == null)
            {
                Debug.LogWarning(
                    "[InternetWindow] Browser Background Sprite가 비어 있습니다. 인터넷 프리팹 Inspector에 IE 이미지를 연결하세요.",
                    this
                );
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

            searchRect = CenterRect(0f, searchY, searchWidth, fieldHeight);

            PlaceSprite(searchBorder, 0f, searchY, searchWidth, fieldHeight);
            PlaceSprite(searchFill, -.10f, searchY, searchWidth - .24f, fieldHeight - .05f);
            PlaceSprite(arrowFill, searchWidth * .5f - .14f, searchY, .25f, fieldHeight - .05f);

            PlaceText(searchLabel, -.12f, searchY, searchWidth - .49f, fieldHeight * .87f);
            PlaceText(arrowLabel, searchWidth * .5f - .14f, searchY, .25f, fieldHeight * .87f);

            float firstRowY = searchY - (fieldHeight + rowHeight) * .5f - .012f;
            float listY = firstRowY - rowHeight;
            float listHeight = rowHeight * 3f + .04f;

            PlaceSprite(listBorder, 0f, listY, searchWidth, listHeight);
            PlaceSprite(listFill, 0f, listY, searchWidth - .04f, listHeight - .04f);

            for (int i = 0; i < 3; i++)
            {
                float y = firstRowY - rowHeight * i;
                rowRects[i] = CenterRect(0f, y, searchWidth, rowHeight);

                PlaceSprite(rowBackgrounds[i], 0f, y, searchWidth - .06f, rowHeight - .03f);
                PlaceText(rowLabels[i], 0f, y, searchWidth - .30f, rowHeight * .88f);
            }

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

            // Phase 1: 네트워크 연결 하노이 탑 퍼즐 배치
            LayoutHanoiElements(width, fullHeight, top);
        }

        private void LayoutHanoiElements(float width, float fullHeight, float top)
        {
            float guideY = top - fullHeight * 0.415f;
            PlaceText(offlineLabel, 0f, guideY, width * 0.74f, fullHeight * 0.10f);
            PlaceText(backOfflineLabel, 0f, guideY, width * 0.74f, fullHeight * 0.10f);

            float boardCenterY = top - fullHeight * 0.64f;
            float boardWidth = width * 0.68f;
            float boardHeight = fullHeight * 0.39f;

            hanoiBoardLocalRect = CenterRect(0f, boardCenterY, boardWidth, boardHeight);

            PlaceSprite(hanoiBackdropFront, 0f, boardCenterY, boardWidth, boardHeight);
            PlaceSprite(hanoiBackdropBack, 0f, boardCenterY, boardWidth, boardHeight);

            float baseY = top - fullHeight * 0.79f;
            float baseWidth = boardWidth * 0.90f;
            float baseThickness = Mathf.Max(0.08f, fullHeight * 0.016f);

            PlaceSprite(pegBaseFront, 0f, baseY, baseWidth, baseThickness);
            PlaceSprite(pegBaseBack, 0f, baseY, baseWidth, baseThickness);

            float pegSpacing = boardWidth * 0.30f;
            float poleHeight = fullHeight * 0.24f;
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
            SetLayer(offlineLabel, PrimarySortingLayerName, baseOrder + 6);

            // WindowBack: Phase 1 하노이 탑 (WINDOW.exe를 통해서만 보임)
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
            SetLayer(backOfflineLabel, SecondarySortingLayerName, baseOrder + 6);

            // WindowFront: Phase 2 검색창 및 드롭다운
            SetLayer(searchBorder, PrimarySortingLayerName, baseOrder + 2);
            SetLayer(searchFill, PrimarySortingLayerName, baseOrder + 3);
            SetLayer(arrowFill, PrimarySortingLayerName, baseOrder + 4);

            SetLayer(heading, PrimarySortingLayerName, baseOrder + 5);
            SetLayer(searchLabel, PrimarySortingLayerName, baseOrder + 5);
            SetLayer(arrowLabel, PrimarySortingLayerName, baseOrder + 5);

            SetLayer(listBorder, PrimarySortingLayerName, baseOrder + 6);
            SetLayer(listFill, PrimarySortingLayerName, baseOrder + 7);

            for (int i = 0; i < 3; i++)
            {
                SetLayer(rowBackgrounds[i], PrimarySortingLayerName, baseOrder + 8);
                SetLayer(rowLabels[i], PrimarySortingLayerName, baseOrder + 9);
            }

            SetLayer(glitchRed, PrimarySortingLayerName, baseOrder + 10);
            SetLayer(glitchBlue, PrimarySortingLayerName, baseOrder + 10);
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
                hanoiBackdropFront == null ||
                hanoiBackdropBack == null ||
                pegBaseFront == null ||
                pegBaseBack == null ||
                searchBorder == null ||
                searchFill == null ||
                arrowFill == null ||
                listBorder == null ||
                listFill == null ||
                heading == null ||
                searchLabel == null ||
                arrowLabel == null ||
                offlineLabel == null ||
                backOfflineLabel == null ||
                glitchRed == null ||
                glitchBlue == null)
            {
                return false;
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
                if (rowBackgrounds[i] == null || rowLabels[i] == null)
                    return false;
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
                    if (pegs[2].Count == HanoiDiskCount)
                        ResetHanoiState();
                }

                RefreshUI();
            }

            // 원반 흔들림(Shake) 이펙트 갱신
            bool shakingUpdated = false;
            for (int d = 0; d < HanoiDiskCount; d++)
            {
                if (diskShakeTimers[d] > 0f)
                {
                    diskShakeTimers[d] = Mathf.Max(0f, diskShakeTimers[d] - Time.unscaledDeltaTime);
                    shakingUpdated = true;
                }
            }

            if (shakingUpdated)
                ApplyDiskVisualsAndPositions();

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

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (!TryGetMouseWorldPosition(out Vector3 mouseWorld))
                return;

            Vector2 local = transform.InverseTransformPoint(mouseWorld);

            if (IsCoveredByFrontWindow(mouseWorld))
                return;

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

            Vector2 screenPos = Mouse.current.position.ReadValue();
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

        private void ChooseHistory(int index)
        {
            if (index == 2 && !decoded)
                return;

            dropdownOpen = false;

            selectedText = index == 0
                ? firstHistory
                : index == 1
                    ? secondHistory
                    : "총";

            RefreshUI();

            string id = index == 0
                ? firstResultId
                : index == 1
                    ? secondResultId
                    : gunFileResultId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("[InternetWindow] 결과 창 ID가 비어 있습니다.", this);
                return;
            }

            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager != null)
                manager.Open(id);
            else
                Debug.LogError("[InternetWindow] WindowManager.Instance가 null입니다.", this);
        }

        private void TryRevealThirdHistory()
        {
            var manager = Manager != null ? Manager : WindowManager.Instance;
            if (manager == null) return;

            Rect target = WorldRect(rowRects[2], transform);

            foreach (var process in manager.ActiveProcesses)
            {
                if (process == null || process.State == WindowState.Minimized || !IsRevealWindowProcess(process))
                    continue;

                Window other = process.Instance;

                if (other == null ||
                    other == this ||
                    !other.gameObject.activeInHierarchy ||
                    other.CurrentInteraction != WindowInteraction.Moving)
                    continue;

                Rect otherBounds = GetWindowWorldRect(other);

                float dx =
                    Mathf.Min(target.xMax, otherBounds.xMax) -
                    Mathf.Max(target.xMin, otherBounds.xMin);

                float dy =
                    Mathf.Min(target.yMax, otherBounds.yMax) -
                    Mathf.Max(target.yMin, otherBounds.yMin);

                if (dx < requiredOverlapWidth || dy < requiredOverlapHeight)
                    continue;

                decoded = true;
                RefreshUI();
                break;
            }
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

            bool online = IsOnline;
            bool showHanoi = !online;

            // Phase 1: 네트워크 연결 하노이 탑 UI
            backArtwork.gameObject.SetActive(showHanoi);
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

            // Phase 2: 검색 기록 UI
            heading.gameObject.SetActive(online);
            searchBorder.gameObject.SetActive(online);
            searchFill.gameObject.SetActive(online);
            arrowFill.gameObject.SetActive(online);
            searchLabel.gameObject.SetActive(online);
            arrowLabel.gameObject.SetActive(online);

            searchLabel.text = selectedText;
            arrowLabel.text = dropdownOpen ? "▲" : "▼";

            bool showDropdown = online && dropdownOpen;

            listBorder.gameObject.SetActive(showDropdown);
            listFill.gameObject.SetActive(showDropdown);

            for (int i = 0; i < 3; i++)
            {
                rowBackgrounds[i].gameObject.SetActive(showDropdown);
                rowLabels[i].gameObject.SetActive(showDropdown);
            }

            rowLabels[0].text = "◷  " + firstHistory;
            rowLabels[1].text = "◷  " + secondHistory;

            rowLabels[2].text = decoded
                ? "◷  총"
                : "◷  " + scrambledHistory;

            rowLabels[2].color = decoded
                ? new Color(.14f, .14f, .14f)
                : new Color(.4f, .4f, .4f);

            glitchRed.gameObject.SetActive(showDropdown && !decoded);
            glitchBlue.gameObject.SetActive(showDropdown && !decoded);

            glitchRed.text = "◷  " + scrambledHistory;
            glitchBlue.text = "◷  " + scrambledHistory;
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
