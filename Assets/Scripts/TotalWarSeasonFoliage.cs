using UnityEngine;

namespace MiniTotalWar
{
    public enum TotalWarSeason
    {
        Spring = 0, // 봄: 싱그러운 연두 풀잎 & 화사한 연분홍/연보라 야생화
        Summer = 1, // 여름: 짙은 녹음의 암녹색 풀잎 & 선명한 보라색 야생화 (원작 사진 1:1)
        Autumn = 2, // 가을: 황금빛/갈색 단풍 억새 & 주황색/갈색 이삭 씨앗머리
        Winter = 3  // 겨울: 차가운 서리와 눈발을 머금은 회갈색 마른풀 & 은회색 봉오리
    }

    /// <summary>
    /// 🍂 [삼탈워 사계절 식생 동적 전환 컴포넌트]
    /// - 인스펙터에서 계절(Season)을 변경하면 실시간으로 계절별 공식 텍스처, 머티리얼, 꽃 색상이 1:1로 전환됩니다.
    /// - Pure ECS 및 GameObject 하이브리드 모드 모두와 호환됩니다.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TotalWar/TotalWar Season Foliage")]
    public class TotalWarSeasonFoliage : MonoBehaviour
    {
        [Header("🌱 현재 전장 계절 (Season)")]
        [SerializeField] private TotalWarSeason _currentSeason = TotalWarSeason.Summer;

        public TotalWarSeason CurrentSeason
        {
            get => _currentSeason;
            set
            {
                if (_currentSeason != value)
                {
                    _currentSeason = value;
                    ApplySeason(_currentSeason);
                }
            }
        }

        private void OnEnable()
        {
            ApplySeason(_currentSeason);
        }

        private void OnValidate()
        {
            ApplySeason(_currentSeason);
        }

        /// <summary>
        /// 지정된 계절에 맞추어 렌더러 머티리얼 및 시각 효과를 즉시 갱신합니다.
        /// </summary>
        public void ApplySeason(TotalWarSeason season)
        {
            _currentSeason = season;
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            string matPath = GetSeasonMaterialPath(season);
#if UNITY_EDITOR
            Material seasonMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (seasonMat != null)
            {
                foreach (var r in renderers)
                {
                    r.sharedMaterial = seasonMat;
                }
            }
#endif
        }

        public static string GetSeasonMaterialPath(TotalWarSeason season)
        {
            switch (season)
            {
                case TotalWarSeason.Spring:
                    return "Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassSwath_Spring_Mat.mat";
                case TotalWarSeason.Summer:
                    return "Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassSwath_Summer_Mat.mat";
                case TotalWarSeason.Autumn:
                    return "Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassSwath_Autumn_Mat.mat";
                case TotalWarSeason.Winter:
                    return "Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassSwath_Winter_Mat.mat";
                default:
                    return "Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassSwath_Summer_Mat.mat";
            }
        }
    }
}

