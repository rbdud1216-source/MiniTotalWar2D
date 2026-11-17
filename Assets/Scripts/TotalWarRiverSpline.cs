using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 🌊 [삼국지 토탈워 스플라인 하천 시스템 (TotalWarRiverSpline)]
/// - 씬 뷰에서 로컬 웨이포인트를 배치하여 S자로 굽이치는 강줄기 경로를 생성 (Catmull-Rom 보간).
/// - [실시간 메쉬 피드백]: 너비, 수심, 노드 이동 시 즉시 수면 리본 메쉬 실시간 갱신.
/// - [강바닥 자동 파내기]: 터레인을 코사인 U자형으로 음각 조각 + 바닥 자갈(Layer 4) 도색 + 잔디 0% 배제.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
[ExecuteAlways]
public class TotalWarRiverSpline : MonoBehaviour
{
    [Header("🌊 강줄기 웨이포인트 경로 (로컬 좌표계)")]
    [Tooltip("강줄기를 이루는 제어점 리스트 (오브젝트 로컬 좌표)")]
    public List<Vector3> waypoints = new List<Vector3>();

    [Header("📐 하천 규격 (Dimensions)")]
    [Tooltip("하천 너비 (단위: m)")]
    [Range(2f, 80f)]
    public float riverWidth = 16f;

    [Tooltip("하천 최대 수심 (단위: m)")]
    [Range(0.5f, 10f)]
    public float riverDepth = 2.5f;

    [Tooltip("강둑 완충 경사 폭 (단위: m)")]
    [Range(1f, 20f)]
    public float bankWidth = 4f;

    [Tooltip("수면 높이 오프셋 (지형 지파이팅 방지용 높이)")]
    [Range(0.01f, 0.5f)]
    public float surfaceYOffset = 0.08f;

    [Header("🎨 하천 3D 메쉬 및 머티리얼")]
    [Tooltip("하천 수면 머티리얼 (비어있으면 TotalWarWater 자동 바인딩)")]
    public Material riverMaterial;

    [Tooltip("스플라인 곡선 세분화 해상도 (세그먼트당 분할 수)")]
    [Range(4, 32)]
    public int resolutionPerSegment = 12;

    [Tooltip("강줄기 길이 방향 텍스처 타일링 비율 (m당 타일링)")]
    public float uvTilingLength = 15f;

    [SerializeField, HideInInspector]
    private MeshFilter riverMeshFilter;
    [SerializeField, HideInInspector]
    private MeshRenderer riverMeshRenderer;

    // 터레인 원복용 메모리 백업 데이터
    private static float[,] _backupHeights;
    private static float[,,] _backupAlphamaps;
    private static List<int[,]> _backupDetails;
    private static bool _hasBackup = false;

    public bool HasBackup => _hasBackup;

    private const string WATER_MAT_PATH = "Assets/TotalWar_ThreeKingdoms_Extracted/Materials/TotalWarWater.mat";

    private static readonly List<TotalWarRiverSpline> activeRivers = new List<TotalWarRiverSpline>();
    public static IReadOnlyList<TotalWarRiverSpline> ActiveRivers => activeRivers;

    // ⚡ [O(1) 공간 캐시]: 매 프레임 GetFoliageResistance 호출 시 List<Vector3> 힙 할당 방지
    private readonly List<Vector3> _cachedWorldPoints = new List<Vector3>(64);
    private float _cacheMinX, _cacheMaxX, _cacheMinZ, _cacheMaxZ;
    private float _cachedHalfWidth;
    private bool _isSplineCacheDirty = true;
    private Vector3 _cachedTransformPos;
    private Quaternion _cachedTransformRot;
    private Vector3 _cachedTransformScale;
    private int _cachedWaypointCount = -1;
    private int _cachedResolution = -1;

    /// <summary>
    /// 스플라인 월드 좌표 및 경계 상자(AABB) 캐시 무효화
    /// </summary>
    public void InvalidateSplineCache()
    {
        _isSplineCacheDirty = true;
    }

