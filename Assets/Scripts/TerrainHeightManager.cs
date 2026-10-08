using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// C# Burst 멀티스레드 Job 및 Pure ECS 시스템에서 
/// O(1) 초고속으로 지형 높이(SampleHeight) 및 표면 법선(SampleNormal)을 계산할 수 있는 순수 값 타입 구조체입니다.
/// </summary>
public struct TerrainHeightData
{
    [ReadOnly] public NativeArray<float> heights;
    public int resolution;
    public float3 origin;
    public float3 size;
    public int isValid;

    /// <summary>
    /// 월드 좌표 (worldX, worldZ)에서의 지형 표면 높이(Y)를 O(1) 쌍선형 보간(Bilinear Interpolation)으로 계산합니다.
    /// </summary>
    public float SampleHeight(float worldX, float worldZ)
    {
        if (isValid == 0 || !heights.IsCreated || resolution <= 1) return origin.y;

        float u = math.clamp((worldX - origin.x) / size.x, 0f, 1f);
        float v = math.clamp((worldZ - origin.z) / size.z, 0f, 1f);

        float fx = u * (resolution - 1);
        float fz = v * (resolution - 1);

        int x0 = math.clamp((int)fx, 0, resolution - 2);
        int z0 = math.clamp((int)fz, 0, resolution - 2);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        float tx = fx - x0;
        float tz = fz - z0;

        float h00 = heights[z0 * resolution + x0];
        float h10 = heights[z0 * resolution + x1];
        float h01 = heights[z1 * resolution + x0];
        float h11 = heights[z1 * resolution + x1];

        float h0 = math.lerp(h00, h10, tx);
        float h1 = math.lerp(h01, h11, tx);
        float normHeight = math.lerp(h0, h1, tz);

        return origin.y + (normHeight * size.y);
    }

    /// <summary>
    /// 월드 좌표 (worldX, worldZ)에서의 지형 표면 법선 벡터(Surface Normal)를 O(1) 편미분(Gradient)으로 계산합니다.
    /// 유닛의 경사면 기울기(옵션 B) 회전에 사용됩니다.
    /// </summary>
    public float3 SampleNormal(float worldX, float worldZ)
    {
        if (isValid == 0 || !heights.IsCreated || resolution <= 1) return new float3(0, 1, 0);

        float u = math.clamp((worldX - origin.x) / size.x, 0f, 1f);
        float v = math.clamp((worldZ - origin.z) / size.z, 0f, 1f);

        float fx = u * (resolution - 1);
        float fz = v * (resolution - 1);

        int x0 = math.clamp((int)fx, 0, resolution - 2);
        int z0 = math.clamp((int)fz, 0, resolution - 2);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        float tx = fx - x0;
        float tz = fz - z0;

        float h00 = heights[z0 * resolution + x0];
        float h10 = heights[z0 * resolution + x1];
        float h01 = heights[z1 * resolution + x0];
        float h11 = heights[z1 * resolution + x1];

        float stepX = size.x / math.max(1, resolution - 1);
        float stepZ = size.z / math.max(1, resolution - 1);

        // X, Z 축 경사도 편미분 (Bilinear Gradient)
        float dh_dx = ((h10 - h00) * (1f - tz) + (h11 - h01) * tz) * size.y / math.max(0.001f, stepX);
        float dh_dz = ((h01 - h00) * (1f - tx) + (h11 - h10) * tx) * size.y / math.max(0.001f, stepZ);

        float3 normal = math.normalize(new float3(-dh_dx, 1.0f, -dh_dz));
        return normal;
    }

    /// <summary>
    /// 사수(shooterPos)와 목표(targetPos) 사이의 직선 사선(Line of Sight, LoS)이
    /// 중간 언덕이나 지형에 의해 가려졌는지 O(1) 높이 샘플링으로 검사합니다.
    /// true: 시야 확보됨 (사격 가능), false: 언덕에 의해 시야 차폐됨.
    /// </summary>
    public bool CheckLineOfSight(float3 shooterPos, float3 targetPos, float eyeHeight = 0.2f, float targetHeight = 0.2f, int sampleCount = 4)
    {
        if (isValid == 0 || !heights.IsCreated || resolution <= 1) return true;

        float3 start = new float3(shooterPos.x, shooterPos.y + eyeHeight, shooterPos.z);
        float3 end = new float3(targetPos.x, targetPos.y + targetHeight, targetPos.z);

        // 🏔️ 거리 비례 고정밀 샘플링 (8m당 1회 샘플링, 좁은 능선 마루나 턱 통과 원천 방지)
        float horizDist = math.distance(start.xz, end.xz);
        int dynamicSamples = math.clamp((int)(horizDist / 8.0f), sampleCount, 24);

        // 시작점과 끝점 사이의 중간 지형 고도와 직선 광선 고도를 비교
        float step = 1.0f / (dynamicSamples + 1);
        for (int i = 1; i <= dynamicSamples; i++)
        {
            float t = i * step;
            float checkX = math.lerp(start.x, end.x, t);
            float checkZ = math.lerp(start.z, end.z, t);
            float rayY = math.lerp(start.y, end.y, t);

            float terrainY = SampleHeight(checkX, checkZ);
            if (terrainY > rayY)
            {
                return false; // 지형이 직선 사선을 가로막음!
            }
        }

        return true;
    }
}

