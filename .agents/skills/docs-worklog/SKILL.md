---
name: docs-worklog
description: Workspace skill to maintain project memory, task history, and documentation in Docs/ directory. Follow this workflow for all development tasks.
---

# 📚 Docs WorkLog & Project Memory Skill

이 스킬은 프로젝트의 모든 작업 내용, 설계 결정, 변경 이력 및 시스템 구조를 `Docs/` 디렉터리에 지속적으로 기록하고 기억하여, 연속성 있는 고품질 개발을 수행하기 위한 지침입니다.

---

## 🎯 핵심 원칙 (Core Principles)

1. **단일 진실 공급원 (Single Source of Truth)**:
   - 모든 기능 수정, 버그 해결, 알고리즘 변경 사항은 사용자의 요구사항과 함께 `Docs/업데이트_내용.md`에 날짜별로 즉각 기록되어야 합니다.
   - `Docs/*.md`의 각 스크립트별 가이드 문서는 항상 최신 코드와 100% 일치하도록 동기화합니다.

2. **기억 및 컨텍스트 로딩 (Memory & Continuity)**:
   - 새로운 작업이나 사용자 요청을 수행하기 전, 반드시 `Docs/업데이트_내용.md`와 관련된 `Docs/<스크립트명>.md`를 먼저 확인하여 기존 설계 의도와 히스토리를 파악합니다.

3. **🎮 GameObject ⇄ Pure ECS 듀얼 모드 아키텍처 필수 원칙 (Dual Mode Principle)**:
   - **게임오브젝트(GameObject) 모드 (`Use Pure ECS = false`)**:
     - **목적**: 개발, 디버깅, 인스펙터 튜닝의 편의성을 극대화하기 위한 모드입니다.
     - 하이어라키에서 개별 유닛을 눈으로 확인하고, 인스펙터 수정 및 개별 유닛 상호작용을 테스트할 수 있도록 유지해야 합니다.
   - **순수 ECS(Pure ECS) 모드 (`Use Pure ECS = true`)**:
     - **목적**: 수만 명(1,200 ~ 50,000기)의 초대규모 대군단을 렉 없이 150~220+ FPS로 초고속 시뮬레이션 및 렌더링하기 위한 최적화 모드입니다.
     - 게임오브젝트를 0개로 만들고, 오직 순수 Entity 컴포넌트와 GPU Instancing으로 구동됩니다.
   - **🚨 개발 시 불변의 철칙**:
     - **코드를 작성할 때 항상 이 두 모드가 공존함을 염두에 두어야 합니다.**
     - 핵심 지휘 수학(진형, 슬롯 계산, 이동 거리 등)은 1곳에서 공통으로 작성하고, 명령 전달 및 UI 연동 시 양쪽 모드 모두에서 100% 정상 작동하도록 호환성을 보장해야 합니다.
     - **작업 순서**: 반드시 **1순위 오브젝트 모드 완성 -> 2순위 ECS 모드 1:1 동기화** 순서로 진행합니다.

4. **⏱️ 실제 총 작업 소요 시간 명시 (Real Wall-Clock Time Rule)**:
   - 사용자와의 모든 작업 완료 보고 시, 도구 실행 시간만이 아닌 **사용자 요청 접수부터 최종 답변 완료까지 걸린 실제 총 작업 시간(IDE 상단 `Worked for ...` 기준)**을 정확하게 상단 또는 요약부에 명시합니다. (예: `⏱️ 소요 시간: 약 1분 20초`, `⏱️ 소요 시간: 약 8분 45초`)

---

## 🔄 표준 작업 파이프라인 (Work Pipeline)

```mermaid
flowchart TD
    A[1. 맥락 & 목적 파악 (요청 및 기존 히스토리)] --> B[2. 현안문제 도출]
    B --> C[3. 정보·개념·관점 수립 (수학·아키텍처)]
    C --> D[4. 논증 (코드 수정 및 결과물 도출)]
    D --> E{5. 검증 성공 여부}
    E -- 실패 --> F[6. 대안 가동 -> 새로운 맥락으로 피드백]
    F --> A
    E -- 성공 --> G[7. Docs/업데이트_내용.md 및 개별 가이드 문서 최신화]
```

---

## 📝 1. `Docs/업데이트_내용.md` 기록 양식

새로운 작업을 완료하면 `Docs/업데이트_내용.md`의 `## 📅 업데이트 이력 및 작업 상태` 섹션 최상단에 아래 형식으로 항목을 추가합니다:

