using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 캠페인 지도(MainMap), 로딩(Loading), 오픈월드 심리스 전투(MainBattle) 3대 씬 간의 전환과
    /// 대륙 SSOT 데이터(WorldMeta, WorldSourceCache, MasterNetworks) 및 사전 합성 섹터 캐시를 중개합니다.
    /// </summary>
    public static class OpenWorldSceneLoader
    {
        public const string MainMapSceneName = "MainMap";
        public const string LoadingSceneName = "Loading";
        public const string MainBattleSceneName = "MainBattle";

        public static string WorldRootFolder = "Assets/WorldData";
        public static string ActiveContinentId = "ThreeKingdoms_China";

        public static string NextTargetSceneName = MainBattleSceneName;
        public static string LoadingHeaderText = "전장 지형 데이터를 불러오는 중...";
        public static string LoadingDetailText = "대륙 좌표 및 3x3 섹터 지형·수계·도로망을 동기화하고 있습니다.";

        // 씬 전환 간 재사용되는 대륙 SSOT 런타임 캐시
        public static WorldMetaData CachedMeta { get; private set; }
        public static WorldSourceCacheData CachedSourceCache { get; private set; }
        public static MasterNetworkData CachedNetworks { get; private set; }
        public static string LoadedContinentId { get; private set; }

        // Loading 씬에서 사전 합성한 3x3 섹터 데이터 버퍼
        public static readonly Dictionary<(int sectorX, int sectorZ), SectorBinaryData> PreSynthesizedSectors
            = new Dictionary<(int, int), SectorBinaryData>();

        /// <summary>
        /// 지정된 대륙의 메타·참조 캐시·마스터 네트워크가 메모리에 올라와 있도록 보장합니다.
        /// </summary>
        public static bool EnsureContinentLoaded(string continentId = null, bool forceReload = false)
        {
            string targetId = string.IsNullOrEmpty(continentId) ? ActiveContinentId : continentId;
            if (!forceReload && CachedMeta != null && LoadedContinentId == targetId)
            {
                return true;
            }

            string folder = GetActiveWorldFolder(targetId);
            WorldMetaData meta = SectorBinaryIO.LoadWorldMeta(folder);
            if (meta == null)
            {
                Debug.LogWarning($"[OpenWorldSceneLoader] 대륙 메타데이터를 찾을 수 없습니다: {folder}/WorldMeta.json");
                return false;
            }

            ActiveContinentId = targetId;
            LoadedContinentId = targetId;
            CachedMeta = meta;
            CachedSourceCache = SectorBinaryIO.LoadSourceCache(folder);
            CachedNetworks = SectorBinaryIO.LoadMasterNetworks(folder) ?? new MasterNetworkData();
            return true;
        }

        public static string GetActiveWorldFolder(string continentId = null)
        {
            string targetId = string.IsNullOrEmpty(continentId) ? ActiveContinentId : continentId;
            return SectorBinaryIO.GetWorldFolder(WorldRootFolder, targetId);
        }

        /// <summary>
        /// Loading 씬을 경유해 오픈월드 전투 씬(MainBattle)으로 진입합니다.
        /// </summary>
        public static void LoadBattleViaLoadingScene(BattleTransitionContext context, string headerText = null)
        {
            if (context != null)
            {
                BattleTransitionContext.ActiveContext = context;
                if (!string.IsNullOrEmpty(context.continentId))
                {
                    ActiveContinentId = context.continentId;
                }
            }

            PreSynthesizedSectors.Clear();
            NextTargetSceneName = MainBattleSceneName;
            LoadingHeaderText = string.IsNullOrEmpty(headerText) ? "전장 전개 중 — 3x3 오픈월드 지형 동기화" : headerText;
            LoadingDetailText = context != null
                ? $"교전 좌표 {context.encounterCoord} · 방위 {context.approachAngleDegrees:F0}도"
                : "기본 전장 좌표를 준비하고 있습니다.";

            SceneManager.LoadScene(LoadingSceneName);
        }

        /// <summary>
        /// Loading 씬을 경유해 캠페인 전략 지도 씬(MainMap)으로 복귀합니다.
        /// </summary>
        public static void LoadMainMapViaLoadingScene(string headerText = null)
        {
            PreSynthesizedSectors.Clear();
            NextTargetSceneName = MainMapSceneName;
            LoadingHeaderText = string.IsNullOrEmpty(headerText) ? "대륙 전략 지도로 복귀하는 중..." : headerText;
            LoadingDetailText = "전선 좌표 및 군단 상태를 대륙 지도에 반영하고 있습니다.";

            SceneManager.LoadScene(LoadingSceneName);
        }
    }
}
