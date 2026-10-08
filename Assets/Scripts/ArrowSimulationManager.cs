using System.Collections.Generic;
using UnityEngine;
using MiniTotalWar.ECS;

/// <summary>
/// 날아가는 개별 화살 투사체의 실시간 물리 탄도학 시뮬레이션 데이터 구조체입니다.
/// </summary>
public struct ArrowData
{
    public bool isAlive;
    public int isPlayer;               // 1 = Player, 0 = Enemy
    public Vector3 startPos;           // 발사 시작 위치
    public Vector3 targetPos;          // 최종 목표 착탄 지점
    public Vector3 horizontalDir;      // 수평 단위 방향 벡터 (xz 평면)
    public float horizontalDist;       // 수평 총 비행 거리 (m)
    public float launchTime;           // 발사 시각 (Time.time)
    public float flightDuration;       // 총 체공 비행 시간 (초)
    public float initialSpeed;         // 발사 초기 속력 (v0, m/s - 직사/곡사 동일 속도)
    public float drag;                 // 공기 저항(항력) 감속 계수 (k, 1/s)
    public float vHorizontal;          // 초기 수평 속력 (v0 * cos(theta))
    public float vInitialY;            // 초기 수직 속력 (v0 * sin(theta))
    public float gravity;              // 중력 가속도 (9.81 * gravityScale)
    public float launchAngleDeg;       // 발사 각도 (도 단위)
    public float damage;               // 최종 대미지 (거리 감쇠 적용 후)
    public float armorPiercingRatio;   // 방어력 관통 비율 (0.0 ~ 1.0)
    public int armorShredAmount;       // 방어력 삭감 수치
    public bool ignoreArmor;           // 방어력 완전 무시 여부
    public TrajectoryMode trajectoryMode; // 곡사 / 직사
    public float gravityScale;         // 중력 배율
    public float knockbackPower;       // 피격 넉백 세기 (m/s)
}

