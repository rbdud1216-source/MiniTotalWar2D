using UnityEngine;
using System.Collections.Generic;

namespace MiniTotalWar
{
    /// <summary>
    /// 🌍 [삼국지 토탈워 전장 10대 대형 생태계 바이옴 테마]
    /// 숲이나 부시존처럼 생태계 테마를 선택하면, 해당 환경에서 서식하는 대표 3D 식생(지피 잔디 + 풀포기 + 이삭 + 고사리군 + 꽃/관목)
    /// 18~30종이 한꺼번에 자동 등록됩니다.
    /// </summary>
    public enum GrassBiomeTheme
    {
        [InspectorName("🌾 1. 중원 대평원 20종 (Central Plains Megapack)")]
        CentralPlains20,
        [InspectorName("🌲 2. 원시림 수림하층 고사리 20종 (Deep Forest Fern Undergrowth)")]
        DeepForest20,
        [InspectorName("🌴 3. 남만 열대밀림 22종 (Nanman Tropical Jungle)")]
        NanmanJungle22,
        [InspectorName("🌾 4. 강남 수변갈대·연꽃 습지 20종 (Jiangnan River Wetlands)")]
        JiangnanWetland20,
        [InspectorName("🏜️ 5. 서량 사막 건조황무지 18종 (Arid Liangzhou Wasteland)")]
        AridWasteland18,
        [InspectorName("❄️ 6. 하북/요동 한랭설원 18종 (Cold Steppe & Tundra)")]
        ColdSteppe18,
        [InspectorName("🌸 7. 화사한 봄꽃 만개들판 22종 (Blooming Flower Meadows)")]
        BloomingMeadows22,
        [InspectorName("🌾 8. 추수철 황금빛 농경지 20종 (Golden Harvest Farmlands)")]
        HarvestFarmlands20,
        [InspectorName("⛰️ 9. 험준한 산악 암반고원 18종 (Rocky Mountain Highlands)")]
        RockyHighlands18,
        [InspectorName("🌟 10. 364종 올인원 하이퍼 30종 (All-Inclusive 30-Pack)")]
        AllInclusive30,
        [InspectorName("🛠️ 사용자 커스텀 (Custom Selection)")]
        CustomSelection,

        // 하위 호환용 레거시 별칭
        TemperatePlains = CentralPlains20,
        SubtropicalJungle = NanmanJungle22,
        AridWasteland = AridWasteland18,
        ColdSteppe = ColdSteppe18,
        BloomingMeadows = BloomingMeadows22
    }

    /// <summary>
    /// 🌾 [삼국지 토탈워 3D 풀포기 & 이삭 커스텀 설정 구조체]
    /// 인스펙터에서 풀의 종류, 규격, 밀도 및 서식 지형 레이어를 자유롭게 제어합니다.
    /// </summary>
    [System.Serializable]
    public class RadialGrassConfig
    {
        [Tooltip("풀포기 고유 이름 / 레이블")]
        public string displayName = "Thick_Grassclump_Summer";

        [Tooltip("식생 3D OBJ 모델 파일명 (Assets/.../Shrubs/ 또는 DetailGrass/ 내)")]
        public string modelFile = "Shrub_Grassclump_Thick_A.obj";

        [Tooltip("전용 아틀라스 텍스처 파일명 (Assets/.../Textures/ 내 PNG)")]
        public string albedoFile = "shrub_grassclump_su_base_colour.png";

        [Tooltip("전용 노멀맵 파일명")]
        public string normalFile = "shrub_grassclump_normal.png";

        [Tooltip("활성화 여부 (체크 해제 시 전장 살포에서 제외)")]
        public bool isEnabled = true;

        [Header("📐 규격 (Width & Height)")]
        [Range(0.2f, 3.0f)] public float minWidth = 0.9f;
        [Range(0.2f, 3.0f)] public float maxWidth = 1.35f;
        [Range(0.2f, 3.0f)] public float minHeight = 0.85f;
        [Range(0.2f, 3.0f)] public float maxHeight = 1.3f;
        [Tooltip("상하 반전 (거꾸로 자란 하수형 풀을 하늘로 똑바로 세우기)")]
        public bool flipY = false;

        [Header("🌱 살포 밀도 & 군락 희소도")]
        [Range(0.05f, 0.95f)]
        [Tooltip("군락 형성 노이즈 임계치 (낮을수록 전역 살포, 높을수록 듬성듬성 패치 형태)")]
        public float noiseThreshold = 0.20f;

        [Range(1, 100)] public int minDensity = 40;
        [Range(1, 150)] public int maxDensity = 90;

        [Header("🌍 서식 지형 레이어 (스플랫맵 필터링)")]
        [Tooltip("푸른 잔디 지형에 서식 허용")]
        public bool growOnGrass = true;

        [Tooltip("흙/자갈/산길 지형에 서식 허용")]
        public bool growOnDirt = false;

        [Tooltip("절벽/암반 바위 지형에 서식 허용")]
        public bool growOnRock = false;

        [Header("⛰️ 지형 경사 밀착 & 높이 오프셋")]
        [Range(0.0f, 1.0f)]
        [Tooltip("지형 경사면 순응도 (1: 지형 표면에 100% 밀착, 0: 완전 수직 고정)")]
        public float alignToGround = 1.0f;

        [Range(-0.5f, 1.5f)]
        [Tooltip("지면 높이 띄움 오프셋 (바닥에 파묻히지 않도록 정점 높이를 미세 조절, 기본: +0.05m, 피벗 중심 메쉬는 +0.58m)")]
        public float groundOffset = 0.05f;

        public RadialGrassConfig Clone()
        {
            return new RadialGrassConfig()
            {
                displayName = this.displayName,
                modelFile = this.modelFile,
                albedoFile = this.albedoFile,
                normalFile = this.normalFile,
                isEnabled = this.isEnabled,
                minWidth = this.minWidth,
                maxWidth = this.maxWidth,
                minHeight = this.minHeight,
                maxHeight = this.maxHeight,
                flipY = this.flipY,
                noiseThreshold = this.noiseThreshold,
                minDensity = this.minDensity,
                maxDensity = this.maxDensity,
                growOnGrass = this.growOnGrass,
                growOnDirt = this.growOnDirt,
                growOnRock = this.growOnRock,
                alignToGround = this.alignToGround,
                groundOffset = this.groundOffset
            };
        }

        // ======================================================================================
        // 🌍 [삼국지 토탈워 원작 고증 10대 대형 생태계 바이옴 팩] (각 18~30종 대규모 풀팩)
        // ======================================================================================

        /// <summary>
        /// 풀포기 설정 객체를 한 줄로 생성하는 최적화 팩토리 헬퍼
        /// </summary>
        public static RadialGrassConfig Item(string name, string model, string tex, string norm, float wMin, float wMax, float hMin, float hMax, float noise, int dMin, int dMax, float align = 0.5f, float offset = 0.05f, bool grass = true, bool dirt = false, bool rock = false)
        {
            return new RadialGrassConfig()
            {
                displayName = name,
                modelFile = model,
                albedoFile = tex,
                normalFile = norm,
                minWidth = wMin, maxWidth = wMax,
                minHeight = hMin, maxHeight = hMax,
                noiseThreshold = noise,
                minDensity = dMin, maxDensity = dMax,
                alignToGround = align,
                groundOffset = offset,
                growOnGrass = grass, growOnDirt = dirt, growOnRock = rock
            };
        }

