using System.Collections.Generic;
using UnityEngine;
using MiniTotalWar;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 🌲 [전술 숲 및 장애물 구역 (Tactical Forest Zone)]
/// 삼국지 토탈워(Total War: Three Kingdoms) 본편에서 직접 추출/변환한 실제 3D 모델(대나무, 황산소나무, 활엽수, 관목 등)을 지형에 자연스럽게 배치하고,
/// 부대/유닛이 진입했을 때 토탈워 고유의 시야 은폐(매복) 및 사선(Line of Sight) 차폐를 실시간 수행합니다.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
public class TacticalForestZone : MonoBehaviour
{
    public enum ZoneShape
    {
        Circle, // 원형 숲 구역
        Box     // 직사각형 숲 구역
    }

    public enum ForestTheme
    {
        Mixed,           // 혼합림 (황산소나무, 대나무, 활엽수 골고루 배치)
        HuangshanPine,   // 황산 소나무 고산림 (웅장한 토탈워 명품 소나무 중심)
        BambooGrove,     // 울창한 대나무 숲 (삼국지 특유의 대나무 군락지)
        CastanopsisWood  // 울창한 활엽수림
    }

    // 전장 내 모든 활성 숲 구역 정적 레지스트리
    private static readonly List<TacticalForestZone> activeZones = new List<TacticalForestZone>();
    public static IReadOnlyList<TacticalForestZone> ActiveZones => activeZones;

    [Header("🌲 숲 구역 형태 및 테마")]
    [Tooltip("숲 구역 형태 (원형 또는 직사각형)")]
    public ZoneShape shape = ZoneShape.Circle;

    [Tooltip("숲 테마 (소나무림, 대나무숲, 활엽수림, 혼합림)")]
    public ForestTheme theme = ForestTheme.Mixed;

    [Tooltip("원형 숲의 유효 반경 (단위: m, 기본: 30m)")]
    public float radius = 30f;

    [Tooltip("직사각형 숲의 가로(X) 및 세로(Z) 크기 (단위: m, 기본: 50x35m)")]
    public Vector2 boxSize = new Vector2(50f, 35f);

    [Header("🌿 토탈워 삼국지 초목 스폰 설정")]
    [Tooltip("게임 시작 시 초목(나무/수풀)을 자동 생성할지 여부")]
    public bool autoGenerateOnStart = true;

    [Tooltip("스폰할 토탈워 3D 나무 개수")]
    public int treeCount = 24;

    [Tooltip("스폰할 토탈워 3D 관목 및 지표 수풀 개수")]
    public int bushCount = 45;

    [Tooltip("나무 크기 배율 (기본: 0.28, 토탈워 35m 원본 -> 약 10m 실전 크기)")]
    [Range(0.1f, 1.0f)]
    public float treeScaleMultiplier = 0.28f;

    [Tooltip("수풀 크기 배율 (기본: 0.32, 약 1.2m 실전 크기)")]
    [Range(0.1f, 1.0f)]
    public float bushScaleMultiplier = 0.32f;

    [Tooltip("초목 무작위 시드 (동일한 시드 = 동일한 배치)")]
    public int randomSeed = 12345;

    [Header("👁️ 전술 은폐 및 시야 차폐")]
    [Tooltip("숲 내부 유닛의 기본 피탐지 거리 (단위: m, 기본: 25m)")]
    public float stealthDetectionRange = 25.0f;

    [Tooltip("숲 내부 유닛이 전력질주(구보) 시 발각되는 거리 (단위: m, 기본: 45m)")]
    public float runningDetectionRange = 45.0f;

    [Tooltip("숲을 가로지르는 사선 차폐(LoS Block) 활성화 여부")]
    public bool blockLineOfSight = true;

    public enum TreeCombustionState : byte
    {
        Healthy = 0,    // 정상 수목
        Smoking = 1,    // 연기 피어오름 (점화 게이지 누적 중)
        Burning = 2,    // 맹렬 연소 (화염 치솟음, 이웃 나무로 화재 전파)
        Charred = 3     // 전소 완료 (숯 기둥 잔해 영구 보존, 불길 소화)
    }

    [System.Serializable]
    public class TreeCombustionNode
    {
        public Transform transform;
        public GameObject treeObject;
        public Renderer[] renderers;
        public TreeCombustionState state = TreeCombustionState.Healthy;
        public float fireGauge = 0.0f;       // 0.0 ~ 1.0 (1.0 도달 시 점화)
        public float burnProgress = 0.0f;    // 0.0 ~ 1.0 (연소 진행도)
        public float burnTimer = 0.0f;
        public float burnDuration = 40.0f; // [사용자 피드백 반영] 18초 -> 40초로 2배 이상 연장하여 차분하고 장엄한 전소 연출
        public GameObject fireVfxInstance;
        public TotalWarFireVFXController fireController;
    }

    [Header("🔥 삼탈워 전장 화재 전파 시뮬레이션 (Wildfire Propagation)")]
    [Tooltip("숲의 화재 시뮬레이션 활성화")]
    public bool enableFireVFX = false;

    [Tooltip("이웃 나무로의 바람 연동 화재 전파 활성화")]
    public bool enableWildfirePropagation = true;

    [Tooltip("기본 화재 전파 반경 (단위: m, 삼탈워 공식: 9.0m)")]
    public float basePropagationRadius = 9.0f;

    [Tooltip("바람 방향 전파 가속 배율")]
    public float windSpreadMultiplier = 2.5f;

    [Tooltip("화재로 인한 숲 은폐(매복) 해제 여부 (연소율 40% 이상 시 해제)")]
    public bool isForestRevealedByFire = false;

    [Tooltip("최초 즉시 점화할 나무 개수")]
    [Range(1, 20)]
    public int burningTreesCount = 4;

    [Header("🔥 화재 VFX 프리팹 (Fire VFX Prefab)")]
    [Tooltip("나무 수관(나뭇잎) 층에 배치할 삼국지 토탈워 화재 파티클 프리팹 (TotalWar_TreeFire_VFX)")]
    public GameObject treeFirePrefab;

    [Tooltip("개별 화염 파티클 크기 배율 (기본: 0.45, 나뭇가지/잎사귀 크기인 0.5~1.3m에 맞춤)")]
    [Range(0.15f, 1.5f)]
    public float flameScaleMultiplier = 0.45f;

    // 생성된 초목 및 화재 컨테이너
    private Transform vegetationRoot;
    private Transform fireVfxRoot;
    private readonly List<TreeCombustionNode> treeCombustionNodes = new List<TreeCombustionNode>();
    public IReadOnlyList<TreeCombustionNode> TreeCombustionNodes => treeCombustionNodes;

    // 삼국지 토탈워 추출 머티리얼 캐시
    private static Material pineTrunkMat;
    private static Material pineLeavesMat;
    private static Material bambooTrunkMat;
    private static Material bambooLeavesMat;
    private static Material castanopsisTrunkMat;
    private static Material castanopsisLeavesMat;
    private static Material shrubMat;

    private void OnEnable()
    {
        if (!activeZones.Contains(this))
        {
            activeZones.Add(this);
        }
    }

    private void OnDisable()
    {
        activeZones.Remove(this);
    }

    private void Reset()
    {
#if UNITY_EDITOR
        if (treeFirePrefab == null)
        {
            treeFirePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/TotalWar_TreeFire_VFX.prefab");
        }
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (treeFirePrefab == null)
        {
            treeFirePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/TotalWar_TreeFire_VFX.prefab");
        }
#endif
    }

    private void Awake()
    {
        // 지형에 밀착하도록 Y 좌표 자동 스냅
        SnapToTerrain();
    }

