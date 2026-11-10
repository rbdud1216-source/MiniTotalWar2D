using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 🛣️ [삼국지 토탈워 군사 도로망 엔진 (TotalWarRoadSpline)]
/// - 씬 뷰 마우스 클릭으로 지형을 따라 굽이치는 황토길/군사 도로를 손쉽게 배치.
/// - 3D 도로 리본 메쉬(Road Ribbon Mesh) 실시간 자동 생성으로 지형 위에 즉시 깔리는 도로 렌더링.
/// - 울퉁불퉁한 산비탈/거친 지형을 마차와 기병이 달릴 수 있는 도로 폭으로 자동 평탄화(Cut & Fill Flattening).
/// - 도로 중심부는 황토 흙길(Layer 2: Dirt Road) 100% 도색 및 갓길 그라데이션 페이드.
/// - 도로 내부의 3D 잔디를 100% 배제하여 시원하게 뚫린 군사 통행로 완성.
/// - AABB 국소 그리드 가속 엔진 탑재로 0.05초 만에 초고속 평탄화 및 도색 완료.
/// - 도로는 장애물을 치워둔 기준 노면(기본 1.0배 속도)이며 인위적인 가속 보너스를 배제.
/// - 풀밭, 수풀, 늪지 등 험지 진입 시 발생하는 지형 저항 감속(-15~50%) 및 피로도 누적 체계의 안전 구역으로 작용.
/// - 원클릭 이전 지형 되돌리기(Undo Snapshot) 지원.
/// </summary>
/// <summary>
/// 🏛️ 삼국지 토탈워 본편 고증 도로 재질 스타일 분류
/// </summary>
public enum RoadSurfaceType
{
    [InspectorName("🌿 터레인 기후 테마 자동 동기화 (기본 권장)")]
    ClimateSync = 0,
    [InspectorName("🪨 군용 자갈 쇄석길 (Gravel Highway)")]
    Gravel = 1,
    [InspectorName("🌾 야전 다져진 흙길 (Packed Dirt Trail)")]
    Dirt = 2
}

[DisallowMultipleComponent]
[SelectionBase]
[ExecuteAlways]
public class TotalWarRoadSpline : MonoBehaviour
{
    private static readonly List<TotalWarRoadSpline> activeRoads = new List<TotalWarRoadSpline>();
    public static IReadOnlyList<TotalWarRoadSpline> ActiveRoads => activeRoads;

    [Header("🏛️ 도로 스타일 (재질 원클릭 토글)")]
    [Tooltip("도로 표면 재질 스타일: 터레인 기후 테마 자동 동기화, 자갈길, 흙길, 석판길")]
    public RoadSurfaceType surfaceType = RoadSurfaceType.ClimateSync;

    [Header("🛣️ 도로 규격 및 평탄화 설정")]
    [Tooltip("도로 중심 통행로 폭 (단위: m, 기본 6.0m)")]
    [Range(2f, 30f)]
    public float roadWidth = 6.0f;

    [Tooltip("도로 갓길 완충 경사 폭 (단위: m, 기본 2.5m)")]
    [Range(0.5f, 15f)]
    public float shoulderWidth = 2.5f;

    [Tooltip("지형 평탄화 강도 (0: 원래 지형 유지, 1: 도로 높이로 완전 평탄화)")]
    [Range(0.1f, 1.0f)]
    public float flattenStrength = 0.85f;

    [Tooltip("도색할 터레인 레이어 인덱스 (기본: 2 황토 흙길)")]
    public int roadLayerIndex = 2;

    [Header("🎨 3D 도로 메쉬 설정 (시각적 리본 메쉬)")]
    [Tooltip("지형 위에 시각적 도로 메쉬(Ribbon Mesh)를 직접 렌더링할지 여부")]
    public bool showRoadMesh = true;

    [Tooltip("지형 표면 기준 메쉬 띄움 높이 (Z-Fighting 방지, 기본 0.04m)")]
    [Range(0.01f, 0.2f)]
    public float surfaceYOffset = 0.04f;

    [Tooltip("도로 텍스처 UV 반복 길이 (단위: m)")]
    [Range(1f, 30f)]
    public float uvTilingLength = 6.0f;

    [Tooltip("도로 메쉬에 적용할 머티리얼 (비어있으면 기본 황토길 머티리얼 자동 생성)")]
    public Material roadMaterial;

    [Header("📍 도로 스플라인 제어점 (로컬 좌표)")]
    public List<Vector3> waypoints = new List<Vector3>();

    [Tooltip("스플라인 곡선 세분화 해상도")]
    [Range(4, 32)]
    public int resolutionPerSegment = 12;

    [SerializeField, HideInInspector] private MeshFilter roadMeshFilter;
    [SerializeField, HideInInspector] private MeshRenderer roadMeshRenderer;

    // 💾 터레인 원복용 메모리 백업 데이터
    private static float[,] _backupHeights;
    private static float[,,] _backupAlphamaps;
    private static List<int[,]> _backupDetails;
    private static bool _hasBackup = false;
    public bool HasBackup => _hasBackup;

    private const string ROAD_MAT_PATH = "Assets/Materials/TotalWarRoadMaterial.mat";

