# 🏹 [RangeVisualizationManager.cs] 원거리 사격 호 & 탄도 궤적 시스템 기술 명세서

> **소스 파일**:
> - [RangeVisualizationManager.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs)
> - [RangeArcRenderer.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeArcRenderer.cs)
> - [TargetTrajectoryPredictor.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/TargetTrajectoryPredictor.cs)
> - [RangeArcOverlay.shader](file:///c:/unityProject/MiniTotalWar2D/Assets/Shaders/RangeArcOverlay.shader)

---

## 💡 핵심 요약 & 역할 (Summary & Role)
토탈워(Total War) 스타일의 **지형 밀착 부채꼴 사격 호(Firing Arc)**, 적 부대 조준 시 **예상 정지 위치 및 3D 물리 포물선 탄도 궤적선**, **언덕/요철 지형 차폐 실시간 경고(크림슨 레드 전환 및 X자 마커)**를 다중 부대 및 자유 유닛에 통합 관장하는 싱글톤 매니저 시스템입니다.

---

## 🌳 1. 아키텍처 트리 맵 (Tree Map)

- 📁 **RangeVisualizationManager (중앙 싱글톤 관제탑)**
  - ├── 📄 **선택 및 필터링 시스템**
  - │   ├── `UpdateSelection`: 선택된 부대 및 자유 유닛 실시간 갱신 (자유 유닛은 1마리 단독 선택 시에만 허용)
  - │   ├── `SetHoveredEnemy`: 마우스 커서 아래 적 부대 호버링 상태 갱신
  - │   └── `SetGroundTarget`: 어택땅(Z키) 공격 지정 모드 시 지면 가상 목표점 갱신
  - ├── 📄 **실시간 렌더링 디스패치 (`RefreshVisualization`)**
  - │   ├── 다중 원거리 부대 각각 1:1 사격 호 및 탄도 궤적 개별 표출
  - │   ├── 자유 유닛 1마리 단독 선택 시 발밑 사격 호 표출
  - │   └── 공격 명령 중에도 타겟을 향한 포물선 궤적 지속 유지 (`keepTrajectoryDuringAttack`)
  - ├── 📄 **우클릭 드래그 배치 선행 투영 시스템**
  - │   ├── `ShowDragPlacementPreview`: 단일 부대 목표 도착 위치 및 각도에 사격 호 선행 프리뷰
  - │   ├── `ShowDragPlacementPreviews`: 다중 부대 진형 배치 시 모든 부대 위치에 일괄 선행 프리뷰
  - │   └── `ClearDragPlacementPreview`: 드래그 완료 시 프리뷰 해제 및 본래 사격 호 복귀
  - └── 📄 **전술 온/오프 토글 시스템**
  -     └── `ToggleVisualization`: [I] 키 및 전술 UI 버튼을 통한 실시간 시각화 토글

- 📁 **RangeArcRenderer (지형 표면 밀착 부채꼴 메쉬)**
  - └── `UpdateArc`: 36세그먼트 x 4링 방사형 절차적 메쉬 생성 (`TerrainHeightManager.SampleHeightFast` 연동)

- 📁 **TargetTrajectoryPredictor (3D 물리 탄도 & 지형 차폐 예측기)**
  - ├── `UpdatePrediction`: 적 부대 조준 시 정지 사격 위치 산출, 이동 경로선, 3D 탄도 궤적 및 지형 충돌 검출
  - ├── `UpdateGroundPrediction`: 어택땅 모드 시 지면 좌표를 향한 3D 탄도 궤적 및 지형 충돌 검출
  - └── `DrawCrossMarker`: 지형 차폐 지점에 빨간색 3D X자 마커 표출

---

## 🗂️ 2. 해시 테이블 메서드/구조체 색인표 (Hash Table Index)

| 메서드/구조체 (Key) | 반환형 / 파라미터 | 핵심 역할 & 기능 요약 (Value) | 호출자 (Callers) | 소스 줄 번호 링크 |
| :--- | :--- | :--- | :--- | :--- |
| `ToggleVisualization` | `void ()` | `[I]` 키 및 UI 버튼으로 사거리 시각화 전체 ON/OFF 토글 | `PlayerController.cs` | [RangeVisualizationManager.cs#L82](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs#L82) |
| `UpdateSelection` | `void (squads, units)` | 선택된 원거리 부대 목록 및 자유 유닛 갱신 (자유 유닛은 1마리만 필터링) | `PlayerController.cs` | [RangeVisualizationManager.cs#L125](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs#L125) |
| `SetHoveredEnemy` | `void (enemySquad)` | 마우스 커서가 적 부대 위에 올려졌거나 벗어났을 때 실시간 갱신 | `PlayerController.cs` | [RangeVisualizationManager.cs#L156](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs#L156) |
| `SetGroundTarget` | `void (Vector3? groundPos)` | 어택땅(Z키) 모드 시 마우스 아래 빈 지면 가상 타겟 좌표 갱신 | `PlayerController.cs` | [RangeVisualizationManager.cs#L166](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs#L166) |
| `ShowDragPlacementPreview` | `void (squad, dest, rot)` | 우클릭 드래그 중인 단일 부대의 목표 도착 지점에 사격 호 미리보기 표출 | `PlayerController.cs` | [RangeVisualizationManager.cs#L309](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs#L309) |
| `ShowDragPlacementPreviews` | `void (previewsList)` | 다중 부대 우클릭 드래그 배치 시 모든 부대 예상 도착점에 사격 호 일괄 표출 | `PlayerController.cs` | [RangeVisualizationManager.cs#L321](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs#L321) |
| `ClearDragPlacementPreview` | `void ()` | 우클릭 드래그 배치 완료 또는 취소 시 선행 미리보기 사격 호 해제 | `PlayerController.cs` | [RangeVisualizationManager.cs#L338](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeVisualizationManager.cs#L338) |
| `UpdateArc` | `void (origin, rot, angle, range, minRange, isFullCircle)` | 36x4 격자 방사형 지형 밀착 절차적 메쉬 실시간 생성 | `RangeVisualizationManager.cs` | [RangeArcRenderer.cs#L74](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/RangeArcRenderer.cs#L74) |
| `UpdatePrediction` | `bool (shooter, enemySquad, range, trajMode, arcAngle, isFullCircle)` | 예상 정지 위치, 이동 경로선, 3D 포물선 탄도 및 지형 차폐 검출 | `RangeVisualizationManager.cs` | [TargetTrajectoryPredictor.cs#L98](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/TargetTrajectoryPredictor.cs#L98) |
| `UpdateGroundPrediction` | `bool (shooter, groundPos, range, trajMode, arcAngle, isFullCircle)` | 어택땅 지면 좌표 대상 3D 가상 탄도 및 지형 차폐 검출 | `RangeVisualizationManager.cs` | [TargetTrajectoryPredictor.cs#L119](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/TargetTrajectoryPredictor.cs#L119) |

---

## 🔀 3. 유향 그래프 흐름도 (Directed Graph - Mermaid)

```mermaid
flowchart TD
    A[플레이어 마우스 입력 / 단축키] --> B{입력 이벤트 분기}
    
    B -->|부대 / 유닛 선택 변경| C[UpdateSelection 호출]
    B -->|단축키 [I] 또는 UI 버튼 클릭| D[ToggleVisualization 호출]
    B -->|마우스 적 부대 호버링| E[SetHoveredEnemy 호출]
    B -->|어택땅 Z키 + 지면 호버링| F[SetGroundTarget 호출]
    B -->|우클릭 드래그 대형 배치| G[ShowDragPlacementPreviews 호출]
    
    C --> H[RefreshVisualization]
    D --> H
    E --> H
    F --> H
    G --> H
    
    H --> I{시각화 활성화 여부 & 드래그 여부}
    I -->|드래그 배치 모드| J[도착 예정 좌표에 사격 호 선행 프리뷰 렌더링]
    I -->|일반 선택 모드| K[선택된 모든 원거리 부대 각각 사격 호 렌더링]
    
    K --> L{타겟 존재 여부}
    L -->|적 부대 호버링 or 공격 중| M[TargetTrajectoryPredictor.UpdatePrediction 실행]
    L -->|어택땅 모드 빈 지면 조준| N[TargetTrajectoryPredictor.UpdateGroundPrediction 실행]
    
    M --> O{탄도 궤적 지형 충돌 검사}
    N --> O
    O -->|사선 완전 확보| P[에메랄드 그린 #2ED573 탄도선 표출]
    O -->|지형에 가로막힘| Q[크림슨 레드 #FF2222 탄도선 + 충돌 지점 빨간 X 마커 표출]
```

---

## ⚙️ 4. 주요 상태 변수, 데이터 구조체 & 인스펙터 옵션 (State, Structs & Inspector Fields)

| 필드 / 옵션명 | 타입 | 기본값 | 상세 역할 및 인스펙터 튜닝 설명 |
| :--- | :--- | :--- | :--- |
| `isVisualizationEnabled` | `bool` | `true` | 사거리 시각화 전체 활성화 플래그. `[I]` 키로 런타임 토글 가능. |
| `keepTrajectoryDuringAttack` | `bool` | `true` | 마우스가 적에게서 벗어나도 현재 공격 타겟을 향한 포물선 탄도를 유지할지 여부. |
| `showFillArea` | `bool` | `false` | 사격 호 내부 반투명 채우기 면 활성화 여부 (기본: False, 토탈워 순정 라인 뷰). |
| `fillAlpha` | `float` | `0.05f` | 사격 호 내부 채우기 면의 반투명 알파 강도 (0.01 ~ 0.40). |
| `colorClear` | `Color` | `#2ED573` (에메랄드 그린) | 장애물이 없는 정상 사격 가능 상태의 표준 궤적 및 테두리 색상. |
| `colorBlocked` | `Color` | `#FF2222` (크림슨 레드) | 지형(언덕 등)에 가로막혀 사격이 차단된 위험 경고 표준 색상. |
| `firingArcAngle` | `float` | 총병 `60°`, 궁수 `120°` | `Squad` 및 `Unit`의 인스펙터 필드. 360° 설정 시 전방위 원형 사거리 호 표출. |

---

## 🛠️ 5. 핵심 알고리즘, 물리/전투 수학 공식 & 조작법 (Algorithms, Math & Controls)

### 1) 고해상도 지형 밀착 사격선 알고리즘 (`RangeArcRenderer`)
- **외곽 사격 호 선 (Outer Arc Line)**: 부채꼴 각도 범위에 걸쳐 120개의 조밀한 세그먼트로 지형 높이(`TerrainHeightManager.SampleHeightFast`)를 샘플링하여 지형 표면 위 0.28m에 선명하게 렌더링.
- **부채꼴 좌/우 경계선 (Border Lines)**: 부대 중심에서 외곽 호 양 끝까지 각각 30개의 포인트로 지형을 타고 내려오며 부채꼴의 방향각을 완벽히 형성.
- **선택적 내부 면 메쉬 (Fill Mesh)**: `showFillArea = true` 시에만 활성화되며, 48세그먼트 x 10링 격자와 0.20m 고도 오프셋으로 험준한 산악 지형에서도 지형 파고듦(Clipping) 없이 안정적으로 렌더링.

### 2) 3D 포물선 탄도 및 지형 차폐 검사 공식 (`TargetTrajectoryPredictor`)
- 출발점 P0 = (x0, y0, z0) (사수 발 위치 + 1.4m 어깨 높이)
- 목표점 P1 = (x1, y1, z1) (적 부대 중심 + 1.0m 가슴 높이)
- 수평 거리 D = sqrt((x1 - x0)^2 + (z1 - z0)^2), 고저차 dy = y1 - y0
- 최고점 정점 앙각 높이 Harc:
  - 곡사 (HighArc): Harc = max(2.5m, D * 0.28)
  - 직사 (Flat): Harc = max(0.15m, D * 0.035)
- 매개변수 t (0 ~ 1)에 대한 3D 궤적 방정식:
  - x(t) = x0 + (x1 - x0) * t
  - z(t) = z0 + (z1 - z0) * t
  - y(t) = y0 + dy * t + 4.0 * Harc * t * (1 - t)
- 중간 지점(t: 0.08 ~ 0.92)에서 y(t) <= TerrainHeight(x(t), z(t)) + 0.12m 조건을 만족하면 **지형 차폐(Blocked)**로 판정하여 즉시 크림슨 레드 전환 및 충돌 좌표에 3D X자 마커 렌더링.

### 3) 인게임 전술 조작법 (Controls Table)

| 단축키 / 조작 | 동작 및 전술 피드백 |
| :--- | :--- |
| **원거리 부대 좌클릭 선택** | 선택된 모든 원거리 부대의 발밑에 각각 1:1 사격 호가 지형에 밀착되어 표출 |
| **자유 유닛 1마리 선택** | 단 1마리만 단독 선택했을 때에만 발밑에 사거리 호 표출 (2마리 이상 다중 선택 시 화면 정돈을 위해 자동 숨김) |
| **적 부대 마우스 오버 (호버링)** | 사거리 밖일 경우 예상 전진 지점까지의 이동선 + 정지 사격 위치 사격 호 + 목표까지의 3D 포물선 탄도선 표출 |
| **우클릭 공격 명령** | 공격 명령 후 마우스 커서가 벗어나도 타겟을 향한 포물선 탄도선 계속 유지 |
| **지형에 막힌 적 공격 명령** | 사선이 트이는 위치(언덕 위/트인 곳)까지 부대가 자동으로 전진하여 사격 개시 |
| **`[Z]` 어택땅 모드 + 지면 호버링** | 빈 지면을 조준하고 있을 때 해당 좌표를 향한 가상 탄도 궤적 및 지형 차폐 여부 표시 |
| **우클릭 드래그 배치** | 마우스를 떼기 전, 도착 예정 위치와 회전각에 맞춰 실시간으로 사격 호 선행 프리뷰 투영 |
| **`[I]` 키 / UI 버튼** | 전투 집중을 위해 사거리 시각화 오버레이 전체를 즉시 ON / OFF 토글 |

---

## 🔗 6. 다른 스크립트와의 연동 관계 (Dependencies & Pipeline)

- **`PlayerController.cs`**:
  - `Update()` 매 프레임 `HandleRangeVisualization()` 호출하여 적 부대 호버링 및 어택땅 지면 호버링 좌표 전달.
  - `[I]` 키 입력 및 `CommandUI` 버튼 클릭 시 `ToggleVisualization()` 호출.
  - 우클릭 드래그 시 `ShowDragPlacementPreviews`, 우클릭 Up 시 `ClearDragPlacementPreview` 호출.
  - 선택 상태 변경 시 `UpdateSelection` 호출.
- **`TerrainHeightManager.cs`**:
  - `SampleHeightFast(x, z)`를 호출하여 사격 호 메쉬 정점 고도화 및 3D 탄도 궤적의 지형 충돌 여부를 초고속 계산.
- **`Squad.cs` & `Unit.cs`**:
  - `firingArcAngle` 필드를 제공하여 총병(60°), 궁수(120°), 전방위 원형(360°) 등 병과 설정에 따라 동적 반응.
  - 사선 차폐 시 최적 사거리 전진 정지 후 사격 명령 수행.
