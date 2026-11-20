using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 🌊 [전술 수계 및 늪지 구역 (Tactical Water Zone)]
/// - 삼국지 토탈워의 고인 물(Pond), 남만 독성 늪지(Swamp), 얕은 도하 여울목(Shallow Ford), 물웅덩이(Puddle)를 
///   원형(Circle) 또는 직사각형(Box) 기즈모로 전장에 자유롭게 배치.
/// - 부대(Unit/Formation)가 진입했을 때 토탈워 고유의 이동 감속, 기병 돌격 무력화, 피로도 가중 디버프를 실시간 적용.
/// - URP 투명 수면 메쉬 자동 생성 및 지형 높이 스냅 기능 지원.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
[ExecuteAlways]
public class TacticalWaterZone : MonoBehaviour
{
    public enum ZoneShape
    {
        [InspectorName("원형 구역 (Circle)")]
        Circle,
        [InspectorName("직사각형 구역 (Box)")]
        Box
    }

    public enum WaterZoneType
    {
        [InspectorName("얕은 여울목 (Shallow Ford - 도하 지점)")]
        ShallowFord,

        [InspectorName("남만·강남 늪지대 (Swamp / Marsh)")]
        Swamp,

        [InspectorName("깊은 고인 물·연못 (Pond - 도하 불가)")]
        Pond,

        [InspectorName("비온 뒤 웅덩이 (Puddle)")]
        Puddle
    }

    private static readonly List<TacticalWaterZone> activeZones = new List<TacticalWaterZone>();
    public static IReadOnlyList<TacticalWaterZone> ActiveZones => activeZones;

    [Header("🌊 수계 구역 형태 및 전술 속성")]
    [Tooltip("수계 구역 형태 (원형 또는 직사각형)")]
    public ZoneShape shape = ZoneShape.Circle;

    [Tooltip("전술 수계 유형 (여울목, 늪지대, 연못, 웅덩이)")]
    public WaterZoneType zoneType = WaterZoneType.ShallowFord;

    [Tooltip("원형 구역의 유효 반경 (단위: m, 기본: 15m)")]
    [Range(2f, 100f)]
    public float radius = 15f;

    [Tooltip("직사각형 구역의 가로(X) 및 세로(Z) 크기 (단위: m, 기본: 30x15m)")]
    public Vector2 boxSize = new Vector2(30f, 15f);

    [Header("📐 수심 및 이동 저항 페널티")]
    [Tooltip("구역 평균 수심 (단위: m, 0.1~3.0m)")]
    [Range(0.05f, 5.0f)]
    public float waterDepth = 0.35f;

    [Tooltip("보병 이동속도 배율")]
    [Range(0.1f, 1.0f)]
    public float infantrySpeedMultiplier = 0.70f;

    [Tooltip("기병 이동속도 배율")]
    [Range(0.1f, 1.0f)]
    public float cavalrySpeedMultiplier = 0.60f;

    [Tooltip("기병 돌격 보너스 허용 배율 (0.0 = 완전 무력화)")]
    [Range(0.0f, 1.0f)]
    public float chargeBonusMultiplier = 0.0f;

    [Tooltip("피로도(Fatigue) 소모 배율")]
    [Range(1.0f, 5.0f)]
    public float fatigueDrainMultiplier = 2.5f;

    [Tooltip("부대 통과 가능 여부")]
    public bool isPassable = true;

    [Header("🎨 비주얼 수면 메쉬 & 오프셋")]
    [Tooltip("구역 내에 URP 수면 메쉬를 자동 생성하여 표시할지 여부")]
    public bool createLocalWaterMesh = true;

    [Tooltip("지형 파묻힘 방지용 수면 상향 오프셋 (단위: m)")]
    [Range(0.02f, 1.0f)]
    public float surfaceYOffset = 0.10f;

    [Tooltip("수면 머티리얼 (비어있으면 TotalWarWater 자동 사용)")]
    public Material customWaterMaterial;

