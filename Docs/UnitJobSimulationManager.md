# ⚡ [UnitJobSimulationManager.cs] C# Job System 멀티코어 시뮬레이터 아키텍처 가이드

> **원본 소스 파일**: [UnitJobSimulationManager.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs) (총 줄 수: 946줄)

---

## 💡 1. 핵심 요약 & 역할
수천~수만 기의 대규모 전장 유닛을 CPU 멀티코어(Unity C# Job System + Burst Compiler)로 병렬 시뮬레이션하여, **물리 충돌 척력(Separation), 최근접/지정 목표 탐색(Targeting), 넉백 및 백병전 전투(Combat), Transform 좌표 갱신**을 일괄 처리하는 최고 성능의 시뮬레이션 매니저입니다.

---

## 🌳 2. 아키텍처 트리 맵 (Architecture Tree Map)

- 📁 **1. 생명주기 및 메모리 관리 (Lifecycle & Native Memory)**
  - `Awake()`, `OnDestroy()`, `OnApplicationQuit()`
  - `AllocateNativeArrays()`, `DisposeNativeArrays()`: GC 부하 없는 `NativeArray` 버퍼 할당/해제
  - `RegisterUnit()`, `UnregisterUnit()`: 3D 유닛 라이프사이클 등록/제거
  - `RebuildSimulationBuffers()`: 유닛 수 변경 시 데이터 보존 및 버퍼 재구축
- 📁 **2. 데이터 브릿지 및 실시간 동기화 (Data Bridge & Sync)**
  - `UpdateUnitTargetPosition()`: 부대 슬롯 좌표/상태 변경 실시간 주입
  - `UpdateUnitSpeedAndAccel()`: 이동 속도 및 가속도 주입
  - `UpdateSquadTargetSquadId()`: 지휘관의 목표 적 부대 ID(`targetSquadId`) 일괄 주입
  - `Update()` (MainThread 루프): 매 프레임 생존/체력/속도/소속 부대 실시간 동기화
- 📁 **3. 멀티코어 병렬 Job 파이프라인 (Multithreaded Job Pipeline)**
  - 📂 **Job 1: `CalculateSeparationJob` (`IJobParallelFor`)**
    - 유닛 간 반경 겹침 및 침범 척력(`separationForces`) 병렬 연산 (최대 5.0f 클램핑)
  - 📂 **Job 2: `EnemySearchJob` (`IJobParallelFor`)**
    - 지정 목표 적 부대(`targetSquadId`) 100% 격리 탐색 + 1.45m 코앞 자기방어 반격
  - 📂 **Job 3: `UnitMovementAndCombatJob` (`IJobParallelForTransform`)**
    - 200명 전원 쇄도 이동, 침범 차단 및 틈새 슬라이딩 굴절, 넉백 물리, 백병전 타격

---

## 🗂️ 3. 해시 테이블 메서드 및 구조체 색인표 (Fast-Index Table)

> ⚡ **에이전트 지침**: 코드 수정 전 아래 색인표에서 줄 번호 링크를 확인하고, `view_file`로 해당 범위만 직접 조회하여 작업하십시오.

| 식별자 (Key) | 종류 / 반환형 | 핵심 역할 & 기능 요약 (Value) | 호출자 / 실행 지점 | 소스 줄 번호 (Line Range) |
| :--- | :--- | :--- | :--- | :--- |
| `UnitJobData` | `struct (Blittable)` | 멀티코어 연산용 32개 필드 순수 값 타입 유닛 데이터 | 전체 Job 공유 | [UnitJobSimulationManager.cs#L12-L44](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L12-L44) |
| `AllocateNativeArrays` | `void (int count)` | NativeArray(Data, Pos, Sep, Target) 메모리 버퍼 생성 | `RebuildSimulationBuffers` | [UnitJobSimulationManager.cs#L125-L136](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L125-L136) |
| `RegisterUnit` | `void (Unit unit)` | 유닛 생성 시 시뮬레이션 배열에 등록 및 버퍼 확장 | `Unit.Start()` | [UnitJobSimulationManager.cs#L138-L150](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L138-L150) |
| `UnregisterUnit` | `void (Unit unit)` | 유닛 사망/삭제 시 시뮬레이션 목록에서 안전 제거 | `Unit.OnDestroy()` | [UnitJobSimulationManager.cs#L152-L185](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L152-L185) |
| `UpdateUnitTargetPosition` | `void (unit, dest, rot, state)` | 부대 이동 명령 시 개별 유닛의 목표 위치/상태 전달 | `Squad.cs`, `Unit.cs` | [UnitJobSimulationManager.cs#L279-L305](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L279-L305) |
| `UpdateSquadTargetSquadId` | `void (squad, targetId)` | 부대 지휘관이 지정한 목표 적 부대 ID 일괄 갱신 | `Squad.SyncTargetSquadId` | [UnitJobSimulationManager.cs#L325-L345](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L325-L345) |
| `Update` | `void ()` | 매 프레임 유닛 상태 동기화 및 3대 Job 스케줄링 | Unity Engine Loop | [UnitJobSimulationManager.cs#L347-L426](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L347-L426) |
| `CalculateSeparationJob` | `struct : IJobParallelFor` | 유닛 간 겹침 방지 밀어내기 척력 병렬 연산 | `Job 1` | [UnitJobSimulationManager.cs#L439-L498](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L439-L498) |
| `EnemySearchJob` | `struct : IJobParallelFor` | 지정 목표 부대 100% 추격 및 1.45m 코앞 자율 반격 | `Job 2` | [UnitJobSimulationManager.cs#L504-L613](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L504-L613) |
| `UnitMovementAndCombatJob` | `struct : IJobParallelForTransform` | 이동, 측면 굴절 슬라이딩, 넉백, 백병전 타격 일괄 실행 | `Job 3` | [UnitJobSimulationManager.cs#L615-L945](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/UnitJobSimulationManager.cs#L615-L945) |

---

## 🔀 4. 유향 그래프 흐름도 (Data & Call Flow Graph)

### 1) 매 프레임 멀티코어 Job 실행 파이프라인
```mermaid
flowchart TD
    A[Update 시작: 이전 프레임 멀티스레드 Job 완료 대기] --> B[메인 스레드: 체력/소속 부대/목표 부대 ID 실시간 동기화]
    B --> C[1단계 Job: 유닛 간 충돌 방지 소프트 척력 연산 (CalculateSeparationJob)]
    C --> D[2단계 Job: 지정 목표 부대 100% 탐색 및 1.45m 코앞 자율 반격 (EnemySearchJob)]
    D --> E[3단계 Job: 이동, 틈새 슬라이딩, 넉백, 백병전 타격 및 좌표 갱신 (UnitMovementAndCombatJob)]
    E --> F[LateUpdate: 메인 스레드 렌더링 프레임 동기화 완료]
```

### 2) 유닛 이동 및 물리 척력 합성 흐름
```mermaid
flowchart TD
    A[목표 이동 좌표 산출 (targetDest)] --> B[희망 이동량 계산 (desiredMove = 방향 * 속도 * dt)]
    B --> C{전방 침범 척력 발생 여부 판정 (Dot > 0)}
    C -- 겹침 감지 시 --> D[전방 침범 속도 성분 100% 강제 소거 (유닛 겹침 Stacking 원천 방지)]
    D --> E[측면 굴절 벡터 합성: 앞사람 좌우 틈새로 미끄러져 전진 (Deflection)]
    C -- 겹침 없을 시 --> F[기존 희망 이동량 (desiredMove) 유지]
    E --> G[최종 프레임 이동량 = 희망이동 + 척력이동 + 넉백이동]
    F --> G
    G --> H[유닛 3D 월드 좌표 갱신 (Transform.position)]
```

---

## ⚙️ 5. 핵심 데이터 구조체 (`UnitJobData`) 상세 명세

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct UnitJobData
{
    public int isPlayer;              // 진영 구분 (1 = 플레이어 아군, 0 = 적군 AI)
    public int isAlive;               // 생존 상태 (1 = 생존, 0 = 사망)
    public int currentState;          // 지휘 상태 (0: Idle, 1: Move, 2: AttackMove, 3: MeleeEngaged)
    public int targetIndex;           // 현재 교전/추격 대상 유닛 인덱스 (-1 = 없음)
    public Vector3 targetPosition;    // 부대 슬롯 또는 목표 이동 좌표
    public Quaternion targetRotation; // 최종 목표 회전각
    public float currentHp;           // 현재 체력
    public float maxHp;               // 최대 체력 (기본 100)
    public float damage;              // 공격력 (기본 10)
    public float attackCooldown;      // 공격 쿨다운 (초 단위, 기본 1.0s)
    public float lastAttackTime;      // 마지막 공격 시점
    public float moveSpeed;           // 기본 이동 속도 (걷기 1.2m/s, 달리기 2.8m/s)
    public float currentSpeed;        // 가속도 보간이 적용된 현재 실제 속도
    public float acceleration;        // 가속도 (기본 5.0m/s², 돌격 8.0m/s²)
    public float stoppingDistance;    // 정지 목표 거리 (0.1m)
    public float detectRange;         // 감지 범위 (12.0m)
    public float attackRange;         // 유효 타격 사거리 (1.45m)
    public float personalRadius;      // 개인 반경 (부대 유닛 0.70m, 자유 유닛 1.15m)
    public int autoAttackEnabled;     // 자동 요격 플래그 (1 = ON, 0 = OFF)
    public int isFreeUnit;            // 자유 유닛 여부 (1 = 자유, 0 = 부대)
    public int squadId;               // 소속 부대 인스턴스 ID (-1 = 자유 유닛)
    public int targetSquadId;         // 지휘관이 지정한 목표 적 부대 ID (-1 = 미지정)
    public float mass;                // 유닛 질량/무게 (kg, 기본 100kg)
    public float chargeSpeed;         // 돌격 속도 (4.8m/s)
    public float chargeBonus;         // 돌격 피해 보너스 계수
    public float maxChargeDamage;     // 돌격 피해 최대 상한치 (35)
    public int chargeImpactReady;     // 돌격 충격 장전 여부 (1 = 장전, 0 = 방전)
    public Vector3 knockbackVelocity; // 넉백/충격 물리 속도
}
```

---

## 🛠️ 6. 3대 멀티코어 Job의 물리 및 전투 알고리즘

### 1. `CalculateSeparationJob` (소프트 물리 척력)
- **공식**: 유닛 간 거리 $d < (r_A + r_B)$ 일 때, 침범 깊이 $\Delta = (r_A + r_B) - d$에 비례하여 밀어내는 척력 벡터를 누적합니다:
  $$\vec{F}_{sep} = \sum \frac{\vec{p}_A - \vec{p}_B}{d} \times \left(1.0 - \frac{d}{r_A + r_B}\right) \times 5.0$$
- 최대 크기를 5.0f로 클램핑하여 유닛이 튕겨 나가는 불안정 현상을 방지합니다.

### 2. `EnemySearchJob` (타겟팅 및 1.45m 코앞 자율 반격)
- **지정 목표 추격**: 지휘관이 지정한 `targetSquadId != -1`일 때는 전장의 다른 적 부대를 일절 무시하고 오직 해당 적 부대원만 100% 추격합니다.
- **코앞 1.45m 자기방어 반격**: 이동 중이라도 1.45m 이내로 스쳐 지나가는 적이 칼을 찌르면 즉시 그 적을 최우선 타격 대상으로 전환하여 자신을 방어합니다.

### 3. `UnitMovementAndCombatJob` (이동, 굴절 슬라이딩, 넉백, 백병전)
- **유닛 겹침(Stacking) 방지**: 전방 진행 방향과 척력의 내적($\vec{v} \cdot (-\vec{n}_{sep}) > 0$) 시 침범 속도 성분을 강제로 소거합니다.
- **측면 굴절 벡터(Deflection)**: 앞사람의 틈새로 파고들도록 접선 벡터($\vec{t} = (-n_z, 0, n_x)$)를 합성하여 200명 전원이 정체 없이 전진합니다.
- **돌격 및 질량 비례 넉백**:
  $$Damage_{final} = \min(Damage + Bonus \times \frac{v}{v_{charge}} \times \frac{Mass_A}{100}, Damage_{max})$$
  $$v_{knockback} = 4.5 \times \frac{v}{v_{charge}} \times \frac{Mass_A}{\max(10, Mass_B)}$$
