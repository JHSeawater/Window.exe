using UnityEngine;
using UnityEngine.SceneManagement;

namespace WindowExe.Desktop
{
    /// <summary>One continuous fan loop across the title, intro and desktop.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class DesktopAmbience : MonoBehaviour
    {
        [SerializeField] private AudioClip fanLoop;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField] private AudioClip bootClip;
        [SerializeField, Range(0f, 1f)] private float bootVolume = 1f;
        [SerializeField, Min(0f)] private float bootFadeSeconds = 1f;
        private static DesktopAmbience instance;
        private static Object bossOwner;
        private AudioSource source;
        private AudioSource bootSource;
        public static bool IsAvailable => instance != null;

        public static void PlayBootSound()
        {
            if (instance == null || instance.bootClip == null) return;
            if (instance.bootSource == null)
            {
                instance.bootSource = instance.gameObject.AddComponent<AudioSource>();
                instance.bootSource.playOnAwake = false;
                instance.bootSource.loop = false;
                instance.bootSource.spatialBlend = 0f;
            }
            instance.bootSource.clip = instance.bootClip;
            instance.bootSource.volume = instance.bootVolume;
            instance.bootSource.Play();
        }

        private void Update()
        {
            if (bootSource == null || !bootSource.isPlaying || bootFadeSeconds <= 0f) return;
            float remaining = (bootClip.samples - bootSource.timeSamples) / (float)bootClip.frequency;
            float fade = Mathf.Clamp01(remaining / bootFadeSeconds);
            bootSource.volume = bootVolume * Mathf.SmoothStep(0f, 1f, fade);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
            bossOwner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureForScene(scene);
        private static void EnsureForScene(Scene scene)
        {
            if (scene.name != "StartScene" && scene.name != "Desktop") return;
            if (instance != null) return;
            var existing = FindAnyObjectByType<DesktopAmbience>();
            if (existing != null) { existing.Activate(); return; }
            var prefab = Resources.Load<DesktopAmbience>("DesktopAmbience");
            if (prefab != null) Instantiate(prefab);
        }

        private void Awake() => Activate();
        private void Start()
        {
            if (instance == this && fanLoop != null) source.Play();
        }

        private void Activate()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.clip = fanLoop;
            source.mute = bossOwner != null;
        }

        // Mute only this source; preserve the playback clock across boss handoffs.
        public static void SetBossFightActive(Object owner, bool active)
        {
            if (owner == null) return;
            if (active) bossOwner = owner;
            else if (bossOwner == owner) bossOwner = null;
            if (instance != null) instance.source.mute = bossOwner != null;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
