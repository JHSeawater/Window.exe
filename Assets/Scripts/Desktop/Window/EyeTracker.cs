using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    public enum EyeJitterType
    {
        Twitch, // 불규칙하게 파들파들 바르르 떠는 공포 미세 경련 (권장)
        Smooth  // 부드럽게 헤엄치듯 흐르는 펄린 노이즈
    }

    /// <summary>
    /// Danger, Watcher 등의 눈 오브젝트에 부착되는 독립 시선 추적 컴포넌트.
    /// 씬에 WINDOW.exe 윈도우가 존재하면 눈알(Pupil)이 윈도우를 향해 조이스틱처럼 움직이고,
    /// 윈도우가 없으면 정면(중앙)으로 부드럽게 복귀합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EyeTracker : MonoBehaviour
    {
        [Header("Target Window Settings")]
        [SerializeField] private string targetWindowId = "window";

        [Header("Eye Movement Settings")]
        [SerializeField] private Transform pupilTransform;
        [SerializeField] private float maxRadius = 0.2f;
        [SerializeField] private float trackSpeed = 8.0f;
        [SerializeField] private float returnSpeed = 5.0f;
        [SerializeField] private Vector2 centerLocalPosition = Vector2.zero;

        [Header("Horror Micro Jitter")]
        [SerializeField] private bool addMicroJitter = true;
        [SerializeField] private EyeJitterType jitterType = EyeJitterType.Twitch;
        [SerializeField] private bool jitterOnlyWhenTarget = false;
        [SerializeField, Range(0f, 0.15f)] private float jitterAmount = 0.04f;
        [SerializeField, Range(5f, 60f)] private float jitterSpeed = 30.0f;

        private Vector2 currentBasePos;
        private Vector2 currentFinalPos;
        private Vector2 currentTwitchOffset;
        private float twitchTimer;
        private float jitterTime;

        public string TargetWindowId { get => targetWindowId; set => targetWindowId = value; }
        public Transform PupilTransform { get => pupilTransform; set => pupilTransform = value; }
        public float MaxRadius { get => maxRadius; set => maxRadius = Mathf.Max(0.001f, value); }
        public float TrackSpeed { get => trackSpeed; set => trackSpeed = value; }
        public float ReturnSpeed { get => returnSpeed; set => returnSpeed = value; }
        public Vector2 CenterLocalPosition { get => centerLocalPosition; set => centerLocalPosition = value; }
        public bool AddMicroJitter { get => addMicroJitter; set => addMicroJitter = value; }
        public EyeJitterType JitterType { get => jitterType; set => jitterType = value; }
        public bool JitterOnlyWhenTarget { get => jitterOnlyWhenTarget; set => jitterOnlyWhenTarget = value; }
        public float JitterAmount { get => jitterAmount; set => jitterAmount = value; }
        public float JitterSpeed { get => jitterSpeed; set => jitterSpeed = value; }

        public Vector2 CurrentLocalPos => currentFinalPos;
        public Vector2 CurrentBasePos => currentBasePos;

        // 단위 테스트 및 외부 제어용 오버라이드
        public bool? OverrideHasTarget { get; set; }
        public Vector2? OverrideTargetPosition { get; set; }

        private void Awake()
        {
            EnsurePupilTransform();
            if (pupilTransform != null)
            {
                currentBasePos = pupilTransform.localPosition;
                currentFinalPos = currentBasePos;
            }
        }

        private void Reset()
        {
            EnsurePupilTransform();
        }

        public void EnsurePupilTransform()
        {
            if (pupilTransform != null) return;

            var pupil = transform.Find("Pupil");
            if (pupil == null) pupil = transform.Find("EyeBall");
            if (pupil == null) pupil = transform.Find("EyePupil");
            if (pupil == null && transform.childCount > 0) pupil = transform.GetChild(0);

            pupilTransform = pupil != null ? pupil : transform;
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        public void ManualTick(float deltaTime)
        {
            if (pupilTransform == null) EnsurePupilTransform();
            if (pupilTransform == null) return;

            var hasTarget = TryGetTargetPosition(out var targetWorldPos);

            // 지터가 가장자리에서 클리핑(벽 끼임)되지 않도록 기본 시선의 최대 반경에 여유를 확보
            var effectiveJitter = addMicroJitter ? Mathf.Min(jitterAmount, maxRadius * 0.45f) : 0f;
            var maxBaseRadius = Mathf.Max(0.001f, maxRadius - effectiveJitter);

            Vector2 targetBasePos;
            float followSpeed;

            if (hasTarget)
            {
                Vector2 eyeCenter = transform.position;
                Vector2 worldDir = targetWorldPos - eyeCenter;
                Vector2 localDir = transform.InverseTransformDirection(worldDir);

                var dist = localDir.magnitude;
                if (dist > 0.0001f)
                {
                    // 조이스틱 클램핑: 거리에 비례하되 maxBaseRadius를 초과하지 않음
                    var clampedDist = Mathf.Min(dist, maxBaseRadius);
                    targetBasePos = centerLocalPosition + (localDir / dist) * clampedDist;
                }
                else
                {
                    targetBasePos = centerLocalPosition;
                }
                followSpeed = trackSpeed;
            }
            else
            {
                // 타깃(윈도우)이 없으면 정면(중앙)으로 서서히 복귀
                targetBasePos = centerLocalPosition;
                followSpeed = returnSpeed;
            }

            // 1. 기본 시선 위치는 Lerp로 부드럽게 추적/복귀
            currentBasePos = Vector2.Lerp(currentBasePos, targetBasePos, Mathf.Clamp01(deltaTime * followSpeed));

            // 2. 지터(Jitter)는 Lerp 이후의 기본 위치 위에 직접 가산하여 감쇄되지 않고 생생하게 떨리도록 적용
            currentFinalPos = currentBasePos;
            var canJitter = addMicroJitter && effectiveJitter > 0f && (!jitterOnlyWhenTarget || hasTarget);

            if (canJitter)
            {
                Vector2 jitterOffset;
                if (jitterType == EyeJitterType.Twitch)
                {
                    // 불규칙하게 파들파들 바르르 떠는 공포 미세 경련 (초당 jitterSpeed 회 갱신)
                    twitchTimer += deltaTime * jitterSpeed;
                    if (twitchTimer >= 1.0f || currentTwitchOffset == Vector2.zero)
                    {
                        twitchTimer %= 1.0f;
                        var randomCircle = Random.insideUnitCircle;
                        if (randomCircle.sqrMagnitude < 0.04f) randomCircle = randomCircle.normalized * 0.2f;
                        currentTwitchOffset = randomCircle * effectiveJitter;
                    }
                    jitterOffset = currentTwitchOffset;
                }
                else
                {
                    // 부드럽게 헤엄치는 펄린 노이즈
                    jitterTime += deltaTime * jitterSpeed;
                    var jx = (Mathf.PerlinNoise(jitterTime, 17.17f) - 0.5f) * 2f * effectiveJitter;
                    var jy = (Mathf.PerlinNoise(73.73f, jitterTime) - 0.5f) * 2f * effectiveJitter;
                    jitterOffset = new Vector2(jx, jy);
                }

                currentFinalPos += jitterOffset;

                // 흰자위 최대 반경을 절대 넘지 않도록 안전 클램핑
                var offsetFromCenter = currentFinalPos - centerLocalPosition;
                if (offsetFromCenter.magnitude > maxRadius)
                {
                    currentFinalPos = centerLocalPosition + offsetFromCenter.normalized * maxRadius;
                }
            }

            pupilTransform.localPosition = new Vector3(currentFinalPos.x, currentFinalPos.y, pupilTransform.localPosition.z);
        }

        public bool TryGetTargetPosition(out Vector2 targetWorldPos)
        {
            if (OverrideHasTarget.HasValue)
            {
                if (OverrideHasTarget.Value)
                {
                    targetWorldPos = OverrideTargetPosition.GetValueOrDefault(Vector2.zero);
                    return true;
                }
                targetWorldPos = Vector2.zero;
                return false;
            }

            targetWorldPos = Vector2.zero;
            var wm = WindowManager.Instance;
            if (wm == null) return false;

            var process = wm.GetProcess(targetWindowId);
            // targetWindowId로 못 찾으면 열려 있는 WindowExeWindow를 스마트 자동 탐색
            if (process == null || process.State == WindowState.Minimized || process.Instance == null || !process.Instance.gameObject.activeInHierarchy)
            {
                process = null;
                var winProc = wm.GetProcess("window");
                if (winProc != null && winProc.State != WindowState.Minimized && winProc.Instance != null && winProc.Instance.gameObject.activeInHierarchy)
                {
                    process = winProc;
                }
                if (process == null)
                {
                    foreach (var p in wm.ActiveProcesses)
                    {
                        if (p.Instance is WindowExeWindow && p.State != WindowState.Minimized && p.Instance.gameObject.activeInHierarchy)
                        {
                            process = p;
                            break;
                        }
                    }
                }
            }

            if (process == null || process.State == WindowState.Minimized || process.Instance == null || !process.Instance.gameObject.activeInHierarchy)
            {
                return false;
            }

            targetWorldPos = process.Instance.OuterBounds.center;
            return true;
        }
    }
}

