# WINDOW.exe — 개발 가이드 과거 작업

> 2026-10-31 문서 정리 시 보존한 과거 기록이다. 당시 계획·확인 결과이며 현재 구현 완료를 뜻하지 않는다.
> 현재 기준과 진행 상태는 [원본 문서](../DEVELOPMENT.md)에서 관리한다. 체크 표시는 당시 스냅샷으로 보존하며 이 파일에서 갱신하지 않는다.

### 10-1. 병합 결과와 실행 기준선 정리

먼저 Play Mode를 정지하고 미저장 씬을 확인한 뒤 컴파일·콘솔·Missing Script·에셋 GUID를 점검한다. 단순 응답 시간초과와 실제 컴파일 오류를 구분하며, 에디터 복구가 필요한 경우 사용자 씬을 보존한다. `Renderer2D`·Sorting Layer·TMP 의존성은 팀원 커밋과 대조하고 필요한 변경만 남긴다.

`SingleResizeHandle`은 현재 다른 MonoBehaviour와 같은 파일에 있고 씬 내부 MonoScript 참조를 사용한다. 독립 `.cs`·`.meta`로 분리하고 8개 핸들의 직렬화 참조를 에디터 도구로 정상 에셋에 연결하는 안이다. 씬·프리팹을 저장하고 다시 열어 스크립트와 방향 값이 유지되는지 확인한다. 기존 `WindowResizeHandle.cs.meta`와 다른 원본 GUID는 유지한다. 클래스·네임스페이스의 일괄 변경과 폴더 재이동은 하지 않는다.

### 11-1. TMP 참조 손상 복구와 회귀 확인 — 2026-10-31

팀원 커밋 `93ea75d`에서 줄바꿈 규칙·스타일·셰이더의 `.meta` GUID가 바뀌었으나 TMP 설정과 공통 폰트는 이전 GUID를 참조했다. 로컬에서 줄바꿈 테이블이 null인 채 `TextMeshPro.GenerateTextMesh:4078` 예외가 발생하고 폰트 재질은 `Hidden/InternalErrorShader`로 대체되는 것을 확인했다.

수정 순서는 현재 파일/사용자 변경 백업 → 누락 참조를 검출하는 회귀 시험 → 실제 `.meta` GUID로 설정/폰트 참조 갱신 → 에셋 재임포트 및 TMP 줄바꿈 캐시 재생성 → 3D/UI 한글 메시·실제 창·기존 회귀 검증이다. 현재 GUID·NeoDunggeunmo 원본·폰트 atlas·사용자 창 재질 변경은 유지한다. PackageCache의 TMP 구현이나 `DesktopWindowSpace.Refresh`에 예외 무시 코드를 넣지 않는다.

`GameTypographyTests`는 공유 줄바꿈/스타일/셰이더가 추적 경로의 에셋으로 해결되는지와 줄바꿈이 발생하는 한글·문장부호의 3D TextMeshPro/TextMeshProUGUI 메시 생성을 검증한다. 실제 완료 범위와 미확인 항목은 체크리스트 17-1장에서 관리한다. `.meta` 변경이 필요하면 같은 커밋에서 모든 참조 에셋을 함께 갱신하며 기존 파일의 GUID를 임의 재생성하지 않는다.