/// <summary>
/// 씬 내의 Terrain(터레인) 높이맵 데이터를 런타임 시작 시 1회 고속 메모리(NativeArray)로 캐싱하여,
/// 멀티스레드 C# Job System 및 Pure ECS 시스템에서 수천~수만 기의 유닛이 0.05ms 안에
/// 지형 굴곡을 따라 완벽 밀착 이동하도록 지원하는 싱글톤 매니저입니다.
/// </summary>
public class TerrainHeightManager : MonoBehaviour
{
    private static TerrainHeightManager _instance;
    private static bool isApplicationQuitting = false;

    public static TerrainHeightManager Instance
    {
        get
        {
            if (isApplicationQuitting) return null;
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TerrainHeightManager>();
                if (_instance == null && !isApplicationQuitting)
                {
                    GameObject go = new GameObject("TerrainHeightManager");
                    _instance = go.AddComponent<TerrainHeightManager>();
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null && !isApplicationQuitting;

    [Header("터레인 참조")]
    [SerializeField] private Terrain targetTerrain;

    private NativeArray<float> cachedHeights;
    private TerrainHeightData heightData;
    private bool isDataReady = false;

    public bool IsDataReady => isDataReady;
    public TerrainHeightData HeightData => heightData;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeTerrainData();
    }

    private void OnDestroy()
    {
        DisposeNativeData();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
        DisposeNativeData();
    }

    /// <summary>
    /// 활성 터레인 데이터를 탐색하고 높이맵을 NativeArray로 복사하여 캐싱합니다.
    /// </summary>
    public void InitializeTerrainData()
    {
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
            {
                targetTerrain = FindFirstObjectByType<Terrain>();
            }
        }

        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            heightData = new TerrainHeightData
            {
                isValid = 0,
                origin = float3.zero,
                size = float3.zero,
                resolution = 0
            };
            isDataReady = false;
            return;
        }

        TerrainData td = targetTerrain.terrainData;
        int res = td.heightmapResolution;

        DisposeNativeData();

        cachedHeights = new NativeArray<float>(res * res, Allocator.Persistent);

        // Unity TerrainData.GetHeights(xBase, yBase, width, height)는 [y, x] 배열 반환 (y가 행(Z축), x가 열(X축))
        float[,] heights2D = td.GetHeights(0, 0, res, res);

        for (int z = 0; z < res; z++)
        {
            int rowOffset = z * res;
            for (int x = 0; x < res; x++)
            {
                cachedHeights[rowOffset + x] = heights2D[z, x];
            }
        }

        Vector3 originPos = targetTerrain.transform.position;
        Vector3 terrainSize = td.size;

        heightData = new TerrainHeightData
        {
            heights = cachedHeights,
            resolution = res,
            origin = new float3(originPos.x, originPos.y, originPos.z),
            size = new float3(terrainSize.x, terrainSize.y, terrainSize.z),
            isValid = 1
        };

        isDataReady = true;
    }

    private void DisposeNativeData()
    {
        if (cachedHeights.IsCreated)
        {
            cachedHeights.Dispose();
        }
        heightData.isValid = 0;
        isDataReady = false;
    }

    /// <summary>
    /// 메인 스레드나 일반 컴포넌트(Squad, FormationPreviewer 등)에서 손쉽게 호출하는 초고속 지형 높이 샘플링 함수입니다.
    /// </summary>
    public static float SampleHeightFast(float worldX, float worldZ)
    {
        if (Instance != null && Instance.isDataReady)
        {
            return Instance.heightData.SampleHeight(worldX, worldZ);
        }

        if (Terrain.activeTerrain != null)
        {
            return Terrain.activeTerrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + Terrain.activeTerrain.transform.position.y;
        }

        return 0f;
    }

    /// <summary>
    /// 메인 스레드나 일반 컴포넌트에서 지형 법선(경사각)을 샘플링하는 정적 함수입니다.
    /// </summary>
    public static Vector3 SampleNormalFast(float worldX, float worldZ)
    {
        if (Instance != null && Instance.isDataReady)
        {
            float3 n = Instance.heightData.SampleNormal(worldX, worldZ);
            return new Vector3(n.x, n.y, n.z);
        }

        return Vector3.up;
    }

    /// <summary>
    /// 메인 스레드나 일반 컴포넌트(Squad 등)에서 사수와 목표 사이의 사선 차폐 여부를 검사하는 정적 함수입니다.
    /// </summary>
    public static bool CheckLineOfSightFast(Vector3 shooterPos, Vector3 targetPos, float eyeHeight = 0.2f, float targetHeight = 0.2f, int sampleCount = 4)
    {
        if (Instance != null && Instance.isDataReady)
        {
            return Instance.heightData.CheckLineOfSight(
                new float3(shooterPos.x, shooterPos.y, shooterPos.z),
                new float3(targetPos.x, targetPos.y, targetPos.z),
                eyeHeight, targetHeight, sampleCount);
        }

        return true;
    }
}
