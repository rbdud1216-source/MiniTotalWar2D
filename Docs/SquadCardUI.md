# 🃏 [SquadCardUI.cs] 하단 부대 카드 UI 시스템 가이드

> **원본 소스 파일**: [SquadCardUI.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/SquadCardUI.cs) (총 줄 수: 152줄), [SquadCardUIManager.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/SquadCardUIManager.cs)

---

## 💡 1. 핵심 요약 & 역할
전장 화면 하단 UI 패널에 아군 전체 부대의 카드를 배치하여, **부대 이름 및 그룹 잠금 뱃지(`[G]`), 실시간 잔여 병력 수 표시, 선택 하이라이트 테두리 동기화 및 카드 클릭(Shift/Ctrl 다중 선택)을 통한 부대 지휘 선택**을 관리하는 UI 컴포넌트입니다.

---

## 🌳 2. 아키텍처 트리 맵 (Architecture Tree Map)

- 📁 **1. 카드 매니저 (`SquadCardUIManager.cs`)**
  - `Awake()`, `Start()`: 아군 부대 수에 맞춰 카드 슬롯 동적 생성 및 정렬
  - `Update()`: 전 부대 카드의 병력 수 및 선택 하이라이트 일괄 동기화
- 📁 **2. 개별 부대 카드 (`SquadCardUI.cs`)**
  - `Initialize(Squad squad, PlayerController pc)`: 대상 부대 바인딩 및 텍스트/이미지 참조 자동 탐색 (`AutoFindReferences`)
  - `UpdateUI()`:
    - 부대명 표시 (속도 잠금 그룹 소속 시 `[G] 부대명` 뱃지 자동 추가)
    - 잔여 병력 수(`targetSquad.MemberCount`) 실시간 갱신
  - `SetHighlight(bool isSelected)`: 선택된 부대의 외곽 테두리 하이라이트 이미지(`highlightImage`) 활성화/비활성화
- 📁 **3. 마우스 클릭 및 다중 선택 처리 (`HandleSquadSelection`)**
  - **단순 좌클릭**: 해당 부대만 단독 선택 (`playerController.SelectSquadDirectly(squad, false)`)
  - **Shift + 좌클릭**: 기존 선택을 유지하며 추가 다중 선택 (`playerController.SelectSquadDirectly(squad, true)`)
  - **Ctrl + 좌클릭**: 해당 부대의 선택 상태 개별 토글 (`playerController.ToggleSquadSelection(squad)`)

---

## 🗂️ 3. 해시 테이블 메서드 색인표 (Method Fast-Index Table)

> ⚡ **에이전트 지침**: 코드 수정 전 아래 색인표에서 줄 번호 링크를 확인하고, `view_file`로 해당 범위만 직접 조회하여 작업하십시오.

| 메서드 (Key) | 반환형 / 파라미터 | 핵심 역할 & 기능 요약 (Value) | 호출자 (Callers) | 소스 줄 번호 (Line Range) |
| :--- | :--- | :--- | :--- | :--- |
| `Initialize` | `void (Squad squad, PlayerController pc)` | 특정 부대와 1:1 바인딩 및 참조 자동 연결 | `SquadCardUIManager` | [SquadCardUI.cs#L68-L76](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/SquadCardUI.cs#L68-L76) |
| `UpdateUI` | `void ()` | 부대명, `[G]` 잠금 뱃지, 실시간 병력 수 텍스트 갱신 | `SquadCardUIManager.Update` | [SquadCardUI.cs#L83-L103](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/SquadCardUI.cs#L83-L103) |
| `SetHighlight` | `void (bool isSelected)` | 선택 상태에 따른 테두리 하이라이트 점등 | `SquadCardUIManager.Update` | [SquadCardUI.cs#L105-L111](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/SquadCardUI.cs#L105-L111) |
| `HandleSquadSelection` | `void ()` | Shift/Ctrl 조합에 따른 단일/추가/토글 부대 선택 디스패치 | `Button.onClick` | [SquadCardUI.cs#L123-L151](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/SquadCardUI.cs#L123-L151) |

---

## 🔀 4. 유향 그래프 흐름도 (Data & Call Flow Graph)

```mermaid
flowchart TD
    A[Squad 상태 변화: MemberCount, 그룹 잠금] --> B[SquadCardUIManager.Update]
    B --> C[SquadCardUI.UpdateUI: [G] 부대명 및 잔여 병력 텍스트 갱신]
    B --> D[SquadCardUI.SetHighlight: 선택 테두리 점등]
    E[유저 카드 클릭] --> F{Shift / Ctrl 키 분기}
    F -- 단순 클릭 --> G[PlayerController.SelectSquadDirectly - 단독 선택]
    F -- Shift + 클릭 --> H[PlayerController.SelectSquadDirectly - 추가 다중 선택]
    F -- Ctrl + 클릭 --> I[PlayerController.ToggleSquadSelection - 선택 토글]
```

---

## ⚙️ 5. 주요 UI 참조 컴포넌트

| 컴포넌트 변수 | 타입 | 설명 |
| :--- | :--- | :--- |
| `squadNameText` | `TextMeshProUGUI` | 부대 이름 및 `[G]` 잠금 뱃지 표시 텍스트 |
| `unitCountText` | `TextMeshProUGUI` | 현재 생존한 부대원 수 표시 텍스트 |
| `highlightImage` | `Image` | 부대 선택 시 표시되는 밝은 테두리/배경 이미지 |

---

## 🔗 6. 다른 스크립트와의 연동 관계 (Dependencies)

- **[PlayerController.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/PlayerController.md)**: 카드 클릭 시 `SelectSquadDirectly`, `ToggleSquadSelection` 호출 및 그룹 잠금 여부(`IsSquadInLockedGroup`) 확인
- **[Squad.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/Squad.md)**: `targetSquad.MemberCount`, `targetSquad.squadName` 참조
