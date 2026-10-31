
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DesktopIconView))]
    public sealed class KeyFileMechanic : MonoBehaviour
    {
        [Header("File Names")]
        [SerializeField] private string initialFileName = "key.txt";
        [SerializeField] private string correctFileName = "key.png";

        [Header("Sprites")]
        [SerializeField] private Sprite textFileIcon;
        [SerializeField] private Sprite imageFileIcon;
        [SerializeField] private Sprite keyImage;

        [Header("Image Window")]
        [SerializeField] private string imageWindowId = "key-image";

        [Header("Popup Font")]
        [SerializeField] private Font uiFont;

        [Header("Double Click")]
        [SerializeField] private float doubleClickSeconds = 0.3f;
        [SerializeField] private float doubleClickDistance = 12f;

        private DesktopIconView icon;
        private Font generatedFont;

        private GameObject overlay;
        private InputField renameInput;

        private bool converted = false;

        private float lastClickTime = -100f;
        private Vector2 lastClickPosition;

        // Cancel owned popup input without confirming a rename or converting the file.
        public void CancelPendingInteraction()
        {
            var input = renameInput; renameInput = null;
            var popup = overlay; overlay = null;
            lastClickTime = -100f;
            if (input != null)
            {
                input.onEndEdit.RemoveListener(FinishRename);
                input.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(input.gameObject); else DestroyImmediate(input.gameObject);
            }
            if (popup != null)
            {
                popup.SetActive(false);
                if (Application.isPlaying) Destroy(popup); else DestroyImmediate(popup);
            }
        }

        private void Awake()
        {
            icon = GetComponent<DesktopIconView>();
        }

        private void Start()
        {
            if (icon == null)
                return;

            icon.SetDisplayName(initialFileName);

            if (textFileIcon != null)
                icon.SetIconSprite(textFileIcon);
        }

        // ==========================================
        // 더블클릭
        // ==========================================

        public void HandleLeftClick(
            Vector2 screenPosition,
            float time)
        {
            if (icon == null ||
                renameInput != null ||
                overlay != null)
                return;

            if (icon.Desktop != null)
                icon.Desktop.Select(icon);

            bool doubleClicked =
                time - lastClickTime <= doubleClickSeconds &&
                Vector2.Distance(
                    screenPosition,
                    lastClickPosition
                ) <= doubleClickDistance;

            lastClickTime = doubleClicked ? -100f : time;
            lastClickPosition = screenPosition;

            if (doubleClicked)
                OpenFile();
        }

        // ==========================================
        // 우클릭 메뉴
        // ==========================================

        public void ShowContextMenu(Vector2 screenPosition)
        {
            if (icon == null || renameInput != null)
                return;

            if (icon.Desktop != null)
                icon.Desktop.Select(icon);

            lastClickTime = -100f;

            DestroyOverlay();

            RectTransform root = CreatePopupRoot(false);

            if (root == null)
                return;

            // 메뉴 바깥 클릭 시 닫기
            Button dismiss = CreateButton(
                root,
                "Dismiss",
                "",
                Color.clear,
                () => DestroyOverlay()
            );

            Stretch((RectTransform)dismiss.transform);
            dismiss.transform.SetAsFirstSibling();

            // 메뉴 배경
            RectTransform menu = CreatePanel(
                root,
                "ContextMenu",
                new Color(.82f, .82f, .82f),
                new Vector2(170f, 72f)
            );

            menu.pivot = new Vector2(0f, 1f);

            // 기존 Canvas의 카메라를 사용하여
            // 화면 좌표를 UI 좌표로 변환
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    root,
                    screenPosition,
                    GetPopupEventCamera(),
                    out Vector2 p
                );

            p.x = Mathf.Clamp(
                p.x,
                root.rect.xMin + 4f,
                root.rect.xMax - 174f
            );

            p.y = Mathf.Clamp(
                p.y,
                root.rect.yMin + 76f,
                root.rect.yMax - 4f
            );

            menu.anchoredPosition = p;

            // 열기 버튼
            Button openButton = CreateButton(
                menu,
                "Open",
                "열기",
                new Color(.94f, .94f, .94f),
                () =>
                {
                    DestroyOverlay();
                    OpenFile();
                }
            );

            Place(
                (RectTransform)openButton.transform,
                new Vector2(0f, 17f),
                new Vector2(162f, 30f)
            );

            // 이름 바꾸기 버튼
            Button renameButton = CreateButton(
                menu,
                "Rename",
                "이름 바꾸기",
                new Color(.94f, .94f, .94f),
                () =>
                {
                    DestroyOverlay();
                    BeginRename();
                }
            );

            Place(
                (RectTransform)renameButton.transform,
                new Vector2(0f, -17f),
                new Vector2(162f, 30f)
            );
        }

        // ==========================================
        // 파일 열기
        // ==========================================

        private void OpenFile()
        {
            // key.txt 상태
            if (!converted)
            {
                ShowDialog(
                    "파일 열기 오류",
                    "파일 이름이 정확하지 않아 파일을 열 수 없습니다.",
                    "확인",
                    () => { }
                );

                return;
            }

            // key.png 상태
            WindowManager manager = WindowManager.Instance;

            if (manager == null)
            {
                Debug.LogError(
                    "[KeyFileMechanic] WindowManager가 없습니다.",
                    this
                );

                return;
            }

            Window window = manager.Open(imageWindowId);

            if (window == null)
            {
                Debug.LogError(
                    "[KeyFileMechanic] 이미지 창을 열 수 없습니다. " +
                    "WindowManager에 key-image가 등록됐는지 확인하세요.",
                    this
                );

                return;
            }

            // 이미지 창 제목
            window.SetWindowTitle("key.png");

            // 이미지 창에 그림 설정
            ImageWindow imageWindow = window as ImageWindow;

            if (imageWindow != null)
            {
                if (keyImage != null)
                {
                    imageWindow.SetPrimarySprite(keyImage);
                }
                else
                {
                    Debug.LogWarning(
                        "[KeyFileMechanic] Key Image가 비어 있습니다.",
                        this
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    "[KeyFileMechanic] key-image에는 " +
                    "ImageWindow 프리팹을 등록해야 합니다.",
                    this
                );
            }
        }

        // ==========================================
        // 이름 바꾸기 시작
        // ==========================================

        private void BeginRename()
        {
            if (icon == null || icon.NameLabel == null)
                return;

            RectTransform original =
                icon.NameLabel.rectTransform;

            GameObject go = new GameObject(
                "KeyFileRenameInput",
                typeof(RectTransform),
                typeof(Image),
                typeof(InputField)
            );

            go.transform.SetParent(original.parent, false);

            RectTransform rect =
                (RectTransform)go.transform;

            rect.anchorMin = original.anchorMin;
            rect.anchorMax = original.anchorMax;
            rect.pivot = original.pivot;
            rect.anchoredPosition = original.anchoredPosition;

            rect.sizeDelta =
                original.sizeDelta + new Vector2(16f, 4f);

            rect.localScale = original.localScale;
            rect.SetAsLastSibling();

            Image background = go.GetComponent<Image>();
            background.color = Color.white;

            Text inputText = CreateText(
                rect,
                "InputText",
                "",
                Color.black,
                TextAnchor.MiddleCenter,
                icon.NameLabel.fontSize
            );

            Stretch(inputText.rectTransform);

            inputText.rectTransform.offsetMin =
                new Vector2(3f, 0f);

            inputText.rectTransform.offsetMax =
                new Vector2(-3f, 0f);

            renameInput = go.GetComponent<InputField>();

            renameInput.textComponent = inputText;
            renameInput.characterLimit = 80;

            renameInput.text = converted
                ? correctFileName
                : initialFileName;

            renameInput.onEndEdit.AddListener(FinishRename);

            renameInput.Select();
            renameInput.ActivateInputField();

            renameInput.caretPosition =
                renameInput.text.Length;
        }

        // ==========================================
        // 이름 변경 결과 검사
        // ==========================================

        private void FinishRename(string value)
        {
            if (renameInput == null)
                return;

            InputField input = renameInput;
            renameInput = null;

            Destroy(input.gameObject);

            value = value == null ? "" : value.Trim();

            // 변경할 필요가 없는 경우
            if (converted || value == initialFileName)
                return;

            // key.png가 아니면 실패
            if (value != correctFileName)
            {
                ShowDialog(
                    "이름 바꾸기",
                    "파일 이름이 올바르지 않습니다.",
                    "확인",
                    () => { }
                );

                return;
            }

            // key.png 입력 성공
            ShowDialog(
                "확장명 변경",
                "파일의 확장명을 변경하면 " +
                "파일을 사용할 수 없게 될 수도 있습니다.\n" +
                "그래도 변경하시겠습니까?",
                "확인",
                ConvertFile,
                "취소"
            );
        }

        // ==========================================
        // key.txt -> key.png 변환
        // ==========================================

        private void ConvertFile()
        {
            if (converted)
                return;

            converted = true;

            // 파일 이름 변경
            icon.SetDisplayName("key.png");

            // 아이콘 이미지 변경
            Sprite resultIcon =
                imageFileIcon != null
                    ? imageFileIcon
                    : keyImage;

            if (resultIcon != null)
                icon.SetIconSprite(resultIcon);

            Debug.Log(
                "[KeyFileMechanic] key.txt -> key.png 변환 성공!"
            );

            // 이미지 창 열기
            OpenFile();
        }

        // ==========================================
        // 키보드 입력
        // ==========================================

        private void Update()
        {
            if (renameInput != null &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current
                    .escapeKey.wasPressedThisFrame)
            {
                InputField input = renameInput;
                renameInput = null;

                Destroy(input.gameObject);
            }
        }

        // ==========================================
        // 팝업 생성
        //
        // 중요:
        // 새 Canvas를 만들지 않음
        // Screen Space - Overlay 사용하지 않음
        // 기존 Canvas의 Render Mode를 변경하지 않음
        // ==========================================

        private RectTransform CreatePopupRoot(bool modal)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas == null)
            {
                Debug.LogError(
                    "[KeyFileMechanic] 부모 Canvas를 찾을 수 없습니다.",
                    this
                );

                return null;
            }

            Canvas rootCanvas = parentCanvas.rootCanvas;

            // Canvas 컴포넌트 없이
            // 기존 Canvas 아래에 UI 오브젝트만 생성
            GameObject go = new GameObject(
                "KeyFilePopup",
                typeof(RectTransform)
            );

            go.transform.SetParent(
                rootCanvas.transform,
                false
            );

            go.transform.SetAsLastSibling();

            overlay = go;

            RectTransform root =
                (RectTransform)go.transform;

            Stretch(root);

            // 경고창이 열렸을 때
            // 뒤쪽 UI 클릭 방지
            if (modal)
            {
                RectTransform shield = CreatePanel(
                    root,
                    "ClickShield",
                    new Color(0f, 0f, 0f, .04f),
                    Vector2.zero
                );

                Stretch(shield);

                Image shieldImage =
                    shield.GetComponent<Image>();

                shieldImage.raycastTarget = true;
            }

            return root;
        }

        // ==========================================
        // 기존 Canvas의 카메라 가져오기
        // ==========================================

        private Camera GetPopupEventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
                return null;

            canvas = canvas.rootCanvas;

            // 카메라 기반 Canvas인 경우
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera ||
                canvas.renderMode == RenderMode.WorldSpace)
            {
                return canvas.worldCamera != null
                    ? canvas.worldCamera
                    : Camera.main;
            }

            // 기존 Canvas가 Overlay인 경우에도
            // 설정을 변경하지 않음
            return null;
        }

        // ==========================================
        // 경고창 생성
        // ==========================================

        private void ShowDialog(
            string title,
            string message,
            string okText,
            System.Action onOk,
            string cancelText = null)
        {
            DestroyOverlay();

            RectTransform root = CreatePopupRoot(true);

            if (root == null)
                return;

            // 창 배경
            RectTransform panel = CreatePanel(
                root,
                "Dialog",
                new Color(.80f, .80f, .80f),
                new Vector2(530f, 222f)
            );

            panel.anchoredPosition = Vector2.zero;

            // 제목 표시줄
            RectTransform bar = CreatePanel(
                panel,
                "TitleBar",
                new Color(.09f, .20f, .57f),
                new Vector2(522f, 32f)
            );

            bar.anchoredPosition =
                new Vector2(0f, 91f);

            Text titleLabel = CreateText(
                bar,
                "Title",
                title,
                Color.white,
                TextAnchor.MiddleLeft,
                21
            );

            Stretch(titleLabel.rectTransform);

            titleLabel.rectTransform.offsetMin =
                new Vector2(10f, 0f);

            // 경고 메시지
            Text body = CreateText(
                panel,
                "Message",
                message,
                Color.black,
                TextAnchor.MiddleLeft,
                21
            );

            Place(
                body.rectTransform,
                new Vector2(0f, 11f),
                new Vector2(485f, 100f)
            );

            body.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            body.verticalOverflow =
                VerticalWrapMode.Overflow;

            // 확인 버튼
            float okX =
                cancelText == null ? 0f : -64f;

            Button ok = CreateButton(
                panel,
                "Confirm",
                okText,
                new Color(.93f, .93f, .93f),
                () =>
                {
                    DestroyOverlay();

                    if (onOk != null)
                        onOk();
                }
            );

            Place(
                (RectTransform)ok.transform,
                new Vector2(okX, -77f),
                new Vector2(116f, 37f)
            );

            // 취소 버튼
            if (cancelText != null)
            {
                Button cancel = CreateButton(
                    panel,
                    "Cancel",
                    cancelText,
                    new Color(.93f, .93f, .93f),
                    () => DestroyOverlay()
                );

                Place(
                    (RectTransform)cancel.transform,
                    new Vector2(64f, -77f),
                    new Vector2(116f, 37f)
                );
            }
        }

        // ==========================================
        // UI 생성 보조 함수
        // ==========================================

        private RectTransform CreatePanel(
            Transform parent,
            string name,
            Color color,
            Vector2 size)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image)
            );

            go.transform.SetParent(parent, false);

            RectTransform rect =
                (RectTransform)go.transform;

            Place(rect, Vector2.zero, size);

            Image image = go.GetComponent<Image>();
            image.color = color;

            return rect;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string caption,
            Color color,
            UnityEngine.Events.UnityAction callback)
        {
            RectTransform rect = CreatePanel(
                parent,
                name,
                color,
                new Vector2(116f, 37f)
            );

            Button button =
                rect.gameObject.AddComponent<Button>();

            button.onClick.AddListener(callback);

            if (!string.IsNullOrEmpty(caption))
            {
                Text text = CreateText(
                    rect,
                    "Label",
                    caption,
                    Color.black,
                    TextAnchor.MiddleCenter,
                    20
                );

                Stretch(text.rectTransform);
            }

            return button;
        }

        private Text CreateText(
            Transform parent,
            string name,
            string value,
            Color color,
            TextAnchor anchor,
            int fontSize)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text)
            );

            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();

            text.font = ResolveFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.text = value;
            text.raycastTarget = false;

            return text;
        }

        // ==========================================
        // 폰트
        // ==========================================

        private Font ResolveFont()
        {
            if (uiFont != null)
                return uiFont;

            if (generatedFont == null)
            {
                generatedFont =
                    Font.CreateDynamicFontFromOSFont(
                        new[]
                        {
                            "Malgun Gothic",
                            "Apple SD Gothic Neo",
                            "Noto Sans CJK KR",
                            "Arial"
                        },
                        24
                    );
            }

            return generatedFont;
        }

        // ==========================================
        // RectTransform 배치
        // ==========================================

        private static void Place(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin =
                new Vector2(.5f, .5f);

            rect.anchorMax =
                new Vector2(.5f, .5f);

            rect.pivot =
                new Vector2(.5f, .5f);

            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;

            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ==========================================
        // 팝업 제거
        // ==========================================

        private void DestroyOverlay()
        {
            if (overlay != null)
            {
                Destroy(overlay);
            }

            overlay = null;
        }

        private void OnDisable()
        {
            DestroyOverlay();

            if (renameInput != null)
            {
                InputField input = renameInput;
                renameInput = null;

                Destroy(input.gameObject);
            }
        }

        private void OnDestroy()
        {
            DestroyOverlay();

            if (generatedFont != null)
            {
                Destroy(generatedFont);
            }
        }
    }
}
