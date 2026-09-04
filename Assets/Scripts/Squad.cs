using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using MiniTotalWar.ECS;

public struct WaypointData
{
    public Vector3 destination;
    public Quaternion rotation;
    public int columns;
}

public struct SlotInfo
{
    public int row;
    public int col;
    public int countInRow;
    public Vector3 localOffset;
    public Quaternion localRotation;
}

/// <summary>
/// 여러 부대원(Unit)들을 하나의 부대로 묶어 대형 배치, 공간 정렬 및 다단계 웨이포인트 이동을 총괄 관리하는 부대 지휘 클래스입니다.
/// </summary>
[DisallowMultipleComponent]
public class Squad : MonoBehaviour
{
    [Header("부대 정보")]
    public string squadName = "보병대";
    public bool isPlayer = true;

    [Header("부대원 목록 및 이동 속도")]
    public List<Unit> members = new List<Unit>();
    public int initialUnitCount = 0;
    public int currentAliveCount = 0;
    public int MemberCount => (members != null && members.Count > 0) ? members.Count : ((currentAliveCount > 0) ? currentAliveCount : initialUnitCount);
    public bool isRunning = false;
    public float walkSpeed = 1.0f;   // 부대 제식 걷기 속도 (C안: 1.0m/s, 3.6km/h)
    public float runSpeed = 2.4f;    // 부대 전술 구보 속도 (C안: 2.4m/s, 8.64km/h)
    public float targetSpeed = 1.0f;
    public float unitMaxSpeed = 5.5f;

    [Header("방진 간격 설정")]
    public const float DEFAULT_SPACING_X = 1.1f;
    public const float DEFAULT_SPACING_Z = 1.2f;
    public const float DEFAULT_LOOSE_SPACING_X = 2.0f;
    public const float DEFAULT_LOOSE_SPACING_Z = 2.2f;

    public float spacing = 1.15f;
    public float spacingX = 1.1f;
    public float spacingZ = 1.2f;
    public float customSpacingMultiplier = 1.0f;
    public bool useStaggeredFormation = true; // 지그재그(체커보드) 엇갈림 배치

    [Tooltip("열(Rank)과 열 사이의 순차적 출발 반응 지연 시간(초)")]
    public float rowReactionDelay = 0.08f;

    [Header("대형 좌표 설정")]
    public Vector3 moveDestination;
    public int currentColumns = 5;

    private int totalGridRows = 0;

    [Header("진형 및 상태 변수")]
    public SquadFormationType currentFormationType = SquadFormationType.Normal;
    public bool isLooseFormation = false;
    public bool isMoving = false;
    public bool isSelected => PlayerController.Instance != null && PlayerController.Instance.IsSquadSelected(this);
    private bool hasAlignedThisArrival = false;
    private float moveStartGraceTimer = 0f;

    public List<WaypointData> waypointQueue = new List<WaypointData>();
    private LineRenderer pathLineRenderer;

    private readonly Dictionary<Unit, Vector3> unitTargetPositions = new Dictionary<Unit, Vector3>();
    private Coroutine alignmentCoroutine;
    private Coroutine formationAssignmentCoroutine;
    private Coroutine staggeredMovementCoroutine;

    public float maxRotationSpeed = 360f;
    private Quaternion targetSquadRotation;
    public UnitCommandState currentCommandState = UnitCommandState.Move;
    public UnitStance currentStance = UnitStance.Aggressive;
    public bool autoAttackEnabled = true;

    private Vector3 ecsVisualCenter = Vector3.zero;
    private bool hasEcsVisualCenter = false;