    [Tooltip("물보라 발자국 이펙트 프리팹 (선택 사항)")]
    public GameObject waterSplashVFX;

    [SerializeField, HideInInspector]
    private GameObject localWaterMeshObject;

    // 💾 [지형 스냅샷 백업 보관소]
    private float[,] _backupHeights;
    private float[,,] _backupAlphamaps;
    private List<int[,]> _backupDetails;
    private bool _hasBackup = false;
    public bool HasBackup => _hasBackup;

    private const string WATER_MAT_PATH = "Assets/TotalWar_ThreeKingdoms_Extracted/Materials/TotalWarWater.mat";

    private void OnEnable()
    {
        if (!activeZones.Contains(this)) activeZones.Add(this);
        UpdatePresetProperties();
        SyncLocalWaterMesh();
    }

    private void OnDisable()
    {
        activeZones.Remove(this);
    }

    private void OnValidate()
    {
        EnforceLevelTransform();
        UpdatePresetProperties();
        SyncLocalWaterMesh();
    }

    /// <summary>
    /// ⚖️ [물의 수평 보장]: 연못/웅덩이 수면의 기울어짐(Roll/Pitch X, Z)을 0으로 강제 수평 보정
    /// </summary>
    public void EnforceLevelTransform()
    {
        Vector3 angles = transform.rotation.eulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(angles.x, 0f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(angles.z, 0f)) > 0.05f)
        {
            transform.rotation = Quaternion.Euler(0f, angles.y, 0f);
        }
    }

    public void UpdatePresetProperties()
    {
        switch (zoneType)
        {
            case WaterZoneType.ShallowFord:
                waterDepth = Mathf.Clamp(waterDepth, 0.2f, 0.6f);
                infantrySpeedMultiplier = 0.70f;
                cavalrySpeedMultiplier = 0.60f;
                chargeBonusMultiplier = 0.0f;
                fatigueDrainMultiplier = 2.2f;
                isPassable = true;
                break;

            case WaterZoneType.Swamp:
                waterDepth = Mathf.Clamp(waterDepth, 0.3f, 0.9f);
                infantrySpeedMultiplier = 0.50f;
                cavalrySpeedMultiplier = 0.40f;
                chargeBonusMultiplier = 0.0f;
                fatigueDrainMultiplier = 3.5f;
                isPassable = true;
                break;

            case WaterZoneType.Pond:
                waterDepth = Mathf.Max(waterDepth, 1.5f);
                infantrySpeedMultiplier = 0.20f;
                cavalrySpeedMultiplier = 0.15f;
                chargeBonusMultiplier = 0.0f;
                fatigueDrainMultiplier = 4.0f;
                isPassable = false;
                break;

            case WaterZoneType.Puddle:
                waterDepth = 0.1f;
                infantrySpeedMultiplier = 0.90f;
                cavalrySpeedMultiplier = 0.85f;
                chargeBonusMultiplier = 0.5f;
                fatigueDrainMultiplier = 1.2f;
                isPassable = true;
                break;
        }
    }

    /// <summary>
    /// 수평(XZ) 평면 기준으로 좌표가 구역 내부에 포함되는지 판정합니다.
    /// </summary>
    public bool ContainsXZ(Vector3 worldPos)
    {
        Vector3 center = transform.position;
        if (shape == ZoneShape.Circle)
        {
            float dx = worldPos.x - center.x;
            float dz = worldPos.z - center.z;
            return (dx * dx + dz * dz) <= (radius * radius);
        }
        else
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            float halfX = boxSize.x * 0.5f;
            float halfZ = boxSize.y * 0.5f;
            return Mathf.Abs(localPos.x) <= halfX && Mathf.Abs(localPos.z) <= halfZ;
        }
    }

    public bool Contains(Vector3 worldPos)
    {
        Vector3 center = transform.position;
        float surfaceY = center.y + surfaceYOffset;

        // 🌊 [높이 Y축 검사]: 공중(화살/비행체/높은 다리 위) 및 지하 판정 완전 배제
        // 유닛의 발이 수면 근처([-waterDepth ~ +0.5m])에 닿아 있을 때만 물의 영향을 받음
        float minY = surfaceY - waterDepth - 0.5f;
        float maxY = surfaceY + 0.6f;
        if (worldPos.y < minY || worldPos.y > maxY) return false;

        return ContainsXZ(worldPos);
    }

    /// <summary>
    /// 🌊 [전역 수계 존 판정]: 지정된 월드 좌표에 배치된 택티컬 워터존이 있는지 O(N) 검사
    /// </summary>
    public static bool IsAnyWaterZoneAt(Vector3 worldPos, out TacticalWaterZone hitZone)
    {
        hitZone = null;
        var zones = activeZones;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] != null && zones[i].Contains(worldPos))
            {
                hitZone = zones[i];
                return true;
            }
        }
        return false;
    }

    public static bool IsAnyWaterZoneAt(Vector3 worldPos)
    {
        return IsAnyWaterZoneAt(worldPos, out _);
    }


    /// <summary>
    /// 로컬 수면 메쉬 생성 및 갱신
    /// </summary>
    public void SyncLocalWaterMesh()
    {
        if (!createLocalWaterMesh)
        {
            if (localWaterMeshObject != null)
            {
                if (Application.isPlaying) Destroy(localWaterMeshObject);
                else DestroyImmediate(localWaterMeshObject);
                localWaterMeshObject = null;
            }
            return;
        }

        if (localWaterMeshObject == null)
        {
            Transform existing = transform.Find("LocalWaterMesh");
            if (existing != null) localWaterMeshObject = existing.gameObject;
        }

        if (localWaterMeshObject == null)
        {
            localWaterMeshObject = new GameObject("LocalWaterMesh");
            localWaterMeshObject.transform.SetParent(transform, false);
        }

        localWaterMeshObject.transform.localPosition = Vector3.up * surfaceYOffset;
        localWaterMeshObject.transform.localRotation = Quaternion.identity;
        localWaterMeshObject.transform.localScale = Vector3.one;

        MeshFilter mf = localWaterMeshObject.GetComponent<MeshFilter>();
        if (mf == null) mf = localWaterMeshObject.AddComponent<MeshFilter>();

        MeshRenderer mr = localWaterMeshObject.GetComponent<MeshRenderer>();
        if (mr == null) mr = localWaterMeshObject.AddComponent<MeshRenderer>();

        // 머티리얼 바인딩
        mr.sharedMaterial = GetWaterMaterial();

        // 메쉬 생성
        if (shape == ZoneShape.Circle)
        {
            mf.sharedMesh = CreateCircleMesh(radius, 32);
        }
        else
        {
            mf.sharedMesh = CreateQuadMesh(boxSize.x, boxSize.y);
        }
    }

    private Material GetWaterMaterial()
    {
        if (customWaterMaterial != null) return customWaterMaterial;

        if (TotalWarSeaPlane.Instance != null && TotalWarSeaPlane.Instance.waterMaterial != null)
        {
            return TotalWarSeaPlane.Instance.waterMaterial;
        }

#if UNITY_EDITOR
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(WATER_MAT_PATH);
        if (mat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/TotalWarWater");
            if (s != null)
            {
                mat = new Material(s);
                string dir = System.IO.Path.GetDirectoryName(WATER_MAT_PATH);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(mat, WATER_MAT_PATH);
            }
        }
        if (mat != null)
        {
            customWaterMaterial = mat;
            return mat;
        }
#endif
        return null;
    }

    private Mesh CreateCircleMesh(float r, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "WaterZoneCircleMesh";

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        float angleStep = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a = i * angleStep;
            float x = Mathf.Cos(a) * r;
            float z = Mathf.Sin(a) * r;

            vertices[i + 1] = new Vector3(x, 0, z);
            uvs[i + 1] = new Vector2((x / (r * 2f)) + 0.5f, (z / (r * 2f)) + 0.5f);
        }

        Vector3[] normals = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++) normals[i] = Vector3.up;

        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            triangles[ti++] = 0;
            triangles[ti++] = (i + 1 >= segments) ? 1 : i + 2;
            triangles[ti++] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh CreateQuadMesh(float width, float length)
    {
        Mesh mesh = new Mesh();
        mesh.name = "WaterZoneBoxMesh";

        float hx = width * 0.5f;
        float hz = length * 0.5f;

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-hx, 0, -hz),
            new Vector3( hx, 0, -hz),
            new Vector3( hx, 0,  hz),
            new Vector3(-hx, 0,  hz)
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 현재 위치의 터레인 바닥 고도에 오브젝트 Y 높이 자동 스냅
    /// </summary>
    public void SnapToTerrainSurface()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 pos = transform.position;
        float h = terrain.SampleHeight(pos) + terrain.transform.position.y;
        pos.y = h;
        transform.position = pos;
        SyncLocalWaterMesh();
    }

    /// <summary>
    /// 🌊 [연못/웅덩이 지형 자동 조각 (Carve Basin)]
    /// - 워터존 형태(원형/박스)에 맞춰 터레인을 오목한 그릇(Bowl) 모양으로 유선형 굴착
    /// - 바닥과 연안 백사장을 진흙/자갈(Layer 4)로 부드럽게 도색
    /// - 수중 3D 잔디를 100% 클리어링하여 완벽한 일체감의 호수/웅덩이 완성
    /// </summary>
    public void CarveTerrainBasin()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("[TacticalWaterZone] 활성 터레인을 찾을 수 없습니다.");
            return;
        }

        TerrainData tData = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = tData.size;

        int hRes = tData.heightmapResolution;
        float[,] heights = tData.GetHeights(0, 0, hRes, hRes);

        // 💾 [지형 스냅샷 백업]: 파내기 직전의 높이/스플랫맵/잔디맵 전수 보존
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

        Vector3 center = transform.position;
        float surfaceY = center.y + surfaceYOffset;
        float carveDepth = Mathf.Max(0.4f, waterDepth);
        float bedCenterY = surfaceY - carveDepth;
        float edgeY = surfaceY - 0.15f; // 수면 가장자리가 0.15m 아래에 안착되어 메쉬 칼자국 방지

        bool[,] modifiedMask = new bool[hRes, hRes];

        float bankWidth = (shape == ZoneShape.Circle) ? Mathf.Max(3f, radius * 0.3f) : 3.5f;
        float totalRadius = radius + bankWidth;
        float hx = boxSize.x * 0.5f;
        float hz = boxSize.y * 0.5f;
        Quaternion invRot = Quaternion.Inverse(transform.rotation);

        // 1. 고도 파내기
        for (int y = 0; y < hRes; y++)
        {
            float normY = (float)y / (hRes - 1);
            float worldZ = tPos.z + normY * tSize.z;

            for (int x = 0; x < hRes; x++)
            {
                float normX = (float)x / (hRes - 1);
                float worldX = tPos.x + normX * tSize.x;
                Vector3 cellPos = new Vector3(worldX, 0, worldZ);

                float currentH = heights[y, x] * tSize.y + tPos.y;
                float targetH = currentH;
                bool inRange = false;

                if (shape == ZoneShape.Circle)
                {
                    float d = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(center.x, center.z));
                    if (d <= totalRadius)
                    {
                        inRange = true;
                        if (d <= radius)
                        {
                            float t = d / radius;
                            float bowl = Mathf.Cos(t * 0.5f * Mathf.PI);
                            targetH = Mathf.Lerp(edgeY, bedCenterY, bowl);
                        }
                        else
                        {
                            float tBank = (d - radius) / bankWidth;
                            float blend = Mathf.SmoothStep(0f, 1f, tBank);
                            targetH = Mathf.Lerp(edgeY, currentH, blend);
                        }
                    }
                }
                else
                {
                    Vector3 diff = invRot * (cellPos - new Vector3(center.x, 0, center.z));
                    float dx = Mathf.Max(0f, Mathf.Abs(diff.x) - hx);
                    float dz = Mathf.Max(0f, Mathf.Abs(diff.z) - hz);
                    float d = Mathf.Sqrt(dx * dx + dz * dz);

                    if (d <= bankWidth)
                    {
                        inRange = true;
                        if (d <= 0.001f)
                        {
                            float tx = Mathf.Clamp01(Mathf.Abs(diff.x) / hx);
                            float tz = Mathf.Clamp01(Mathf.Abs(diff.z) / hz);
                            float tBox = Mathf.Max(tx, tz);
                            float bowl = Mathf.Cos(tBox * 0.5f * Mathf.PI);
                            targetH = Mathf.Lerp(edgeY, bedCenterY, bowl);
                        }
                        else
                        {
                            float tBank = d / bankWidth;
                            float blend = Mathf.SmoothStep(0f, 1f, tBank);
                            targetH = Mathf.Lerp(edgeY, currentH, blend);
                        }
                    }
                }

                if (inRange && targetH < currentH)
                {
                    heights[y, x] = (targetH - tPos.y) / tSize.y;
                    modifiedMask[y, x] = true;
                }
            }
        }

        // 2. 3x3 가우시안 스무딩 필터 (경계 단차 및 칼자국 매끄럽게 마감)
        float[,] smoothedHeights = (float[,])heights.Clone();
        for (int y = 1; y < hRes - 1; y++)
        {
            for (int x = 1; x < hRes - 1; x++)
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

        // 3. 진흙/자갈(Layer 4) 스플랫맵 도색 (수면 밖 1.5m 젖은 백사장 포함)
        int layerCount = tData.alphamapLayers;
        if (layerCount >= 5)
        {
            float[,,] alphas = tData.GetAlphamaps(0, 0, aRes, aRes);
            float beachMargin = 1.5f;

            for (int y = 0; y < aRes; y++)
            {
                float normY = (float)y / (aRes - 1);
                float worldZ = tPos.z + normY * tSize.z;

                for (int x = 0; x < aRes; x++)
                {
                    float normX = (float)x / (aRes - 1);
                    float worldX = tPos.x + normX * tSize.x;
                    Vector3 cellPos = new Vector3(worldX, 0, worldZ);

                    float d;
                    float coreLimit;
                    float outerLimit;

                    if (shape == ZoneShape.Circle)
                    {
                        d = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(center.x, center.z));
                        coreLimit = radius + beachMargin;
                        outerLimit = totalRadius;
                    }
                    else
                    {
                        Vector3 diff = invRot * (cellPos - new Vector3(center.x, 0, center.z));
                        float dx = Mathf.Max(0f, Mathf.Abs(diff.x) - hx);
                        float dz = Mathf.Max(0f, Mathf.Abs(diff.z) - hz);
                        d = Mathf.Sqrt(dx * dx + dz * dz);
                        coreLimit = beachMargin;
                        outerLimit = bankWidth;
                    }

                    if (d <= coreLimit)
                    {
                        for (int l = 0; l < layerCount; l++) alphas[y, x, l] = 0f;
                        alphas[y, x, 4] = 1.0f; // 수면 및 젖은 모래 백사장은 100% 진흙/자갈
                    }
                    else if (d <= outerLimit)
                    {
                        float t = (d - coreLimit) / Mathf.Max(0.5f, outerLimit - coreLimit);
                        float mudWeight = Mathf.SmoothStep(1.0f, 0.0f, t);
                        float baseWeight = 1.0f - mudWeight;

                        for (int l = 0; l < layerCount; l++)
                        {
                            if (l == 4) alphas[y, x, l] = mudWeight;
                            else alphas[y, x, l] = alphas[y, x, l] * baseWeight;
                        }
                    }
                }
            }
            tData.SetAlphamaps(0, 0, alphas);
        }

        // 4. 수중 잔디 100% 배제
        ClearGrassInBasin(terrain, totalRadius);

        // 5. 로컬 수면 메쉬 갱신
        SyncLocalWaterMesh();

        Debug.Log($"[TacticalWaterZone] '{gameObject.name}' 연못/웅덩이 지형 조각 및 진흙 도색, 잔디 클리어링 완료!");
    }

    /// <summary>
    /// ⏪ [파내기 전 지형으로 원클릭 되돌리기 (Undo)]
    /// </summary>
    public void RestoreTerrainBackup()
    {
        if (!_hasBackup || _backupHeights == null)
        {
            Debug.LogWarning("[TacticalWaterZone] 되돌릴 이전 지형 백업 데이터가 없습니다.");
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
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
            int dRes = tData.detailResolution;
            for (int l = 0; l < _backupDetails.Count; l++)
            {
                tData.SetDetailLayer(0, 0, l, _backupDetails[l]);
            }
        }

        SyncLocalWaterMesh();
        Debug.Log($"[TacticalWaterZone] '{gameObject.name}' 파내기 전 원본 지형으로 100% 원상복구 완료!");
    }

    private void ClearGrassInBasin(Terrain terrain, float effectRadius)
    {
        TerrainData tData = terrain.terrainData;
        int numLayers = tData.detailPrototypes.Length;
        if (numLayers == 0) return;

        int dRes = tData.detailResolution;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = tData.size;
        Vector3 center = transform.position;
        Quaternion invRot = Quaternion.Inverse(transform.rotation);
        float hx = boxSize.x * 0.5f;
        float hz = boxSize.y * 0.5f;

        for (int l = 0; l < numLayers; l++)
        {
            int[,] layerMap = tData.GetDetailLayer(0, 0, dRes, dRes, l);
            bool modified = false;

            for (int y = 0; y < dRes; y++)
            {
                float normY = (float)y / (dRes - 1);
                float worldZ = tPos.z + normY * tSize.z;

                for (int x = 0; x < dRes; x++)
                {
                    if (layerMap[y, x] == 0) continue;

                    float normX = (float)x / (dRes - 1);
                    float worldX = tPos.x + normX * tSize.x;

                    float d;
                    if (shape == ZoneShape.Circle)
                    {
                        d = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(center.x, center.z));
                    }
                    else
                    {
                        Vector3 diff = invRot * (new Vector3(worldX, 0, worldZ) - new Vector3(center.x, 0, center.z));
                        float dx = Mathf.Max(0f, Mathf.Abs(diff.x) - hx);
                        float dz = Mathf.Max(0f, Mathf.Abs(diff.z) - hz);
                        d = Mathf.Sqrt(dx * dx + dz * dz);
                    }

                    if (d <= effectRadius)
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

    private void OnDrawGizmos()
    {
        Color zoneColor;
        switch (zoneType)
        {
            case WaterZoneType.ShallowFord:
                zoneColor = new Color(0.2f, 0.8f, 1.0f, 0.55f);
                break;
            case WaterZoneType.Swamp:
                zoneColor = new Color(0.35f, 0.65f, 0.2f, 0.55f);
                break;
            case WaterZoneType.Pond:
                zoneColor = new Color(0.05f, 0.3f, 0.7f, 0.65f);
                break;
            case WaterZoneType.Puddle:
            default:
                zoneColor = new Color(0.4f, 0.7f, 0.9f, 0.45f);
                break;
        }

        Gizmos.color = zoneColor;
        Vector3 center = transform.position + Vector3.up * surfaceYOffset;

        if (shape == ZoneShape.Circle)
        {
            Gizmos.DrawWireSphere(center, radius);
        }
        else
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize.x, 0.1f, boxSize.y));
            Gizmos.matrix = oldMatrix;
        }
    }
}
