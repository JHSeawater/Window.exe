
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WindowExe.Desktop
{
    /// <summary>
    /// WINDOW.exe 시작 화면 컨트롤러.
    ///
    /// 1. 시작 화면과 배경음악 재생
    /// 2. PRESS ANY KEY 텍스트 깜빡임
    /// 3. 키보드 / 마우스 입력 감지
    /// 4. BGM 정지
    /// 5. 줌인 영상 + 효과음 재생
    /// 6. 영상 종료 후 Desktop 씬으로 이동
    /// </summary>
    [AddComponentMenu("WINDOW.exe/Start Screen Controller")]
    public sealed class StartScreenController : MonoBehaviour
    {
        // =========================================
        // UI
        // =========================================

        [Header("UI References")]

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Image titleImage;

        [SerializeField]
        private TMP_Text pressAnyKeyText;

        [SerializeField]
        private Text legacyPressAnyKeyText;


        // =========================================
        // Scene
        // =========================================

        [Header("Scene Transition")]

        [SerializeField]
        private string nextSceneName = "Desktop";

        [SerializeField, Min(0f)]
        private float inputDelay = 0.3f;

        [SerializeField]
        private bool allowMouseClick = true;


        // =========================================
        // Blink
        // =========================================

        [Header("Blink Settings")]

        [SerializeField, Min(0.1f)]
        private float blinkFrequency = 2.0f;

        [SerializeField, Range(0f, 1f)]
        private float minAlpha = 0.15f;

        [SerializeField, Range(0f, 1f)]
        private float maxAlpha = 1.0f;

        [SerializeField]
        private bool useColorFade = false;

        [SerializeField]
        private Color brightColor = Color.white;

        [SerializeField]
        private Color darkColor =
            new Color(0.25f, 0.25f, 0.25f, 1f);


        // =========================================
        // Start Screen BGM
        // =========================================

        [Header("Start Screen BGM")]

        [SerializeField]
        private AudioSource bgmAudioSource;

        [SerializeField]
        private AudioClip startBgm;

        [SerializeField, Range(0f, 1f)]
        private float bgmVolume = 0.7f;


        // =========================================
        // Intro Video
        // =========================================

        [Header("Intro Video")]

        [Tooltip("줌인 영상 재생용 VideoPlayer")]
        [SerializeField]
        private VideoPlayer introVideoPlayer;

        [Tooltip("재생할 줌인 MP4 영상")]
        [SerializeField]
        private VideoClip introVideoClip;

        [Tooltip("영상이 표시될 RawImage")]
        [SerializeField]
        private RawImage introVideoScreen;

        [Tooltip("VideoPlayer와 RawImage에서 사용할 RenderTexture")]
        [SerializeField]
        private RenderTexture introRenderTexture;

        [Tooltip("영상 준비 최대 대기 시간")]
        [SerializeField, Min(1f)]
        private float videoPrepareTimeout = 15f;


        // =========================================
        // Zoom Sound
        // =========================================

        [Header("Zoom In Sound Effect")]

        [SerializeField]
        private AudioSource sfxAudioSource;

        [SerializeField]
        private AudioClip zoomInSfx;

        [SerializeField, Range(0f, 1f)]
        private float sfxVolume = 1f;


        // =========================================
        // Events
        // =========================================

        [Header("Events")]

        [SerializeField]
        private UnityEvent onStartRequested = new UnityEvent();


        // =========================================
        // Private Variables
        // =========================================

        private bool isTransitioning;
        private bool sceneLoadStarted;
        private bool videoFailed;

        private float elapsedTime;

        private Color baseTextColor = Color.white;

        private Coroutine introCoroutine;


        // =========================================
        // Public Properties
        // =========================================

        public event Action OnStartTriggered;

        public Image BackgroundImage => backgroundImage;

        public Image TitleImage => titleImage;

        public TMP_Text PressAnyKeyText => pressAnyKeyText;

        public Text LegacyPressAnyKeyText =>
            legacyPressAnyKeyText;

        public string NextSceneName => nextSceneName;

        public bool IsTransitioning => isTransitioning;

        public float InputDelay => inputDelay;

        public float BlinkFrequency => blinkFrequency;


        // =========================================
        // Configure
        // =========================================

        public void Configure(
            Image background,
            Image title,
            TMP_Text text,
            string scene = "Desktop")
        {
            backgroundImage = background;
            titleImage = title;
            pressAnyKeyText = text;
            nextSceneName = scene;
        }


        // =========================================
        // Awake
        // =========================================

        private void Awake()
        {
            Time.timeScale = 1f;

            // 기존 폰트 설정 유지
            if (pressAnyKeyText != null)
            {
                GameTypography.Apply(pressAnyKeyText);
                baseTextColor = pressAnyKeyText.color;
            }
            else if (legacyPressAnyKeyText != null)
            {
                GameTypography.Apply(legacyPressAnyKeyText);
                baseTextColor = legacyPressAnyKeyText.color;
            }

            SetupAudio();
            SetupVideo();
        }


        // =========================================
        // Start
        // =========================================

        private void Start()
        {
            // 시작 화면 배경음악 재생
            if (bgmAudioSource != null &&
                startBgm != null)
            {
                bgmAudioSource.Play();
            }
        }


        // =========================================
        // Update
        // =========================================

        private void Update()
        {
            elapsedTime += Time.unscaledDeltaTime;

            if (isTransitioning)
                return;

            // PRESS ANY KEY 깜빡임
            UpdateBlink(elapsedTime);

            // 입력 대기
            if (elapsedTime < inputDelay)
                return;

            if (CheckAnyKeyPressed())
            {
                TriggerStart();
            }
        }


        // =========================================
        // Audio Setup
        // =========================================

        private void SetupAudio()
        {
            if (bgmAudioSource != null)
            {
                bgmAudioSource.playOnAwake = false;

                bgmAudioSource.loop = true;

                bgmAudioSource.clip = startBgm;

                bgmAudioSource.volume = bgmVolume;

                bgmAudioSource.spatialBlend = 0f;
            }

            if (sfxAudioSource != null)
            {
                sfxAudioSource.playOnAwake = false;

                sfxAudioSource.loop = false;

                sfxAudioSource.volume = sfxVolume;

                sfxAudioSource.spatialBlend = 0f;
            }
        }


        // =========================================
        // Video Setup
        // =========================================

        private void SetupVideo()
        {
            if (introVideoScreen != null)
            {
                introVideoScreen.enabled = false;

                introVideoScreen.raycastTarget = true;
                introVideoScreen.color = Color.white;

                // UI에서 가장 앞에 표시
                introVideoScreen.transform.SetAsLastSibling();
            }

            if (introVideoPlayer == null)
                return;

            introVideoPlayer.playOnAwake = false;

            introVideoPlayer.isLooping = false;

            introVideoPlayer.waitForFirstFrame = true;

            introVideoPlayer.source = VideoSource.VideoClip;

            introVideoPlayer.clip = introVideoClip;

            // 영상을 RenderTexture로 출력
            introVideoPlayer.renderMode =
                VideoRenderMode.RenderTexture;

            introVideoPlayer.targetTexture =
                introRenderTexture;

            // 영상 자체 오디오는 사용하지 않음
            // 별도의 효과음 AudioSource를 사용
            introVideoPlayer.audioOutputMode =
                VideoAudioOutputMode.None;

            introVideoPlayer.loopPointReached +=
                OnVideoFinished;

            introVideoPlayer.errorReceived +=
                OnVideoError;
        }


        // =========================================
        // Blink Animation
        // =========================================

        public float CalculateBlinkT(float time)
        {
            return (
                Mathf.Sin(
                    time * Mathf.PI * blinkFrequency
                ) + 1f
            ) * 0.5f;
        }


        public Color EvaluateTextColor(float t)
        {
            if (useColorFade)
            {
                return Color.Lerp(
                    darkColor,
                    brightColor,
                    t
                );
            }

            Color color = baseTextColor;

            color.a = Mathf.Lerp(
                minAlpha,
                maxAlpha,
                t
            );

            return color;
        }


        private void UpdateBlink(float time)
        {
            float t = CalculateBlinkT(time);

            Color color = EvaluateTextColor(t);

            if (pressAnyKeyText != null)
            {
                pressAnyKeyText.color = color;
            }

            if (legacyPressAnyKeyText != null)
            {
                legacyPressAnyKeyText.color = color;
            }
        }


        // =========================================
        // Input Detection
        // =========================================

        public bool CheckAnyKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null &&
                keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            if (allowMouseClick)
            {
                Mouse mouse = Mouse.current;

                if (mouse != null)
                {
                    if (
                        mouse.leftButton.wasPressedThisFrame ||
                        mouse.rightButton.wasPressedThisFrame ||
                        mouse.middleButton.wasPressedThisFrame
                    )
                    {
                        return true;
                    }
                }

                Pointer pointer = Pointer.current;

                if (pointer != null &&
                    pointer.press.wasPressedThisFrame)
                {
                    return true;
                }
            }

            Gamepad gamepad = Gamepad.current;

            if (gamepad != null)
            {
                foreach (var control in gamepad.allControls)
                {
                    if (
                        control is
                        UnityEngine.InputSystem.Controls.ButtonControl
                        button &&
                        button.wasPressedThisFrame
                    )
                    {
                        return true;
                    }
                }
            }

#endif

#if ENABLE_LEGACY_INPUT_MANAGER

            if (Input.anyKeyDown)
            {
                return true;
            }

            if (allowMouseClick)
            {
                if (
                    Input.GetMouseButtonDown(0) ||
                    Input.GetMouseButtonDown(1)
                )
                {
                    return true;
                }
            }

#endif

            return false;
        }


        // =========================================
        // Start Trigger
        // =========================================

        public void TriggerStart()
        {
            if (isTransitioning)
                return;

            // 필수 영상 연결 확인
            bool hasIntro = introVideoPlayer != null && introVideoClip != null &&
                introVideoScreen != null && introRenderTexture != null;

            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.LogError(
                    "[StartScreenController] " +
                    "Next Scene Name이 비어 있습니다.",
                    this
                );

                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(
                    nextSceneName))
            {
                Debug.LogError(
                    "[StartScreenController] '" +
                    nextSceneName +
                    "' 씬을 로드할 수 없습니다. " +
                    "Build Profiles의 Scene List를 확인하세요.",
                    this
                );

                return;
            }

            isTransitioning = true;
            videoFailed = false;

            // 기존 이벤트 유지
            OnStartTriggered?.Invoke();

            onStartRequested?.Invoke();

            // 시작 화면 BGM 정지
            if (bgmAudioSource != null)
            {
                bgmAudioSource.Stop();
            }

            // 추가 입력 차단
            if (pressAnyKeyText != null)
            {
                pressAnyKeyText.enabled = false;
            }

            if (legacyPressAnyKeyText != null)
            {
                legacyPressAnyKeyText.enabled = false;
            }

            // 영상 시작
            if (!hasIntro)
            {
                Debug.LogWarning("[StartScreenController] Intro assets unavailable; loading " + nextSceneName + ".", this);
                LoadNextScene();
                return;
            }

            introCoroutine =
                StartCoroutine(PlayIntroSequence());
        }


        // =========================================
        // Intro Sequence
        // =========================================

        private IEnumerator PlayIntroSequence()
        {
            introVideoPlayer.Stop();

            introVideoPlayer.clip = introVideoClip;

            introVideoPlayer.targetTexture =
                introRenderTexture;

            // 이전 프레임이 남아 있지 않도록 초기화
            RenderTexture previous = RenderTexture.active;

            RenderTexture.active = introRenderTexture;

            GL.Clear(
                true,
                true,
                Color.black
            );

            RenderTexture.active = previous;

            // RawImage에 영상 연결
            introVideoScreen.texture =
                introRenderTexture;

            // 영상 준비
            introVideoPlayer.Prepare();

            float prepareElapsed = 0f;

            while (!introVideoPlayer.isPrepared)
            {
                if (videoFailed)
                {
                    introCoroutine = null;
                    yield break;
                }

                prepareElapsed +=
                    Time.unscaledDeltaTime;

                if (prepareElapsed >= videoPrepareTimeout)
                {
                    Debug.LogError(
                        "[StartScreenController] " +
                        "영상 준비 시간이 초과되었습니다.",
                        this
                    );

                    RestoreStartScreen();

                    introCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            // 영상 화면 표시
            introVideoScreen.enabled = true;
            introVideoScreen.transform.SetAsLastSibling();

            // 줌인 영상 재생
            introVideoPlayer.Play();

            // 영상과 함께 효과음 재생
            if (
                sfxAudioSource != null &&
                zoomInSfx != null
            )
            {
                sfxAudioSource.PlayOneShot(
                    zoomInSfx
                );
            }

            introCoroutine = null;
        }


        // =========================================
        // Video Finished
        // =========================================

        private void OnVideoFinished(
            VideoPlayer player)
        {
            LoadNextScene();
        }

        private void LoadNextScene()
        {
            if (!isTransitioning ||
                sceneLoadStarted)
            {
                return;
            }

            sceneLoadStarted = true;

            Debug.Log(
                "[StartScreenController] " +
                "인트로 영상 종료 → " +
                nextSceneName +
                " 씬 이동"
            );

            // 영상이 끝나면 자동으로 게임 시작
            SceneManager.LoadScene(nextSceneName);
        }


        // =========================================
        // Video Error
        // =========================================

        private void OnVideoError(
            VideoPlayer player,
            string message)
        {
            Debug.LogError(
                "[StartScreenController] " +
                "영상 재생 오류: " +
                message,
                this
            );

            videoFailed = true;

            RestoreStartScreen();
        }


        // =========================================
        // Restore Start Screen
        // =========================================

        private void RestoreStartScreen()
        {
            if (introVideoPlayer != null)
            {
                introVideoPlayer.Stop();
            }

            if (sfxAudioSource != null)
            {
                sfxAudioSource.Stop();
            }

            if (introVideoScreen != null)
            {
                introVideoScreen.enabled = false;
            }

            if (pressAnyKeyText != null)
            {
                pressAnyKeyText.enabled = true;
            }

            if (legacyPressAnyKeyText != null)
            {
                legacyPressAnyKeyText.enabled = true;
            }

            if (
                bgmAudioSource != null &&
                startBgm != null
            )
            {
                bgmAudioSource.Play();
            }

            isTransitioning = false;
            sceneLoadStarted = false;
        }


        // =========================================
        // Cleanup
        // =========================================

        private void OnDestroy()
        {
            if (introVideoPlayer != null)
            {
                introVideoPlayer.loopPointReached -=
                    OnVideoFinished;

                introVideoPlayer.errorReceived -=
                    OnVideoError;
            }
        }
    }
}
