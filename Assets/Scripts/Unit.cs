using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Unit : MonoBehaviour
{
    [Header("유닛 기본 설정")]
    [Tooltip("플레이어 진영 여부 (체크 시 아군 Player, 해제 시 적군 Enemy)")]
    public bool isPlayer = false;

    [Tooltip("유닛 최대 체력 (기본: 100)")]
    public float maxHp = 100f;

    [Tooltip("유닛 실시간 현재 체력 (게임 시작 시 최대 체력으로 자동 초기화)")]
    public float currentHp = 100f;

    [Tooltip("유닛 기본 방어력 (0 ~ 10000 정수 입력, 10000 = 100.00% 완전 방어. 예: 2812 입력 시 28.12% 피해 감쇄)")]
    [Range(0, 10000)]
    public int armor = 0;

    [System.NonSerialized] public int baseArmor = 0;
    [System.NonSerialized] public float baseMass = 100f;
    [System.NonSerialized] public float baseAttackCooldown = 1.0f;

    [Tooltip("일반 백병전 1회 타격 공격력")]
    public float damage = 10f;

    [Tooltip("공격 쿨다운 주기 (초 단위, 수치가 작을수록 빠르게 공격)")]
    public float attackCooldown = 1.0f;

    [Tooltip("공격 유효 사거리 (미터 단위, 기본 보병: 1.45m, 장창병: 2.5m+, 사격 유닛 확장 가능)")]
    public float attackRange = 1.45f;

    [Tooltip("주무기 최소 사거리 (m 단위, 0이면 최소사거리 제한 없음. 검병 세팅: 0m, 장창병 세팅: 1.2m~1.5m 권장)")]
    public float minAttackRange = 0f;

    [Tooltip("주무기 최적 사거리(스위트스팟) 최소 기준거리 (m 단위, 이 거리 이상에서 100% 정상 데미지 적용. 0이면 거리 감쇠 없음. 검병 세팅: 0m, 장창병 세팅: 1.8m~2.0m 권장)")]
    public float optimalRangeMin = 0f;

    [Tooltip("최적 사거리 미만(품 안)으로 파고든 적에게 가하는 주무기 피해량 비율 (0.1 ~ 1.0, 1.0=감쇠 없음. 검병 세팅: 1.0, 장창병 세팅: 0.35 권장)")]
    [Range(0.1f, 1.0f)]
    public float closeRangeDamageRatio = 1.0f;

    [Tooltip("주무기 타격 및 백병전 시 적을 밀쳐내는 넉백 세기 배율 (기본: 1.0, 검병 세팅: 1.0, 장창병 세팅: 2.0~2.5 권장)")]
    public float knockbackPower = 1.0f;

    [Tooltip("돌격이 아닌 일반 백병전 평타(창 찌르기, 칼질) 시 발생하는 기본 저지 넉백 속도(m/s)입니다. 수치가 낮을수록 적이 덜 밀려나며 창벽을 파고들기 쉬워집니다. (기본: 0.45m/s)")]
    public float baseMeleeKnockback = 0.45f;

    [Tooltip("여러 아군이 동시에 한 명의 적을 집중 공격할 때, 피격 유닛에게 누적될 수 있는 평타 넉백 물리 속도의 최대 상한선(m/s)입니다. 수십 미터 튕겨나가는 것을 원천 차단합니다. (기본: 1.2m/s, 밀림 거리 약 9~15cm)")]
    public float maxMeleeKnockbackCap = 1.2f;

    [Header("보조무기 (Sidearm) 설정")]
    [Tooltip("적이 일정 거리 이내로 밀착 시 보조무기로 자동 전환 여부 (검병 세팅: 체크 해제, 장창병 세팅: 체크 권장)")]
    public bool useSidearm = false;

    [Tooltip("보조무기로 전환하는 적과의 거리 기준 (m 단위, 권장: 1.2m)")]
    public float sidearmSwitchDistance = 1.2f;

    [Tooltip("보조무기 유효 공격 사거리 (m 단위, 권장: 1.0m)")]
    public float sidearmAttackRange = 1.0f;

    [Tooltip("보조무기 1회 타격 공격력 (권장: 4.0f)")]
    public float sidearmDamage = 4.0f;

    [Tooltip("보조무기 공격 쿨다운 주기 (초 단위, 빠를수록 연타. 권장: 0.8s)")]
    public float sidearmAttackCooldown = 0.8f;

    [Tooltip("보조무기 타격 시 적을 밀쳐내는 넉백 배율 (기본: 0.1 권장)")]
    public float sidearmKnockbackPower = 0.1f;

    [Tooltip("보조무기 평타 타격 시 발생하는 기본 저지 넉백 속도(m/s)입니다. 보조무기는 품 안에서 호신용으로 휘두르므로 밀치는 힘이 매우 미약합니다. (기본: 0.2m/s)")]
    public float sidearmBaseKnockback = 0.2f;

    [Tooltip("여러 아군이 보조무기로 동시에 한 명의 적을 집중 타격할 때 누적될 수 있는 넉백 속도의 최대 상한선(m/s)입니다. (기본: 0.8m/s, 밀림 거리 약 5cm)")]
    public float sidearmMaxKnockbackCap = 0.8f;

    [Tooltip("교전(백병전) 시 발을 멈추고 제자리에서 공격하는 정지 거리 (m 단위, 기본 보병: 1.05m, 장창병: 4.0m+, 사격병: 12m+)")]
    public float combatStoppingDistance = 1.05f;

    [Tooltip("적을 향해 이동할 때 적 중심으로부터 유지할 목표 교전 간격/위치 (m 단위, 기본 보병: 0.4m, 장창병: 3.5m+, 사격병: 10m+)")]
    public float engagementOffset = 0.40f;

    [Tooltip("달리기(구보) 모드 활성화 여부")]
    public bool isRunning = false;

    [Tooltip("제식 걷기 속도 (m/s, 기본: 1.2m/s)")]
    public float walkSpeed = 1.2f;

    [Tooltip("전술 구보 달리기 속도 (m/s, 기본: 2.8m/s)")]
    public float runSpeed = 2.8f;

    [Tooltip("적을 향해 쇄도하는 돌격 이동 속도 (m/s, 기본: 4.8m/s)")]
    public float chargeSpeed = 4.8f;

    [Tooltip("물리 충격/가속 시 유닛의 한계 최고 속도 제한 (m/s, 기본: 5.5m/s)")]
    public float unitMaxSpeed = 5.5f;

    [Header("돌격 및 물리 충격 설정")]
    [Tooltip("돌격 반사 (창벽 방어) 능력 보유 여부입니다. 체크 시 제자리에서 적의 돌격을 받을 때 적의 돌격 피해를 역으로 반사하여 큰 피해를 주고 적의 돌격을 분쇄합니다. (검병: 체크 해제, 장창병: 체크 권장)")]
    public bool canReflectCharge = false;

    [Tooltip("유닛 질량/무게 (kg 단위, 넉백 저항력 및 충돌 시 상대방을 밀쳐내는 물리 충격량의 기준 분모/분자)")]
    public float mass = 100f;

    [Tooltip("돌격 충격 보너스 계수입니다. 돌격 속도가 빠를수록 피해량이 증가합니다.\n공식: [돌격 추가 피해 = 돌격 보너스 × (돌격 쇄도 속도 ÷ 4.8m/s)]\n예: 기본 속도 4.8m/s 충돌 시 15.0 추가 피해, 기병 등 8.0m/s 충돌 시 25.0 추가 피해")]
    public float chargeBonus = 15f;

    [Tooltip("돌격 첫 충돌 시 가해질 수 있는 1회 최대 피해량 상한선입니다. (기본 공격력 + 돌격 추가 피해의 최대 상한선)")]
    public float maxChargeDamage = 35f;

    [Tooltip("넘어짐(무력화) 판정 넉백 속도 임계값 (m/s)입니다. 피격 넉백 속도가 이 기준 이상이면 충격을 이기지 못하고 그 자리에 넘어져 무력화 상태가 됩니다. (기본: 2.0 m/s)")]
    public float knockdownSpeedThreshold = 2.0f;

    [Tooltip("넘어져서 무력화되었을 때 다시 일어나서 전열로 복귀하기까지 걸리는 시간(초)입니다. 무력화 중에는 이동 및 공격이 정지됩니다. (기본: 3.0초)")]
    public float knockdownDuration = 3.0f;

    [Tooltip("넘어짐/무력화 면역 특수능력 보유 여부입니다. 체크(V) 시 아무리 강한 넉백 충격을 받아도 넘어지거나 무력화되지 않고 굳건히 버팁니다. (정예 보병, 중기병, 괴수, 영웅 등 특수능력 유닛용)")]
    public bool isImmuneToKnockdown = false;

    [Header("지휘 및 상태 설정")]
    [Tooltip("플레이어 마우스 선택 여부")]
    public bool isSelected = false;

    [Tooltip("유닛 현재 명령 상태 (Idle: 대기, Move: 이동, AttackMove: 공격이동, MeleeEngaged: 백병전)")]
    public UnitCommandState currentState = UnitCommandState.Idle;

    [Tooltip("유닛 전술 태세 (Aggressive: 공세, Defensive: 방어)")]
    public UnitStance currentStance = UnitStance.Aggressive;

    [Tooltip("선제 돌격 요격 활성화 여부 (V키 토글, 해제 시 제자리 방어선 사수 반격)")]
    public bool autoAttackEnabled = true;

    [Tooltip("자유 유닛 상태에서 주변 적을 탐지하는 시야 반경 (미터 단위)")]
    public float detectRange = 5.0f;

    public int Row { get; set; } = -1;
    public int Col { get; set; } = -1;
    public int simulationIndex { get; set; } = -1;

    [Header("개별 복귀 설정")]
    [Tooltip("대형 슬롯 위치에서 몇 미터 이상 벗어났을 때 복귀 카운트다운을 시작할지 결정하는 허용 오차")]
    [SerializeField] private float reformThreshold = 0.5f;

    [Tooltip("대형에서 벗어난 뒤 복귀 기동을 시작하기까지의 대기 시간 (초 단위)")]
    [SerializeField] private float reformDelay = 2.0f;
    private float reformTimer = 0f;
    private bool isOutOfFormation = false;
    private bool isReturningToFormation = false;

    [Header("겹침 방지 (Separation) 설정")]
    [Tooltip("유닛 간 물리적 겹침을 방지하기 위한 개인 방어 반경 (미터 단위)")]
    [SerializeField] private float separationRadius = 0.45f;

    [Tooltip("유닛끼리 겹쳤을 때 서로를 밀어내는 물리 척력의 세기")]
    [SerializeField] private float separationForce = 1.2f;
    private static readonly Collider[] separationBuffer = new Collider[16];

    private NavMeshAgent agent;
    private Renderer unitRenderer;

    public Squad mySquad = null;

    public Vector3 fixedTargetPos;
    public Vector3 formationOffset; // 부대 기준 순수 로컬 오프셋 저장
    public int formationIndex = -1;
    public bool hasSquadCommand = false;
    public Quaternion targetRotation = Quaternion.identity;

    public Vector3 FixedTargetPos => fixedTargetPos;
    public Quaternion TargetRotation => targetRotation;

    private Unit target;
    private float lastAttackTime;

    private float nextSearchTime = 0f;
    private float searchInterval = 0.2f;

    public void SetGridPosition(int row, int col)
    {
        Row = row;
        Col = col;
    }

    public void SetGridIndices(int newRow, int newCol)
    {
        Row = newRow;
        Col = newCol;
    }

    public void SetInitialFormationPosition(Squad squad, Vector3 targetWorldPos, Vector3 offset, int index, int row = 0, int col = 0)
    {
        mySquad = squad;
        fixedTargetPos = targetWorldPos;
        formationOffset = offset;
        formationIndex = index;
        Row = row;
        Col = col;
        hasSquadCommand = false;
        currentState = UnitCommandState.Idle;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.speed = Mathf.Min(agent.speed, unitMaxSpeed);
        }

        if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(this, targetWorldPos, transform.rotation, UnitCommandState.Idle);
        }
    }

    private void Awake()
    {
        unitRenderer = GetComponent<Renderer>();
        if (unitRenderer != null && unitRenderer.sharedMaterial != null)
        {
            unitRenderer.sharedMaterial.enableInstancing = true;
        }

        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.updatePosition = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.enabled = false; // ⚡ Job System이 직접 Transform을 제어하므로 1,200개 NavMeshAgent의 무거운 C++ 내부 연산 100% 차단!
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false; // ⚡ 1,200개 유닛 간의 PhysX 물리 충돌/트리거 연산 100% 차단!
        }

        transform.localScale = new Vector3(0.78f, 0.78f, 0.78f);
        currentHp = maxHp;

        // 🛡️ 구버전 프리팹/씬 직렬화 속도 값(0.6f / 1.8f) 자동 보정
        if (unitMaxSpeed < 5.0f) unitMaxSpeed = 5.5f;
        if (chargeSpeed < 4.0f) chargeSpeed = 4.8f;
        if (runSpeed < 2.0f) runSpeed = 2.8f;
        if (walkSpeed < 1.0f) walkSpeed = 1.2f;
        if (attackRange <= 0.1f) attackRange = 1.45f;
        if (combatStoppingDistance <= 0.1f) combatStoppingDistance = 1.05f;
        if (engagementOffset <= 0.05f) engagementOffset = 0.40f;
        baseArmor = armor;
        baseMass = mass;
        baseAttackCooldown = attackCooldown;
    }

    private void Start()
    {
        if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
        {
            MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.RegisterUnitEntity(this);
        }
        else if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.RegisterUnit(this);
        }

        // ⚡ 멀티코어 Job System / ECS가 Transform을 직접 제어하므로 1,200개 MonoBehaviour의 개별 Update() 호출을 100% 차단!
        this.enabled = false;
    }

    private void OnDestroy()
    {
        if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
        {
            MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UnregisterUnitEntity(this);
        }
        if (UnitJobSimulationManager.HasInstance)
        {
            UnitJobSimulationManager.Instance.UnregisterUnit(this);
        }
    }

    private void Update()
    {
        // 멀티코어 Job System이 실행 중일 때는 개별 Update 부하를 생략하여 극강의 성능 확보
        if (UnitJobSimulationManager.Instance != null)
        {
            return;
        }

        if (isPlayer)
        {
            if (currentState == UnitCommandState.Move)
            {
                // 강제 이동: 적을 무시하고 목표지점으로 감
                HandleMovementToFixedPos();
            }
            else if (currentState == UnitCommandState.AttackMove)
            {
                // 어택땅: 이동 중 적이 보이면 교전
                if (autoAttackEnabled)
                {
                    if (Time.time >= nextSearchTime)
                    {
                        nextSearchTime = Time.time + searchInterval;
                        float searchRange = (mySquad != null && mySquad.currentTargetSquad != null) ? 25.0f : detectRange;
                        Unit foundEnemy = (mySquad != null && mySquad.currentTargetSquad != null)
                            ? FindClosestEnemyInSquad(mySquad.currentTargetSquad, searchRange)
                            : FindClosestEnemyWithinRange(searchRange);

                        if (foundEnemy != null)
                        {
                            target = foundEnemy;
                            currentState = UnitCommandState.MeleeEngaged;
                        }
                    }
                }
                
                if (currentState == UnitCommandState.AttackMove) 
                {
                    HandleMovementToFixedPos();
                }
            }
            else if (currentState == UnitCommandState.MeleeEngaged)
            {
                // 교전 중: 타겟이 죽거나 멀어지면 다시 원래 상태로
                if (target == null || target.currentHp <= 0)
                {
                    target = null;
                    currentState = (mySquad != null && mySquad.isMoving) ? mySquad.currentCommandState : (hasSquadCommand ? UnitCommandState.AttackMove : UnitCommandState.Idle);
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = false;
                        float targetSpd = mySquad != null ? mySquad.targetSpeed : (isRunning ? runSpeed : walkSpeed);
                        agent.speed = Mathf.Min(targetSpd, unitMaxSpeed);
                        agent.acceleration = isRunning ? 8.0f : 4.5f;
                    }
                }
                else
                {
                    // 추격/돌격(Aggressive)일 경우 타겟을 향해 전속력(4.8m/s) 돌격 쇄도
                    if (currentStance == UnitStance.Aggressive)
                    {
                        if (agent != null && agent.isOnNavMesh)
                        {
                            agent.isStopped = false;
                            agent.speed = chargeSpeed;
                            agent.acceleration = 12.0f;
                            agent.SetDestination(target.transform.position);
                        }
                    }
                    else if (currentStance == UnitStance.HoldPosition)
                    {
                        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                    }
                }
            }
            else // Idle
            {
                if (!hasSquadCommand && mySquad != null && !mySquad.isMoving)
                {
                    HandleIndividualAutoReform();
                }
                else if (autoAttackEnabled && Time.time >= nextSearchTime)
                {
                    nextSearchTime = Time.time + searchInterval;
                    float searchRange = (mySquad != null && mySquad.currentTargetSquad != null) ? 25.0f : detectRange;
                    Unit foundEnemy = (mySquad != null && mySquad.currentTargetSquad != null)
                        ? FindClosestEnemyInSquad(mySquad.currentTargetSquad, searchRange)
                        : FindClosestEnemyWithinRange(searchRange);

                    if (foundEnemy != null)
                    {
                        target = foundEnemy;
                        currentState = UnitCommandState.MeleeEngaged;
                    }
                }
            }
        }
        else // 적 AI
        {
            if (Time.time >= nextSearchTime)
            {
                nextSearchTime = Time.time + searchInterval;
                if (target == null)
                {
                    if (mySquad != null && mySquad.currentTargetSquad != null && mySquad.currentTargetSquad.MemberCount > 0)
                        target = FindClosestEnemyInSquad(mySquad.currentTargetSquad, 80.0f);
                    else
                        target = FindClosestEnemy();
                }

                if (target != null && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.speed = chargeSpeed;
                    agent.acceleration = 6.0f;
                    agent.SetDestination(target.transform.position);
                }
                else if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }
        }

        // 주변 겹친 유닛과의 소프트 척력(Separation) 적용 (네비메시 무결성 유지)
        HandleSeparation();
    }

    private void HandleSeparation()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, separationBuffer);
        if (count <= 1) return;

        Vector3 separationVector = Vector3.zero;
        Vector3 myPos = transform.position;

        for (int i = 0; i < count; i++)
        {
            Collider col = separationBuffer[i];
            if (col == null || col.gameObject == gameObject) continue;

            Unit otherUnit = col.GetComponent<Unit>();
            if (otherUnit == null) continue;

            Vector3 diff = myPos - otherUnit.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist < 0.001f)
            {
                // 완전히 동일한 좌표에 겹쳐있는 경우 임의 방향으로 부드럽게 밀어냄
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                diff = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle)) * 0.05f;
                dist = 0.05f;
            }

            if (dist < separationRadius)
            {
                float weight = (separationRadius - dist) / separationRadius;
                separationVector += diff.normalized * weight;
            }
        }

        if (separationVector.sqrMagnitude > 0.001f)
        {
            // 이동 상태에 따른 가중치 차등 적용: 대형 이동 중에는 대형 유지가 우선이므로 약하게 적용
            float multiplier = (mySquad != null && mySquad.isMoving) ? 0.25f : 1.0f;
            Vector3 pushDelta = separationVector.normalized * (separationForce * multiplier * Time.deltaTime);
            agent.Move(pushDelta);
        }
    }

    private void HandleMovementToFixedPos()
    {
        if (hasSquadCommand)
        {
            ResetReformTimer();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                if (agent.velocity.sqrMagnitude > 0.05f)
                {
                    Vector3 moveDir = agent.velocity;
                    moveDir.y = 0f;
                    if (moveDir != Vector3.zero)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(moveDir);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRot, 360f * Time.deltaTime);
                    }
                }
                if (Vector3.Distance(agent.destination, fixedTargetPos) > 0.05f)
                {
                    agent.SetDestination(fixedTargetPos);
                }
            }
        }
    }

    private Unit FindClosestEnemyWithinRange(float range)
    {
        Unit[] allSoldiers = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        Unit closest = null;
        float closestDistance = range;
        Vector3 currentPos = transform.position;

        foreach (Unit soldier in allSoldiers)
        {
            if (soldier != null && soldier.isPlayer != this.isPlayer)
            {
                float distance = Vector3.Distance(currentPos, soldier.transform.position);
                if (distance <= closestDistance)
                {
                    closestDistance = distance;
                    closest = soldier;
                }
            }
        }
        return closest;
    }

    private Unit FindClosestEnemyInSquad(Squad targetSquad, float range)
    {
        if (targetSquad == null || targetSquad.members == null || targetSquad.members.Count == 0) return null;

        Unit closest = null;
        float closestDistance = range;
        Vector3 currentPos = transform.position;

        for (int i = 0; i < targetSquad.members.Count; i++)
        {
            Unit enemy = targetSquad.members[i];
            if (enemy != null && enemy.currentHp > 0)
            {
                float distance = Vector3.Distance(currentPos, enemy.transform.position);
                if (distance <= closestDistance)
                {
                    closestDistance = distance;
                    closest = enemy;
                }
            }
        }
        return closest;
    }

    public void SetRunMode(bool run, float customSpeed = -1f)
    {
        isRunning = run;
        float effectiveSpeed = (customSpeed > 0f) ? customSpeed : (isRunning ? runSpeed : walkSpeed);
        float speed = Mathf.Min(effectiveSpeed, unitMaxSpeed);
        float accel = isRunning ? 8.0f : 4.5f;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.speed = speed;
            agent.acceleration = accel;
        }

        if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
        {
            MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateUnitSpeed(this, speed, accel);
        }
        else if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.UpdateUnitSpeedAndAccel(this, speed, accel);
        }
    }

    public void MoveToFixedSquadPosition(Squad squad, Vector3 targetWorldPos, Vector3 offset, int index, Quaternion squadRotation = default, UnitCommandState cmdState = UnitCommandState.Move)
    {
        mySquad = squad;
        fixedTargetPos = targetWorldPos;
        formationOffset = offset;
        formationIndex = index;

        targetRotation = (squadRotation == default) ? (squad != null ? squad.transform.rotation : transform.rotation) : squadRotation;

        hasSquadCommand = true;
        currentState = cmdState;
        target = null;
        ResetReformTimer();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.stoppingDistance = 0.01f;

            float targetSpd = squad != null ? squad.targetSpeed : (isRunning ? runSpeed : walkSpeed);
            agent.speed = Mathf.Min(targetSpd, unitMaxSpeed);
            agent.acceleration = isRunning ? 8.0f : 4.5f;
            agent.angularSpeed = 300f;

            agent.SetDestination(targetWorldPos);
        }

        if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(this, targetWorldPos, targetRotation, cmdState);
            float targetSpd = squad != null ? squad.targetSpeed : (isRunning ? runSpeed : walkSpeed);
            float accel = isRunning ? 8.0f : 4.5f;
            UnitJobSimulationManager.Instance.UpdateUnitSpeedAndAccel(this, targetSpd, accel);
        }
    }

    private void HandleIndividualAutoReform()
    {
        if (mySquad == null) return;

        // 순수 로컬 formationOffset에 부대 현재 회전만 적용하여 완벽한 목표 위치 재계산
        Vector3 rotatedOffset = mySquad.transform.rotation * formationOffset;
        Vector3 finalDestination = mySquad.transform.position + rotatedOffset;
        float distanceToTarget = Vector3.Distance(transform.position, finalDestination);

        if (distanceToTarget > reformThreshold)
        {
            if (!isOutOfFormation)
            {
                isOutOfFormation = true;
                reformTimer = 0f;
            }

            if (!isReturningToFormation)
            {
                reformTimer += Time.deltaTime;
                if (reformTimer >= reformDelay)
                {
                    isReturningToFormation = true;
                    MoveToFixedSquadPosition(mySquad, finalDestination, formationOffset, formationIndex, default, mySquad.currentCommandState);
                }
            }
        }
        else
        {
            ResetReformTimer();
        }
    }

    public void ResetReformTimer()
    {
        reformTimer = 0f;
        isOutOfFormation = false;
        isReturningToFormation = false;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (isPlayer && unitRenderer != null && unitRenderer.material != null && unitRenderer.material.HasProperty("_Color"))
        {
            unitRenderer.material.color = selected ? Color.cyan : Color.blue;
        }
    }

    private Unit FindClosestEnemy()
    {
        if (mySquad != null && mySquad.currentTargetSquad != null && mySquad.currentTargetSquad.MemberCount > 0)
        {
            Unit inSquad = FindClosestEnemyInSquad(mySquad.currentTargetSquad, 80.0f);
            if (inSquad != null) return inSquad;
        }

        Unit[] allSoldiers = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        Unit closest = null;
        float closestDistance = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (Unit soldier in allSoldiers)
        {
            if (soldier != null && soldier.isPlayer != this.isPlayer)
            {
                float distance = Vector3.Distance(currentPos, soldier.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = soldier;
                }
            }
        }
        return closest;
    }

    public void TakeDamage(float amount, bool ignoreArmor = false)
    {
        float effectiveDamage = amount;
        if (!ignoreArmor && amount < 9000f)
        {
            float damageReduction = Mathf.Clamp(armor, 0, 10000) / 10000f;
            effectiveDamage = (armor >= 10000) ? 0f : Mathf.Max(1.0f, amount * (1.0f - damageReduction));
        }

        currentHp -= effectiveDamage;
        if (currentHp <= 0)
        {
            if (mySquad != null)
            {
                mySquad.OnUnitDied(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;

        // 멀티코어 Job System(UnitJobSimulationManager)이 전투/데미지를 전담할 때는 
        // 물리 트리거 다중 호출로 인한 중복 데미지 폭증(우르르 사망) 방지
        if (UnitJobSimulationManager.Instance != null) return;

        Unit otherSoldier = other.GetComponent<Unit>();
        if (otherSoldier != null && otherSoldier.isPlayer != this.isPlayer)
        {
            // 🚨 타겟 이탈 방지: 지정된 목표 부대가 있다면 그 부대 소속이 아닌 옆 부대원은 타겟으로 락온하지 않음!
            if (mySquad != null && mySquad.currentTargetSquad != null && otherSoldier.mySquad != null)
            {
                if (otherSoldier.mySquad != mySquad.currentTargetSquad) return;
            }

            // 부대가 이동 중이거나 무시 이동(Move) 상태일 때는 멈추지 않고 전진하면서 공격
            bool isSquadMoving = (mySquad != null && mySquad.isMoving);

            if (currentState != UnitCommandState.Move && !isSquadMoving)
            {
                currentState = UnitCommandState.MeleeEngaged;
                target = otherSoldier;
                ResetReformTimer();

                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
            }

            float distToEnemy = Vector3.Distance(transform.position, otherSoldier.transform.position);
            bool isSidearmActive = (useSidearm && distToEnemy <= sidearmSwitchDistance);

            if (isSidearmActive)
            {
                if (distToEnemy <= sidearmAttackRange && Time.time >= lastAttackTime + sidearmAttackCooldown)
                {
                    otherSoldier.TakeDamage(sidearmDamage);
                    lastAttackTime = Time.time;
                }
            }
            else
            {
                bool isInsideMinRange = (minAttackRange > 0.05f && distToEnemy < minAttackRange);
                if (!isInsideMinRange && distToEnemy <= attackRange && Time.time >= lastAttackTime + attackCooldown)
                {
                    float finalDamage = damage;
                    if (optimalRangeMin > 0.05f && distToEnemy < optimalRangeMin)
                    {
                        finalDamage *= closeRangeDamageRatio;
                    }
                    otherSoldier.TakeDamage(finalDamage);
                    lastAttackTime = Time.time;
                }
            }
        }
    }

    public void MoveTo(Vector3 targetPosition)
    {
        hasSquadCommand = false;
        target = null;
        currentState = UnitCommandState.Move;
        fixedTargetPos = targetPosition;
        ResetReformTimer();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.stoppingDistance = 0.15f;
            agent.isStopped = false;
            agent.speed = isRunning ? runSpeed : walkSpeed;
            agent.SetDestination(targetPosition);
        }

        if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
        {
            MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateEntityTarget(this, targetPosition, transform.rotation, UnitCommandState.Move);
        }
        else if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(this, targetPosition, transform.rotation, UnitCommandState.Move);
        }
    }

    public void AttackMoveTo(Vector3 targetPosition)
    {
        hasSquadCommand = false;
        target = null;
        currentState = UnitCommandState.AttackMove;
        fixedTargetPos = targetPosition;
        ResetReformTimer();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.stoppingDistance = 0.15f;
            agent.isStopped = false;
            agent.speed = isRunning ? runSpeed : walkSpeed;
            agent.SetDestination(targetPosition);
        }

        if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
        {
            MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateEntityTarget(this, targetPosition, transform.rotation, UnitCommandState.AttackMove);
        }
        else if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(this, targetPosition, transform.rotation, UnitCommandState.AttackMove);
        }
    }

    public void UpdateTargetPosition(Vector3 newWorldTargetPos, Quaternion squadRotation)
    {
        fixedTargetPos = newWorldTargetPos;
        targetRotation = squadRotation;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.SetDestination(fixedTargetPos);
        }

        if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
        {
            MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateEntityTarget(this, fixedTargetPos, targetRotation, currentState);
        }
        else if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(this, fixedTargetPos, targetRotation, currentState);
        }
    }
}