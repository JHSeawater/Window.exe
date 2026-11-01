
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
        // ����Ŭ��
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
        // ��Ŭ�� �޴�
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

            // �޴� �ٱ� Ŭ�� �� �ݱ�
            Button dismiss = CreateButton(
                root,
                "Dismiss",
                "",
                Color.clear,
                () => DestroyOverlay()
            );

            Stretch((RectTransform)dismiss.transform);
            dismiss.transform.SetAsFirstSibling();

            // �޴� ���
            RectTransform menu = CreatePanel(
                root,
                "ContextMenu",
                new Color(.82f, .82f, .82f),
                new Vector2(170f, 72f)
            );

            menu.pivot = new Vector2(0f, 1f);

            // ���� Canvas�� ī�޶� ����Ͽ�
            // ȭ�� ��ǥ�� UI ��ǥ�� ��ȯ
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

            // ���� ��ư
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

            // �̸� �ٲٱ� ��ư
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
        // ���� ����
        // ==========================================

        private void OpenFile()
        {
            // key.txt ����
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

            // key.png ����
            WindowManager manager = WindowManager.Instance;

            if (manager == null)
            {
                Debug.LogError(
                    "[KeyFileMechanic] WindowManager�� �����ϴ�.",
                    this
                );

                return;
            }

            Window window = manager.Open(imageWindowId);

            if (window == null)
            {
                Debug.LogError(
                    "[KeyFileMechanic] �̹��� â�� �� �� �����ϴ�. " +
                    "WindowManager�� key-image�� ��ϵƴ��� Ȯ���ϼ���.",
                    this
                );

                return;
            }

            // �̹��� â ����
            window.SetWindowTitle("key.png");

            // �̹��� â�� �׸� ����
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
                        "[KeyFileMechanic] Key Image�� ��� �ֽ��ϴ�.",
                        this
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    "[KeyFileMechanic] key-image���� " +
                    "ImageWindow �������� ����ؾ� �մϴ�.",
                    this
                );
            }
        }

        // ==========================================
        // �̸� �ٲٱ� ����
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
        // �̸� ���� ��� �˻�
        // ==========================================

        private void FinishRename(string value)
        {
            if (renameInput == null)
                return;

            InputField input = renameInput;
            renameInput = null;

            Destroy(input.gameObject);

            value = value == null ? "" : value.Trim();

            // ������ �ʿ䰡 ���� ���
            if (converted || value == initialFileName)
                return;

            // key.png�� �ƴϸ� ����
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

            // key.png �Է� ����
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
        // key.txt -> key.png ��ȯ
        // ==========================================

        private void ConvertFile()
        {
            if (converted)
                return;

            converted = true;

            // ���� �̸� ����
            icon.SetDisplayName("key.png");

            // ������ �̹��� ����
            Sprite resultIcon =
                imageFileIcon != null
                    ? imageFileIcon
                    : keyImage;

            if (resultIcon != null)
                icon.SetIconSprite(resultIcon);

            Debug.Log(
                "[KeyFileMechanic] key.txt -> key.png ��ȯ ����!"
            );

            // �̹��� â ����
            OpenFile();
        }

        // ==========================================
        // Ű���� �Է�
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
        // �˾� ����
        //
        // �߿�:
        // �� Canvas�� ������ ����
        // Screen Space - Overlay ������� ����
        // ���� Canvas�� Render Mode�� �������� ����
        // ==========================================

        private RectTransform CreatePopupRoot(bool modal)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas == null)
            {
                Debug.LogError(
                    "[KeyFileMechanic] �θ� Canvas�� ã�� �� �����ϴ�.",
                    this
                );

                return null;
            }

            Canvas rootCanvas = parentCanvas.rootCanvas;

            // Canvas ������Ʈ ����
            // ���� Canvas �Ʒ��� UI ������Ʈ�� ����
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

            // ���â�� ������ ��
            // ���� UI Ŭ�� ����
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
        // ���� Canvas�� ī�޶� ��������
        // ==========================================

        private Camera GetPopupEventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
                return null;

            canvas = canvas.rootCanvas;

            // ī�޶� ��� Canvas�� ���
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera ||
                canvas.renderMode == RenderMode.WorldSpace)
            {
                return canvas.worldCamera != null
                    ? canvas.worldCamera
                    : Camera.main;
            }

            // ���� Canvas�� Overlay�� ��쿡��
            // ������ �������� ����
            return null;
        }

        // ==========================================
        // ���â ����
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

            // â ���
            RectTransform panel = CreatePanel(
                root,
                "Dialog",
                new Color(.80f, .80f, .80f),
                new Vector2(530f, 222f)
            );

            panel.anchoredPosition = Vector2.zero;

            // ���� ǥ����
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

            // ��� �޽���
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

            // Ȯ�� ��ư
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

            // ��� ��ư
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
        // UI ���� ���� �Լ�
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
        // ��Ʈ
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
        // RectTransform ��ġ
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
        // �˾� ����
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
