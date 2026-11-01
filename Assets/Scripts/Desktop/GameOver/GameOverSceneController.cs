using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop
{
    /// <summary>
    /// 게임오버(YouAreDie) 씬에서 사용자의 임의의 키/마우스 입력을 감지하여
    /// 데스크톱(Desktop) 씬을 처음부터 다시 시작하는 컨트롤러입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameOverSceneController : MonoBehaviour
    {
        [Header("Scene Settings")]
        [Tooltip("다시 시작할 씬 이름 (기본값: Desktop)")]
        [SerializeField] private string restartSceneName = "Desktop";

        [Tooltip("씬 로드 직후 실수로 바로 넘어가지 않도록 하는 최소 대기 시간(초)")]
        [SerializeField] private float inputDelay = 0.25f;

        private float elapsedTimer;
        private bool isRestarting;

        public string RestartSceneName
        {
            get => restartSceneName;
            set => restartSceneName = value;
        }

        public float InputDelay
        {
            get => inputDelay;
            set => inputDelay = value;
        }

        public bool IsRestarting => isRestarting;

        private void Awake()
        {
            elapsedTimer = 0f;
            isRestarting = false;
        }

        private void Update()
        {
            if (isRestarting) return;

            elapsedTimer += Time.unscaledDeltaTime;
            if (elapsedTimer < inputDelay) return;

            if (CheckAnyInputPressed())
            {
                RestartDesktopScene();
            }
        }

        /// <summary>
        /// 키보드 또는 마우스 등 임의의 입력이 발생했는지 검사합니다.
        /// </summary>
        public bool CheckAnyInputPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame ||
                    Mouse.current.rightButton.wasPressedThisFrame ||
                    Mouse.current.middleButton.wasPressedThisFrame)
                {
                    return true;
                }
            }

            try
            {
                if (Input.anyKeyDown)
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // New Input System 전용 모드일 때 Input.anyKeyDown 접근 예외 방어
            }

            return false;
        }

        /// <summary>
        /// 전역 데스크톱 상태를 초기화하고 Desktop 씬을 다시 로드합니다.
        /// </summary>
        public void RestartDesktopScene()
        {
            if (isRestarting) return;
            isRestarting = true;

            // 데스크톱 전역 상태 및 시간 배율 리셋
            ImageWindow.ResetGlobalExtractionState();
            WifiConnection.ResetGlobalState();
            Time.timeScale = 1f;

            Debug.Log($"[GameOverSceneController] 아무 키 입력이 감지되어 '{restartSceneName}' 씬을 다시 시작합니다.");
            if (Application.isPlaying && !string.IsNullOrEmpty(restartSceneName))
            {
                SceneManager.LoadScene(restartSceneName);
            }
        }
    }
}

