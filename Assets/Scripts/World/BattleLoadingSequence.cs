using Unity.Mathematics;
using UnityEngine;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 오픈월드 전투 씬(MainBattle.unity) 진입 부트스트래퍼입니다.
    /// BattleTransitionContext를 읽어 3x3 지형 스트리머, 해수면, 바람, 교전 진영 배치를 순서대로 초기화합니다.
    /// </summary>
    public class BattleLoadingSequence : MonoBehaviour
    {
        [Header("연동 컴포넌트 (미지정 시 자동 탐색)")]
        public SeamlessBattleWorldStreamer worldStreamer;
        public BattleManager battleManager;
        public CameraController cameraController;

        [Header("교전 배치 간격 (미터)")]
        public float armyEngagementDistance = 110f;

        private void Start()
        {
            if (worldStreamer == null) worldStreamer = SeamlessBattleWorldStreamer.Instance ?? FindFirstObjectByType<SeamlessBattleWorldStreamer>();
            if (battleManager == null) battleManager = BattleManager.Instance ?? FindFirstObjectByType<BattleManager>();
            if (cameraController == null && Camera.main != null) cameraController = Camera.main.GetComponent<CameraController>();

            BattleTransitionContext ctx = BattleTransitionContext.ActiveContext;
            if (ctx == null)
            {
                OpenWorldSceneLoader.EnsureContinentLoaded(OpenWorldSceneLoader.ActiveContinentId);
                WorldMetaData meta = OpenWorldSceneLoader.CachedMeta;
                int sx = (meta != null) ? Mathf.Clamp(Mathf.RoundToInt(meta.sectorCountX * 0.52f), 1, Mathf.Max(1, meta.sectorCountX - 2)) : 115;
                int sz = (meta != null) ? Mathf.Clamp(Mathf.RoundToInt(meta.sectorCountZ * 0.56f), 1, Mathf.Max(1, meta.sectorCountZ - 2)) : 98;
                float sectorSize = (meta != null) ? meta.sectorSizeMeters : WorldCoord.DefaultSectorSize;

                ctx = new BattleTransitionContext
                {
                    continentId = OpenWorldSceneLoader.ActiveContinentId,
                    encounterCoord = new WorldCoord(sx, sz, sectorSize * 0.5f, sectorSize * 0.5f),
                    approachAngleDegrees = 0f,
                    season = TacticalForestZone.Season.Summer,
                    windAngle = 45f,
                    windStrength = 0.55f,
                    snowAmount = 0f
                };
                BattleTransitionContext.ActiveContext = ctx;
            }

            // 1. 3x3 오픈월드 지형 및 수계 구축
            if (worldStreamer != null)
            {
                worldStreamer.continentId = ctx.continentId;
                worldStreamer.InitializeAtWorldCoord(ctx.encounterCoord, ctx.season, ctx.snowAmount);
            }

            WorldMetaData loadedMeta = OpenWorldSceneLoader.CachedMeta;

            // 2. 바다 수위 및 오픈월드 카메라 추적 활성화
            if (TotalWarSeaPlane.Instance != null && loadedMeta != null)
            {
                TotalWarSeaPlane.Instance.waterLevelY = loadedMeta.seaLevelY;
                TotalWarSeaPlane.Instance.followTargetCamera = true;
                TotalWarSeaPlane.Instance.ApplyWaterLevelPosition();
                if (TerrainHeightManager.HasInstance)
                {
                    TotalWarSeaPlane.Instance.ConfigureForWorldBounds(TerrainHeightManager.Instance.ActiveWorldBounds);
                }
            }

            // 3. 바람 및 기후 조명 동기화
            if (TotalWarWindManager.Instance != null)
            {
                TotalWarWindManager.Instance.windAngle = ctx.windAngle;
                TotalWarWindManager.Instance.windStrength = ctx.windStrength;
                TotalWarWindManager.Instance.UpdateWindShaderGlobals();
            }

            // 4. 교전 중심 로컬 좌표 산출 + 수면 위 안전 스폰 위치 보정
            Vector3 encounterLocal = (FloatingOriginManager.Instance != null)
                ? FloatingOriginManager.Instance.WorldToLocal(ctx.encounterCoord, 0f)
                : new Vector3(256f, 0f, 256f);

            float halfGap = armyEngagementDistance * 0.5f;
            float rad = ctx.approachAngleDegrees * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

            Vector3 playerCenter = FindDryLandNear(encounterLocal - dir * halfGap, loadedMeta);
            Vector3 enemyCenter = FindDryLandNear(encounterLocal + dir * halfGap, loadedMeta);
            float playerYaw = ctx.approachAngleDegrees;
            float enemyYaw = Mathf.Repeat(ctx.approachAngleDegrees + 180f, 360f);

            if (battleManager != null)
            {
                battleManager.ConfigureEncounterSpawn(playerCenter, playerYaw, enemyCenter, enemyYaw);
                battleManager.SpawnBattleScenario();
            }

            // 5. 카메라 초기 포커스 및 오픈월드 자유 이동 개방
            if (cameraController != null)
            {
                float groundY = TerrainHeightManager.SampleHeightFast(encounterLocal.x, encounterLocal.z);
                Vector3 camPos = playerCenter - dir * 55f + Vector3.up * 60f;
                camPos.y = Mathf.Max(camPos.y, groundY + 45f);
                cameraController.transform.position = camPos;
                cameraController.transform.rotation = Quaternion.Euler(42f, playerYaw, 0f);
                cameraController.SetRuntimeBounds(new Vector2(-99999f, -99999f), new Vector2(99999f, 99999f), 5f, 450f, enableBounds: false);
            }
        }

        private static Vector3 FindDryLandNear(Vector3 candidate, WorldMetaData meta)
        {
            float seaY = (meta != null) ? meta.seaLevelY : 2.0f;
            float h = TerrainHeightManager.SampleHeightFast(candidate.x, candidate.z);
            if (h > seaY + 0.35f)
            {
                candidate.y = h;
                return candidate;
            }

            // 교전 중심이 해안/강물 위라면 반경 150m 내 가장 가까운 육지 지점 탐색
            for (float r = 15f; r <= 150f; r += 15f)
            {
                for (int step = 0; step < 12; step++)
                {
                    float a = (step / 12f) * Mathf.PI * 2f;
                    float tx = candidate.x + Mathf.Cos(a) * r;
                    float tz = candidate.z + Mathf.Sin(a) * r;
                    float th = TerrainHeightManager.SampleHeightFast(tx, tz);
                    if (th > seaY + 0.35f)
                    {
                        return new Vector3(tx, th, tz);
                    }
                }
            }

            candidate.y = h;
            return candidate;
        }
    }
}