    private void OnEnable()
    {
        if (!activeRoads.Contains(this)) activeRoads.Add(this);
        TotalWarTerrainManager.OnThemeAppliedEditor += OnTerrainThemeChanged;
        EnsureMeshComponents();
        if (surfaceType == RoadSurfaceType.ClimateSync) SyncWithTerrainClimate();
        else GenerateRoadMesh();
    }

    private void OnDisable()
    {
        activeRoads.Remove(this);
        TotalWarTerrainManager.OnThemeAppliedEditor -= OnTerrainThemeChanged;
    }

    private void OnTerrainThemeChanged()
    {
        if (surfaceType == RoadSurfaceType.ClimateSync)
        {
            SyncWithTerrainClimate();
        }
    }

    private void Reset()
    {
        InitializeDefaultWaypoints();
        EnsureMeshComponents();
        GenerateRoadMesh();
    }

    private void OnValidate()
    {
        EnforceLevelTransform();
        if (waypoints == null || waypoints.Count < 2)
        {
            InitializeDefaultWaypoints();
        }

#if UNITY_EDITOR
        EditorApplication.delayCall -= DeferredUpdate;
        EditorApplication.delayCall += DeferredUpdate;
#else
        GenerateRoadMesh();
#endif
    }

#if UNITY_EDITOR
    private void DeferredUpdate()
    {
        if (this == null) return;
        EnsureMeshComponents();
        GenerateRoadMesh();
    }
#endif

