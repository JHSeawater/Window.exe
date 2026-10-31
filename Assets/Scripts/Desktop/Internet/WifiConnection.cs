using System;
using UnityEngine;

namespace WindowExe.Desktop
{
    /// <summary>
    /// 게임 속 가상 Wi-Fi 상태. 실제 인터넷/운영체제 Wi-Fi에는 접근하지 않습니다.
    /// 씬에서 한 번만 생성하고, 퍼즐 코드에서는 SetConnected(true/false)를 호출하세요.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WifiConnection : MonoBehaviour
    {
        public static WifiConnection Instance { get; private set; }

        [SerializeField] private bool isConnected = true;
        public bool IsConnected => isConnected;
        public event Action<bool> ConnectionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WifiConnection] 씬에 Wi-Fi 상태 컴포넌트가 둘 이상 있습니다.", this);
                return;
            }
            Instance = this;
        }

        public void SetConnected(bool connected)
        {
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
