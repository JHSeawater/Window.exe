using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    public enum WindowState
    {
        Normal,
        Minimized,
        Maximized
    }

    /// <summary>
    /// 열린 창 프로세스 정보 (작업 표시줄 및 관리용 데이터)
    /// </summary>
    public class WindowProcess
    {
        public string Id { get; }
        public string Title => Instance != null ? Instance.WindowTitle : Id;
        public Window Instance { get; }
        public WindowState State { get; set; }

        public WindowProcess(string id, Window instance)
        {
            Id = id;
            Instance = instance;
            State = WindowState.Normal;
        }
    }

    [Serializable]
    public struct WindowPrefabEntry
    {
        public string id;
        public Window prefab;
    }

    /// <summary>
    /// 창 생성, 최소화, 보이기(Enshow), 닫기(Close), 프로세스 목록을 총괄하는 창 매니저
    /// </summary>
    [DisallowMultipleComponent]
    public class WindowManager : MonoBehaviour
    {
        public static WindowManager Instance { get; private set; }

        [Header("Registered Window Prefabs")]
        [SerializeField] private List<WindowPrefabEntry> registeredPrefabs = new List<WindowPrefabEntry>();

        [Header("Hierarchy Root")]
        [SerializeField] private Transform windowRoot;

        private readonly Dictionary<string, WindowProcess> _activeProcesses = new Dictionary<string, WindowProcess>();
        private readonly List<WindowProcess> _processListCache = new List<WindowProcess>();

        // 이벤트 (작업 표시줄 및 게임 시스템 연동용)
        public event Action<WindowProcess> OnProcessOpened;
        public event Action<string> OnProcessClosed;
        public event Action<WindowProcess> OnProcessStateChanged;
        public event Action<WindowProcess> OnProcessFocused;

        /// <summary>
        /// 현재 열려 있는 모든 창 프로세스 목록 (작업 표시줄에서 순회 접근 가능)
        /// </summary>
        public IReadOnlyList<WindowProcess> ActiveProcesses
        {
            get
            {
                _processListCache.Clear();
                _processListCache.AddRange(_activeProcesses.Values);
                return _processListCache;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (windowRoot == null) windowRoot = transform;
        }

        /// <summary>
        /// 창 열기: 이미 열려 있으면 맨 앞으로 포커스 및 복원(Enshow), 없으면 프리팹을 인스턴스화하여 생성
        /// </summary>
        public Window Open(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("[WindowManager] 열려는 창의 ID가 비어 있습니다!");
                return null;
            }

            // 1. 이미 열려 있는 창인 경우 -> 복원 및 포커스
            if (_activeProcesses.TryGetValue(id, out var existingProcess))
            {
                if (existingProcess.State == WindowState.Minimized)
                {
                    Enshow(id);
                }
                else
                {
                    FocusWindow(id);
                }
                return existingProcess.Instance;
            }

            // 2. 새 창 생성
            Window prefab = FindPrefab(id);
            if (prefab == null)
            {
                Debug.LogError($"[WindowManager] ID '{id}'에 해당하는 등록된 창 프리팹을 찾을 수 없습니다! 인스펙터의 Registered Prefabs를 확인하세요.", this);
                return null;
            }

            Transform root = windowRoot != null ? windowRoot : transform;
            Window newWindow = Instantiate(prefab, root);
            newWindow.WindowId = id;
            newWindow.gameObject.name = $"Window_{id}";

            var process = new WindowProcess(id, newWindow);
            _activeProcesses.Add(id, process);

            FocusWindow(id);
            OnProcessOpened?.Invoke(process);

            return newWindow;
        }

        /// <summary>
        /// 창 최소화: 창 GameObject를 비활성화(안 보이게)하고 상태를 Minimized로 변경
        /// </summary>
        public void Minimize(string id)
        {
            if (!_activeProcesses.TryGetValue(id, out var process)) return;
            if (process.Instance == null) return;

            process.Instance.gameObject.SetActive(false);
            process.State = WindowState.Minimized;

            OnProcessStateChanged?.Invoke(process);
        }

        /// <summary>
        /// 창 보이기(Enshow): 최소화된 창을 다시 화면에 활성화하고 맨 앞으로 포커스
        /// </summary>
        public void Enshow(string id)
        {
            if (!_activeProcesses.TryGetValue(id, out var process)) return;
            if (process.Instance == null) return;

            process.Instance.gameObject.SetActive(true);
            process.State = process.Instance.IsMaximized ? WindowState.Maximized : WindowState.Normal;

            FocusWindow(id);
            OnProcessStateChanged?.Invoke(process);
        }

        /// <summary>
        /// 창 닫기: 창 인스턴스를 파괴하고 프로세스 목록에서 제거
        /// </summary>
        public void Close(string id)
        {
            if (!_activeProcesses.TryGetValue(id, out var process)) return;

            _activeProcesses.Remove(id);

            if (process.Instance != null)
            {
                Destroy(process.Instance.gameObject);
            }

            OnProcessClosed?.Invoke(id);
        }

        /// <summary>
        /// 창 최대화 / 복원 토글
        /// </summary>
        public void ToggleMaximize(string id)
        {
            if (!_activeProcesses.TryGetValue(id, out var process)) return;
            if (process.Instance == null) return;

            process.Instance.ToggleMaximize();
            process.State = process.Instance.IsMaximized ? WindowState.Maximized : WindowState.Normal;

            FocusWindow(id);
            OnProcessStateChanged?.Invoke(process);
        }

        /// <summary>
        /// 창을 맨 앞으로 가져오기 (Sibling Index 최상단 배치 및 포커스)
        /// </summary>
        public void FocusWindow(string id)
        {
            if (!_activeProcesses.TryGetValue(id, out var process)) return;
            if (process.Instance == null) return;

            process.Instance.transform.SetAsLastSibling();
            OnProcessFocused?.Invoke(process);
        }

        /// <summary>
        /// 특정 ID의 창 프로세스 조회
        /// </summary>
        public WindowProcess GetProcess(string id)
        {
            _activeProcesses.TryGetValue(id, out var process);
            return process;
        }

        /// <summary>
        /// 특정 ID의 창이 열려 있는지 여부
        /// </summary>
        public bool IsWindowOpen(string id)
        {
            return _activeProcesses.ContainsKey(id);
        }

        private Window FindPrefab(string id)
        {
            for (int i = 0; i < registeredPrefabs.Count; i++)
            {
                if (registeredPrefabs[i].id == id)
                {
                    return registeredPrefabs[i].prefab;
                }
            }
            return null;
        }
    }
}

