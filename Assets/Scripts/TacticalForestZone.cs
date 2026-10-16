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

    // 생성된 초목 컨테이너
    private Transform vegetationRoot;

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
    }

    private void Update()
    {
        // 숲 구역 중심점의 지형 높이 실시간 유지
        SnapToTerrain();
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
    }

    [ContextMenu("🧹 초목 제거 (Clear Vegetation)")]
    public void ClearVegetation()
    {
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
            // 나무 그림자 투사(Cast) 및 수신(Receive) 100% 활성화 (지면에 울창한 나무 그림자 형성)
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            ren.receiveShadows = true;

            string name = ren.gameObject.name.ToLower();
            if (name.Contains("trunk"))
            {
                ren.sharedMaterial = trunkMat;
            }
            else if (name.Contains("leave") || name.Contains("leaf"))
            {
                ren.sharedMaterial = leavesMat;
            }
            else
            {
                // 서브메시 2개인 경우 [0] Trunk, [1] Leaves 순서 매핑
                Material[] mats = ren.sharedMaterials;
                if (mats.Length >= 2)
                {
                    mats[0] = trunkMat;
                    mats[1] = leavesMat;
                    ren.sharedMaterials = mats;
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
            if (zone == null || !zone.enabled || !zone.blockLineOfSight || !zone.gameObject.activeInHierarchy) continue;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateDefaultForests()
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

    private static void CreateDefaultZone(string name, Vector3 pos, float rad, ForestTheme theme)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        TacticalForestZone zone = go.AddComponent<TacticalForestZone>();
        zone.shape = ZoneShape.Circle;
        zone.theme = theme;
        zone.radius = rad;
        zone.treeCount = Mathf.RoundToInt(rad * 0.85f);
        zone.bushCount = Mathf.RoundToInt(rad * 1.5f);
        zone.SnapToTerrain();
        zone.GenerateVegetation();
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