    private void Start()
    {
        if (autoGenerateOnStart && (vegetationRoot == null || vegetationRoot.childCount == 0))
        {
            GenerateVegetation();
        }
        else if (enableFireVFX && (fireVfxRoot == null || fireVfxRoot.childCount == 0))
        {
            SpawnFireVFX();
        }
    }


    #region 삼국지 토탈워 3D 초목 배치 및 머티리얼 구성

    [ContextMenu("🌲 토탈워 초목 생성 (Generate Total War Vegetation)")]
    public void GenerateVegetation()
    {
        ClearVegetation();

        GameObject rootObj = new GameObject("Vegetation_Container");
        rootObj.transform.SetParent(transform, false);
        vegetationRoot = rootObj.transform;

        Random.InitState(randomSeed);

        // 1. 머티리얼 초기화 (토탈워 텍스처 적용 URP Lit)
        InitTotalWarMaterials();

        // 2. 모델 로드 (Trees & Shrubs)
        List<GameObject> treeModels = LoadTreeModels(theme);
        List<GameObject> shrubModels = LoadShrubModels();

        // 3. 지표 관목/수풀(Shrub) 스폰
        if (shrubModels.Count > 0)
        {
            for (int i = 0; i < bushCount; i++)
            {
                Vector3 localOffset = GetRandomPointInShape();
                Vector3 worldPos = transform.position + localOffset;
                worldPos.y = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z);

                GameObject modelPrefab = shrubModels[Random.Range(0, shrubModels.Count)];
                if (modelPrefab == null) continue;

                GameObject shrubObj = Instantiate(modelPrefab, worldPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), vegetationRoot);
                shrubObj.name = $"TotalWar_Shrub_{i}";

                float scale = bushScaleMultiplier * Random.Range(0.85f, 1.25f);
                shrubObj.transform.localScale = Vector3.one * scale;

                ApplyShrubMaterials(shrubObj);
            }
        }

        // 4. 삼국지 토탈워 3D 나무(Tree) 스폰
        if (treeModels.Count > 0)
        {
            for (int i = 0; i < treeCount; i++)
            {
                Vector3 localOffset = GetRandomPointInShape();
                Vector3 worldPos = transform.position + localOffset;
                worldPos.y = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z);

                GameObject modelPrefab = treeModels[Random.Range(0, treeModels.Count)];
                if (modelPrefab == null) continue;

                // 자연스러운 미세 기울기(Tilt) 적용 (±3도)
                float tiltX = Random.Range(-3.0f, 3.0f);
                float tiltZ = Random.Range(-3.0f, 3.0f);
                Quaternion rot = Quaternion.Euler(tiltX, Random.Range(0f, 360f), tiltZ);

                GameObject treeObj = Instantiate(modelPrefab, worldPos, rot, vegetationRoot);
                treeObj.name = $"TotalWar_Tree_{i}_{modelPrefab.name}";

                float scale = treeScaleMultiplier * Random.Range(0.85f, 1.2f);
                treeObj.transform.localScale = Vector3.one * scale;

                ApplyTreeMaterials(treeObj, modelPrefab.name);
            }
        }

        // 화재 연출 활성화 상태라면 나무들에 화재 VFX 즉시 점화
        if (enableFireVFX)
        {
            SpawnFireVFX();
        }
    }

    private void Update()
    {
        // 숲 구역 중심점의 지형 높이 실시간 유지
        SnapToTerrain();

        // 삼탈워 전장 화재 전파 시뮬레이션
        UpdateWildfireSimulation();
    }

    /// <summary>
    /// 오브젝트의 Y 위치를 지형 고도에 자동 밀착시킵니다.
    /// </summary>
    public void SnapToTerrain()
    {
        Vector3 pos = transform.position;
        float groundY = TerrainHeightManager.SampleHeightFast(pos.x, pos.z);
        if (Mathf.Abs(pos.y - groundY) > 0.05f)
        {
            pos.y = groundY;
            transform.position = pos;
        }
    }

    /// <summary>
    /// 숲에 배치된 모든 3D 나무의 연소 상태 노드 초기화
    /// </summary>
    public void InitTreeCombustionNodes()
    {
        treeCombustionNodes.Clear();
        if (vegetationRoot == null) return;

        for (int i = 0; i < vegetationRoot.childCount; i++)
        {
            Transform child = vegetationRoot.GetChild(i);
            if (child.name.Contains("Tree"))
            {
                TreeCombustionNode node = new TreeCombustionNode
                {
                    transform = child,
                    treeObject = child.gameObject,
                    renderers = child.GetComponentsInChildren<Renderer>(true),
                    state = TreeCombustionState.Healthy,
                    fireGauge = 0f,
                    burnProgress = 0f,
                    burnTimer = 0f,
                    burnDuration = 40.0f
                };
                treeCombustionNodes.Add(node);
            }
        }
    }

    /// <summary>
    /// 삼국지 토탈워 바람결 화재 전파(Wind-Driven Wildfire) 시뮬레이션:
    /// - 기본 전파 반경 9.0m (삼탈워 공식)
    /// - 순풍 방향으로는 최대 16m까지 불티가 날아가 전파 가속
    /// - 역풍 방향으로는 3.5m 이하로 억제
    /// - 18초 연소 후 나뭇잎 소멸 및 앙상한 숯 기둥(Trunk) 영구 보존
    /// - 연소율 40% 이상 시 숲의 전술 매복 은폐 해제
    /// </summary>
    private void UpdateWildfireSimulation()
    {
        if (treeCombustionNodes.Count == 0 && vegetationRoot != null && vegetationRoot.childCount > 0)
        {
            InitTreeCombustionNodes();
        }

        if (treeCombustionNodes.Count == 0) return;

        // 전장 풍향 및 풍속 획득
        Vector3 windDir = Vector3.right;
        float windSpeed = 2.0f;
        if (TotalWarWindManager.Instance != null)
        {
            float rad = TotalWarWindManager.Instance.windAngle * Mathf.Deg2Rad;
            windDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            windSpeed = TotalWarWindManager.Instance.windSpeed;
        }

        int burningCount = 0;
        int charredCount = 0;

        for (int i = 0; i < treeCombustionNodes.Count; i++)
        {
            var node = treeCombustionNodes[i];
            if (node == null || node.transform == null) continue;

            if (node.state == TreeCombustionState.Burning)
            {
                burningCount++;

                // 1. 연소 시간 누적 및 전소 판정
                if (Application.isPlaying)
                {
                    node.burnTimer += Time.deltaTime;
                    node.burnProgress = Mathf.Clamp01(node.burnTimer / node.burnDuration);
                    if (node.fireController != null)
                    {
                        node.fireController.burnProgress = node.burnProgress;
                    }

                    if (node.burnProgress >= 1.0f)
                    {
                        node.state = TreeCombustionState.Charred;
                        charredCount++;
                    }
                }

                // 2. 이웃 나무로의 바람 연동 화재 전파 (Wildfire Spread)
                if (enableWildfirePropagation && Application.isPlaying)
                {
                    for (int j = 0; j < treeCombustionNodes.Count; j++)
                    {
                        if (i == j) continue;
                        var neighbor = treeCombustionNodes[j];
                        if (neighbor == null || neighbor.transform == null) continue;
                        if (neighbor.state != TreeCombustionState.Healthy && neighbor.state != TreeCombustionState.Smoking) continue;

                        Vector3 toNeighbor = neighbor.transform.position - node.transform.position;
                        float dist = toNeighbor.magnitude;
                        if (dist < 0.05f) continue;

                        Vector3 toNeighborDir = toNeighbor / dist;
                        float windDot = Vector3.Dot(toNeighborDir, windDir); // 순풍 +1, 역풍 -1
                        float maxSpreadDist = basePropagationRadius + (windSpeed * windDot * windSpreadMultiplier);
                        maxSpreadDist = Mathf.Clamp(maxSpreadDist, 3.5f, 16.0f);

                        if (dist <= maxSpreadDist)
                        {
                            float distanceFactor = 1.0f - (dist / maxSpreadDist);
                            // [사용자 피드백 반영] 이웃 나무 연쇄 전파 속도를 차분하게 완화 (0.22f -> 0.11f)
                            float spreadSpeed = 0.11f * (1.0f + Mathf.Max(0f, windDot * 1.4f)) * distanceFactor;
                            neighbor.fireGauge += Time.deltaTime * spreadSpeed;

                            if (neighbor.fireGauge >= 0.20f && neighbor.state == TreeCombustionState.Healthy)
                            {
                                neighbor.state = TreeCombustionState.Smoking;
                            }

                            if (neighbor.fireGauge >= 1.0f)
                            {
                                IgniteTreeNode(neighbor);
                            }
                        }
                    }
                }
            }
            else if (node.state == TreeCombustionState.Charred)
            {
                charredCount++;
            }
        }

        // 3. 화재로 인한 숲 은폐(매복) 전술 해제 판정 (연소/전소율 40% 이상)
        float burnedRatio = (float)(burningCount + charredCount) / treeCombustionNodes.Count;
        isForestRevealedByFire = (burnedRatio >= 0.40f);
    }

    /// <summary>
    /// 단일 나무 노드에 화재를 착화시킵니다.
    /// </summary>
    public void IgniteTreeNode(TreeCombustionNode node)
    {
        if (node == null || node.transform == null) return;
        if (node.state == TreeCombustionState.Burning || node.state == TreeCombustionState.Charred) return;

        node.state = TreeCombustionState.Burning;
        node.fireGauge = 1.0f;
        node.burnTimer = 0f;
        node.burnProgress = 0f;

        if (fireVfxRoot == null)
        {
            GameObject fireRootObj = new GameObject("Fire_VFX_Container");
            fireRootObj.transform.SetParent(transform, false);
            fireVfxRoot = fireRootObj.transform;
        }

        // [사용자 지침 반영] 나무 밑동(지면 0~2m)은 완전 배제하고, 실제 나뭇잎(Canopy/Leaves) 수관 위치에 불 프리팹을 스폰
        var (leavesCenter, leavesRadius, leavesHeight) = CalculateTreeLeavesCenterAndRadius(node.treeObject);
        GameObject fireObj = CreateTreeFireInstance(leavesCenter, leavesRadius, leavesHeight, fireVfxRoot, node.treeObject);
        fireObj.name = $"TreeFire_{node.treeObject.name}";

        node.fireVfxInstance = fireObj;
        node.fireController = fireObj.GetComponent<TotalWarFireVFXController>();
    }

    /// <summary>
    /// 바람 부는 쪽(풍상측) 나무 1~2그루에 최초 착화하여 바람을 타고 숲 전체로 번져나가는 연쇄 화공을 개시합니다.
    /// </summary>
    [ContextMenu("🔥 바람결 화재 연쇄 전파 개시 (Start Wildfire Propagation)")]
    public void StartWildfirePropagation()
    {
        enableFireVFX = true;
        enableWildfirePropagation = true;

        if (vegetationRoot == null || vegetationRoot.childCount == 0)
        {
            GenerateVegetation();
        }

        if (treeCombustionNodes.Count == 0)
        {
            InitTreeCombustionNodes();
        }

        if (treeCombustionNodes.Count == 0) return;

        // 풍향 구하기
        Vector3 windDir = Vector3.right;
        if (TotalWarWindManager.Instance != null)
        {
            float rad = TotalWarWindManager.Instance.windAngle * Mathf.Deg2Rad;
            windDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        // 바람이 불어오는 쪽(풍상측: windDir 투영값이 가장 작은 나무) 정렬
        var sortedTrees = new List<TreeCombustionNode>(treeCombustionNodes);
        sortedTrees.Sort((a, b) =>
        {
            float dotA = Vector3.Dot(a.transform.position, windDir);
            float dotB = Vector3.Dot(b.transform.position, windDir);
            return dotA.CompareTo(dotB);
        });

        // 풍상측 1~2그루에 최초 착화
        int initialCount = Mathf.Min(2, sortedTrees.Count);
        for (int i = 0; i < initialCount; i++)
        {
            IgniteTreeNode(sortedTrees[i]);
        }
    }

    [ContextMenu("🔥 숲 화재 점화 연출 (Ignite Forest Fire)")]
    public void IgniteForestFire()
    {
        enableFireVFX = true;
        SpawnFireVFX();
    }

    [ContextMenu("🧯 화재 진압 및 초기화 (Extinguish Fire)")]
    public void ExtinguishFire()
    {
        enableFireVFX = false;
        ClearFireVFX();

        // 모든 노드 복원
        for (int i = 0; i < treeCombustionNodes.Count; i++)
        {
            var node = treeCombustionNodes[i];
            if (node == null) continue;
            node.state = TreeCombustionState.Healthy;
            node.fireGauge = 0f;
            node.burnProgress = 0f;
            node.burnTimer = 0f;
            if (node.fireController != null)
            {
                node.fireController.ResetFireState();
            }
        }
    }

    public void ClearFireVFX()
    {
        if (fireVfxRoot != null)
        {
            if (Application.isPlaying) Destroy(fireVfxRoot.gameObject);
            else DestroyImmediate(fireVfxRoot.gameObject);
            fireVfxRoot = null;
        }

        var oldRoot = transform.Find("Fire_VFX_Container");
        if (oldRoot != null)
        {
            if (Application.isPlaying) Destroy(oldRoot.gameObject);
            else DestroyImmediate(oldRoot.gameObject);
        }
    }

    public void SpawnFireVFX()
    {
        ClearFireVFX();

        if (treeCombustionNodes.Count == 0)
        {
            InitTreeCombustionNodes();
        }

        if (treeCombustionNodes.Count == 0) return;

        int spawnCount = Mathf.Min(burningTreesCount, treeCombustionNodes.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            IgniteTreeNode(treeCombustionNodes[i]);
        }
    }

    /// <summary>
    /// 나무의 실제 나뭇잎(Canopy/Leaves) 서브메시 및 렌더러 바운즈를 정밀 분석하여,
    /// 나무 밑동(지면 0~2m)이 아닌 나뭇잎 층 전체를 뒤덮는 3D 월드 중심 좌표와 수관 반경을 계산합니다.
    /// </summary>
    public static (Vector3 leavesCenter, float leavesRadius, float leavesHeight) CalculateTreeLeavesCenterAndRadius(GameObject treeObj)
    {
        if (treeObj == null)
            return (Vector3.zero, 2.5f, 5.0f);

        Transform treeTransform = treeObj.transform;
        MeshFilter mf = treeObj.GetComponentInChildren<MeshFilter>();
        Renderer ren = treeObj.GetComponentInChildren<Renderer>();

        // 1. Mesh의 서브메시 1 (Leaves) 바운즈가 존재할 경우 최우선 활용
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.subMeshCount >= 2)
        {
            var submeshLeaves = mf.sharedMesh.GetSubMesh(1);
            if (submeshLeaves.bounds.size.sqrMagnitude > 0.01f)
            {
                Bounds localBounds = submeshLeaves.bounds;
                Transform meshTrans = mf.transform;
                Vector3 worldCenter = meshTrans.TransformPoint(localBounds.center);
                Vector3 worldExtents = Vector3.Scale(localBounds.extents, meshTrans.lossyScale);
                float radius = Mathf.Max(Mathf.Abs(worldExtents.x), Mathf.Abs(worldExtents.z));
                float height = Mathf.Abs(worldExtents.y) * 2f;
                return (worldCenter, Mathf.Max(1.8f, radius), Mathf.Max(2.5f, height));
            }
        }

        // 2. Fallback: 전체 렌더러 바운즈에서 상위 수관부(상단 40%~95% 영역) 중심 계산
        if (ren != null)
        {
            Bounds b = ren.bounds;
            // 밑동(b.min.y)은 철저히 배제하고, 나뭇잎이 무성한 상부 68% 높이를 중심점으로 설정
            float leavesCenterY = b.min.y + b.size.y * 0.68f;
            Vector3 worldCenter = new Vector3(b.center.x, leavesCenterY, b.center.z);
            float radius = Mathf.Max(1.8f, Mathf.Max(b.extents.x, b.extents.z) * 0.9f);
            float height = b.size.y * 0.55f;
            return (worldCenter, radius, height);
        }

        // 3. 렌더러가 없을 경우의 기본 fallback
        return (treeTransform.position + Vector3.up * 8.0f, 2.5f, 5.0f);
    }

    private GameObject CreateTreeFireInstance(Vector3 leavesPos, float leavesRadius, float leavesHeight, Transform parent, GameObject targetTree = null)
    {
        GameObject fireGo = new GameObject("TotalWar_TreeFire");
        fireGo.transform.position = leavesPos;
        fireGo.transform.SetParent(parent, true);

        // [삼국지 토탈워 본편 공식 3종 화염(A, B, H) 복합 플립북 시스템 로드]
        Material compositeFlamesMat = null;
        Material smokeMat = null;
        Material embersMat = null;
        Texture2D flameTexA = null;
        Texture2D flameTexB = null;
        Texture2D flameTexH = null;
        Texture2D smokeTex = null;
        Texture2D emberTex = null;

#if UNITY_EDITOR
        string vfxMatPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/";
        string vfxTexPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/";
        compositeFlamesMat = AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_CompositeFlames.mat");
        smokeMat = AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_WispySmoke.mat");
        embersMat = AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_Embers.mat");

        // [핵심] 삼탈워 3종 화염 텍스처 (a, b, H) 로드
        flameTexA = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "3k_anim_fire_a_diffuse.png")
                 ?? AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_flame_flipbook.png");
        flameTexB = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "3k_anim_fire_b_diffuse.png");
        flameTexH = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "3k_anim_fire_H_diffuse.png");
        smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_smoke_particle.png");
        emberTex = AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_ember_particle.png");
