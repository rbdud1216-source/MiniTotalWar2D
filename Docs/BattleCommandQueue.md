# 📜 [BattleCommandQueue.cs] 전술 명령 큐 & 멀티플레이 확장 레이어 명세서

> **원본 소스 파일**: 
> - [BattleCommand.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommand.cs) (명령 구조체 및 열거형 정의)
> - [BattleCommandQueue.cs](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs) (명령 큐 매니저)

---

## 💡 핵심 요약 & 역할 (Summary & Role)

`BattleCommandQueue`는 플레이어 조작(`PlayerController`, `CommandUIManager`) 및 향후 전술 AI와 실제 부대 실행 로직(`Squad`) 사이에 위치하는 **전술 명령 큐(Command Queue) 중계 레이어**입니다.

1. **싱글플레이 무지연 조작 (0ms Direct Execution)**:
   - 마우스 클릭 및 핫키 입력 시 명령을 구조체(`BattleCommand`)로 큐에 인큐한 후 즉시 실행하여 지연 없는 쾌적한 전술 조작감을 제공합니다.
2. **향후 멀티플레이 락스텝 완벽 대비 (Lockstep Ready)**:
   - 모든 명령이 직렬화 가능한 정형 데이터로 캡슐화되어 있어, 멀티플레이 활성화 시 `INetworkCommandDispatcher`를 통해 패킷 송수신 및 고정 시뮬레이션 틱(Tick) 단위 동기화로 1:1 확장이 가능합니다.
3. **명령 히스토리 및 리플레이 기반 (History & Replay)**:
   - 실행된 모든 전술 명령이 순서대로 누적 기록되어 리플레이 시스템 구축 및 디버깅을 지원합니다.

---

## 🌳 1. 아키텍처 트리 맵 (Tree Map)

```text
BattleCommandQueue Subsystem
├── 1. Data Structures (BattleCommand.cs)
│   ├── BattleCommandType (이동, 공격, 정지, 태세, 진형, 토글 등 13종)
│   └── BattleCommand Struct (Blittable 직렬화 가능 명령 패킷)
├── 2. Core Queue & Dispatcher (BattleCommandQueue.cs)
│   ├── Singleton LifeCycle (Awake, Instance 자동 보장)
│   ├── Tick Management (Fixed Tick Counter: 초당 20Hz 갱신)
│   ├── Pending Queue & Command History (최근 100개 명령 보관)
│   └── Network Dispatcher Binding (INetworkCommandDispatcher)
├── 3. Command Execution Engine
│   ├── ProcessPendingCommands (큐 일괄 디큐 및 실행)
│   ├── ExecuteCommand (타입별 Squad 메서드 라우팅)
│   └── ExecuteMoveInternal (단일/복수 부대 횡대/군단 이동)
└── 4. Client Issue Interface (Convenience Methods)
    ├── IssueMoveCommand / IssueMinimapCommand
    ├── IssueAttackSquadCommand / IssueAttackPositionCommand
    └── IssueStance / Run / AutoAttack / FireAtWill / Formation Commands
```

---

## 🗂️ 2. 해시 테이블 메서드/구조체 색인표 (Hash Table Index)