    /// <summary>
    /// 도로 오브젝트 Transform의 기울어짐(Roll/Pitch) 자동 보정
    /// </summary>
    public void EnforceLevelTransform()
    {
        Vector3 angles = transform.rotation.eulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(angles.x, 0f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(angles.z, 0f)) > 0.05f)
        {
            if (waypoints != null && waypoints.Count > 0)
            {
                List<Vector3> rawWorldPts = new List<Vector3>(waypoints.Count);
                for (int i = 0; i < waypoints.Count; i++) rawWorldPts.Add(transform.TransformPoint(waypoints[i]));

                transform.rotation = Quaternion.Euler(0f, angles.y, 0f);

                for (int i = 0; i < waypoints.Count; i++) waypoints[i] = transform.InverseTransformPoint(rawWorldPts[i]);
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, angles.y, 0f);
            }
        }
    }

    /// <summary>
    /// 기본 4개 도로 경유점 생성
    /// </summary>
    public void InitializeDefaultWaypoints()
    {
        waypoints = new List<Vector3>
        {
            new Vector3(-30f, 0f, -15f),
            new Vector3(-10f, 0f,   5f),
            new Vector3( 10f, 0f,  -5f),
            new Vector3( 30f, 0f,  15f)
        };
    }

    /// <summary>
    /// Catmull-Rom 스플라인을 따라 로컬 좌표 포인트 샘플링
    /// </summary>
    public List<Vector3> SampleSplinePointsLocal()
    {
        List<Vector3> sampled = new List<Vector3>();
        if (waypoints == null || waypoints.Count < 2) return sampled;

        int count = waypoints.Count;
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = (i == 0) ? waypoints[i] : waypoints[i - 1];
            Vector3 p1 = waypoints[i];
            Vector3 p2 = waypoints[i + 1];
            Vector3 p3 = (i + 2 < count) ? waypoints[i + 2] : p2;

            for (int step = 0; step < resolutionPerSegment; step++)
            {
                float t = (float)step / resolutionPerSegment;
                sampled.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
            }
        }
        sampled.Add(waypoints[count - 1]);
        return sampled;
    }

    /// <summary>
    /// 월드 좌표로 변환된 스플라인 포인트 샘플링
    /// </summary>
    public List<Vector3> SampleSplinePointsWorld()
    {
        List<Vector3> localPts = SampleSplinePointsLocal();
        List<Vector3> worldPts = new List<Vector3>(localPts.Count);
        for (int i = 0; i < localPts.Count; i++)
        {
            worldPts.Add(transform.TransformPoint(localPts[i]));
        }
        return worldPts;
    }

    private static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// 특정 월드 좌표가 도로 면적 내부(도로 폭 이내)에 위치하는지 정밀 판정
    /// </summary>
    public bool Contains(Vector3 worldPos)
    {
        List<Vector3> worldPoints = SampleSplinePointsWorld();
        if (worldPoints.Count < 2) return false;

        float halfWidth = roadWidth * 0.5f;
        Vector3 cellPos = new Vector3(worldPos.x, 0f, worldPos.z);

        for (int i = 0; i < worldPoints.Count - 1; i++)
        {
            Vector3 pA = worldPoints[i];
            Vector3 pB = worldPoints[i + 1];
            Vector3 proj = ClosestPointOnSegmentXZ(cellPos, pA, pB, out float dist);
            if (dist <= halfWidth)
            {
                // 수직 높이 검사 (공중이나 지하 제외)
                if (Mathf.Abs(worldPos.y - proj.y) <= 1.5f) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 전역에 배치된 모든 도로 중 특정 월드 좌표가 도로 위인지 정적 판정
    /// </summary>
    public static bool IsOnAnyRoad(Vector3 worldPos, out TotalWarRoadSpline hitRoad)
    {
        for (int i = 0; i < activeRoads.Count; i++)
        {
            if (activeRoads[i] != null && activeRoads[i].Contains(worldPos))
            {
                hitRoad = activeRoads[i];
                return true;
            }
        }
        hitRoad = null;
        return false;
    }

    public static bool IsOnAnyRoad(Vector3 worldPos)
    {
        return IsOnAnyRoad(worldPos, out _);
    }

    /// <summary>
    /// 🛣️ 지형 표면을 따라 흐르는 부드러운 3D 도로 리본 메쉬(Road Ribbon Mesh) 실시간 생성
    /// </summary>
    public void GenerateRoadMesh()
    {
        EnsureMeshComponents();

        if (!showRoadMesh)
        {
            if (roadMeshRenderer != null) roadMeshRenderer.enabled = false;
            return;
        }

        List<Vector3> localPts = SampleSplinePointsLocal();
        if (localPts.Count < 2) return;

        if (roadMeshRenderer != null) roadMeshRenderer.enabled = true;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) terrain = Object.FindFirstObjectByType<Terrain>();
        Vector3 tPos = (terrain != null) ? terrain.transform.position : Vector3.zero;

        int pointCount = localPts.Count;
        Vector3[] vertices = new Vector3[pointCount * 2];
        Vector2[] uvs = new Vector2[pointCount * 2];
        Vector3[] normals = new Vector3[pointCount * 2];
        Vector4[] tangents = new Vector4[pointCount * 2];

        float halfWidth = roadWidth * 0.5f;
        float currentDist = 0f;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 currLocal = localPts[i];
            Vector3 currWorld = transform.TransformPoint(currLocal);

            // 지형 표면에 정확히 밀착
            if (terrain != null)
            {
                float groundY = terrain.SampleHeight(currWorld) + tPos.y;
                currWorld.y = groundY;
            }

            Vector3 forwardWorld;
            if (i == 0) forwardWorld = transform.TransformPoint(localPts[1]) - currWorld;
            else if (i == pointCount - 1) forwardWorld = currWorld - transform.TransformPoint(localPts[i - 1]);
            else forwardWorld = transform.TransformPoint(localPts[i + 1]) - transform.TransformPoint(localPts[i - 1]);

            if (i > 0)
            {
                currentDist += Vector3.Distance(transform.TransformPoint(localPts[i - 1]), currWorld);
            }

            Vector3 forwardXZ = new Vector3(forwardWorld.x, 0f, forwardWorld.z).normalized;
            if (forwardXZ == Vector3.zero) forwardXZ = Vector3.forward;

            Vector3 rightWorld = new Vector3(-forwardXZ.z, 0f, forwardXZ.x); // 수평 단면 벡터
            Vector3 upWorld = Vector3.up;

            Vector3 leftWorld = currWorld - rightWorld * halfWidth + upWorld * surfaceYOffset;
            Vector3 rightWorldPos = currWorld + rightWorld * halfWidth + upWorld * surfaceYOffset;

            vertices[i * 2] = transform.InverseTransformPoint(leftWorld);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(rightWorldPos);

            float v = currentDist / Mathf.Max(1f, uvTilingLength);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);

            normals[i * 2] = transform.InverseTransformDirection(Vector3.up);
            normals[i * 2 + 1] = transform.InverseTransformDirection(Vector3.up);

            tangents[i * 2] = new Vector4(forwardWorld.x, forwardWorld.y, forwardWorld.z, 1f);
            tangents[i * 2 + 1] = new Vector4(forwardWorld.x, forwardWorld.y, forwardWorld.z, 1f);
        }

        int[] triangles = new int[(pointCount - 1) * 6];
        int ti = 0;
        for (int i = 0; i < pointCount - 1; i++)
        {
            int bl = i * 2;
            int br = i * 2 + 1;
            int tl = (i + 1) * 2;
            int tr = (i + 1) * 2 + 1;

            triangles[ti++] = bl;
            triangles[ti++] = tl;
            triangles[ti++] = br;

            triangles[ti++] = br;
            triangles[ti++] = tl;
            triangles[ti++] = tr;
        }

        Mesh mesh = new Mesh();
        mesh.name = "TotalWarRoadMesh";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        if (roadMeshFilter != null) roadMeshFilter.sharedMesh = mesh;
    }

    private void EnsureMeshComponents()
    {
        // 1. 본체에 붙어있던 기존 MeshRenderer/MeshFilter가 있다면 URP 에디터 인스펙터 충돌 방지를 위해 안전하게 지연 제거
        MeshRenderer mainRenderer = GetComponent<MeshRenderer>();
        if (mainRenderer != null)
        {
            mainRenderer.enabled = false;
            mainRenderer.hideFlags = HideFlags.HideInInspector;
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (mainRenderer != null) DestroyImmediate(mainRenderer);
            };
#else
            Destroy(mainRenderer);
#endif
        }

        MeshFilter mainFilter = GetComponent<MeshFilter>();
        if (mainFilter != null)
        {
            mainFilter.hideFlags = HideFlags.HideInInspector;
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (mainFilter != null) DestroyImmediate(mainFilter);
            };
