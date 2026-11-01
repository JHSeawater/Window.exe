using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace WindowExe.Desktop
{
    /// <summary>Observes mouse press/release edges without consuming gameplay input.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class GlobalClickAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip pressClip;
        [SerializeField] private AudioClip releaseClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField, Min(0f)] private float pressGain = 17f;
        [SerializeField, Min(0f)] private float releaseGain = 43f;
        private static GlobalClickAudio instance;
        private AudioSource source;
        public int PlayCount { get; private set; }
        public AudioClip LastPlayedClip { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        {
            var existing = FindAnyObjectByType<GlobalClickAudio>();
            if (existing != null) { existing.Activate(); return; }
            var prefab = Resources.Load<GlobalClickAudio>("GlobalClickAudio");
            if (prefab != null) Instantiate(prefab);
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Activate();
        }

        private void Activate()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            InputSystem.onEvent -= ObserveMouse;
            InputSystem.onEvent += ObserveMouse;
        }

        private void ObserveMouse(InputEventPtr input, InputDevice device)
        {
            if ((InputState.currentUpdateType & InputUpdateType.Dynamic) == 0 ||
                !(device is Mouse mouse) ||
                (!input.IsA<StateEvent>() && !input.IsA<DeltaStateEvent>())) return;
            PlayEdge(mouse.leftButton, input);
            PlayEdge(mouse.rightButton, input);
            PlayEdge(mouse.middleButton, input);
            PlayEdge(mouse.backButton, input);
            PlayEdge(mouse.forwardButton, input);
        }

        private void PlayEdge(ButtonControl button, InputEventPtr input)
        {
            if (!button.ReadValueFromEvent(input, out var value)) return;
            bool pressed = value >= button.pressPointOrDefault;
            if (pressed == button.isPressed) return;
            var clip = pressed ? pressClip : releaseClip;
            if (clip == null) return;
            source.PlayOneShot(clip, pressed ? pressGain : releaseGain);
            LastPlayedClip = clip;
            PlayCount++;
        }

        private void OnDisable() => InputSystem.onEvent -= ObserveMouse;
        private void OnDestroy()
        {
            InputSystem.onEvent -= ObserveMouse;
            if (instance == this) instance = null;
        }
    }
}
