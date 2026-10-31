using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
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
        [SerializeField] private float jitterAmount = 0.015f;
        [SerializeField] private float jitterSpeed = 20.0f;

        private Vector2 currentLocalPos;
        private float jitterTime;

        public string TargetWindowId { get => targetWindowId; set => targetWindowId = value; }
        public Transform PupilTransform { get => pupilTransform; set => pupilTransform = value; }
        public float MaxRadius { get => maxRadius; set => maxRadius = Mathf.Max(0.001f, value); }
        public float TrackSpeed { get => trackSpeed; set => trackSpeed = value; }
        public float ReturnSpeed { get => returnSpeed; set => returnSpeed = value; }
        public Vector2 CenterLocalPosition { get => centerLocalPosition; set => centerLocalPosition = value; }
        public bool AddMicroJitter { get => addMicroJitter; set => addMicroJitter = value; }
        public float JitterAmount { get => jitterAmount; set => jitterAmount = value; }

        public Vector2 CurrentLocalPos => currentLocalPos;

        // 단위 테스트 및 외부 제어용 오버라이드
        public bool? OverrideHasTarget { get; set; }
        public Vector2? OverrideTargetPosition { get; set; }

        private void Awake()
        {
            EnsurePupilTransform();
            if (pupilTransform != null)
            {
                currentLocalPos = pupilTransform.localPosition;
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

            Vector2 targetLocalPos;

            if (hasTarget)
            {
                Vector2 eyeCenter = transform.position;
                Vector2 worldDir = targetWorldPos - eyeCenter;
                Vector2 localDir = transform.InverseTransformDirection(worldDir);

                var dist = localDir.magnitude;
                if (dist > 0.0001f)
                {
                    // 조이스틱 클램핑: 거리에 비례하되 maxRadius를 초과하지 않음
                    var clampedDist = Mathf.Min(dist, maxRadius);
                    targetLocalPos = centerLocalPosition + (localDir / dist) * clampedDist;
                }
                else
                {
                    targetLocalPos = centerLocalPosition;
                }

                // 미세한 떨림 효과 (호러 연출)
                if (addMicroJitter && jitterAmount > 0f)
                {
                    jitterTime += deltaTime * jitterSpeed;
                    var jx = (Mathf.PerlinNoise(jitterTime, 0f) - 0.5f) * 2f * jitterAmount;
                    var jy = (Mathf.PerlinNoise(0f, jitterTime) - 0.5f) * 2f * jitterAmount;
                    targetLocalPos += new Vector2(jx, jy);

                    // 떨림이 추가되어도 최대 반경 내로 유지
                    var offsetFromCenter = targetLocalPos - centerLocalPosition;
                    if (offsetFromCenter.magnitude > maxRadius)
                    {
                        targetLocalPos = centerLocalPosition + offsetFromCenter.normalized * maxRadius;
                    }
                }

                // 부드럽게 추적 이동
                currentLocalPos = Vector2.Lerp(currentLocalPos, targetLocalPos, Mathf.Clamp01(deltaTime * trackSpeed));
            }
            else
            {
                // 타깃(윈도우)이 없으면 정면(중앙)으로 서서히 복귀
                targetLocalPos = centerLocalPosition;
                currentLocalPos = Vector2.Lerp(currentLocalPos, targetLocalPos, Mathf.Clamp01(deltaTime * returnSpeed));
            }

            pupilTransform.localPosition = new Vector3(currentLocalPos.x, currentLocalPos.y, pupilTransform.localPosition.z);
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
            if (process == null || process.State == WindowState.Minimized || process.Instance == null || !process.Instance.gameObject.activeInHierarchy)
            {
                return false;
            }

            targetWorldPos = process.Instance.OuterBounds.center;
            return true;
        }
    }
}