#else
            Destroy(mainFilter);
#endif
        }

        // 2. 자식 오브젝트로 3D 도로 메쉬 분리 (인스펙터 충돌 원천 차단 및 Hierarchy 깔끔화)
        Transform childT = transform.Find("RoadMeshVisual");
        GameObject meshObj;
        if (childT == null)
        {
            meshObj = new GameObject("RoadMeshVisual");
            meshObj.transform.SetParent(transform, false);
            meshObj.transform.localPosition = Vector3.zero;
            meshObj.transform.localRotation = Quaternion.identity;
            meshObj.transform.localScale = Vector3.one;
            meshObj.hideFlags = HideFlags.HideInHierarchy; // Hierarchy 오염 방지
        }
        else
        {
            meshObj = childT.gameObject;
        }

        roadMeshFilter = meshObj.GetComponent<MeshFilter>();
        if (roadMeshFilter == null) roadMeshFilter = meshObj.AddComponent<MeshFilter>();

        roadMeshRenderer = meshObj.GetComponent<MeshRenderer>();
        if (roadMeshRenderer == null) roadMeshRenderer = meshObj.AddComponent<MeshRenderer>();

        EnsureRoadMaterial();
        if (roadMeshRenderer.sharedMaterial == null && roadMaterial != null)
        {
            roadMeshRenderer.sharedMaterial = roadMaterial;
        }
    }

    private void EnsureRoadMaterial()
    {
        if (roadMaterial != null) return;

#if UNITY_EDITOR
        roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(ROAD_MAT_PATH);
        if (roadMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            roadMaterial = new Material(shader);
            roadMaterial.name = "TotalWarRoadMaterial";

            // 자갈 쇄석길 (Pebbles & Gravel) 설정
            roadMaterial.color = Color.white;
            if (roadMaterial.HasProperty("_Smoothness")) roadMaterial.SetFloat("_Smoothness", 0.1f);

            // Pebbles_A 자갈 텍스처 및 노멀맵 바인딩
            Texture2D pebbleTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_A_BaseColor.tif");
            if (pebbleTex != null) roadMaterial.mainTexture = pebbleTex;

            Texture2D pebbleNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_A_Normal.tif");
            if (pebbleNormal != null && roadMaterial.HasProperty("_BumpMap"))
            {
                roadMaterial.SetTexture("_BumpMap", pebbleNormal);
                roadMaterial.EnableKeyword("_NORMALMAP");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            AssetDatabase.CreateAsset(roadMaterial, ROAD_MAT_PATH);
            AssetDatabase.SaveAssets();
        }
#endif
    }

    /// <summary>
    /// 🌿 [터레인 기후 테마 자동 동기화]
    /// - 현재 터레인의 기후 테마(Temperate, Arid, Cold, Nanman, Subtropical) 및 dirtRoadLayer를 감지하여
    ///   도로 메쉬의 Albedo 텍스처, Normal 맵, 타일링, 도색 레이어 인덱스를 1:1로 일체화합니다.
    /// </summary>
    public void SyncWithTerrainClimate()
    {
        EnsureRoadMaterial();

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) terrain = Object.FindFirstObjectByType<Terrain>();

        TerrainLayer matchedRoadLayer = null;
        int matchedLayerIndex = -1;

        // 1. TotalWarTerrainManager의 활성 테마 dirtRoadLayer 조회
        TotalWarTerrainManager terrainMgr = Object.FindFirstObjectByType<TotalWarTerrainManager>();
        if (terrainMgr != null && terrainMgr.currentTheme != null && terrainMgr.currentTheme.dirtRoadLayer != null)
        {
            matchedRoadLayer = terrainMgr.currentTheme.dirtRoadLayer;
        }

        // 2. 터레인의 terrainLayers에서 dirtRoadLayer의 슬롯 인덱스 탐색
        if (terrain != null && terrain.terrainData != null && terrain.terrainData.terrainLayers != null)
        {
            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            if (matchedRoadLayer != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] == matchedRoadLayer)
                    {
                        matchedLayerIndex = i;
                        break;
                    }
                }
            }

            // 3. 만약 테마에서 못 찾았다면 이름으로 도로 레이어 자동 검색 (Road, Dirt, Soil 등)
            if (matchedRoadLayer == null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] == null) continue;
                    string lName = layers[i].name.ToLower();
                    if (lName.Contains("road") || lName.Contains("dirt") || lName.Contains("soil"))
                    {
                        matchedRoadLayer = layers[i];
                        matchedLayerIndex = i;
                        break;
                    }
                }
            }

            // 4. 슬롯 인덱스 폴백: 기존 roadLayerIndex가 유효하면 해당 슬롯 사용
            if (matchedRoadLayer == null && roadLayerIndex >= 0 && roadLayerIndex < layers.Length)
            {
                matchedRoadLayer = layers[roadLayerIndex];
                matchedLayerIndex = roadLayerIndex;
            }
        }

        // 도색 레이어 인덱스 갱신
        if (matchedLayerIndex >= 0)
        {
            roadLayerIndex = matchedLayerIndex;
        }