/// <summary>
/// 수백 ~ 수천 발의 화살을 유니티 물리 엔진(PhysX) 부하 없이
/// GPU 인스턴싱 및 실제 물리 포물선 탄도학으로 초고속 시뮬레이션하는 전역 매니저입니다.
/// </summary>
[DisallowMultipleComponent]
public class ArrowSimulationManager : MonoBehaviour
{
    private static ArrowSimulationManager _instance;
    public static ArrowSimulationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ArrowSimulationManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[ArrowSimulationManager]");
                    _instance = go.AddComponent<ArrowSimulationManager>();
                }
            }
            return _instance;
        }
    }

    [Header("화살 외형 및 렌더링 설정")]
    [Tooltip("화살 3D 메쉬 (미지정 시 내장 실린더 메쉬로 자동 생성)")]
    [SerializeField] private Mesh arrowMesh;

    [Tooltip("아군 화살 머티리얼")]
    [SerializeField] private Material playerArrowMaterial;

    [Tooltip("적군 화살 머티리얼")]
    [SerializeField] private Material enemyArrowMaterial;

    [Header("외부 시뮬레이션 주도 여부")]
    [Tooltip("UnitJobSimulationManager에서 잡 완료 후 수동 틱을 수행하면 Update는 자동 스킵")]
    public bool drivenExternally = false;

    // 최대 동시 비행 화살 풀 크기
    private const int MAX_ARROWS = 2048;
    private ArrowData[] arrowPool = new ArrowData[MAX_ARROWS];
    private int activeArrowCount = 0;

    // GPU 인스턴싱 렌더링 버퍼 (최대 1023개 단위 DrawMeshInstanced)
    private Matrix4x4[] playerMatrices = new Matrix4x4[1023];
    private Matrix4x4[] enemyMatrices = new Matrix4x4[1023];
    private int playerCount = 0;
    private int enemyCount = 0;

    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        propBlock = new MaterialPropertyBlock();

        // 🏹 화살 기본 메쉬가 없으면 가늘고 긴 화살 형태의 메쉬 자동 구성
        if (arrowMesh == null)
        {
            arrowMesh = CreateArrowMesh();
        }

        // 기본 머티리얼 자동 생성 (GPU 인스턴싱 활성화)
        if (playerArrowMaterial == null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            playerArrowMaterial = new Material(standardShader);
            playerArrowMaterial.color = new Color(0.2f, 0.7f, 1.0f); // 청록/파랑
            playerArrowMaterial.enableInstancing = true;
        }

        if (enemyArrowMaterial == null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            enemyArrowMaterial = new Material(standardShader);
            enemyArrowMaterial.color = new Color(1.0f, 0.35f, 0.15f); // 주황/빨강
            enemyArrowMaterial.enableInstancing = true;
        }
    }

    /// <summary>
    /// 가늘고 날렵한 기본 화살 3D 메쉬를 절차적으로 생성합니다.
    /// </summary>
    private Mesh CreateArrowMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralArrow";

        float length = 0.8f;
        float radius = 0.025f;

        Vector3[] vertices = new Vector3[]
        {
            // 화살대 (직육면체)
            new Vector3(-radius, -radius, -length * 0.5f),
            new Vector3( radius, -radius, -length * 0.5f),
            new Vector3( radius,  radius, -length * 0.5f),
            new Vector3(-radius,  radius, -length * 0.5f),
            new Vector3(-radius, -radius,  length * 0.35f),
            new Vector3( radius, -radius,  length * 0.35f),
            new Vector3( radius,  radius,  length * 0.35f),
            new Vector3(-radius,  radius,  length * 0.35f),

            // 화살촉 (사각뿔)
            new Vector3(-radius * 2.2f, -radius * 2.2f, length * 0.35f),
            new Vector3( radius * 2.2f, -radius * 2.2f, length * 0.35f),
            new Vector3( radius * 2.2f,  radius * 2.2f, length * 0.35f),
            new Vector3(-radius * 2.2f,  radius * 2.2f, length * 0.35f),
            new Vector3(0f, 0f, length * 0.5f) // 화살촉 끝점
        };

        int[] triangles = new int[]
        {
            // 화살대 6면
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6,
            3, 0, 4, 3, 4, 7,

            // 화살촉 사각뿔 옆면 4개
            8, 9, 12,
            9, 10, 12,
            10, 11, 12,
            11, 8, 12,
            // 화살촉 밑면
            8, 10, 9, 8, 11, 10
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 새로운 화살을 물리 탄도학 공식에 맞추어 발사합니다.
    /// 동일 에너지 가정 하에 가까우면 직사(0도~), 멀면 곡사(~45도),
    /// <summary>
    /// 새로운 화살을 정통 물리 탄도학 공식(포물선 궤적 방정식의 이중근 + 공기 저항 감속)에 맞추어 발사합니다.
    /// 직사(저각)와 곡사(고각) 모두 초기 발사 속력(projectileSpeed)은 100% 동일하게 유지되며,
    /// 비행하는 동안 공기 저항(projectileDrag)을 받아 날아갈수록 점점 감속되어 묵직하게 착탄합니다.
    /// </summary>
    public void LaunchArrow(
        int isPlayer,
        Vector3 shooterPos,
        Vector3 targetPos,
        float projectileSpeed,
        float projectileDrag,
        float damage,
        float armorPiercingRatio,
        int armorShredAmount,
        bool ignoreArmor,
        TrajectoryMode trajectoryMode,
        float gravityScale,
        float spreadRadius = 0.5f,
        bool hasAllyObstruction = false,
        int shooterRow = 0,
        float knockbackPower = 0.35f)
    {
        // 🎯 탄착군 오차 반경 무작위 분산 적용
        if (spreadRadius > 0.05f)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spreadRadius;
            targetPos.x += randomCircle.x;
            targetPos.z += randomCircle.y;
        }

        // 빈 화살 슬롯 탐색
        int slotIndex = -1;
        for (int i = 0; i < MAX_ARROWS; i++)
        {
            if (!arrowPool[i].isAlive)
            {
                slotIndex = i;
                break;
            }
        }

        if (slotIndex == -1) return; // 풀 초과 시 안전 스킵

        // 📐 정통 뉴턴 역학 탄도학 연산 엔진 (Ballistics Trajectory Engine)
        float g = 9.81f * Mathf.Max(0.2f, gravityScale);

        Vector3 diff = targetPos - shooterPos;
        float diffY = diff.y;
        Vector3 horizDiff = new Vector3(diff.x, 0f, diff.z);
        float horizDist = horizDiff.magnitude;
        Vector3 horizDir = horizDist > 0.001f ? (horizDiff / horizDist) : Vector3.forward;

        if (horizDist < 0.1f) horizDist = 0.1f;

        // 🏹 [정통 탄도학 원칙 1: 고정된 초기 발사 속력 v0]
        // 직사(Flat)와 곡사(High Arc) 모두 유닛의 고유 발사 속력(projectileSpeed)을 100% 동일하게 사용!
        float v0 = Mathf.Max(10.0f, projectileSpeed);
        float v0Sq = v0 * v0;
        float v0Quad = v0Sq * v0Sq;

        // 🏹 [정통 탄도학 원칙 2: 포물선 궤적 방정식의 전방위 이중근 및 수직 특이해 분기]
        float launchAngleDeg;
        float launchAngleRad;
        float cosTheta;
        float sinTheta;
        float vHoriz;
        float v0Y;

        if (horizDist < 0.2f)
        {
            // 🎯 [완전 수직 사격 특이해]: 수평 거리(R)가 0에 수렴할 때 NaN 방지 및 90도 수직 사격 분기
            if (diffY >= 0f)
            {
                // 완전 수직 상향 (+89.9도): 절벽 위/머리 위 적 사격
                launchAngleDeg = 89.9f;
                // 도달에 필요한 최소 물리 속도 보정
                float minV = Mathf.Sqrt(2.0f * g * Mathf.Max(0.1f, diffY)) * 1.05f;
                v0 = Mathf.Max(v0, minV);
            }
            else
            {
                // 완전 수직 하향 (-89.9도): 절벽 밑/성벽 바로 아래 적 사격
                launchAngleDeg = -89.9f;
            }

            launchAngleRad = launchAngleDeg * Mathf.Deg2Rad;
            cosTheta = Mathf.Cos(launchAngleRad);
            sinTheta = Mathf.Sin(launchAngleRad);
            vHoriz = Mathf.Max(0.05f, v0 * cosTheta);
            v0Y = v0 * sinTheta;
        }
        else
        {
            // 공식: tan(theta) = (v0^2 ± sqrt(v0^4 - g * (g * R^2 + 2 * dy * v0^2))) / (g * R)
            float discr = v0Quad - g * (g * horizDist * horizDist + 2.0f * diffY * v0Sq);

            float tanTheta;
            if (discr >= 0f)
            {
                float sqrtDiscr = Mathf.Sqrt(discr);
                float tanLow = (v0Sq - sqrtDiscr) / (g * horizDist);   // 저각 해 (직사: Flat)
                float tanHigh = (v0Sq + sqrtDiscr) / (g * horizDist); // 고각 해 (곡사: High Arc)

                if (trajectoryMode == TrajectoryMode.Flat)
                {
                    // 🏹 직사 유닛 (쇠뇌, 직사 화살): 저각 해 채택
                    tanTheta = tanLow;
                }
                else if (hasAllyObstruction)
                {
                    // 🛡️ 아군 차폐 / 언덕 너머: 머리 위를 넘기는 고각 해 채택
                    tanTheta = tanHigh;
                }
                else
                {
                    // 🏹 일반 곡사 유닛 (궁병):
                    // 사선이 열려 있고 앞에 아군 차폐가 없으면, 활이라도 적을 향해 직접 내리꽂는
                    // 가장 빠르고 정확한 직사/저각(tanLow)으로 발사!
                    // 특히 적이 언덕 아래에 있을 때(diffY < 0)는 시원한 하향 직사(Downhill Direct Fire)를 구사!
                    tanTheta = tanLow;
                }
            }
            else
            {
                // 🏹 사거리 한계 초과 시: 목표물 도달에 필요한 최소 물리 속도로 미세 보정 후 최대 사거리 발사
                float minSpeedRequired = Mathf.Sqrt(g * (diffY + Mathf.Sqrt(horizDist * horizDist + diffY * diffY))) * 1.02f;
                v0 = Mathf.Max(v0, minSpeedRequired);
                v0Sq = v0 * v0;
                float adjDiscr = Mathf.Max(0f, (v0Sq * v0Sq) - g * (g * horizDist * horizDist + 2.0f * diffY * v0Sq));
                float adjSqrt = Mathf.Sqrt(adjDiscr);
                tanTheta = (trajectoryMode == TrajectoryMode.Flat || !hasAllyObstruction)
                    ? (v0Sq - adjSqrt) / (g * horizDist)
                    : (v0Sq + adjSqrt) / (g * horizDist);
            }

            // 라디안 및 각도(Deg) 산출 (-89.5도 ~ +89.5도 전방위 90도 사격 완벽 개방)
            float baseAngleRad = Mathf.Atan(tanTheta);
            float baseAngleDeg = baseAngleRad * Mathf.Rad2Deg;

            // 🏹 [일제사격 화살 다발(Volley Bundle) 연출]: 뒷열 사수는 앞사람 머리 위를 넘기도록 행당 +0.7도 미세 분산
            float rowOffset = Mathf.Clamp(shooterRow * 0.7f, 0f, 3.5f);
            launchAngleDeg = Mathf.Clamp(baseAngleDeg + rowOffset, -89.5f, 89.5f);
            launchAngleRad = launchAngleDeg * Mathf.Deg2Rad;

            cosTheta = Mathf.Cos(launchAngleRad);
            sinTheta = Mathf.Sin(launchAngleRad);

            // 초기 수평/수직 분속도 벡터 크기
            vHoriz = Mathf.Max(0.05f, v0 * cosTheta);
            v0Y = v0 * sinTheta;
        }

        // 🏹 [정통 탄도학 원칙 3: 공기 저항(항력) 감속 및 총 체공 시간 산출]
        // 선형 공기 저항(Linear Drag: a_drag = -k * v) 모델의 정확한 비행 시간 T:
        float k = Mathf.Clamp(projectileDrag, 0f, 1.0f);
        float flightDuration;

        if (k > 0.001f)
        {
            float dragRatio = (k * horizDist) / Mathf.Max(0.5f, vHoriz);
            dragRatio = Mathf.Clamp(dragRatio, 0f, 0.85f);
            flightDuration = -Mathf.Log(1.0f - dragRatio) / k;
        }
        else
        {
            flightDuration = horizDist / Mathf.Max(0.5f, vHoriz);
        }

        // 수직 운동 기반 최소 비행 시간 보정 (수직 사격 시 horizDist가 작아도 정확한 낙하/상승 시간 보장)
        if (Mathf.Abs(diffY) > 0.5f)
        {
            float vertFlightTime = (diffY < 0f)
                ? ((-v0Y + Mathf.Sqrt(Mathf.Max(0.1f, v0Y * v0Y + 2f * g * Mathf.Abs(diffY)))) / g)
                : Mathf.Abs(diffY) / Mathf.Max(1.0f, Mathf.Abs(v0Y));
            if (vertFlightTime > 0.05f && vertFlightTime < 10.0f)
            {
                flightDuration = Mathf.Max(flightDuration, vertFlightTime);
            }
        }

        if (flightDuration <= 0.05f) flightDuration = 0.05f;

        arrowPool[slotIndex] = new ArrowData
        {
            isAlive = true,
            isPlayer = isPlayer,
            startPos = shooterPos,
            targetPos = targetPos,
            horizontalDir = horizDir,
            horizontalDist = horizDist,
            launchTime = Time.time,
            flightDuration = flightDuration,
            initialSpeed = v0,
            drag = k,
            vHorizontal = vHoriz,
            vInitialY = v0Y,
            gravity = g,
            launchAngleDeg = launchAngleDeg,
            damage = damage,
            armorPiercingRatio = armorPiercingRatio,
            armorShredAmount = armorShredAmount,
            ignoreArmor = ignoreArmor,
            trajectoryMode = trajectoryMode,
            gravityScale = gravityScale,
            knockbackPower = knockbackPower
        };

        activeArrowCount++;
    }

    private void Update()
    {
        // 외부(UnitJobSimulationManager.LateUpdate)에서 직접 호출할 경우 중복 실행 방지
        if (drivenExternally) return;

        UpdateSimulation(Time.time);
    }

    /// <summary>
    /// UnitJobSimulationManager 등 외부 매니저가 잡 완료 직후 안전하게 화살 시뮬레이션을 구동하는 수동 틱 메서드
    /// </summary>
    public void ManualUpdate(float deltaTime)
    {
        drivenExternally = true;
        UpdateSimulation(Time.time);
    }

    /// <summary>
    /// 모든 비행 중인 화살의 물리 위치, 속도 변화(최고점 감속, 낙하 가속) 및 착탄을 처리합니다.
    /// </summary>
    private void UpdateSimulation(float currentTime)
    {
        if (activeArrowCount <= 0) return;

        playerCount = 0;
        enemyCount = 0;

        for (int i = 0; i < MAX_ARROWS; i++)
        {
            if (!arrowPool[i].isAlive) continue;

            float elapsed = currentTime - arrowPool[i].launchTime;

            // 🎯 [착탄 도달 판정]: 물리 체공 시간이 종료되면 착탄
            if (elapsed >= arrowPool[i].flightDuration)
            {
                OnArrowImpact(ref arrowPool[i]);
                arrowPool[i].isAlive = false;
                activeArrowCount--;
                continue;
            }

            // 🚀 실제 중력 가속도 물리 법칙을 따르는 실시간 위치 및 속도 벡터 연산
            Vector3 currentPos = CalculatePhysicsPosition(ref arrowPool[i], elapsed, out Vector3 velocity);

            // 🏔️ [지형 및 언덕 충돌 판정]: 발사 직후 안전 마진(0.10초) 경과 후 화살이 지형 표면 이하로 떨어지면 충돌 검사
            if (elapsed > 0.10f && TerrainHeightManager.HasInstance)
            {
                float terrainH = TerrainHeightManager.SampleHeightFast(currentPos.x, currentPos.z);
                if (currentPos.y <= terrainH)
                {
                    // 목표 지점 근접(체공 시간의 85% 이상 경과) 상태에서 지표면에 닿은 경우 -> 정상 착탄 대미지 적용!
                    if (elapsed >= arrowPool[i].flightDuration * 0.85f)
                    {
                        OnArrowImpact(ref arrowPool[i]);
                    }
                    // 중간 언덕 충돌 또는 조기 착탄으로 화살 수명 종료
                    arrowPool[i].isAlive = false;
                    activeArrowCount--;
                    continue;
                }
            }

            // 화살의 비행 자세(방향)를 실시간 속도 벡터에 정렬 (상승 시 상향, 정점 시 수평, 낙하 시 하향)
            Vector3 forward = velocity.sqrMagnitude > 0.01f ? velocity.normalized : arrowPool[i].horizontalDir;
            Quaternion rotation = Quaternion.LookRotation(forward);
            Matrix4x4 matrix = Matrix4x4.TRS(currentPos, rotation, Vector3.one);

            if (arrowPool[i].isPlayer == 1)
            {
                if (playerCount < playerMatrices.Length)
                {
                    playerMatrices[playerCount++] = matrix;
                }
            }
            else
            {
                if (enemyCount < enemyMatrices.Length)
                {
                    enemyMatrices[enemyCount++] = matrix;
                }
            }
        }

        // 🎨 GPU 인스턴싱으로 프레임당 단 몇 번의 DrawCall로 일괄 렌더링
        RenderArrowsGPU();
    }

    /// <summary>
    /// 정통 뉴턴 역학 탄도학 공식과 공기 저항 감속 모델에 따라 실시간 3D 좌표와 속도 벡터를 산출합니다.
    /// 발사 직후에는 초기 속도(projectileSpeed)로 출발하고, 날아갈수록 공기 저항으로 점점 느려지며,
    /// 포물선 궤적 방정식을 엄격하게 준수하여 목표 지점에 오차 없이 정확히 착탄합니다.
    /// </summary>
    private Vector3 CalculatePhysicsPosition(ref ArrowData arrow, float elapsed, out Vector3 velocity)
    {
        float t = Mathf.Clamp(elapsed, 0f, arrow.flightDuration);
        float T = arrow.flightDuration;
        float R = arrow.horizontalDist;
        float k = arrow.drag;
        float v0 = arrow.initialSpeed;
        float thetaRad = arrow.launchAngleDeg * Mathf.Deg2Rad;
        float cosTheta = Mathf.Cos(thetaRad);
        float tanTheta = Mathf.Tan(thetaRad);

        // 1. 공기 저항(항력) 감속을 반영한 실시간 진행률 s(t) 및 실시간 수평 속력 vx(t)
        float s;
        float vx;
        if (k > 0.001f)
        {
            float expTerm = Mathf.Exp(-k * t);
            float expTotal = Mathf.Exp(-k * T);
            float denom = Mathf.Max(0.0001f, 1.0f - expTotal);
            s = Mathf.Clamp01((1.0f - expTerm) / denom);
            // 실시간 수평 속력: dx/dt = R * k * exp(-kt) / (1 - exp(-kT))
            vx = (R * k * expTerm) / denom;
        }
        else
        {
            s = Mathf.Clamp01(t / Mathf.Max(0.001f, T));
            vx = arrow.vHorizontal;
        }

        // 2. 실시간 수평 위치: x = R * s
        float currentX = R * s;
        Vector3 horizPos = arrow.startPos + (arrow.horizontalDir * currentX);

        // 3. 포물선 궤적 방정식에 따른 실시간 높이 y(x):
        // 공식: y(x) = y0 + x * tan(theta) - (g * x^2) / (2 * v0^2 * cos^2(theta))
        float denomY = 2.0f * v0 * v0 * cosTheta * cosTheta;
        float currentY;
        if (denomY > 0.0001f)
        {
            currentY = arrow.startPos.y + (currentX * tanTheta) - ((arrow.gravity * currentX * currentX) / denomY);
        }
        else
        {
            currentY = Mathf.Lerp(arrow.startPos.y, arrow.targetPos.y, s);
        }
        horizPos.y = currentY;

        // 4. 비행 경로의 접선에 일치하는 실시간 수직 속도 vy(t):
        // dy/dt = (dy/dx) * (dx/dt) = (tan(theta) - (g * x) / (v0^2 * cos^2(theta))) * vx
        float vy;
        if (denomY > 0.0001f)
        {
            float slope = tanTheta - ((arrow.gravity * currentX) / (v0 * v0 * cosTheta * cosTheta));
            vy = slope * vx;
        }
        else
        {
            vy = arrow.vInitialY - (arrow.gravity * t);
        }

        velocity = (arrow.horizontalDir * vx) + (Vector3.up * vy);
        return horizPos;
    }

    private void RenderArrowsGPU()
    {
        if (arrowMesh == null) return;

        // 1. 아군 화살 렌더링
        if (playerCount > 0 && playerArrowMaterial != null)
        {
            Graphics.DrawMeshInstanced(
                arrowMesh,
                0,
                playerArrowMaterial,
                playerMatrices,
                playerCount,
                propBlock,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false
            );
        }

        // 2. 적군 화살 렌더링
        if (enemyCount > 0 && enemyArrowMaterial != null)
        {
            Graphics.DrawMeshInstanced(
                arrowMesh,
                0,
                enemyArrowMaterial,
                enemyMatrices,
                enemyCount,
                propBlock,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false
            );
        }
    }

    /// <summary>
    /// 화살이 목표 지면에 꽂히는 순간 착탄 반경 내 적에게 대미지를 가합니다.
    /// (하이브리드 GameObject 모드 및 순수 Pure ECS 모드 듀얼 지원)
    /// </summary>
    private void OnArrowImpact(ref ArrowData arrow)
    {
        Vector3 impactPos = arrow.targetPos;

        // 1. ⚔️ 하이브리드 GameObject 모드 유닛 목록에서 대미지 적용
        if (UnitJobSimulationManager.Instance != null && UnitJobSimulationManager.Instance.TotalRegisteredUnits > 0)
        {
            UnitJobSimulationManager.Instance.ApplyArrowDamageAtPosition(
                impactPos,
                arrow.isPlayer,
                arrow.damage,
                arrow.armorPiercingRatio,
                arrow.armorShredAmount,
                arrow.ignoreArmor,
                hitRadius: 1.0f,
                knockbackPower: arrow.knockbackPower
            );
        }

        // 2. ⚡ 초고성능 순수 ECS 모드 (Zero-GameObject) 엔티티 대미지 적용
        if (SquadECSSimulationBridge.Instance != null && SquadECSSimulationBridge.Instance.IsInitialized)
        {
            SquadECSSimulationBridge.Instance.ApplyArrowDamageAtPosition(
                impactPos,
                arrow.isPlayer,
                arrow.damage,
                arrow.armorPiercingRatio,
                arrow.armorShredAmount,
                arrow.ignoreArmor,
                hitRadius: 1.0f,
                knockbackPower: arrow.knockbackPower
            );
        }
    }
}
