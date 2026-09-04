# 👻 [FormationPreviewer.cs] 대형 드래그 실시간 고스트 프리뷰 가이드

> **원본 소스 파일**: [FormationPreviewer.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs) (총 줄 수: 275줄)

---

## 💡 1. 핵심 요약 & 역할
마우스 우클릭 드래그로 부대 대형을 전개하거나 Alt+좌클릭으로 곡선 대형을 조작할 때, **지형 위에 각 병사가 배치될 예상 슬롯 좌표와 시선 회전각을 반투명 도트(Dot) 오브젝트 풀을 통해 실시간으로 렌더링**하는 시각화 컴포넌트입니다.

---

## 🌳 2. 아키텍처 트리 맵 (Architecture Tree Map)

- 📁 **1. 생명주기 및 도트 오브젝트 풀링 (Lifecycle & Object Pooling)**
  - `Awake()`: `PreviewDots_Pool` 전용 컨테이너 생성 및 계층 구조 설정
  - `EnsureDotPoolCount(int targetCount)`: 요구 수량만큼 도트 오브젝트 풀 확장 (프리팹 미지정 시 Cyan Sphere 폴백 생성)
  - `HidePreview()`: 모든 활성 도트 일괄 비활성화 (`SetActive(false)`)
- 📁 **2. 단일 부대 프리뷰 렌더링 (Single Squad Preview)**
  - `ShowPreview(Squad, dest, rot, cols)`: 단일 부대 슬롯 위치 및 시선 회전(사각방진 4방향, 원형진 방사형) 렌더링
  - `ShowCurvedPreview(Squad, dest, rot, cols, curve)`: Alt+좌클릭 2차 포물선 곡선 대형 슬롯 렌더링
- 📁 **3. 다중 부대 군단 프리뷰 렌더링 (Multi-Squad Army Preview)**
  - `ShowMultiSquadPreview(squadConfigs)`: 여러 부대의 동시 배치 및 통합 회전 대형 렌더링
  - `ShowMultiSquadCurvedPreview(squadConfigs)`: 군단 일체형 거대 초승달 전선(Grand Continuous Arc) 프리뷰 렌더링
- 📁 **4. 자유 유닛 프리뷰 렌더링 (Free Units Preview)**
  - `ShowFreeUnitPreview(slotPositions, slotRotations)`: 부대가 없는 독립 유닛 무리의 대형 프리뷰

---

## 🗂️ 3. 해시 테이블 메서드 색인표 (Method Fast-Index Table)

> ⚡ **에이전트 지침**: 코드 수정 전 아래 색인표에서 줄 번호 링크를 확인하고, `view_file`로 해당 범위만 직접 조회하여 작업하십시오.

| 메서드 (Key) | 반환형 / 파라미터 | 핵심 역할 & 기능 요약 (Value) | 호출자 (Callers) | 소스 줄 번호 (Line Range) |
| :--- | :--- | :--- | :--- | :--- |
| `ShowFreeUnitPreview` | `void (slots, rots)` | 자유 유닛 그룹의 슬롯 위치 및 회전 프리뷰 도트 표시 | `PlayerController` | [FormationPreviewer.cs#L28-L56](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs#L28-L56) |
| `ShowPreview` | `void (squad, dest, rot, cols)` | 단일 부대 이동/배치 시 진형별 슬롯 도트 표시 | `PlayerController` | [FormationPreviewer.cs#L64-L93](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs#L64-L93) |
| `ShowCurvedPreview` | `void (squad, dest, rot, cols, curve)` | 단일 부대 2차 곡선 대형 프리뷰 도트 표시 | `PlayerController` | [FormationPreviewer.cs#L98-L127](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs#L98-L127) |
| `ShowMultiSquadPreview` | `void (squadConfigs)` | 다중 부대 동시 이동 및 군단 진형 프리뷰 표시 | `PlayerController` | [FormationPreviewer.cs#L132-L174](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs#L132-L174) |
| `ShowMultiSquadCurvedPreview` | `void (squadConfigs)` | 군단 일체형 거대 초승달 곡선 전선 프리뷰 표시 | `PlayerController` | [FormationPreviewer.cs#L179-L224](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs#L179-L224) |
| `EnsureDotPoolCount` | `void (int targetCount)` | 필요 슬롯 수만큼 도트 오브젝트 풀 동적 확장 | 내부 렌더링 메서드들 | [FormationPreviewer.cs#L229-L260](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs#L229-L260) |
| `HidePreview` | `void ()` | 모든 프리뷰 도트를 즉시 화면에서 숨김 | `PlayerController` 마우스 업 | [FormationPreviewer.cs#L265-L274](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/FormationPreviewer.cs#L265-L274) |

---

## 🔀 4. 유향 그래프 흐름도 (Data & Call Flow Graph)

```mermaid
flowchart TD
    A[PlayerController: 마우스 우클릭/Alt 드래그] --> B{드래그 모드 분기}
    B -- 단일 부대 드래그 --> C[FormationPreviewer.ShowPreview]
    B -- 곡선 대형 조작 --> D[FormationPreviewer.ShowCurvedPreview]
    B -- 다중 부대 드래그 --> E[FormationPreviewer.ShowMultiSquadPreview]
    C --> F[EnsureDotPoolCount: 풀 크기 자동 확장]
    D --> F
    E --> F
    F --> G[각 도트 position.y += yOffset 보정 및 SetActive(true)]
    H[PlayerController: 마우스 버튼 뗌] --> I[FormationPreviewer.HidePreview]
```

---

## ⚙️ 5. 주요 인스펙터 설정 변수

| 변수명 | 타입 | 기본값 | 설명 |
| :--- | :--- | :--- | :--- |
| `previewDotPrefab` | `GameObject` | - | 지형 위에 렌더링할 프리뷰 도트 프리팹 (미할당 시 기본 구체 폴백 생성) |
| `yOffset` | `float` | `0.05f` | 지형 메쉬 묻힘(Z-Fighting)을 방지하기 위한 Y축 높이 보정값 |

---

## 🔗 6. 다른 스크립트와의 연동 관계 (Dependencies)

- **[PlayerController.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/PlayerController.md)**: 마우스 드래그 중 `ShowPreview`, `HidePreview` 호출
- **[Squad.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/Squad.md)**: `squad.GetPreviewSlotTransforms()` 및 `GetCurvedPreviewSlotPositions()`를 호출하여 기하학 슬롯 좌표 산출
