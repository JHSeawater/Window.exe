using System;
using UnityEngine;
using UnityEngine.Events;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop
{
    /// <summary>
    /// 데스크톱 씬 메인 퀘스트 진행 디렉터:
    /// 1. 시작 시 초기 아이콘 4개만 노출(image.png, key.txt는 비활성화)하고 memo.txt 창을 자동 오픈합니다.
    /// 2. WINDOW.exe와 memo.txt 창이 겹치는 즉시 image.png 아이콘을 활성화합니다.
    /// 3. 하노이 탑 퍼즐 해결로 Wi-Fi가 연결되면 key.txt 아이콘을 활성화합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class DesktopQuestDirector : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private DesktopController desktop;
        [SerializeField] private WindowManager windowManager;
        [SerializeField] private WifiConnection wifiConnection;

        [Header("Quest Desktop Icons (숨김 대상 오브젝트)")]
        [Tooltip("HELP ME를 확인한 뒤 바탕화면에 나타날 image.png 아이콘 (초기 비활성)")]
        [SerializeField] private DesktopIconView imageIcon;
        [Tooltip("Wi-Fi 연결 후 바탕화면에 나타날 key.txt 아이콘 (초기 비활성)")]
        [SerializeField] private DesktopIconView keyFileIcon;
        [SerializeField] private string imageIconItemId = "peepWindow";
        [SerializeField] private string keyFileIconItemId = "key-file";

        [Header("Window IDs")]
        [SerializeField] private string memoWindowId = "memo";
        [SerializeField] private string windowExeId = "window";

        [Header("Quest Settings")]
        [Tooltip("씬 시작 시 memo.txt 창을 자동으로 열지 여부")]
        [SerializeField] private bool openMemoOnStart = true;
        [Tooltip("씬 시작 시 image.png와 key.txt 아이콘을 비활성화 상태로 시작할지 여부")]
        [SerializeField] private bool manageInitialIconVisibility = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onHelpMeSeen;
        [SerializeField] private UnityEvent onKeyFileRevealed;

        private bool isHelpMeSeen;
        private bool isKeyFileRevealed;

        public event Action OnHelpMeSeen;
        public event Action OnKeyFileRevealed;

        public bool IsHelpMeSeen => isHelpMeSeen;
        public bool IsKeyFileRevealed => isKeyFileRevealed;

        public DesktopIconView ImageIcon
        {
            get => imageIcon;
            set => imageIcon = value;
        }

        public DesktopIconView KeyFileIcon
        {
            get => keyFileIcon;
            set => keyFileIcon = value;
        }

        public bool OpenMemoOnStart
        {
            get => openMemoOnStart;
            set => openMemoOnStart = value;
        }

        public void Configure(
            DesktopController desktopController,
            WindowManager manager,
            WifiConnection wifi,
            DesktopIconView targetImageIcon = null,
            DesktopIconView targetKeyIcon = null)
        {
            UnsubscribeEvents();

            desktop = desktopController;
            windowManager = manager;
            wifiConnection = wifi;
            if (targetImageIcon != null) imageIcon = targetImageIcon;
            if (targetKeyIcon != null) keyFileIcon = targetKeyIcon;

            WifiConnection.ResetGlobalState();
            ResolveReferences();
            if (isActiveAndEnabled)
            {
                SubscribeEvents();
            }
        }

        private void Awake()
        {
            WifiConnection.ResetGlobalState();
            ResolveReferences();
            ApplyInitialIconVisibility();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeEvents();
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeEvents();

            if (openMemoOnStart && windowManager != null && !windowManager.IsWindowOpen(memoWindowId))
            {
                windowManager.Open(memoWindowId);
            }

            CheckWifiConnectionState();
            CheckHelpMeOverlap();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        /// <summary>
        /// 프레임 진행 및 단위 테스트용 수동 틱.
        /// </summary>
        public void ManualTick(float deltaTime)
        {
            ResolveReferencesIfMissing();

            if (!isHelpMeSeen)
            {
                CheckHelpMeOverlap();
            }

            if (!isKeyFileRevealed)
            {
                CheckWifiConnectionState();
            }
        }

        public void ApplyInitialIconVisibility()
        {
            if (!manageInitialIconVisibility) return;

            if (!isHelpMeSeen && imageIcon != null && imageIcon.gameObject.activeSelf)
            {
                imageIcon.gameObject.SetActive(false);
            }

            if (!isKeyFileRevealed && keyFileIcon != null && keyFileIcon.gameObject.activeSelf)
            {
                keyFileIcon.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// WINDOW.exe와 memo.txt 창이 화면상에서 겹쳐졌는지 즉시 판정합니다.
        /// </summary>
        public bool CheckHelpMeOverlap()
        {
            if (isHelpMeSeen) return true;

            var wm = windowManager != null ? windowManager : WindowManager.Instance;
            if (wm == null) return false;

            var memoProc = wm.GetProcess(memoWindowId);
            var windowExeProc = wm.GetProcess(windowExeId);

            if (!IsProcessVisible(memoProc) || !IsProcessVisible(windowExeProc))
            {
                return false;
            }

            Rect memoBounds = memoProc.Instance.OuterBounds;
            Rect windowExeBounds = windowExeProc.Instance.OuterBounds;

            if (memoBounds.Overlaps(windowExeBounds))
            {
                TriggerHelpMeSeen();
                return true;
            }

            return false;
        }

        public void TriggerHelpMeSeen()
        {
            if (isHelpMeSeen) return;
            isHelpMeSeen = true;

            RevealDesktopIcon(imageIcon);

            onHelpMeSeen?.Invoke();
            OnHelpMeSeen?.Invoke();
        }

        public void CheckWifiConnectionState()
        {
            if (isKeyFileRevealed) return;

            var wifi = wifiConnection != null ? wifiConnection : WifiConnection.Instance;
            if (wifi != null && wifi.IsConnected)
            {
                TriggerKeyFileReveal();
            }
        }

        public void TriggerKeyFileReveal()
        {
            if (isKeyFileRevealed) return;
            isKeyFileRevealed = true;

            RevealDesktopIcon(keyFileIcon);

            onKeyFileRevealed?.Invoke();
            OnKeyFileRevealed?.Invoke();
        }

        private void RevealDesktopIcon(DesktopIconView icon)
        {
            if (icon == null) return;

            icon.gameObject.SetActive(true);

            if (desktop != null && desktop.IconArea != null)
            {
                Canvas.ForceUpdateCanvases();
                if (!desktop.CanPlace(icon))
                {
                    PlaceIconInNextAvailableGridSlot(icon);
                }
                desktop.EnsureValidLayout();
            }
        }

        private void PlaceIconInNextAvailableGridSlot(DesktopIconView targetIcon)
        {
            if (desktop == null || desktop.IconArea == null || targetIcon == null) return;

            RectTransform iconArea = desktop.IconArea;
            const float padding = 8f;
            const float gap = 12f;

            Vector2 cell = Vector2.zero;
            foreach (var icon in desktop.Icons)
            {
                if (icon != null && icon.gameObject.activeInHierarchy)
                {
                    cell = Vector2.Max(cell, DesktopLayout.BoundsInParent(icon.Rect, iconArea).size);
                }
            }

            if (cell.x <= 0f || cell.y <= 0f) return;

            int rows = Mathf.Max(1, Mathf.FloorToInt((iconArea.rect.height - padding * 2f + gap) / (cell.y + gap)));
            int maxSlots = Mathf.Max(desktop.Icons.Count + 4, rows * 6);

            Vector3 originalPos = targetIcon.Rect.localPosition;
            for (int index = 0; index < maxSlots; index++)
            {
                Rect bounds = DesktopLayout.BoundsInParent(targetIcon.Rect, iconArea);
                Vector2 topLeft = new Vector2(
                    iconArea.rect.xMin + padding + (index / rows) * (cell.x + gap),
                    iconArea.rect.yMax - padding - (index % rows) * (cell.y + gap));
                Vector2 offset = topLeft - new Vector2(bounds.xMin, bounds.yMax);
                targetIcon.Rect.localPosition += new Vector3(offset.x, offset.y, 0f);

                if (desktop.CanPlace(targetIcon))
                {
                    return;
                }
            }

            targetIcon.Rect.localPosition = originalPos;
        }

        private static bool IsProcessVisible(WindowProcess process)
        {
            return process != null
                && process.State != WindowState.Minimized
                && process.Instance != null
                && process.Instance.gameObject.activeInHierarchy;
        }

        private void HandleWifiConnectionChanged(bool connected)
        {
            if (connected)
            {
                TriggerKeyFileReveal();
            }
        }

        private void SubscribeEvents()
        {
            if (wifiConnection != null)
            {
                wifiConnection.ConnectionChanged -= HandleWifiConnectionChanged;
                wifiConnection.ConnectionChanged += HandleWifiConnectionChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (wifiConnection != null)
            {
                wifiConnection.ConnectionChanged -= HandleWifiConnectionChanged;
            }
        }

        private void ResolveReferencesIfMissing()
        {
            if (desktop == null || windowManager == null || wifiConnection == null || imageIcon == null || keyFileIcon == null)
            {
                ResolveReferences();
            }
        }

        private void ResolveReferences()
        {
            if (desktop == null)
            {
                desktop = FindAnyObjectByType<DesktopController>();
            }

            if (windowManager == null)
            {
                windowManager = WindowManager.Instance != null
                    ? WindowManager.Instance
                    : FindAnyObjectByType<WindowManager>();
            }

            if (wifiConnection == null)
            {
                wifiConnection = WifiConnection.Instance != null
                    ? WifiConnection.Instance
                    : FindAnyObjectByType<WifiConnection>();
            }

            if (desktop != null)
            {
                var icons = desktop.Icons;
                for (int i = 0; i < icons.Count; i++)
                {
                    var icon = icons[i];
                    if (icon == null) continue;

                    string itemId = icon.Item != null ? icon.Item.ItemId : string.Empty;
                    if (imageIcon == null && (
                        string.Equals(itemId, imageIconItemId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemId, "peep-window", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(icon.gameObject.name, "Icon-peep", StringComparison.OrdinalIgnoreCase)))
                    {
                        imageIcon = icon;
                    }

                    if (keyFileIcon == null && (
                        icon.GetComponent<KeyFileMechanic>() != null ||
                        string.Equals(itemId, keyFileIconItemId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemId, "key.txt", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(icon.gameObject.name, "KeyFileIcon", StringComparison.OrdinalIgnoreCase)))
                    {
                        keyFileIcon = icon;
                    }
                }
            }
        }
    }
}
