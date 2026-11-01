using TMPro;
using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>
    /// 순수 2D(SpriteRenderer + TextMeshPro) 기반 메모장 창 클래스
    /// Primary(WindowFront)에 배경 스프라이트(Order: 0)와 텍스트(Order: 1)를 렌더링하며,
    /// 부모 Window의 Secondary(WindowBack, order 0)와 동일 위치, 동일 크기로 겹쳐집니다.
    /// </summary>
    public class NotepadWindow : Window
    {
        [Header("Primary Background")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Color backgroundColor = Color.white;
        [SerializeField] private SpriteDrawMode backgroundDrawMode = SpriteDrawMode.Simple;

        [Header("Primary Image Alignment")]
        [SerializeField] private bool alignImageTopLeft = false;
        [SerializeField] private Vector2 imageSize = Vector2.zero;
        [SerializeField] private Vector2 imageOffset = Vector2.zero;

        [Header("Primary (Front Text)")]
        [TextArea(3, 10)]
        [SerializeField] private string text;

        [Header("Text Appearance")]
        [SerializeField] private float fontSize = 3.5f;
        [SerializeField] private Color textColor = Color.black;
        [SerializeField] private TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
        [SerializeField] private Vector2 textPadding = new Vector2(0.25f, 0.25f);

        [Header("Secondary (Back) Appearance")]
        [SerializeField] private bool enableSecondaryWhiteBackground = true;
        [SerializeField] private string secondaryText = "HELP ME";
        [SerializeField] private Color secondaryTextColor = Color.black;
        [SerializeField] private float secondaryFontSize = 5f;
        [SerializeField] private TextAlignmentOptions secondaryAlignment = TextAlignmentOptions.Center;

        private SpriteRenderer _backgroundRenderer;
        private TextMeshPro _tmpText;
        private TextMeshPro _secondaryTmpText;

        public string Text
        {
            get => GetText();
            set => SetText(value);
        }

        public Sprite BackgroundSprite
        {
            get => backgroundSprite;
            set => SetBackgroundSprite(value);
        }

        public SpriteDrawMode BackgroundDrawMode
        {
            get => backgroundDrawMode;
            set
            {
                backgroundDrawMode = value;
                if (_backgroundRenderer != null) _backgroundRenderer.drawMode = value;
                ApplySize(size);
            }
        }

        public bool AlignImageTopLeft
        {
            get => alignImageTopLeft;
            set
            {
                alignImageTopLeft = value;
                ApplySize(size);
            }
        }

        public Vector2 ImageSize
        {
            get => imageSize;
            set
            {
                imageSize = value;
                ApplySize(size);
            }
        }

        public Vector2 ImageOffset
        {
            get => imageOffset;
            set
            {
                imageOffset = value;
                ApplySize(size);
            }
        }

        public string SecondaryText
        {
            get => _secondaryTmpText != null ? _secondaryTmpText.text : secondaryText;
            set
            {
                secondaryText = value;
                if (_secondaryTmpText != null) _secondaryTmpText.text = value;
            }
        }

        protected override void InitializePrimary()
        {
            SetupPrimaryComponents();
            SetupSecondaryComponents();
            SetBackgroundSprite(backgroundSprite);
            SetText(text);
        }

        /// <summary>
        /// 메모장의 텍스트를 새로운 내용으로 갱신합니다.
        /// </summary>
        public void SetText(string newText)
        {
            text = newText;

            if (_tmpText == null)
            {
                SetupPrimaryComponents();
            }

            if (_tmpText != null)
            {
                _tmpText.text = newText;
                _tmpText.gameObject.SetActive(!string.IsNullOrEmpty(newText));
            }
        }

        /// <summary>
        /// 메모장 배경 스프라이트를 설정합니다. (null 전달 시 배경 비활성화)
        /// </summary>
        public void SetBackgroundSprite(Sprite sprite)
        {
            backgroundSprite = sprite;

            if (_backgroundRenderer == null)
            {
                SetupPrimaryComponents();
            }

            if (_backgroundRenderer != null)
            {
                _backgroundRenderer.sprite = sprite;
                _backgroundRenderer.color = backgroundColor;
                _backgroundRenderer.enabled = (sprite != null);
            }

            ApplySize(size);
        }

        public string GetText()
        {
            if (_tmpText != null && _tmpText.gameObject.activeSelf) return _tmpText.text;
            return text ?? string.Empty;
        }

        protected override void ApplySize(Vector2 newSize)
        {
            base.ApplySize(newSize);

            if (_backgroundRenderer != null)
            {
                _backgroundRenderer.drawMode = backgroundDrawMode;

                Vector2 renderSize = (imageSize.x > 0f && imageSize.y > 0f) ? imageSize : newSize;

                if (backgroundDrawMode == SpriteDrawMode.Sliced)
                {
                    _backgroundRenderer.size = renderSize;
                    _backgroundRenderer.transform.localScale = Vector3.one;
                }
                else
                {
                    // Simple 모드: 스프라이트를 창 크기(renderSize)에 맞춰 꽉 채우도록 localScale 계산
                    Sprite s = _backgroundRenderer.sprite;
                    if (s != null && s.rect.width > 0f && s.rect.height > 0f)
                    {
                        Vector2 spriteBaseSize = s.rect.size / s.pixelsPerUnit;
                        float scaleX = renderSize.x / spriteBaseSize.x;
                        float scaleY = renderSize.y / spriteBaseSize.y;
                        _backgroundRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                    }
                    else
                    {
                        _backgroundRenderer.transform.localScale = Vector3.one;
                    }
                }

                if (alignImageTopLeft)
                {
                    Vector2 topLeft = new Vector2(-newSize.x / 2f, newSize.y / 2f);
                    Sprite s = _backgroundRenderer.sprite;
                    Vector2 pivotNorm = (s != null && s.rect.width > 0f && s.rect.height > 0f)
                        ? new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height)
                        : new Vector2(0f, 1f);

                    float pivotOffsetX = pivotNorm.x * renderSize.x;
                    float pivotOffsetY = -(1f - pivotNorm.y) * renderSize.y;

                    _backgroundRenderer.transform.localPosition = new Vector3(
                        topLeft.x + pivotOffsetX + imageOffset.x,
                        topLeft.y + pivotOffsetY + imageOffset.y,
                        0f
                    );
                }
                else
                {
                    _backgroundRenderer.transform.localPosition = Vector3.zero;
                }
            }

            if (_tmpText != null)
            {
                // 패딩을 적용하여 텍스트가 배경 테두리 안쪽에 정렬되도록 처리
                Vector2 textAreaSize = new Vector2(
                    Mathf.Max(0.1f, newSize.x - textPadding.x * 2f),
                    Mathf.Max(0.1f, newSize.y - textPadding.y * 2f)
                );
                _tmpText.rectTransform.sizeDelta = textAreaSize;
                _tmpText.rectTransform.localPosition = Vector3.zero;
            }

            if (_secondaryTmpText != null)
            {
                _secondaryTmpText.rectTransform.localPosition = Vector3.zero;
                _secondaryTmpText.rectTransform.sizeDelta = new Vector2(
                    Mathf.Max(0.1f, newSize.x - 0.4f),
                    Mathf.Max(0.1f, newSize.y - 0.4f)
                );
            }
        }

        private void SetupSecondaryComponents()
        {
            if (enableSecondaryWhiteBackground && SecondaryRenderer != null)
            {
                if (SecondaryRenderer.sprite == null)
                {
                    SecondaryRenderer.sprite = WindowTitleBar.WhiteSprite;
                }
                SecondaryRenderer.color = Color.white;
                SecondaryRenderer.drawMode = SpriteDrawMode.Sliced;
                SecondaryRenderer.size = size;
                SecondaryRenderer.enabled = true;
            }

            Transform secondaryTransform = transform.Find("Secondary");
            if (secondaryTransform != null)
            {
                if (_secondaryTmpText == null)
                {
                    Transform textTransform = secondaryTransform.Find("Text");
                    if (textTransform != null)
                    {
                        _secondaryTmpText = textTransform.GetComponent<TextMeshPro>();
                    }

                    if (_secondaryTmpText == null && !string.IsNullOrEmpty(secondaryText))
                    {
                        var textGo = new GameObject("Text");
                        textGo.transform.SetParent(secondaryTransform, false);
                        textGo.transform.localPosition = Vector3.zero;
                        textGo.transform.localScale = Vector3.one;
                        _secondaryTmpText = textGo.AddComponent<TextMeshPro>();
                    }
                }

                if (_secondaryTmpText != null)
                {
                    GameTypography.Apply(_secondaryTmpText);
                    _secondaryTmpText.sortingLayerID = SortingLayer.NameToID(SecondarySortingLayerName);
                    _secondaryTmpText.sortingOrder = 1;
                    _secondaryTmpText.color = secondaryTextColor;
                    _secondaryTmpText.text = secondaryText;
                    _secondaryTmpText.alignment = secondaryAlignment;
                    _secondaryTmpText.fontSize = secondaryFontSize;
                    _secondaryTmpText.rectTransform.localPosition = Vector3.zero;
                    _secondaryTmpText.rectTransform.sizeDelta = new Vector2(
                        Mathf.Max(0.1f, size.x - 0.4f),
                        Mathf.Max(0.1f, size.y - 0.4f)
                    );
                }
            }
        }

        private void SetupPrimaryComponents()
        {
            Transform primaryTransform = transform.Find("Primary");
            if (primaryTransform == null)
            {
                var primaryGo = new GameObject("Primary");
                primaryGo.transform.SetParent(transform, false);
                primaryGo.transform.localPosition = Vector3.zero;
                primaryGo.transform.localScale = Vector3.one;
                primaryTransform = primaryGo.transform;
            }

            // 1. Primary 배경 스프라이트 (Sorting Layer: WindowFront, Order: 0)
            if (_backgroundRenderer == null)
            {
                Transform bgTransform = primaryTransform.Find("Background");
                if (bgTransform == null)
                {
                    var bgGo = new GameObject("Background");
                    bgGo.transform.SetParent(primaryTransform, false);
                    bgGo.transform.localPosition = Vector3.zero;
                    bgGo.transform.localScale = Vector3.one;
                    bgTransform = bgGo.transform;
                }

                _backgroundRenderer = bgTransform.GetComponent<SpriteRenderer>();
                if (_backgroundRenderer == null)
                {
                    _backgroundRenderer = bgTransform.gameObject.AddComponent<SpriteRenderer>();
                }

                _backgroundRenderer.sortingLayerName = PrimarySortingLayerName;
                _backgroundRenderer.sortingOrder = 0; // 배경은 0
                _backgroundRenderer.drawMode = backgroundDrawMode;
                if (backgroundDrawMode == SpriteDrawMode.Sliced)
                {
                    _backgroundRenderer.size = size;
                }
                _backgroundRenderer.color = backgroundColor;
                _backgroundRenderer.sprite = backgroundSprite;
                _backgroundRenderer.enabled = (backgroundSprite != null);
            }

            // 2. Primary 텍스트 (Sorting Layer: WindowFront, Order: 1)
            if (_tmpText == null)
            {
                Transform textTransform = primaryTransform.Find("Text");
                if (textTransform == null)
                {
                    // 이전 버전 호환: primaryTransform 자체에 TextMeshPro가 붙어 있는지 확인
                    _tmpText = primaryTransform.GetComponent<TextMeshPro>();
                    if (_tmpText == null)
                    {
                        var textGo = new GameObject("Text");
                        textGo.transform.SetParent(primaryTransform, false);
                        textGo.transform.localPosition = Vector3.zero;
                        textGo.transform.localScale = Vector3.one;
                        textTransform = textGo.transform;
                        _tmpText = textTransform.gameObject.AddComponent<TextMeshPro>();
                    }
                }
                else
                {
                    _tmpText = textTransform.GetComponent<TextMeshPro>();
                    if (_tmpText == null)
                    {
                        _tmpText = textTransform.gameObject.AddComponent<TextMeshPro>();
                    }
                }

                GameTypography.Apply(_tmpText);
                _tmpText.sortingLayerID = SortingLayer.NameToID(PrimarySortingLayerName);
                _tmpText.sortingOrder = 1; // 텍스트는 배경보다 위인 1

                _tmpText.fontSize = fontSize;
                _tmpText.color = textColor;
                _tmpText.alignment = alignment;
                _tmpText.text = text;

                Vector2 textAreaSize = new Vector2(
                    Mathf.Max(0.1f, size.x - textPadding.x * 2f),
                    Mathf.Max(0.1f, size.y - textPadding.y * 2f)
                );
                _tmpText.rectTransform.sizeDelta = textAreaSize;
            }
        }


    }
}
