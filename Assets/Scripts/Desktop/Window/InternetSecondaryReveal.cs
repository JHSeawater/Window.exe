
using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    /// <summary>
    /// WINDOW.exe의 뷰포트를 통해서만
    /// 인터넷의 Secondary 이미지를 보여준다.
    ///
    /// Primary 이미지는 변경하지 않는다.
    /// 드러난 세 번째 검색기록을 클릭하면 gun-file을 연다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InternetWindow))]
    public class InternetSecondaryReveal : MonoBehaviour
    {
        [Header("Secondary Image")]
        [SerializeField]
        private Sprite secondaryImage;

        [Header("WINDOW.exe")]
        [SerializeField]
        private string windowExeId = "WINDOW.exe";

        [Header("Result")]
        [SerializeField]
        private string gunFileId = "gun-file";

        [Header("Reveal")]
        [SerializeField]
        private bool onlyWhenDropdownOpen = true;

        private InternetWindow internetWindow;
        private WindowExeWindow windowExe;

        private SpriteRenderer browserArtwork;
        private SpriteRenderer thirdHistoryRow;

        private SpriteRenderer secondaryRenderer;
        private SpriteMask revealMask;

        private GameObject revealRoot;
        private GameObject maskObject;

        private Sprite maskSprite;
        private Texture2D maskTexture;

        private bool revealed;

        private void Awake()
        {
            internetWindow = GetComponent<InternetWindow>();
        }

        private void Start()
        {
            CreateRevealObjects();
        }

        private void LateUpdate()
        {
            if (revealRoot == null)
                CreateRevealObjects();

            FindRequiredObjects();

            if (!CanReveal())
            {
                SetRevealVisible(false);
                return;
            }

            UpdateSecondaryImage();
            UpdateMaskPosition();

            SetRevealVisible(true);

            CheckGunClick();
        }

        // =====================================
        // 필요한 오브젝트 찾기
        // =====================================

        private void FindRequiredObjects()
        {
            if (browserArtwork == null)
            {
                Transform t = transform.Find(
                    "Primary/IE_BrowserArtwork"
                );

                if (t != null)
                    browserArtwork =
                        t.GetComponent<SpriteRenderer>();
            }

            if (thirdHistoryRow == null)
            {
                Transform t = transform.Find(
                    "Primary/IE_RowBackground_2"
                );

                if (t != null)
                    thirdHistoryRow =
                        t.GetComponent<SpriteRenderer>();
            }

            WindowManager manager = WindowManager.Instance;

            if (manager == null)
                return;

            if (windowExe == null)
            {
                WindowProcess process =
                    manager.GetProcess(windowExeId);

                if (process != null)
                    windowExe =
                        process.Instance as WindowExeWindow;
            }
        }

        // =====================================
        // 마스킹 오브젝트 생성
        // =====================================

        private void CreateRevealObjects()
        {
            if (revealRoot != null)
                return;

            revealRoot = new GameObject(
                "SecondaryReveal"
            );

            revealRoot.transform.SetParent(
                transform,
                false
            );

            secondaryRenderer =
                revealRoot.AddComponent<SpriteRenderer>();

            secondaryRenderer.sprite = secondaryImage;
            secondaryRenderer.color = Color.white;

            secondaryRenderer.drawMode =
                SpriteDrawMode.Simple;

            secondaryRenderer.sortingLayerName =
                Window.PrimarySortingLayerName;

            secondaryRenderer.sortingOrder = 25000;

            secondaryRenderer.maskInteraction =
                SpriteMaskInteraction.VisibleInsideMask;

            // SpriteMask용 흰색 스프라이트 생성
            maskTexture = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false
            );

            maskTexture.SetPixels(new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            });

            maskTexture.Apply();

            maskSprite = Sprite.Create(
                maskTexture,
                new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f),
                2f
            );

            // 마스크는 창의 자식이 아닌
            // 독립 오브젝트로 생성
            maskObject = new GameObject(
                "WINDOWexe_RevealMask"
            );

            revealMask =
                maskObject.AddComponent<SpriteMask>();

            revealMask.sprite = maskSprite;

            revealMask.isCustomRangeActive = true;

            int layerId = SortingLayer.NameToID(
                Window.PrimarySortingLayerName
            );

            revealMask.frontSortingLayerID = layerId;
            revealMask.backSortingLayerID = layerId;

            revealMask.backSortingOrder = 24999;
            revealMask.frontSortingOrder = 25001;

            SetRevealVisible(false);
        }

        // =====================================
        // 표시 가능 여부
        // =====================================

        private bool CanReveal()
        {
            if (internetWindow == null ||
                windowExe == null ||
                browserArtwork == null ||
                thirdHistoryRow == null ||
                secondaryImage == null)
            {
                return false;
            }

            if (!internetWindow.gameObject.activeInHierarchy ||
                !windowExe.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (onlyWhenDropdownOpen &&
                !internetWindow.DropdownOpen)
            {
                return false;
            }

            SpriteRenderer viewport =
                windowExe.ViewportRenderer;

            if (viewport == null ||
                !viewport.enabled ||
                !viewport.gameObject.activeInHierarchy)
            {
                return false;
            }

            // WINDOW.exe가 인터넷 창보다 앞에 있는지
            if (windowExe.transform.parent ==
                internetWindow.transform.parent)
            {
                if (windowExe.transform.GetSiblingIndex() <=
                    internetWindow.transform.GetSiblingIndex())
                {
                    return false;
                }
            }

            // 실제로 화면 영역이 겹치는지
            Bounds a = browserArtwork.bounds;
            Bounds b = viewport.bounds;

            bool overlaps =
                a.min.x < b.max.x &&
                a.max.x > b.min.x &&
                a.min.y < b.max.y &&
                a.max.y > b.min.y;

            return overlaps;
        }

        // =====================================
        // Secondary 이미지의 크기와 위치 맞추기
        // =====================================

        private void UpdateSecondaryImage()
        {
            if (secondaryRenderer == null ||
                browserArtwork == null ||
                secondaryImage == null)
            {
                return;
            }

            secondaryRenderer.sprite = secondaryImage;

            Bounds browserBounds = browserArtwork.bounds;

            Vector2 spriteSize =
                secondaryImage.bounds.size;

            if (spriteSize.x <= 0f ||
                spriteSize.y <= 0f)
            {
                return;
            }

            // Primary 브라우저 이미지와
            // Secondary 브라우저 이미지의
            // 화면상 크기를 동일하게 맞춤
            float scaleX =
                browserBounds.size.x / spriteSize.x;

            float scaleY =
                browserBounds.size.y / spriteSize.y;

            revealRoot.transform.position =
                new Vector3(
                    browserBounds.center.x,
                    browserBounds.center.y,
                    browserArtwork.transform.position.z - 0.02f
                );

            revealRoot.transform.rotation =
                browserArtwork.transform.rotation;

            // 부모 Window의 Scale 보정
            Vector3 parentScale =
                transform.lossyScale;

            float parentX =
                Mathf.Abs(parentScale.x) > 0.0001f
                    ? parentScale.x
                    : 1f;

            float parentY =
                Mathf.Abs(parentScale.y) > 0.0001f
                    ? parentScale.y
                    : 1f;

            revealRoot.transform.localScale =
                new Vector3(
                    scaleX / parentX,
                    scaleY / parentY,
                    1f
                );
        }

        // =====================================
        // WINDOW.exe 뷰포트를 마스크로 사용
        // =====================================

        private void UpdateMaskPosition()
        {
            if (windowExe == null ||
                revealMask == null ||
                maskSprite == null)
            {
                return;
            }

            SpriteRenderer viewport =
                windowExe.ViewportRenderer;

            if (viewport == null)
                return;

            Bounds bounds = viewport.bounds;

            // WINDOW.exe 창 내부의 실제 화면과
            // 마스크 위치를 일치시킨다.
            maskObject.transform.position =
                new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    viewport.transform.position.z
                );

            maskObject.transform.rotation =
                Quaternion.identity;

            Vector2 spriteSize = maskSprite.bounds.size;

            maskObject.transform.localScale =
                new Vector3(
                    bounds.size.x / spriteSize.x,
                    bounds.size.y / spriteSize.y,
                    1f
                );
        }

        // =====================================
        // 마스크 표시 / 숨기기
        // =====================================

        private void SetRevealVisible(bool visible)
        {
            revealed = visible;

            if (secondaryRenderer != null)
                secondaryRenderer.enabled = visible;

            if (revealMask != null)
                revealMask.enabled = visible;
        }

        // =====================================
        // 세 번째 검색기록 클릭
        // =====================================

        private void CheckGunClick()
        {
            if (DesktopInputRouter.IsWorldInputBlocked) return;
            if (!revealed)
                return;

            if (Mouse.current == null)
                return;

            if (!Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (windowExe.CurrentInteraction !=
                WindowInteraction.None)
            {
                return;
            }

            Camera cam = Camera.main;

            if (cam == null)
                return;

            Vector2 mouseScreen =
                Mouse.current.position.ReadValue();

            float distance =
                thirdHistoryRow.transform.position.z -
                cam.transform.position.z;

            if (distance <= 0f)
                return;

            Vector3 mouseWorld =
                cam.ScreenToWorldPoint(
                    new Vector3(
                        mouseScreen.x,
                        mouseScreen.y,
                        distance
                    )
                );

            Vector2 mousePoint = new Vector2(
                mouseWorld.x,
                mouseWorld.y
            );

            // 세 번째 검색기록을 클릭했는지
            Bounds rowBounds = thirdHistoryRow.bounds;

            Rect rowRect = Rect.MinMaxRect(
                rowBounds.min.x,
                rowBounds.min.y,
                rowBounds.max.x,
                rowBounds.max.y
            );

            if (!rowRect.Contains(mousePoint))
                return;

            // WINDOW.exe 뷰포트 안을 클릭했는지
            Bounds viewportBounds =
                windowExe.ViewportRenderer.bounds;

            Rect viewportRect = Rect.MinMaxRect(
                viewportBounds.min.x,
                viewportBounds.min.y,
                viewportBounds.max.x,
                viewportBounds.max.y
            );

            if (!viewportRect.Contains(mousePoint))
                return;

            OpenGunFile();
        }

        // =====================================
        // 총 파일 열기
        // =====================================

        private void OpenGunFile()
        {
            WindowManager manager =
                WindowManager.Instance;

            if (manager == null)
            {
                Debug.LogError(
                    "[InternetSecondaryReveal] " +
                    "WindowManager가 없습니다."
                );

                return;
            }

            Window opened = manager.Open(gunFileId);

            if (opened == null)
            {
                Debug.LogError(
                    "[InternetSecondaryReveal] " +
                    gunFileId +
                    " 프리팹이 등록되지 않았습니다."
                );

                return;
            }

            Debug.Log(
                "[InternetSecondaryReveal] " +
                "'총' 검색기록 클릭 → " +
                gunFileId
            );

            SetRevealVisible(false);
        }

        private void OnDisable()
        {
            SetRevealVisible(false);
        }

        private void OnDestroy()
        {
            if (maskObject != null)
                Destroy(maskObject);

            if (maskSprite != null)
                Destroy(maskSprite);

            if (maskTexture != null)
                Destroy(maskTexture);
        }
    }
}
