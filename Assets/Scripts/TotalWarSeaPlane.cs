using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 삼국지 토탈워 전장 바다 및 대형 호수 전역 수면 평면 관리자 (TotalWarSeaPlane)
/// - 전역 수위(Water Level Y) 기준 광활한 수면 평면을 터레인 전체에 투영
/// - URP 투명 수면 셰이더(TotalWarWater.shader) 자동 바인딩 및 머티리얼 제어
/// - 지형 최저 고도 자동 감지 및 5색 지형 페인팅/풀 배제 시스템과 연동
/// </summary>
[ExecuteAlways]
[SelectionBase]
[DisallowMultipleComponent]
public class TotalWarSeaPlane : MonoBehaviour
{
    private static TotalWarSeaPlane _instance;
    public static TotalWarSeaPlane Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindFirstObjectByType<TotalWarSeaPlane>();
            }
            return _instance;
        }
    }

    [Header("🌊 전역 수위 설정 (Water Level)")]
    [Tooltip("전역 수면의 월드 Y 높이 (미터 단위)")]
    [Range(-20f, 100f)]
    public float waterLevelY = 2.0f;

    [Tooltip("수면 평면이 터레인 외곽으로 확장되는 여유 반경 (미터)")]
    public float seaMargin = 200f;

    [Header("🎨 수면 머티리얼 (Water Material)")]
    public Material waterMaterial;

    [Header("⚙️ 렌더러 참조")]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    private const string WATER_SHADER_NAME = "Universal Render Pipeline/TotalWarWater";
    private const string WATER_MAT_PATH = "Assets/TotalWar_ThreeKingdoms_Extracted/Materials/TotalWarWater.mat";

    private void Awake()
    {
        _instance = this;
    }

    private void OnEnable()
    {
        _instance = this;
        ApplyWaterLevelPosition();
    }

    private void Update()
    {
        // 에디터 및 런타임에서 위치 동기화
        if (transform.position.y != waterLevelY)
        {
            ApplyWaterLevelPosition();
        }
    }

    /// <summary>
    /// 전역 수위 높이를 오브젝트 위치에 반영
    /// </summary>
    public void ApplyWaterLevelPosition()
    {
        Vector3 pos = transform.position;
        pos.y = waterLevelY;
        transform.position = pos;
    }

    /// <summary>
    /// 전역 수위 Y 높이 반환 (다른 시스템에서 조회용)
    /// </summary>
    public static float GetGlobalWaterLevel(float defaultLevel = 2.0f)
    {
        if (Instance != null)
        {
            return Instance.waterLevelY;
        }
        return defaultLevel;
    }

    /// <summary>
    /// 터레인 크기에 맞춘 대형 수면 평면 메쉬 생성 및 머티리얼 바인딩
    /// </summary>
    public void GenerateSeaPlane()
    {
        Terrain terrain = Terrain.activeTerrain;
        float sizeX = 1000f;
        float sizeZ = 1000f;
        Vector3 terrainCenter = Vector3.zero;

        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 tSize = terrain.terrainData.size;
            Vector3 tPos = terrain.transform.position;
            sizeX = tSize.x + seaMargin * 2f;
            sizeZ = tSize.z + seaMargin * 2f;
            terrainCenter = tPos + new Vector3(tSize.x * 0.5f, 0, tSize.z * 0.5f);
        }

        transform.position = new Vector3(terrainCenter.x, waterLevelY, terrainCenter.z);

        // 메쉬 필터 및 렌더러 준비
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // 머티리얼 확인 및 생성
        EnsureWaterMaterial();
        meshRenderer.sharedMaterial = waterMaterial;

        // 메쉬 생성 (가로세로 서브디비전이 있는 평면)
        meshFilter.sharedMesh = CreatePlaneMesh(sizeX, sizeZ, 32, 32);

        Debug.Log($"[TotalWarSeaPlane] 전역 수면 평면 생성 완료! (크기: {sizeX:F0}x{sizeZ:F0}m, 수위: Y={waterLevelY:F1}m)");
    }

    /// <summary>
    /// 터레인의 최저 고도를 스캔하여 적절한 수위 자동 산출
    /// </summary>
    public void AutoDetectWaterLevelFromTerrain()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogWarning("[TotalWarSeaPlane] 활성 터레인을 찾을 수 없습니다.");
            return;
        }

        TerrainData tData = terrain.terrainData;
        int res = tData.heightmapResolution;
        float[,] heights = tData.GetHeights(0, 0, res, res);

        float minH = float.MaxValue;
        float maxH = float.MinValue;

        for (int y = 0; y < res; y += 4)
        {
            for (int x = 0; x < res; x += 4)
            {
                float h = heights[y, x];
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }
        }

        float worldMinH = terrain.transform.position.y + minH * tData.size.y;
        float worldMaxH = terrain.transform.position.y + maxH * tData.size.y;

        // 최저점에서 전체 고도차의 10% 지점을 이상적인 수위로 권장 설정
        float recommendedLevel = worldMinH + (worldMaxH - worldMinH) * 0.08f;
        if (recommendedLevel <= worldMinH + 0.5f)
        {
            recommendedLevel = worldMinH + 1.5f;
        }

        waterLevelY = Mathf.Round(recommendedLevel * 10f) / 10f;
        ApplyWaterLevelPosition();

        Debug.Log($"[TotalWarSeaPlane] 터레인 고도 스캔 완료: 최저 {worldMinH:F1}m, 최고 {worldMaxH:F1}m -> 권장 수위 Y={waterLevelY:F1}m 설정");
    }

    /// <summary>
    /// URP 수면 머티리얼 로드 또는 자동 생성
    /// </summary>
    public void EnsureWaterMaterial()
    {
        if (waterMaterial != null) return;

#if UNITY_EDITOR
        waterMaterial = AssetDatabase.LoadAssetAtPath<Material>(WATER_MAT_PATH);
        if (waterMaterial == null)
        {
            Shader waterShader = Shader.Find(WATER_SHADER_NAME);
            if (waterShader == null)
            {
                waterShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            waterMaterial = new Material(waterShader);
            waterMaterial.name = "TotalWarWater";

            string dir = System.IO.Path.GetDirectoryName(WATER_MAT_PATH);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            AssetDatabase.CreateAsset(waterMaterial, WATER_MAT_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TotalWarSeaPlane] 신규 수면 머티리얼 생성: {WATER_MAT_PATH}");
        }
#endif
    }

    /// <summary>
    /// 가로/세로 분할된 평면 메쉬 생성
    /// </summary>
    private Mesh CreatePlaneMesh(float width, float length, int segmentsX, int segmentsZ)
    {
        Mesh mesh = new Mesh();
        mesh.name = "TotalWarSeaPlane_Mesh";

        int vCount = (segmentsX + 1) * (segmentsZ + 1);
        Vector3[] vertices = new Vector3[vCount];
        Vector2[] uvs = new Vector2[vCount];
        Vector3[] normals = new Vector3[vCount];
        Vector4[] tangents = new Vector4[vCount];

        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        float dx = width / segmentsX;
        float dz = length / segmentsZ;

        int vi = 0;
        for (int z = 0; z <= segmentsZ; z++)
        {
            for (int x = 0; x <= segmentsX; x++)
            {
                float px = -halfW + x * dx;
                float pz = -halfL + z * dz;

                vertices[vi] = new Vector3(px, 0, pz);
                uvs[vi] = new Vector2((float)x / segmentsX, (float)z / segmentsZ);
                normals[vi] = Vector3.up;
                tangents[vi] = new Vector4(1, 0, 0, 1);
                vi++;
            }
        }

        int[] triangles = new int[segmentsX * segmentsZ * 6];
        int ti = 0;
        for (int z = 0; z < segmentsZ; z++)
        {
            for (int x = 0; x < segmentsX; x++)
            {
                int row1 = z * (segmentsX + 1);
                int row2 = (z + 1) * (segmentsX + 1);

                triangles[ti++] = row1 + x;
                triangles[ti++] = row2 + x;
                triangles[ti++] = row1 + x + 1;

                triangles[ti++] = row1 + x + 1;
                triangles[ti++] = row2 + x;
                triangles[ti++] = row2 + x + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private void OnDrawGizmosSelected()
    {
        // 씬 뷰 선택 시 수면 높이를 푸른색 와이어프레임과 반투명 판으로 시각화
        Gizmos.color = new Color(0.1f, 0.6f, 0.9f, 0.35f);
        Terrain terrain = Terrain.activeTerrain;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(1000f, 0.05f, 1000f);

        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 tSize = terrain.terrainData.size;
            Vector3 tPos = terrain.transform.position;
            center = new Vector3(tPos.x + tSize.x * 0.5f, waterLevelY, tPos.z + tSize.z * 0.5f);
            size = new Vector3(tSize.x + seaMargin * 2f, 0.05f, tSize.z + seaMargin * 2f);
        }

        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.1f, 0.8f, 1.0f, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
}
