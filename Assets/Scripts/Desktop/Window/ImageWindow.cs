using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>
    /// 순수 2D(SpriteRenderer) 기반 이미지 창 클래스
    /// Primary(WindowFront, order 0)에 메인 스프라이트를 렌더링하며,
    /// 부모 Window의 Secondary(WindowBack, order 0)와 동일 위치, 동일 크기로 겹쳐집니다.
    /// </summary>
    public class ImageWindow : Window
    {
        [Header("Primary (Front Image)")]
        [SerializeField] private Sprite primarySprite;

        private SpriteRenderer _primaryRenderer;

        public Sprite PrimarySprite
        {
            get => GetPrimarySprite();
            set => SetPrimarySprite(value);
        }

        protected override void InitializePrimary()
        {
            SetupPrimaryRenderer();
            SetPrimarySprite(primarySprite);
        }

        public void SetPrimarySprite(Sprite sprite)
        {
            primarySprite = sprite;

            if (_primaryRenderer == null)
            {
                SetupPrimaryRenderer();
            }

            _primaryRenderer.sprite = sprite;
            _primaryRenderer.enabled = (sprite != null);
        }

        public Sprite GetPrimarySprite()
        {
            if (_primaryRenderer != null) return _primaryRenderer.sprite;
            return primarySprite;
        }

        protected override void ApplySize(Vector2 newSize)
        {
            base.ApplySize(newSize);

            if (_primaryRenderer != null)
            {
                _primaryRenderer.drawMode = SpriteDrawMode.Sliced;
                _primaryRenderer.size = newSize;
            }
        }

        private void SetupPrimaryRenderer()
        {
            if (_primaryRenderer != null) return;

            Transform primaryTransform = transform.Find("Primary");
            if (primaryTransform == null)
            {
                var primaryGo = new GameObject("Primary");
                primaryGo.transform.SetParent(transform, false);
                primaryGo.transform.localPosition = Vector3.zero;
                primaryGo.transform.localScale = Vector3.one;
                primaryTransform = primaryGo.transform;
            }

            _primaryRenderer = primaryTransform.GetComponent<SpriteRenderer>();
            if (_primaryRenderer == null)
            {
                _primaryRenderer = primaryTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            _primaryRenderer.sortingLayerName = PrimarySortingLayerName;
            _primaryRenderer.sortingOrder = DefaultSortingOrder;
            _primaryRenderer.drawMode = SpriteDrawMode.Sliced;
            _primaryRenderer.size = size;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetupPrimaryRenderer();
            ApplySize(size);
            if (primarySprite != null && _primaryRenderer != null)
            {
                _primaryRenderer.sprite = primarySprite;
            }
        }

        protected override void Reset()
        {
            base.Reset();
            SetupPrimaryRenderer();
            ApplySize(size);
        }
#endif
    }
}