        /// <summary>
        /// 지정한 기후 생태계에 서식하는 대표 3D 식생(지피 잔디 + 풀포기 + 이삭 + 고사리군 + 꽃/관목) 18~30종을 일괄 생성합니다.
        /// </summary>
        public static List<RadialGrassConfig> GetBiomeFoliagePack(GrassBiomeTheme theme)
        {
            switch (theme)
            {
                case GrassBiomeTheme.CentralPlains20:
                    return Pack_CentralPlains20();

                case GrassBiomeTheme.DeepForest20:
                    return Pack_DeepForest20();

                case GrassBiomeTheme.NanmanJungle22:
                    return Pack_NanmanJungle22();

                case GrassBiomeTheme.JiangnanWetland20:
                    return Pack_JiangnanWetland20();

                case GrassBiomeTheme.AridWasteland18:
                    return Pack_AridWasteland18();

                case GrassBiomeTheme.ColdSteppe18:
                    return Pack_ColdSteppe18();

                case GrassBiomeTheme.BloomingMeadows22:
                    return Pack_BloomingMeadows22();

                case GrassBiomeTheme.HarvestFarmlands20:
                    return Pack_HarvestFarmlands20();

                case GrassBiomeTheme.RockyHighlands18:
                    return Pack_RockyHighlands18();

                case GrassBiomeTheme.AllInclusive30:
                    return Pack_AllInclusive30();

                default:
                    return Pack_CentralPlains20();
            }
        }

        public static List<RadialGrassConfig> GetDefaultFoliageList()
        {
            return Pack_CentralPlains20();
        }