| Key (메서드 / 구조체) | 분류 | 매개변수 및 반환형 | 핵심 역할 & 사용처 | 소스 줄 번호 링크 |
| :--- | :--- | :--- | :--- | :--- |
| `BattleCommandType` | `enum` | 13종 열거형 값 | 전술 명령 종류 분류 (Move, AttackSquad, Stop 등) | [BattleCommand.cs#L6-L22](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommand.cs#L6-L22) |
| `BattleCommand` | `struct` | `commandId`, `playerId`, `targetSquadIds` 등 | 직렬화 가능한 단일 전술 명령 데이터 모델 | [BattleCommand.cs#L28-L77](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommand.cs#L28-L77) |
| `INetworkCommandDispatcher` | `interface` | `void SendCommand(BattleCommand)` | 멀티플레이 패킷 송신 모듈 연결 인터페이스 | [BattleCommandQueue.cs#L8-L11](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L8-L11) |
| `EnqueueCommand` | `method` | `(BattleCommand) -> void` | 명령 큐에 신규 명령 삽입 및 즉시/틱 처리 | [BattleCommandQueue.cs#L121-L138](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L121-L138) |
| `ReceiveRemoteCommand` | `method` | `(BattleCommand) -> void` | 멀티플레이 수신 패킷을 로컬 큐에 주입 | [BattleCommandQueue.cs#L143-L146](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L143-L146) |
| `ProcessPendingCommands` | `method` | `() -> void` | 대기 큐의 모든 명령을 순차적으로 디큐하여 실행 | [BattleCommandQueue.cs#L151-L173](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L151-L173) |
| `ExecuteCommand` | `method` | `(BattleCommand) -> void` | 명령 타입에 따라 실제 `Squad`의 전술 함수 호출 | [BattleCommandQueue.cs#L178-L300](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L178-L300) |
| `IssueMoveCommand` | `method` | `(IReadOnlyList<Squad>, Vector3, ...) -> void` | 외부 클라이언트용 이동 명령 간편 발행 | [BattleCommandQueue.cs#L379-L396](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L379-L396) |
| `IssueAttackSquadCommand` | `method` | `(IReadOnlyList<Squad>, Squad, bool) -> void` | 외부 클라이언트용 적 부대 타겟 공격 발행 | [BattleCommandQueue.cs#L398-L413](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L398-L413) |
| `IssueAttackPositionCommand` | `method` | `(IReadOnlyList<Squad>, Vector3, bool) -> void` | 외부 클라이언트용 어택땅 공격 이동 발행 | [BattleCommandQueue.cs#L415-L430](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L415-L430) |
| `IssueStopCommand` | `method` | `(IReadOnlyList<Squad>) -> void` | 외부 클라이언트용 제자리 정지 명령 발행 | [BattleCommandQueue.cs#L432-L444](file:///c:/unityProject/MiniTotalWar2D/Assets/Scripts/BattleCommandQueue.cs#L432-L444) |

---

## 🔀 3. 유향 그래프 흐름도 (Directed Graph - Mermaid)

```mermaid
flowchart TD
    subgraph InputLayer [입력 레이어]
        A1[PlayerController 마우스/키보드] --> B[BattleCommand 생성]
        A2[CommandUIManager UI 버튼 클릭] --> B
        A3[전술 AI 또는 원격 네트워크] --> B
    end

    subgraph QueueLayer [명령 큐 매니저 (BattleCommandQueue)]
        B --> C{네트워크 모드 활성화?}
        C -- Yes --> D[INetworkCommandDispatcher 패킷 송신]
        C -- No (싱글플레이) --> E[PendingCommands 큐에 Enqueue]
        D -. 상대방 수신 .-> F[ReceiveRemoteCommand] --> E

        E --> G[ProcessPendingCommands]
        G --> H[ExecuteCommand 디스패처]
    end

    subgraph SimulationLayer [부대 실행 레이어]
        H --> I1[Squad.CommandMoveWithFormation]
        H --> I2[Squad.CommandAttackSquad / MeleeCharge]
        H --> I3[Squad.CommandStop]
        H --> I4[Squad.SetFormationType / Stance / Loose]
    end
```

---

## ⚙️ 4. 주요 상태 변수, 데이터 구조체 & 인스펙터 옵션

| 필드 / 변수명 | 타입 | 기본값 | 한글 인스펙터 라벨 및 툴팁 가이드 |
| :--- | :--- | :--- | :--- |
| `isNetworkMode` | `bool` | `false` | **네트워크 멀티플레이 모드**: 체크 시 로컬 즉시 실행 대신 네트워크 디스패처를 통해 패킷을 동기화합니다. |
| `tickRate` | `float` | `20f` | **초당 틱 빈도 (Hz)**: 락스텝 시뮬레이션의 초당 고정 틱 수입니다. (기본 20Hz = 50ms마다 1틱) |
| `currentTick` | `uint` | `0` | **현재 시뮬레이션 틱**: 게임 시작 후 누적된 고정 시뮬레이션 틱 번호입니다. |
| `totalCommandsProcessed` | `uint` | `0` | **누적 처리 명령 수**: 지금까지 큐를 거쳐 실행된 총 전술 명령 개수입니다. |
| `recentCommandHistory` | `List<BattleCommand>` | - | 최근 실행된 명령들을 최대 100개까지 보관하는 링 버퍼입니다. |

---

## 🛠️ 5. 핵심 알고리즘, 물리/전투 수학 공식 & 조작법

### 1) 군단 중심점(Centroid) 및 상대 오프셋 회전 공식
복수 부대가 선택된 상태에서 회전 각도를 포함한 이동 명령을 내릴 때, 군단 고정(Locked Group) 여부에 따라 다음 공식을 거쳐 개별 부대 목표 좌표가 산출됩니다:

* **군단 중심점**:
  `Centroid = Sum(squad[i].Position) / N`
* **부대별 상대 벡터**:
  `relOffset[i] = squad[i].Position - Centroid`
* **군단 회전 변환 (Locked Group 시)**:
  `deltaRot = targetRot * Inverse(squad[0].Rotation)`
  `finalOffset[i] = deltaRot * relOffset[i]`
  `targetSquadPos[i] = centerPos + finalOffset[i]`
  `finalSquadRot[i] = deltaRot * squad[i].Rotation`

### 2) 원거리 부대 사거리 기반 자동 전진/정지 판정 (AttackPosition)
사격 부대가 특정 지점에 공격 이동(`AttackPosition`)할 때:
* 목표 지점과의 거리 `dist <= maxRange * 0.90f`인 경우:
  `CommandRangedHaltAndFire(faceRot, null)` -> 제자리에서 즉시 사격 대형 전개
* 목표 지점과의 거리 `dist > maxRange * 0.90f`인 경우:
  `standPos = targetWorldPos - (dir.normalized * (maxRange * 0.70f))`
  `CommandMoveWithFormation(standPos, faceRot, ...)` -> 안전 사거리 내로 전술 전진

---

## 🔗 6. 다른 스크립트와의 연동 관계 (Dependencies & Pipeline)

- **[PlayerController.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/PlayerController.md)**: 마우스 우클릭, 드래그 및 핫키 조작 결과를 `BattleCommandQueue.Instance.Issue*` 메서드를 통해 발행합니다.
- **[Squad.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/Squad.md)**: 큐에서 디스패치된 최종 전술 명령을 수신하여 대형 슬롯 정렬, 물리 이동, 원거리 일제사격, 백병전 돌격을 수행합니다.
- **[BattleManager.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/BattleManager.md)**: 게임 시작 시 `BattleCommandQueue` 싱글톤의 존재를 보장하며, 전장 내 모든 부대 목록(`GetAllSquads`)을 제공합니다.
- **[CommandUIManager.cs](file:///c:/unityProject/MiniTotalWar2D/Docs/CommandUI.md)**: UI 버튼 클릭 액션을 커맨드 큐에 중계합니다.
