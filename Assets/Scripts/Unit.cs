using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Unit : MonoBehaviour
{
    [Header("유닛 기본 설정")]
    public bool isPlayer = false;
    public float maxHp = 100f;
    public float currentHp;
    public float damage = 10f;
    public float attackCooldown = 1.0f;
    public bool isRunning = false;
    public float walkSpeed = 1.2f;   // 자유 유닛 기본 걷기 속도 (A안: 1.2m/s, 4.32km/h)
    public float runSpeed = 2.8f;    // 자유 유닛 기본 달리기 속도 (A안: 2.8m/s, 10.08km/h)
    public float chargeSpeed = 4.8f; // 돌격 속도 (4.8m/s, 17.28km/h)
    public float unitMaxSpeed = 5.5f;

    [Header("돌격 및 물리 충격 설정")]
    public float mass = 100f;              // 유닛 질량/무게 (kg)
    public float chargeBonus = 15f;         // 돌격 보너스 계수
    public float maxChargeDamage = 35f;     // 첫 충돌 시 최대 데미지 한계치

    [Header("지휘 및 상태 설정")]
    public bool isSelected = false;
    public UnitCommandState currentState = UnitCommandState.Idle;
    public UnitStance currentStance = UnitStance.Aggressive;
    public bool autoAttackEnabled = true;
    public float detectRange = 5.0f;

    public int Row { get; set; } = -1;
    public int Col { get; set; } = -1;
    public int simulationIndex { get; set; } = -1;

    [Header("개별 복귀 설정")]
    [SerializeField] private float reformThreshold = 0.5f;
    [SerializeField] private float reformDelay = 2.0f;
    private float reformTimer = 0f;
    private bool isOutOfFormation = false;
    private bool isReturningToFormation = false;

    [Header("겹침 방지 (Separation) 설정")]
    [SerializeField] private float separationRadius = 0.45f;
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

    public void TakeDamage(float amount)
    {
        currentHp -= amount;
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

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                otherSoldier.TakeDamage(damage);
                lastAttackTime = Time.time;
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