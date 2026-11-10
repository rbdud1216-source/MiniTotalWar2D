using UnityEngine;
using System.Collections.Generic;
using MiniTotalWar;

/// <summary>
/// 🌍 [삼국지 토탈워 전장 터레인 및 사계절 환경 관리자]
/// 씬 내의 유니티 Terrain 컴포넌트와 연동하여,
/// 1) 기후 테마(TotalWarTerrainTheme)에 따른 지형 텍스처 및 238종 디테일 잔디 자동 세팅,
/// 2) 사계절(봄/여름/가을/겨울)에 따른 지표면 텍스처 스와핑,
/// 3) 실시간 전역 적설량(_TotalWarTerrainSnowAmount) 셰이더 브로드캐스트를 총괄합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TotalWarTerrainManager : MonoBehaviour
{
    private static TotalWarTerrainManager _instance;
    public static TotalWarTerrainManager Instance => _instance;

    [Header("🏔️ 대상 터레인 참조")]
    [Tooltip("관리할 목표 Terrain 컴포넌트 (미지정 시 자동 탐색)")]
    public Terrain targetTerrain;

    [Header("🌍 기후 테마 프리셋")]
    [Tooltip("적용할 기후 테마 (온대, 남만, 건조, 한랭 등)")]
    public TotalWarTerrainTheme currentTheme;

    [Header("🍂 사계절 및 환경")]
    [Tooltip("현재 사계절 환경")]
    public TacticalForestZone.Season season = TacticalForestZone.Season.Summer;

    [Range(0f, 1f)]
    [Tooltip("전장 전역 눈 덮임 비율 (0.0: 완전 녹음 -> 1.0: 100% 설경)")]
    public float globalSnowAmount = 0.0f;

    [Range(0f, 1f)]
    [Tooltip("전장 전역 가을 단풍 틴트 강도")]
    public float globalAutumnTint = 0.0f;

    [Header("🌬️ 잔디 바람 흔들림 (Waving) 제어")]
    [Range(0f, 0.5f)]
    [Tooltip("잔디 흔들림 강도 (0: 완전 고정, 0.04: 자연스러운 미풍, 0.5: 강풍)")]
    public float grassWavingStrength = 0.04f;

    [Range(0f, 0.5f)]
    [Tooltip("잔디 흔들림 속도")]
    public float grassWavingSpeed = 0.08f;

    [Header("🌾 3D 풀포기 & 식생 기후 환경 테마 (Biome Theme)")]
    [Tooltip("전장 식생 생태계 테마 (선택 시 숲/부시존처럼 해당 환경의 풀들이 일괄 자동 세팅됩니다)")]
    public GrassBiomeTheme grassBiomeTheme = GrassBiomeTheme.CentralPlains20;

    [Tooltip("전장에 살포할 삼탈워 3D 풀포기 목록 (종류, 크기, 밀도, 서식 지형 레이어)")]
    public List<RadialGrassConfig> customGrassList = new List<RadialGrassConfig>();

    [Header("🌾 청크별 데이터 기반 방사형 지피식생 (Tactical Grass System)")]
    [Tooltip("32m 청크 단위 데이터 스트리밍 렌더러 참조")]
    public TacticalGrassRenderer grassRenderer;

    [Range(1.0f, 6.0f)]
    [Tooltip("기본 지피 잔디 배치 간격 (군락 지름 0.9m에 맞추어 1.6m로 배치 시 20~30% 적절히 겹치며 완벽한 토탈워 삼국지 잔디 융단 형성, 기본: 1.6m)")]
    public float defaultGrassSpacing = 1.6f;

    [Tooltip("청크별(32m x 32m) 커스텀 바이옴 및 밀도 오버라이드 목록")]
    public List<ChunkFoliageOverride> chunkFoliageOverrides = new List<ChunkFoliageOverride>();

    /// <summary>
    /// 터레인 매니저에서 청크별 지피잔디 데이터 즉시 빌드 및 디스크 파일 영구 저장
    /// </summary>
    [ContextMenu("🌾 청크별 지피잔디 데이터 즉시 빌드 및 저장 (Bake Chunk Foliage)")]
    public void BakeChunkFoliage()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null)
        {
            Debug.LogWarning("[TotalWarTerrainManager] Terrain이 없습니다.");
            return;
        }

        if (grassRenderer == null) grassRenderer = targetTerrain.GetComponent<TacticalGrassRenderer>();
        if (grassRenderer == null) grassRenderer = targetTerrain.gameObject.AddComponent<TacticalGrassRenderer>();

        grassRenderer.targetTerrain = targetTerrain;
        grassRenderer.sampleSpacing = defaultGrassSpacing;
        grassRenderer.chunkOverrides = new List<ChunkFoliageOverride>(chunkFoliageOverrides);
        
        if (customGrassList == null || customGrassList.Count == 0)
        {
            customGrassList = RadialGrassConfig.GetBiomeFoliagePack(grassBiomeTheme);
        }
        grassRenderer.SyncPrototypesFromConfigs(customGrassList);

        grassRenderer.BakeFoliageData();
        grassRenderer.SaveFoliageDataToDisk();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(grassRenderer);
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        UnityEditor.SceneView.RepaintAll();
#endif
        Debug.Log($"<color=#00FF88><b>[TotalWarTerrainManager] 🌾 청크별 지피잔디 빌드 완료! 총 {grassRenderer.TotalInstanceCount:N0}개의 지피잔디가 영구 저장되었습니다.</b></color>");
    }

    /// <summary>
    /// 식생 기후 환경 테마를 변경하고, 해당 생태계의 대표 3D 풀들을 한꺼번에 일괄 등록합니다.
    /// (사계절 및 전역 셰이더 프로퍼티 자동 동기화 지원)
    /// </summary>
    public void SetGrassBiomeTheme(GrassBiomeTheme newTheme, bool autoSyncSeason = true)
    {
        grassBiomeTheme = newTheme;
        if (newTheme != GrassBiomeTheme.CustomSelection)
        {
            customGrassList = RadialGrassConfig.GetBiomeFoliagePack(newTheme);
        }

        if (autoSyncSeason)
        {
            switch (newTheme)
            {
                case GrassBiomeTheme.HarvestFarmlands20: // 가을 황금빛 농경지
                    season = TacticalForestZone.Season.Autumn;
                    globalAutumnTint = 1.0f;
                    globalSnowAmount = 0.0f;
                    break;
                case GrassBiomeTheme.ColdSteppe18: // 하북/요동 한랭 설원
                    season = TacticalForestZone.Season.Winter;
                    globalSnowAmount = 0.85f;
                    globalAutumnTint = 0.0f;
                    break;
                case GrassBiomeTheme.AridWasteland18: // 서량/사막 황무지
                    season = TacticalForestZone.Season.Summer;
                    globalAutumnTint = 0.35f;
                    globalSnowAmount = 0.0f;
                    break;
                case GrassBiomeTheme.RockyHighlands18: // 산악 암반 고원
                    season = TacticalForestZone.Season.Autumn;
                    globalAutumnTint = 0.30f;
                    globalSnowAmount = 0.0f;
                    break;
                case GrassBiomeTheme.BloomingMeadows22: // 봄꽃 만개 들판
                case GrassBiomeTheme.CentralPlains20: // 중원 대평원
                case GrassBiomeTheme.AllInclusive30: // 올인원
                    season = TacticalForestZone.Season.Spring;
                    globalAutumnTint = 0.0f;
                    globalSnowAmount = 0.0f;
                    break;
                case GrassBiomeTheme.DeepForest20: // 원시림 수림하층
                case GrassBiomeTheme.NanmanJungle22: // 남만 열대 정글
                case GrassBiomeTheme.JiangnanWetland20: // 강남 수변 습지
                    season = TacticalForestZone.Season.Summer;
                    globalAutumnTint = 0.0f;
                    globalSnowAmount = 0.0f;
                    break;
            }

            UpdateGlobalShaderParameters();
            SyncSceneForestZones();
        }
    }

    /// <summary>
    /// 씬 내 모든 TacticalForestZone의 계절 및 적설량을 현재 지형 테마와 자동 동기화합니다.
    /// </summary>
    public void SyncSceneForestZones()
    {
        var forests = FindObjectsByType<TacticalForestZone>(FindObjectsSortMode.None);
        if (forests == null) return;
        foreach (var f in forests)
        {
            if (f == null) continue;
            f.currentSeason = season;
            f.enableWinterSnow = (season == TacticalForestZone.Season.Winter || globalSnowAmount > 0.1f);
            f.winterSnowAmount = globalSnowAmount;
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        if (targetTerrain == null) targetTerrain = GetComponent<Terrain>();
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;

        if (customGrassList == null || customGrassList.Count == 0)
        {
            customGrassList = RadialGrassConfig.GetBiomeFoliagePack(grassBiomeTheme);
        }

#if UNITY_EDITOR
        if (currentTheme == null)
        {
            currentTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Temperate_Theme.asset");
        }
#endif
    }

    private void OnEnable()
    {
        if (_instance == null) _instance = this;
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;

#if UNITY_EDITOR
        if (currentTheme == null)
        {
            currentTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Temperate_Theme.asset");
        }
#endif
        UpdateGlobalShaderParameters();
    }

    private void Update()
    {
        // 실시간 전역 셰이더 파라미터 동기화
        UpdateGlobalShaderParameters();
    }

    /// <summary>
    /// 전역 셰이더 프로퍼티 브로드캐스트
    /// </summary>
    public void UpdateGlobalShaderParameters()
    {
        Shader.SetGlobalFloat("_TotalWarTerrainSnowAmount", globalSnowAmount);
        Shader.SetGlobalFloat("_TotalWarTerrainAutumnTint", globalAutumnTint);
        Shader.SetGlobalFloat("_TotalWarGlobalSnow", globalSnowAmount);
        ApplyWavingSettings();
    }

    /// <summary>
    /// 2D 잔디의 과도한 비틀림을 억제하고 자연스러운 미풍을 적용합니다.
    /// </summary>
    public void ApplyWavingSettings(Terrain specificTerrain = null)
    {
        Terrain[] terrains = (specificTerrain != null) ? new[] { specificTerrain } : Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            if (targetTerrain != null) terrains = new[] { targetTerrain };
        }

        if (terrains != null)
        {
            foreach (var ter in terrains)
            {
                if (ter == null || ter.terrainData == null) continue;
                ter.terrainData.wavingGrassStrength = grassWavingStrength;
                ter.terrainData.wavingGrassAmount = grassWavingStrength > 0.001f ? 0.05f : 0f;
                ter.terrainData.wavingGrassSpeed = grassWavingSpeed;
            }
        }
    }

    /// <summary>
    /// 에디터 환경에서 기후 테마 적용 완료 시 3D 식생 자동 동기화를 위한 정적 이벤트
    /// </summary>
    public static event System.Action OnThemeAppliedEditor;

    /// <summary>
    /// 현재 설정된 기후 테마와 사계절 설정을 터레인에 즉시 적용합니다.
    /// </summary>
    public void ApplyCurrentTheme()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null || currentTheme == null)
        {
            Debug.LogWarning("[TotalWarTerrainManager] 적용할 Terrain 또는 TotalWarTerrainTheme이 지정되지 않았습니다.");
            return;
        }

        currentTheme.ApplyThemeToTerrain(targetTerrain, season, globalSnowAmount);
        UpdateGlobalShaderParameters();

        // 🌾 3D 풀포기 & 이삭 자동 연동 이벤트 발행
        OnThemeAppliedEditor?.Invoke();

        Debug.Log($"[TotalWarTerrainManager] 기후 테마 '{currentTheme.climate}' 및 계절 '{season}'(적설량: {globalSnowAmount:F2}) 적용 완료!");
    }

    /// <summary>
    /// 계절을 전환하고 터레인 및 식생을 동기화합니다.
    /// </summary>
    public void SetSeason(TacticalForestZone.Season newSeason)
    {
        season = newSeason;
        if (season == TacticalForestZone.Season.Winter)
        {
            if (globalSnowAmount < 0.2f) globalSnowAmount = 0.85f;
            globalAutumnTint = 0f;
        }
        else if (season == TacticalForestZone.Season.Autumn)
        {
            globalAutumnTint = 1.0f;
            globalSnowAmount = 0f;
        }
        else
        {
            globalSnowAmount = 0f;
            globalAutumnTint = 0f;
        }

        ApplyCurrentTheme();
        SyncSceneForestZones();
    }

    /// <summary>
    /// 터레인의 경사도(Slope)를 분석하여 완만한 평지/구릉/산비탈에 테마 디테일 잔디를 자동 살포합니다.
    /// (Unity 6 DetailScatterMode.CoverageMode 기준: 밀도 스케일 30~85/100)
    /// </summary>
    public void AutoScatterDetailGrass(float coverage = 0.75f, float maxSlope = 48f, int minDensity = 30, int maxDensity = 85)
    {
        // 씬 내 모든 활성 터레인(타일 지형 포함) 수집
        Terrain[] terrains = Terrain.activeTerrains;
        if ((terrains == null || terrains.Length == 0) && targetTerrain != null)
        {
            terrains = new[] { targetTerrain };
        }

        if (terrains == null || terrains.Length == 0)
        {
            Debug.LogWarning("[TotalWarTerrainManager] 씬에 활성화된 Terrain이 없습니다.");
            return;
        }

        // 테마가 아직 적용되지 않았으면 Temperate 기본 테마 로드
        if (currentTheme == null)
        {
#if UNITY_EDITOR
            currentTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Temperate_Theme.asset");
#endif
        }

        foreach (Terrain t in terrains)
        {
            if (t == null || t.terrainData == null) continue;
            TerrainData tData = t.terrainData;

            int prototypeCount = tData.detailPrototypes != null ? tData.detailPrototypes.Length : 0;
            if (prototypeCount == 0 && currentTheme != null)
            {
                currentTheme.ApplyThemeToTerrain(t, season, globalSnowAmount);
                prototypeCount = tData.detailPrototypes != null ? tData.detailPrototypes.Length : 0;
            }

            if (prototypeCount == 0)
            {
                Debug.LogWarning($"[TotalWarTerrainManager] '{t.name}'에 등록된 잔디 프로토타입이 없습니다.");
                continue;
            }

            // Unity 6 호환: DetailScatterMode를 CoverageMode로 명시
            tData.SetDetailScatterMode(DetailScatterMode.CoverageMode);

            int detailRes = tData.detailResolution;
            int activeLayers = Mathf.Min(8, prototypeCount);
            long totalPlantedInstances = 0;

            // 대표 잔디 4~8종을 골고루 분산 살포
            for (int layer = 0; layer < activeLayers; layer++)
            {
                int[,] map = new int[detailRes, detailRes];
                float seedOffset = layer * 17.31f;

                // 야생화(꽃) 레이어는 잔디밭 사이에 5~15% 수준으로 은은하게 피어나도록 차별화
                var protoTex = (tData.detailPrototypes != null && layer < tData.detailPrototypes.Length) ? tData.detailPrototypes[layer].prototypeTexture : null;
                bool isFlower = (protoTex != null && protoTex.name.Contains("flower"));
                float layerCoverage = isFlower ? (coverage * 0.45f) : coverage;
                int layerMinDensity = isFlower ? 5 : minDensity;
                int layerMaxDensity = isFlower ? 18 : maxDensity;

                for (int y = 0; y < detailRes; y++)
                {
                    float normY = (float)y / detailRes;
                    for (int x = 0; x < detailRes; x++)
                    {
                        float normX = (float)x / detailRes;
                        float slope = tData.GetSteepness(normX, normY);

                        // 지정된 경사도 이하의 모든 완경사/중경사 지면에 살포
                        if (slope <= maxSlope)
                        {
                            // 펄린 노이즈로 자연스러운 패치형 잔디 군락 형성
                            float noise = Mathf.PerlinNoise(normX * 12f + seedOffset, normY * 12f + seedOffset);
                            if (noise > (1.0f - layerCoverage))
                            {
                                // Unity 6 CoverageMode: 꽃은 은은하게, 잔디는 빽빽하게 살포
                                float slopeFactor = 1.0f - Mathf.Clamp01(slope / maxSlope);
                                int density = Mathf.Clamp(Mathf.RoundToInt(noise * layerMaxDensity * (0.6f + 0.4f * slopeFactor)), layerMinDensity, layerMaxDensity);
                                map[y, x] = density;
                                totalPlantedInstances += density;
                            }
                        }
                    }
                }
                tData.SetDetailLayer(0, 0, layer, map);
            }

            // 렌더링 가시거리 및 밀도 최대치 보장 (유니티 에디터/인게임 즉각 렌더링)
            t.detailObjectDistance = 250f;
            t.detailObjectDensity = 1.0f;
            t.drawTreesAndFoliage = true;
            ApplyWavingSettings(t);
            t.Flush();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tData);
            UnityEditor.EditorUtility.SetDirty(t);
#endif
            Debug.Log($"<color=#00FF88><b>[TotalWarTerrainManager] '{t.name}'에 {activeLayers}종 디테일 잔디 자동 살포 완료! (총 밀도: {totalPlantedInstances:N0}, 밀도범위: {minDensity}~{maxDensity})</b></color>");
        }

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    /// <summary>
    /// 터레인에 살포된 모든 디테일 잔디 맵을 지웁니다.
    /// </summary>
    public void ClearDetailGrass()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null || targetTerrain.terrainData == null) return;

        TerrainData tData = targetTerrain.terrainData;
        int detailRes = tData.detailResolution;
        for (int layer = 0; layer < tData.detailPrototypes.Length; layer++)
        {
            int[,] emptyMap = new int[detailRes, detailRes];
            tData.SetDetailLayer(0, 0, layer, emptyMap);
        }
        targetTerrain.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tData);
        UnityEditor.EditorUtility.SetDirty(targetTerrain);
        UnityEditor.SceneView.RepaintAll();
