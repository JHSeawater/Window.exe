# WINDOW.exe

오래된 컴퓨터에 남겨진 전 사용자의 흔적과 창 너머 공간을 조사하며, 정체불명의 프로그램을 삭제하고 살아남는 아날로그 호러·미스터리·퍼즐 어드벤처 프로젝트다.

## 문서 안내

상세 문서는 `docs/`에 모으고, 루트에는 이 안내 문서와 [AGENTS.md](AGENTS.md)를 둔다. AGENTS.md는 개발 전 문서 확인과 개발 중·후 문서 갱신 절차를 정한다.

각 내용은 아래 담당 문서 한 곳에서 관리한다. 다른 문서에는 필요한 연결만 남긴다.

| 문서 | 역할 | 이곳에서 다룰 내용 |
|---|---|---|
| [PROJECT_CONTEXT.md](docs/PROJECT_CONTEXT.md) | 프로젝트 배경 | 게임잼, 행사 조건, 채점 기준, 제작 목표의 배경 |
| [GDD.md](docs/GDD.md) | 순수 게임 기획 | 플레이 경험, 세계관, 등장인물, 규칙, 퍼즐, 엔딩, 연출, 기획 미정 사항 |
| [BOSS_FIGHT.md](docs/BOSS_FIGHT.md) | 보스전 상세 기획·현재 설정 기준 | 두 페이즈의 규칙·수치·유언·화면 신호·검증 기준. GDD와 설정이 충돌하면 이 문서 기준으로 수정 |
| [IMPLEMENTATION_CHECKLIST.md](docs/IMPLEMENTATION_CHECKLIST.md) | 구현 작업과 상태 | 담당, 오늘 할 일, 후속 작업, 리소스 제작·적용, 완료 조건과 체크 |
| [DEVELOPMENT.md](docs/DEVELOPMENT.md) | 개발 가이드 | Unity 환경·프로젝트 열기, Git 공유, 시스템 연결, 데이터·UI 구조, 구현 방식 |
| [PRODUCTION_PLAN.md](docs/PRODUCTION_PLAN.md) | 제작·시연 계획 | 목표 분량, 우선순위, 축소안, 일정, 발표·시연 순서 |
| [CHANGELOG.md](docs/CHANGELOG.md) | 변경 이력 | 결정 변경의 이유와 날짜, 문서 이동, 이전 기록 |

## 어디부터 읽을까

개발·수정은 [AGENTS.md의 작업 범위·검증 기준](AGENTS.md#작업-시간과-범위)을 따른다. 관련 문서의 현재 범위와 필요한 절부터 읽고, 같은 Unity 에디터의 시험·빌드·씬 변경은 한 작업씩 실행한다.

| 목적 | 먼저 확인할 곳 |
|---|---|
| 현재 담당·미완료·최근 검증 제약 | [체크리스트의 현재 진행 상태](docs/IMPLEMENTATION_CHECKLIST.md#현재-진행-상태-찾아보기) |
| 환경·공유·시스템 연결·API | [개발 가이드의 작업별 안내](docs/DEVELOPMENT.md#현재-작업에서-먼저-볼-곳) |
| 게임 규칙·보스전 설정 | [GDD](docs/GDD.md), [BOSS_FIGHT](docs/BOSS_FIGHT.md) |
| 우선순위·시연 | [제작 계획의 현재 작업 운영](docs/PRODUCTION_PLAN.md#현재-작업-운영-확정--2026-10-31-사용자-지시) |

데스크톱은 Unity에서 `Assets/Scenes/Desktop.unity`를 열고 Play한다. Build Settings는 StartScene → Desktop → YouAreDie 순서이며, 시작 자원 누락 시 진입 방식과 최신 병합 연결은 개발 가이드 21장을 따른다. YouAreDie는 엿보기 실패 후 새 입력으로 Desktop을 재시작하는 화면이며 보스전 엔딩과 구분한다. 보스전 전체 검증 씬은 `Assets/Scenes/BossFightFullValidation.unity`, 2페이즈 단독은 `BossFightValidation.unity`, 입력 도구는 `BossInputValidation.unity`다. 실행법과 인계 API는 개발 가이드 17-10장에 둔다.

Assets는 역할별·기능별 배치(개발 가이드 2장)를 따르고, 글꼴은 제공 NeoDunggeunmo와 공통 GameFontSettings(11장)를 사용한다. 좌하단 시작 메뉴는 제안 상태(12장)이며, 간헐적인 마우스 끊김은 제외 상태(체크리스트 23장)다. 문서 정리로 해당 상태를 바꾸지 않는다.

이전 상세 기록은 필요할 때만 읽는다. [개발 가이드 과거 작업](docs/history/DEVELOPMENT_HISTORY.md), [체크리스트 과거 검증](docs/history/IMPLEMENTATION_CHECKLIST_HISTORY.md), [초기 제작 순서](docs/history/PRODUCTION_PLAN_HISTORY.md)는 당시 기록이며, 원래 장 번호의 이동 안내도 유지한다. 초기 데스크톱·작업 표시줄·폴더 이동 기록은 체크리스트 8~13장, 중요한 결정의 날짜와 이유는 [변경 이력](docs/CHANGELOG.md)에서 확인한다.

## 문서 관리 규칙

- **[확정]**은 기존 합의나 사용자가 지정한 내용이다. **[제안]**은 합의 전인 안, **[미정]**은 결정이 필요한 질문이다. 문서를 옮겼다는 이유로 상태를 바꾸지 않는다.
- 확정 여부는 기획·요구사항의 상태이고, 체크박스는 실제 작업의 완료 확인 상태다. 기획 확정만으로 완료 체크하지 않는다.
- 내용이 바뀌면 위 표의 담당 문서를 수정한다. 구현 완료 체크는 **구현 체크리스트에서만** 갱신한다.
- GDD에는 담당·날짜별 할 일·기술 구조를 넣지 않는다. 전체 기획 미정 사항은 GDD, 보스전 상세 규칙·수치·내부 미정 사항은 BOSS_FIGHT, 구현 방식의 제안은 개발 가이드에 남긴다. BOSS_FIGHT의 [제안]·[미정]을 설정 기준 지정만으로 확정하지 않는다.
- 중요한 결정이나 범위가 바뀌면 담당 문서를 고치고 **CHANGELOG**에 날짜와 이유를 기록한다. 단순 완료 체크마다 이력을 추가할 필요는 없다.
- 로컬 시험 구현·원문의 제작 기록과 공유 프로젝트에서 확인한 완료 상태를 구분한다.
- `docs/history/`는 과거 기록이다. 현재 계약·미완료 항목은 본문에 유지하고, 이동 시 상대 링크·장 참조를 함께 갱신한다.
