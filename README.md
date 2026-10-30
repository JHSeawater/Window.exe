# WINDOW.exe

오래된 컴퓨터 속 정체불명의 프로그램에 갇힌 사람과 협력해, 프로그램을 삭제하고 살아남는 아날로그 호러 · 미스터리 · 퍼즐 어드벤처 프로젝트입니다.

## 개발 환경

- Unity **6000.6.7f1**
- Universal Render Pipeline (2D)

## 프로젝트 열기

1. 이 저장소를 클론합니다.
2. Unity Hub에서 **Add → Add project from disk**를 선택하고 저장소 폴더를 등록합니다.
3. Unity **6000.6.7f1**로 프로젝트를 열고 패키지 설치와 에셋 임포트가 끝날 때까지 기다립니다.
4. `Assets/Scenes/SampleScene.unity`를 엽니다.

`Library`, `Temp`, `Logs`, `UserSettings`와 IDE 생성 파일은 Git에서 제외되며 각 개발 환경에서 다시 생성됩니다. 에셋과 함께 해당 `.meta` 파일도 커밋해야 합니다.

## 현재 작업과 공유 범위

문서 정리를 먼저 진행하고, 구현은 이후에 착수합니다. 구현 착수 시의 첫 범위는 데스크톱 기본 기능, 메모장, WINDOW.exe의 기본 창 동작입니다.

팀원과 프로젝트 기준을 맞추기 위해 `Assets`에서는 `Scenes`, `Settings`, `Welcome`과 각 폴더의 `.meta`만 Git에 포함합니다. 나머지 폴더와 `.meta`는 로컬에 보존하되 `.gitignore`로 제외합니다. 이후 구현 파일을 공유할 때 팀에서 폴더 구조를 맞추고 제외 규칙을 변경합니다.

로컬 시험용 `DesktopPrototype.unity`와 해당 `.meta`도 제외합니다. 공유되지 않는 스크립트를 참조하는 씬이 저장소에 단독으로 포함되지 않도록 하기 위한 규칙입니다. 로컬 에디터 도구가 만든 Pipeline 설정과 SceneTemplateSettings도 제외합니다.

## 문서

- [게임 기획서](GDD.md)
- [프로젝트 상황과 개발 조건](PROJECT_CONTEXT.md)