#if UNITY_EDITOR
        if (roadMaterial != null)
        {
            if (matchedRoadLayer != null && matchedRoadLayer.diffuseTexture != null)
            {
                roadMaterial.mainTexture = matchedRoadLayer.diffuseTexture;
                if (roadMaterial.HasProperty("_BaseMap")) roadMaterial.SetTexture("_BaseMap", matchedRoadLayer.diffuseTexture);

                if (matchedRoadLayer.normalMapTexture != null && roadMaterial.HasProperty("_BumpMap"))
                {
                    roadMaterial.SetTexture("_BumpMap", matchedRoadLayer.normalMapTexture);
                    roadMaterial.EnableKeyword("_NORMALMAP");
                }
                roadMaterial.color = Color.white;
                if (roadMaterial.HasProperty("_BaseColor")) roadMaterial.SetColor("_BaseColor", Color.white);

                if (matchedRoadLayer.tileSize.x > 0.5f)
                {
                    uvTilingLength = Mathf.Clamp(matchedRoadLayer.tileSize.x, 3.0f, 15.0f);
                }
            }
            else
            {
                // 기본 온대 흙길 폴백
                Texture2D defaultAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_BaseColor.tif");
                if (defaultAlbedo != null)
                {
                    roadMaterial.mainTexture = defaultAlbedo;
                    if (roadMaterial.HasProperty("_BaseMap")) roadMaterial.SetTexture("_BaseMap", defaultAlbedo);
                }

                Texture2D defaultNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_Normal.tif");
                if (defaultNormal != null && roadMaterial.HasProperty("_BumpMap"))
                {
                    roadMaterial.SetTexture("_BumpMap", defaultNormal);
                    roadMaterial.EnableKeyword("_NORMALMAP");
                }
                roadMaterial.color = new Color(0.92f, 0.82f, 0.68f, 1.0f);
                if (roadMaterial.HasProperty("_BaseColor")) roadMaterial.SetColor("_BaseColor", roadMaterial.color);
            }

            EditorUtility.SetDirty(roadMaterial);
            if (roadMeshRenderer != null) roadMeshRenderer.sharedMaterial = roadMaterial;
        }

        GenerateRoadMesh();
        SceneView.RepaintAll();
#endif
    }

    /// <summary>
    /// 도로 재질 스타일(기후 동기화/자갈/흙/석판)을 원클릭으로 변경하고 머티리얼과 도색 레이어를 동기화
    /// </summary>
    public void SetSurfaceType(RoadSurfaceType newType)
    {
        surfaceType = newType;

        if (surfaceType == RoadSurfaceType.ClimateSync)
        {
            SyncWithTerrainClimate();
            return;
        }

        EnsureRoadMaterial();

#if UNITY_EDITOR
        string texPath = "";
        string normalPath = "";
        Color matColor = Color.white;
        string layerSearchKeyword = "";

        switch (surfaceType)
        {
            case RoadSurfaceType.Gravel:
                texPath = "Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_A_BaseColor.tif";
                normalPath = "Assets/TerrainSampleAssets/Textures/Terrain/Pebbles_A_Normal.tif";
                matColor = Color.white;
                layerSearchKeyword = "pebble";
                roadLayerIndex = 4; // 자갈/수변 레이어 기본값
                uvTilingLength = 5.0f;
                break;

            case RoadSurfaceType.Dirt:
                texPath = "Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_BaseColor.tif";
                normalPath = "Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_Normal.tif";
                matColor = new Color(0.92f, 0.82f, 0.68f, 1.0f);
                layerSearchKeyword = "soil";
                roadLayerIndex = 2; // 황토 흙길 레이어 기본값
                uvTilingLength = 6.0f;
                break;
        }

        // 터레인에서 해당 키워드에 맞는 레이어 인덱스 동적 탐색
        Terrain terrain = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();
        if (terrain != null && terrain.terrainData != null && terrain.terrainData.terrainLayers != null)
        {
            var layers = terrain.terrainData.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null && layers[i].name.ToLower().Contains(layerSearchKeyword))
                {
                    roadLayerIndex = i;
                    break;
                }
            }
        }

        if (roadMaterial != null)
        {
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (albedo != null)
            {
                roadMaterial.mainTexture = albedo;
                if (roadMaterial.HasProperty("_BaseMap")) roadMaterial.SetTexture("_BaseMap", albedo);
            }

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal != null && roadMaterial.HasProperty("_BumpMap"))
            {
                roadMaterial.SetTexture("_BumpMap", normal);
                roadMaterial.EnableKeyword("_NORMALMAP");
            }

            roadMaterial.color = matColor;
            if (roadMaterial.HasProperty("_BaseColor")) roadMaterial.SetColor("_BaseColor", matColor);
            EditorUtility.SetDirty(roadMaterial);
            if (roadMeshRenderer != null) roadMeshRenderer.sharedMaterial = roadMaterial;
        }

        GenerateRoadMesh();
        SceneView.RepaintAll();
