using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop.Desktop.Window
{
    public class ExtractedKeyEntity : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer frontRenderer;
        [SerializeField] private SpriteRenderer backRenderer;
        [SerializeField] private bool requireWindowExeToDrag = false;
        [SerializeField] private bool preventDraggingOutsideWindowExe = false;

        public bool RequireWindowExeToDrag { get => requireWindowExeToDrag; set => requireWindowExeToDrag = value; }
        public bool PreventDraggingOutsideWindowExe { get => preventDraggingOutsideWindowExe; set => preventDraggingOutsideWindowExe = value; }
        public bool IsDragging { get; private set; }
        public bool IsConsumed { get; private set; }
        public Rect? OverrideRecycleBinWorldRect { get; set; }

        public event Action OnUsedOnRepairWindow;
        [System.Obsolete("Use OnUsedOnRepairWindow instead")]
        public event Action OnDroppedToRecycleBin;

        public void Initialize(Sprite sprite, Vector3 scale, bool requireWindowExe, Rect? overrideRecycleBinRect = null, bool preventOutside = false)
        {
            requireWindowExeToDrag = requireWindowExe;
            preventDraggingOutsideWindowExe = preventOutside;
            OverrideRecycleBinWorldRect = overrideRecycleBinRect;

            if (frontRenderer == null)
            {
                var fGo = new GameObject("Front");
                fGo.transform.SetParent(transform, false);
                frontRenderer = fGo.AddComponent<SpriteRenderer>();
            }
            frontRenderer.sprite = sprite;
            frontRenderer.sortingLayerName = Window.PrimarySortingLayerName;
            frontRenderer.sortingOrder = 32000;
            frontRenderer.transform.localScale = scale;

            if (backRenderer == null)
            {
                var bGo = new GameObject("Back");
                bGo.transform.SetParent(transform, false);
                backRenderer = bGo.AddComponent<SpriteRenderer>();
            }
            backRenderer.sprite = sprite;
            backRenderer.sortingLayerName = Window.SecondarySortingLayerName;
            backRenderer.sortingOrder = 32000;
            backRenderer.transform.localScale = scale;
        }

        public Rect GetKeyWorldRect()
        {
            if (frontRenderer != null && frontRenderer.sprite != null)
            {
                var b = frontRenderer.bounds;
                if (b.size.x > 0.001f && b.size.y > 0.001f)
                    return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
            }
            return new Rect(transform.position.x - 0.5f, transform.position.y - 0.5f, 1f, 1f);
        }

        public bool BeginDrag(Vector2 worldPos)
        {
            if (IsConsumed) return false;
            Rect keyRect = GetKeyWorldRect();
            if (!keyRect.Contains(worldPos)) return false;

            if (requireWindowExeToDrag && !ImageWindow.IsKeyObjectInsideWindowExe(transform.position, worldPos))
                return false;

            IsDragging = true;
            transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
            return true;
        }

        public void UpdateDrag(Vector2 worldPos)
        {
            if (!IsDragging) return;
            Vector2 target = worldPos;
            if (preventDraggingOutsideWindowExe && ImageWindow.TryGetWindowExeBounds(out Rect exeBounds))
            {
                float margin = 0.25f;
                target.x = Mathf.Clamp(target.x, exeBounds.xMin + margin, exeBounds.xMax - margin);
                target.y = Mathf.Clamp(target.y, exeBounds.yMin + margin, exeBounds.yMax - margin);
            }
            transform.position = new Vector3(target.x, target.y, transform.position.z);
        }

        public bool EndDrag(Vector2 worldPos, Vector2? screenPos = null)
        {
            if (!IsDragging) return false;
            UpdateDrag(worldPos);
            IsDragging = false;

            RepairWindow repairWindow = RepairWindow.FindRepairWindowAt(worldPos);
            if (repairWindow != null && repairWindow.TryUseKey())
            {
                if (repairWindow.ConsumeKeyOnUse)
                {
                    ConsumeKey();
                }
                else
                {
                    // 열쇠 미소모: 놓인 위치에 그대로 유지
                    transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
                    OnUsedOnRepairWindow?.Invoke();
                    ImageWindow.NotifyKeyUsedOnRepairWindow(this);
                }
                return true;
            }

            return false;
        }

        public void ConsumeKey()
        {
            if (IsConsumed) return;
            IsConsumed = true;
            Debug.Log("[KeyToRepair] 열쇠 이미지를 복원 창에 사용했습니다! (독립 엔티티 소모)", this);
            OnUsedOnRepairWindow?.Invoke();
            ImageWindow.NotifyKeyUsedOnRepairWindow(this);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        public bool IsPointOverRecycleBin(Vector2 worldPoint, Vector2? screenPoint = null)
        {
            if (OverrideRecycleBinWorldRect.HasValue)
            {
                return OverrideRecycleBinWorldRect.Value.Contains(worldPoint);
            }

            // 1. 바탕화면 휴지통 아이콘 탐색
            var icons = FindObjectsByType<DesktopIconView>(FindObjectsSortMode.None);
            Vector3[] corners = new Vector3[4];
            foreach (var icon in icons)
            {
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;

                bool isRecycleIcon =
                    (icon.Item != null && string.Equals(icon.Item.ItemId, "recycle-bin", StringComparison.OrdinalIgnoreCase)) ||
                    icon.name.IndexOf("recycle-bin", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isRecycleIcon) continue;

                var rectTransform = icon.Rect;
                if (rectTransform != null)
                {
                    rectTransform.GetWorldCorners(corners);
                    Rect worldRect = Rect.MinMaxRect(
                        Mathf.Min(corners[0].x, corners[2].x),
                        Mathf.Min(corners[0].y, corners[2].y),
                        Mathf.Max(corners[0].x, corners[2].x),
                        Mathf.Max(corners[0].y, corners[2].y)
                    );
                    if (worldRect.width > 0.0001f && worldRect.height > 0.0001f && worldRect.Contains(worldPoint))
                        return true;

                    if (screenPoint.HasValue)
                    {
                        var canvas = icon.GetComponentInParent<Canvas>();
                        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                            ? canvas.worldCamera
                            : null;
                        if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint.Value, cam))
                            return true;
                    }
                }
            }

            // 2. 열려 있는 휴지통 창("recycle-bin") 탐색
            var wm = WindowManager.Instance;
            if (wm != null)
            {
                var recycleProc = wm.GetProcess("recycle-bin");
                if (recycleProc != null &&
                    recycleProc.State != WindowState.Minimized &&
                    recycleProc.Instance != null &&
                    recycleProc.Instance.gameObject.activeInHierarchy &&
                    recycleProc.Instance.OuterBounds.Contains(worldPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            if (DesktopInputRouter.IsWorldInputBlocked) return;
            if (IsConsumed || Mouse.current == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            if (!TryGetMouseWorld(screenPos, out Vector2 worldPos))
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame && !IsDragging)
            {
                BeginDrag(worldPos);
            }
            else if (IsDragging && Mouse.current.leftButton.isPressed)
            {
                UpdateDrag(worldPos);
            }
            else if (IsDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndDrag(worldPos, screenPos);
            }
        }

        private bool TryGetMouseWorld(Vector2 screenPos, out Vector2 worldPos)
        {
            if (DesktopWindowSpace.Instance != null &&
                DesktopWindowSpace.Instance.Valid &&
                DesktopWindowSpace.Instance.TryWorld(screenPos, out worldPos))
            {
                return true;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                worldPos = new Vector2(w.x, w.y);
                return true;
            }

            worldPos = Vector2.zero;
            return false;
        }
    }
}