#endif
        Debug.Log("[TotalWarTerrainManager] 터레인의 모든 디테일 잔디가 깨끗이 제거되었습니다.");
    }

    /// <summary>
    /// 터레인의 경사도를 계산하여 평지에는 잔디 레이어, 가파른 절벽에는 암반 레이어를 자동 페인팅합니다.
    /// </summary>
    public void AutoPaintSplatmap(float slopeThreshold = 35f)
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null || targetTerrain.terrainData == null) return;

        if (currentTheme == null)
        {
#if UNITY_EDITOR
            currentTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<TotalWarTerrainTheme>("Assets/Settings/TerrainThemes/Temperate_Theme.asset");
#endif
        }

        TerrainData tData = targetTerrain.terrainData;
        int layerCount = tData.terrainLayers != null ? tData.terrainLayers.Length : 0;

        if (layerCount < 2 && currentTheme != null)
        {
            ApplyCurrentTheme();
            layerCount = tData.terrainLayers != null ? tData.terrainLayers.Length : 0;
        }

        if (layerCount < 2)
        {
            Debug.LogWarning("[TotalWarTerrainManager] 터레인 레이어가 2개 미만입니다. 기후 테마 프리셋(.asset)을 인스펙터에 연결하세요.");
            return;
        }

        int alphaRes = tData.alphamapResolution;
        float[,,] splat = new float[alphaRes, alphaRes, layerCount];
        int cliffIndex = Mathf.Min(1, layerCount - 1);
        int stoneIndex = layerCount >= 3 ? 2 : cliffIndex;

        for (int y = 0; y < alphaRes; y++)
        {
            float normY = (float)y / alphaRes;
            for (int x = 0; x < alphaRes; x++)
            {
                float normX = (float)x / alphaRes;
                float slope = tData.GetSteepness(normX, normY);

                float grassW = 0f;
                float stoneW = 0f;
                float rockW = 0f;

                if (slope < 24f)
                {
                    grassW = 1.0f;
                }
                else if (slope < slopeThreshold)
                {
                    float t = (slope - 24f) / Mathf.Max(1f, slopeThreshold - 24f);
                    grassW = 1.0f - t;
                    stoneW = t;
                }
                else
                {
                    float t = Mathf.Clamp01((slope - slopeThreshold) / 10f);
                    stoneW = 1.0f - t;
                    rockW = t;
                }

                float total = grassW + stoneW + rockW;
                if (total > 0f)
                {
                    splat[y, x, 0] = grassW / total;
                    splat[y, x, cliffIndex] = rockW / total;
                    if (layerCount >= 3) splat[y, x, stoneIndex] = stoneW / total;
                }
                else
                {
                    splat[y, x, 0] = 1.0f;
                }
            }
        }

        tData.SetAlphamaps(0, 0, splat);
        targetTerrain.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tData);
        UnityEditor.EditorUtility.SetDirty(targetTerrain);
        UnityEditor.SceneView.RepaintAll();
#endif
        Debug.Log($"<color=#00FF88><b>[TotalWarTerrainManager] 평지/경사면 잔디 및 절벽(경사도 {slopeThreshold}° 이상) 암반 레이어 자동 페인팅 완료!</b></color>");
    }
}