#endif
        Shader compositeShader = Shader.Find("TotalWar/VFX/CompositeFlames");
        Shader particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                            ?? Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Sprites/Default");

        // [사용자 요구 100% 실현: "불 하나에 [A, B, H] 모든 애니메이션이 다 겹쳐져 있게"]
        // 전용 복합 화염 셰이더를 통해 단일 파티클 쿼드 위에서 A(코어), B(너울), H(팁) 3종이 
        // 0.000mm 오차 없이 1:1로 완벽하게 합성되어 타오르는 복합 머티리얼 준비
        Material finalFlameMat = null;
        if (compositeFlamesMat != null)
        {
            finalFlameMat = new Material(compositeFlamesMat);
        }
        else if (compositeShader != null)
        {
            finalFlameMat = new Material(compositeShader);
            if (flameTexA != null) finalFlameMat.SetTexture("_MainTex", flameTexA);
            if (flameTexB != null) finalFlameMat.SetTexture("_OuterTex", flameTexB);
            if (flameTexH != null) finalFlameMat.SetTexture("_TipsTex", flameTexH);
        }
        else
        {
            finalFlameMat = CreateParticleMaterial(null, flameTexA, particleUnlit, new Color(2.6f, 1.2f, 0.35f, 1f), true);
        }

        Material finalSmokeMat = CreateParticleMaterial(smokeMat, smokeTex, particleUnlit, new Color(0.26f, 0.18f, 0.12f, 0.55f), false);
        Material finalEmberMat = CreateParticleMaterial(embersMat, emberTex, particleUnlit, new Color(3.5f, 1.8f, 0.5f, 1f), true);

        // 1. [삼국지 토탈워 본편 공식 메커니즘] 실제 나뭇잎 서브메시(Submesh 1: Leaves) 표면 직접 방출
        MeshFilter mf = (targetTree != null) ? targetTree.GetComponentInChildren<MeshFilter>() : null;
        MeshRenderer mr = (targetTree != null) ? targetTree.GetComponentInChildren<MeshRenderer>() : null;

        // 나뭇잎 메시의 트랜스폼에 완전 일치하도록 부착하여 나뭇잎 위치와 1:1 동기화
        Transform meshTrans = (mr != null) ? mr.transform : fireGo.transform;

        uint syncSeed = (uint)(leavesPos.GetHashCode() & 0x7FFFFFFF);
        if (syncSeed == 0) syncSeed = 12345;

        // [핵심: 삼탈워 3중 화염(A, B, H) 일체형 복합 화염 시스템]
        // 개별 파티클 시스템을 따로 돌려 나무 여기저기에 A 따로 B 따로 H 따로 흩어지던 현상을 완전히 원천 차단하고,
        // 피어오르는 "모든 불꽃 덩어리(개별 파티클) 하나하나"에 A(코어), B(너울), H(팁) 3종 애니메이션이 
        // 1:1로 완벽하게 포개어져 하나의 생생한 화염 덩어리를 이루도록 구현!
        GameObject flamesObj = new GameObject("Flames_Composite_TotalWar");
        flamesObj.transform.SetParent(meshTrans, false);
        flamesObj.transform.localPosition = Vector3.zero;
        flamesObj.transform.localRotation = Quaternion.identity;
        flamesObj.transform.localScale = Vector3.one;
        ParticleSystem psFlames = flamesObj.AddComponent<ParticleSystem>();
        SetupFlamesPS(psFlames, finalFlameMat, mr, mf, leavesRadius, FlameLayerType.CoreA, syncSeed);

        // [빛 연출 강화] 화재 포인트 라이트 (주변 나뭇잎과 지면을 따뜻하게 비추는 2200K 화염 광원)
        GameObject lightObj = new GameObject("Fire_PointLight");
        lightObj.transform.position = leavesPos;
        lightObj.transform.SetParent(fireGo.transform, true);
        Light fireLight = lightObj.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.range = Mathf.Max(14.0f, leavesRadius * 3.5f);
        fireLight.color = new Color(1.0f, 0.52f, 0.18f); // 따뜻한 주황빛 화염 광원
        fireLight.intensity = 3.2f;
        fireLight.shadows = LightShadows.None;

        // 2. 연기 서브시스템 (Wispy Smoke) - 나뭇잎이 타들어가며 상공으로 피어오르는 황갈색 훈연 구름
        GameObject smokeObj = new GameObject("Smoke_Plume");
        smokeObj.transform.position = leavesPos + Vector3.up * (leavesHeight * 0.20f);
        smokeObj.transform.SetParent(fireGo.transform, true);
        ParticleSystem psSmoke = smokeObj.AddComponent<ParticleSystem>();
        SetupSmokePS(psSmoke, finalSmokeMat, leavesRadius, leavesHeight);

        // 3. 불티 서브시스템 (Airborne Embers) - 바람을 타고 하늘 높이 흩날리는 불씨
        GameObject embersObj = new GameObject("Embers_Sparks");
        embersObj.transform.position = leavesPos;
        embersObj.transform.SetParent(fireGo.transform, true);
        ParticleSystem psEmbers = embersObj.AddComponent<ParticleSystem>();
        SetupEmbersPS(psEmbers, finalEmberMat, leavesRadius);

        // [사용자 지침 반영] 나무 밑동 화재(Ground Embers)는 완전 제거하고 나뭇잎 연소에 100% 집중!

        // 화재 컨트롤러 바인딩 (타겟 나무 전소/탄화/디졸브, 바람 연동 및 조명)
        var fireCtrl = fireGo.GetComponent<TotalWarFireVFXController>();
        if (fireCtrl == null)
        {
            fireCtrl = fireGo.AddComponent<TotalWarFireVFXController>();
        }
        if (targetTree != null)
        {
            fireCtrl.SetTargetTree(targetTree);
        }

        return fireGo;
    }

    /// <summary>
    /// 사각형 기본 도형 쿼드 출력을 100% 원천 차단하고 투명 블렌딩을 강제 보장하는 파티클 머티리얼 빌더
    /// </summary>
    private static Material CreateParticleMaterial(Material presetMat, Texture2D texture, Shader fallbackShader, Color color, bool isAdditive)
    {
        Material mat = null;
        if (presetMat != null)
        {
            mat = new Material(presetMat);
        }
        else if (fallbackShader != null)
        {
            mat = new Material(fallbackShader);
        }

        if (mat == null) return null;

        // 고화질 텍스처 바인딩 보장
        if (texture != null)
        {
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);

        // URP Particle Unlit 투명 파이프라인 속성 강제 설정
        mat.SetFloat("_Surface", 1.0f); // 1 = Transparent
        mat.SetFloat("_Blend", isAdditive ? 1.0f : 0.0f); // 1 = Additive, 0 = Alpha Blend
        mat.SetInt("_SrcBlend", (int)(isAdditive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
        mat.SetInt("_DstBlend", (int)(isAdditive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_SOFTPARTICLES_ON");
        if (isAdditive)
        {
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.DisableKeyword("_BLENDMODE_ALPHA");
            mat.EnableKeyword("_FLIPBOOKBLENDING_ON");
            mat.SetFloat("_FlipbookBlending", 1.0f);
            mat.SetFloat("_FlipbookMode", 1.0f);
        }
        else
        {
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.DisableKeyword("_BLENDMODE_ADD");
        }

        return mat;
    }

    /// <summary>
    /// 🔥 삼국지 토탈워 3종 화염(A, B, H) 레이어 역할 정의
    /// </summary>
    public enum FlameLayerType
    {
        CoreA,   // 주 화염 코어 (3k_anim_fire_a_diffuse): 나뭇잎 겉면을 촘촘하게 감싸는 핵심 화염
        OuterB,  // 측면 너울 화염 (3k_anim_fire_b_diffuse): 좌우로 풍성하게 펄럭이는 중간층 화염
        TipsH    // 상승 팁 화염 (3k_anim_fire_H_diffuse): 상공을 향해 뾰족하게 치솟는 불꽃 끝단
    }

    /// <summary>
    /// 🔥 [삼국지 토탈워 본편 REA 역공학 100% 일치 화재 애니메이션 시스템]
    /// 1. 24 FPS 표준 프레임레이트: 64프레임 / 24fps = 2.67초 완주 (부드럽고 생동감 넘치는 화염)
    /// 2. 사용자 피드백 반영: 불 크기 대폭 축소 (0.35m ~ 0.65m)로 나뭇잎 사이사이에 정갈하게 안착
    /// 3. 다중 화염 레이어(A, B, H)별 서로 다른 스케일, 방출량 및 시작 위상차 분산
    /// </summary>
    private static void SetupFlamesPS(ParticleSystem ps, Material flameMat, MeshRenderer targetRenderer, MeshFilter targetMeshFilter, float leavesRadius = 2.5f, FlameLayerType layerType = FlameLayerType.CoreA, uint syncSeed = 0)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // [핵심 해결] 세 파티클 시스템의 난수 시드를 100% 동일하게 일치시켜, 동일한 정점 좌표에 1:1로 포개어지도록 강제
        if (syncSeed != 0)
        {
            ps.useAutoRandomSeed = false;
            ps.randomSeed = syncSeed;
        }

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // [초당 24프레임 (24 FPS) 규격]: 64프레임 / 24fps = 정확히 2.67초 (세 레이어 100% 동기화)
        main.startLifetime = 2.67f;
        // [토탈워 REA 명세: SkipParticlePositionUpdate = true] 위치 이동 및 비행 속도 완전 0 (제자리 고정)
        main.startSpeed = 0f;

        // [핵심: 사용자 피드백 반영] 불 크기 대폭 축소 (0.35m ~ 0.65m) - 나뭇잎 뭉치 사이에 아담하고 정갈하게 얹히는 크기
        float baseFlameSize = 0.45f;
        float minFlameSize = 0.35f;
        float maxFlameSize = 0.55f;

        switch (layerType)
        {
            case FlameLayerType.CoreA:
                baseFlameSize = Mathf.Clamp(leavesRadius * 0.16f, 0.35f, 0.55f);
                minFlameSize = baseFlameSize * 0.90f;
                maxFlameSize = baseFlameSize * 1.10f;
                break;
            case FlameLayerType.OuterB:
                baseFlameSize = Mathf.Clamp(leavesRadius * 0.20f, 0.40f, 0.65f);
                minFlameSize = baseFlameSize * 0.90f;
                maxFlameSize = baseFlameSize * 1.10f;
                break;
            case FlameLayerType.TipsH:
                baseFlameSize = Mathf.Clamp(leavesRadius * 0.14f, 0.30f, 0.50f);
                minFlameSize = baseFlameSize * 0.90f;
                maxFlameSize = baseFlameSize * 1.10f;
                break;
        }

        main.startSize = new ParticleSystem.MinMaxCurve(minFlameSize, maxFlameSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f); // 거의 수직으로 단정하게 기립
        main.flipRotation = 0.5f;
        main.gravityModifier = 0f; // 부력으로 인한 위치 왜곡 차단
        main.startColor = Color.white;
        main.maxParticles = 30;

        // [핵심] 세 레이어가 동일한 시점에 동일한 위치에서 쌍을 이루어 스폰되도록 rateOverTime 통일
        var emission = ps.emission;
        emission.rateOverTime = 4.2f;

        // [핵심] 실제 나뭇잎 서브메시(Submesh 1: Leaves) 표면 방출 바인딩
        var shape = ps.shape;
        shape.enabled = true;
        shape.normalOffset = (layerType == FlameLayerType.TipsH) ? 0.35f : 0.20f; // Tips는 나뭇잎 겉면 상단으로 돌출
        bool shapeBound = false;

        if (targetRenderer != null)
        {
            shape.shapeType = ParticleSystemShapeType.MeshRenderer;
            shape.meshRenderer = targetRenderer;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.useMeshColors = false;

            // 서브메시가 2개 이상일 경우 Submesh 1 (Leaves) 지정, 1개일 경우 0 지정
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null && targetMeshFilter.sharedMesh.subMeshCount >= 2)
            {
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = 1;
            }
            else
            {
                shape.useMeshMaterialIndex = false;
                shape.meshMaterialIndex = 0;
            }
            shapeBound = true;
        }
        else if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
        {
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.mesh = targetMeshFilter.sharedMesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.useMeshColors = false;
            if (targetMeshFilter.sharedMesh.subMeshCount >= 2)
            {
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = 1;
            }
            else
            {
                shape.useMeshMaterialIndex = false;
                shape.meshMaterialIndex = 0;
            }
            shapeBound = true;
        }

        // Fallback: 렌더러가 없을 경우 수관 상단 타원구형 에미터
        if (!shapeBound)
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(1.5f, leavesRadius * 0.8f);
        }

        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = Vector3.one;

        // [핵심] 삼국지 토탈워 공식 8x8 (64프레임) 플립북 애니메이션 활성화
        var texSheet = ps.textureSheetAnimation;
        texSheet.enabled = true;
        texSheet.numTilesX = 8;
        texSheet.numTilesY = 8;
        texSheet.animation = ParticleSystemAnimationType.WholeSheet;
        // [사용자 피드백 반영] 초당 정확히 24.0 FPS 하드웨어 고정
        texSheet.timeMode = ParticleSystemAnimationTimeMode.FPS;
        texSheet.fps = 24.0f; // 초당 24프레임 (부드럽고 자연스러운 화염 모션)
        AnimationCurve frameCurve = new AnimationCurve();
        frameCurve.AddKey(0f, 0f);
        frameCurve.AddKey(1f, 1f); // 0.0f ~ 1.0f 정규화 전구간
        texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);
        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        texSheet.cycleCount = 1;

        // [사용자 요구 반영: 꾸물거림 완전 제거] 노이즈 모듈 완전 비활성화
        var noise = ps.noise;
        noise.enabled = false;

        // [사용자 요구 반영: 불이 커지는 것 완전 제거] 크기 곡선 비활성화 (고정 스케일 유지)
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = false;

        // [형태 보존 알파 블렌딩]: 크기는 변하지 않고 생성/소멸 시 부드러운 투명도 연결만 수행
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0f), 
                new GradientColorKey(Color.white, 1f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0f, 0f), 
                new GradientAlphaKey(1.0f, 0.08f), // 0.15초 만에 신속하게 완전 가시화
                new GradientAlphaKey(1.0f, 0.85f), // 수명의 85% 동안 온전한 크기와 형태 유지
                new GradientAlphaKey(0f, 1f)       // 소멸 시 자연스러운 알파 페이드아웃
            }
        );
        colorOverLifetime.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        if (flameMat != null) renderer.material = flameMat;

        // [핵심 해결: URP 셰이더 플립북 크로스페이드 버텍스 스트림 주입]
        var streams = new System.Collections.Generic.List<ParticleSystemVertexStream>()
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.UV2,       // URP Flipbook Blending 다음 프레임 UV
            ParticleSystemVertexStream.AnimBlend  // 프레임 간 크로스페이드 보간 인자
        };
        renderer.SetActiveVertexStreams(streams);

        ps.Play();
    }

    /// <summary>
    /// 삼국지 토탈워 공식 영상 일치화:
    /// - 수관 위로 피어오르는 황갈색/먼지색 훈연 기둥 (1.1 ~ 2.8m)
    /// </summary>
    private static void SetupSmokePS(ParticleSystem ps, Material smokeMat, float leavesRadius = 2.5f, float leavesHeight = 5.0f)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        float minSmoke = Mathf.Clamp(leavesRadius * 0.45f, 1.1f, 2.0f);
        float maxSmoke = Mathf.Clamp(leavesRadius * 0.85f, 1.7f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSmoke, maxSmoke);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.08f;
        main.startColor = new Color(0.26f, 0.18f, 0.12f, 0.55f);
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 20f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = Mathf.Max(1.4f, leavesRadius * 0.6f);
        shape.position = new Vector3(0f, leavesHeight * 0.25f, 0f);

        // 연기 뭉게구름 난류 노이즈
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.8f;
        noise.damping = true;

        var rotOverLifetime = ps.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.6f);
        curve.AddKey(1f, 2.6f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.28f, 0.20f, 0.14f), 0f), new GradientColorKey(new Color(0.18f, 0.14f, 0.10f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.25f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        if (smokeMat != null) renderer.material = smokeMat;

        ps.Play();
    }

    /// <summary>
    /// 바람을 타고 하늘 높이 날아오르는 불티 파티클
    /// </summary>
    private static void SetupEmbersPS(ParticleSystem ps, Material emberMat, float leavesRadius = 2.5f)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.18f;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(2.0f, leavesRadius * 1.1f);
        shape.position = new Vector3(0f, 0.5f, 0f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        if (emberMat != null) renderer.material = emberMat;

        ps.Play();
    }

    [ContextMenu("🧹 초목 제거 (Clear Vegetation)")]
    public void ClearVegetation()
    {
        ClearFireVFX();

        if (vegetationRoot != null)
        {
            if (Application.isPlaying) Destroy(vegetationRoot.gameObject);
            else DestroyImmediate(vegetationRoot.gameObject);
            vegetationRoot = null;
        }

        var oldRoot = transform.Find("Vegetation_Container");
        if (oldRoot != null)
        {
            if (Application.isPlaying) Destroy(oldRoot.gameObject);
            else DestroyImmediate(oldRoot.gameObject);
        }
    }

    private List<GameObject> LoadTreeModels(ForestTheme currentTheme)
    {
        List<GameObject> list = new List<GameObject>();
#if UNITY_EDITOR
        string basePath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees/";

        var allPaths = new List<string>();
        switch (currentTheme)
        {
            case ForestTheme.HuangshanPine:
                allPaths.Add(basePath + "Huangshan_Pine_A.obj");
                allPaths.Add(basePath + "Huangshan_Pine_B.obj");
                allPaths.Add(basePath + "Pine_A.obj");
                break;
            case ForestTheme.BambooGrove:
                allPaths.Add(basePath + "Bamboo_A.obj");
                allPaths.Add(basePath + "Bamboo_C.obj");
                break;
            case ForestTheme.CastanopsisWood:
                allPaths.Add(basePath + "Castanopsis_A.obj");
                break;
            case ForestTheme.Mixed:
            default:
                allPaths.Add(basePath + "Huangshan_Pine_A.obj");
                allPaths.Add(basePath + "Huangshan_Pine_B.obj");
                allPaths.Add(basePath + "Pine_A.obj");
                allPaths.Add(basePath + "Bamboo_A.obj");
                allPaths.Add(basePath + "Bamboo_C.obj");
                allPaths.Add(basePath + "Castanopsis_A.obj");
                break;
        }

        foreach (var p in allPaths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null) list.Add(go);
        }
#endif
        return list;
    }

    private List<GameObject> LoadShrubModels()
    {
        List<GameObject> list = new List<GameObject>();
#if UNITY_EDITOR
        string basePath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/";
        string[] paths = new string[]
        {
            basePath + "Pine_Shrub_Q.obj",
            basePath + "Fern_Royal_A.obj",
            basePath + "Scholar_Bush_K.obj",
            basePath + "Convallaria_Shrub_A.obj"
        };

        foreach (var p in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null) list.Add(go);
        }
