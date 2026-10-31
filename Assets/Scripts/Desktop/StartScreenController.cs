using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WindowExe.Desktop
{
    /// <summary>
    /// Handles the start screen title presentation, blinking "Press Any Key" text,
    /// and transitioning to the Desktop scene upon receiving any key/pointer press.
    /// </summary>
    [AddComponentMenu("WINDOW.exe/Start Screen Controller")]
    public sealed class StartScreenController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Background sprite image (assigned manually in inspector)")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Title logo sprite image (assigned manually in inspector)")]
        [SerializeField] private Image titleImage;

        [Tooltip("Press Any Key TextMeshPro text (assigned manually in inspector)")]
        [SerializeField] private TMP_Text pressAnyKeyText;

        [Tooltip("Fallback UI Text if TMP is not used")]
        [SerializeField] private Text legacyPressAnyKeyText;

        [Header("Scene Transition")]
        [SerializeField] private string nextSceneName = "Desktop";
        [SerializeField, Min(0f)] private float inputDelay = 0.3f;
        [SerializeField] private bool allowMouseClick = true;

        [Header("Blink Settings")]
        [SerializeField, Min(0.1f)] private float blinkFrequency = 2.0f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.15f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 1.0f;
        [SerializeField] private bool useColorFade = false;
        [SerializeField] private Color brightColor = Color.white;
        [SerializeField] private Color darkColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        [Header("Events")]
        [SerializeField] private UnityEvent onStartRequested = new UnityEvent();

        private bool isTransitioning;
        private float elapsedTime;
        private Color baseTextColor = Color.white;

        public event Action OnStartTriggered;

        public Image BackgroundImage => backgroundImage;
        public Image TitleImage => titleImage;
        public TMP_Text PressAnyKeyText => pressAnyKeyText;
        public Text LegacyPressAnyKeyText => legacyPressAnyKeyText;
        public string NextSceneName => nextSceneName;
        public bool IsTransitioning => isTransitioning;
        public float InputDelay => inputDelay;
        public float BlinkFrequency => blinkFrequency;

        public void Configure(Image background, Image title, TMP_Text text, string scene = "Desktop")
        {
            backgroundImage = background;
            titleImage = title;
            pressAnyKeyText = text;
            nextSceneName = scene;
        }

        private void Awake()
        {
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
        }

        private void Update()
        {
            elapsedTime += Time.deltaTime;

            if (!isTransitioning)
            {
                UpdateBlink(elapsedTime);

                if (elapsedTime >= inputDelay && CheckAnyKeyPressed())
                {
                    TriggerStart();
                }
            }
        }

        /// <summary>
        /// Calculates the smooth 0.0 ~ 1.0 ping-pong blink interpolation factor.
        /// </summary>
        public float CalculateBlinkT(float time)
        {
            // Sin wave mapped from [-1, 1] to [0, 1] for smooth breathing effect.
            return (Mathf.Sin(time * Mathf.PI * blinkFrequency) + 1f) * 0.5f;
        }

        /// <summary>
        /// Evaluates text color based on the current blink factor t (0.0 = dark, 1.0 = bright).
        /// </summary>
        public Color EvaluateTextColor(float t)
        {
            if (useColorFade)
            {
                return Color.Lerp(darkColor, brightColor, t);
            }

            var color = baseTextColor;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            return color;
        }

        private void UpdateBlink(float time)
        {
            var t = CalculateBlinkT(time);
            var color = EvaluateTextColor(t);

            if (pressAnyKeyText != null)
            {
                pressAnyKeyText.color = color;
            }
            if (legacyPressAnyKeyText != null)
            {
                legacyPressAnyKeyText.color = color;
            }
        }

        /// <summary>
        /// Checks if any keyboard key, mouse click, or gamepad button was pressed this frame.
        /// Compatible with both New Input System and Legacy Input.
        /// </summary>
        public bool CheckAnyKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            if (allowMouseClick)
            {
                var mouse = Mouse.current;
                if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                                      mouse.rightButton.wasPressedThisFrame ||
                                      mouse.middleButton.wasPressedThisFrame))
                {
                    return true;
                }

                var pointer = Pointer.current;
                if (pointer != null && pointer.press.wasPressedThisFrame)
                {
                    return true;
                }
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                foreach (var control in gamepad.allControls)
                {
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
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
            if (allowMouseClick && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// Initiates scene loading to the next desktop scene.
        /// </summary>
        public void TriggerStart()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            OnStartTriggered?.Invoke();
            onStartRequested?.Invoke();

            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
    }
}