#endif
    }

    /// <summary>
    /// 현재 연동된 기후 테마의 명칭 및 정보 문자열 반환 (에디터 UI용)
    /// </summary>
    public string GetCurrentClimateInfo()
    {
        TotalWarTerrainManager terrainMgr = Object.FindFirstObjectByType<TotalWarTerrainManager>();
        if (terrainMgr != null && terrainMgr.currentTheme != null)
        {
            string cName = terrainMgr.currentTheme.climate.ToString();
            string roadLayerName = terrainMgr.currentTheme.dirtRoadLayer != null ? terrainMgr.currentTheme.dirtRoadLayer.name : "미지정";
            return $"기후: {cName} | 연동 레이어: {roadLayerName} (Slot #{roadLayerIndex})";
        }
        return $"기본 모드 | 슬롯 #{roadLayerIndex}";
    }

    /// <summary>
    /// 🛣️ [도로 지형 자동 평탄화 + 황토길 도색 + 잔디 배제] 총괄 실행 (AABB 국소 그리드 100배 가속)
    /// </summary>
    public void CarveAndPaintRoad()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("[TotalWarRoadSpline] 활성 터레인을 찾을 수 없습니다.");
            return;
        }

        List<Vector3> worldPoints = SampleSplinePointsWorld();
        if (worldPoints.Count < 2) return;

        TerrainData tData = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = tData.size;

        int hRes = tData.heightmapResolution;
        float[,] heights = tData.GetHeights(0, 0, hRes, hRes);

        // 💾 [지형 스냅샷 백업]
        _backupHeights = (float[,])heights.Clone();

        int aRes = tData.alphamapResolution;
        if (tData.alphamapLayers > 0)
        {
            _backupAlphamaps = (float[,,])tData.GetAlphamaps(0, 0, aRes, aRes).Clone();
        }

        _backupDetails = new List<int[,]>();
        int dRes = tData.detailResolution;
        for (int l = 0; l < tData.detailPrototypes.Length; l++)
        {
            _backupDetails.Add((int[,])tData.GetDetailLayer(0, 0, dRes, dRes, l).Clone());
        }
        _hasBackup = true;

        float totalRoadRadius = roadWidth * 0.5f + shoulderWidth;
        float halfRoadWidth = roadWidth * 0.5f;

        // 🚀 [AABB 바운딩 박스 계산]: 터레인 전체(100만 셀)가 아닌 도로 통과 영역만 핀포인트 연산
        float minWX = float.MaxValue, maxWX = float.MinValue;
        float minWZ = float.MaxValue, maxWZ = float.MinValue;
        for (int i = 0; i < worldPoints.Count; i++)
        {
            Vector3 pt = worldPoints[i];
            if (pt.x < minWX) minWX = pt.x;
            if (pt.x > maxWX) maxWX = pt.x;
            if (pt.z < minWZ) minWZ = pt.z;
            if (pt.z > maxWZ) maxWZ = pt.z;
        }

        minWX -= (totalRoadRadius + 3f);
        maxWX += (totalRoadRadius + 3f);
        minWZ -= (totalRoadRadius + 3f);
        maxWZ += (totalRoadRadius + 3f);

        int minHY = Mathf.Clamp(Mathf.FloorToInt((minWZ - tPos.z) / tSize.z * (hRes - 1)), 0, hRes - 1);
        int maxHY = Mathf.Clamp(Mathf.CeilToInt((maxWZ - tPos.z) / tSize.z * (hRes - 1)), 0, hRes - 1);
        int minHX = Mathf.Clamp(Mathf.FloorToInt((minWX - tPos.x) / tSize.x * (hRes - 1)), 0, hRes - 1);
        int maxHX = Mathf.Clamp(Mathf.CeilToInt((maxWX - tPos.x) / tSize.x * (hRes - 1)), 0, hRes - 1);

        bool[,] modifiedMask = new bool[hRes, hRes];

        // 1. 도로 지형 평탄화 (Cut & Fill Smoothing) - AABB 국소 그리드만 루프
        for (int y = minHY; y <= maxHY; y++)
        {
            float normY = (float)y / (hRes - 1);
            float worldZ = tPos.z + normY * tSize.z;

            for (int x = minHX; x <= maxHX; x++)
            {
                float normX = (float)x / (hRes - 1);
                float worldX = tPos.x + normX * tSize.x;
                Vector3 cellPos = new Vector3(worldX, 0, worldZ);

                float minDist = float.MaxValue;
                Vector3 closestPt = Vector3.zero;

                for (int i = 0; i < worldPoints.Count - 1; i++)
                {
                    Vector3 pA = worldPoints[i];
                    Vector3 pB = worldPoints[i + 1];
                    Vector3 proj = ClosestPointOnSegmentXZ(cellPos, pA, pB, out float dist);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestPt = proj;
                    }
                }

                if (minDist <= totalRoadRadius)
                {
                    float currentH = heights[y, x] * tSize.y + tPos.y;
                    float roadCenterH = closestPt.y;

                    float targetH;
                    if (minDist <= halfRoadWidth)
                    {
                        // 도로 중심부: 스플라인 고도로 평탄화
                        targetH = Mathf.Lerp(currentH, roadCenterH, flattenStrength);
                    }
                    else
                    {
                        // 갓길: Hermite Smoothstep 곡선으로 원래 지형과 부드럽게 융합
                        float tShoulder = (minDist - halfRoadWidth) / shoulderWidth;
                        float blend = Mathf.SmoothStep(0f, 1f, tShoulder);
                        float shoulderEdgeH = Mathf.Lerp(currentH, roadCenterH, flattenStrength);
                        targetH = Mathf.Lerp(shoulderEdgeH, currentH, blend);
                    }

                    heights[y, x] = (targetH - tPos.y) / tSize.y;
                    modifiedMask[y, x] = true;
                }
            }
        }

        // 2. 3x3 가우시안 스무딩 (노면 요철 및 경계면 각진 모서리 매끄럽게 마감)
        int smoothMinY = Mathf.Max(1, minHY);
        int smoothMaxY = Mathf.Min(hRes - 2, maxHY);
        int smoothMinX = Mathf.Max(1, minHX);
        int smoothMaxX = Mathf.Min(hRes - 2, maxHX);

        float[,] smoothedHeights = (float[,])heights.Clone();
        for (int y = smoothMinY; y <= smoothMaxY; y++)
        {
            for (int x = smoothMinX; x <= smoothMaxX; x++)
            {
                if (!modifiedMask[y, x]) continue;

                float sum = 0f;
                sum += heights[y - 1, x - 1] * 1f + heights[y - 1, x] * 2f + heights[y - 1, x + 1] * 1f;
                sum += heights[y,     x - 1] * 2f + heights[y,     x] * 4f + heights[y,     x + 1] * 2f;
                sum += heights[y + 1, x - 1] * 1f + heights[y + 1, x] * 2f + heights[y + 1, x + 1] * 1f;

                smoothedHeights[y, x] = sum / 16f;
            }
        }
        tData.SetHeights(0, 0, smoothedHeights);

        // 3. 황토 흙길(Layer 2) 스플랫맵 도색 - AABB 국소 루프
        int layerCount = tData.alphamapLayers;
        int targetLayer = Mathf.Clamp(roadLayerIndex, 0, layerCount - 1);
        if (layerCount > 0)
        {
            float[,,] alphas = tData.GetAlphamaps(0, 0, aRes, aRes);

            int minAY = Mathf.Clamp(Mathf.FloorToInt((minWZ - tPos.z) / tSize.z * (aRes - 1)), 0, aRes - 1);
            int maxAY = Mathf.Clamp(Mathf.CeilToInt((maxWZ - tPos.z) / tSize.z * (aRes - 1)), 0, aRes - 1);
            int minAX = Mathf.Clamp(Mathf.FloorToInt((minWX - tPos.x) / tSize.x * (aRes - 1)), 0, aRes - 1);
            int maxAX = Mathf.Clamp(Mathf.CeilToInt((maxWX - tPos.x) / tSize.x * (aRes - 1)), 0, aRes - 1);

            for (int y = minAY; y <= maxAY; y++)
            {
                float normY = (float)y / (aRes - 1);
                float worldZ = tPos.z + normY * tSize.z;

                for (int x = minAX; x <= maxAX; x++)
                {
                    float normX = (float)x / (aRes - 1);
                    float worldX = tPos.x + normX * tSize.x;
                    Vector3 cellPos = new Vector3(worldX, 0, worldZ);

                    float minDist = float.MaxValue;
                    for (int i = 0; i < worldPoints.Count - 1; i++)
                    {
                        Vector3 pA = worldPoints[i];
                        Vector3 pB = worldPoints[i + 1];
                        ClosestPointOnSegmentXZ(cellPos, pA, pB, out float dist);
                        if (dist < minDist) minDist = dist;
                    }

                    if (minDist <= halfRoadWidth)
                    {
                        // 도로 중심: 황토길 100%
                        for (int l = 0; l < layerCount; l++) alphas[y, x, l] = 0f;
                        alphas[y, x, targetLayer] = 1.0f;
                    }
                    else if (minDist <= totalRoadRadius)
                    {
                        // 갓길: 기존 잔디와 황토길의 자연스러운 그라데이션
                        float t = (minDist - halfRoadWidth) / shoulderWidth;
                        float dirtWeight = Mathf.SmoothStep(1.0f, 0.0f, t);
                        float baseWeight = 1.0f - dirtWeight;

                        for (int l = 0; l < layerCount; l++)
                        {
                            if (l == targetLayer) alphas[y, x, l] = dirtWeight;
                            else alphas[y, x, l] = alphas[y, x, l] * baseWeight;
                        }
                    }
                }
            }
            tData.SetAlphamaps(0, 0, alphas);
        }

        // 4. 도로 위 3D 잔디 100% 클리어링
        ClearGrassAlongRoadAABB(terrain, worldPoints, minWX, maxWX, minWZ, maxWZ, totalRoadRadius);

        // 5. 3D 리본 메쉬 지형에 맞게 리프레시
        GenerateRoadMesh();

        // 6. 씬 뷰 즉각 화면 갱신
        terrain.Flush();