    private void EnsureSplineCache()
    {
        int wpCount = (waypoints != null) ? waypoints.Count : 0;
        bool transformChanged = transform.position != _cachedTransformPos
            || transform.rotation != _cachedTransformRot
            || transform.lossyScale != _cachedTransformScale;

        if (!_isSplineCacheDirty
            && !transformChanged
            && _cachedWaypointCount == wpCount
            && _cachedResolution == resolutionPerSegment
            && Mathf.Approximately(_cachedHalfWidth, riverWidth * 0.5f))
        {
            return;
        }

        _cachedWorldPoints.Clear();
        _cacheMinX = float.MaxValue;
        _cacheMaxX = float.MinValue;
        _cacheMinZ = float.MaxValue;
        _cacheMaxZ = float.MinValue;

        if (waypoints != null && wpCount >= 2)
        {
            for (int i = 0; i < wpCount - 1; i++)
            {
                Vector3 p0 = (i == 0) ? waypoints[i] : waypoints[i - 1];
                Vector3 p1 = waypoints[i];
                Vector3 p2 = waypoints[i + 1];
                Vector3 p3 = (i + 2 < wpCount) ? waypoints[i + 2] : p2;

                for (int step = 0; step < resolutionPerSegment; step++)
                {
                    float t = (float)step / resolutionPerSegment;
                    Vector3 wp = transform.TransformPoint(EvaluateCatmullRom(p0, p1, p2, p3, t));
                    _cachedWorldPoints.Add(wp);
                    if (wp.x < _cacheMinX) _cacheMinX = wp.x;
                    if (wp.x > _cacheMaxX) _cacheMaxX = wp.x;
                    if (wp.z < _cacheMinZ) _cacheMinZ = wp.z;
                    if (wp.z > _cacheMaxZ) _cacheMaxZ = wp.z;
                }
            }

            Vector3 lastWp = transform.TransformPoint(waypoints[wpCount - 1]);
            _cachedWorldPoints.Add(lastWp);
            if (lastWp.x < _cacheMinX) _cacheMinX = lastWp.x;
            if (lastWp.x > _cacheMaxX) _cacheMaxX = lastWp.x;
            if (lastWp.z < _cacheMinZ) _cacheMinZ = lastWp.z;
            if (lastWp.z > _cacheMaxZ) _cacheMaxZ = lastWp.z;
        }

        float halfWidth = riverWidth * 0.5f;
        _cacheMinX -= halfWidth;
        _cacheMaxX += halfWidth;
        _cacheMinZ -= halfWidth;
        _cacheMaxZ += halfWidth;
        _cachedHalfWidth = halfWidth;

        _cachedTransformPos = transform.position;
        _cachedTransformRot = transform.rotation;
        _cachedTransformScale = transform.lossyScale;
        _cachedWaypointCount = wpCount;
        _cachedResolution = resolutionPerSegment;
        _isSplineCacheDirty = false;
    }

    private void OnEnable()
    {
        if (!activeRivers.Contains(this)) activeRivers.Add(this);
        InvalidateSplineCache();
    }

    private void OnDisable()
    {
        activeRivers.Remove(this);
    }