        // --- 🌾 1. 중원 대평원 완벽 표준 식생 팩 (1~4번 보존 + 4대 유형 정제 메쉬) ---
        private static List<RadialGrassConfig> Pack_CentralPlains20() => new List<RadialGrassConfig>()
        {
            // ① [1자 잔디 띠 10열 교차 군락]: 1~4번 기존 검증 메쉬 보존 + 온대 사계절 군락
            Item("🌾 온대 순수 잔디 다방향 군락", "TotalWar_MultiDirectionalGrassCluster_CustomMesh.asset", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.05f, 65, 95, 0.25f, -0.015f),
            Item("🌸 분홍 꽃망울 다방향 군락 (Photo 1)", "TotalWar_FlowerCluster_PinkBuds_Photo1_Mesh.asset", "tem_grass_flower_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.15f, 15, 30, 0.25f, -0.015f),
            Item("💜 연보라 꽃송이 다방향 군락 (Photo 2)", "TotalWar_FlowerCluster_PurplePetals_Photo2_Mesh.asset", "tem_grass_flower_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.15f, 15, 30, 0.25f, -0.015f),
            Item("🧡 주황 꽃이삭 다방향 군락 (Photo 3)", "TotalWar_FlowerCluster_OrangeSpikes_Photo3_Mesh.asset", "sbt_grass0_flower_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.15f, 15, 30, 0.25f, -0.015f),
            Item("🌱 중원 온대 봄 지피 군락", "TotalWar_GrassCluster_Tem_Spring_Mesh.asset", "tem_grass_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.10f, 50, 85, 0.25f, -0.015f),
            Item("🌿 중원 온대 여름 지피 군락", "TotalWar_GrassCluster_Tem_Summer_Mesh.asset", "tem_grass_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.12f, 45, 80, 0.25f, -0.015f),
            // ② [3D 입체 풀포기]: 중심 피벗 방사형 부채꼴
            Item("🌿 중원 방사형 3D 풀포기 (봄)", "TotalWar_RadialFan3D_Spring_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.90f, 1.15f, 0.22f, 20, 45, 0.20f, -0.015f),
            // ③ [키 큰 갈대/이삭]: 0.8~1.2m 직립 포인트
            Item("🎋 중원 키 큰 갈대 이삭 (봄)", "TotalWar_TallReed3D_Spring_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.15f, 0.90f, 1.15f, 0.30f, 10, 25, 0.12f, -0.015f)
            Item("🍂 중원 온대 마른 갈대/가을 풀밭", "TotalWar_GrassCluster_Tem_Autumn_Mesh.asset", "tem_grass_au_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.25f, 0.90f, 1.15f, 0.16f, 35, 65, 0.25f, -0.015f),
            // ② [3D 입체 풀포기]: 중심 피벗 방사형 부채꼴 (실제 Clump 메쉬 + 봄/여름 머티리얼)
            Item("🌿 중원 방사형 3D 풀포기 (봄)", "TotalWar_RadialFan3D_Clump_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.25f, 0.95f, 1.20f, 0.20f, 25, 50, 0.20f, -0.015f),
            Item("🌿 중원 방사형 3D 풀포기 (여름)", "TotalWar_RadialFan3D_Clump_Mesh.asset", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.25f, 0.95f, 1.20f, 0.22f, 25, 45, 0.20f, -0.015f),
            // ③ [키 큰 갈대/이삭]: 0.8~1.2m 직립 포인트 (실제 Plume/Spike 메쉬)
            Item("🎋 중원 키 큰 갈대 이삭 (봄)", "TotalWar_TallReed3D_Plume_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.25f, 0.25f, 15, 35, 0.12f, -0.015f),
            Item("🌾 중원 키 큰 이삭 줄기 (Spike)", "TotalWar_TallReed3D_Spike_Mesh.asset", "tem_grass_au_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.25f, 0.26f, 15, 35, 0.12f, -0.015f),
            // ④ [산기슭/수풀 관목 고사리]: 중원 구릉지 저층 관목
            Item("🌲 중원 기슭 검고사리 관목", "TotalWar_ForestFern_Sword_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.95f, 1.30f, 0.95f, 1.20f, 0.22f, 20, 40, 0.30f, -0.015f),
            Item("🌲 중원 기슭 왕고사리 덤불", "TotalWar_ForestFern_Royal_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.95f, 1.30f, 0.95f, 1.20f, 0.24f, 15, 35, 0.30f, -0.015f)
        };

        // --- 🌲 2. 원시림 수림하층 지피 (음지 고사리 4종 + 수림 전용 잔디 군락 + 은방울꽃/진달래 + 3D 풀포기) ---
        private static List<RadialGrassConfig> Pack_DeepForest20() => new List<RadialGrassConfig>()
        {
            Item("🌲 음지 검고사리 부채꼴 (여름)", "TotalWar_ForestFern_Sword_Summer_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.95f, 1.35f, 0.95f, 1.20f, 0.08f, 45, 80, 0.35f, -0.015f),
            Item("🌲 음지 왕고사리 부채꼴 (여름)", "TotalWar_ForestFern_Royal_Summer_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.95f, 1.35f, 0.95f, 1.20f, 0.10f, 45, 80, 0.35f, -0.015f),
            Item("🌲 음지 우드고사리 부채꼴 (여름)", "TotalWar_ForestFern_Wood_Summer_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.90f, 1.25f, 0.90f, 1.15f, 0.12f, 35, 70, 0.35f, -0.015f),
            Item("🌲 음지 공작고사리 바닥 화초", "TotalWar_ForestFern_Maidenhair_Summer_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.90f, 1.25f, 0.90f, 1.15f, 0.14f, 35, 70, 0.35f, -0.015f),
            Item("🌲 수림 3갈래 양치류 (여름)", "TotalWar_ForestFern_ForestTriple_Summer_Mesh.asset", "tem_forest_grass_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.12f, 35, 70, 0.30f, -0.015f),
            Item("🌱 온대 수림 전용 지피 군락 (여름)", "TotalWar_GrassCluster_TemForest_Summer_Mesh.asset", "tem_forest_grass_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.08f, 55, 90, 0.25f, -0.015f),
            Item("🌱 한랭 수림 전용 지피 군락 (여름)", "TotalWar_GrassCluster_CldForest_Summer_Mesh.asset", "cld_forest_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.12f, 45, 80, 0.25f, -0.015f),
            Item("💜 수림 보라 들꽃 군락", "TotalWar_FlowerCluster_ForestViolet_Tem_Mesh.asset", "tem_forest_grass_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.16f, 20, 40, 0.25f, -0.015f),
            Item("🌸 수림 은방울꽃 3D 포기", "TotalWar_RadialFan3D_Convallaria_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.18f, 20, 45, 0.20f, -0.015f),
            Item("🌺 수림 진달래/철쭉 꽃덤불", "TotalWar_RadialFan3D_Rhododendron_Spring_Mesh.asset", "shrub_rhododendron_sp_base_colour.png", "shrub_rhododendron_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.22f, 15, 35, 0.20f, -0.015f),
            Item("🌿 수림 방사형 3D 풀포기 (여름)", "TotalWar_RadialFan3D_Summer_Mesh.asset", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.20f, 25, 50, 0.20f, -0.015f),
            Item("🎋 수림 키 큰 이삭 풀 (여름)", "TotalWar_TallReed3D_Summer_Mesh.asset", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.15f, 0.90f, 1.15f, 0.28f, 12, 28, 0.15f, -0.015f)
        };

        // --- 🌴 3. 남만 열대밀림 (열대/아열대 지피 + 열대 들꽃 + 야자고사리/파초일엽 + 코끼리풀/토란초) ---
        private static List<RadialGrassConfig> Pack_NanmanJungle22() => new List<RadialGrassConfig>()
        {
            Item("🌴 남만 열대 초원 지피 군락", "TotalWar_GrassCluster_Tro_Summer_Mesh.asset", "tro_grass_base_colour.png", "shrub_grassclump_normal.png", 1.00f, 1.35f, 0.95f, 1.20f, 0.06f, 65, 95, 0.25f, -0.015f),
            Item("🌴 남만 열대 정글 수림 지피 군락", "TotalWar_GrassCluster_TroForest_Summer_Mesh.asset", "tro_forest_grass_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.08f, 55, 85, 0.25f, -0.015f),
            Item("🌴 남만 아열대 여름 지피 군락", "TotalWar_GrassCluster_Sbt_Summer_Mesh.asset", "sbt_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 1.00f, 1.35f, 0.95f, 1.20f, 0.10f, 55, 85, 0.25f, -0.015f),
            Item("🧡 남만 주황 꽃이삭 군락 (Photo 3)", "TotalWar_FlowerCluster_OrangeSpikes_Photo3_Mesh.asset", "sbt_grass0_flower_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.15f, 20, 40, 0.25f, -0.015f),
            Item("💙 남만 청보라 들꽃 군락", "TotalWar_FlowerCluster_BluePetals_Sbt_Mesh.asset", "sbt_grass0_flower_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.16f, 20, 40, 0.25f, -0.015f),
            Item("🍀 남만 열대 클로버 지피 군락", "TotalWar_FlowerCluster_TropicalClover_Tro_Mesh.asset", "tro_grass_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.14f, 25, 50, 0.25f, -0.015f),
            Item("🌲 남만 열대 야자고사리 부채꼴", "TotalWar_ForestFern_TropicalPalm_Summer_Mesh.asset", "3k_tropical_shrubs_base_colour.png", "3k_tropical_shrubs_normal.png", 1.00f, 1.40f, 1.00f, 1.25f, 0.12f, 35, 70, 0.35f, -0.015f),
            Item("🌲 남만 열대 파초일엽 활엽고사리", "TotalWar_ForestFern_TropicalStrap_Summer_Mesh.asset", "3k_tropical_shrubs_base_colour.png", "3k_tropical_shrubs_normal.png", 1.00f, 1.40f, 1.00f, 1.25f, 0.14f, 35, 70, 0.35f, -0.015f),
            Item("🌿 남만 열대 토란 활엽초 포기", "TotalWar_RadialFan3D_TropicalBigleaf_Mesh.asset", "3k_tropical_shrubs_base_colour.png", "3k_tropical_shrubs_normal.png", 1.00f, 1.35f, 1.00f, 1.20f, 0.16f, 30, 60, 0.20f, -0.015f),
            Item("🌿 남만 열대 코끼리풀 포기", "TotalWar_RadialFan3D_ElephantGrass_Mesh.asset", "3k_tropical_shrubs_base_colour.png", "3k_tropical_shrubs_normal.png", 1.00f, 1.35f, 1.00f, 1.20f, 0.18f, 30, 60, 0.20f, -0.015f),
            Item("🎋 남만 열대 대왕 코끼리풀 줄기", "TotalWar_TallReed3D_ElephantTall_Mesh.asset", "3k_tropical_shrubs_base_colour.png", "3k_tropical_shrubs_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.24f, 18, 35, 0.12f, -0.015f),
            Item("🎋 남만 수변 물억새 갈대", "TotalWar_TallReed3D_RiverPlume_Mesh.asset", "sbt_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.25f, 18, 35, 0.12f, -0.015f)
        };

        // --- 🌾 4. 강남 수변 습지 (수변 물가 군락 + 수생 연꽃/연잎 + 초록 수전 벼 + 물억새 갈대) ---
        private static List<RadialGrassConfig> Pack_JiangnanWetland20() => new List<RadialGrassConfig>()
        {
            Item("🌊 강남 수변 습지 지피 군락", "TotalWar_GrassCluster_Sbt_Wetland_Mesh.asset", "dyn_sbt_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.06f, 65, 95, 0.25f, -0.015f),
            Item("🌱 강남 아열대 수변 군락", "TotalWar_GrassCluster_Sbt_Summer_Mesh.asset", "sbt_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.10f, 55, 85, 0.25f, -0.015f),
            Item("🌾 강남 수전 초록 벼 3D 포기", "TotalWar_RadialFan3D_RicePaddy_Green_Mesh.asset", "gen_rice_paddy0_base_colour.png", "gen_rice_paddy0_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.14f, 40, 75, 0.20f, -0.015f),
            Item("🪷 강남 수생 분홍 연꽃 & 연잎 군락", "TotalWar_LotusCluster_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 1.15f, 1.45f, 0.95f, 1.20f, 0.16f, 25, 55, 0.15f, -0.015f),
            Item("🎋 수변 키 큰 물억새 갈대", "TotalWar_TallReed3D_RiverPlume_Mesh.asset", "sbt_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.18f, 25, 55, 0.12f, -0.015f),
            Item("🎋 수전 초록 벼 이삭 줄기", "TotalWar_TallReed3D_RiceEar_Green_Mesh.asset", "gen_rice_paddy0_base_colour.png", "gen_rice_paddy0_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.20f, 25, 50, 0.12f, -0.015f),
            Item("🎋 수변 억새 이삭 줄기 군락", "TotalWar_TallReed3D_Spike_Mesh.asset", "tem_grass_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.22f, 20, 45, 0.12f, -0.015f),
            Item("🍃 강남 다원 차나무 덤불", "TotalWar_RadialFan3D_TeaBush_Summer_Mesh.asset", "shrub_tea_base_colour.png", "shrub_tea_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.22f, 20, 45, 0.20f, -0.015f),
            Item("🌲 수변 물가 검고사리", "TotalWar_ForestFern_Sword_Summer_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.90f, 1.25f, 0.90f, 1.15f, 0.22f, 20, 45, 0.35f, -0.015f)
        };

        // --- 🏜️ 5. 서북 건조황무지 (건조 지피 군락 + 건조 수림 잔디 + 건조 보리/밀 + 마른 갈대) ---
        private static List<RadialGrassConfig> Pack_AridWasteland18() => new List<RadialGrassConfig>()
        {
            Item("🏜️ 서북 건조 봄 지피 군락", "TotalWar_GrassCluster_Ard_Spring_Mesh.asset", "ard_grass0_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.10f, 50, 85, 0.25f, -0.015f, true, true),
            Item("🏜️ 서북 건조 여름 지피 군락", "TotalWar_GrassCluster_Ard_Summer_Mesh.asset", "ard_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.12f, 45, 80, 0.25f, -0.015f, true, true),
            Item("🏜️ 서북 건조 가을 지피 군락", "TotalWar_GrassCluster_Ard_Autumn_Mesh.asset", "ard_grass0_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.15f, 45, 80, 0.25f, -0.015f, true, true),
            Item("🏜️ 서북 건조 수림 지피 군락", "TotalWar_GrassCluster_ArdForest_Autumn_Mesh.asset", "ard_forest_grass0_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.16f, 40, 75, 0.25f, -0.015f, true, true),
            Item("🏜️ 서북 건조 겨울 지피 군락", "TotalWar_GrassCluster_Ard_Winter_Mesh.asset", "ard_grass0_wi_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.85f, 1.05f, 0.18f, 35, 70, 0.25f, -0.015f, true, true, true),
            Item("🌿 건조 황무지 3D 풀포기", "TotalWar_RadialFan3D_Arid_Mesh.asset", "shrub_grassclump_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.90f, 1.10f, 0.22f, 25, 50, 0.20f, -0.015f, true, true),
            Item("🌾 건조 황무지 메마른 보리 포기", "TotalWar_RadialFan3D_Wheat_Dry_Mesh.asset", "sbt_agri1_base_colour.png", "sbt_agri1_normal.png", 0.90f, 1.20f, 0.90f, 1.10f, 0.24f, 20, 45, 0.20f, -0.015f, true, true),
            Item("🎋 건조 황무지 마른 갈대", "TotalWar_TallReed3D_Arid_Mesh.asset", "shrub_grassclump_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.15f, 0.90f, 1.10f, 0.28f, 12, 28, 0.12f, -0.015f, true, true)
        };

        // --- ❄️ 6. 하북/요동 한랭설원 (한랭 설원/수림 지피 군락 + 한랭 보라꽃 + 설원 3D 풀포기 + 겨울 고사리) ---
        private static List<RadialGrassConfig> Pack_ColdSteppe18() => new List<RadialGrassConfig>()
        {
            Item("❄️ 한랭 설원 겨울 지피 군락", "TotalWar_GrassCluster_Cld_Winter_Mesh.asset", "cld_grass0_wi_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.10f, 55, 85, 0.25f, -0.015f, true, true),
            Item("❄️ 한랭 수림 겨울 지피 군락", "TotalWar_GrassCluster_CldForest_Winter_Mesh.asset", "cld_forest_grass0_wi_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.12f, 50, 80, 0.25f, -0.015f, true, true),
            Item("❄️ 한랭 북방 가을 지피 군락", "TotalWar_GrassCluster_Cld_Autumn_Mesh.asset", "cld_grass0_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.14f, 45, 80, 0.25f, -0.015f, true, true),
            Item("❄️ 한랭 북방 봄 지피 군락", "TotalWar_GrassCluster_Cld_Spring_Mesh.asset", "cld_grass0_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.16f, 40, 75, 0.25f, -0.015f),
            Item("💜 한랭 설원 보라 들꽃 군락", "TotalWar_FlowerCluster_ColdViolet_Cld_Mesh.asset", "cld_grass0_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.90f, 1.10f, 0.18f, 20, 40, 0.25f, -0.015f),
            Item("❄️ 온대 겨울 서리 지피 군락", "TotalWar_GrassCluster_Tem_Winter_Mesh.asset", "tem_grass_wi_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.18f, 40, 75, 0.25f, -0.015f, true, true),
            Item("🌿 설원 눈 덮인 3D 풀포기", "TotalWar_RadialFan3D_Winter_Mesh.asset", "shrub_grassclump_wi_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.90f, 1.10f, 0.22f, 20, 45, 0.20f, -0.015f, true, true),
            Item("🌲 설원 겨울 검고사리", "TotalWar_ForestFern_Sword_Winter_Mesh.asset", "shrubs_ferns_wi_base_colour.png", "shrubs_ferns_normal.png", 0.90f, 1.20f, 0.90f, 1.10f, 0.25f, 20, 40, 0.35f, -0.015f, true, true)
        };

        // --- 🌸 7. 화사한 봄꽃 만개들판 (봄철 잔디 군락 + 8종 야생화 군락 + 진달래/목련 + 봄 3D 풀포기) ---
        private static List<RadialGrassConfig> Pack_BloomingMeadows22() => new List<RadialGrassConfig>()
        {
            Item("🌾 온대 순수 잔디 다방향 군락", "TotalWar_MultiDirectionalGrassCluster_CustomMesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.05f, 65, 95, 0.25f, -0.015f),
            Item("🌱 온대 봄 신록 지피 군락", "TotalWar_GrassCluster_Tem_Spring_Mesh.asset", "tem_grass_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.08f, 55, 85, 0.25f, -0.015f),
            Item("🌸 분홍 꽃망울 다방향 군락 (Photo 1)", "TotalWar_FlowerCluster_PinkBuds_Photo1_Mesh.asset", "tem_grass_flower_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.12f, 25, 45, 0.25f, -0.015f),
            Item("💜 연보라 꽃송이 다방향 군락 (Photo 2)", "TotalWar_FlowerCluster_PurplePetals_Photo2_Mesh.asset", "tem_grass_flower_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.12f, 25, 45, 0.25f, -0.015f),
            Item("🧡 주황 꽃이삭 다방향 군락 (Photo 3)", "TotalWar_FlowerCluster_OrangeSpikes_Photo3_Mesh.asset", "sbt_grass0_flower_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.12f, 25, 45, 0.25f, -0.015f),
            Item("💙 청보라 들꽃 다방향 군락", "TotalWar_FlowerCluster_BluePetals_Sbt_Mesh.asset", "sbt_grass0_flower_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.14f, 20, 40, 0.25f, -0.015f),
            Item("💎 푸른 수레국화 들꽃 군락", "TotalWar_FlowerCluster_Cornflower_Sbt_Mesh.asset", "sbt_grass_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.14f, 20, 40, 0.25f, -0.015f),
            Item("🌼 노란 들국화 야생화 군락", "TotalWar_FlowerCluster_YellowDaisy_Sbt_Mesh.asset", "sbt_grass_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.14f, 20, 40, 0.25f, -0.015f),
            Item("🤍 하얀 구절초 들꽃 군락", "TotalWar_FlowerCluster_WhiteAster_Sbt_Mesh.asset", "sbt_grass_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.14f, 20, 40, 0.25f, -0.015f),
            Item("🌺 봄 진달래/철쭉 꽃덤불", "TotalWar_RadialFan3D_Rhododendron_Spring_Mesh.asset", "shrub_rhododendron_sp_base_colour.png", "shrub_rhododendron_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.22f, 15, 35, 0.20f, -0.015f),
            Item("💮 봄 백목련 꽃덤불", "TotalWar_RadialFan3D_Magnolia_Spring_Mesh.asset", "shrub_magnolia_sp_base_colour.png", "shrub_magnolia_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.22f, 15, 35, 0.20f, -0.015f),
            Item("🌿 봄철 방사형 3D 풀포기", "TotalWar_RadialFan3D_Spring_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.90f, 1.15f, 0.20f, 20, 40, 0.20f, -0.015f),
            Item("🎋 봄철 키 큰 이삭 갈대", "TotalWar_TallReed3D_Spring_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.15f, 0.90f, 1.15f, 0.28f, 10, 25, 0.12f, -0.015f)
        };

        // --- 🌾 8. 추수철 황금빛 농경지 (가을 황금 지피 + 수전 벼 포기/이삭 + 황금 보리/밀 + 억새 갈대) ---
        private static List<RadialGrassConfig> Pack_HarvestFarmlands20() => new List<RadialGrassConfig>()
        {
            Item("🌾 온대 가을 황금 지피 군락", "TotalWar_GrassCluster_Tem_Autumn_Mesh.asset", "tem_grass_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.06f, 65, 95, 0.25f, -0.015f, true, true),
            Item("🌾 아열대 가을 논둑 지피 군락", "TotalWar_GrassCluster_Sbt_Autumn_Mesh.asset", "sbt_grass0_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.10f, 55, 85, 0.25f, -0.015f, true, true),
            Item("🌾 건조 가을 밭고랑 지피 군락", "TotalWar_GrassCluster_Ard_Autumn_Mesh.asset", "ard_grass0_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.90f, 1.10f, 0.14f, 45, 80, 0.25f, -0.015f, true, true),
            Item("🌾 추수철 수전 황금 벼 3D 포기", "TotalWar_RadialFan3D_RicePaddy_Gold_Mesh.asset", "gen_rice_paddy0_au_base_colour.png", "gen_rice_paddy0_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.14f, 40, 75, 0.20f, -0.015f, true, true),
            Item("🌾 추수철 황금 보리/밀 3D 포기", "TotalWar_RadialFan3D_Wheat_Gold_Mesh.asset", "sbt_agri1_base_colour.png", "sbt_agri1_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.16f, 35, 70, 0.20f, -0.015f, true, true),
            Item("🌿 추수철 황금 기장 3D 포기", "TotalWar_RadialFan3D_Harvest_Mesh.asset", "shrub_rice_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.20f, 0.18f, 30, 60, 0.20f, -0.015f, true, true),
            Item("🎋 추수철 수전 황금 벼 이삭 줄기", "TotalWar_TallReed3D_RiceEar_Gold_Mesh.asset", "gen_rice_paddy0_au_base_colour.png", "gen_rice_paddy0_normal.png", 0.95f, 1.20f, 0.95f, 1.20f, 0.20f, 25, 50, 0.12f, -0.015f, true, true),
            Item("🎋 추수철 황금 보리/밀 이삭 줄기", "TotalWar_TallReed3D_BarleyEar_Gold_Mesh.asset", "sbt_agri1_base_colour.png", "sbt_agri1_normal.png", 0.95f, 1.20f, 0.95f, 1.20f, 0.22f, 20, 45, 0.12f, -0.015f, true, true),
            Item("🎋 가을 키 큰 억새 갈대", "TotalWar_TallReed3D_Autumn_Mesh.asset", "shrub_grassclump_au_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.20f, 0.95f, 1.20f, 0.24f, 20, 45, 0.12f, -0.015f, true, true),
            Item("🌲 가을 단풍 왕고사리", "TotalWar_ForestFern_Royal_Autumn_Mesh.asset", "shrubs_ferns_au_base_colour.png", "shrubs_ferns_normal.png", 0.90f, 1.20f, 0.90f, 1.15f, 0.25f, 15, 35, 0.35f, -0.015f, true, true)
        };

        // --- ⛰️ 9. 산악 암반 험지 (암벽 틈새 건조/한랭 지피 군락 + 고사리 + 3D 풀포기) ---
        private static List<RadialGrassConfig> Pack_RockyHighlands18() => new List<RadialGrassConfig>()
        {
            Item("⛰️ 산악 암반 건조 지피 군락", "TotalWar_GrassCluster_Ard_Summer_Mesh.asset", "ard_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.85f, 1.05f, 0.14f, 40, 75, 0.25f, -0.015f, true, true, true),
            Item("⛰️ 고산 한랭 바위 지피 군락", "TotalWar_GrassCluster_Cld_Autumn_Mesh.asset", "cld_grass0_au_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.85f, 1.05f, 0.16f, 40, 70, 0.25f, -0.015f, true, true, true),
            Item("⛰️ 산악 온대 틈새 지피 군락", "TotalWar_GrassCluster_Tem_Autumn_Mesh.asset", "tem_grass_au_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.85f, 1.05f, 0.18f, 35, 65, 0.25f, -0.015f, true, true, true),
            Item("🌿 암반 틈새 3D 풀포기", "TotalWar_RadialFan3D_Arid_Mesh.asset", "shrub_grassclump_base_colour.png", "shrub_grassclump_normal.png", 0.85f, 1.15f, 0.85f, 1.05f, 0.22f, 20, 45, 0.20f, -0.015f, true, true, true),
            Item("🌲 바위 그늘 검고사리", "TotalWar_ForestFern_Sword_Autumn_Mesh.asset", "shrubs_ferns_au_base_colour.png", "shrubs_ferns_normal.png", 0.85f, 1.15f, 0.85f, 1.10f, 0.24f, 20, 40, 0.35f, -0.015f, true, true, true),
            Item("🎋 고산 바람막이 갈대", "TotalWar_TallReed3D_Arid_Mesh.asset", "shrub_grassclump_base_colour.png", "shrub_grassclump_normal.png", 0.85f, 1.10f, 0.85f, 1.05f, 0.28f, 10, 25, 0.12f, -0.015f, true, true, true)
        };

        // --- 👑 10. 삼탈워 4대 유형 올인원 식생 (지피 잔디 + 야생화 + 3D 풀포기 + 농작물 + 연꽃/갈대 + 고사리 총망라) ---
        private static List<RadialGrassConfig> Pack_AllInclusive30() => new List<RadialGrassConfig>()
        {
            Item("🌾 온대 순수 잔디 다방향 군락", "TotalWar_MultiDirectionalGrassCluster_CustomMesh.asset", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.05f, 65, 95, 0.25f, -0.015f),
            Item("🌱 온대 봄 지피 군락", "TotalWar_GrassCluster_Tem_Spring_Mesh.asset", "tem_grass_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.08f, 55, 85, 0.25f, -0.015f),
            Item("🌴 아열대 여름 지피 군락", "TotalWar_GrassCluster_Sbt_Summer_Mesh.asset", "sbt_grass0_su_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.10f, 50, 80, 0.25f, -0.015f),
            Item("🌸 분홍 꽃망울 다방향 군락 (Photo 1)", "TotalWar_FlowerCluster_PinkBuds_Photo1_Mesh.asset", "tem_grass_flower_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.14f, 18, 35, 0.25f, -0.015f),
            Item("💜 연보라 꽃송이 다방향 군락 (Photo 2)", "TotalWar_FlowerCluster_PurplePetals_Photo2_Mesh.asset", "tem_grass_flower_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.14f, 18, 35, 0.25f, -0.015f),
            Item("🧡 주황 꽃이삭 다방향 군락 (Photo 3)", "TotalWar_FlowerCluster_OrangeSpikes_Photo3_Mesh.asset", "sbt_grass0_flower_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.14f, 18, 35, 0.25f, -0.015f),
            Item("💎 푸른 수레국화 들꽃 군락", "TotalWar_FlowerCluster_Cornflower_Sbt_Mesh.asset", "sbt_grass_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.15f, 15, 30, 0.25f, -0.015f),
            Item("🌼 노란 들국화 야생화 군락", "TotalWar_FlowerCluster_YellowDaisy_Sbt_Mesh.asset", "sbt_grass_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.15f, 0.15f, 15, 30, 0.25f, -0.015f),
            Item("🌿 방사형 3D 풀포기 (봄)", "TotalWar_RadialFan3D_Spring_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.18f, 25, 50, 0.20f, -0.015f),
            Item("🌾 수전 초록 벼 3D 포기", "TotalWar_RadialFan3D_RicePaddy_Green_Mesh.asset", "gen_rice_paddy0_base_colour.png", "gen_rice_paddy0_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.20f, 25, 50, 0.20f, -0.015f),
            Item("🌾 추수철 황금 보리/밀 포기", "TotalWar_RadialFan3D_Wheat_Gold_Mesh.asset", "sbt_agri1_base_colour.png", "sbt_agri1_normal.png", 0.95f, 1.25f, 0.95f, 1.15f, 0.22f, 20, 45, 0.20f, -0.015f),
            Item("🎋 키 큰 갈대 이삭 (봄)", "TotalWar_TallReed3D_Spring_Mesh.asset", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", 0.90f, 1.20f, 0.95f, 1.20f, 0.25f, 15, 30, 0.12f, -0.015f),
            Item("🪷 수생 분홍 연꽃 & 연잎 군락", "TotalWar_LotusCluster_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 1.15f, 1.45f, 0.95f, 1.20f, 0.26f, 15, 30, 0.15f, -0.015f),
            Item("🌲 음지 검고사리 부채꼴", "TotalWar_ForestFern_Sword_Summer_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.20f, 25, 50, 0.35f, -0.015f),
            Item("🌲 음지 왕고사리 부채꼴", "TotalWar_ForestFern_Royal_Summer_Mesh.asset", "shrubs_ferns_su_base_colour.png", "shrubs_ferns_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.22f, 25, 50, 0.35f, -0.015f),
            Item("🌲 열대 야자고사리 부채꼴", "TotalWar_ForestFern_TropicalPalm_Summer_Mesh.asset", "3k_tropical_shrubs_base_colour.png", "3k_tropical_shrubs_normal.png", 0.95f, 1.30f, 0.95f, 1.15f, 0.24f, 20, 45, 0.35f, -0.015f)
        };

        // --- 🌿 초원 평야 잔디류 ---
        public static RadialGrassConfig Preset_ThickGrassclumpA() => new RadialGrassConfig()
        {
            displayName = "🌿 풍성한 풀포기 A (Thick Clump A)",
            modelFile = "Shrub_Grassclump_Thick_A.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.9f, maxWidth = 1.35f, minHeight = 0.85f, maxHeight = 1.3f,
            noiseThreshold = 0.15f, minDensity = 45, maxDensity = 95,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_ThickGrassclumpB() => new RadialGrassConfig()
        {
            displayName = "🌿 넓은 풀포기 B (Thick Clump B)",
            modelFile = "Shrub_Grassclump_Thick_B.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.95f, maxWidth = 1.4f, minHeight = 0.8f, maxHeight = 1.25f,
            noiseThreshold = 0.25f, minDensity = 40, maxDensity = 85,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_ThickGrassclumpC() => new RadialGrassConfig()
        {
            displayName = "🌿 촘촘한 풀포기 C (Thick Clump C)",
            modelFile = "Shrub_Grassclump_Thick_C.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.8f, maxWidth = 1.2f, minHeight = 0.75f, maxHeight = 1.15f,
            noiseThreshold = 0.20f, minDensity = 45, maxDensity = 90,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_TallGrassA() => new RadialGrassConfig()
        {
            displayName = "🌿 키 큰 들풀 A (Tall Grass A)",
            modelFile = "Shrub_Grassclump_Tall_A.obj",
            albedoFile = "shrub_grassclump_sp_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.85f, maxWidth = 1.3f, minHeight = 0.9f, maxHeight = 1.4f,
            noiseThreshold = 0.35f, minDensity = 30, maxDensity = 70,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_TallGrassB() => new RadialGrassConfig()
        {
            displayName = "🌿 키 큰 들풀 B (Tall Grass B)",
            modelFile = "Shrub_Grassclump_Tall_B.obj",
            albedoFile = "shrub_grassclump_sp_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.9f, maxWidth = 1.35f, minHeight = 0.95f, maxHeight = 1.45f,
            noiseThreshold = 0.40f, minDensity = 25, maxDensity = 65,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_FlatHerbA() => new RadialGrassConfig()
        {
            displayName = "🌱 지면 납작 들풀 A (Flat Herb A)",
            modelFile = "Shrub_Flat_A.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.9f, maxWidth = 1.35f, minHeight = 0.4f, maxHeight = 0.75f,
            noiseThreshold = 0.20f, minDensity = 40, maxDensity = 80,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_FlatHerbB() => new RadialGrassConfig()
        {
            displayName = "🌱 지면 납작 들풀 B (Flat Herb B)",
            modelFile = "Shrub_Flat_B.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.8f, maxWidth = 1.25f, minHeight = 0.35f, maxHeight = 0.7f,
            noiseThreshold = 0.25f, minDensity = 35, maxDensity = 75,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        // --- 🌾 꽃대 이삭 & 롱 글래스 & 거대 갈대/곡초류 ---
        public static RadialGrassConfig Preset_TallSeedheadA() => new RadialGrassConfig()
        {
            displayName = "🌾 주황빛 꽃대 이삭 A (Tall Seedhead A - 영화적 고증)",
            modelFile = "Shrub_Grassclump_Tall_Seedhead_A.obj",
            albedoFile = "shrub_grassclump_au_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.85f, maxWidth = 1.3f, minHeight = 0.95f, maxHeight = 1.5f,
            noiseThreshold = 0.45f, minDensity = 25, maxDensity = 60,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_TallSeedheadB() => new RadialGrassConfig()
        {
            displayName = "🌾 황금빛 꽃대 이삭 B (Tall Seedhead B)",
            modelFile = "Shrub_Grassclump_Tall_Seedhead_B.obj",
            albedoFile = "shrub_grassclump_au_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.8f, maxWidth = 1.25f, minHeight = 0.9f, maxHeight = 1.45f,
            noiseThreshold = 0.50f, minDensity = 20, maxDensity = 55,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_TallSeedheadC() => new RadialGrassConfig()
        {
            displayName = "🌾 붉은빛 꽃대 이삭 C (Tall Seedhead C)",
            modelFile = "Shrub_Grassclump_Tall_Seedhead_C.obj",
            albedoFile = "shrub_grassclump_au_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.75f, maxWidth = 1.2f, minHeight = 0.85f, maxHeight = 1.35f,
            noiseThreshold = 0.55f, minDensity = 20, maxDensity = 50,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_DrySeedheadsA() => new RadialGrassConfig()
        {
            displayName = "🌾 건조 야생 이삭 A (Dry Seedheads A)",
            modelFile = "Shrub_Grassclump_Dry_Seedheads_A.obj",
            albedoFile = "shrub_grassclump_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.85f, maxWidth = 1.25f, minHeight = 0.85f, maxHeight = 1.35f,
            noiseThreshold = 0.55f, minDensity = 20, maxDensity = 50,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_ElephantGrassA() => new RadialGrassConfig()
        {
            displayName = "🌾 거대 야생 갈대 A (Elephant Grass A)",
            modelFile = "Shrub_ElephantGrass_A.obj",
            albedoFile = "shrub_elephantgrass_a_billboard_base_colour.png",
            normalFile = "shrub_elephantgrass_a_billboard_normal.png",
            minWidth = 1.1f, maxWidth = 1.6f, minHeight = 1.2f, maxHeight = 1.8f,
            noiseThreshold = 0.60f, minDensity = 15, maxDensity = 40,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_ElephantGrassB() => new RadialGrassConfig()
        {
            displayName = "🌾 키 큰 물가 갈대 B (Elephant Grass B)",
            modelFile = "Shrub_Elephantgrass_B.obj",
            albedoFile = "shrub_elephantgrass_a_billboard_base_colour.png",
            normalFile = "shrub_elephantgrass_a_billboard_normal.png",
            minWidth = 1.0f, maxWidth = 1.5f, minHeight = 1.1f, maxHeight = 1.7f,
            noiseThreshold = 0.65f, minDensity = 15, maxDensity = 35,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_WildMilletA() => new RadialGrassConfig()
        {
            displayName = "🌾 야생 기장/조 풀 A (Wild Millet A)",
            modelFile = "Shrub_Millet_A.obj",
            albedoFile = "shrub_grassclump_au_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.9f, maxWidth = 1.4f, minHeight = 1.0f, maxHeight = 1.6f,
            noiseThreshold = 0.55f, minDensity = 20, maxDensity = 45,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_WildRiceA() => new RadialGrassConfig()
        {
            displayName = "🌾 야생 벼풀/물억새 A (Wild Rice A)",
            modelFile = "Shrub_Rice_A.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.95f, maxWidth = 1.45f, minHeight = 0.9f, maxHeight = 1.5f,
            noiseThreshold = 0.50f, minDensity = 20, maxDensity = 50,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        // --- 🍂 서량 사막 & 마른 들풀류 ---
        public static RadialGrassConfig Preset_DryGrassA() => new RadialGrassConfig()
        {
            displayName = "🍂 거친 자연 들풀 A (Dry Grass A)",
            modelFile = "Shrub_Grassclump_Dry_A.obj",
            albedoFile = "shrub_grassclump_sp_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.9f, maxWidth = 1.3f, minHeight = 0.75f, maxHeight = 1.2f,
            noiseThreshold = 0.30f, minDensity = 35, maxDensity = 75,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_DryGrassB() => new RadialGrassConfig()
        {
            displayName = "🍂 거친 자연 들풀 B (Dry Grass B)",
            modelFile = "Shrub_Grassclump_Dry_B.obj",
            albedoFile = "shrub_grassclump_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.85f, maxWidth = 1.25f, minHeight = 0.7f, maxHeight = 1.15f,
            noiseThreshold = 0.35f, minDensity = 30, maxDensity = 70,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_SemidryGrassA() => new RadialGrassConfig()
        {
            displayName = "🍂 반건조 풀포기 A (Semidry Grass A)",
            modelFile = "Shrub_Grassclump_Semidry_A.obj",
            albedoFile = "shrub_grassclump_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.85f, maxWidth = 1.25f, minHeight = 0.8f, maxHeight = 1.25f,
            noiseThreshold = 0.40f, minDensity = 25, maxDensity = 65,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_ArdGrass0_1() => new RadialGrassConfig()
        {
            displayName = "🍂 서량 건조 풀포기 A (Dry Clump A)",
            modelFile = "Shrub_Grassclump_Dry_A.obj",
            albedoFile = "shrub_grassclump_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.85f, maxWidth = 1.3f, minHeight = 0.8f, maxHeight = 1.25f,
            noiseThreshold = 0.30f, minDensity = 30, maxDensity = 70,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        // --- ❄️ 북방 한랭 잔디류 ---
        public static RadialGrassConfig Preset_CldGrass0_1() => new RadialGrassConfig()
        {
            displayName = "❄️ 북방 한대 잔디 A (Cold Clump A)",
            modelFile = "Shrub_Grassclump_Tall_C.obj",
            albedoFile = "shrub_grassclump_sp_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.85f, maxWidth = 1.25f, minHeight = 0.75f, maxHeight = 1.2f,
            noiseThreshold = 0.30f, minDensity = 35, maxDensity = 75,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        // --- 🌸 화사한 봄꽃 야생화 & 개화 수풀류 ---
        public static RadialGrassConfig Preset_MagnoliaSpring() => new RadialGrassConfig()
        {
            displayName = "🌸 봄철 만개 목련꽃 (Spring Magnolia Flower)",
            modelFile = "Shrub_Magnolia_A.obj",
            albedoFile = "shrub_magnolia_sp_base_colour.png",
            normalFile = "shrub_magnolia_normal.png",
            minWidth = 0.85f, maxWidth = 1.35f, minHeight = 0.8f, maxHeight = 1.3f,
            noiseThreshold = 0.55f, minDensity = 15, maxDensity = 45,
            alignToGround = 0.45f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_RhododendronSpring() => new RadialGrassConfig()
        {
            displayName = "🌺 붉은 진달래/철쭉 꽃수풀 (Spring Rhododendron)",
            modelFile = "Shrub_Rhododendron_A.obj",
            albedoFile = "shrub_rhododendron_sp_base_colour.png",
            normalFile = "shrub_rhododendron_normal.png",
            minWidth = 0.8f, maxWidth = 1.25f, minHeight = 0.7f, maxHeight = 1.2f,
            noiseThreshold = 0.50f, minDensity = 15, maxDensity = 40,
            alignToGround = 0.45f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_LotusA() => new RadialGrassConfig()
        {
            displayName = "🪷 수변 연꽃 / 들꽃 A (Lotus A)",
            modelFile = "Shrub_Lotus_A.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.9f, maxWidth = 1.4f, minHeight = 0.6f, maxHeight = 1.1f,
            noiseThreshold = 0.55f, minDensity = 15, maxDensity = 40,
            alignToGround = 0.60f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        // --- 🌿 기후 환경별 대표 지피 디테일 잔디 (DetailGrass 238종 전수 복구 연동) ---
        public static RadialGrassConfig Preset_DetailGrassTemperate() => new RadialGrassConfig()
        {
            displayName = "🌱 온대 지피 잔디 융단 (Temperate Ground Carpet)",
            modelFile = "Tem_Grass0_1.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 1.1f, maxWidth = 1.6f, minHeight = 0.65f, maxHeight = 1.1f,
            noiseThreshold = 0.15f, minDensity = 45, maxDensity = 90,
            alignToGround = 1.0f, groundOffset = 0.04f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_DetailGrassSubtropical() => new RadialGrassConfig()
        {
            displayName = "🌱 아열대 윤기 지피 잔디 (Subtropical Ground Carpet)",
            modelFile = "Sbt_Grass0_1.obj",
            albedoFile = "shrub_grassclump_su_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 1.1f, maxWidth = 1.65f, minHeight = 0.7f, maxHeight = 1.15f,
            noiseThreshold = 0.15f, minDensity = 45, maxDensity = 90,
            alignToGround = 1.0f, groundOffset = 0.04f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_DetailGrassArid() => new RadialGrassConfig()
        {
            displayName = "🌱 서량 건조 지피 잔디 (Arid Ground Carpet)",
            modelFile = "Ard_Grass0_1.obj",
            albedoFile = "shrub_grassclump_sp_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 1.05f, maxWidth = 1.55f, minHeight = 0.6f, maxHeight = 1.05f,
            noiseThreshold = 0.20f, minDensity = 35, maxDensity = 80,
            alignToGround = 1.0f, groundOffset = 0.04f,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_DetailGrassAridRocky() => new RadialGrassConfig()
        {
            displayName = "🌱 사막 바위 지피 잔디 (Arid Rocky Carpet)",
            modelFile = "Ard_Grass1_1.obj",
            albedoFile = "shrub_grassclump_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.95f, maxWidth = 1.45f, minHeight = 0.55f, maxHeight = 0.95f,
            noiseThreshold = 0.25f, minDensity = 30, maxDensity = 70,
            alignToGround = 1.0f, groundOffset = 0.04f,
            growOnGrass = true, growOnDirt = true, growOnRock = true
        };

        public static RadialGrassConfig Preset_DetailGrassCold() => new RadialGrassConfig()
        {
            displayName = "🌱 한랭 침엽수림 지피 잔디 (Cold Forest Carpet)",
            modelFile = "Cld_Grass_Forest0_1.obj",
            albedoFile = "shrub_grassclump_wi_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 1.0f, maxWidth = 1.5f, minHeight = 0.6f, maxHeight = 1.0f,
            noiseThreshold = 0.15f, minDensity = 40, maxDensity = 85,
            alignToGround = 1.0f, groundOffset = 0.04f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_WinterGrassclumpA() => new RadialGrassConfig()
        {
            displayName = "❄️ 겨울철 억센 들풀포기 A (Winter Clump A)",
            modelFile = "Shrub_Grassclump_Winter_A.obj",
            albedoFile = "shrub_grassclump_wi_base_colour.png",
            normalFile = "shrub_grassclump_normal.png",
            minWidth = 0.9f, maxWidth = 1.35f, minHeight = 0.8f, maxHeight = 1.25f,
            noiseThreshold = 0.30f, minDensity = 30, maxDensity = 70,
            alignToGround = 0.55f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = true, growOnRock = false
        };

        public static RadialGrassConfig Preset_SwordFernA() => new RadialGrassConfig()
        {
            displayName = "🌲 칼잎 야생 고사리 A (Sword Fern A)",
            modelFile = "Shrub_Fern_Sword_A.obj",
            albedoFile = "shrubs_ferns_wi_base_colour.png",
            normalFile = "shrubs_ferns_normal.png",
            minWidth = 0.85f, maxWidth = 1.3f, minHeight = 0.75f, maxHeight = 1.2f,
            noiseThreshold = 0.40f, minDensity = 20, maxDensity = 50,
            alignToGround = 0.50f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        // --- 🌿 열대 관목 덤불 (구 노란 야생화: 정품 2048 고해상도 아틀라스 바인딩) ---
        public static RadialGrassConfig Preset_TropicalShrubFlowerA() => new RadialGrassConfig()
        {
            displayName = "🌿 열대 활엽 관목 덤불 (Tropical Shrub Flower A)",
            modelFile = "Shrub_Flower_A.obj",
            albedoFile = "3k_tropical_shrubs_base_colour.png",
            normalFile = "3k_tropical_shrubs_normal.png",
            minWidth = 0.85f, maxWidth = 1.35f, minHeight = 0.75f, maxHeight = 1.25f,
            noiseThreshold = 0.40f, minDensity = 20, maxDensity = 50,
            alignToGround = 0.45f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_YellowFlowerA() => Preset_TropicalShrubFlowerA();

        public static RadialGrassConfig Preset_YellowFlowerB() => new RadialGrassConfig()
        {
            displayName = "🌿 열대 활엽 관목 B (Tropical Shrub Flower B)",
            modelFile = "Shrub_Flower_B.obj",
            albedoFile = "3k_tropical_shrubs_base_colour.png",
            normalFile = "3k_tropical_shrubs_normal.png",
            minWidth = 0.85f, maxWidth = 1.35f, minHeight = 0.75f, maxHeight = 1.25f,
            noiseThreshold = 0.45f, minDensity = 20, maxDensity = 45,
            alignToGround = 0.45f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_ConvallariaA() => new RadialGrassConfig()
        {
            displayName = "🌼 청초한 은방울초 A (Convallaria A)",
            modelFile = "Shrub_Convallaria_A.obj",
            albedoFile = "3k_tropical_shrubs_base_colour.png",
            normalFile = "3k_tropical_shrubs_normal.png",
            minWidth = 0.65f, maxWidth = 1.05f, minHeight = 0.5f, maxHeight = 0.85f,
            noiseThreshold = 0.70f, minDensity = 10, maxDensity = 30,
            alignToGround = 0.50f, groundOffset = 0.05f,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        // --- 🌲 숲속 그늘 고사리 & 습지 식생 ---
        public static RadialGrassConfig Preset_WildFernA() => new RadialGrassConfig()
        {
            displayName = "🌲 울창한 왕고사리 A (Royal Fern A)",
            modelFile = "Shrub_Fern_Royal_A.obj",
            albedoFile = "shrubs_ferns_su_base_colour.png",
            normalFile = "shrubs_ferns_normal.png",
            minWidth = 0.8f, maxWidth = 1.15f, minHeight = 0.7f, maxHeight = 1.05f,
            noiseThreshold = 0.65f, minDensity = 15, maxDensity = 35,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_WildFernB() => new RadialGrassConfig()
        {
            displayName = "🌲 숲속 왕고사리 B (Royal Fern B)",
            modelFile = "Shrub_Fern_Royal_B.obj",
            albedoFile = "shrubs_ferns_su_base_colour.png",
            normalFile = "shrubs_ferns_normal.png",
            minWidth = 0.85f, maxWidth = 1.25f, minHeight = 0.75f, maxHeight = 1.15f,
            noiseThreshold = 0.60f, minDensity = 15, maxDensity = 40,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };

        public static RadialGrassConfig Preset_FishboneFernA() => new RadialGrassConfig()
        {
            displayName = "🌲 피시본 고사리 A (Fishbone Fern A)",
            modelFile = "Shrub_Fern_Fishbone_A.obj",
            albedoFile = "shrubs_ferns_su_base_colour.png",
            normalFile = "shrubs_ferns_normal.png",
            minWidth = 0.75f, maxWidth = 1.1f, minHeight = 0.65f, maxHeight = 0.95f,
            noiseThreshold = 0.65f, minDensity = 15, maxDensity = 35,
            growOnGrass = true, growOnDirt = false, growOnRock = false
        };
    }
}
