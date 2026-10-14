# 💡 UnitTargetSearchSystem.cs 핵심 요약 & 역할 (Summary & Role)

> **원본 소스 파일**: [UnitTargetSearchSystem.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/ECS/Systems/UnitTargetSearchSystem.cs)

**이 시스템은 목표 부대가 명시적으로 지정되지 않은 유닛(대기 중 부대원 및 자유 유닛)의 인접 적군 락온 레이더망을 담당합니다.**  
2.0m 균일 격자 공간 분할(`SpatialHashGridSystem.SpatialMap`)을 기반으로, 0.1초(10Hz) 주기마다 병렬 잡(`SearchEnemyTargetGridJob`)을 가동하여 인접 적군 엔티티를 O(1)에 준하는 속도로 찾아내어 `combat.RadarTargetEntity`에 갱신합니다.

2026-10-14 업데이트를 통해, 기존 14m 고정 반경이었던 레이더를 원거리 사수(`combat.IsRangedUnit == 1`)에 한해 사거리에 맞춰 **최대 65m까지 동적으로 확장**하여, 자유 원거리 유닛 및 사격 대치 상태의 병사들이 원거리 적을 즉시 포착하고 사격할 수 있도록 개선되었습니다.

---

## 🌳 1. 아키텍처 트리 맵 (Tree Map)

- 📁 **UnitTargetSearchSystem (ISystem)**
  - `OnCreate()`: 시스템 초기화 및 UnitEntityTag 쿼리 등록
  - `OnUpdate()`: 0.1초(10Hz) 주기 타이머 검사 및 잡 스케줄링
  - 📁 **SearchEnemyTargetGridJob (IJobEntity, Parallel)**
    - 목표 부대 지정 유닛(`TargetSquadId != -1`) 및 사망 유닛 즉시 제외 최적화
    - 병과별 동적 탐색 반경 결정 (근접: 14m / 원거리: 최대 65m)
    - 2.0m 격자 셀 다중 해시 순회 (`SpatialHashGridSystem.HashCoords`)
    - 정규 부대원 시선 정면 축 필터링 (`forwardDot > 0.25f`) vs 자유 유닛 360도 전방위 탐색
    - 최단 거리 적군 엔티티 `combat.RadarTargetEntity` 락온 갱신

---

## 🗂️ 2. 해시 테이블 메서드/구조체 색인표 (Hash Table Index)

