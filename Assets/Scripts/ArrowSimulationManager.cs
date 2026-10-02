using System.Collections.Generic;
using UnityEngine;

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
    public float vHorizontal;          // 수평 등속 속력 (v0 * cos(theta))
    public float vInitialY;            // 수직 초기 속력 (v0 * sin(theta))
    public float gravity;              // 중력 가속도 (9.81 * gravityScale)
    public float launchAngleDeg;       // 발사 각도 (도 단위)
    public float damage;               // 최종 대미지 (거리 감쇠 적용 후)
    public float armorPiercingRatio;   // 방어력 관통 비율 (0.0 ~ 1.0)
    public int armorShredAmount;       // 방어력 삭감 수치
    public bool ignoreArmor;           // 방어력 완전 무시 여부
    public TrajectoryMode trajectoryMode; // 곡사 / 직사
    public float gravityScale;         // 중력 배율
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
    /// 앞에 아군이 있으면 아군 머리 위를 넘기는 고각 곡사(48도~75도)로 발사합니다.
    /// </summary>
    public void LaunchArrow(
        int isPlayer,
        Vector3 shooterPos,
        Vector3 targetPos,
        float projectileSpeed,
        float damage,
        float armorPiercingRatio,
        int armorShredAmount,
        bool ignoreArmor,
        TrajectoryMode trajectoryMode,
        float gravityScale,
        float spreadRadius = 0.5f,
        bool hasAllyObstruction = false)
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

        // 📐 물리 탄도학 연산 (뉴턴 역학 정밀 역산 엔진)
        float g = 9.81f * Mathf.Max(0.2f, gravityScale);

        Vector3 diff = targetPos - shooterPos;
        float diffY = diff.y;
        Vector3 horizDiff = new Vector3(diff.x, 0f, diff.z);
        float horizDist = horizDiff.magnitude;
        Vector3 horizDir = horizDist > 0.001f ? (horizDiff / horizDist) : Vector3.forward;

        if (horizDist < 0.1f) horizDist = 0.1f;

        // 1. 전술적 발사 각도(theta) 결정
        float finalAngleDeg;
        float distRatio = Mathf.Clamp01(horizDist / 150.0f);

        if (trajectoryMode == TrajectoryMode.Flat)
        {
            // 🏹 [완전 직사 / 평사]: 쇠뇌, 직사 화살 - 4도 ~ 14도 최저각 수평 탄도
            finalAngleDeg = Mathf.Lerp(4.0f, 14.0f, distRatio);
        }
        else if (hasAllyObstruction)
        {
            // 🛡️ [고각 곡사]: 전방에 아군 백병전 전선이 있거나 깊은 후열 발사 시 머리 위를 넘기는 45~58도 고각
            finalAngleDeg = Mathf.Lerp(45.0f, 58.0f, distRatio);
        }
        else
        {
            // 🏹 [토탈워 현실적 탄도학]:
            // 0m ~ 50m: 5도 ~ 12도 완전한 수평 직사 (근거리 적에게 직선으로 시원하게 꽂힘!)
            // 50m ~ 100m: 12도 ~ 22도 낮은 표준 포물선
            // 100m ~ 150m: 22도 ~ 32도 장거리 사격 탄도
            if (horizDist <= 50.0f)
            {
                float closeRatio = Mathf.Clamp01(horizDist / 50.0f);
                finalAngleDeg = Mathf.Lerp(5.0f, 12.0f, closeRatio);
            }
            else if (horizDist <= 100.0f)
            {
                float midRatio = Mathf.Clamp01((horizDist - 50.0f) / 50.0f);
                finalAngleDeg = Mathf.Lerp(12.0f, 22.0f, midRatio);
            }
            else
            {
                float longRatio = Mathf.Clamp01((horizDist - 100.0f) / 50.0f);
                finalAngleDeg = Mathf.Lerp(22.0f, 32.0f, longRatio);
            }
        }

        float thetaRad = finalAngleDeg * Mathf.Deg2Rad;
        float cosTheta = Mathf.Cos(thetaRad);
        float sinTheta = Mathf.Sin(thetaRad);
        float tanTheta = Mathf.Tan(thetaRad);

        // 2. 🎯 [정밀 v0 역산 공식]: 목표 좌표(horizDist, diffY)에 100% 오차 없이 정확히 도달하는 초기 속력 v0 산출
        // 공식: v0 = sqrt( (g * R^2) / (2 * cos^2(theta) * (R * tan(theta) - diffY)) )
        float denom = 2.0f * cosTheta * cosTheta * (horizDist * tanTheta - diffY);
        float v0;
        if (denom > 0.01f)
        {
            float v0Sq = (g * horizDist * horizDist) / denom;
            v0 = Mathf.Sqrt(Mathf.Max(1.0f, v0Sq));
        }
        else
        {
            // 예외 방어: 각도가 너무 낮아 고도차를 극복하지 못할 경우 안전 속력 보정
            v0 = Mathf.Max(15.0f, projectileSpeed);
        }

        // 최소/최대 속력 안전 캡 (너무 느리거나 비정상적으로 빠르지 않도록)
        v0 = Mathf.Clamp(v0, 10.0f, 80.0f);

        float vHoriz = v0 * cosTheta;
        float v0Y = v0 * sinTheta;

        // 3. 체공 시간 산출: t = R / vHoriz
        float flightDuration = horizDist / Mathf.Max(1.0f, vHoriz);
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
            vHorizontal = vHoriz,
            vInitialY = v0Y,
            gravity = g,
            launchAngleDeg = finalAngleDeg,
            damage = damage,
            armorPiercingRatio = armorPiercingRatio,
            armorShredAmount = armorShredAmount,
            ignoreArmor = ignoreArmor,
            trajectoryMode = trajectoryMode,
            gravityScale = gravityScale
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
    /// 실제 뉴턴 역학 물리 공식에 따라 실시간 3D 좌표와 속도 벡터를 산출합니다.
    /// 최고점에서는 수직 속도가 0이 되어 감속되고, 낙하할 때는 중력 가속도로 빨라집니다.
    /// </summary>
    private Vector3 CalculatePhysicsPosition(ref ArrowData arrow, float elapsed, out Vector3 velocity)
    {
        float t = Mathf.Clamp(elapsed, 0f, arrow.flightDuration);

        // 1. 수평 등속도 운동: x(t) = start.x + vHoriz * t
        Vector3 horizPos = arrow.startPos + (arrow.horizontalDir * (arrow.vHorizontal * t));

        // 2. 수직 가속도 운동: y(t) = start.y + v0Y * t - 0.5 * g * t^2
        float yPos = arrow.startPos.y + (arrow.vInitialY * t) - (0.5f * arrow.gravity * t * t);
        horizPos.y = yPos;

        // 3. 실시간 속도 벡터: v(t) = vHoriz * dir + (v0Y - g * t) * up
        float currentVy = arrow.vInitialY - (arrow.gravity * t);
        velocity = (arrow.horizontalDir * arrow.vHorizontal) + (Vector3.up * currentVy);

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
    /// </summary>
    private void OnArrowImpact(ref ArrowData arrow)
    {
        Vector3 impactPos = arrow.targetPos;

        // ⚡ Job Simulation Manager 유닛 목록에서 착탄 지점 1.0m 내 적 탐색 및 대미지 적용
        if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.ApplyArrowDamageAtPosition(
                impactPos,
                arrow.isPlayer,
                arrow.damage,
                arrow.armorPiercingRatio,
                arrow.armorShredAmount,
                arrow.ignoreArmor,
                hitRadius: 1.0f
            );
        }
    }
}