    /// <summary>
    /// 특정 월드 좌표가 하천 수면 영역 내부(강폭 및 수면 높이 범위)에 진입했는지 여부 정밀 판정
    /// 특정 월드 좌표가 하천 수면 영역 내부(강폭 및 수면 높이 범위)에 진입했는지 여부 정밀 판정 (Zero-Allocation)
    /// - 🌊 [Y축 높이 검사]: 공중(다리 위/화살 투사체) 및 지하 판정 완전 배제
    /// </summary>
    public bool Contains(Vector3 worldPos)
    {
        List<Vector3> worldPoints = SampleSplinePointsWorld();
        if (worldPoints.Count < 2) return false;
        EnsureSplineCache();
        if (_cachedWorldPoints.Count < 2) return false;

        float halfWidth = riverWidth * 0.5f;
        Vector3 cellPos = new Vector3(worldPos.x, 0f, worldPos.z);
        // 1단계: O(1) AABB 빠른 배제
        if (worldPos.x < _cacheMinX || worldPos.x > _cacheMaxX || worldPos.z < _cacheMinZ || worldPos.z > _cacheMaxZ)
        {
            return false;
        }

        float minDist = float.MaxValue;
        Vector3 closestPt = Vector3.zero;
        float minDistSqr = float.MaxValue;
        float closestY = 0f;
        float px = worldPos.x;
        float pz = worldPos.z;

        for (int i = 0; i < worldPoints.Count - 1; i++)
        int count = _cachedWorldPoints.Count;
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 pA = worldPoints[i];
            Vector3 pB = worldPoints[i + 1];
            Vector3 proj = ClosestPointOnSegmentXZ(cellPos, pA, pB, out float dist);
            if (dist < minDist)
            Vector3 pA = _cachedWorldPoints[i];
            Vector3 pB = _cachedWorldPoints[i + 1];

            float abX = pB.x - pA.x;
            float abZ = pB.z - pA.z;
            float lenSq = abX * abX + abZ * abZ;
            float t = (lenSq > 1e-6f) ? Mathf.Clamp01(((px - pA.x) * abX + (pz - pA.z) * abZ) / lenSq) : 0f;

            float projX = pA.x + abX * t;
            float projZ = pA.z + abZ * t;
            float dx = px - projX;
            float dz = pz - projZ;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < minDistSqr)
            {
                minDist = dist;
                closestPt = proj;
                minDistSqr = distSqr;
                closestY = pA.y + (pB.y - pA.y) * t;
            }
        }

        if (minDist <= halfWidth)
        if (minDistSqr <= (_cachedHalfWidth * _cachedHalfWidth))
        {
            float surfaceY = closestPt.y + surfaceYOffset;
            float surfaceY = closestY + surfaceYOffset;
            float minY = surfaceY - riverDepth - 0.5f;
            float maxY = surfaceY + 0.6f;

            // 공중(다리 위, 화살 투사체)이나 강바닥 밑 지하는 물의 영향을 받지 않음
            return worldPos.y >= minY && worldPos.y <= maxY;
        }

        return false;
    }

    /// <summary>
    /// 전역에 배치된 모든 하천 중 특정 월드 좌표에 물이 있는지 정적 판정
    /// </summary>
    public static bool IsAnyRiverAt(Vector3 worldPos, out TotalWarRiverSpline hitRiver)
    {
        for (int i = 0; i < activeRivers.Count; i++)
        {
            if (activeRivers[i] != null && activeRivers[i].Contains(worldPos))
            {
                hitRiver = activeRivers[i];
                return true;
            }
        }
        hitRiver = null;
        return false;
    }

    private void Reset()
    {
        InitializeDefaultWaypoints();
        GenerateRiverMesh();
    }

    private void OnValidate()
    {
        InvalidateSplineCache();
        EnforceLevelTransform();
        if (waypoints == null || waypoints.Count < 2)
        {
            InitializeDefaultWaypoints();
        }
        GenerateRiverMesh();
    }

    /// <summary>
    /// ⚖️ [물의 물리적 수평 보장]: Transform의 기울기(Roll/Pitch X, Z)를 0으로 강제 수평 보정
    /// 웨이포인트들의 월드 위치는 100% 그대로 보존
    /// </summary>
    public void EnforceLevelTransform()
    {
        Vector3 angles = transform.rotation.eulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(angles.x, 0f)) > 0.05f || Mathf.Abs(Mathf.DeltaAngle(angles.z, 0f)) > 0.05f)
        {
            if (waypoints != null && waypoints.Count > 0)
            {
                // 웨이포인트들의 현재 월드 좌표 백업
                List<Vector3> rawWorldWaypoints = new List<Vector3>(waypoints.Count);
                for (int i = 0; i < waypoints.Count; i++)
                {
                    rawWorldWaypoints.Add(transform.TransformPoint(waypoints[i]));
                }

                // X, Z 회전 0으로 강제 수평화 (Y축 회전만 유지)
                transform.rotation = Quaternion.Euler(0f, angles.y, 0f);

                // 새로운 수평 로컬 좌표계로 웨이포인트 재투영
                for (int i = 0; i < waypoints.Count; i++)
                {
                    waypoints[i] = transform.InverseTransformPoint(rawWorldWaypoints[i]);
                }
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, angles.y, 0f);
            }
        }
    }

    /// <summary>
    /// 로컬 좌표 기준 4개 웨이포인트로 초기화
    /// </summary>
    public void InitializeDefaultWaypoints()
    {
        waypoints = new List<Vector3>
        {
            new Vector3(-40f, 0f, -20f),
            new Vector3(-15f, 0f,   5f),
            new Vector3( 15f, 0f,  -5f),
            new Vector3( 40f, 0f,  20f)
        };
    }

    /// <summary>
    /// Catmull-Rom 스플라인을 따라 로컬 좌표 포인트 배열 샘플링
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
    /// 월드 좌표로 변환된 스플라인 포인트 배열 샘플링
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
            (2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// 강줄기 스플라인을 따라 3D 리본 메쉬 실시간 빌드
    /// </summary>
    public void GenerateRiverMesh()
    {
        List<Vector3> localPoints = SampleSplinePointsLocal();
        if (localPoints.Count < 2) return;

        if (riverMeshFilter == null) riverMeshFilter = GetComponent<MeshFilter>();
        if (riverMeshFilter == null) riverMeshFilter = gameObject.AddComponent<MeshFilter>();

        if (riverMeshRenderer == null) riverMeshRenderer = GetComponent<MeshRenderer>();
        if (riverMeshRenderer == null) riverMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        if (riverMeshRenderer != null) riverMeshRenderer.hideFlags = HideFlags.HideInInspector;

        EnsureRiverMaterial();
        riverMeshRenderer.sharedMaterial = riverMaterial;

        int pointCount = localPoints.Count;
        Vector3[] vertices = new Vector3[pointCount * 2];
        Vector2[] uvs = new Vector2[pointCount * 2];
        Vector3[] normals = new Vector3[pointCount * 2];
        Vector4[] tangents = new Vector4[pointCount * 2];

        float currentDist = 0f;
        float halfWidth = riverWidth * 0.5f;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 curr = localPoints[i];
            Vector3 forward;

            if (i == 0)
                forward = (localPoints[1] - curr).normalized;
            else if (i == pointCount - 1)
                forward = (curr - localPoints[i - 1]).normalized;
            else
                forward = (localPoints[i + 1] - localPoints[i - 1]).normalized;

            if (i > 0)
            {
                currentDist += Vector3.Distance(localPoints[i], localPoints[i - 1]);
            }

            // 🌊 [물의 수평 단면 절대 보장]: 강 단면의 좌우 벡터(right)는 월드 수평(World XZ) 기준으로 산출하여
            // 강이 상류에서 하류로 경사지더라도 좌우로는 절대 기울어지지(Bank/Roll) 않음!
            Vector3 forwardWorld = transform.TransformDirection(forward);
            Vector3 forwardXZ = new Vector3(forwardWorld.x, 0f, forwardWorld.z).normalized;
            if (forwardXZ == Vector3.zero) forwardXZ = Vector3.forward;

            Vector3 rightWorld = new Vector3(-forwardXZ.z, 0f, forwardXZ.x); // 완전한 월드 수평 벡터 (Y = 0)
            Vector3 rightLocal = transform.InverseTransformDirection(rightWorld).normalized;
            Vector3 upLocal = transform.InverseTransformDirection(Vector3.up).normalized;

            Vector3 leftPos = curr - rightLocal * halfWidth + upLocal * surfaceYOffset;
            Vector3 rightPos = curr + rightLocal * halfWidth + upLocal * surfaceYOffset;

            vertices[i * 2] = leftPos;
            vertices[i * 2 + 1] = rightPos;

            float v = currentDist / maxTiling(uvTilingLength);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);

            normals[i * 2] = upLocal;
            normals[i * 2 + 1] = upLocal;

            tangents[i * 2] = new Vector4(forward.x, forward.y, forward.z, 1f);
            tangents[i * 2 + 1] = new Vector4(forward.x, forward.y, forward.z, 1f);
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
        mesh.name = "RiverSplineMesh";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        riverMeshFilter.sharedMesh = mesh;
        InvalidateSplineCache();
    }

    private static float maxTiling(float val) => Mathf.Max(1f, val);

    /// <summary>
    /// 스플라인 궤적을 따라 터레인 고도 U자 음각 파내기 + 강바닥 자갈(Layer 4) 페인팅 + 잔디 배제
    /// </summary>
    public void CarveTerrainRiverbed()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("[TotalWarRiverSpline] 활성 터레인을 찾을 수 없습니다.");
            return;
        }

        List<Vector3> worldSplinePoints = SampleSplinePointsWorld();
        if (worldSplinePoints.Count < 2) return;

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

        float totalRiverRadius = riverWidth * 0.5f + bankWidth;
        float halfRiverWidth = riverWidth * 0.5f;

        // 1. 고도 파내기 (유선형 S-Curve Hermite 곡면 조각)
        bool[,] modifiedMask = new bool[hRes, hRes];

        for (int y = 0; y < hRes; y++)
        {
            float normY = (float)y / (hRes - 1);
            float worldZ = tPos.z + normY * tSize.z;

            for (int x = 0; x < hRes; x++)
            {
                float normX = (float)x / (hRes - 1);
                float worldX = tPos.x + normX * tSize.x;
                Vector3 cellPos = new Vector3(worldX, 0, worldZ);

                float minDist = float.MaxValue;
                Vector3 closestPt = Vector3.zero;

                for (int i = 0; i < worldSplinePoints.Count - 1; i++)
                {
                    Vector3 pA = worldSplinePoints[i];
                    Vector3 pB = worldSplinePoints[i + 1];
                    Vector3 proj = ClosestPointOnSegmentXZ(cellPos, pA, pB, out float dist);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestPt = proj;
                    }
                }

                if (minDist <= totalRiverRadius)
                {
                    float currentH = heights[y, x] * tSize.y + tPos.y;
                    float riverSurfaceY = closestPt.y;
                    float centerBedY = riverSurfaceY - riverDepth;
                    // 수면 가장자리가 수면보다 0.15m 아래에 형성되어 물 메쉬가 지형 틈새 없이 부드럽게 감싸임
                    float edgeBedY = riverSurfaceY - 0.15f;

                    float targetH;

                    if (minDist <= halfRiverWidth)
                    {
                        // [강바닥 구간]: 중심에서 가장자리까지 완만한 U자형 곡선 (Cosine Profile)
                        float tCenter = minDist / halfRiverWidth;
                        float bedFactor = Mathf.Cos(tCenter * 0.5f * Mathf.PI);
                        targetH = Mathf.Lerp(edgeBedY, centerBedY, bedFactor);
                    }
                    else
                    {
                        // [강둑 경사면 구간]: Hermite Smoothstep S자 곡선으로 원래 지형과 부드러운 접선(C1 연속) 연결
                        float tBank = (minDist - halfRiverWidth) / bankWidth;
                        float bankBlend = Mathf.SmoothStep(0f, 1f, tBank);
                        targetH = Mathf.Lerp(edgeBedY, currentH, bankBlend);
                    }

                    // 원래 지형이 목표 높이보다 높을 때만 유선형으로 파냄
                    if (targetH < currentH)
                    {
                        heights[y, x] = (targetH - tPos.y) / tSize.y;
                        modifiedMask[y, x] = true;
                    }
                }
            }
        }

        // 2. 찰흙처럼 매끄러운 3x3 가우시안 스무딩 필터 (계단식 절벽 턱 및 각진 모서리 완전 제거)
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

        // 3. 강바닥 자갈(Layer 4) 스플랫맵 자연스러운 알파 블렌딩 도색
        int layerCount = tData.alphamapLayers;
        if (layerCount >= 5)
        {
            float[,,] alphas = tData.GetAlphamaps(0, 0, aRes, aRes);

            for (int y = 0; y < aRes; y++)
            {
                float normY = (float)y / (aRes - 1);
                float worldZ = tPos.z + normY * tSize.z;

                for (int x = 0; x < aRes; x++)
                {
                    float normX = (float)x / (aRes - 1);
                    float worldX = tPos.x + normX * tSize.x;
                    Vector3 cellPos = new Vector3(worldX, 0, worldZ);

                    float minDist = float.MaxValue;
                    for (int i = 0; i < worldSplinePoints.Count - 1; i++)
                    {
                        Vector3 pA = worldSplinePoints[i];
                        Vector3 pB = worldSplinePoints[i + 1];
                        ClosestPointOnSegmentXZ(cellPos, pA, pB, out float dist);
                        if (dist < minDist) minDist = dist;
                    }

                    float beachMargin = 1.5f; // 수면 밖 1.5m까지 젖은 자갈 강변을 형성하여 물과 육지의 완벽한 일체감 부여
                    float gravelCoreRadius = halfRiverWidth + beachMargin;

                    if (minDist <= gravelCoreRadius)
                    {
                        for (int l = 0; l < layerCount; l++) alphas[y, x, l] = 0f;
                        alphas[y, x, 4] = 1.0f; // 수면 바닥 및 강변 백사장은 100% 자갈
                    }
                    else if (minDist <= totalRiverRadius)
                    {
                        float t = (minDist - gravelCoreRadius) / Mathf.Max(0.5f, bankWidth - beachMargin);
                        float gravelWeight = Mathf.SmoothStep(1.0f, 0.0f, t);

                        // 기존 지형 텍스처와 자갈을 부드럽게 혼합
                        float baseWeight = 1.0f - gravelWeight;
                        for (int l = 0; l < layerCount; l++)
                        {
                            if (l == 4)
                            {
                                alphas[y, x, l] = gravelWeight;
                            }
                            else
                            {
                                alphas[y, x, l] = alphas[y, x, l] * baseWeight;
                            }
                        }
                    }
                }
            }
            tData.SetAlphamaps(0, 0, alphas);
        }

        // 3. 디테일 맵 잔디 클리어링
        ClearGrassAlongRiver(terrain, worldSplinePoints, totalRiverRadius);

        // 4. 수면 리본 메쉬 갱신
        GenerateRiverMesh();

        Debug.Log("[TotalWarRiverSpline] 터레인 강바닥 음각 조각 및 자갈 도색, 수중 잔디 클리어링 완료!");
    }

    private void ClearGrassAlongRiver(Terrain terrain, List<Vector3> worldPoints, float radius)
    {
        TerrainData tData = terrain.terrainData;
        int numLayers = tData.detailPrototypes.Length;
        if (numLayers == 0) return;

        int dRes = tData.detailResolution;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = tData.size;

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

    private static Vector3 ClosestPointOnSegmentXZ(Vector3 p, Vector3 a, Vector3 b, out float dist)
    {
        Vector2 p2 = new Vector2(p.x, p.z);
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);

        Vector2 ab = b2 - a2;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f)
        {
            dist = Vector2.Distance(p2, a2);
            return a;
        }

        float t = Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / lenSq);
        Vector2 proj2 = a2 + ab * t;
        dist = Vector2.Distance(p2, proj2);

        return Vector3.Lerp(a, b, t);
    }

    public void EnsureRiverMaterial()
    {
        if (riverMaterial != null) return;

#if UNITY_EDITOR
        riverMaterial = AssetDatabase.LoadAssetAtPath<Material>(WATER_MAT_PATH);
        if (riverMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/TotalWarWater");
            if (s != null)
            {
                riverMaterial = new Material(s);
                string dir = System.IO.Path.GetDirectoryName(WATER_MAT_PATH);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(riverMaterial, WATER_MAT_PATH);
            }
        }
#endif
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        // 로컬 웨이포인트를 월드 좌표로 변환하여 시각화
        Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.9f);
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 worldPos = transform.TransformPoint(waypoints[i]);
            Gizmos.DrawSphere(worldPos, 1.2f);
        }

        // 스플라인 곡선 와이어
        List<Vector3> worldPoints = SampleSplinePointsWorld();
        Gizmos.color = new Color(0.1f, 0.9f, 0.7f, 1.0f);
        for (int i = 0; i < worldPoints.Count - 1; i++)
        {
            Gizmos.DrawLine(worldPoints[i] + Vector3.up * 0.1f, worldPoints[i + 1] + Vector3.up * 0.1f);
        }
    }

    /// <summary>
    /// 강바닥을 파내기 직전의 원본 지형(높이, 스플랫맵, 잔디)으로 1초 만에 완벽 복원
    /// </summary>
    public void RestoreTerrainBackup()
    {
        if (!_hasBackup || _backupHeights == null)
        {
            Debug.LogWarning("[TotalWarRiverSpline] 복원할 이전 지형 백업 데이터가 없습니다.");
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null) return;

        TerrainData tData = terrain.terrainData;

        // 1. 높이맵 복원
        tData.SetHeights(0, 0, _backupHeights);

        // 2. 스플랫맵(텍스처) 복원
        if (_backupAlphamaps != null)
        {
            tData.SetAlphamaps(0, 0, _backupAlphamaps);
        }

        // 3. 디테일맵(3D 잔디) 복원
        if (_backupDetails != null)
        {
            int dRes = tData.detailResolution;
            for (int l = 0; l < _backupDetails.Count && l < tData.detailPrototypes.Length; l++)
            {
                tData.SetDetailLayer(0, 0, l, _backupDetails[l]);
            }
        }

        Debug.Log("<color=#00FF88><b>[TotalWarRiverSpline] 이전 지형(고도 + 텍스처 + 잔디)이 100% 완벽하게 원상복구되었습니다!</b></color>");
    }
}