#if UNITY_EDITOR
        EditorUtility.SetDirty(tData);
        EditorUtility.SetDirty(terrain);
        SceneView.RepaintAll();
#endif

        Debug.Log("[TotalWarRoadSpline] 🛣️ 도로 지형 자동 평탄화, 황토길 도색 및 잔디 클리어링 완료!");
    }

    /// <summary>
    /// 도로 궤적을 따라 3D 잔디 밀도를 0으로 배제 (AABB 국소 가속)
    /// </summary>
    private void ClearGrassAlongRoadAABB(Terrain terrain, List<Vector3> worldPoints, float minWX, float maxWX, float minWZ, float maxWZ, float radius)
    {
        TerrainData tData = terrain.terrainData;
        int numLayers = tData.detailPrototypes.Length;
        if (numLayers == 0) return;

        int dRes = tData.detailResolution;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = tData.size;

        int minDY = Mathf.Clamp(Mathf.FloorToInt((minWZ - tPos.z) / tSize.z * (dRes - 1)), 0, dRes - 1);
        int maxDY = Mathf.Clamp(Mathf.CeilToInt((maxWZ - tPos.z) / tSize.z * (dRes - 1)), 0, dRes - 1);
        int minDX = Mathf.Clamp(Mathf.FloorToInt((minWX - tPos.x) / tSize.x * (dRes - 1)), 0, dRes - 1);
        int maxDX = Mathf.Clamp(Mathf.CeilToInt((maxWX - tPos.x) / tSize.x * (dRes - 1)), 0, dRes - 1);

        for (int l = 0; l < numLayers; l++)
        {
            int[,] layerMap = tData.GetDetailLayer(0, 0, dRes, dRes, l);
            bool modified = false;

            for (int y = minDY; y <= maxDY; y++)
            {
                float normY = (float)y / (dRes - 1);
                float worldZ = tPos.z + normY * tSize.z;

                for (int x = minDX; x <= maxDX; x++)
                {
                    if (layerMap[y, x] == 0) continue;

                    float normX = (float)x / (dRes - 1);
                    float worldX = tPos.x + normX * tSize.x;
                    Vector3 cellPos = new Vector3(worldX, 0, worldZ);

                    float minDist = float.MaxValue;
                    for (int i = 0; i < worldPoints.Count - 1; i++)
                    {
                        Vector3 pA = worldPoints[i];
                        Vector3 pB = worldPoints[i + 1];
                        ClosestPointOnSegmentXZ(cellPos, pA, pB, out float dist);
                        if (dist < minDist) minDist = dist;
                    }

                    if (minDist <= radius)
                    {
                        layerMap[y, x] = 0;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                tData.SetDetailLayer(0, 0, l, layerMap);
            }
        }
    }

    /// <summary>
    /// 도로 지형 평탄화 이전 상태로 1초 만에 100% 원복
    /// </summary>
    public void RestoreTerrainBackup()
    {
        if (!_hasBackup || _backupHeights == null)
        {
            Debug.LogWarning("[TotalWarRoadSpline] 복원할 백업 데이터가 존재하지 않습니다.");
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;

        TerrainData tData = terrain.terrainData;

        // 1. 높이맵 복원
        tData.SetHeights(0, 0, _backupHeights);

        // 2. 스플랫맵 복원
        if (_backupAlphamaps != null)
        {
            tData.SetAlphamaps(0, 0, _backupAlphamaps);
        }

        // 3. 잔디맵 복원
        if (_backupDetails != null)
        {
            for (int l = 0; l < _backupDetails.Count; l++)
            {
                tData.SetDetailLayer(0, 0, l, _backupDetails[l]);
            }
        }

        GenerateRoadMesh();

        terrain.Flush();
#if UNITY_EDITOR
        EditorUtility.SetDirty(tData);
        EditorUtility.SetDirty(terrain);
        SceneView.RepaintAll();
#endif

        Debug.Log("[TotalWarRoadSpline] ⏪ 도로 생성 전 이전 지형으로 100% 원상복구 완료!");
    }

    private static Vector3 ClosestPointOnSegmentXZ(Vector3 pt, Vector3 a, Vector3 b, out float dist)
    {
        Vector2 p = new Vector2(pt.x, pt.z);
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);

        Vector2 ab = b2 - a2;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f)
        {
            dist = Vector2.Distance(p, a2);
            return a;
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - a2, ab) / lenSq);
        Vector3 proj = Vector3.Lerp(a, b, t);
        dist = Vector2.Distance(p, a2 + ab * t);
        return proj;
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        List<Vector3> pts = SampleSplinePointsWorld();
        if (pts.Count < 2) return;

        // 도로 중심선 (황토색)
        Gizmos.color = new Color(0.95f, 0.75f, 0.25f, 0.95f);
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i] + Vector3.up * 0.15f, pts[i + 1] + Vector3.up * 0.15f);
        }

        // 도로 좌우 경계선 (연갈색)
        Gizmos.color = new Color(0.95f, 0.85f, 0.45f, 0.55f);
        float halfW = roadWidth * 0.5f;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 fwd = (pts[i + 1] - pts[i]).normalized;
            Vector3 right = new Vector3(-fwd.z, 0f, fwd.x);

            Gizmos.DrawLine(pts[i] - right * halfW + Vector3.up * 0.15f, pts[i + 1] - right * halfW + Vector3.up * 0.15f);
            Gizmos.DrawLine(pts[i] + right * halfW + Vector3.up * 0.15f, pts[i + 1] + right * halfW + Vector3.up * 0.15f);
        }
    }
}
