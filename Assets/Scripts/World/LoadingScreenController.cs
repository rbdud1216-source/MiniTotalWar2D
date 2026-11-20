using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// Loading 씬에서 대륙 SSOT 데이터 로드 및 전투 대상 3x3 섹터 사전 합성을 프레임 분할로 처리하고
    /// 진행률 게이지·전술 팁 UI를 갱신한 뒤 목표 씬(MainMap 또는 MainBattle)으로 비동기 전환합니다.
    /// </summary>
    public class LoadingScreenController : MonoBehaviour
    {
        [Header("UI 참조 (OpenWorldSceneBuilder 자동 연결)")]
        public Text titleText;
        public Text statusText;
        public Text tipText;
        public Image progressFillImage;
        public Text progressPercentText;

        [Header("로딩 옵션")]
        [Tooltip("씬 전환 직전 완료 게이지 확인용 최소 대기 시간(초)")]
        [Range(0f, 1.5f)]
        public float minimumDisplaySeconds = 0.25f;

        private static readonly string[] LoadingTips = new string[]
        {
            "전술 팁: 114.2km x 89.6km 대륙 어디서든 교전이 발생하면 해당 지점의 3x3 섹터가 즉시 실체화됩니다.",
            "전술 팁: 전투 중 부대를 맵 끝으로 계속 행군시키면 새로운 지형이 끊김 없이 연속으로 스트리밍됩니다.",
            "전술 팁: 고사리와 덤불이 우거진 숲 구역(Foliage) 안에 정지하면 적에게 탐지되는 거리가 25m로 줄어듭니다.",
            "전술 팁: Tab 키를 눌러 3D 자유 시점과 2D 탑다운 전술 지도 시점을 자유롭게 오갈 수 있습니다.",
            "전술 팁: 강 여울목이나 습지에서는 기병의 돌격 보너스가 무력화되므로 도하 전 정찰이 중요합니다."
        };

        private IEnumerator Start()
        {
            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(OpenWorldSceneLoader.LoadingHeaderText)
                    ? "전장 데이터를 준비하는 중..."
                    : OpenWorldSceneLoader.LoadingHeaderText;
            }

            if (tipText != null)
            {
                tipText.text = LoadingTips[Random.Range(0, LoadingTips.Length)];
            }

            SetProgress(0.05f, OpenWorldSceneLoader.LoadingDetailText);
            yield return null;

            string targetScene = string.IsNullOrEmpty(OpenWorldSceneLoader.NextTargetSceneName)
                ? OpenWorldSceneLoader.MainMapSceneName
                : OpenWorldSceneLoader.NextTargetSceneName;

            // 1단계: 대륙 메타데이터 및 WorldSourceCache.bin 로드 (0.05 -> 0.25)
            SetProgress(0.15f, $"대륙 마스터 데이터 로드 중 ({OpenWorldSceneLoader.ActiveContinentId})...");
            bool loaded = OpenWorldSceneLoader.EnsureContinentLoaded(OpenWorldSceneLoader.ActiveContinentId);
            SetProgress(0.25f, loaded ? "대륙 참조 캐시 로드 완료" : "대륙 데이터를 찾을 수 없어 기본값으로 진행합니다.");
            yield return null;

            // 2단계: 목표가 MainBattle인 경우 진입 좌표 기준 3x3(9개 섹터) 사전 합성 (0.25 -> 0.72)
            if (targetScene == OpenWorldSceneLoader.MainBattleSceneName && loaded)
            {
                var meta = OpenWorldSceneLoader.CachedMeta;
                var cache = OpenWorldSceneLoader.CachedSourceCache;
                var networks = OpenWorldSceneLoader.CachedNetworks;
                string worldFolder = OpenWorldSceneLoader.GetActiveWorldFolder(meta.continentId);

                if (BattleTransitionContext.ActiveContext == null)
                {
                    BattleTransitionContext.ActiveContext = CreateDefaultBattleContext(meta);
                }

                WorldCoord centerCoord = BattleTransitionContext.ActiveContext.encounterCoord;
                OpenWorldSceneLoader.PreSynthesizedSectors.Clear();

                int built = 0;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int sx = Mathf.Clamp(centerCoord.sectorX + dx, 0, Mathf.Max(0, meta.sectorCountX - 1));
                        int sz = Mathf.Clamp(centerCoord.sectorZ + dz, 0, Mathf.Max(0, meta.sectorCountZ - 1));
                        var key = (sx, sz);

                        if (!OpenWorldSceneLoader.PreSynthesizedSectors.ContainsKey(key))
                        {
                            SectorBinaryData sector = ContinentSectorSynthesizer.LoadOrSynthesizeSector(
                                worldFolder, meta, cache, networks, sx, sz);
                            OpenWorldSceneLoader.PreSynthesizedSectors[key] = sector;
                        }

                        built++;
                        float p = Mathf.Lerp(0.25f, 0.72f, built / 9f);
                        SetProgress(p, $"3x3 전장 섹터 지형 합성 중... ({sx}, {sz}) [{built}/9]");
                        yield return null;
                    }
                }
            }

            if (minimumDisplaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(minimumDisplaySeconds);
            }

            // 3단계: 비동기 씬 로드 (0.72 -> 1.00)
            SetProgress(0.75f, $"{targetScene} 씬 초기화 중...");
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(targetScene);
            if (asyncOp == null)
            {
                SetProgress(1f, $"[오류] '{targetScene}' 씬을 Build Settings에서 찾을 수 없습니다.");
                yield break;
            }

            while (!asyncOp.isDone)
            {
                float norm = Mathf.Clamp01(asyncOp.progress / 0.9f);
                SetProgress(Mathf.Lerp(0.75f, 1.0f, norm), $"{targetScene} 씬 전개 중... ({Mathf.RoundToInt(norm * 100f)}%)");
                yield return null;
            }
        }

        private void SetProgress(float normalized, string detail)
        {
            normalized = Mathf.Clamp01(normalized);
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = normalized;
            }
            if (progressPercentText != null)
            {
                progressPercentText.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            }
            if (statusText != null && !string.IsNullOrEmpty(detail))
            {
                statusText.text = detail;
            }
        }

        private static BattleTransitionContext CreateDefaultBattleContext(WorldMetaData meta)
        {
            int sx = Mathf.Clamp(Mathf.RoundToInt(meta.sectorCountX * 0.52f), 1, Mathf.Max(1, meta.sectorCountX - 2));
            int sz = Mathf.Clamp(Mathf.RoundToInt(meta.sectorCountZ * 0.56f), 1, Mathf.Max(1, meta.sectorCountZ - 2));
            return new BattleTransitionContext
            {
                continentId = meta.continentId,
                encounterCoord = new WorldCoord(sx, sz, meta.sectorSizeMeters * 0.5f, meta.sectorSizeMeters * 0.5f),
                approachAngleDegrees = 0f,
                season = TacticalForestZone.Season.Summer,
                windAngle = 45f,
                windStrength = 0.55f,
                snowAmount = 0f,
                returnToCampaignOnFinish = true
            };
        }
    }
}