| Key (메서드/구조체) | 반환형 / 파라미터 | 역할 및 의존성 | 소스 줄 번호 링크 |
| :--- | :--- | :--- | :--- |
| `UnitTargetSearchSystem` | `partial struct : ISystem` | 10Hz 주기 타이머 제어 및 탐색 잡 스케줄러 | [UnitTargetSearchSystem.cs#L14](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/ECS/Systems/UnitTargetSearchSystem.cs#L14) |
| `SearchEnemyTargetGridJob` | `partial struct : IJobEntity` | 공간 해시 기반 인접 적군 레이더 탐색 코어 잡 | [UnitTargetSearchSystem.cs#L51](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/ECS/Systems/UnitTargetSearchSystem.cs#L51) |
| `Execute` | `void (Entity, in Tag, in Mov, ref Combat)` | 개별 유닛 병과별 사거리 기반 적군 탐색 및 락온 | [UnitTargetSearchSystem.cs#L56](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/ECS/Systems/UnitTargetSearchSystem.cs#L56) |

---

## 🔀 3. 유향 그래프 흐름도 (Directed Graph - Mermaid)

```mermaid
flowchart TD
    A[OnUpdate: 0.1초 10Hz 주기 타이머] --> B{사망 여부 OR TargetSquadId != -1}
    B -->|Yes| C[레이더 생략: RadarTargetEntity = Entity.Null]
    B -->|No| D[병과별 레이더 반경 결정]
    
    D -->|근접 보병| E[radarRange = 14m, searchCells = 7]
    D -->|원거리 사수 & 탄약 보유| F[radarRange = clamp(RangedAttackRange, 14m, 65m), searchCells = ceil(radarRange * 0.5)]
    
    E --> G[myCoord 기준 2.0m 셀 그리드 루프 순회]
    F --> G
    
    G --> H{Faction != myFaction & 생존 적군}
    H -->|통과| I{정규 부대원 여부: tag.IsFreeUnit == 0}
    I -->|정규 부대원| J{forwardDot > 0.25f 정면 각도}
    I -->|자유 유닛| K[360도 전방위 허용]
    J -->|통과| L[최단 거리 minSqrDist 갱신]
    K --> L
    
    L --> M[최종 combat.RadarTargetEntity 저장]
```

---

## ⚙️ 4. 주요 상태 변수, 데이터 구조체 & 인스펙터 옵션 (State, Structs & Inspector Fields)

| 변수명 | 타입 | 기본값 | 상세 역할 & 튜닝 가이드 |
| :--- | :--- | :--- | :--- |
| `searchTimer` | `float` | `0f` | 레이더 잡 실행 주기 타이머 (0.1초 = 10Hz 간격으로만 실행하여 CPU 점유율 최소화) |
| `radarRange` | `float` | `14.0f` | 기본 근접 보병 적군 감지 반경 (14m) |
| `dynamicRange` | `float` | `14.0f ~ 65.0f` | 원거리 유닛의 사거리에 비례한 동적 레이더 반경 (`math.clamp(combat.RangedAttackRange, 14.0f, 65.0f)`) |
| `searchCells` | `int` | `7 ~ 33` | 2.0m 셀 기준 탐색 반경 셀 개수 (`(int)math.ceil(radarRange * InvCellSize)`) |
| `minSqrDist` | `float` | `radarRange^2` | 레이더 범위 내 최단 거리 비교용 거리 제곱 임계값 |
| `RadarTargetEntity` | `Entity` | `Entity.Null` | 레이더가 탐지한 최단 거리 인접 적군 엔티티 (전투 시스템에서 참조) |

---

## 🛠️ 5. 핵심 알고리즘, 물리/전투 수학 공식 & 조작법 (Algorithms, Math & Controls)

### (1) 병과별 사거리 비례 동적 레이더 반경 산출식
* **근접 보병**: $R = 14.0\text{ m}$, $Cells = 7$ (2.0m 격자 기준 15×15 = 225셀)
* **원거리 사수**: 
  $$R = \text{clamp}(RangedAttackRange, 14.0\text{ m}, 65.0\text{ m})$$
  $$Cells = \lceil R \times \text{InvCellSize} \rceil$$
  * 활/총의 사거리가 50m인 경우 $Cells = 25$로 확장되어, 50m 전방의 자유 유닛 및 적군을 즉시 락온 가능.

### (2) 정규 부대 정면 시선 필터링 vs 자유 유닛 360도 전방위 탐색
* **정규 부대원 (`tag.IsFreeUnit == 0`)**:
  $$\vec{Fwd} = Rotation \times (0, 0, 1)$$
  $$\vec{Diff} = Pos_{enemy} - Pos_{my}$$
  $$\cos\theta = \vec{Fwd} \cdot \frac{\vec{Diff}}{\|\vec{Diff}\|} > 0.25\quad (\approx 75.5^\circ \text{ 이내})$$
  측면 및 후방의 다른 적 부대에 한눈팔지 않고 정면 대형을 사수하도록 보장.
* **자유 유닛 (`tag.IsFreeUnit == 1`)**:
  시선 각도 제한을 전면 해제하여, 전후좌우 360도 전방위에서 다가오는 적을 즉시 감지하고 사격 및 대응.

---

## 🔗 6. 다른 스크립트와의 연동 관계 (Dependencies & Pipeline)

*   **`SpatialHashGridSystem.cs`**:
    *   `SpatialMap`을 읽기 전용(`[ReadOnly]`)으로 참조하여 2.0m 균일 격자에서 인접 적군 정보를 O(1)로 조회.
*   **`UnitCombatAndMovementSystem.cs`**:
    *   이 시스템이 산출한 `combat.RadarTargetEntity`를 전달받아, 목표 부대가 없는 유닛(자유 유닛 및 대기 상태 부대원)이 2단계 레이더 타겟팅으로 활용하여 원거리 제자리 사격(Stand & Fire) 및 교전을 수행.