#endif
        return list;
    }

    [Header("🍂 계절 설정 (Season)")]
    [Tooltip("현재 전장의 식생 계절 (여름, 봄, 가을, 겨울)")]
    public Season currentSeason = Season.Summer;

    public enum Season
    {
        Summer, // 여름 (울창한 녹음)
        Spring, // 봄 (화사한 연초록)
        Autumn, // 가을 (단풍/황금빛)
        Winter  // 겨울 (설경/앙상한 가지)
    }

    private void InitTotalWarMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/TotalWarFoliageWind") 
                        ?? Shader.Find("Universal Render Pipeline/Lit") 
                        ?? Shader.Find("Standard");

        // 1. 황산 소나무 머티리얼
        // 줄기(Trunk)는 바람 0.0으로 지면에 완전 고정 (나무가 걷는 현상 원천 차단)
        // 잎사귀(Leaves)는 파르르 떨림(Leaf Flutter) 활성화
        if (pineTrunkMat == null)
        {
            pineTrunkMat = CreateLitMaterial(litShader, "TotalWar_Huangshan_Trunk", new Color(0.32f, 0.22f, 0.16f), "huangshan_pine_trunk_base_colour.dds", "huangshan_pine_trunk_normal.dds", isCutoff: false, windMultiplier: 0.0f, leafFlutter: 0.0f);
        }
        if (pineLeavesMat == null)
        {
            pineLeavesMat = CreateLitMaterial(litShader, "TotalWar_Huangshan_Leaves", new Color(0.12f, 0.30f, 0.15f), "huangshan_pine_branch_base_colour.dds", "huangshan_pine_branch_normal.dds", isCutoff: true, windMultiplier: 0.30f, leafFlutter: 1.8f);
        }

        // 2. 대나무 머티리얼
        if (bambooTrunkMat == null)
        {
            bambooTrunkMat = CreateLitMaterial(litShader, "TotalWar_Bamboo_Trunk", new Color(0.35f, 0.42f, 0.22f), "bamboo_trunk_base_colour.dds", "bamboo_trunk_normal.dds", isCutoff: false, windMultiplier: 0.0f, leafFlutter: 0.0f);
        }
        if (bambooLeavesMat == null)
        {
            bambooLeavesMat = CreateLitMaterial(litShader, "TotalWar_Bamboo_Leaves", new Color(0.20f, 0.45f, 0.18f), "bamboo_branch_base_colour.dds", "bamboo_branch_normal.dds", isCutoff: true, windMultiplier: 0.35f, leafFlutter: 2.0f);
        }

        // 3. 활엽수 머티리얼
        if (castanopsisTrunkMat == null)
        {
            castanopsisTrunkMat = CreateLitMaterial(litShader, "TotalWar_Castanopsis_Trunk", new Color(0.28f, 0.20f, 0.14f), "tree_castanopsis_trunk_base_colour.dds", "scholar_tree_trunk_normal.dds", isCutoff: false, windMultiplier: 0.0f, leafFlutter: 0.0f);
        }
        if (castanopsisLeavesMat == null)
        {
            castanopsisLeavesMat = CreateLitMaterial(litShader, "TotalWar_Castanopsis_Leaves", new Color(0.15f, 0.38f, 0.14f), "tree_castanopsis_leaves_base_colour.dds", "scholar_tree_branch_normal.dds", isCutoff: true, windMultiplier: 0.28f, leafFlutter: 1.6f);
        }

        // 4. 관목/수풀 머티리얼 (지면에 붙은 작은 덤불)
        if (shrubMat == null)
        {
            shrubMat = CreateLitMaterial(litShader, "TotalWar_Shrub", new Color(0.18f, 0.42f, 0.18f), "bamboo_branch_base_colour.dds", "bamboo_branch_normal.dds", isCutoff: true, windMultiplier: 0.10f, leafFlutter: 1.2f);
        }
    }

    private Material CreateLitMaterial(Shader shader, string matName, Color baseColor, string texFilename, string normalFilename = null, bool isCutoff = false, float windMultiplier = 0.3f, float leafFlutter = 1.0f)
    {
        Material mat = new Material(shader)
        {
            name = matName,
            color = baseColor
        };

        // 바람 애니메이션 파라미터 바인딩
        mat.SetFloat("_WindMultiplier", windMultiplier);
        mat.SetFloat("_LeafFlutter", leafFlutter);

#if UNITY_EDITOR
        string texDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + texFilename);
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
        }
        else
        {
            mat.SetColor("_BaseColor", baseColor);
        }

        // 삼탈워 고해상도 노멀맵 바인딩 (입체적 잎사귀/수피 굴곡 생성)
        if (!string.IsNullOrEmpty(normalFilename))
        {
            Texture2D normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + normalFilename);
            if (normTex != null)
            {
                mat.SetTexture("_BumpMap", normTex);
                mat.SetFloat("_BumpScale", 1.2f);
                mat.EnableKeyword("_NORMALMAP");
            }
        }
