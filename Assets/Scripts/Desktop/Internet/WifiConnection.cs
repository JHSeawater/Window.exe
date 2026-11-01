using System;
using UnityEngine;

namespace WindowExe.Desktop
{
    /// <summary>
    /// 게임 속 가상 Wi-Fi 상태. 실제 인터넷/운영체제 Wi-Fi에는 접근하지 않습니다.
    /// 씬에서 한 번만 생성하고, 퍼즐 코드에서는 SetConnected(true/false)를 호출하세요.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class WifiConnection : MonoBehaviour
    {
        public static WifiConnection Instance { get; private set; }

        private static bool globalIsConnected = false;

        /// <summary>
        /// 전역 Wi-Fi 연결 상태 (인스턴스가 있으면 인스턴스와 동기화)
        /// </summary>
        public static bool GlobalIsConnected
        {
            get
            {
                var inst = Instance != null ? Instance : FindAnyObjectByType<WifiConnection>();
                if (inst != null) return inst.isConnected;
                return globalIsConnected;
            }
            set
            {
                globalIsConnected = value;
                var inst = Instance != null ? Instance : FindAnyObjectByType<WifiConnection>();
                if (inst != null)
                {
                    inst.SetConnected(value);
                }
            }
        }

        /// <summary>
        /// Desktop 초기화 시 호출: 전역 및 현재 인스턴스의 Wi-Fi 연결 상태를 항상 false로 확실하게 초기화합니다.
        /// </summary>
        public static void ResetGlobalState()
        {
            globalIsConnected = false;
            var inst = Instance != null ? Instance : FindAnyObjectByType<WifiConnection>();
            if (inst != null)
            {
                inst.ResetState();
            }
        }

        public static void ResetInstance(WifiConnection connection = null)
        {
            Instance = connection;
        }

        [SerializeField] private bool isConnected = false;
        public bool IsConnected => isConnected;
        public event Action<bool> ConnectionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WifiConnection] 씬에 Wi-Fi 상태 컴포넌트가 둘 이상 있어 새 컴포넌트로 갱신합니다.", this);
            }
            Instance = this;

            // Desktop 초기화 시 Wi-Fi 연결 상태를 항상 false로 확실하게 리셋
            ResetState();
        }

        /// <summary>
        /// Wi-Fi 상태를 false로 재설정합니다.
        /// </summary>
        public void ResetState()
        {
            globalIsConnected = false;
            isConnected = false;
            ConnectionChanged?.Invoke(false);
        }

        public void SetConnected(bool connected)
        {
            globalIsConnected = connected;
            if (isConnected == connected) return;
            isConnected = connected;
            ConnectionChanged?.Invoke(isConnected);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