    private void Start()
    {
        // 🛡️ 구버전 프리팹/씬 직렬화 부대 속도 값(0.5f / 1.0f) 자동 보정
        if (walkSpeed < 0.9f) walkSpeed = 1.0f;
        if (runSpeed < 2.0f) runSpeed = 2.4f;
        if (targetSpeed < 0.9f) targetSpeed = isRunning ? runSpeed : walkSpeed;
        if (unitMaxSpeed < 5.0f) unitMaxSpeed = 5.5f;

        InitializeSquadOnStart();
        InitPathLineRenderer();

        if (members.Count > 0 && members[0] != null)
        {
            isPlayer = members[0].isPlayer;
        }

        // 플레이어 부대인 경우에만 화면 좌하단 부대 카드를 생성합니다.
        if (isPlayer)
        {
            if (SquadCardUIManager.Instance != null)
            {
                SquadCardUIManager.Instance.RegisterSquad(this);
            }
        }

        // 플레이어 및 적군 부대 모두 머리 위 부대 아이콘(SquadIconUI)을 생성합니다.
        if (SquadIconUIManager.Instance != null)
        {
            SquadIconUIManager.Instance.RegisterSquad(this);
        }

        // 전술 미니맵(MinimapManager)에 부대 마커 등록
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterSquad(this);
        }
    }

    private void OnDestroy()
    {
        if (SquadCardUIManager.Instance != null)
        {
            SquadCardUIManager.Instance.UnregisterSquad(this);
        }

        if (SquadIconUIManager.Instance != null)
        {
            SquadIconUIManager.Instance.UnregisterSquad(this);
        }

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterSquad(this);
        }
    }

    private void InitPathLineRenderer()
    {
        if (pathLineRenderer != null) return;

        GameObject lineObj = new GameObject("SquadPathLine");
        lineObj.transform.SetParent(transform);
        pathLineRenderer = lineObj.AddComponent<LineRenderer>();
        pathLineRenderer.startWidth = 0.1f;
        pathLineRenderer.endWidth = 0.1f;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) pathLineRenderer.material = new Material(shader);

        pathLineRenderer.startColor = Color.yellow;
        pathLineRenderer.endColor = Color.yellow;
        pathLineRenderer.positionCount = 0;
    }



    private void UpdatePathLine()
    {
        if (pathLineRenderer == null) return;

        if (isMoving || waypointQueue.Count > 0)
        {
            List<Vector3> points = new List<Vector3> { transform.position + Vector3.up * 0.1f };
            if (isMoving) points.Add(moveDestination + Vector3.up * 0.1f);

            foreach (var wp in waypointQueue)
            {
                points.Add(wp.destination + Vector3.up * 0.1f);
            }

            pathLineRenderer.positionCount = points.Count;
            pathLineRenderer.SetPositions(points.ToArray());
            pathLineRenderer.enabled = true;
        }
        else
        {
            pathLineRenderer.enabled = false;
        }
    }

    private void InitializeSquadOnStart()
    {
        members.RemoveAll(m => m == null);
        if (members.Count == 0) return;

        if (initialUnitCount <= 0) initialUnitCount = members.Count;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < members.Count; i++)
        {
            members[i].mySquad = this;
            sum += members[i].transform.position;
        }
        Vector3 center = sum / members.Count;

        transform.position = center;
        moveDestination = center;
        targetSquadRotation = transform.rotation;

        RebuildGridStructure(currentColumns, forceSpatialSort: true);
    }

    // 전체 대형의 열(maxColumns)을 기준으로 정확한 중앙 정렬 계산 (오른쪽 쏠림 현상 방지)
    public Vector3 CalculateSlotLocalOffset(int r, int c, int countInThisRow, int totalRows, int maxColumns)
    {
        if (totalRows <= 0)
        {
            int memberCount = (members != null && members.Count > 0) ? members.Count : 1;
            totalRows = Mathf.CeilToInt((float)memberCount / Mathf.Max(1, maxColumns));
        }

        float offsetX = (c - (maxColumns - 1) * 0.5f) * spacing;
        float offsetZ = ((totalRows - 1) * 0.5f - r) * spacing;
        return new Vector3(offsetX, 0f, offsetZ);
    }

    public Vector3 CalculateSlotLocalOffset(int r, int c, int countInThisRow, int totalRows)
    {
        return CalculateSlotLocalOffset(r, c, countInThisRow, totalRows, currentColumns);
    }

    public Vector3 CalculateSlotLocalOffset(int r, int c, int countInThisRow)
    {
        int rows = (totalGridRows > 0) ? totalGridRows : Mathf.CeilToInt((float)(members != null ? members.Count : 1) / Mathf.Max(1, currentColumns));
        return CalculateSlotLocalOffset(r, c, countInThisRow, rows, currentColumns);
    }

    public Vector3 CalculateSlotWorldPosition(int r, int c, int countInThisRow, int totalRows, int maxColumns, Vector3 centerPos, Quaternion rot)
    {
        Vector3 localOffset = CalculateSlotLocalOffset(r, c, countInThisRow, totalRows, maxColumns);
        return centerPos + (rot * localOffset);
    }

    public Vector3 CalculateSlotWorldPosition(int r, int c, int countInThisRow, int totalRows, Vector3 centerPos, Quaternion rot)
    {
        return CalculateSlotWorldPosition(r, c, countInThisRow, totalRows, currentColumns, centerPos, rot);
    }

    public Vector3 CalculateSlotWorldPosition(int r, int c, Vector3 centerPos, Quaternion rot)
    {
        return CalculateSlotWorldPosition(r, c, currentColumns, totalGridRows, currentColumns, centerPos, rot);
    }

    [Header("진형 가로/세로 비율 (Width & Length Multipliers)")]
    public float formationWidthMultiplier = 1.0f;   // 횡방향 밀집/산개 (Alt+우클릭)
    public float formationLengthMultiplier = 1.0f;  // 종방향 길이/종심 (Alt+좌클릭)

    [Header("사각방진/원형진 겹수 및 크기")]
    public int formationLayers = 2;                         // 방진 겹수 (Alt+좌클릭 드래그 시 크기에 맞춰 자동 연동)
    public float hollowRadiusMultiplier = 1.0f;             // 내부 빈 공간 크기 배율 (Alt+좌클릭)
    public float formationLayersSpacingMultiplier = 1.0f;   // 🛡️ 방진 열(층간) 거리 배율 (Alt+우클릭, 기본 1.0x, 범위 0.3x ~ 4.0x)

    public List<SlotInfo> GenerateFormationSlots(int memberCount, int columns, out int outTotalRows, SquadFormationType formType)
    {
        List<SlotInfo> slots = new List<SlotInfo>();
        if (memberCount <= 0)
        {
            outTotalRows = 0;
            return slots;
        }

        // 산개 대형 토글 여부에 따라 간격 2배 확대 및 가로/세로 독립 비율 적용
        float baseSpacing = isLooseFormation ? (spacing * 2.0f) : spacing;
        float currentSpacingX = baseSpacing * formationWidthMultiplier;
        float currentSpacingZ = baseSpacing * formationLengthMultiplier;

        if (formType == SquadFormationType.Diamond)
        {
            // 마름모 대형: 1명 시작 -> 중앙 최대 -> 1명 끝
            List<int> rowCounts = new List<int>();
            int currentStep = 1;
            int remaining = memberCount;

            // 전반부 (홀수 증가: 1, 3, 5, 7 ...)
            while (remaining > 0)
            {
                int count = Mathf.Min(currentStep, remaining);
                rowCounts.Add(count);
                remaining -= count;
                currentStep += 2;
                if (remaining <= (memberCount / 2)) break;
            }

            // 후반부 (홀수 감소: ... 7, 5, 3, 1)
            currentStep = Mathf.Max(1, currentStep - 4);
            while (remaining > 0)
            {
                int count = Mathf.Min(currentStep, remaining);
                rowCounts.Add(count);
                remaining -= count;
                currentStep = Mathf.Max(1, currentStep - 2);
            }

            outTotalRows = rowCounts.Count;

            for (int r = 0; r < outTotalRows; r++)
            {
                int countInThisRow = rowCounts[r];
                for (int i = 0; i < countInThisRow; i++)
                {
                    float offsetX = (i - (countInThisRow - 1) * 0.5f) * currentSpacingX;
                    float offsetZ = ((outTotalRows - 1) * 0.5f - r) * currentSpacingZ;

                    slots.Add(new SlotInfo
                    {
                        row = r,
                        col = i,
                        countInRow = countInThisRow,
                        localOffset = new Vector3(offsetX, 0f, offsetZ),
                        localRotation = Quaternion.identity
                    });
                }
            }
        }
        else if (formType == SquadFormationType.Wedge)
        {
            // 삼각 쐐기 대형: 1명 선봉 -> 2명 -> 3명 -> 4명 ... 뒤로 갈수록 점진적 확장 (중심 피벗 Center Pivot)
            List<int> rowCounts = new List<int>();
            int currentStep = 1;
            int remaining = memberCount;

            while (remaining > 0)
            {
                int count = Mathf.Min(currentStep, remaining);
                rowCounts.Add(count);
                remaining -= count;
                currentStep++;
            }

            outTotalRows = rowCounts.Count;

            for (int r = 0; r < outTotalRows; r++)
            {
                int countInThisRow = rowCounts[r];
                for (int i = 0; i < countInThisRow; i++)
                {
                    float offsetX = (i - (countInThisRow - 1) * 0.5f) * currentSpacingX;
                    float offsetZ = ((outTotalRows - 1) * 0.5f - r) * currentSpacingZ;

                    slots.Add(new SlotInfo
                    {
                        row = r,
                        col = i,
                        countInRow = countInThisRow,
                        localOffset = new Vector3(offsetX, 0f, offsetZ),
                        localRotation = Quaternion.identity
                    });
                }
            }
        }
        else if (formType == SquadFormationType.Square)
        {
            // 🟦 중공 사각방진 (Hollow Square): 
            // - 유닛 간격 1.0m 초밀착 방패벽 고정
            // - Alt+좌클릭: 1열 단위로 얇아지면서 넓어짐
            // - Alt+우클릭: 층간 거리(stepD)만 벌림
            float stepD = currentSpacingZ * formationLayersSpacingMultiplier; // 전후 층간 간격
            float spacingS = Mathf.Max(0.75f, currentSpacingX * 0.85f); // 좌우 유닛 간격 (어깨 맞댄 초밀착 방패벽 ~1.0m)

            int maxPossibleLayers = Mathf.Clamp(memberCount / 4, 1, 32);
            int numLayers = Mathf.Clamp(formationLayers, 1, maxPossibleLayers);
            outTotalRows = numLayers;

            // 1. 외벽 반폭 산출: 현재 겹수(numLayers)에 정확히 맞추어 병사 간격이 1.0m로 100% 촘촘하게 고정됨
            float xOuter = ((memberCount * spacingS) / (8f * numLayers)) + (((numLayers - 1) * 0.5f) * stepD);
            xOuter = Mathf.Max((numLayers - 1) * stepD + 0.6f, xOuter);

            float[] layerHalfX = new float[numLayers];
            float totalCapacity = 0f;

            // 2. 외벽부터 안쪽으로 층간 거리(stepD)만큼 밀착 적층
            for (int l = 0; l < numLayers; l++)
            {
                layerHalfX[l] = Mathf.Max(0.5f, xOuter - (l * stepD));
                float perimeterUnits = (layerHalfX[l] * 8f) / spacingS;
                totalCapacity += perimeterUnits;
            }

            // 3. 각 레이어별 둘레 인원 정밀 배분 (합계 = memberCount)
            int remaining = memberCount;
            int[] layerUnitCounts = new int[numLayers];

            for (int l = 0; l < numLayers; l++)
            {
                if (l == numLayers - 1)
                {
                    layerUnitCounts[l] = remaining;
                }
                else
                {
                    float p = (layerHalfX[l] * 8f) / spacingS;
                    int units = Mathf.Clamp(Mathf.RoundToInt(memberCount * (p / totalCapacity)), 4, Mathf.Max(4, remaining - (numLayers - 1 - l) * 4));
                    layerUnitCounts[l] = units;
                    remaining -= units;
                }
            }

            // 4. 각 레이어별 4변 균등 배치
            for (int layer = 0; layer < numLayers; layer++)
            {
                int layerUnits = layerUnitCounts[layer];
                if (layerUnits <= 0) continue;

                float halfExtentX = layerHalfX[layer] * formationWidthMultiplier;
                float halfExtentZ = layerHalfX[layer] * formationLengthMultiplier;

                int nNorth = Mathf.CeilToInt((float)layerUnits / 4.0f);
                int nEast = Mathf.CeilToInt((float)(layerUnits - nNorth) / 3.0f);
                int nSouth = Mathf.CeilToInt((float)(layerUnits - nNorth - nEast) / 2.0f);
                int nWest = layerUnits - nNorth - nEast - nSouth;

                int[] sideCounts = new int[] { nNorth, nEast, nSouth, nWest };
                int layerCol = 0;

                for (int side = 0; side < 4; side++)
                {
                    int countOnSide = sideCounts[side];
                    if (countOnSide <= 0) continue;

                    for (int i = 0; i < countOnSide; i++)
                    {
                        float t = (countOnSide > 1) ? ((float)i / (countOnSide - 1)) : 0.5f;
                        Vector3 pos = Vector3.zero;
                        Quaternion rot = Quaternion.identity;

                        if (side == 0) // 북쪽 변 (전방 0도)
                        {
                            float x = Mathf.Lerp(-halfExtentX, halfExtentX, t);
                            pos = new Vector3(x, 0f, halfExtentZ);
                            rot = Quaternion.identity;
                        }
                        else if (side == 1) // 동쪽 변 (우측 90도)
                        {
                            float z = Mathf.Lerp(halfExtentZ, -halfExtentZ, t);
                            pos = new Vector3(halfExtentX, 0f, z);
                            rot = Quaternion.Euler(0f, 90f, 0f);
                        }
                        else if (side == 2) // 남쪽 변 (후방 180도)
                        {
                            float x = Mathf.Lerp(halfExtentX, -halfExtentX, t);
                            pos = new Vector3(x, 0f, -halfExtentZ);
                            rot = Quaternion.Euler(0f, 180f, 0f);
                        }
                        else if (side == 3) // 서쪽 변 (좌측 -90도)
                        {
                            float z = Mathf.Lerp(-halfExtentZ, halfExtentZ, t);
                            pos = new Vector3(-halfExtentX, 0f, z);
                            rot = Quaternion.Euler(0f, -90f, 0f);
                        }

                        slots.Add(new SlotInfo
                        {
                            row = layer,
                            col = layerCol++,
                            countInRow = layerUnits,
                            localOffset = pos,
                            localRotation = rot
                        });
                    }
                }
            }
        }
        else if (formType == SquadFormationType.Circle)
        {
            // 🔵 중공 원형진 (Hollow Circle / Orb): 
            // - 유닛 간격 1.0m 초밀착 방패벽 고정
            // - Alt+좌클릭: 1열 단위로 얇아지면서 넓어짐
            // - Alt+우클릭: 층간 거리(stepD)만 벌림
            float stepD = baseSpacing * formationLayersSpacingMultiplier;
            float spacingS = Mathf.Max(0.75f, baseSpacing * 0.85f); // 어깨 맞댄 초밀착 방패벽 ~1.0m

            int maxPossibleLayers = Mathf.Clamp(memberCount / 4, 1, 32);
            int numLayers = Mathf.Clamp(formationLayers, 1, maxPossibleLayers);
            outTotalRows = numLayers;

            // 1. 외벽 반경 산출: 현재 겹수(numLayers)에 정확히 맞추어 둘레 간격이 1.0m로 100% 촘촘하게 고정됨
            float rOuter = ((memberCount * spacingS) / (2f * Mathf.PI * numLayers)) + (((numLayers - 1) * 0.5f) * stepD);
            rOuter = Mathf.Max((numLayers - 1) * stepD + 0.6f, rOuter);

            float[] layerRadii = new float[numLayers];
            float totalCapacity = 0f;

            // 2. 외벽부터 안쪽으로 층간 거리(stepD)만큼 동심원 적층
            for (int l = 0; l < numLayers; l++)
            {
                layerRadii[l] = Mathf.Max(0.5f, rOuter - (l * stepD));
                totalCapacity += (2f * Mathf.PI * layerRadii[l]) / spacingS;
            }

            int remaining = memberCount;
            int[] layerUnitCounts = new int[numLayers];

            for (int l = 0; l < numLayers; l++)
            {
                if (l == numLayers - 1)
                {
                    layerUnitCounts[l] = remaining;
                }
                else
                {
                    float cap = (2f * Mathf.PI * layerRadii[l]) / spacingS;
                    int units = Mathf.Clamp(Mathf.RoundToInt(memberCount * (cap / totalCapacity)), 3, Mathf.Max(3, remaining - (numLayers - 1 - l) * 3));
                    layerUnitCounts[l] = units;
                    remaining -= units;
                }
            }

            for (int layer = 0; layer < numLayers; layer++)
            {
                int layerUnits = layerUnitCounts[layer];
                if (layerUnits <= 0) continue;

                float r = layerRadii[layer];
                float angleStep = 360f / Mathf.Max(1, layerUnits);
                float staggerOffset = (layer % 2 == 1) ? (angleStep * 0.5f) : 0f;

                for (int i = 0; i < layerUnits; i++)
                {
                    float deg = (i * angleStep) + staggerOffset;
                    float rad = deg * Mathf.Deg2Rad;

                    Vector3 pos = new Vector3(Mathf.Sin(rad) * r * formationWidthMultiplier, 0f, Mathf.Cos(rad) * r * formationLengthMultiplier);
                    Quaternion rot = Quaternion.Euler(0f, deg, 0f);

                    slots.Add(new SlotInfo
                    {
                        row = layer,
                        col = i,
                        countInRow = layerUnits,
                        localOffset = pos,
                        localRotation = rot
                    });
                }
            }
        }
        else
        {
            // Normal / Loose (표준 사각 대형 및 산개 대형: 실질적 방진 정중앙이 Z=0이 되는 Center Pivot & 곡선 지원)
            outTotalRows = Mathf.CeilToInt((float)memberCount / Mathf.Max(1, columns));
            int processed = 0;
            for (int r = 0; r < outTotalRows; r++)
            {
                int countInThisRow = Mathf.Min(columns, memberCount - processed);
                float startColOffset = (columns - countInThisRow) * 0.5f; // 자투리 열 완벽 중앙 정렬

                for (int i = 0; i < countInThisRow; i++)
                {
                    float virtualCol = startColOffset + i;
                    int colInt = Mathf.RoundToInt(virtualCol);

                    Vector3 localOffset = CalculateCurvedSlotOffset(r, virtualCol, columns, outTotalRows, formType);

                    slots.Add(new SlotInfo
                    {
                        row = r,
                        col = colInt,
                        countInRow = countInThisRow,
                        localOffset = localOffset,
                        localRotation = Quaternion.identity
                    });
                }
                processed += countInThisRow;
            }
        }

        return slots;
    }

    public List<SlotInfo> GenerateFormationSlots(int memberCount, int columns, out int outTotalRows)
    {
        return GenerateFormationSlots(memberCount, columns, out outTotalRows, currentFormationType);
    }

    public void ToggleRunMode()
    {
        SetRunMode(!isRunning);
    }

    public void SetRunMode(bool run)
    {
        isRunning = run;
        targetSpeed = isRunning ? runSpeed : walkSpeed;
        foreach (Unit u in members)
        {
            if (u != null)
            {
                u.SetRunMode(isRunning, targetSpeed);
            }
        }

        // ⚡ 순수 ECS 엔티티 속도 동기화
        if (members.Count == 0 && initialUnitCount > 0)
        {
            UpdateECSEntitiesSpeed(targetSpeed);
        }
    }

    private void UpdateECSEntitiesSpeed(float speed)
    {
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(
            typeof(MiniTotalWar.ECS.UnitEntityTag),
            typeof(MiniTotalWar.ECS.UnitMovementData),
            typeof(MiniTotalWar.ECS.UnitCombatData)
        );

        using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
        using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
        {
            int squadId = GetInstanceID();
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].SquadId == squadId && tags[i].IsAlive == 1)
                {
                    Entity ent = entities[i];
                    var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                    var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);
                    mov.MoveSpeed = speed;
                    combat.ChargeSpeed = 4.8f;
                    em.SetComponentData(ent, mov);
                    em.SetComponentData(ent, combat);
                }
            }
        }
    }

    public void ToggleLooseFormation()
    {
        isLooseFormation = !isLooseFormation;
        customSpacingMultiplier = 1.0f; // 🌟 산개진형 토글 시 커스텀 간격 초기화!
        spacingX = isLooseFormation ? DEFAULT_LOOSE_SPACING_X : DEFAULT_SPACING_X;
        spacingZ = isLooseFormation ? DEFAULT_LOOSE_SPACING_Z : DEFAULT_SPACING_Z;
        spacing = (spacingX + spacingZ) * 0.5f;

        RebuildGridStructure(currentColumns, forceSpatialSort: true);
        if (!isMoving)
        {
            CommandMoveWithFormation(transform.position, transform.rotation, currentColumns, forceSort: true, UnitCommandState.Idle);
        }
    }

    /// <summary>
    /// 병사들이 중심부에서 서로 겹치지 않고 가장 콤팩트하게 밀착할 수 있는 최적의 최대 겹수(Layers)를 계산합니다.
    /// </summary>
    public static int CalculateOptimalHollowLayers(int memberCount, SquadFormationType formType)
    {
        if (memberCount <= 12) return 1;
        if (memberCount <= 28) return 2;
        if (memberCount <= 52) return 3;
        if (memberCount <= 84) return 4;
        if (memberCount <= 124) return 5;
        if (memberCount <= 172) return 6;
        if (memberCount <= 228) return 7;
        if (memberCount <= 292) return 8;

        return Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(memberCount * 0.22f)), 1, 16);
    }

    public void SetFormationType(SquadFormationType formType)
    {
        currentFormationType = formType;

        if (formType == SquadFormationType.Square || formType == SquadFormationType.Circle)
        {
            // 🛡️ 처음 방진 형성 시 중심부에서 겹치지 않는 한 가장 좁고 가장 많은 겹수의 콤팩트 방진 형성
            formationLayers = CalculateOptimalHollowLayers(MemberCount, formType);
            hollowRadiusMultiplier = 1.0f;
        }
        else
        {
            formationWidthMultiplier = 1.0f;
            formationLengthMultiplier = 1.0f;
        }

        RebuildGridStructure(currentColumns, forceSpatialSort: true);
        if (!isMoving)
        {
            CommandMoveWithFormation(transform.position, transform.rotation, currentColumns, forceSort: true, UnitCommandState.Idle);
        }
    }

    public void RebuildGridStructure(int targetCols = -1, bool forceSpatialSort = false)
    {
        members.RemoveAll(m => m == null);
        int memberCount = MemberCount;
        if (memberCount <= 0) return;

        int chosenCols = (targetCols > 0) ? targetCols : currentColumns;
        currentColumns = Mathf.Clamp(chosenCols, 1, memberCount);

        List<SlotInfo> slots = GenerateFormationSlots(memberCount, currentColumns, out totalGridRows, currentFormationType);
        if (members.Count == 0) return;

        // 대규모 군단(1500명 초과)일 때는 무거운 O(N^2) 정렬을 건너뛰어 즉각 반응성 확보
        if (forceSpatialSort && memberCount <= 1500)
        {
            Quaternion invTargetRot = Quaternion.Inverse(transform.rotation);
            Vector3 refPos = transform.position;

            List<Unit> unassigned = new List<Unit>(members);
            List<Unit> sortedMembers = new List<Unit>();

            foreach (var slot in slots)
            {
                Unit bestUnit = null;
                float minCost = float.MaxValue;

                for (int i = 0; i < unassigned.Count; i++)
                {
                    Unit u = unassigned[i];
                    Vector3 localPos = invTargetRot * (u.transform.position - refPos);

                    float dx = Mathf.Abs(localPos.x - slot.localOffset.x);
                    float dz = Mathf.Abs(localPos.z - slot.localOffset.z);
                    float cost = (dx * 4.0f) + dz;

                    if (cost < minCost)
                    {
                        minCost = cost;
                        bestUnit = u;
                    }
                }

                if (bestUnit != null)
                {
                    bestUnit.SetGridPosition(slot.row, slot.col);
                    sortedMembers.Add(bestUnit);
                    unassigned.Remove(bestUnit);
                }
            }

            if (unassigned.Count > 0)
            {
                sortedMembers.AddRange(unassigned);
            }

            members = sortedMembers;
        }

        for (int i = 0; i < members.Count; i++)
        {
            Unit u = members[i];
            if (u == null) continue;

            int r = u.Row;
            int c = u.Col;

            // 정렬이 강제되었거나 아직 좌표가 할당되지 않은 경우에만 슬롯에서 부여
            if (forceSpatialSort || r < 0 || c < 0)
            {
                if (i < slots.Count)
                {
                    r = slots[i].row;
                    c = slots[i].col;
                    u.SetGridPosition(r, c);
                }
            }

            Vector3 localOffset = (i < slots.Count) ? slots[i].localOffset : GetSlotLocalOffset(r, c, currentColumns, totalGridRows, currentFormationType);
            Vector3 currentWorldPos = transform.position + (transform.rotation * localOffset);

            u.SetInitialFormationPosition(this, currentWorldPos, localOffset, i, r, c);
        }
    }

    public void CommandMoveWithFormation(Vector3 destination, Quaternion rotation, int columns = -1, bool forceSort = false, UnitCommandState cmdState = UnitCommandState.Move)
    {
        int targetCols = (columns > 0) ? columns : currentColumns;
        bool colChanged = (targetCols != currentColumns);
        if (targetCols > 0) currentColumns = targetCols;

        if (alignmentCoroutine != null) { StopCoroutine(alignmentCoroutine); alignmentCoroutine = null; }
        if (formationAssignmentCoroutine != null) { StopCoroutine(formationAssignmentCoroutine); formationAssignmentCoroutine = null; }
        if (staggeredMovementCoroutine != null) { StopCoroutine(staggeredMovementCoroutine); staggeredMovementCoroutine = null; }

        bool wasMoving = isMoving;
        isMoving = true;
        hasAlignedThisArrival = false;
        moveStartGraceTimer = 0f;
        moveDestination = destination;
        targetSquadRotation = rotation;
        currentCommandState = cmdState;

        if (cmdState == UnitCommandState.AttackMove)
        {
            hasAttackMoveDestination = true;
            originalAttackMoveDestination = destination;
            originalAttackMoveRotation = rotation;
            
            // 🚨 버그 수정: AttackMove(돌격) 명령 시에도 모든 부대원의 상태(currentState)를 2로 확실하게 갱신!
            foreach (Unit u in members)
            {
                if (u != null)
                {
                    u.currentState = cmdState;
                    if (UnitJobSimulationManager.Instance != null)
                    {
                        UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(u, destination, rotation, cmdState);
                    }
                }
            }
        }
        else
        {
            hasAttackMoveDestination = false;
            currentTargetSquad = null;
            hasActiveEnemyTarget = false;
            targetEnemyWidth = -1f;
            currentEncirclementFactor = 0f;
            SyncTargetSquadIdToSimulations(-1);

            // 🚨 Frame 0 즉시 교전 이탈: 모든 부대원의 상태를 즉시 Move(1)로 변경하고 타겟 해제
            foreach (Unit u in members)
            {
                if (u != null)
                {
                    u.currentState = cmdState;
                    if (UnitJobSimulationManager.Instance != null)
                    {
                        UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(u, destination, rotation, cmdState);
                    }
                }
            }
        }

        float angleDiff = Quaternion.Angle(transform.rotation, rotation);
        bool isLargeTurn = angleDiff >= 135f;
        bool shouldSpatialSort = (isLargeTurn || colChanged || forceSort) && (members.Count <= 1500);

        if (members.Count == 0 && initialUnitCount > 0)
        {
            UpdateECSEntitiesTarget(destination, rotation, currentColumns, cmdState);
            return;
        }

        RebuildGridStructure(currentColumns, forceSpatialSort: false);

        List<Unit> validMembers = new List<Unit>(members);
        validMembers.RemoveAll(m => m == null);

        formationAssignmentCoroutine = StartCoroutine(AssignFormationPositionsRoutine(validMembers, targetSquadRotation, isLargeTurn, shouldSpatialSort));
    }

    private void UpdateECSEntitiesTarget(Vector3 destination, Quaternion rotation, int cols, UnitCommandState cmdState)
    {
        moveDestination = destination;
        targetSquadRotation = rotation;

        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(
            typeof(MiniTotalWar.ECS.UnitEntityTag),
            typeof(MiniTotalWar.ECS.UnitMovementData),
            typeof(MiniTotalWar.ECS.UnitCombatData)
        );

        using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            int squadId = GetInstanceID();
            List<Entity> myEntities = new List<Entity>();
            List<Vector3> myEntityCurrentPositions = new List<Vector3>();

            for (int i = 0; i < entities.Length; i++)
            {
                Entity ent = entities[i];
                var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(ent);
                if (tag.SquadId == squadId && tag.IsAlive == 1)
                {
                    myEntities.Add(ent);
                    var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                    myEntityCurrentPositions.Add((Vector3)mov.Position);
                }
            }

            int myEntityCount = myEntities.Count;
            if (myEntityCount == 0) return;

            float angleDiff = Quaternion.Angle(transform.rotation, rotation);
            bool isLargeTurn = angleDiff >= 135f;

            // 🛡️ 실제 생존 인원수(myEntityCount)에 맞춰 열 수(cols) 및 슬롯 개수를 완벽히 동기화
            cols = Mathf.Clamp(cols, 1, myEntityCount);
            currentColumns = cols;

            var slots = GenerateFormationSlots(myEntityCount, cols, out totalGridRows, currentFormationType);

            // 🛡️ [정통 토탈워 종대 불변 앞열 보충 정렬]:
            // 기존 Row(앞열 우선) 및 Col(좌측 우선) 순서대로 정렬하여 전사자로 생긴 구멍을 메우고 연속된 인덱스를 확보
            myEntities.Sort((a, b) =>
            {
                var tagA = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(a);
                var tagB = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(b);
                if (tagA.Row != tagB.Row) return tagA.Row.CompareTo(tagB.Row);
                return tagA.Col.CompareTo(tagB.Col);
            });

            transform.rotation = rotation;

            if (isLargeTurn)
            {
                // 🔄 180도 대회전: 자투리 행 가운데 정렬(Center Alignment) 및 수직 전진 채우기(Column-wise Row Shifting)
                int rows = totalGridRows;
                Entity[,] grid = new Entity[rows, cols];
                int lastRowCount = myEntityCount - (rows - 1) * cols;
                int startColOffset = Mathf.Max(0, (cols - lastRowCount) / 2);

                // 1. 압축된 순서 기준 그리드 채우기 (자투리 행은 가운데 정렬 c로 안착)
                for (int i = 0; i < myEntities.Count; i++)
                {
                    Entity ent = myEntities[i];
                    int idx = i;
                    int r = idx / cols;
                    int cInRow = idx % cols;
                    int c = (r == rows - 1) ? (startColOffset + cInRow) : cInRow;

                    if (r < rows && c < cols) grid[r, c] = ent;
                }

                // 2. 180도 반전 (자투리가 1열의 가운데로 1:1 제자리 안착)
                Entity[,] newGrid = new Entity[rows, cols];
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if (grid[r, c] != Entity.Null)
                        {
                            newGrid[(rows - 1) - r, (cols - 1) - c] = grid[r, c];
                        }
                    }
                }

                // 3. 수직 전진 채우기: 1열의 양옆 빈 구멍을 바로 뒤 열 병사가 1칸씩 앞으로 당겨서 채움
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows - 1; r++)
                    {
                        if (newGrid[r, c] == Entity.Null)
                        {
                            for (int rNext = r + 1; rNext < rows; rNext++)
                            {
                                if (newGrid[rNext, c] != Entity.Null)
                                {
                                    newGrid[r, c] = newGrid[rNext, c];
                                    newGrid[rNext, c] = Entity.Null;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 4. 새로운 슬롯에 1:1 확정 매칭 및 고유 SlotIndex/Row/Col 동기화
                int newSlotIdx = 0;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        Entity ent = newGrid[r, c];
                        if (ent != Entity.Null && newSlotIdx < slots.Count)
                        {
                            var slot = slots[newSlotIdx];
                            var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(ent);
                            var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                            var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                            tag.SlotIndex = newSlotIdx;
                            tag.Row = slot.row;
                            tag.Col = slot.col;

                            mov.TargetPosition = destination + (rotation * slot.localOffset);
                            mov.TargetRotation = rotation * slot.localRotation;

                            // [수정] AttackMove 시 돌격 속도 4.8m/s 강제 적용 (targetSpeed는 도보/달리기 속도이며 돌격 속도가 아님)
                            if (cmdState == UnitCommandState.AttackMove)
                                mov.MoveSpeed = 4.8f;
                            else
                                mov.MoveSpeed = targetSpeed > 0 ? targetSpeed : 1.0f;

                            combat.CurrentState = (int)cmdState;
                            // [수정] TargetSquadId는 기존 값 유지 (SyncTargetSquadIdToSimulations에서 이미 주입됨)

                            em.SetComponentData(ent, tag);
                            em.SetComponentData(ent, mov);
                            em.SetComponentData(ent, combat);

                            newSlotIdx++;
                        }
                    }
                }
            }
            else
            {
                // 🛡️ 통상 이동 및 일반 회전: 압축 정렬된 순서대로 1:1 슬롯 매핑하여 완벽한 방진 유지
                for (int i = 0; i < myEntities.Count; i++)
                {
                    Entity ent = myEntities[i];
                    var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(ent);
                    var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                    var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                    var slot = slots[i];

                    tag.SlotIndex = i;
                    tag.Row = slot.row;
                    tag.Col = slot.col;

                    mov.TargetPosition = destination + (rotation * slot.localOffset);
                    mov.TargetRotation = rotation * slot.localRotation;

                    // [수정] AttackMove 시 돌격 속도 4.8m/s 강제 적용 (targetSpeed는 도보/달리기 속도이며 돌격 속도가 아님)
                    if (cmdState == UnitCommandState.AttackMove)
                        mov.MoveSpeed = 4.8f;
                    else
                        mov.MoveSpeed = targetSpeed > 0 ? targetSpeed : 1.0f;

                    combat.CurrentState = (int)cmdState;
                    // [수정] TargetSquadId는 기존 값 유지 (SyncTargetSquadIdToSimulations에서 이미 주입됨)

                    if (cmdState == UnitCommandState.Move)
                    {
                        combat.EngagementStartTime = 0f;
                        combat.TargetEntity = Unity.Entities.Entity.Null;
                        combat.ChargeImpactReady = 0;
                        combat.KnockbackVelocity = Unity.Mathematics.float3.zero;
                    }

                    em.SetComponentData(ent, tag);
                    em.SetComponentData(ent, mov);
                    em.SetComponentData(ent, combat);
                }
            }
        }
    }

    public void CommandStop()
    {
        currentCommandState = UnitCommandState.Idle;
        currentTargetSquad = null;
        targetEnemyWidth = -1f;
        isMoving = false;
        ClearWaypoints();

        if (alignmentCoroutine != null) { StopCoroutine(alignmentCoroutine); alignmentCoroutine = null; }
        if (formationAssignmentCoroutine != null) { StopCoroutine(formationAssignmentCoroutine); formationAssignmentCoroutine = null; }
        if (staggeredMovementCoroutine != null) { StopCoroutine(staggeredMovementCoroutine); staggeredMovementCoroutine = null; }

        // 🌟 [정지 수정] 이동 목표지와 부대 위치를 유닛들의 실제 물리적 중심(GetVisualCenter())으로 즉시 스냅
        // 이동 방향으로 앞서가던 transform.position으로 인한 아이콘 튐 및 유닛 재전진 100% 원천 차단!
        Vector3 actualCenter = GetVisualCenter();
        transform.position = actualCenter;
        moveDestination = actualCenter;
        targetSquadRotation = transform.rotation;

        if (members != null && members.Count > 0)
        {
            // 현재 멈춘 위치(actualCenter)를 기준으로 제자리 방진 슬롯 재계산하여 흐트러진 대열만 정돈(재정비)
            List<SlotInfo> slots = GenerateFormationSlots(members.Count, currentColumns, out totalGridRows, currentFormationType);

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null) continue;

                member.currentState = UnitCommandState.Idle;
                member.hasSquadCommand = false;
                member.ResetReformTimer();

                var slot = (i < slots.Count) ? slots[i] : default;
                Vector3 localOffset = (i < slots.Count) ? slot.localOffset : Vector3.zero;
                Vector3 inPlaceSlotPos = actualCenter + (transform.rotation * localOffset);

                member.fixedTargetPos = inPlaceSlotPos;
                member.formationOffset = localOffset;
                member.targetRotation = transform.rotation * (i < slots.Count ? slot.localRotation : Quaternion.identity);

                var agent = member.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                    agent.velocity = Vector3.zero;
                }

                // Job 시스템 NativeArray에도 제자리 Idle 상태 주입
                if (UnitJobSimulationManager.HasInstance)
                {
                    UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(member, inPlaceSlotPos, member.targetRotation, UnitCommandState.Idle);
                }
                if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
                {
                    MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateEntityTarget(member, inPlaceSlotPos, member.targetRotation, UnitCommandState.Idle);
                }
            }
        }

        // ✅ [핵심 픽스] 정지 명령 시 ECS/Job 시스템 양쪽에 추격하던 적 부대(TargetSquadId)를 -1로 완벽히 초기화하여 더 이상 쫓지 않게 만듦!
        SyncTargetSquadIdToSimulations(-1);

        if (members.Count == 0 && initialUnitCount > 0)
        {
            UpdateECSEntitiesTarget(actualCenter, transform.rotation, currentColumns, UnitCommandState.Idle);
        }
    }

    public void SetAutoAttack(bool enabled)
    {
        autoAttackEnabled = enabled;

        foreach (var member in members)
        {
            if (member != null) member.autoAttackEnabled = enabled;
        }

        if (members.Count == 0 && initialUnitCount > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                var query = em.CreateEntityQuery(
                    typeof(MiniTotalWar.ECS.UnitEntityTag),
                    typeof(MiniTotalWar.ECS.UnitCombatData)
                );

                using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
                {
                    int squadId = GetInstanceID();
                    for (int i = 0; i < tags.Length; i++)
                    {
                        if (tags[i].SquadId == squadId && tags[i].IsAlive == 1)
                        {
                            Entity ent = entities[i];
                            var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);
                            combat.AutoAttackEnabled = enabled ? 1 : 0;
                            em.SetComponentData(ent, combat);
                        }
                    }
                }
            }
        }
    }

    public void SetStance(UnitStance stance)
    {
        currentStance = stance;

        foreach (var member in members)
        {
            if (member != null) member.currentStance = stance;
        }
    }

    private IEnumerator AssignFormationPositionsRoutine(List<Unit> validMembers, Quaternion targetRotation, bool isLargeTurn, bool shouldSpatialSort)
    {
        int memberCount = validMembers.Count;
        if (memberCount == 0) yield break;

        unitTargetPositions.Clear();
        totalGridRows = Mathf.CeilToInt((float)memberCount / Mathf.Max(1, currentColumns));

        Dictionary<Unit, Vector3> targetPosMap = new Dictionary<Unit, Vector3>();
        Dictionary<Unit, Vector3> localOffsetMap = new Dictionary<Unit, Vector3>();

        transform.rotation = targetRotation;
        List<SlotInfo> slots = GenerateFormationSlots(memberCount, currentColumns, out totalGridRows, currentFormationType);

        if (isLargeTurn)
        {
            // 🔄 180도 대회전: 자투리 행 가운데 정렬(Center Alignment) 및 수직 전진 채우기(Column-wise Row Shifting)
            int cols = currentColumns;
            int rows = totalGridRows;
            Unit[,] grid = new Unit[rows, cols];
            int lastRowCount = memberCount - (rows - 1) * cols;
            int startColOffset = Mathf.Max(0, (cols - lastRowCount) / 2);

            for (int i = 0; i < validMembers.Count; i++)
            {
                Unit u = validMembers[i];
                if (u != null)
                {
                    int r = i / cols;
                    int cInRow = i % cols;
                    int c = (r == rows - 1) ? (startColOffset + cInRow) : cInRow;
                    if (r < rows && c < cols) grid[r, c] = u;
                }
            }

            Unit[,] newGrid = new Unit[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (grid[r, c] != null)
                    {
                        newGrid[(rows - 1) - r, (cols - 1) - c] = grid[r, c];
                    }
                }
            }

            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows - 1; r++)
                {
                    if (newGrid[r, c] == null)
                    {
                        for (int rNext = r + 1; rNext < rows; rNext++)
                        {
                            if (newGrid[rNext, c] != null)
                            {
                                newGrid[r, c] = newGrid[rNext, c];
                                newGrid[rNext, c] = null;
                                break;
                            }
                        }
                    }
                }
            }

            List<Unit> sortedMembers = new List<Unit>();
            int newSlotIdx = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Unit u = newGrid[r, c];
                    if (u != null && newSlotIdx < slots.Count)
                    {
                        var slot = slots[newSlotIdx];
                        u.SetGridPosition(slot.row, slot.col);

                        Vector3 targetWorldPos = moveDestination + (targetRotation * slot.localOffset);
                        Vector3 localOffset = slot.localOffset;

                        targetPosMap[u] = targetWorldPos;
                        localOffsetMap[u] = localOffset;
                        u.targetRotation = targetRotation * slot.localRotation;

                        sortedMembers.Add(u);
                        newSlotIdx++;
                    }
                }
            }

            members = sortedMembers;
        }
        else
        {
            // 🛡️ 통상 이동 및 일반 회전: 기존 유닛 순서(SlotIndex)를 100% 칼같이 유지!
            for (int i = 0; i < validMembers.Count; i++)
            {
                Unit member = validMembers[i];
                if (member == null) continue;

                var slot = (i < slots.Count) ? slots[i] : default;
                Vector3 localOffset = (i < slots.Count) ? slot.localOffset : Vector3.zero;
                Vector3 targetWorldPos = moveDestination + (targetRotation * localOffset);

                targetPosMap[member] = targetWorldPos;
                localOffsetMap[member] = localOffset;

                if (i < slots.Count)
                {
                    member.SetGridPosition(slot.row, slot.col);
                    member.targetRotation = targetRotation * slot.localRotation;
                }
            }
        }

        // 이동 방향과 부대 현재 정면의 내적을 통한 후진(Retreat/Backstep) 여부 판정
        Vector3 moveVec = moveDestination - transform.position;
        moveVec.y = 0f;
        bool isReverseMove = false;
        if (moveVec.sqrMagnitude > 0.05f)
        {
            float forwardDot = Vector3.Dot(transform.forward, moveVec.normalized);
            if (forwardDot < -0.2f)
            {
                isReverseMove = true;
            }
        }

        staggeredMovementCoroutine = StartCoroutine(StartUnitsMovementStaggered(members, targetPosMap, localOffsetMap, targetRotation, isReverseMove));
        yield return null;
    }

    private IEnumerator StartUnitsMovementStaggered(
        List<Unit> targetMembers,
        Dictionary<Unit, Vector3> targetPosMap,
        Dictionary<Unit, Vector3> localOffsetMap,
        Quaternion targetRotation,
        bool isReverseMove)
    {
        int count = targetMembers.Count;
        if (count == 0)
        {
            staggeredMovementCoroutine = null;
            yield break;
        }

        bool isMassiveArmy = count > 1500;
        bool isAttackOrCharge = (currentCommandState == UnitCommandState.AttackMove || currentTargetSquad != null);

        // [핵심 안전장치] 코루틴 중단(Interrupt)이나 갱신 주기로 인한 유닛 멈춤을 원천 방지하기 위해,
        // 모든 유닛에게 1차적으로 즉시 목적지 및 이동 명령을 100% 일괄 하달
        for (int i = 0; i < targetMembers.Count; i++)
        {
            Unit member = targetMembers[i];
            if (member == null) continue;

            if (targetPosMap.TryGetValue(member, out Vector3 targetWorldPos) &&
                localOffsetMap.TryGetValue(member, out Vector3 localOffset))
            {
                unitTargetPositions[member] = targetWorldPos;

                NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.speed = Mathf.Min(targetSpeed, unitMaxSpeed);
                    agent.acceleration = isRunning ? 3.5f : 2.0f;
                    agent.angularSpeed = 260f;
                    agent.stoppingDistance = 0.01f;
                    agent.radius = 0.05f;
                    agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                    agent.SetDestination(targetWorldPos);
                }

                int originalIndex = targetMembers.IndexOf(member);
                member.SetInitialFormationPosition(this, targetWorldPos, localOffset, originalIndex, member.Row, member.Col);
                member.MoveToFixedSquadPosition(this, targetWorldPos, localOffset, originalIndex, targetRotation, currentCommandState);
            }
        }

        // 공격/돌격 중이거나 대규모 군단일 때는 추가 지연 없이 즉시 완료 (전원 일제 쇄도)
        if (isAttackOrCharge || isMassiveArmy || rowReactionDelay <= 0.001f)
        {
            staggeredMovementCoroutine = null;
            yield break;
        }

        // 1. 유닛들을 행(Row/Rank)별로 그룹화
        Dictionary<int, List<Unit>> rowGroups = new Dictionary<int, List<Unit>>();
        foreach (Unit u in targetMembers)
        {
            if (u == null) continue;
            int r = u.Row >= 0 ? u.Row : 0;
            if (!rowGroups.TryGetValue(r, out var list))
            {
                list = new List<Unit>();
                rowGroups[r] = list;
            }
            list.Add(u);
        }

        // 2. 출발 우선순위 정렬 (일반 제식 이동 시에만 물결 연출)
        List<int> sortedRowKeys = new List<int>(rowGroups.Keys);
        if (isReverseMove)
        {
            sortedRowKeys.Sort((a, b) => b.CompareTo(a)); // 내림차순 (뒷열 우선)
        }
        else
        {
            sortedRowKeys.Sort((a, b) => a.CompareTo(b)); // 오름차순 (앞열 우선)
        }

        for (int rIdx = 0; rIdx < sortedRowKeys.Count; rIdx++)
        {
            if (rIdx < sortedRowKeys.Count - 1 && rowReactionDelay > 0f)
            {
                yield return new WaitForSeconds(rowReactionDelay);
            }
        }
        staggeredMovementCoroutine = null;
    }

    private float enemyAiCheckTimer = 0f;
    private bool wasEngagedInCombat = false;
    private float postCombatReformTimer = 0f;

    public Squad currentTargetSquad = null;
    private float targetTrackTimer = 0f;
    private Vector3 lastTrackedTargetPos = Vector3.zero;
    private float lastTrackedEnemyWidth = 0f;
    private float lastTrackedEnemyDepth = 0f;

    private void Update()
    {
        UpdateSquadCenter();
        UpdateEnemySquadAI();
        UpdateTargetSquadTracking();
        UpdatePostCombatAutoReform();
        UpdateAutoAttack();

        if (isMoving)
        {
            moveStartGraceTimer += Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetSquadRotation, maxRotationSpeed * Time.deltaTime);
            CheckAndExecuteArrival();
        }
        else if (alignmentCoroutine == null && waypointQueue.Count > 0)
        {
            WaypointData nextWp = waypointQueue[0];
            waypointQueue.RemoveAt(0);
            CommandMoveWithFormation(nextWp.destination, nextWp.rotation, nextWp.columns, forceSort: false);
        }

        UpdatePathLine();
        DrawEnvelopmentDebugLines();
    }

    private void DrawEnvelopmentDebugLines()
    {
        if (hasActiveEnemyTarget && currentTargetSquad != null)
        {
            Vector3 basePos = transform.position;
            Quaternion baseRot = transform.rotation;
            Vector3 enemyCenter = currentTargetSquad.GetVisualCenter();

            for (int i = 0; i < members.Count; i++)
            {
                Unit u = members[i];
                if (u != null)
                {
                    Vector3 slotLocal = GetSlotLocalOffset(u.Row, u.Col, currentColumns, totalGridRows, currentFormationType);
                    Vector3 slotWorld = basePos + (baseRot * slotLocal);
                    // 유닛 -> 슬롯(청록색), 슬롯 -> 적 중심(노란색)
                    Debug.DrawLine(u.transform.position, slotWorld, Color.cyan, 0.05f);
                }
            }
        }
    }

    /// <summary>
    /// 목표 적 부대를 향해 정상 대형으로 직진 돌격한 뒤, 충돌 시 포위망을 발동합니다.
    /// </summary>
    public void CommandAttackSquad(Squad enemySquad)
    {
        if (enemySquad == null || (enemySquad.members.Count == 0 && enemySquad.initialUnitCount == 0)) return;

        currentTargetSquad = enemySquad;

        // ⚔️ [돌격 개편] 공격 명령 즉시 전군 돌격/달리기 모드(Run Mode) 강제 가동 (도보 걷기 배제)
        if (!isRunning)
        {
            SetRunMode(true);
        }

        Vector3 enemyCenter = enemySquad.GetVisualCenter();
        Vector3 myPos = GetVisualCenter();
        Vector3 dirToEnemy = enemyCenter - myPos;
        dirToEnemy.y = 0f;

        Quaternion faceRot = (dirToEnemy.sqrMagnitude > 0.001f)
            ? Quaternion.LookRotation(dirToEnemy.normalized)
            : transform.rotation;

        // 돌격 시선 축 기준으로 적의 360도 최근접 접적면(Z_min) 사전 계산
        UpdateEnemyProjectionData(enemySquad, faceRot, enemyCenter);

        // 🌟 핵심: 공격 명령을 내리는 즉시 포위 대형(날개 전진 U자형)으로 전개하여 접근!
        // 1자를 만든 뒤 지체하는 현상을 100% 제거하고, 이동하면서부터 양 날개가 전진하여 감싸며 쇄도
        hasActiveEnemyTarget = true;
        targetEnemyWidth = maxEnemyProjectionX - minEnemyProjectionX;

        int myMemberCount = MemberCount;
        int rows = Mathf.CeilToInt((float)myMemberCount / Mathf.Max(1, currentColumns));
        float currentSpacing = (currentFormationType == SquadFormationType.Loose || isLooseFormation) ? (spacing * 2.0f) : spacing;
        float baseFrontZ = (rows - 1) * 0.5f * currentSpacing;

        // 🦅 공격 시 전열을 적진 앞에 예쁘게 세우지 않고, 적진 한가운데(Center)를 향해 그대로 쇄도(Charge)하도록 변경!
        // 사용자 피드백 반영: 슬롯에 안착하려고 멈추는 현상을 방지하기 위해 목적지를 적 부대 정중앙으로 밀어넣음.
        Vector3 chargeDestination = enemyCenter; 
        lastTrackedTargetPos = enemyCenter;

        Debug.Log($"<color=#00FFFF><b>[Squad] ⚔️ 적 부대({enemySquad.name}) 중심부를 향해 전군 돌격 개시!</b></color> (거리: {dirToEnemy.magnitude:F1}m)");

        // 포위 대형으로 돌격 시작 (양쪽 시뮬레이션 매니저 모두에 목표 부대 ID 확실하게 주입)
        SyncTargetSquadIdToSimulations(enemySquad.GetInstanceID());
        CommandMoveWithFormation(chargeDestination, faceRot, currentColumns, forceSort: false, cmdState: UnitCommandState.AttackMove);
    }

    /// <summary>
    /// GameObject 모드(Job System) 및 Pure ECS 모드 양쪽에 목표 적 부대 ID를 완벽하게 동기화합니다.
    /// </summary>
    public void SyncTargetSquadIdToSimulations(int targetSquadId)
    {
        if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
        {
            MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateSquadTargetSquadId(this, targetSquadId);
        }
        if (UnitJobSimulationManager.Instance != null)
        {
            UnitJobSimulationManager.Instance.UpdateSquadTargetSquadId(this, targetSquadId);
        }
    }

    public void UpdateEnemyProjectionData(Squad enemySquad, Quaternion faceRot, Vector3 enemyPos)
    {
        Vector3 rightToEnemy = faceRot * Vector3.right;
        Vector3 forwardToEnemy = faceRot * Vector3.forward;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        int activeEnemyCount = 0;
        float maxAllowedWidth = 100f;
        float maxAllowedDepth = 60f;

        bool useECS = (BattleManager.Instance != null && BattleManager.Instance.usePureECS);
        if (!useECS)
        {
            for (int i = 0; i < enemySquad.members.Count; i++)
            {
                Unit enemy = enemySquad.members[i];
                if (enemy != null && enemy.currentHp > 0)
                {
                    Vector3 relPos = enemy.transform.position - enemyPos;
                    float projX = Vector3.Dot(relPos, rightToEnemy);
                    float projZ = Vector3.Dot(relPos, forwardToEnemy);

                    if (Mathf.Abs(projX) <= maxAllowedWidth && Mathf.Abs(projZ) <= maxAllowedDepth)
                    {
                        if (projX < minX) minX = projX;
                        if (projX > maxX) maxX = projX;
                        if (projZ < minZ) minZ = projZ;
                        if (projZ > maxZ) maxZ = projZ;
                        activeEnemyCount++;
                    }
                }
            }
        }
        else
        {
            // ⚡ 순수 ECS 모드 적 엔티티 좌표 투영 (ToEntityArray 기반 1:1 안전 조회로 찌그러짐 0%)
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                var query = em.CreateEntityQuery(
                    typeof(MiniTotalWar.ECS.UnitEntityTag),
                    typeof(MiniTotalWar.ECS.UnitMovementData)
                );
                using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    int enemySquadId = enemySquad.GetInstanceID();
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(entities[i]);
                        if (tag.SquadId == enemySquadId && tag.IsAlive == 1)
                        {
                            var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(entities[i]);
                            Vector3 relPos = (Vector3)mov.Position - enemyPos;
                            float projX = Vector3.Dot(relPos, rightToEnemy);
                            float projZ = Vector3.Dot(relPos, forwardToEnemy);

                            if (Mathf.Abs(projX) <= maxAllowedWidth && Mathf.Abs(projZ) <= maxAllowedDepth)
                            {
                                if (projX < minX) minX = projX;
                                if (projX > maxX) maxX = projX;
                                if (projZ < minZ) minZ = projZ;
                                if (projZ > maxZ) maxZ = projZ;
                                activeEnemyCount++;
                            }
                        }
                    }
                }
            }
        }

        if (activeEnemyCount > 0 && minX <= maxX && minZ <= maxZ)
        {
            // 🛡️ 인원수 대비 비정상적 너비 팽창 방지 (최대 허용 폭 클램핑)
            float maxRealisticWidth = Mathf.Max(6.0f, activeEnemyCount * enemySquad.spacing * 1.35f);
            float rawWidth = (maxX - minX) + 0.7f;
            float liveWidth = Mathf.Min(rawWidth, maxRealisticWidth);

            float liveDepth = Mathf.Clamp(maxZ - minZ + 0.5f, 0.5f, maxAllowedDepth);
            float liveCenterX = (minX + maxX) * 0.5f;

            // 만약 적이 좌우로 크게 갈라져서 rawWidth > maxRealisticWidth 인 경우: 주력 덩어리 쪽으로 liveCenterX 보정
            if (rawWidth > maxRealisticWidth * 1.5f)
            {
                liveCenterX = Mathf.Clamp(liveCenterX, -maxRealisticWidth * 0.5f, maxRealisticWidth * 0.5f);
            }

            Vector3 liveContact = enemyPos + (forwardToEnemy * minZ) + (rightToEnemy * liveCenterX);

            int engagedCount = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null && members[i].currentState == UnitCommandState.MeleeEngaged)
                    engagedCount++;
            }

            if (!hasActiveEnemyTarget || contactSurfaceCenter == Vector3.zero || Vector3.Distance(contactSurfaceCenter, liveContact) > 20f)
            {
                targetEnemyWidth = liveWidth;
                enemyDepth = liveDepth;
                enemyCenterOffsetX = liveCenterX;
                contactSurfaceCenter = liveContact;
            }
            else if (engagedCount > 0)
            {
                targetEnemyWidth = Mathf.MoveTowards(targetEnemyWidth, liveWidth, 1.5f * Time.deltaTime);
                enemyDepth = Mathf.MoveTowards(enemyDepth, liveDepth, 1.5f * Time.deltaTime);
                enemyCenterOffsetX = Mathf.MoveTowards(enemyCenterOffsetX, liveCenterX, 1.5f * Time.deltaTime);
                contactSurfaceCenter = Vector3.MoveTowards(contactSurfaceCenter, liveContact, 2.5f * Time.deltaTime);
            }
            else
            {
                targetEnemyWidth = Mathf.MoveTowards(targetEnemyWidth, liveWidth, 4.0f * Time.deltaTime);
                enemyDepth = Mathf.MoveTowards(enemyDepth, liveDepth, 4.0f * Time.deltaTime);
                enemyCenterOffsetX = Mathf.MoveTowards(enemyCenterOffsetX, liveCenterX, 4.0f * Time.deltaTime);
                contactSurfaceCenter = Vector3.MoveTowards(contactSurfaceCenter, liveContact, 6.0f * Time.deltaTime);
            }

            minEnemyProjectionX = minX - 0.35f;
            maxEnemyProjectionX = maxX + 0.35f;
            minEnemyProjectionZ = minZ;
            maxEnemyProjectionZ = maxZ;
            hasActiveEnemyTarget = true;
        }
        else
        {
            float halfWidth = (enemySquad.currentColumns * enemySquad.spacing) * 0.5f;
            float halfDepth = (enemySquad.totalGridRows > 0 ? enemySquad.totalGridRows : 5) * enemySquad.spacing * 0.5f;
            minEnemyProjectionX = -halfWidth - 0.35f;
            maxEnemyProjectionX = halfWidth + 0.35f;
            minEnemyProjectionZ = -halfDepth;
            maxEnemyProjectionZ = halfDepth;
            enemyDepth = halfDepth * 2.0f;
            hasActiveEnemyTarget = true;
            targetEnemyWidth = maxEnemyProjectionX - minEnemyProjectionX;
            enemyCenterOffsetX = 0f;
            contactSurfaceCenter = enemyPos - (forwardToEnemy * halfDepth);
        }

        // 🏃 돌격 전진 시 곡률 동적 변화 계수 (8m 이내 접근 및 돌격 시 0.0 -> 1.0으로 서서히 형성)
        float distToContact = Vector3.Distance(transform.position, contactSurfaceCenter);
        float targetMorph = (hasActiveEnemyTarget && distToContact <= 8.0f) ? 1.0f : 0.0f;
        envelopmentMorph = Mathf.MoveTowards(envelopmentMorph, targetMorph, 1.2f * Time.deltaTime);
    }

    public Vector3 originalAttackMoveDestination;
    public Quaternion originalAttackMoveRotation;
    public bool hasAttackMoveDestination = false;
    public float currentEncirclementFactor = 0f; // 0 (None) ~ 1 (Full Orb)
    public float envelopmentMorph = 0f; // 0.0 (직선) ~ 1.0 (완전 포위 곡률)

    public void DetectEnvelopmentThreat(Squad enemySquad)
    {
        if (enemySquad == null || enemySquad.members == null || enemySquad.members.Count == 0)
        {
            currentEncirclementFactor = Mathf.MoveTowards(currentEncirclementFactor, 0f, 2.0f * Time.deltaTime);
            return;
        }

        Vector3 myCenter = GetVisualCenter();
        Quaternion invRot = Quaternion.Inverse(transform.rotation);

        float halfWidth = (currentColumns * spacingX) * 0.5f;
        float halfDepth = (totalGridRows * spacingZ) * 0.5f;

        int flankThreatCount = 0;
        int rearThreatCount = 0;

        for (int i = 0; i < enemySquad.members.Count; i++)
        {
            Unit enemy = enemySquad.members[i];
            if (enemy != null && enemy.currentHp > 0)
            {
                Vector3 relPos = invRot * (enemy.transform.position - myCenter);
                float x = Mathf.Abs(relPos.x);
                float z = relPos.z;

                if (relPos.sqrMagnitude < 400f) // 20m 이내
                {
                    // 측면 깊이 침투 판정 (내 폭보다 바깥에 위치하면서 측면 깊이에 도달)
                    if (x > halfWidth - 0.2f && z < halfDepth + 0.5f && z > -halfDepth - 1.0f)
                    {
                        flankThreatCount++;
                    }
                    // 배후 차단 판정 (내 등 뒤에 위치)
                    if (z <= -halfDepth + 0.2f)
                    {
                        rearThreatCount++;
                    }
                }
            }
        }

        float targetFactor = 0f;
        if (flankThreatCount > 0 || rearThreatCount > 0)
        {
            targetFactor = 1.0f;
        }

        // 🌟 실시간 부드러운 유기적 보간 (적이 둘러싸는 즉시 신속하고 매끄럽게 4방위 요격 방진 전개)
        currentEncirclementFactor = Mathf.MoveTowards(currentEncirclementFactor, targetFactor, 3.0f * Time.deltaTime);
    }

    public bool CalculateOrganicDefensiveSlot(int row, float col, int columns, int totalRows, SquadFormationType formType, out Vector3 localPos, out Vector3 colNormalDir)
    {
        localPos = Vector3.zero;
        colNormalDir = Vector3.forward;

        float curSpacingX = (formType == SquadFormationType.Loose && !isLooseFormation) ? (DEFAULT_LOOSE_SPACING_X * customSpacingMultiplier) : spacingX;
        float curSpacingZ = (formType == SquadFormationType.Loose && !isLooseFormation) ? (DEFAULT_LOOSE_SPACING_Z * customSpacingMultiplier) : spacingZ;

        if (totalRows <= 0)
        {
            int memberCount = (members != null && members.Count > 0) ? members.Count : 1;
            totalRows = Mathf.CeilToInt((float)memberCount / Mathf.Max(1, columns));
        }

        float baseFrontZ = (totalRows - 1) * 0.5f * curSpacingZ;
        float baseColX = (col - (columns - 1) * 0.5f) * curSpacingX;
        Vector3 rectPos = new Vector3(baseColX, 0f, baseFrontZ - (row * curSpacingZ));

        if (currentEncirclementFactor <= 0.01f)
        {
            localPos = rectPos;
            colNormalDir = Vector3.forward;
            return true;
        }

        // 🛡️ 수비 부대 4방위 능동 요격 (Active 4-Sided Flank Interception):
        // 적이 둘러쌀 때 외곽 열(좌익, 우익, 후방열)이 1.2m 바깥으로 능동 전진하여 마주보고 요격!
        Vector3 outwardDir = Vector3.forward;
        Vector3 interceptOffset = Vector3.zero;

        bool isLeftFlank = (col <= 1.0f);
        bool isRightFlank = (col >= columns - 2.0f);
        bool isRearRank = (row >= totalRows - 1.5f);

        if (isLeftFlank)
        {
            outwardDir = -Vector3.right;
            interceptOffset = -Vector3.right * 1.2f;
        }
        else if (isRightFlank)
        {
            outwardDir = Vector3.right;
            interceptOffset = Vector3.right * 1.2f;
        }
        else if (isRearRank)
        {
            outwardDir = -Vector3.forward;
            interceptOffset = -Vector3.forward * 1.2f;
        }
        else
        {
            outwardDir = Vector3.forward;
        }

        float factor = Mathf.SmoothStep(0f, 1f, currentEncirclementFactor);
        localPos = rectPos + (interceptOffset * factor);
        colNormalDir = Vector3.Slerp(Vector3.forward, outwardDir, factor);

        return true;
    }

    private void UpdateTargetSquadTracking()
    {
        // 🚨 핵심: 플레이어가 이동/후퇴(Move) 명령을 내린 상태라면 적 부대 추적 및 포위 슬롯 갱신을 즉시 중단하여 탈출 보장!
        if (currentCommandState != UnitCommandState.AttackMove) return;

        // 🛡️ 사각방진/원형진은 고유한 진지 사수 방어진형이므로 적을 향해 슬롯을 강제로 이동시키지 않고 제자리 방패벽 유지
        if (currentFormationType == SquadFormationType.Square || currentFormationType == SquadFormationType.Circle) return;

        if (currentTargetSquad == null || currentTargetSquad.MemberCount == 0)
        {
            if (hasActiveEnemyTarget || currentTargetSquad != null)
            {
                currentTargetSquad = null;
                targetEnemyWidth = -1f;
                hasActiveEnemyTarget = false;
                SyncTargetSquadIdToSimulations(-1);
            }

            // ⚔️ 어택땅(AttackMove) 중 전방 30m 내 적 부대 마주침 감지 -> 자동 락온 및 일제 집중 타격
            if (BattleManager.Instance != null)
            {
                var allSquads = BattleManager.Instance.GetAllSquads();
                Squad closestEnemy = null;
                float minSqr = 70.0f * 70.0f; // 🎯 전멸 후 70m 내 다른 적 부대로 즉시 자동 연계 돌격
                Vector3 myCenter = GetVisualCenter();

                foreach (var s in allSquads)
                {
                    if (s == null || s.isPlayer == this.isPlayer || s.MemberCount <= 0) continue;
                    float dSqr = (s.GetVisualCenter() - myCenter).sqrMagnitude;
                    if (dSqr < minSqr)
                    {
                        minSqr = dSqr;
                        closestEnemy = s;
                    }
                }

                if (closestEnemy != null)
                {
                    CommandAttackSquad(closestEnemy);
                }
            }
            return;
        }

        targetTrackTimer += Time.deltaTime;
        if (targetTrackTimer < 0.25f) return;
        targetTrackTimer = 0f;

        Vector3 enemyCenter = currentTargetSquad.GetVisualCenter();
        Vector3 myPos = GetVisualCenter();
        Vector3 myFront = GetFrontLineCenter();
        Vector3 dirToEnemy = enemyCenter - myPos;
        dirToEnemy.y = 0f;
        float distToEnemy = dirToEnemy.magnitude;

        Quaternion faceRot = (dirToEnemy.sqrMagnitude > 0.001f)
            ? Quaternion.LookRotation(dirToEnemy.normalized)
            : transform.rotation;

        UpdateEnemyProjectionData(currentTargetSquad, faceRot, enemyCenter);

        float frontlineDist = Vector3.Distance(myFront, contactSurfaceCenter);

        // ⚔️ 25m 거리 진입 시 부대원 200명 전원 일제 돌격 가속(1.8m/s) 동기화!
        if (frontlineDist <= 25.0f || distToEnemy <= 25.0f)
        {
            if (!isRunning)
            {
                SetRunMode(true);
            }
        }

        int engagedCount = 0;
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != null && members[i].currentState == UnitCommandState.MeleeEngaged)
                engagedCount++;
        }

        bool isContacted = (frontlineDist <= 5.0f || distToEnemy <= 6.0f || engagedCount > 0);

        // 🚨 이미 교전/접촉 중일 때는 슬롯을 180도 뒤집어 재배치하지 않고 현재 돌격 상태 유지!
        if (isContacted)
        {
            return;
        }

        float shiftSqr = (contactSurfaceCenter - lastTrackedTargetPos).sqrMagnitude;

        int myCount = MemberCount;
        int totalRows = Mathf.CeilToInt((float)myCount / Mathf.Max(1, currentColumns));
        float currentSpacing = (currentFormationType == SquadFormationType.Loose || isLooseFormation) ? (spacing * 2.0f) : spacing;
        float baseFrontZ = (totalRows - 1) * 0.5f * currentSpacing;

        // 🦅 실시간 추적 시에도 접적면 앞 0.45m가 아닌 적진 한가운데(Center)를 향해 그대로 쇄도 유지
        Vector3 chargeDestination = enemyCenter;
        if (shiftSqr > 0.5f)
        {
            lastTrackedTargetPos = contactSurfaceCenter;
            lastTrackedEnemyWidth = targetEnemyWidth;
            lastTrackedEnemyDepth = enemyDepth;
            CommandMoveWithFormation(chargeDestination, faceRot, currentColumns, forceSort: false, cmdState: UnitCommandState.AttackMove);
        }
    }

    private void UpdatePostCombatAutoReform()
    {
        if (members.Count == 0) return;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != null && members[i].currentState == UnitCommandState.MeleeEngaged)
            {
                wasEngagedInCombat = true;
                postCombatReformTimer = 0f;
                return;
            }
        }

        if (wasEngagedInCombat)
        {
            bool hasAliveTarget = false;
            if (currentTargetSquad != null && currentTargetSquad.members != null)
            {
                for (int i = 0; i < currentTargetSquad.members.Count; i++)
                {
                    if (currentTargetSquad.members[i] != null && currentTargetSquad.members[i].currentHp > 0)
                    {
                        hasAliveTarget = true;
                        break;
                    }
                }
            }

            if (hasAliveTarget)
            {
                postCombatReformTimer = 0f;
                return;
            }

            postCombatReformTimer += Time.deltaTime;
            if (postCombatReformTimer >= 1.0f)
            {
                wasEngagedInCombat = false;
                postCombatReformTimer = 0f;
                ReformSquadAfterCombat();
            }
        }
    }

    public void ReformSquadAfterCombat()
    {
        members.RemoveAll(m => m == null);
        if (members.Count == 0) return;

        targetEnemyWidth = -1f;
        hasActiveEnemyTarget = false;
        currentTargetSquad = null;
        currentEncirclementFactor = 0f;

        Vector3 visualCenter = GetVisualCenter();
        Quaternion currentRot = transform.rotation;

        transform.position = visualCenter;
        moveDestination = visualCenter;
        targetSquadRotation = currentRot;

        // 🌟 어택땅(AttackMove)으로 가던 중 교전이 끝난 경우: 원래 목적지로 행군 자동 재개!
        if (hasAttackMoveDestination && Vector3.Distance(visualCenter, originalAttackMoveDestination) > 2.0f)
        {
            CommandMoveWithFormation(originalAttackMoveDestination, originalAttackMoveRotation, currentColumns, forceSort: false, cmdState: UnitCommandState.AttackMove);
        }
        else if (waypointQueue.Count > 0)
        {
            WaypointData nextWp = waypointQueue[0];
            waypointQueue.RemoveAt(0);
            CommandMoveWithFormation(nextWp.destination, nextWp.rotation, nextWp.columns, forceSort: false, cmdState: UnitCommandState.AttackMove);
        }
        else
        {
            hasAttackMoveDestination = false;
            CommandMoveWithFormation(visualCenter, currentRot, currentColumns, forceSort: true, cmdState: UnitCommandState.Idle);
        }
    }

    private float autoAttackCheckTimer = 0f;

    /// <summary>
    /// V 모드 (자동 요격 모드): 부대가 대기 중이거나 이동을 완료했을 때, 12m 내로 적이 접근하면 자동으로 부대 전체 돌격 명령을 내립니다.
    /// </summary>
    private void UpdateAutoAttack()
    {
        // 아군(Player)이고, 요격 모드가 켜져 있으며, 현재 단순 이동 중이 아닐 때 작동
        if (!isPlayer || MemberCount <= 0 || !autoAttackEnabled) return;
        if (isMoving && currentCommandState != UnitCommandState.AttackMove) return;
        if (currentTargetSquad != null) return; // 이미 목표가 지정되어 공격 중이면 통과

        autoAttackCheckTimer += Time.deltaTime;
        if (autoAttackCheckTimer < 0.25f) return;
        autoAttackCheckTimer = 0f;

        if (BattleManager.Instance != null)
        {
            var squads = BattleManager.Instance.GetAllSquads();
            Squad bestTarget = null;
            float bestScore = float.MinValue;

            Vector3 myPos = GetVisualCenter();
            Vector3 myFront = GetFrontLineCenter();
            Vector3 myFwd = transform.forward;

            foreach (var enemySquad in squads)
            {
                if (enemySquad == null || enemySquad == this || enemySquad.isPlayer == this.isPlayer || enemySquad.MemberCount <= 0) continue;

                Vector3 enemyPos = enemySquad.GetVisualCenter();
                Vector3 enemyFront = enemySquad.GetFrontLineCenter();

                float frontDist = Vector3.Distance(myFront, enemyFront);
                Vector3 toEnemy = enemyPos - myPos;
                toEnemy.y = 0f;
                float dist = toEnemy.magnitude;
                float dot = (dist > 0.001f) ? Vector3.Dot(myFwd, toEnemy / dist) : 1f;

                // 🎯 방진 정면 22m 이내 또는 중심 기준 30m 이내에 적 접근 시, 시선 정면(dot > 0.15f) 적 부대 우선 요격
                if (dot > 0.15f && (frontDist <= 22.0f || dist <= 30.0f))
                {
                    float score = (dot * 50f) - dist;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = enemySquad;
                    }
                }
            }

            if (bestTarget != null)
            {
                Debug.Log($"<color=#FF8C00><b>[Squad] 🛡️ 자동 요격 발동! ({name}) -> 적 부대({bestTarget.name}) 접근 감지! 전군 맞돌격 개시!</b></color>");
                CommandAttackSquad(bestTarget);
            }
        }
    }

    private void UpdateEnemySquadAI()
    {
        if (isPlayer || MemberCount <= 0) return;

        enemyAiCheckTimer += Time.deltaTime;
        if (enemyAiCheckTimer < 0.5f) return;
        enemyAiCheckTimer = 0f;

        Vector3 myPos = GetVisualCenter();

        // 🔒 [끈질긴 목표 고수 (Sticky Target Lock)]:
        // 현재 목표 부대가 유효하고 살아있으며, 아군이 도망쳐서 거리 80m 이상 완전히 멀어지지 않은 경우:
        // 타겟을 절대로 변경하지 않고 끝까지 공격 유지! (접근 도중 타겟이 바뀌며 옆 부대로 꺾이는 현상 100% 원천 차단)
        if (currentTargetSquad != null && currentTargetSquad.MemberCount > 0)
        {
            float distToCurrentTarget = Vector3.Distance(myPos, currentTargetSquad.GetVisualCenter());
            if (distToCurrentTarget <= 80.0f)
            {
                return;
            }
        }

        float minScore = float.MaxValue;
        Squad bestTargetSquad = null;

        if (BattleManager.Instance != null)
        {
            var squads = BattleManager.Instance.GetAllSquads();

            // 🎯 [전방 정면 1:1 완벽 전선 매칭 & 중복 타겟팅 쏠림 방지]:
            // 다른 적 부대가 이미 타겟으로 잡은 아군 부대는 페널티를 부여하여 좌/중/우 부대가 1:1로 분산 맞대결!
            for (int i = 0; i < squads.Count; i++)
            {
                Squad s = squads[i];
                if (s == null || s == this || !s.isPlayer || s.MemberCount <= 0) continue;

                Vector3 targetCenter = s.GetVisualCenter();
                Vector3 toTarget = targetCenter - myPos;
                toTarget.y = 0f;

                float lateralOffset = Mathf.Abs(Vector3.Dot(toTarget, transform.right)); // 좌우 벌어짐

                // 다른 적 부대가 이미 이 아군 부대를 타겟팅하고 있는지 카운트
                int alreadyTargetedCount = 0;
                for (int j = 0; j < squads.Count; j++)
                {
                    Squad otherEnemy = squads[j];
                    if (otherEnemy != null && otherEnemy != this && !otherEnemy.isPlayer && otherEnemy.currentTargetSquad == s)
                    {
                        alreadyTargetedCount++;
                    }
                }

                // 1:1 전선 매칭 점수 (중복 타겟팅된 부대는 3000점 페널티로 피해가고, 비어있는 정면 상대를 선택)
                float score = toTarget.sqrMagnitude + (lateralOffset * lateralOffset * 4.0f) + (alreadyTargetedCount * 3000f);
                if (score < minScore)
                {
                    minScore = score;
                    bestTargetSquad = s;
                }
            }
        }

        if (bestTargetSquad != null)
        {
            // ⚔️ 1순위: 아군 부대를 향해 일제 돌격 명령
            if (currentTargetSquad != bestTargetSquad || currentCommandState != UnitCommandState.AttackMove)
            {
                CommandAttackSquad(bestTargetSquad);
            }
        }
        else
        {
            // 🦅 2순위 (아군 부대 전멸/부재 시): 살아있는 아군 자유 유닛 무리를 포위/추적하여 토벌
            Vector3 bestFreeUnitPos = Vector3.zero;
            float minFreeUnitSqrDist = float.MaxValue;
            bool foundFreeUnit = false;

            // [A] 순수 ECS 자유 유닛 (Faction == 1 && (SquadId == -1 || IsFreeUnit == 1))
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                var query = em.CreateEntityQuery(
                    typeof(MiniTotalWar.ECS.UnitEntityTag),
                    typeof(MiniTotalWar.ECS.UnitMovementData)
                );

                using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
                using (var movs = query.ToComponentDataArray<MiniTotalWar.ECS.UnitMovementData>(Unity.Collections.Allocator.Temp))
                {
                    for (int i = 0; i < tags.Length; i++)
                    {
                        if (tags[i].Faction == 1 && tags[i].IsAlive == 1 && (tags[i].SquadId == -1 || tags[i].IsFreeUnit == 1))
                        {
                            Vector3 pos = (Vector3)movs[i].Position;
                            float dSqr = (pos - myPos).sqrMagnitude;
                            if (dSqr < minFreeUnitSqrDist)
                            {
                                minFreeUnitSqrDist = dSqr;
                                bestFreeUnitPos = pos;
                                foundFreeUnit = true;
                            }
                        }
                    }
                }
            }

            // [B] GameObject 자유 유닛
            if (!foundFreeUnit)
            {
                Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
                foreach (var u in allUnits)
                {
                    if (u != null && u.isPlayer && u.mySquad == null && u.currentHp > 0)
                    {
                        float dSqr = (u.transform.position - myPos).sqrMagnitude;
                        if (dSqr < minFreeUnitSqrDist)
                        {
                            minFreeUnitSqrDist = dSqr;
                            bestFreeUnitPos = u.transform.position;
                            foundFreeUnit = true;
                        }
                    }
                }
            }

            if (foundFreeUnit)
            {
                Vector3 toFreeUnit = bestFreeUnitPos - myPos;
                toFreeUnit.y = 0f;
                Quaternion faceRot = (toFreeUnit.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(toFreeUnit.normalized) : transform.rotation;

                // 목표와 3m 이상 차이날 때 자유 유닛 무리로 대형 유지 진격
                if (currentCommandState != UnitCommandState.AttackMove || Vector3.Distance(moveDestination, bestFreeUnitPos) > 3.0f)
                {
                    CommandMoveWithFormation(bestFreeUnitPos, faceRot, currentColumns, forceSort: false, cmdState: UnitCommandState.AttackMove);
                }
            }
        }
    }

    public void UpdateSquadCenter()
    {
        members.RemoveAll(m => m == null);
        if (members.Count == 0)
        {
            if (initialUnitCount > 0)
            {
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    var em = world.EntityManager;
                    var query = em.CreateEntityQuery(
                        typeof(MiniTotalWar.ECS.UnitEntityTag),
                        typeof(MiniTotalWar.ECS.UnitMovementData)
                    );

                    using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                    {
                        int squadId = GetInstanceID();
                        Vector3 sum = Vector3.zero;
                        int count = 0;

                        for (int i = 0; i < entities.Length; i++)
                        {
                            var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(entities[i]);
                            if (tag.SquadId == squadId && tag.IsAlive == 1)
                            {
                                var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(entities[i]);
                                sum += (Vector3)mov.Position;
                                count++;
                            }
                        }

                        currentAliveCount = count;

                        if (count > 0)
                        {
                            ecsVisualCenter = sum / count;
                            hasEcsVisualCenter = true;
                            transform.position = ecsVisualCenter;

                            if (isMoving && Vector3.Distance(ecsVisualCenter, moveDestination) <= 0.25f)
                            {
                                isMoving = false;
                                AutoAcquireFrontEnemyTarget();
                            }
                        }
                        else
                        {
                            // 💀 부대 전멸: 부대 오브젝트 파괴 및 UI 자동 해제
                            if (BattleManager.Instance != null)
                            {
                                BattleManager.Instance.UnregisterSquad(this);
                            }
                            DestroySquadObject();
                            return;
                        }
                    }
                }

                // 목표 적 부대가 전멸했거나 파괴되었는지 검사
                if (currentTargetSquad != null && currentTargetSquad.MemberCount == 0)
                {
                    currentTargetSquad = null;
                    hasActiveEnemyTarget = false;
                    currentCommandState = UnitCommandState.Idle;
                    CommandMoveWithFormation(transform.position, transform.rotation, currentColumns, forceSort: false, cmdState: UnitCommandState.Idle);
                }

                return;
            }

            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.UnregisterSquad(this);
            }
            DestroySquadObject();
            return;
        }

        int engagedCount = 0;
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != null && members[i].currentState == UnitCommandState.MeleeEngaged)
            {
                engagedCount++;
            }
        }

        // 실시간 포위 위협 감지 (적의 포위 침투도에 따라 서서히 원형 방진으로 변형)
        if (currentTargetSquad != null)
        {
            DetectEnvelopmentThreat(currentTargetSquad);
        }
        else
        {
            currentEncirclementFactor = Mathf.MoveTowards(currentEncirclementFactor, 0f, 2.0f * Time.deltaTime);
        }

        int enemyMemberCount = (currentTargetSquad != null) ? currentTargetSquad.MemberCount : 0;
        bool hasActiveCombat = (engagedCount > 0) || (hasActiveEnemyTarget && currentTargetSquad != null && enemyMemberCount > 0);

        if (hasActiveCombat && currentTargetSquad != null && enemyMemberCount > 0)
        {
            int myCount = MemberCount;
            int totalRows = Mathf.CeilToInt((float)myCount / Mathf.Max(1, currentColumns));
            float curSpacingZ = (currentFormationType == SquadFormationType.Loose && !isLooseFormation) ? (DEFAULT_LOOSE_SPACING_Z * customSpacingMultiplier) : (spacingZ * customSpacingMultiplier);
            float baseFrontZ = (totalRows - 1) * 0.5f * curSpacingZ;

            // ⚔️ 1열이 접적면(contactSurfaceCenter)에 완벽 밀착하도록 부대 기준점 동기화
            Vector3 pushTarget = contactSurfaceCenter - (targetSquadRotation * Vector3.forward) * (baseFrontZ + 0.1f);
            transform.position = Vector3.MoveTowards(transform.position, pushTarget, 2.0f * Time.deltaTime);
            moveDestination = transform.position;

            transform.rotation = targetSquadRotation;

            Vector3 basePos = transform.position;
            Quaternion baseRot = transform.rotation;

            bool isAttackerEnveloping = hasActiveEnemyTarget && (currentColumns * spacingX >= currentTargetSquad.currentColumns * currentTargetSquad.spacingX * 0.9f);

            if (isAttackerEnveloping)
            {
                // [A. 공격/포위자]: 매끄러운 동심원 초승달 포위 슬롯 동기화
                for (int i = 0; i < members.Count; i++)
                {
                    Unit u = members[i];
                    if (u == null) continue;

                    int col = Mathf.Clamp(u.Col, 0, currentColumns - 1);
                    int row = Mathf.Max(0, u.Row);

                    if (CalculateEnvelopmentSlot(row, (float)col, currentColumns, totalGridRows, currentFormationType, out Vector3 envelopSlot, out Vector3 colNormalDir))
                    {
                        Vector3 worldSlotPos = basePos + (baseRot * envelopSlot);
                        Quaternion slotRot = baseRot * Quaternion.LookRotation(colNormalDir);
                        u.UpdateTargetPosition(worldSlotPos, slotRot);
                    }
                    else
                    {
                        Vector3 fallbackSlot = GetSlotLocalOffset(row, col, currentColumns, totalGridRows, currentFormationType);
                        Vector3 worldSlotPos = basePos + (baseRot * fallbackSlot);
                        u.UpdateTargetPosition(worldSlotPos, baseRot);
                    }
                }
                return;
            }
            else
            {
                // [B. 피포위/수비자]: 인위적인 원형 변형 없이 정통 사각 방진(Solid Shieldwall) 유지하며 자연스럽게 교전
                for (int i = 0; i < members.Count; i++)
                {
                    Unit u = members[i];
                    if (u == null) continue;

                    int col = Mathf.Clamp(u.Col, 0, currentColumns - 1);
                    int row = Mathf.Max(0, u.Row);

                    Vector3 normalSlot = GetSlotLocalOffset(row, col, currentColumns, totalGridRows, currentFormationType);
                    Vector3 worldSlotPos = basePos + (baseRot * normalSlot);
                    u.UpdateTargetPosition(worldSlotPos, baseRot);
                }
                return;
            }
        }

        // 부대가 이동 중이 아닐 때는 고정 대형 프레임(moveDestination)에 앵커를 완벽 고정!
        if (!isMoving)
        {
            transform.position = moveDestination;
            transform.rotation = targetSquadRotation;
            return;
        }

        // 이동 중일 때도 좌우 사망자 불균형으로 중심점이 옆으로 덜컹거리지 않도록 moveDestination 궤적 축을 부드럽게 추종
        transform.position = Vector3.MoveTowards(transform.position, moveDestination, 2.5f * Time.deltaTime);
    }

    public bool IsSquadFullyArrived
    {
        get
        {
            if (!isMoving) return true;
            if (moveStartGraceTimer < 0.35f) return false;

            if (members.Count > 0)
            {
                int arrivedCount = 0;
                for (int i = 0; i < members.Count; i++)
                {
                    var member = members[i];
                    if (member == null) continue;

                    NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        float dist = Vector3.Distance(member.transform.position, agent.destination);
                        if (!agent.pathPending && dist <= 0.25f)
                        {
                            arrivedCount++;
                        }
                    }
                    else
                    {
                        float dist = Vector3.Distance(member.transform.position, member.fixedTargetPos);
                        if (dist <= 0.25f) arrivedCount++;
                    }
                }

                return arrivedCount >= Mathf.CeilToInt(members.Count * 0.95f);
            }

            // ⚡ 순수 ECS 모드: 엔티티들의 95% 이상이 목표 슬롯에 도달했는지 검사!
            if (initialUnitCount > 0)
            {
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    var em = world.EntityManager;
                    var query = em.CreateEntityQuery(
                        typeof(MiniTotalWar.ECS.UnitEntityTag),
                        typeof(MiniTotalWar.ECS.UnitMovementData)
                    );

                    using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
                    using (var movs = query.ToComponentDataArray<MiniTotalWar.ECS.UnitMovementData>(Unity.Collections.Allocator.Temp))
                    {
                        int squadId = GetInstanceID();
                        int totalAlive = 0;
                        int arrived = 0;

                        for (int i = 0; i < tags.Length; i++)
                        {
                            if (tags[i].SquadId == squadId && tags[i].IsAlive == 1)
                            {
                                totalAlive++;
                                float distSqr = Unity.Mathematics.math.distancesq(movs[i].Position, movs[i].TargetPosition);
                                if (distSqr <= 0.35f * 0.35f)
                                {
                                    arrived++;
                                }
                            }
                        }

                        if (totalAlive > 0)
                        {
                            return arrived >= Mathf.CeilToInt(totalAlive * 0.95f);
                        }
                    }
                }
            }

            return true;
        }
    }

    private void CheckAndExecuteArrival()
    {
        if (!isMoving) return;

        if (IsSquadFullyArrived)
        {
            if (waypointQueue.Count > 0)
            {
                WaypointData nextWp = waypointQueue[0];
                waypointQueue.RemoveAt(0);
                CommandMoveWithFormation(nextWp.destination, nextWp.rotation, nextWp.columns, forceSort: false);
                return;
            }

            if (!hasAlignedThisArrival)
            {
                hasAlignedThisArrival = true;
                isMoving = false;
                transform.position = moveDestination;
                transform.rotation = targetSquadRotation;
                AutoAcquireFrontEnemyTarget();
            }
        }
    }

    /// <summary>
    /// 부대가 이동을 마쳤거나 대기 중일 때, 시선 정면의 최근접 적 부대를 자동으로 락온하여
    /// TargetSquadId == -1 공백 상태에서 발생하는 타부대 쏠림 및 레이더 누수를 100% 방지합니다.
    /// </summary>
    public void AutoAcquireFrontEnemyTarget()
    {
        if (currentTargetSquad != null && currentTargetSquad.MemberCount > 0) return;
        if (BattleManager.Instance == null) return;

        Squad bestEnemy = null;
        float bestScore = float.MinValue;
        Vector3 myPos = GetVisualCenter();
        Vector3 myFwd = transform.forward;

        var allSquads = BattleManager.Instance.GetAllSquads();
        for (int i = 0; i < allSquads.Count; i++)
        {
            Squad enemy = allSquads[i];
            if (enemy == null || enemy == this || enemy.isPlayer == this.isPlayer || enemy.MemberCount <= 0) continue;

            Vector3 toEnemy = enemy.GetVisualCenter() - myPos;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist > 80f) continue;

            float dot = (dist > 0.001f) ? Vector3.Dot(myFwd, toEnemy / dist) : 1f;
            // 시선 정면(dot > 0.2f)에 위치하는 적 부대 우선
            if (dot > 0.2f)
            {
                float score = (dot * 60f) - dist;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestEnemy = enemy;
                }
            }
        }

        if (bestEnemy != null)
        {
            currentTargetSquad = bestEnemy;
            SyncTargetSquadIdToSimulations(bestEnemy.GetInstanceID());
        }
    }

    public void OnUnitDied(Unit deadUnit)
    {
        if (deadUnit == null || !members.Contains(deadUnit)) return;

        int deadRow = deadUnit.Row;
        int deadCol = deadUnit.Col;

        members.Remove(deadUnit);
        unitTargetPositions.Remove(deadUnit);

        Destroy(deadUnit.gameObject);

        if (members.Count == 0)
        {
            DestroySquadObject();
            return;
        }

        // 🛡️ [정통 토탈워 종대 불변의 법칙 (Zero Horizontal Shift)]:
        // 전사자 발생 시 횡방향 이동(열 바꿈)을 100% 금지하고,
        // 오직 동일한 열(deadCol)의 바로 뒷 행(Row+1, Row+2...) 유닛만 앞으로 1보 전진 보충!
        int targetCol = deadCol;
        int currentCheckRow = deadRow;

        while (true)
        {
            Unit backUnit = members.Find(u => u != null && u.Row == currentCheckRow + 1 && u.Col == targetCol);
            if (backUnit != null)
            {
                backUnit.SetGridPosition(currentCheckRow, targetCol);
                currentCheckRow++;
            }
            else
            {
                break;
            }
        }

        int oldTotalRows = totalGridRows;

        // 2. 새로운 총 행 수 갱신
        totalGridRows = Mathf.CeilToInt((float)members.Count / currentColumns);

        // 전선 앵커 절대 고정 (Frontline Anchor Lock):
        // 행 수가 줄어들 때 방진 중심점 수축으로 인해 1열 전선이 뒤로 물러서는 현상을 100% 원천 차단
        if (totalGridRows < oldTotalRows)
        {
            float currentSpacing = (currentFormationType == SquadFormationType.Loose) ? (spacing * 2.0f) : spacing;
            float shiftForward = (oldTotalRows - totalGridRows) * 0.5f * currentSpacing;
            transform.position += transform.rotation * new Vector3(0f, 0f, shiftForward);
            moveDestination = transform.position;
        }

        // 4. 살아있는 전원의 슬롯 좌표를 새로운 행/열 번호에 맞춰 일괄 동기화 (단차 및 삐져나옴 100% 제거)
        Vector3 basePos = transform.position;
        Quaternion baseRot = transform.rotation;

        for (int i = 0; i < members.Count; i++)
        {
            Unit u = members[i];
            if (u == null) continue;

            Vector3 newLocalOffset = GetSlotLocalOffset(u.Row, u.Col, currentColumns, totalGridRows, currentFormationType);
            Vector3 newTargetPos = basePos + (baseRot * newLocalOffset);
            u.UpdateTargetPosition(newTargetPos, baseRot);
        }
    }

    public float targetEnemyWidth = -1f;
    public float customCurvature = 0f;

    public float minEnemyProjectionX = 0f;
    public float maxEnemyProjectionX = 0f;
    public float minEnemyProjectionZ = 0f;
    public float maxEnemyProjectionZ = 0f;
    public float enemyCenterOffsetX = 0f;
    public Vector3 contactSurfaceCenter = Vector3.zero;
    public float enemyDepth = 1.0f;
    public bool hasActiveEnemyTarget = false;

    public void AdjustSpacing(float delta)
    {
        customSpacingMultiplier = Mathf.Clamp(customSpacingMultiplier + delta, 0.45f, 3.0f);
        spacingX = (isLooseFormation ? DEFAULT_LOOSE_SPACING_X : DEFAULT_SPACING_X) * customSpacingMultiplier;
        spacingZ = (isLooseFormation ? DEFAULT_LOOSE_SPACING_Z : DEFAULT_SPACING_Z) * customSpacingMultiplier;
        spacing = (spacingX + spacingZ) * 0.5f;

        // 즉시 현재 대형에 반영
        if (!isMoving && (members.Count > 0 || initialUnitCount > 0))
        {
            CommandMoveWithFormation(transform.position, transform.rotation, currentColumns, forceSort: false);
        }
    }

    public bool CalculateEnvelopmentSlot(int row, float col, int columns, int totalRows, SquadFormationType formType, out Vector3 localPos, out Vector3 colNormalDir)
    {
        localPos = Vector3.zero;
        colNormalDir = Vector3.forward;

        if (!hasActiveEnemyTarget || currentTargetSquad == null || targetEnemyWidth <= 0f)
        {
            return false;
        }

        float curSpacingX = (formType == SquadFormationType.Loose && !isLooseFormation) ? (DEFAULT_LOOSE_SPACING_X * customSpacingMultiplier) : (spacingX * customSpacingMultiplier);
        float curSpacingZ = (formType == SquadFormationType.Loose && !isLooseFormation) ? (DEFAULT_LOOSE_SPACING_Z * customSpacingMultiplier) : (spacingZ * customSpacingMultiplier);

        if (totalRows <= 0)
        {
            int memberCount = (members != null && members.Count > 0) ? members.Count : 1;
            totalRows = Mathf.CeilToInt((float)memberCount / Mathf.Max(1, columns));
        }

        float baseFrontZ = (totalRows - 1) * 0.5f * curSpacingZ;

        float enemyCenterX = enemyCenterOffsetX;
        float enemyHalfWidth = Mathf.Max(0.2f, targetEnemyWidth * 0.5f);
        float currentEnemyDepth = Mathf.Max(0.4f, enemyDepth);

        // ────────────────────────────────────────────────────────────
        // 🏛️ [원래의 균일 격자 간격(1.1m x 1.2m) 포위 전진 공식]
        //
        // 1. 인위적인 반달/베지어 왜곡 없이, 원래의 깔끔한 열 간격(1.1m)을 그대로 유지.
        // 2. 정면 열: 적 정면에 배치.
        // 3. 날개 열: 자신의 원래 열 레인을 따라 앞으로 전진하여 적 옆구리를 감쌈.
        // 4. 행 간격(1.2m) 균일 유지로 후방 붕 뜸 및 겹침 0% 보장.
        // ────────────────────────────────────────────────────────────

        float midCol = (columns - 1) * 0.5f;
        float deltaCol = col - midCol;
        float absDeltaCol = Mathf.Abs(deltaCol);
        float side = (deltaCol >= 0f) ? 1f : -1f;

        float frontColsHalf = enemyHalfWidth / Mathf.Max(0.1f, curSpacingX);

        float frontZ = baseFrontZ;
        float localX;

        // 📏 아군 부대 폭과 적군 부대 폭의 비율에 따른 동적 포위 곡률(Dynamic Curvature) 계산
        float mySquadWidth = columns * curSpacingX;
        float widthRatio = mySquadWidth / Mathf.Max(1.0f, targetEnemyWidth);
        float widthAdvantage = Mathf.Clamp01((widthRatio - 1.0f) / 2.0f);

        // 🏃 돌격 진행도(envelopmentMorph): 돌격하며 앞으로 나아갈수록 곡률이 서서히 발현 (0.0 -> 1.0)
        float morph = envelopmentMorph;

        // 🏹 Alt+좌클릭 곡선 배치와 동일한 정규화 열 좌표 (u: -1.0 ~ +1.0)
        float u = (midCol > 0.001f) ? (deltaCol / midCol) : 0f;
        float uSqr = u * u; // 2차 포물선 곡선

        if (absDeltaCol <= frontColsHalf)
        {
            // [A. 짧은 쪽(적 정면 접적선)]: 곡률 왜곡 없이 단단한 직선 방패벽 유지
            localX = enemyCenterX + (deltaCol * curSpacingX);
            frontZ = baseFrontZ;
        }
        else
        {
            // [B. 긴 쪽(외곽 날개)]: 50대 10 등 큰 전열 차이에서도 잉여 열 수에 비례하여 거대하게 꺾임
            float surplusCols = midCol - frontColsHalf; // 잉여 열 수 (50대 10일 때 약 20열)
            float maxWingAdvance = currentEnemyDepth + (surplusCols * curSpacingX * 0.70f); // 잉여 폭에 비례하여 전진
            frontZ = baseFrontZ + (uSqr * maxWingAdvance * morph);

            // 잉여 폭에 비례하여 적 측후방을 완벽히 감싸 안는 안쪽 오므라듦 (Inward Curl)
            float maxCurlInward = surplusCols * curSpacingX * 0.55f;
            float curlInward = uSqr * maxCurlInward * morph;
            float targetX = (deltaCol * curSpacingX) - (side * curlInward);
            float minSafeX = side * (enemyHalfWidth + 0.70f);

            if (side > 0f)
                localX = enemyCenterX + Mathf.Max(minSafeX, targetX);
            else
                localX = enemyCenterX + Mathf.Min(minSafeX, targetX);
        }

        // 🛡️ [옆 부대 전선 침범 100% 원천 차단]:
        // 양 날개(외곽) 병사들이 인접한 옆 부대의 영역으로 튀어나가지 않도록 부대 본래의 횡 폭 내로 엄격히 클램핑!
        float maxAllowedHalfWidth = (columns - 1) * 0.5f * curSpacingX;
        localX = Mathf.Clamp(localX, -maxAllowedHalfWidth, maxAllowedHalfWidth);

        // 정면 전열보다 앞으로 지나치게 튀어나가지 않도록 날개 전진 폭도 최대 0.5m 이내로 제한
        frontZ = Mathf.Min(frontZ, baseFrontZ + 0.5f);

        // 후방 2~6열: 1열 등 뒤로 1.2m씩 정연하게 적층 (체적 100% 보존)
        float localZ = frontZ - (row * curSpacingZ);

        localPos = new Vector3(localX, 0f, localZ);
        colNormalDir = Vector3.forward;
        return true;
    }

    public Vector3 GetSlotLocalOffset(int row, int col, int columns, int totalRows, SquadFormationType formType)
    {
        return CalculateCurvedSlotOffset(row, (float)col, columns, totalRows, formType);
    }

    public Vector3 CalculateCurvedSlotOffset(int row, float col, int columns, int totalRows, SquadFormationType formType)
    {
        float curSpacingX = (formType == SquadFormationType.Loose || isLooseFormation) ? (spacingX * 1.5f) : spacingX;
        float curSpacingZ = (formType == SquadFormationType.Loose || isLooseFormation) ? (spacingZ * 1.5f) : spacingZ;
        
        if (totalRows <= 0)
        {
            int memberCount = (members != null && members.Count > 0) ? members.Count : 1;
            totalRows = Mathf.CeilToInt((float)memberCount / Mathf.Max(1, columns));
        }

        // 🌟 [공격/포위 시 부대 분할 없이 하나의 일체형 대형으로 완전 통합]:
        // 단, 평행 횡대(Line) 진형에서는 양 날개가 벌어지며 옆 부대 전선을 침범하지 않도록,
        // 인위적 포위 왜곡을 배제하고 단단한 직사각형 방패벽 슬롯을 엄격히 유지합니다.
        if (hasActiveEnemyTarget && currentTargetSquad != null && targetEnemyWidth > 0f && formType != SquadFormationType.Line)
        {
            if (CalculateEnvelopmentSlot(row, col, columns, totalRows, formType, out Vector3 envelopSlot, out _))
            {
                return envelopSlot;
            }
        }

        if (formType == SquadFormationType.Diamond || formType == SquadFormationType.Wedge || formType == SquadFormationType.Square || formType == SquadFormationType.Circle)
        {
            // 🌟 마름모, 쐐기진, 사각방진, 원형진은 GenerateFormationSlots의 전용 대칭 슬롯을 직접 참조
            int count = (members != null && members.Count > 0) ? members.Count : (columns * totalRows);
            List<SlotInfo> formSlots = GenerateFormationSlots(count, columns, out _, formType);
            for (int s = 0; s < formSlots.Count; s++)
            {
                if (formSlots[s].row == row && formSlots[s].col == Mathf.RoundToInt(col))
                {
                    return formSlots[s].localOffset;
                }
            }
            int fallbackIdx = Mathf.Clamp(Mathf.RoundToInt(row * columns + col), 0, formSlots.Count - 1);
            if (formSlots.Count > 0) return formSlots[fallbackIdx].localOffset;
        }

        // 🏛️ [평상시 기본 사각 방진 및 Alt+좌클릭 수동 곡선 배치 좌표]
        float baseColOffset = (col - (columns - 1) * 0.5f) * curSpacingX;
        float baseFrontZ = (totalRows - 1) * 0.5f * curSpacingZ;
        float rowDepth = row * curSpacingZ;

        // 🏹 [Alt+좌클릭 수동 곡선 배치 곡률 반영]:
        float curveZ = 0f;
        if (Mathf.Abs(customCurvature) > 0.001f)
        {
            float midCol = (columns - 1) * 0.5f;
            float u = (midCol > 0.001f) ? ((col - midCol) / midCol) : 0f;
            curveZ = (1.0f - (u * u)) * customCurvature;
        }

        return new Vector3(baseColOffset, 0f, baseFrontZ - rowDepth + curveZ);
    }

    /// <summary>
    /// 대형 프리뷰 도트 좌표 및 회전 목록을 반환합니다 (사각방진 4방향 및 원형진 방사형 시선각 지원).
    /// </summary>
    public List<(Vector3 position, Quaternion rotation)> GetPreviewSlotTransforms(Vector3 destination, Quaternion rotation, int columns)
    {
        List<(Vector3, Quaternion)> list = new List<(Vector3, Quaternion)>();
        int count = (members != null && members.Count > 0) ? members.Count : ((currentAliveCount > 0) ? currentAliveCount : initialUnitCount);
        if (count <= 0) return list;

        int cols = Mathf.Clamp(columns, 1, count);
        List<SlotInfo> slots = GenerateFormationSlots(count, cols, out int totalRows, currentFormationType);
        foreach (var slot in slots)
        {
            Vector3 pos = destination + (rotation * slot.localOffset);
            Quaternion rot = rotation * slot.localRotation;
            list.Add((pos, rot));
        }
        return list;
    }

    /// <summary>
    /// 곡선 배치 프리뷰 도트 좌표 목록을 계산하여 반환합니다 (모든 진형 곡선 지원).
    /// </summary>
    public List<Vector3> GetCurvedPreviewSlotPositions(Vector3 destination, Quaternion rotation, int columns, float curvatureHeight)
    {
        List<Vector3> positions = new List<Vector3>();
        int count = (members != null && members.Count > 0) ? members.Count : ((currentAliveCount > 0) ? currentAliveCount : initialUnitCount);
        if (count <= 0) return positions;

        int cols = Mathf.Clamp(columns, 1, count);
        float oldCurv = customCurvature;
        customCurvature = curvatureHeight;

        List<SlotInfo> slots = GenerateFormationSlots(count, cols, out int totalRows, currentFormationType);
        foreach (var slot in slots)
        {
            positions.Add(destination + (rotation * slot.localOffset));
        }

        customCurvature = oldCurv;
        return positions;
    }

    /// <summary>
    /// 곡선 대형으로 부대를 이동 및 재배치합니다.
    /// </summary>
    public void CommandCurvedFormation(float curvatureHeight, int newColumns = -1)
    {
        customCurvature = curvatureHeight;
        int targetCols = (newColumns > 0) ? newColumns : currentColumns;

        CommandMoveWithFormation(transform.position, transform.rotation, targetCols, forceSort: false);
    }

    /// <summary>
    /// 지정된 위치와 회전각으로 곡선 대형을 형성하며 이동합니다.
    /// </summary>
    public void CommandCurvedFormationAt(Vector3 destination, Quaternion rotation, int newColumns, float curvatureHeight)
    {
        customCurvature = curvatureHeight;
        int targetCols = (newColumns > 0) ? newColumns : currentColumns;

        CommandMoveWithFormation(destination, rotation, targetCols, forceSort: false);
    }

    public Vector3 GetFrontLineCenter()
    {
        if (members != null && members.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null && members[i].Row == 0)
                {
                    sum += members[i].transform.position;
                    count++;
                }
            }
            if (count > 0) return sum / count;
            return transform.position;
        }

        // ⚡ 순수 ECS 모드 정면 1열 중심 좌표
        int rows = totalGridRows > 0 ? totalGridRows : Mathf.CeilToInt((float)MemberCount / Mathf.Max(1, currentColumns));
        float spZ = spacingZ > 0f ? spacingZ : Squad.DEFAULT_SPACING_Z;
        float frontOffsetZ = (rows - 1) * 0.5f * spZ;
        return transform.position + (transform.rotation * new Vector3(0f, 0f, frontOffsetZ));
    }

    /// <summary>
    /// 살아있는 부대원 전체의 기하학적 무게중심(정중앙) 좌표를 반환합니다.
    /// 부대 머리 위 UI 아이콘 위치를 1열이 아닌 전체 방진 중앙에 정확히 배치하기 위해 사용됩니다.
    /// </summary>
    public Vector3 GetVisualCenter()
    {
        if (members != null && members.Count > 0)
        {
            if (!isMoving && currentCommandState != UnitCommandState.MeleeEngaged)
            {
                return transform.position;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null)
                {
                    sum += members[i].transform.position;
                    count++;
                }
            }
            if (count > 0) return sum / count;
            return transform.position;
        }

        if (hasEcsVisualCenter)
        {
            return ecsVisualCenter;
        }

        return transform.position;
    }

    public void AddWaypoint(Vector3 destination, Quaternion rotation, int columns = -1)
    {
        int cols = (columns > 0) ? columns : currentColumns;
        WaypointData wp = new WaypointData { destination = destination, rotation = rotation, columns = cols };

        if (!isMoving && waypointQueue.Count == 0)
        {
            CommandMoveWithFormation(destination, rotation, cols, forceSort: false);
        }
        else
        {
            waypointQueue.Add(wp);
        }
    }

    public void AddWaypoint(Vector3 destination)
    {
        AddWaypoint(destination, transform.rotation, currentColumns);
    }

    public void ClearWaypoints()
    {
        waypointQueue.Clear();
    }

    public void CommandRotateInPlace(Quaternion targetRotation)
    {
        CommandMoveWithFormation(transform.position, targetRotation, currentColumns, forceSort: false);
    }

    public void DetachUnit(Unit unit)
    {
        if (unit == null || !members.Contains(unit)) return;

        members.Remove(unit);
        unitTargetPositions.Remove(unit);

        unit.mySquad = null;
        unit.SetGridPosition(-1, -1);

        if (members.Count == 0) DestroySquadObject();
        else RebuildGridStructure(currentColumns, forceSpatialSort: true);
    }

    public void DisbandSquad()
    {
        int mySquadId = GetInstanceID();

        // 1. GameObject 모드 부대원 해체
        List<Unit> currentMembers = new List<Unit>(members);
        foreach (Unit unit in currentMembers)
        {
            if (unit != null)
            {
                unit.mySquad = null;
                unit.SetGridPosition(-1, -1);
            }
        }
        members.Clear();

        // 2. 순수 ECS 모드 엔티티 소속 해제 (자유 유닛으로 승격)
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(MiniTotalWar.ECS.UnitEntityTag));
            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(entities[i]);
                    if (tag.SquadId == mySquadId)
                    {
                        tag.SquadId = -1;
                        tag.IsFreeUnit = 1;
                        em.SetComponentData(entities[i], tag);
                    }
                }
            }
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.UnregisterSquad(this);
        }

        DestroySquadObject();
    }

    private void DestroySquadObject()
    {
        Destroy(gameObject);
    }

    public void SelectAllMembers(bool select)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] != null) members[i].SetSelected(select);
        }
    }

    public List<Vector3> GetPreviewSlotPositions(Vector3 destination, Quaternion rotation, int columns)
    {
        if (Mathf.Abs(customCurvature) > 0.001f)
        {
            return GetCurvedPreviewSlotPositions(destination, rotation, columns, customCurvature);
        }

        List<Vector3> previewSlots = new List<Vector3>();
        int memberCount = (members != null && members.Count > 0) ? members.Count : ((currentAliveCount > 0) ? currentAliveCount : initialUnitCount);
        if (memberCount <= 0) return previewSlots;

        int cols = Mathf.Clamp(columns, 1, memberCount);
        List<SlotInfo> slots = GenerateFormationSlots(memberCount, cols, out int totalRows, currentFormationType);

        foreach (var slot in slots)
        {
            Vector3 worldPos = destination + (rotation * slot.localOffset);
            previewSlots.Add(worldPos);
        }

        return previewSlots;
    }

    private void OnDrawGizmosSelected()
    {
        if (hasActiveEnemyTarget && currentTargetSquad != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 basePos = transform.position;
            Quaternion baseRot = transform.rotation;

            for (int i = 0; i < members.Count; i++)
            {
                Unit u = members[i];
                if (u != null)
                {
                    Vector3 localOffset = GetSlotLocalOffset(u.Row, u.Col, currentColumns, totalGridRows, currentFormationType);
                    Vector3 worldPos = basePos + (baseRot * localOffset);
                    Gizmos.DrawWireSphere(worldPos, 0.25f);
                }
            }
        }
    }
}
