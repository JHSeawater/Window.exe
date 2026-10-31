using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class EyeTrackerTests
    {
        private GameObject rootGo;
        private GameObject pupilGo;
        private EyeTracker eyeTracker;

        [SetUp]
        public void SetUp()
        {
            rootGo = new GameObject("EyeRoot");
            pupilGo = new GameObject("Pupil");
            pupilGo.transform.SetParent(rootGo.transform, false);

            eyeTracker = rootGo.AddComponent<EyeTracker>();
            eyeTracker.PupilTransform = pupilGo.transform;
            eyeTracker.MaxRadius = 0.2f;
            eyeTracker.TrackSpeed = 10f;
            eyeTracker.ReturnSpeed = 10f;
            eyeTracker.AddMicroJitter = false; // 테스트 결정론적 판정을 위해 지터 비활성화
        }

        [TearDown]
        public void TearDown()
        {
            if (rootGo != null)
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void MovesTowardsTargetWithinMaxRadius()
        {
            // 타깃이 멀리 우측(X=10, Y=0)에 위치
            eyeTracker.OverrideHasTarget = true;
            eyeTracker.OverrideTargetPosition = new Vector2(10f, 0f);

            // 1초 동안 충분히 틱 진행
            for (var i = 0; i < 20; i++)
            {
                eyeTracker.ManualTick(0.05f);
            }

            var localPos = pupilGo.transform.localPosition;

            // X축 방향으로 이동했는지 확인
            Assert.Greater(localPos.x, 0.15f);
            Assert.AreEqual(0f, localPos.y, 0.01f);

            // 최대 반경(0.2f)을 초과하지 않는지 확인
            var dist = ((Vector2)localPos).magnitude;
            Assert.LessOrEqual(dist, eyeTracker.MaxRadius + 0.001f);
        }

        [Test]
        public void ReturnsToCenterWhenTargetIsLost()
        {
            // 1. 먼저 타깃을 바라보게 함
            eyeTracker.OverrideHasTarget = true;
            eyeTracker.OverrideTargetPosition = new Vector2(0f, 10f); // 위쪽

            for (var i = 0; i < 20; i++)
            {
                eyeTracker.ManualTick(0.05f);
            }

            Assert.Greater(pupilGo.transform.localPosition.y, 0.15f);

            // 2. 타깃 사라짐
            eyeTracker.OverrideHasTarget = false;

            // 1초 동안 복귀 틱 진행
            for (var i = 0; i < 20; i++)
            {
                eyeTracker.ManualTick(0.05f);
            }

            // 중앙(0, 0)으로 복귀했는지 확인
            var localPos = pupilGo.transform.localPosition;
            Assert.AreEqual(0f, localPos.x, 0.01f);
            Assert.AreEqual(0f, localPos.y, 0.01f);
        }

        [Test]
        public void FastTrackerRespondsFasterThanSlowTracker()
        {
            // 빠른 추적 (예: Danger) vs 느린 추적 (예: Watcher)
            var slowGo = new GameObject("SlowEye");
            var slowPupil = new GameObject("Pupil");
            slowPupil.transform.SetParent(slowGo.transform, false);

            var slowTracker = slowGo.AddComponent<EyeTracker>();
            slowTracker.PupilTransform = slowPupil.transform;
            slowTracker.MaxRadius = 0.2f;
            slowTracker.TrackSpeed = 2f; // 느린 속도
            slowTracker.AddMicroJitter = false;

            eyeTracker.TrackSpeed = 20f; // 빠른 속도

            // 둘 다 우측 타깃 설정
            eyeTracker.OverrideHasTarget = true;
            eyeTracker.OverrideTargetPosition = new Vector2(5f, 0f);

            slowTracker.OverrideHasTarget = true;
            slowTracker.OverrideTargetPosition = new Vector2(5f, 0f);

            // 짧은 시간(0.05초) 단 1회 틱
            eyeTracker.ManualTick(0.05f);
            slowTracker.ManualTick(0.05f);

            // 빠른 추적기의 이동량이 느린 추적기보다 유의미하게 큼을 검증
            Assert.Greater(pupilGo.transform.localPosition.x, slowPupil.transform.localPosition.x);

            Object.DestroyImmediate(slowGo);
        }
    }
}