```markdown
### [YYYY-MM-DD] 작업 제목
- **사용자 요구 사항 (User Request)**:
  - 사용자가 요청한 구체적인 내용 및 목적
- **대상 스크립트 / 파일**: `수정된스크립트1.cs`, `수정된스크립트2.cs`
- **문제점 / 배경**:
  - 기존 동작의 문제점 및 사용자 요구사항 요약
- **변경 내용**:
  1. **주요 변경 항목 1**:
     - 구체적인 로직 변경 및 함수/구조체 수정 내용
  2. **주요 변경 항목 2**:
     - 상호작용 및 파이프라인 연계 개선 내용
- **개별 문서 반영 여부**: [x] 반영 완료 / [ ] 대기 중
```

---

## 📖 2. 개별 스크립트 가이드 (`Docs/*.md`) 완전체 표준 템플릿 (7대 항목)

대형 스크립트의 복잡도를 제어하고 AI 에이전트의 slice 검색 속도 및 시스템 이해도를 극대화하기 위해, 모든 개별 스크립트 문서는 다음 7대 필수 섹션을 포함합니다:

1. **💡 핵심 요약 & 역할 (Summary & Role)**: 스크립트의 존재 목적과 시스템 내 책임 영역 요약.
2. **🌳 1. 아키텍처 트리 맵 (Tree Map)**: 생명주기, 기하학 연산, 하위 서브시스템 등 계층적 구조 맵.
3. **🗂️ 2. 해시 테이블 메서드/구조체 색인표 (Hash Table Index)**: `Key : Value(역할, 의존성, 소스 줄 번호 링크)`를 통한 O(1) 초고속 조회 색인.
4. **🔀 3. 유향 그래프 흐름도 (Directed Graph - Mermaid)**: 호출 순서, 데이터 파이프라인 및 상태 전이도 시각화.
5. **⚙️ 4. 주요 상태 변수, 데이터 구조체 & 인스펙터 옵션 (State, Structs & Inspector Fields)**: 변수 타입, 기본값, 상세 역할 및 인스펙터 튜닝 가이드.
6. **🛠️ 5. 핵심 알고리즘, 물리/전투 수학 공식 & 조작법 (Algorithms, Math & Controls)**: 기하학 수학 공식, 공간 정렬/포위 알고리즘, 넉백/돌격 물리 수식, 키보드/마우스 단축키 조작 일람표 등 상세 기술 명세.
7. **🔗 6. 다른 스크립트와의 연동 관계 (Dependencies & Pipeline)**: 외부 스크립트와의 상호작용 및 파이프라인 연계 관계.

---

## 📂 3. 문서 목록 및 매핑

| 스크립트 파일 | 담당 문서 경로 | 주요 역할 |
| :--- | :--- | :--- |
| `UnitJobSimulationManager.cs` | [UnitJobSimulationManager.md](file:///c:/unityProject/MiniTotalWar2D/Docs/UnitJobSimulationManager.md) | 멀티코어 C# Job System 유닛 시뮬레이션 |
| `Squad.cs` | [Squad.md](file:///c:/unityProject/MiniTotalWar2D/Docs/Squad.md) | 부대 대형, 공간 정렬, 포위 및 순차 기동 |
| `PlayerController.cs` | [PlayerController.md](file:///c:/unityProject/MiniTotalWar2D/Docs/PlayerController.md) | 유저 마우스/키보드 입력 및 명령 전달 |
| `Unit.cs` | [Unit.md](file:///c:/unityProject/MiniTotalWar2D/Docs/Unit.md) | 개별 3D 유닛 라이프사이클 및 네비메시 연동 |
| `BattleManager.cs` | [BattleManager.md](file:///c:/unityProject/MiniTotalWar2D/Docs/BattleManager.md) | 군단 생성 및 전투 관리 |
| `SquadCardUI.cs` | [SquadCardUI.md](file:///c:/unityProject/MiniTotalWar2D/Docs/SquadCardUI.md) | 좌하단 부대 카드 UI |
| `SquadIconUI.cs` | [SquadIconUI.md](file:///c:/unityProject/MiniTotalWar2D/Docs/SquadIconUI.md) | 부대 머리 위 3D 월드 UI |
| `CommandUI.cs` | [CommandUI.md](file:///c:/unityProject/MiniTotalWar2D/Docs/CommandUI.md) | 우하단 명령 버튼 UI |
| 전체 히스토리 | [업데이트_내용.md](file:///c:/unityProject/MiniTotalWar2D/Docs/업데이트_내용.md) | 전체 프로젝트 변경 내역 및 작업 로그 |