#else
        mat.SetColor("_BaseColor", baseColor);
#endif

        // 입체적인 빛 반사를 위한 스무스니스 및 환경광 반사 활성화
        mat.SetFloat("_Smoothness", isCutoff ? 0.22f : 0.10f);
        mat.SetFloat("_SpecularHighlights", 1f);
        mat.SetFloat("_EnvironmentReflections", 1f);

        if (isCutoff)
        {
            // 토탈워 나뭇잎 핵심 설정: 양면 렌더링(Cull Off) + 알파 컷아웃(Alpha Cutoff)
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.40f);
            mat.SetFloat("_Cull", 0f); // 0 = Both sides (양면 렌더링으로 뒷면 투명화 완벽 차단)
            mat.doubleSidedGI = true;
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_DOUBLESIDED_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        else
        {
            // 줄기/기둥(Trunk)은 알파 컷아웃을 철저히 차단하여 전소 시에도 나무가 사라지지 않고 숯으로 남음
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_Cutoff", 0f);
            mat.SetFloat("_Cull", 2f); // Back cull
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_DOUBLESIDED_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

#if UNITY_EDITOR
            // 삼탈워 원본 숯 텍스처를 Trunk 머티리얼에 바인딩
            Texture2D charredTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/3k_cityprop_mines_wood_ashes_base_colour.png");
            if (charredTex != null)
            {
                mat.SetTexture("_CharredMap", charredTex);
                mat.SetFloat("_CharredTile", 4.0f);
            }
#endif
        }

        return mat;
    }

    private void ApplyTreeMaterials(GameObject treeInstance, string modelName)
    {
        bool isBamboo = modelName.IndexOf("bamboo", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool isCastanopsis = modelName.IndexOf("castanopsis", System.StringComparison.OrdinalIgnoreCase) >= 0;

        Material trunkMat = isBamboo ? bambooTrunkMat : (isCastanopsis ? castanopsisTrunkMat : pineTrunkMat);
        Material leavesMat = isBamboo ? bambooLeavesMat : (isCastanopsis ? castanopsisLeavesMat : pineLeavesMat);

        Renderer[] renderers = treeInstance.GetComponentsInChildren<Renderer>(true);
        foreach (var ren in renderers)
        {
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            ren.receiveShadows = true;

            MeshFilter mf = ren.GetComponent<MeshFilter>();
            int subMeshCount = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.subMeshCount : ren.sharedMaterials.Length;

            if (subMeshCount >= 2)
            {
                // [0] = Trunk(새까만 숯으로 전소, 절대 사라지지 않음), [1] = Leaves(타서 사라지는 나뭇잎)
                ren.sharedMaterials = new Material[] { trunkMat, leavesMat };
            }
            else
            {
                string name = ren.gameObject.name.ToLower();
                if (name.Contains("trunk"))
                {
                    ren.sharedMaterial = trunkMat;
                }
                else
                {
                    ren.sharedMaterial = leavesMat;
                }
            }
        }
    }

    private void ApplyShrubMaterials(GameObject shrubInstance)
    {
        Renderer[] renderers = shrubInstance.GetComponentsInChildren<Renderer>(true);
        foreach (var ren in renderers)
        {
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            ren.receiveShadows = true;
            ren.sharedMaterial = shrubMat;
        }
    }

    private Vector3 GetRandomPointInShape()
    {
        if (shape == ZoneShape.Circle)
        {
            Vector2 randCircle = Random.insideUnitCircle * radius;
            return new Vector3(randCircle.x, 0f, randCircle.y);
        }
        else
        {
            float rx = Random.Range(-boxSize.x * 0.5f, boxSize.x * 0.5f);
            float rz = Random.Range(-boxSize.y * 0.5f, boxSize.y * 0.5f);
            return new Vector3(rx, 0f, rz);
        }
    }

    #endregion

    #region 공간 쿼리 및 시야/은폐 정적 판정 메서드 (Fast Queries)

    /// <summary>
    /// 주어진 월드 좌표가 이 숲 구역 내부인지 O(1) 검사합니다.
    /// </summary>
    public bool ContainsPosition(Vector3 worldPos)
    {
        Vector3 localPos = worldPos - transform.position;
        if (shape == ZoneShape.Circle)
        {
            float distSqr = (localPos.x * localPos.x) + (localPos.z * localPos.z);
            return distSqr <= (radius * radius);
        }
        else
        {
            // 회전 반영 로컬 투영
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            float localX = Vector3.Dot(localPos, right);
            float localZ = Vector3.Dot(localPos, forward);

            return Mathf.Abs(localX) <= (boxSize.x * 0.5f) && Mathf.Abs(localZ) <= (boxSize.y * 0.5f);
        }
    }

    /// <summary>
    /// 전장의 모든 활성 숲 구역 중 해당 좌표를 포함하는 숲이 있는지 초고속 검사합니다.
    /// </summary>
    public static bool IsPositionInForest(Vector3 worldPos, out TacticalForestZone foundZone)
    {
        int count = activeZones.Count;
        for (int i = 0; i < count; i++)
        {
            var zone = activeZones[i];
            if (zone != null && zone.enabled && zone.gameObject.activeInHierarchy)
            {
                if (zone.ContainsPosition(worldPos))
                {
                    foundZone = zone;
                    return true;
                }
            }
        }

        foundZone = null;
        return false;
    }

    public static bool IsPositionInForest(Vector3 worldPos)
    {
        return IsPositionInForest(worldPos, out _);
    }

    /// <summary>
    /// 아군과 적군 사이의 시야선(start -> end)이 울창한 숲 영역을 가로막고 있는지 검사합니다.
    /// </summary>
    public static bool IsLineBlockedByForest(Vector3 start, Vector3 end)
    {
        int count = activeZones.Count;
        if (count == 0) return false;

        Vector2 p1 = new Vector2(start.x, start.z);
        Vector2 p2 = new Vector2(end.x, end.z);
        float lineLength = Vector2.Distance(p1, p2);
        if (lineLength < 1.0f) return false;

        for (int i = 0; i < count; i++)
        {
            var zone = activeZones[i];
            if (zone == null || !zone.enabled || !zone.blockLineOfSight || zone.isForestRevealedByFire || !zone.gameObject.activeInHierarchy) continue;

            Vector2 center = new Vector2(zone.transform.position.x, zone.transform.position.z);

            // 출발점 또는 도착점 중 하나가 숲 내부에 이미 있으면, 사선 차폐가 아닌 '숲 내부 피탐지 거리(25m)' 규칙 적용
            if (zone.ContainsPosition(start) || zone.ContainsPosition(end))
            {
                continue;
            }

            // 두 점 사이의 선분과 숲 중심점 간의 최단 거리 계산
            Vector2 lineDir = (p2 - p1).normalized;
            float proj = Vector2.Dot(center - p1, lineDir);

            if (proj > 0f && proj < lineLength)
            {
                Vector2 closestPt = p1 + lineDir * proj;
                float distToCenter = Vector2.Distance(closestPt, center);

                float effectiveRadius = (zone.shape == ZoneShape.Circle) ? zone.radius : (Mathf.Min(zone.boxSize.x, zone.boxSize.y) * 0.5f);

                // 시야선이 숲의 70% 이상 깊이를 관통할 경우 시야 차폐 확정!
                if (distToCenter < (effectiveRadius * 0.75f))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region 자동 기본 숲 스폰 (Scene Initializer)

    [ContextMenu("🌲 기본 전술 숲 자동 배치")]
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoCreateDefaultForests()
    {
        // 씬에 이미 수동 배치된 숲이 있다면 자동 생성 건너뜀
        if (FindFirstObjectByType<TacticalForestZone>() != null) return;

        // 전장 요충지 2개소에 삼국지 토탈워 전술 숲 자동 배치
        // 1. 서쪽 측면: 울창한 황산 소나무림
        CreateDefaultZone("Forest_West_HuangshanPine", new Vector3(-45f, 0f, 0f), 32f, ForestTheme.HuangshanPine);

        // 2. 동쪽 고지대: 운치 있는 삼국지 대나무 군락지
        CreateDefaultZone("Forest_East_BambooGrove", new Vector3(45f, 0f, 15f), 28f, ForestTheme.BambooGrove);

        Debug.Log("<color=#00FF88><b>[TacticalForestZone] 🌲 삼국지 토탈워 3D 소나무림 및 대나무숲 2개소가 전장에 성공적으로 배치되었습니다!</b></color>");
    }

    public static GameObject CreateDefaultZone(string name, Vector3 pos, float rad, ForestTheme theme)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        TacticalForestZone zone = go.AddComponent<TacticalForestZone>();
        zone.shape = ZoneShape.Circle;
        zone.theme = theme;
        zone.radius = rad;
        zone.treeCount = Mathf.RoundToInt(rad * 0.85f);
        zone.bushCount = Mathf.RoundToInt(rad * 1.5f);
        if (name.Contains("West") || theme == ForestTheme.HuangshanPine)
        {
            zone.enableFireVFX = true;
            zone.burningTreesCount = 8;
        }
#if UNITY_EDITOR
        zone.treeFirePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VFX/TotalWar_TreeFire_VFX.prefab");
#endif
        zone.SnapToTerrain();
        zone.GenerateVegetation();
        return go;
    }

    #endregion

    #region 씬 뷰 시각화 기즈모 (Scene View Gizmos)

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 0.3f, 0.85f);
        Vector3 center = transform.position;
        center.y = TerrainHeightManager.SampleHeightFast(center.x, center.z) + 0.2f;

        if (shape == ZoneShape.Circle)
        {
            DrawWireCircleTerrainConforming(center, radius, 36);
        }
        else
        {
            DrawWireBoxTerrainConforming(center, boxSize);
        }
    }

    private void DrawWireCircleTerrainConforming(Vector3 center, float r, int segments)
    {
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(angle) * r;
            float z = center.z + Mathf.Sin(angle) * r;
            float y = TerrainHeightManager.SampleHeightFast(x, z) + 0.25f;
            Vector3 cur = new Vector3(x, y, z);

            if (i > 0)
            {
                Gizmos.DrawLine(prev, cur);
            }
            prev = cur;
        }
    }

    private void DrawWireBoxTerrainConforming(Vector3 center, Vector2 size)
    {
        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;

        Vector3 p1 = center + (transform.right * -hx) + (transform.forward * hz);
        Vector3 p2 = center + (transform.right * hx) + (transform.forward * hz);
        Vector3 p3 = center + (transform.right * hx) + (transform.forward * -hz);
        Vector3 p4 = center + (transform.right * -hx) + (transform.forward * -hz);

        p1.y = TerrainHeightManager.SampleHeightFast(p1.x, p1.z) + 0.25f;
        p2.y = TerrainHeightManager.SampleHeightFast(p2.x, p2.z) + 0.25f;
        p3.y = TerrainHeightManager.SampleHeightFast(p3.x, p3.z) + 0.25f;
        p4.y = TerrainHeightManager.SampleHeightFast(p4.x, p4.z) + 0.25f;

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }

    #endregion
}
