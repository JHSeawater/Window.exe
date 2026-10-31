
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    public sealed class InternetWindow : Window
    {
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

        [Header("Font")]
        [SerializeField] private TMP_FontAsset koreanFont;

        private static Sprite whitePixel;

        private Transform contentRoot;
        private WindowTitleBar ieTitleBar;

        private SpriteRenderer artwork;
        private SpriteRenderer searchBorder;
        private SpriteRenderer searchFill;
        private SpriteRenderer arrowFill;
        private SpriteRenderer listBorder;
        private SpriteRenderer listFill;

        private readonly SpriteRenderer[] rowBackgrounds = new SpriteRenderer[3];

        private TextMeshPro heading;
        private TextMeshPro searchLabel;
        private TextMeshPro arrowLabel;
        private TextMeshPro offlineLabel;
        private TextMeshPro glitchRed;
        private TextMeshPro glitchBlue;

        private readonly TextMeshPro[] rowLabels = new TextMeshPro[3];
        private readonly Rect[] rowRects = new Rect[3];

        private Rect searchRect;

        private bool initialized;
        private bool initializing;
        private bool dropdownOpen;
        private bool decoded;
        private bool lastOnline;

        private string selectedText = "검색 기록을 선택하세요";

        public bool IsDecoded => decoded;
        public bool DropdownOpen => dropdownOpen;

        private bool IsOnline
        {
            get
            {
                var connection = wifi != null
                    ? wifi
                    : global::WindowExe.Desktop.WifiConnection.Instance;

                return connection != null && connection.IsConnected;
            }
        }

        protected override void InitializePrimary()
        {
            if (initializing) return;

            if (initialized && ReferencesReady())
            {
                ApplyArtwork();
                HideDefaultArtwork();
                ApplySize(size);
                RefreshUI();
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

                artwork = CreatePanel("IE_BrowserArtwork", Color.white);

                searchBorder = CreatePanel(
                    "IE_SearchBorder",
                    new Color(.35f, .35f, .35f)
                );

                searchFill = CreatePanel("IE_SearchFill", Color.white);

                arrowFill = CreatePanel(
                    "IE_ArrowFill",
                    new Color(.80f, .80f, .80f)
                );

                listBorder = CreatePanel(
                    "IE_ListBorder",
                    new Color(.35f, .35f, .35f)
                );

                listFill = CreatePanel("IE_ListFill", Color.white);

                for (int i = 0; i < 3; i++)
                {
                    rowBackgrounds[i] = CreatePanel(
                        "IE_RowBackground_" + i,
                        i % 2 == 0
                            ? Color.white
                            : new Color(.94f, .95f, .97f)
                    );
                }

                heading = CreateLabel(
                    "IE_Heading",
                    "검색",
                    3.1f,
                    new Color(.2f, .2f, .2f),
                    TextAlignmentOptions.Center
                );

                searchLabel = CreateLabel(
                    "IE_SearchLabel",
                    selectedText,
                    2.2f,
                    new Color(.12f, .12f, .12f),
                    TextAlignmentOptions.MidlineLeft
                );

                arrowLabel = CreateLabel(
                    "IE_ArrowLabel",
                    "▼",
                    2.1f,
                    Color.black,
                    TextAlignmentOptions.Center
                );

                offlineLabel = CreateLabel(
                    "IE_Offline",
                    "페이지를 표시할 수 없습니다.\n인터넷 연결을 확인하세요.",
                    3f,
                    new Color(.22f, .22f, .22f),
                    TextAlignmentOptions.Center
                );

                offlineLabel.textWrappingMode = TextWrappingModes.Normal;

                for (int i = 0; i < 3; i++)
                {
                    rowLabels[i] = CreateLabel(
                        "IE_RowText_" + i,
                        "",
                        2.1f,
                        new Color(.14f, .14f, .14f),
                        TextAlignmentOptions.MidlineLeft
                    );
                }

                glitchRed = CreateLabel(
                    "IE_GlitchRed",
                    "",
                    2.1f,
                    new Color(.85f, .14f, .20f, .36f),
                    TextAlignmentOptions.MidlineLeft
                );

                glitchBlue = CreateLabel(
                    "IE_GlitchBlue",
                    "",
                    2.1f,
                    new Color(.12f, .53f, .87f, .36f),
                    TextAlignmentOptions.MidlineLeft
                );

                ieTitleBar = GetComponentInChildren<WindowTitleBar>(true);

                ApplyArtwork();
                HideDefaultArtwork();

                // 기존 Secondary 배경 제거
                SetSecondarySprite(null);

                // 이전 버전의 안내 문구가 남아 있다면 비활성화
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
            }
            finally
            {
                initializing = false;
            }
        }

        private void ApplyArtwork()
        {
            if (artwork == null) return;

            artwork.sprite = browserBackgroundSprite != null
                ? browserBackgroundSprite
                : WhitePixel();

            artwork.color = browserBackgroundSprite != null
                ? browserBackgroundTint
                : Color.white;

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
                // 기존 프리팹의 배경 SpriteRenderer가
                // 브라우저 이미지를 덮지 못하도록 비활성화
                foreach (var sprite in
                    contentRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sprite == artwork ||
                        sprite.gameObject.name.StartsWith(
                            "IE_",
                            StringComparison.Ordinal))
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
                bool show = !(
                    artworkIncludesTitleBar &&
                    browserBackgroundSprite != null
                );

                var barSprite = ieTitleBar.GetComponent<SpriteRenderer>();

                if (barSprite != null)
                    barSprite.enabled = show;

                foreach (var label in
                    ieTitleBar.GetComponentsInChildren<TextMeshPro>(true))
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

            PlaceSprite(
                artwork,
                0f,
                bar * .5f,
                width,
                fullHeight
            );

            float headingY = top - fullHeight * .435f;
            float searchY = top - fullHeight * .555f;

            float searchWidth = width * .59f;
            float fieldHeight = Mathf.Max(.30f, fullHeight * .068f);
            float rowHeight = Mathf.Max(.29f, fullHeight * .065f);

            PlaceText(
                heading,
                0f,
                headingY,
                width * .65f,
                fullHeight * .085f
            );

            searchRect = CenterRect(
                0f,
                searchY,
                searchWidth,
                fieldHeight
            );

            PlaceSprite(
                searchBorder,
                0f,
                searchY,
                searchWidth,
                fieldHeight
            );

            PlaceSprite(
                searchFill,
                -.10f,
                searchY,
                searchWidth - .24f,
                fieldHeight - .05f
            );

            PlaceSprite(
                arrowFill,
                searchWidth * .5f - .14f,
                searchY,
                .25f,
                fieldHeight - .05f
            );

            PlaceText(
                searchLabel,
                -.12f,
                searchY,
                searchWidth - .49f,
                fieldHeight * .87f
            );

            PlaceText(
                arrowLabel,
                searchWidth * .5f - .14f,
                searchY,
                .25f,
                fieldHeight * .87f
            );

            float firstRowY =
                searchY -
                (fieldHeight + rowHeight) * .5f -
                .012f;

            float listY = firstRowY - rowHeight;
            float listHeight = rowHeight * 3f + .04f;

            PlaceSprite(
                listBorder,
                0f,
                listY,
                searchWidth,
                listHeight
            );

            PlaceSprite(
                listFill,
                0f,
                listY,
                searchWidth - .04f,
                listHeight - .04f
            );

            for (int i = 0; i < 3; i++)
            {
                float y = firstRowY - rowHeight * i;

                rowRects[i] = CenterRect(
                    0f,
                    y,
                    searchWidth,
                    rowHeight
                );

                PlaceSprite(
                    rowBackgrounds[i],
                    0f,
                    y,
                    searchWidth - .06f,
                    rowHeight - .03f
                );

                PlaceText(
                    rowLabels[i],
                    0f,
                    y,
                    searchWidth - .30f,
                    rowHeight * .88f
                );
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

            PlaceText(
                offlineLabel,
                0f,
                top - fullHeight * .55f,
                width * .78f,
                fullHeight * .18f
            );
        }

        // 부모 Window.ApplySorting()이 렌더링 순서를 덮어쓰므로
        // 인터넷 창의 내부 UI 순서를 매 프레임 보정
        private void LateUpdate()
        {
            if (!initialized || !ReferencesReady()) return;

            var secondary = transform.Find("Secondary");

            var secondaryRenderer = secondary != null
                ? secondary.GetComponent<SpriteRenderer>()
                : null;

            int baseOrder = secondaryRenderer != null
                ? secondaryRenderer.sortingOrder
                : 0;

            // 브라우저 이미지가 가장 뒤
            SetLayer(artwork, baseOrder + 0);

            // 검색창
            SetLayer(searchBorder, baseOrder + 2);
            SetLayer(searchFill, baseOrder + 3);
            SetLayer(arrowFill, baseOrder + 4);

            // 검색 텍스트
            SetLayer(heading, baseOrder + 5);
            SetLayer(searchLabel, baseOrder + 5);
            SetLayer(arrowLabel, baseOrder + 5);
            SetLayer(offlineLabel, baseOrder + 5);

            // 검색기록 드롭다운
            SetLayer(listBorder, baseOrder + 6);
            SetLayer(listFill, baseOrder + 7);

            for (int i = 0; i < 3; i++)
            {
                SetLayer(rowBackgrounds[i], baseOrder + 8);
                SetLayer(rowLabels[i], baseOrder + 9);
            }

            SetLayer(glitchRed, baseOrder + 10);
            SetLayer(glitchBlue, baseOrder + 10);
        }

        private static void SetLayer(Renderer renderer, int order)
        {
            if (renderer == null) return;

            renderer.sortingLayerName = PrimarySortingLayerName;
            renderer.sortingOrder = order;
        }

        private static void SetLayer(TextMeshPro text, int order)
        {
            if (text == null) return;

            Renderer renderer = text.GetComponent<Renderer>();

            if (renderer == null) return;

            renderer.sortingLayerName = PrimarySortingLayerName;
            renderer.sortingOrder = order;
        }

        private bool ReferencesReady()
        {
            if (contentRoot == null ||
                artwork == null ||
                searchBorder == null ||
                searchFill == null ||
                arrowFill == null ||
                listBorder == null ||
                listFill == null ||
                heading == null ||
                searchLabel == null ||
                arrowLabel == null ||
                offlineLabel == null ||
                glitchRed == null ||
                glitchBlue == null)
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (rowBackgrounds[i] == null ||
                    rowLabels[i] == null)
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
                    dropdownOpen = false;

                RefreshUI();
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

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 mouseWorld = cam.ScreenToWorldPoint(
                Mouse.current.position.ReadValue()
            );

            Vector2 local = transform.InverseTransformPoint(mouseWorld);

            if (IsCoveredByFrontWindow(mouseWorld) || !online)
                return;

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
                Debug.LogWarning(
                    "[InternetWindow] 결과 창 ID가 비어 있습니다.",
                    this
                );
                return;
            }

            if (WindowManager.Instance != null)
                WindowManager.Instance.Open(id);
            else
                Debug.LogError(
                    "[InternetWindow] WindowManager.Instance가 null입니다.",
                    this
                );
        }

        private void TryRevealThirdHistory()
        {
            var manager = WindowManager.Instance;
            if (manager == null) return;

            Rect target = WorldRect(rowRects[2], transform);

            foreach (var process in manager.ActiveProcesses)
            {
                if (!string.Equals(
                    process.Id,
                    requiredRevealWindowId,
                    StringComparison.Ordinal))
                    continue;

                Window other = process.Instance;

                if (other == null ||
                    other == this ||
                    !other.gameObject.activeInHierarchy ||
                    other.CurrentInteraction != WindowInteraction.Moving)
                    continue;

                float bar = other.BarHeight;

                Rect localBounds = Rect.MinMaxRect(
                    -other.Size.x * .5f,
                    -other.Size.y * .5f,
                    other.Size.x * .5f,
                    other.Size.y * .5f + bar
                );

                Rect otherBounds = WorldRect(localBounds, other.transform);

                float dx =
                    Mathf.Min(target.xMax, otherBounds.xMax) -
                    Mathf.Max(target.xMin, otherBounds.xMin);

                float dy =
                    Mathf.Min(target.yMax, otherBounds.yMax) -
                    Mathf.Max(target.yMin, otherBounds.yMin);

                if (dx < requiredOverlapWidth ||
                    dy < requiredOverlapHeight)
                    continue;

                decoded = true;
                RefreshUI();
                break;
            }
        }

        private bool IsCoveredByFrontWindow(Vector3 point)
        {
            var manager = WindowManager.Instance;
            if (manager == null) return false;

            foreach (var process in manager.ActiveProcesses)
            {
                Window other = process.Instance;

                if (other == null ||
                    other == this ||
                    !other.gameObject.activeInHierarchy)
                    continue;

                if (other.transform.parent != transform.parent ||
                    other.transform.GetSiblingIndex() <=
                    transform.GetSiblingIndex())
                    continue;

                Rect bounds = Rect.MinMaxRect(
                    -other.Size.x * .5f,
                    -other.Size.y * .5f,
                    other.Size.x * .5f,
                    other.Size.y * .5f + other.BarHeight
                );

                if (bounds.Contains(
                    other.transform.InverseTransformPoint(point)))
                    return true;
            }

            return false;
        }

        private void RefreshUI()
        {
            if (!initialized || !ReferencesReady()) return;

            bool online = IsOnline;

            heading.gameObject.SetActive(online);
            searchBorder.gameObject.SetActive(online);
            searchFill.gameObject.SetActive(online);
            arrowFill.gameObject.SetActive(online);
            searchLabel.gameObject.SetActive(online);
            arrowLabel.gameObject.SetActive(online);
            offlineLabel.gameObject.SetActive(!online);

            searchLabel.text = selectedText;
            arrowLabel.text = dropdownOpen ? "▲" : "▼";

            bool show = online && dropdownOpen;

            listBorder.gameObject.SetActive(show);
            listFill.gameObject.SetActive(show);

            for (int i = 0; i < 3; i++)
            {
                rowBackgrounds[i].gameObject.SetActive(show);
                rowLabels[i].gameObject.SetActive(show);
            }

            rowLabels[0].text = "◷  " + firstHistory;
            rowLabels[1].text = "◷  " + secondHistory;

            rowLabels[2].text = decoded
                ? "◷  총"
                : "◷  " + scrambledHistory;

            rowLabels[2].color = decoded
                ? new Color(.14f, .14f, .14f)
                : new Color(.4f, .4f, .4f);

            glitchRed.gameObject.SetActive(show && !decoded);
            glitchBlue.gameObject.SetActive(show && !decoded);

            glitchRed.text = "◷  " + scrambledHistory;
            glitchBlue.text = "◷  " + scrambledHistory;
        }

        private SpriteRenderer CreatePanel(string name, Color color)
        {
            Transform child = contentRoot.Find(name);

            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(contentRoot, false);
            }

            var renderer = child.GetComponent<SpriteRenderer>();

            if (renderer == null)
                renderer = child.gameObject.AddComponent<SpriteRenderer>();

            renderer.sprite = WhitePixel();
            renderer.color = color;
            renderer.sortingLayerName = PrimarySortingLayerName;

            return renderer;
        }

        private TextMeshPro CreateLabel(
            string name,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            Transform child = contentRoot.Find(name);

            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(contentRoot, false);
            }

            var label = child.GetComponent<TextMeshPro>();

            if (label == null)
                label = child.gameObject.AddComponent<TextMeshPro>();

            if (koreanFont != null)
                label.font = koreanFont;

            label.text = value;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.sortingLayerID = SortingLayer.NameToID(PrimarySortingLayerName);
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
            Vector3 a = t.TransformPoint(
                new Vector3(rect.xMin, rect.yMin, 0f)
            );

            Vector3 b = t.TransformPoint(
                new Vector3(rect.xMax, rect.yMin, 0f)
            );

            Vector3 c = t.TransformPoint(
                new Vector3(rect.xMax, rect.yMax, 0f)
            );

            Vector3 d = t.TransformPoint(
                new Vector3(rect.xMin, rect.yMax, 0f)
            );

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

            var texture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false
            );

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
