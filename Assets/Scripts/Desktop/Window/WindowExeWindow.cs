using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>
    /// WINDOW.exe 메인 뷰포트 창.
    /// 기본적인 창 프레임(제목 표시줄, 8방향 리사이즈, 조작 버튼)을 가지며,
    /// 창 내용 영역(Primary)에는 사용자가 지정한 특수 셰이더 머티리얼 하나만 렌더링됩니다.
    /// </summary>
    public class WindowExeWindow : Window
    {
        [Header("Viewport Appearance")]
        [SerializeField] private Material viewportMaterial;
        [SerializeField] private Sprite fillSprite;
        [SerializeField] private Color viewportColor = Color.white;

        private SpriteRenderer viewportRenderer;

        public Material ViewportMaterial
        {
            get => viewportMaterial;
            set => SetMaterial(value);
        }

        public Sprite FillSprite
        {
            get => fillSprite;
            set => SetFillSprite(value);
        }

        public Color ViewportColor
        {
            get => viewportColor;
            set => SetViewportColor(value);
        }

        public SpriteRenderer ViewportRenderer => viewportRenderer;

        protected override void InitializePrimary()
        {
            var primary = transform.Find("Primary");
            if (primary == null)
            {
                primary = new GameObject("Primary").transform;
                primary.SetParent(transform, false);
            }

            var viewportObj = primary.Find("Viewport");
            if (viewportObj == null)
            {
                viewportObj = new GameObject("Viewport").transform;
                viewportObj.SetParent(primary, false);
            }

            viewportRenderer = viewportObj.GetComponent<SpriteRenderer>();
            if (viewportRenderer == null)
            {
                viewportRenderer = viewportObj.gameObject.AddComponent<SpriteRenderer>();
            }

            viewportRenderer.sortingLayerName = PrimarySortingLayerName;
            viewportRenderer.sortingOrder = 0;
            viewportRenderer.drawMode = SpriteDrawMode.Sliced;

            if (fillSprite == null) fillSprite = WindowTitleBar.WhiteSprite;
            viewportRenderer.sprite = fillSprite;
            viewportRenderer.color = viewportColor;

            if (viewportMaterial != null)
            {
                viewportRenderer.sharedMaterial = viewportMaterial;
            }

            ApplySize(size);
        }

        public void SetMaterial(Material material)
        {
            viewportMaterial = material;
            if (viewportRenderer != null)
            {
                viewportRenderer.sharedMaterial = material;
            }
        }

        public void SetFillSprite(Sprite sprite)
        {
            fillSprite = sprite != null ? sprite : WindowTitleBar.WhiteSprite;
            if (viewportRenderer != null)
            {
                viewportRenderer.sprite = fillSprite;
            }
        }

        public void SetViewportColor(Color color)
        {
            viewportColor = color;
            if (viewportRenderer != null)
            {
                viewportRenderer.color = color;
            }
        }

        protected override void ApplySize(Vector2 value)
        {
            base.ApplySize(value);
            if (viewportRenderer != null)
            {
                viewportRenderer.size = value;
            }
        }
    }
}

