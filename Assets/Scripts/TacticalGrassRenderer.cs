using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MiniTotalWar
{
    /// <summary>
    /// 🌱 [풀/식생 상태 열거형]
    /// </summary>
    public enum FoliageState : byte
    {
        Healthy = 0,   // 정상 자연 잔디
        Trampled = 1,  // 보병/기병/차량 통행으로 짓밟혀 누움
        Burning = 2,   // 화재로 연소 중
        AshDecal = 3   // 전소 완료된 잿더미 흔적
    }

    /// <summary>
    /// 🌿 [청크별 식생 바이옴 분류]
    /// </summary>
    public enum FoliageBiomeType : byte
    {
        PlainsCarpet = 0,   // 평원 지피 융단 (삼탈워 본편 납작 잔디, 기본형)
        ForestShade = 1,    // 숲속 그늘 (고사리 및 음지 지피)
        HighlandDry = 2,    // 고지대/구릉 마른풀 (마른 잡초와 듬성듬성한 들풀)
        RiverWetland = 3,   // 하천/습지대 갈대 (물가 수변풀)
        BarrenOrClear = 4   // 공터/암반/도로 (풀 0%)
    }

    /// <summary>
    /// 🏷️ [청크별 커스텀 식생 오버라이드 설정]
    /// </summary>
    [Serializable]
    public class ChunkFoliageOverride
    {
        public Vector2Int chunkCoord;
        public FoliageBiomeType biomeType = FoliageBiomeType.PlainsCarpet;
        [Range(0.0f, 2.0f)] public float densityMultiplier = 1.0f;

        [Tooltip("해당 청크에 배치할 특정 풀 모델 인덱스 목록 (비어있으면 바이옴 기본 풀 전체 살포)")]
        public List<int> allowedModelIndices = new List<int>();
    }

    /// <summary>
    /// 🌾 [3D 풀포기 프로토타입 페어]
    /// 메쉬와 인스턴싱 머티리얼, 스케일 범위 정의
    /// </summary>
    [Serializable]
    public class TacticalGrassPrototype
    {
        public string protoName = "Grass";
        public Mesh mesh;
        public Material material;
        [Range(0.3f, 3.0f)] public float minScale = 0.85f;
        [Range(0.3f, 3.0f)] public float maxScale = 1.25f;
        [Range(0.1f, 1.5f)] public float heightMultiplier = 0.42f; // Y축 높이 스케일 (0.42배: 솟구치지 않고 바닥에 납작한 융단 형성)
        [Range(-0.4f, 0.1f)] public float groundEmbedOffset = -0.08f; // 바닥 파묻힘 오프셋 (-0.08m: 피벗이 지면에 착 달라붙음)
        [Range(0.0f, 1.0f)] public float alignToGround = 0.45f; // 지형 경사면 법선 정렬 반영 비율 (0 = 수직 기립, 1 = 지표면 100% 일치)
        [Range(0.0f, 0.6f)] public float slopeSinkFactor = 0.28f; // 경사면 추가 침하 계수 (경사도 비례 피벗 파묻힘으로 잎 허공 부유 방지)
        [Range(0.0f, 1.0f)] public float densityThreshold = 0.15f;
        public FoliageBiomeType targetBiome = FoliageBiomeType.PlainsCarpet;

        [Header("⚔️ 전술 은폐, 이동 저항 & 인화성")]
        [Range(0.0f, 1.0f)] public float stealthCoverage = 0.0f; // 은폐 기여도 (0: 지피잔디 노출 -> 1.0: 빽빽한 이삭/고사리 완전 은신)
        [Range(0.5f, 1.0f)] public float speedMultiplier = 0.95f; // 이동속도 계수 (0.68: 거대 수풀 헤침 -> 0.96: 지피잔디 저항 미미)
        [Range(0.0f, 1.0f)] public float flammability = 0.5f; // 인화성 (0.1: 수분잔디 -> 1.0: 폭발착화 마른이삭/벼밭)
        public float burnDuration = 6.0f; // 연소 지속 시간 (초)
    }

    /// <summary>
    /// 📦 [전장 식생 공간 분할 청크 (32m x 32m)]
    /// 카메라 줌인 시 반경 내의 청크만 O(1)로 빠르게 쿼리하여 GPU 인스턴싱 드로우콜 실행
    /// </summary>
    public class TacticalGrassChunk
    {
        public Bounds bounds;
        public Vector2Int chunkCoord;
        public FoliageBiomeType biomeType = FoliageBiomeType.PlainsCarpet;
        public float densityMultiplier = 1.0f;
        public List<Vector3> worldPositions = new List<Vector3>();
        public List<FoliageState> states = new List<FoliageState>();

        // ⚔️ 전술 은폐, 이동 저항 및 화재 상태
        public float chunkStealthPower = 0.0f; // 청크 실효 은폐력 (0.0 ~ 1.0, 0.55 이상 시 완전 은신 제공)
        public float chunkSpeedMultiplier = 1.0f; // 청크 실효 이동속도 계수 (0.60 ~ 1.0)
        public float chunkFlammability = 0.5f; // 청크 평균 인화율
        public FoliageState chunkState = FoliageState.Healthy; // 청크 화재/물리 상태
        public float burnTimer = 0.0f; // 연소 경과 시간
        public GameObject fireVfxInstance = null; // 실시간 활성 화염 파티클 VFX 인스턴스

        // 프로토타입별 1,023개 단위의 인스턴스 행렬 배치 목록 (DrawMeshInstanced 한도 대응)
        public List<Matrix4x4[]>[] protoBatches;
        public int[] protoBatchCounts;

        public void InitializeBatches(int protoCount)
        {
            protoBatches = new List<Matrix4x4[]>[protoCount];
            protoBatchCounts = new int[protoCount];
            for (int i = 0; i < protoCount; i++)
            {
                protoBatches[i] = new List<Matrix4x4[]>();
            }
        }

        /// <summary>
        /// 특정 프로토타입의 모든 인스턴스 행렬을 평탄화된 단일 List로 반환합니다.
        /// </summary>
        public List<Matrix4x4> GetMatrixList(int protoIdx)
        {
            var result = new List<Matrix4x4>();
            if (protoBatches == null || protoIdx < 0 || protoIdx >= protoBatches.Length) return result;
            var bList = protoBatches[protoIdx];
            if (bList == null) return result;
            for (int b = 0; b < bList.Count; b++)
            {
                if (bList[b] != null) result.AddRange(bList[b]);
            }
            return result;
        }

        /// <summary>
        /// List&lt;Matrix4x4&gt; 목록을 바탕으로 1,023개 단위의 DrawMeshInstanced 배치 배열들을 즉시 재구성합니다.
        /// </summary>
        public void RebuildBatchesFromList(int protoIdx, List<Matrix4x4> mList)
        {
            if (protoBatches == null || protoIdx < 0 || protoIdx >= protoBatches.Length) return;
            protoBatches[protoIdx].Clear();
            if (mList == null || mList.Count == 0)
            {
                protoBatchCounts[protoIdx] = 0;
                return;
            }

            protoBatchCounts[protoIdx] = mList.Count;
            for (int start = 0; start < mList.Count; start += 1023)
            {
                int batchSize = Mathf.Min(1023, mList.Count - start);
                Matrix4x4[] batch = new Matrix4x4[batchSize];
                mList.CopyTo(start, batch, 0, batchSize);
                protoBatches[protoIdx].Add(batch);
            }
        }

        /// <summary>
        /// 이 청크에 존재하는 모든 프로토타입의 활성 풀포기 총합을 계산합니다.
        /// </summary>
        public int GetTotalTuftCount()
        {
            if (protoBatchCounts == null) return 0;
            int sum = 0;
            for (int i = 0; i < protoBatchCounts.Length; i++) sum += protoBatchCounts[i];
            return sum;
        }
    }

    /// <summary>
    /// 🔥 [개별 풀포기 연소 인스턴스]
    /// 풀포기 고유의 메쉬 폭, 높이, 스케일에 1:1로 일치하는 화염 크기로 개별 연소 및 실시간 숯 탄화
    /// </summary>
    public class BurningPlantInstance
    {
        public Vector2Int chunkCoord;
        public int protoIndex;
        public int batchIndex;
        public int instanceIndex;
        public Vector3 worldPos;
        public Vector3 originalScale;
        public Quaternion rotation;
        public float plantWidth;
        public float plantHeight;
        public float burnTimer;
        public float burnDuration;
        public GameObject fireVfxInstance;
        public MeshRenderer burningMeshRenderer;
        public Transform burningMeshTransform;
        public MaterialPropertyBlock burnPropBlock;
        public Light fireLight;
        public bool hasSpread;
        public int spreadStep; // 0: 미전파, 1: 1차 순풍 전파, 2: 2차 측면 전파, 3: 3차 확산 완료
    }

    /// <summary>
    /// 📄 [전장 식생 매니페스트 JSON 직렬화 구조체]
    /// </summary>
    [Serializable]
    public class FoliageManifestData
    {
        public string mapName;
        public string saveTimestamp;
        public Vector3 terrainPosition;
        public Vector3 terrainSize;
        public float chunkSize;
        public float sampleSpacing;
        public int totalChunkCount;
        public int totalInstanceCount;
        public List<string> prototypeNames = new List<string>();
    }

    /// <summary>
    /// 📄 [런타임 짓밟힘/화재 변경분 JSON 직렬화 구조체]
    /// </summary>
    [Serializable]
    public class FoliageDeltaRecord
    {
        public int chunkX;
        public int chunkZ;
        public int instanceIndex;
        public FoliageState state;
    }

    [Serializable]
    public class FoliageDeltaContainer
    {
        public string mapName;
        public string updateTimestamp;
        public List<FoliageDeltaRecord> records = new List<FoliageDeltaRecord>();
    }

    /// <summary>
    /// 🌾 [데이터 기반 방사형 잔디 스트리밍 렌더러 (Tactical Grass Renderer)]
    /// - 풀을 씬에 무겁게 미리 심어두지 않고 가벼운 데이터 그리드로 보관합니다.
    /// - 카메라가 전술 줌아웃(원거리) 상태일 때는 렌더링 비용이 0입니다.
    /// - 카메라를 지면 근처로 확대(줌인)하면, 카메라 반경(40~80m) 안의 풀들만 
    ///   GPU 인스턴싱(DrawMeshInstanced)으로 초고속 실시간 렌더링합니다!
    /// - 게임 시작/저장 시 Assets/Settings/Vegetation/Data/[MapName]/ 에 독립된 바이너리/JSON 파일로 영구 보관.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TacticalGrassRenderer : MonoBehaviour
    {
        private static TacticalGrassRenderer _instance;
        public static TacticalGrassRenderer Instance => _instance;

        [Header("🏔️ 대상 터레인 참조")]
        [Tooltip("풀을 생성할 목표 Terrain 컴포넌트 (미지정 시 activeTerrain 자동 탐색)")]
        public Terrain targetTerrain;

        [Header("💾 데이터 파일 저장 폴더")]
        [Tooltip("Assets/Settings/Vegetation/Data/ 하위에 생성될 전장 폴더 명칭 (비어있으면 현재 씬 이름 자동 사용)")]
        public string mapFolderName = "";

        [Header("🌾 3D 풀 프로토타입 목록")]
        [Tooltip("렌더링할 3D 풀포기 메쉬 및 인스턴싱 머티리얼 목록")]
        public List<TacticalGrassPrototype> prototypes = new List<TacticalGrassPrototype>();

        [Header("📐 카메라 스트리밍 & 줌 렌더링 옵션")]
        [Range(30f, 150f)]
        [Tooltip("풀이 보이기 시작하는 카메라와 지면 사이의 최대 유효 거리 (단위: m, 기본: 80m). 이 거리보다 멀어지면 렌더링 연산 0!")]
        public float maxRenderDistance = 80f;

        [Range(16f, 64f)]
        [Tooltip("공간 분할 청크 크기 (기본: 32m)")]
        public float chunkSize = 32f;

        [Range(1.0f, 6.0f)]
        [Tooltip("풀포기 배치 간격 (군락 지름 0.9m에 맞추어 1.6m로 배치 시 20~30% 적절히 겹치며 완벽한 토탈워 삼국지 잔디 융단 형성, 기본: 1.6m)")]
        public float sampleSpacing = 1.6f;

        [Header("🌿 복합 식생 다층 혼합 살포 (Multi-Species Blending)")]
        [Tooltip("청크 내 다종 식생 복합 살포 활성화 (지피 잔디 + 키 큰 이삭 + 야생화 + 고사리 등이 한 청크에 자연스럽게 공존)")]
        public bool enableMultiSpeciesBlending = true;

        [Range(0.05f, 0.5f)]
        [Tooltip("키 큰 3D 갈대/수풀 출현 확률 (기획서 표준: 15%)")]
        public float tallGrassChance = 0.15f;

        [Range(0.0f, 0.6f)]
        [Tooltip("야생화(꽃) 군락 패치 출현 빈도 (기획서 표준: 15%)")]
        public float wildflowerPatchChance = 0.25f;

        [Range(0.0f, 0.5f)]
        [Tooltip("고사리 및 관목 덤불 출현 빈도 (기본: 20%)")]
        public float fernAndShrubChance = 0.20f;

        [Header("🏷️ 청크별 커스텀 바이옴/밀도 오버라이드")]
        [Tooltip("특정 청크에 원하는 바이옴이나 밀도 배율을 개별 지정하는 목록")]
        public List<ChunkFoliageOverride> chunkOverrides = new List<ChunkFoliageOverride>();

        [Header("🎬 씬 뷰(Scene View) 전용 렌더링 옵션")]
        [Range(50f, 1000f)]
        [Tooltip("에디터 씬 뷰에서 줌아웃 시에도 풀을 넓게 조망할 수 있는 가시 거리 (단위: m, 기본: 450m)")]
        public float sceneViewRenderDistance = 450f;

        [Tooltip("씬 뷰에서 카메라 고도 제한(Y축 높이)을 해제하여 높은 각도나 쿼터뷰에서도 풀이 보이도록 허용")]
        public bool sceneViewIgnoreHeightLimit = true;

        [Tooltip("씬 뷰에서 풀 데이터가 생성된 청크 영역을 직관적인 와이어 기즈모로 표시")]
        public bool showChunkGizmosInSceneView = true;

        [Header("🔥 화재 VFX 프리팹 (Fire Particles & Light)")]
        [Tooltip("청크 연소 시 불타오르는 화염, 연기, 불티, 화재 조명 VFX 프리팹")]
        public GameObject fireVfxPrefab;
        private Transform fireVfxRoot;
        private double _lastEditorTime = 0.0;

        [Header("🔥 화재 전이 확률 및 바람 영향 (Spread Probability & Wind)")]
        [Range(0.1f, 0.9f)]
        [Tooltip("풀포기 간 기본 화재 전이 확률 (기본: 38% - 무조건 100% 번지지 않고 바람/식생에 따라 자연 진화 발생)")]
        public float baseSpreadChance = 0.38f;

        [Range(1.0f, 2.5f)]
        [Tooltip("순풍 방향 화재 전이 확률 가중 배율 (기본: 1.6배 - 바람 부는 쪽으로 불씨 비산)")]
        public float downwindSpreadBonus = 1.6f;

        [Range(0.05f, 0.5f)]
        [Tooltip("역풍 방향 화재 전이 저항 계수 (기본: 0.20배 - 바람을 거스르면 불길 차단)")]
        public float upwindSpreadPenalty = 0.20f;

        [Header("🪵 연소 잿더미 & 그을음 흔적 튜닝 (Ash & Scorch Tuning)")]
        [Tooltip("불탄 자리 잿더미의 중심 코어 색상 (기본: 은은한 회갈색 잿빛 투과형)")]
        public Color ashCoreColor = new Color(0.35f, 0.32f, 0.28f, 0.52f);

        [Tooltip("불탄 자리 잿더미의 외곽 페더링 색상 (RGB 색감 유지, 알파는 외곽에서 0으로 페이드아웃)")]
        public Color ashEdgeColor = new Color(0.48f, 0.44f, 0.40f, 0.0f);

        [Range(0.1f, 1.0f)]
        [Tooltip("잿더미 전체 불투명도 배율 (낮을수록 지형 바닥 텍스처가 더 투명하게 비침, 기본 1.0)")]
        public float ashOpacity = 1.0f;

        [Range(0.5f, 2.5f)]
        [Tooltip("풀포기 크기 대비 잿더미 번짐 반경 배율 (기본: 1.35배)")]
        public float ashRadiusScale = 1.35f;

        [Range(0.2f, 0.8f)]
        [Tooltip("연소 완료 후 남는 3D 숯 풀 밑동의 탄화 진행도 (0.45: 연한 갈색 탄화 ~ 0.65: 짙은 숯, 기본 0.52)")]
        public float charredBurnProgress = 0.52f;

        [Header("🍂 계절별 화재 연소 물리 & 잿더미 동기화 (Seasonal Fire Simulation)")]
        [Tooltip("현재 씬의 TotalWarTerrainManager 사계절(여름/겨울/가을/봄) 및 설경 적설량에 따라 잿더미 색상, 잔해 형태, 확산율을 실시간 자동 동기화")]
        public bool enableSeasonalFireSimulation = true;

        /// <summary>
        /// 🍂 동아시아(중국 대륙·한국) 몬순 기후에 기반한 계절별 과학적 화재 연소 및 연기 물리 파라미터를 계산합니다.
        /// - 봄 (연중 최고 건조기/산불 시즌): 바싹 마른 풀과 강풍 -> 폭발적 확산(+20%), 백회색 미네랄 재, 가볍고 맑은 청회색 연기
        /// - 가을 (건조한 수확기/갈대): 건조한 가을바람 -> 빠른 확산(+10%), 짙은 회갈색 잿더미, 매캐한 황갈색 연기
        /// - 겨울: 
        ///    * 설경(Snow > 0.2): 차가운 눈과 습기로 저온 훈연 연소(-20%), 백색 설원 위 칠흑 숯검댕, 차가운 서리빛 청백색 수증기 연기
        ///    * 마른 겨울(Snow <= 0.2): 시베리아 북서풍 한랭건조 -> 마른 풀 확산(+10%), 흑회색 숯, 짙은 흑회색 검댕 연기
        /// - 여름 (장마/고온다습): 몬순 강우와 수분 포화 -> 화재 전파 억제(-18%), 고온 수증기로 인한 짙은 백색 훈연 연기 다량 발생, 연한 갈색 그을음
        /// </summary>
        public (Color coreCol, Color edgeCol, float opacity, float burnProg, float stumpHeightScale, float spreadProbBonus, Color smokeStartCol, Color smokeEndCol, float smokeAlpha, float smokeRateMult) GetSeasonalFireParameters()
        {
            if (!enableSeasonalFireSimulation)
            {
                return (ashCoreColor, ashEdgeColor, ashOpacity, charredBurnProgress, 0.42f, 0f, new Color(0.88f, 0.88f, 0.85f, 1f), new Color(0.76f, 0.76f, 0.74f, 1f), 0.75f, 1.45f);
            }

            TacticalForestZone.Season currentSeason = TacticalForestZone.Season.Summer;
            float snowAmount = 0f;

            if (TotalWarTerrainManager.Instance != null)
            {
                currentSeason = TotalWarTerrainManager.Instance.season;
                snowAmount = TotalWarTerrainManager.Instance.globalSnowAmount;
            }

            switch (currentSeason)
            {
                case TacticalForestZone.Season.Spring:
                    // 🌸 봄 (동아시아 연중 최고 건조기 & 대형 산불 시즌):
                    // 강한 봄바람과 극도로 메마른 마른 풀 -> 초고온 완전 연소(미네랄 백회색 재) 및 폭발적 확산
                    // 연기: 수분이 극소하여 빠르게 흩어지는 맑고 가벼운 연회색/청회색 연기
                    return (
                        new Color(0.56f, 0.54f, 0.50f, 0.44f), // 뽀얀 백회색/연회색 미네랄 재
                        new Color(0.68f, 0.65f, 0.60f, 0.0f),
                        0.95f,
                        0.46f, // 바싹 마른 짚풀 탄화 톤
                        0.32f, // 고온으로 바닥까지 완전히 타버린 낮은 잔해
                        0.20f, // 연중 최고 건조도로 확산 확률 20% 폭발적 증가
                        new Color(0.65f, 0.68f, 0.72f, 1.0f),  // 청회색/연회색 맑은 연기 시작색
                        new Color(0.80f, 0.82f, 0.85f, 1.0f),  // 흩어지는 연회색 끝색
                        0.42f, // 낮은 수증기로 맑고 가벼운 투과도
                        1.0f   // 표준 연기 발생량
                    );

                case TacticalForestZone.Season.Autumn:
                    // 🍂 가을 (건조한 단풍·수확기·마른 억새):
                    // 청명하고 건조한 가을바람 -> 짙은 회갈색 잿더미와 짙은 숯
                    // 연기: 마른 벼/억새/낙엽 연소로 발생하는 매캐한 황갈색/고동색 연기
                    return (
                        new Color(0.35f, 0.30f, 0.26f, 0.52f),
                        new Color(0.48f, 0.42f, 0.38f, 0.0f),
                        1.0f,
                        0.58f,
                        0.40f,
                        0.10f, // 건조한 가을바람으로 확산 확률 10% 증가
                        new Color(0.48f, 0.40f, 0.32f, 1.0f),  // 매캐한 황갈색 연기 시작색
                        new Color(0.60f, 0.54f, 0.46f, 1.0f),  // 옅은 볏짚색 연기 끝색
                        0.58f, // 보통 밀도
                        1.10f  // 건조 짚풀로 약간 많은 연기
                    );

                case TacticalForestZone.Season.Winter:
                    // ❄️ 겨울: 눈 덮인 설원(Snowy) vs 마른 겨울(Dry Winter) 분기
                    if (snowAmount > 0.20f)
                    {
                        // 설원 겨울: 차가운 눈과 얼음으로 저온 불완전 연소 -> 설원 위 칠흑 같은 흑색 숯검댕
                        // 연기: 눈덮임 비율(snowAmount)에 정비례하여 차가운 눈이 녹아 증발하는 수증기 연기 방출량과 안개 밀도 동적 증가!
                        float snowDarken = Mathf.Lerp(1.0f, 0.65f, snowAmount);
                        float dynamicSmokeRate = Mathf.Lerp(1.15f, 1.70f, snowAmount); // 폭설(1.0)일수록 수증기 연기 1.70배 대량 방출
                        float dynamicSmokeAlpha = Mathf.Lerp(0.60f, 0.88f, snowAmount); // 눈이 두꺼울수록 짙은 백색 안개감

                        return (
                            new Color(0.06f * snowDarken, 0.05f * snowDarken, 0.05f * snowDarken, 0.82f),
                            new Color(0.18f * snowDarken, 0.16f * snowDarken, 0.14f * snowDarken, 0.0f),
                            1.0f,
                            0.72f, // 새까만 칠흑 숯
                            0.52f, // 덜 타서 억센 숯 기둥 잔해
                            -0.20f,// 차가운 눈과 습기로 확산 확률 20% 감소
                            new Color(0.82f, 0.86f, 0.94f, 1.0f),  // 차가운 청백색 서리 연기 시작색
                            new Color(0.90f, 0.93f, 0.97f, 1.0f),  // 서리빛 백색 끝색
                            dynamicSmokeAlpha,                      // 적설량 비례 짙은 수증기 안개감
                            dynamicSmokeRate                        // 적설량 비례 수증기 방출량 동적 스케일링
                        );
                    }
                    else
                    {
                        // 마른 겨울: 시베리아 북서풍의 한랭건조 -> 바싹 마른 겨울 풀밭 확산
                        // 연기: 한랭건조 바람 속에 타오르는 짙은 흑회색 검댕 연기
                        return (
                            new Color(0.22f, 0.20f, 0.18f, 0.65f), // 짙은 흑회색 숯
                            new Color(0.35f, 0.32f, 0.30f, 0.0f),
                            1.0f,
                            0.65f,
                            0.45f,
                            0.10f, // 마른 겨울바람으로 확산 확률 10% 증가
                            new Color(0.25f, 0.23f, 0.21f, 1.0f),  // 짙은 흑회색 검댕 연기 시작색
                            new Color(0.40f, 0.37f, 0.34f, 1.0f),  // 회갈색 끝색
                            0.65f, // 검댕 밀도 높음
                            1.0f   // 표준 방출량
                        );
                    }

                case TacticalForestZone.Season.Summer:
                default:
                    // ☀️ 여름 (동아시아 몬순 우기 & 고온다습):
                    // 장마와 잦은 호우로 식물이 수분을 가득 머금음 -> 산불 위험 최저, 연기 다량 발생, 연한 갈색 그을음
                    // 연기: 다량의 수분이 증발하며 뭉게뭉게 피어오르는 짙은 백색 수증기 훈연 연기
                    return (
                        new Color(0.38f, 0.35f, 0.32f, 0.38f), // 지형이 많이 비치는 연한 갈색 그을음
                        new Color(0.50f, 0.46f, 0.42f, 0.0f),
                        0.85f,
                        0.48f, // 수분을 머금어 덜 탄 갈색 마름 톤
                        0.38f,
                        -0.18f,// 고온다습한 수분으로 화재 확산 18% 억제
                        new Color(0.88f, 0.88f, 0.85f, 1.0f),  // 짙은 백색 훈연 수증기 시작색
                        new Color(0.76f, 0.76f, 0.74f, 1.0f),  // 뽀얀 백회색 끝색
                        0.75f, // 짙은 수증기 안개감
                        1.45f  // 수증기로 인한 연기 발생량 대폭 증가 (+45%)
                    );
            }
        }

        [Header("⚙️ 렌더링 최적화 & 그림자")]
        public UnityEngine.Rendering.ShadowCastingMode castShadows = UnityEngine.Rendering.ShadowCastingMode.Off;
        public bool receiveShadows = true;

        [Header("📊 실시간 스트리밍 대시보드 (읽기 전용)")]
        [SerializeField] private int _totalChunkCount = 0;
        [SerializeField] private int _totalInstanceCount = 0;
        [SerializeField] private int _renderedInstanceCount = 0;
        [SerializeField] private int _activeDrawCalls = 0;

        public int TotalInstanceCount => _totalInstanceCount;
        public int RenderedInstanceCount => _renderedInstanceCount;
        public int ActiveDrawCalls => _activeDrawCalls;
        public int ActiveChunkCount => _chunks != null ? _chunks.Count : 0;
        public IReadOnlyDictionary<Vector2Int, TacticalGrassChunk> GetChunks() => _chunks;

        // 청크 맵 (Grid Coordinate -> Chunk)
        private readonly Dictionary<Vector2Int, TacticalGrassChunk> _chunks = new Dictionary<Vector2Int, TacticalGrassChunk>();

        // 드로우콜 임시 캐시
        private readonly List<TacticalGrassChunk> _visibleChunks = new List<TacticalGrassChunk>();

        // 🔥 [개별 풀포기 크기 비례 실시간 연소 리스트]
        private readonly List<BurningPlantInstance> _burningPlants = new List<BurningPlantInstance>();

        // 런타임 짓밟힘/화재 변경분 델타 캐시
        private readonly List<FoliageDeltaRecord> _runtimeDeltas = new List<FoliageDeltaRecord>();

        private const string DATA_BASE_DIR = "Assets/Settings/Vegetation/Data";

        public string GetEffectiveFolderName()
        {
            if (!string.IsNullOrEmpty(mapFolderName)) return mapFolderName.Trim();

            string sceneName = SceneManager.GetActiveScene().name;
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = EditorSceneManager.GetActiveScene().name;
            }
#endif
            if (string.IsNullOrEmpty(sceneName)) sceneName = "Battlefield_Default";
            return sceneName;
        }

        public string GetTargetDataDirectory()
        {
            return Path.Combine(DATA_BASE_DIR, GetEffectiveFolderName()).Replace("\\", "/");
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            if (targetTerrain == null) targetTerrain = GetComponent<Terrain>();
            if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;

            // 🧯 [핵심 버그 수정] 이전 에디터 테스트 잔여 화재 VFX 및 하늘에 떠다니는 불티 파티클 즉시 완전 소거
            CleanupResidualFireVFX();
        }

        /// <summary>
        /// 🧯 게임 시작 시 씬에 남아있던 모든 이전 잔여 화재 VFX, 불티(Embers), 화염 파티클 및 잿더미를 완전 소거합니다.
        /// </summary>
        [ContextMenu("🧯 잔여 화재 VFX 완전 소거 (Cleanup Residual Fire VFX)")]
        public void CleanupResidualFireVFX()
        {
            _burningPlants.Clear();

            // 1. 컴포넌트 트랜스폼 및 자식의 모든 Foliage_FireVFX_Root 탐색 및 제거
            var existingRoots = GetComponentsInChildren<Transform>(true);
            for (int i = existingRoots.Length - 1; i >= 0; i--)
            {
                var t = existingRoots[i];
                if (t == null) continue;
                if (t.name == "Foliage_FireVFX_Root" || t.name.StartsWith("Fire_Chunk_") || t.name.StartsWith("Fire_Plant_") || t.name.StartsWith("Ash_Plant_"))
                {
                    // 모든 파티클 시스템 즉시 정지 및 버퍼 클리어
                    var psArray = t.GetComponentsInChildren<ParticleSystem>(true);
                    for (int p = 0; p < psArray.Length; p++)
                    {
                        if (psArray[p] != null)
                        {
                            psArray[p].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }

                    if (Application.isPlaying) Destroy(t.gameObject);
                    else DestroyImmediate(t.gameObject);
                }
            }
            fireVfxRoot = null;
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;

            // 🧯 [씬 상태 화재 오염 방지] 에디터 모드 및 씬 로드 시 잔여 화재/불티/파티클 즉시 완전 소거
            CleanupResidualFireVFX();

            EnsureDefaultPrototypes(false);
            EnsureAllPrototypesGrounded();

            // 🎥 [빌트인 + URP 듀얼 호환 렌더링 이벤트 구독]
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPreCull += OnCameraPreCull;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
#if UNITY_EDITOR
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
#endif

            // 1. 디스크에 저장된 파일이 있는지 확인하고 로드 (구버전 파일이면 자동으로 새로 Bake & Save)
            if (_chunks.Count == 0 && targetTerrain != null)
            {
                if (!LoadFoliageDataFromDisk())
                {
                    BakeFoliageData();
                    SaveFoliageDataToDisk();
                }
            }
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
#if UNITY_EDITOR
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
#endif
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
#if UNITY_EDITOR
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
#endif
        }

#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (!Application.isPlaying && _burningPlants.Count > 0)
            {
                Update();
            }
        }
#endif

        /// <summary>
        /// 🎥 [Unity 빌트인 렌더 파이프라인 공식 콜백]: 씬 뷰 카메라와 게임 뷰 카메라 렌더 직전 실시간 드로우
        /// </summary>
        private void OnCameraPreCull(Camera cam)
        {
            if (cam == null) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            RenderGrassBatchesForCamera(cam);
        }

        /// <summary>
        /// 📷 [외부 카메라 강제 렌더링 호출]
        /// 스크린샷 캡처 또는 커스텀 렌더 패스에서 특정 카메라를 기준으로 잔디 인스턴싱 드로우콜을 즉시 발행합니다.
        /// </summary>
        public void ForceRenderCamera(Camera cam)
        {
            if (cam != null) RenderGrassBatchesForCamera(cam);
        }

        /// <summary>
        /// 🎥 [URP 카메라 렌더링 직전 콜백]: URP 환경 대응
        /// </summary>
        private void OnBeginCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
        {
            if (cam == null) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            RenderGrassBatchesForCamera(cam);
        }

        private void Update()
        {
            if (_chunks == null || _chunks.Count == 0) return;

            // 🎮 [인게임 F12 디버그 단축키]: F12 디버그 모드가 켜져 있을 때
            // - [B] 키 또는 [Ctrl+좌클릭]: 마우스 커서 위치 지면/식생에 즉시 발화!
            // - 풀(Grass)뿐만 아니라 관목 부시(Bush)와 숲 수목(Trees)까지 마우스 커서 위치에 동시에 100% 확정 발화!
            if (Application.isPlaying && FPSCounterHUD.IsDebugModeActive)
            {
                bool pressB = Input.GetKeyDown(KeyCode.B);
                bool pressCtrlClick = Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButtonDown(0);

                if (pressB || pressCtrlClick)
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out RaycastHit hit, 1500f))
                        {
                            IgniteAllVegetationAt(hit.point, 5.0f);
                        }
                        else
                        {
                            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                            if (groundPlane.Raycast(ray, out float enter))
                            {
                                Vector3 hitPt = ray.GetPoint(enter);
                                hitPt.y = TerrainHeightManager.SampleHeightFast(hitPt.x, hitPt.z);
                                IgniteAllVegetationAt(hitPt, 5.0f);
                            }
                        }
                    }
                }
            }

            // 🔥 [실시간 풀포기 1:1 크기 화재 시뮬레이션 및 전파]
            if (_burningPlants.Count > 0)
            {
                // 에디터 비플레이 모드에서는 Time.deltaTime이 0이므로 시간 델타 보정
                float dt = Time.deltaTime;
                if (!Application.isPlaying)
                {
#if UNITY_EDITOR
                    double now = UnityEditor.EditorApplication.timeSinceStartup;
                    dt = (float)(now - _lastEditorTime);
                    if (dt < 0.016f) return;
                    if (dt > 0.1f) dt = 0.033f;
                    _lastEditorTime = now;
                    UnityEditor.SceneView.RepaintAll();
#else
                    dt = 0.033f;
#endif
                }

                Vector3 windDir = Vector3.forward;
                if (TotalWarWindManager.Instance != null)
                {
                    windDir = TotalWarWindManager.Instance.GetWindDirectionVector();
                }

                for (int i = _burningPlants.Count - 1; i >= 0; i--)
                {
                    var bPlant = _burningPlants[i];
                    bPlant.burnTimer += dt;
                    float progress = Mathf.Clamp01(bPlant.burnTimer / bPlant.burnDuration);

                    // 에디터 비플레이 모드에서도 파티클 시스템이 실시간 재생되도록 시뮬레이션 구동
                    if (!Application.isPlaying && bPlant.fireVfxInstance != null)
                    {
                        var psArr = bPlant.fireVfxInstance.GetComponentsInChildren<ParticleSystem>(false);
                        for (int p = 0; p < psArr.Length; p++)
                        {
                            if (psArr[p] != null) psArr[p].Simulate(dt, false, false);
                        }
                    }

                    // 1. 실시간 연소 중인 풀포기 메쉬의 탄화(_BurnProgress: 주황 열기 -> 흑색 숯 -> 재 바스러짐) 및 수축 연출
                    if (bPlant.burningMeshRenderer != null)
                    {
                        if (bPlant.burnPropBlock == null) bPlant.burnPropBlock = new MaterialPropertyBlock();
                        // 0.05 ~ 0.78 구간으로 탄화 진행하여 완전 클리핑 소멸 대신 시꺼먼 숯 잔해가 남도록 유도
                        float shaderBurn = Mathf.Lerp(0.06f, 0.76f, progress);
                        bPlant.burnPropBlock.SetFloat("_BurnProgress", shaderBurn);
                        bPlant.burningMeshRenderer.SetPropertyBlock(bPlant.burnPropBlock);
                    }

                    if (bPlant.burningMeshTransform != null)
                    {
                        // 연소 전반(0~35%)에는 원래 크기로 활활 불타오르고, 중후반(35~100%)에 서서히 바닥으로 오그라들며 주저앉음
                        float shrinkT = Mathf.Clamp01((progress - 0.30f) / 0.70f);
                        float curY = Mathf.Lerp(bPlant.originalScale.y, bPlant.originalScale.y * 0.14f, shrinkT);
                        float curXZ = Mathf.Lerp(1.0f, 0.55f, shrinkT);
                        bPlant.burningMeshTransform.localScale = new Vector3(
                            bPlant.originalScale.x * curXZ,
                            curY,
                            bPlant.originalScale.z * curXZ
                        );
                    }

                    // 화염 조명 일렁임(Flicker) 및 소멸 감쇄
                    if (bPlant.fireLight != null)
                    {
                        float flicker = 0.85f + Mathf.PerlinNoise(bPlant.burnTimer * 8.5f, bPlant.worldPos.x) * 0.35f;
                        float fadeOut = progress > 0.75f ? Mathf.Lerp(1.0f, 0.0f, (progress - 0.75f) / 0.25f) : 1.0f;
                        bPlant.fireLight.intensity = 2.8f * flicker * fadeOut;
                    }

                    // 2. 풀과 풀 사이(Plant-to-Plant) 3단계 연쇄 전파 & 부시/나무 상호 발화 (플레이 모드에서만 전파 허용)
                    if (Application.isPlaying && _burningPlants.Count < 45)
                    {
                        // 1차 전파 (연소 25% 시점): 순풍 전방 인접 풀포기(1.4m ~ 2.6m)로 도화선 전파
                        if (progress >= 0.25f && bPlant.spreadStep == 0)
                        {
                            bPlant.spreadStep = 1;
                            Vector3 forwardTarget = bPlant.worldPos + (windDir * UnityEngine.Random.Range(1.4f, 2.5f)) + (UnityEngine.Random.insideUnitSphere * 0.4f);
                            forwardTarget.y = TerrainHeightManager.SampleHeightFast(forwardTarget.x, forwardTarget.z);
                            IgnitePlantAt(forwardTarget, 2.6f, true, bPlant.worldPos, windDir);
                            TrySpreadFireToBushAndForest(bPlant.worldPos, windDir);
                        }
                        // 2차 전파 (연소 50% 시점): 좌우 측풍 대각선 인접 풀포기로 화염선(Front) 확산
                        else if (progress >= 0.50f && bPlant.spreadStep == 1)
                        {
                            bPlant.spreadStep = 2;
                            Vector3 crossDir = Vector3.Cross(windDir, Vector3.up).normalized * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
                            Vector3 flankTarget = bPlant.worldPos + (windDir * 1.1f) + (crossDir * UnityEngine.Random.Range(1.3f, 2.3f));
                            flankTarget.y = TerrainHeightManager.SampleHeightFast(flankTarget.x, flankTarget.z);
                            IgnitePlantAt(flankTarget, 2.6f, true, bPlant.worldPos, windDir);
                        }
                        // 3차 전파 (연소 75% 시점): 반대쪽 대각선 또는 전방 2차 도약
                        else if (progress >= 0.75f && bPlant.spreadStep == 2)
                        {
                            bPlant.spreadStep = 3;
                            bPlant.hasSpread = true;
                            Vector3 crossDir = Vector3.Cross(windDir, Vector3.up).normalized * (UnityEngine.Random.value > 0.5f ? -1f : 1f);
                            Vector3 secondFlank = bPlant.worldPos + (windDir * 1.6f) + (crossDir * UnityEngine.Random.Range(1.4f, 2.4f));
                            secondFlank.y = TerrainHeightManager.SampleHeightFast(secondFlank.x, secondFlank.z);
                            IgnitePlantAt(secondFlank, 2.8f, true, bPlant.worldPos, windDir);
                            TrySpreadFireToBushAndForest(secondFlank, windDir);
                        }
                    }

                    // 3. 연소 완료 시 화염 소멸 및 지면 밀착 검은 잿더미(Ash/Scorch Decal) + 3D 숯 풀 밑동 잔해 영구 보존!
                    if (progress >= 1.0f)
                    {
                        if (bPlant.fireVfxInstance != null)
                        {
                            var psList = bPlant.fireVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
                            for (int p = 0; p < psList.Length; p++)
                            {
                                if (psList[p] != null) psList[p].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                            }

                            if (bPlant.fireLight != null)
                            {
                                bPlant.fireLight.enabled = false;
                            }

                            // 🪵 [토탈워식 3D 숯 풀 밑동 잔해 (사계절 물리 연동)]:
                            // 화재 메쉬가 생성될 때부터 지표면 정위치에 완벽하게 안착되었으므로,
                            // 계절에 따라 여름엔 낮고 마른 잔해(0.30배), 겨울엔 억센 칠흑 숯 기둥(0.52배)으로 자연스럽게 높이 축소!
                            var sFireParams = (_instance != null) ? _instance.GetSeasonalFireParameters()
                                : (coreCol: new Color(0.35f, 0.32f, 0.28f, 0.52f), edgeCol: new Color(0.48f, 0.44f, 0.40f, 0f), opacity: 1f, burnProg: 0.52f, stumpHeightScale: 0.42f, spreadProbBonus: 0f, smokeStartCol: new Color(0.88f, 0.88f, 0.85f, 1f), smokeEndCol: new Color(0.76f, 0.76f, 0.74f, 1f), smokeAlpha: 0.75f, smokeRateMult: 1.45f);

                            if (bPlant.burningMeshTransform != null)
                            {
                                float charredH = Mathf.Clamp(bPlant.originalScale.y * sFireParams.stumpHeightScale, 0.20f, 0.70f);
                                bPlant.burningMeshTransform.localScale = new Vector3(
                                    bPlant.originalScale.x * 1.05f,
                                    charredH,
                                    bPlant.originalScale.z * 1.05f
                                );
                            }
                            if (bPlant.burningMeshRenderer != null)
                            {
                                if (bPlant.burnPropBlock == null) bPlant.burnPropBlock = new MaterialPropertyBlock();
                                bPlant.burnPropBlock.SetFloat("_BurnProgress", sFireParams.burnProg); // 계절별 숯 탄화도(여름: 갈탄 ~ 겨울: 칠흑)
                                bPlant.burningMeshRenderer.SetPropertyBlock(bPlant.burnPropBlock);
                            }

                            // 🪵 [핵심: 지면 밀착 잿더미 메쉬(Terrain Conforming Ash Scorch Mark) 생성!]
                            // 풀포기가 타서 사라진 지면에 100% 지형 곡면을 따라 덮이는 잿더미/그을음 메쉬 안착
                            float rScale = (_instance != null) ? _instance.ashRadiusScale : 1.35f;
                            SpawnTerrainConformingAshMark(bPlant.worldPos, Mathf.Max(1.10f, bPlant.originalScale.x * rScale), bPlant.fireVfxInstance.transform);

                            // 화염 오브젝트 이름을 "Charred_Plant_..."로 변경하여 납작한 숯 풀 잔해 및 잿더미로 영구 보존!
                            bPlant.fireVfxInstance.name = bPlant.fireVfxInstance.name.Replace("Fire_Plant_", "Charred_Plant_");
                            bPlant.fireVfxInstance = null;
                        }

                        _burningPlants.RemoveAt(i);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // 빌트인 파이프라인 에디터 씬 뷰 및 런타임 게임 뷰 렌더링 보장
            if (Camera.main != null)
            {
                RenderGrassBatchesForCamera(Camera.main);
            }
#if UNITY_EDITOR
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                RenderGrassBatchesForCamera(SceneView.lastActiveSceneView.camera);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 🎥 [에디터 씬 뷰 실시간 렌더링 훅]: 에디터에서 카메라 회전/이동 시 잔디를 끊김 없이 즉각 드로우
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (sceneView != null && sceneView.camera != null)
            {
                RenderGrassBatchesForCamera(sceneView.camera);
            }
        }
#endif

        // 🌿 [정규화된 개별 풀포기 메쉬 런타임/에디터 캐시]
        private static readonly Dictionary<string, Mesh> _groundedMeshCache = new Dictionary<string, Mesh>();

        /// <summary>
        /// .obj 또는 에셋 파일에서 Mesh를 안전하게 로드하는 헬퍼 함수
        /// </summary>
        public static Mesh LoadMeshSafe(string path)
        {
#if UNITY_EDITOR
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;

            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            if (all != null)
            {
                foreach (var obj in all)
                {
                    if (obj is Mesh subMesh) return subMesh;
                }
            }
#endif
            return null;
        }

        /// <summary>
        /// 🌿 [핵심 해결책: 8m 광역 평판 메쉬 -> 지면 100% 밀착형 1.15m 개별 풀포기(Grounded Single Tuft) 정규화]
        /// - 삼탈워 추출 원본 .obj 메쉬(Shrub_Flat_A, Sbt_Grass0_1 등)는 폭 7~9.5m에 달하는 넓은 패치 안에
        ///   20여 개의 서브 풀포기가 흩어져 있으며, 외곽 풀포기들의 뿌리가 Y=+0.44m 허공에 떠 있거나 Y=-0.62m 지하에 박혀 있습니다.
        /// - 이로 인해 가운데만 땅에 닿고 외곽 풀이 공중에 붕 뜨거나, 불을 붙이면 8m 영역 전체가 한꺼번에 줄어드는 문제가 발생했습니다.
        /// - 본 메서드는 메쉬 내부의 연결된 풀잎 군집(Island)들을 분리하여 모든 풀잎의 뿌리를 예외 없이 Y=0.000m 지면에 맞추고,
        ///   멀리 흩어진 외곽 서브 풀포기들을 반경 maxRadius(0.58m, 지름 ~1.16m) 이내로 응집시켜
        ///   지형 굴곡을 따라 빈틈없이 덮이고 개별 포기 단위로 불타는 완벽한 단일 풀포기 메쉬를 생성합니다.
        /// </summary>
        public static Mesh GetOrCreateGroundedTuftMesh(Mesh sourceMesh, bool flipY = false, float maxRadius = 0.58f)
        {
            if (sourceMesh == null) return null;
            if (sourceMesh.name.EndsWith("_GroundedTuft")) return sourceMesh;

            string cacheKey = $"{sourceMesh.name}_{sourceMesh.GetInstanceID()}_{flipY}_{maxRadius:F2}_v4SafeUVGrounded";
            if (_groundedMeshCache.TryGetValue(cacheKey, out Mesh cached) && cached != null)
            {
                return cached;
            }

            Vector3[] verts = sourceMesh.vertices;
            if (verts == null || verts.Length == 0) return sourceMesh;

            int vCount = verts.Length;
            Vector3[] normals = sourceMesh.normals;
            Vector4[] tangents = sourceMesh.tangents;
            Vector2[] uvs = sourceMesh.uv;
            int[] tris = sourceMesh.triangles;

            // 1. 상하 반전(flipY) 모델인 경우 음수 스케일 대신 버텍스/노멀/와인딩을 정방향으로 뒤집어 법선 및 그림자 깨짐 방지
            if (flipY)
            {
                float globalMinY = float.MaxValue;
                float globalMaxY = float.MinValue;
                for (int i = 0; i < vCount; i++)
                {
                    if (verts[i].y < globalMinY) globalMinY = verts[i].y;
                    if (verts[i].y > globalMaxY) globalMaxY = verts[i].y;
                }

                for (int i = 0; i < vCount; i++)
                {
                    verts[i].y = globalMaxY - (verts[i].y - globalMinY);
                    if (normals != null && normals.Length == vCount)
                    {
                        normals[i].y = -normals[i].y;
                    }
                }

                for (int t = 0; t < tris.Length; t += 3)
                {
                    int tmp = tris[t + 1];
                    tris[t + 1] = tris[t + 2];
                    tris[t + 2] = tmp;
                }
            }

            // 2. Union-Find로 연결된 삼각형 및 인접 뿌리(14cm 격자) 단위의 서브 풀포기 군집(Island) 분리
            int[] parent = new int[vCount];
            for (int i = 0; i < vCount; i++) parent[i] = i;

            int FindRoot(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void UnionNodes(int a, int b)
            {
                int ra = FindRoot(a);
                int rb = FindRoot(b);
                if (ra != rb) parent[rb] = ra;
            }

            for (int t = 0; t < tris.Length; t += 3)
            {
                UnionNodes(tris[t], tris[t + 1]);
                UnionNodes(tris[t + 1], tris[t + 2]);
            }

            Dictionary<long, int> spatialCells = new Dictionary<long, int>();
            const float cellSnap = 0.14f;
            for (int i = 0; i < vCount; i++)
            {
                int gx = Mathf.RoundToInt(verts[i].x / cellSnap);
                int gz = Mathf.RoundToInt(verts[i].z / cellSnap);
                long cellKey = ((long)gx << 32) ^ (uint)gz;
                if (spatialCells.TryGetValue(cellKey, out int repIdx))
                {
                    UnionNodes(i, repIdx);
                }
                else
                {
                    spatialCells[cellKey] = i;
                }
            }

            Dictionary<int, List<int>> islands = new Dictionary<int, List<int>>();
            for (int i = 0; i < vCount; i++)
            {
                int r = FindRoot(i);
                if (!islands.TryGetValue(r, out var vList))
                {
                    vList = new List<int>();
                    islands[r] = vList;
                }
                vList.Add(i);
            }

            // 3. 각 서브 풀포기(Island)별로 진짜 뿌리(기저부)를 Y=0.000m에 100% 영점 정렬하고, 원점 반경(maxRadius) 내로 응집
            int islandOrder = 0;
            foreach (var kvp in islands)
            {
                List<int> idxs = kvp.Value;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                for (int k = 0; k < idxs.Count; k++)
                {
                    Vector3 v = verts[idxs[k]];
                    if (v.y < minY) minY = v.y;
                    if (v.y > maxY) maxY = v.y;
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.z < minZ) minZ = v.z;
                    if (v.z > maxZ) maxZ = v.z;
                }

                float cx = (minX + maxX) * 0.5f;
                float cz = (minZ + maxZ) * 0.5f;
                float spanXZ = Mathf.Max(maxX - minX, maxZ - minZ);
                float maxAllowedSpan = maxRadius * 1.85f;
                float localScaleXZ = (spanXZ > maxAllowedSpan && spanXZ > 0.001f) ? (maxAllowedSpan / spanXZ) : 1.0f;

                // 🌿 [진짜 중심 뿌리(Stem/Root Base) 검출 알고리즘]
                // 토탈워 원본 수풀은 외곽 잎이 지면으로 처진 돔형 구조이므로,
                // 절대 최저 Y(처진 잎끝)를 기준으로 삼으면 줄기/뿌리가 허공으로 솟구쳐 거꾸로 선 풀이 됩니다.
                // 따라서 섬의 중심부(반경 35% 이내 또는 0.28m 이내)에 위치한 진짜 뿌리 정점들의 최저 Y를 검출합니다.
                float islandRadius = spanXZ * 0.5f;
                float coreRadius = Mathf.Min(0.28f, Mathf.Max(0.06f, islandRadius * 0.38f));
                float coreRadiusSq = coreRadius * coreRadius;
                float rootY = float.MaxValue;

                for (int k = 0; k < idxs.Count; k++)
                {
                    Vector3 v = verts[idxs[k]];
                    float dx = v.x - cx;
                    float dz = v.z - cz;
                    if (dx * dx + dz * dz <= coreRadiusSq)
                    {
                        if (v.y < rootY) rootY = v.y;
                    }
                }

                // 만약 중심부에 정점이 드문 경우 대비 폴백: 섬 전체 정점 중 Y 하위 25%인 정점 중 중심에 가장 가까운 정점의 Y 탐색
                if (rootY == float.MaxValue)
                {
                    float yThreshold = minY + (maxY - minY) * 0.25f;
                    float closestDistSq = float.MaxValue;
                    for (int k = 0; k < idxs.Count; k++)
                    {
                        Vector3 v = verts[idxs[k]];
                        if (v.y <= yThreshold)
                        {
                            float dsq = (v.x - cx) * (v.x - cx) + (v.z - cz) * (v.z - cz);
                            if (dsq < closestDistSq)
                            {
                                closestDistSq = dsq;
                                rootY = v.y;
                            }
                        }
                    }
                }
                if (rootY == float.MaxValue) rootY = minY;

                // 외곽에 멀리 떨어져 있는 서브 풀포기 중심을 원점 반경 내 황금각 위치로 당겨옴 (잎 크기는 유지!)
                float distFromOrigin = Mathf.Sqrt(cx * cx + cz * cz);
                float targetCx = cx;
                float targetCz = cz;
                if (distFromOrigin > maxRadius * 0.78f)
                {
                    float goldenAngle = islandOrder * 2.3999632f;
                    float radialT = ((islandOrder * 0.6180339f) % 1.0f);
                    float newR = Mathf.Lerp(0.06f, maxRadius * 0.78f, radialT);
                    targetCx = Mathf.Cos(goldenAngle) * newR;
                    targetCz = Mathf.Sin(goldenAngle) * newR;
                }

                for (int k = 0; k < idxs.Count; k++)
                {
                    int vi = idxs[k];
                    Vector3 v = verts[vi];

                    // 진짜 중심 뿌리 기저부를 Y = 0.000m에 정확히 일치!
                    v.y -= rootY;

                    // 잎끝이 지면 아래로 파고들어 뒤집히거나 파묻히지 않도록 부드럽게 지면 위(+0.003m)로 안착
                    if (v.y < 0.003f) v.y = 0.003f;

                    // 섬 중심 기준 로컬 폭 보정 후 목표 중심점으로 이동
                    v.x = targetCx + (v.x - cx) * localScaleXZ;
                    v.z = targetCz + (v.z - cz) * localScaleXZ;

                    verts[vi] = v;
                }

                islandOrder++;
            }

            // 4. 전체 풀포기 메쉬의 XZ 바운딩 중심을 (0, 0)에 정렬 (인위적 Dome Drop 완전 제거)
            float finalMinX = float.MaxValue, finalMaxX = float.MinValue;
            float finalMinZ = float.MaxValue, finalMaxZ = float.MinValue;
            for (int i = 0; i < vCount; i++)
            {
                if (verts[i].x < finalMinX) finalMinX = verts[i].x;
                if (verts[i].x > finalMaxX) finalMaxX = verts[i].x;
                if (verts[i].z < finalMinZ) finalMinZ = verts[i].z;
                if (verts[i].z > finalMaxZ) finalMaxZ = verts[i].z;
            }

            float centerOffsetX = (finalMinX + finalMaxX) * 0.5f;
            float centerOffsetZ = (finalMinZ + finalMaxZ) * 0.5f;

            for (int i = 0; i < vCount; i++)
            {
                Vector3 v = verts[i];
                v.x -= centerOffsetX;
                v.z -= centerOffsetZ;
                verts[i] = v;
            }

            Mesh groundedMesh = new Mesh
            {
                name = sourceMesh.name + "_GroundedTuft",
                vertices = verts,
                triangles = tris
            };
            if (uvs != null && uvs.Length == vCount)
            {
                // 🌿 [토탈워 아틀라스 UV 상단 칼자국 & 공중 파편 원천 차단: 절차적 안전 영역 리매핑 (Procedural Safe Margin)]
                // 1) 상단 수평 칼자국(V = 1.000 경계 절단) 방지: V > 0.940 이상 정점을 안전 영역(최대 0.975)으로 부드럽게 압축
                // 2) 외곽 공중 파편(U = 0.000 / 1.000 경계 누수) 방지: U < 0.006 및 U > 0.994 정점을 안전 마진 안쪽으로 클램핑
                Vector2[] safeUvs = new Vector2[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    Vector2 uv = uvs[i];
                    if (uv.y > 0.940f)
                    {
                        uv.y = Mathf.Lerp(0.940f, 0.975f, (uv.y - 0.940f) / 0.060f);
                    }
                    if (uv.x < 0.006f) uv.x = 0.008f;
                    else if (uv.x > 0.994f) uv.x = 0.992f;
                    safeUvs[i] = uv;
                }
                groundedMesh.uv = safeUvs;
            }
            if (normals != null && normals.Length == vCount) groundedMesh.normals = normals;
            else groundedMesh.RecalculateNormals();
            if (tangents != null && tangents.Length == vCount) groundedMesh.tangents = tangents;
            groundedMesh.RecalculateBounds();

            _groundedMeshCache[cacheKey] = groundedMesh;
            return groundedMesh;
        }

        /// <summary>
        /// 🌾 [3D 단일 풀포기 정제 알고리즘]
        /// 원시 5m 패치 OBJ에서 중심(0,0)에 위치한 단 1개의 주 풀포기(Main Island)만 엄선 추출하여,
        /// 바닥에 눕혀진 파편 잎사귀들을 100% 제거하고 Y=0 기저부에 직립 정렬된 단정하고 깔끔한 3D 풀포기 메쉬 에셋을 생성 및 반환합니다.
        /// </summary>
        public static Mesh GetOrCreateClean3DTuftMesh(Mesh sourceMesh, bool flipY = false, float maxRadius = 0.38f, float heightScale = 1.0f)
        {
            if (sourceMesh == null) return null;
            if (sourceMesh.name.EndsWith("_Clean3DTuft")) return sourceMesh;

            string assetName = $"{sourceMesh.name}_Clean3DTuft";
            string assetPath = $"Assets/Settings/Vegetation/RadialMeshes/{assetName}.asset";

#if UNITY_EDITOR
            Mesh diskMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (diskMesh != null) return diskMesh;
#endif

            Vector3[] srcVerts = sourceMesh.vertices;
            if (srcVerts == null || srcVerts.Length == 0) return sourceMesh;

            int vCount = srcVerts.Length;
            Vector3[] srcNormals = sourceMesh.normals;
            Vector4[] srcTangents = sourceMesh.tangents;
            Vector2[] srcUvs = sourceMesh.uv;
            int[] srcTris = sourceMesh.triangles;

            Vector3[] verts = (Vector3[])srcVerts.Clone();
            Vector3[] normals = srcNormals != null ? (Vector3[])srcNormals.Clone() : null;
            int[] tris = (int[])srcTris.Clone();

            if (flipY)
            {
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < vCount; i++)
                {
                    if (verts[i].y < minY) minY = verts[i].y;
                    if (verts[i].y > maxY) maxY = verts[i].y;
                }
                for (int i = 0; i < vCount; i++)
                {
                    verts[i].y = maxY - (verts[i].y - minY);
                    if (normals != null && i < normals.Length) normals[i].y = -normals[i].y;
                }
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int tmp = tris[t + 1];
                    tris[t + 1] = tris[t + 2];
                    tris[t + 2] = tmp;
                }
            }

            int[] parent = new int[vCount];
            for (int i = 0; i < vCount; i++) parent[i] = i;

            int FindRoot(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void UnionNodes(int a, int b)
            {
                int ra = FindRoot(a);
                int rb = FindRoot(b);
                if (ra != rb) parent[rb] = ra;
            }

            for (int t = 0; t < tris.Length; t += 3)
            {
                UnionNodes(tris[t], tris[t + 1]);
                UnionNodes(tris[t + 1], tris[t + 2]);
            }

            const float snap = 0.12f;
            Dictionary<long, int> grid = new Dictionary<long, int>();
            for (int i = 0; i < vCount; i++)
            {
                int gx = Mathf.RoundToInt(verts[i].x / snap);
                int gz = Mathf.RoundToInt(verts[i].z / snap);
                long key = ((long)gx << 32) ^ (uint)gz;
                if (grid.TryGetValue(key, out int rep)) UnionNodes(i, rep);
                else grid[key] = i;
            }

            Dictionary<int, List<int>> islands = new Dictionary<int, List<int>>();
            for (int i = 0; i < vCount; i++)
            {
                int r = FindRoot(i);
                if (!islands.TryGetValue(r, out var vList))
                {
                    vList = new List<int>();
                    islands[r] = vList;
                }
                vList.Add(i);
            }

            int bestIslandRoot = -1;
            float bestScore = float.MinValue;

            foreach (var kvp in islands)
            {
                var idxs = kvp.Value;
                if (idxs.Count < 24) continue;

                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;

                for (int k = 0; k < idxs.Count; k++)
                {
                    Vector3 v = verts[idxs[k]];
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.z < minZ) minZ = v.z;
                    if (v.z > maxZ) maxZ = v.z;
                    if (v.y < minY) minY = v.y;
                    if (v.y > maxY) maxY = v.y;
                }

                float cx = (minX + maxX) * 0.5f;
                float cz = (minZ + maxZ) * 0.5f;
                float distFromOrigin = Mathf.Sqrt(cx * cx + cz * cz);
                float height = maxY - minY;

                float score = (idxs.Count * 2.0f) + (height * 100.0f) - (distFromOrigin * 150.0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIslandRoot = kvp.Key;
                }
            }

            if (bestIslandRoot == -1)
            {
                int maxCount = 0;
                foreach (var kvp in islands)
                {
                    if (kvp.Value.Count > maxCount)
                    {
                        maxCount = kvp.Value.Count;
                        bestIslandRoot = kvp.Key;
                    }
                }
            }

            if (bestIslandRoot == -1) return sourceMesh;

            List<int> bestIdxs = islands[bestIslandRoot];
            HashSet<int> bestVertSet = new HashSet<int>(bestIdxs);

            float bMinX = float.MaxValue, bMaxX = float.MinValue;
            float bMinZ = float.MaxValue, bMaxZ = float.MinValue;
            float bMinY = float.MaxValue, bMaxY = float.MinValue;

            for (int k = 0; k < bestIdxs.Count; k++)
            {
                Vector3 v = verts[bestIdxs[k]];
                if (v.x < bMinX) bMinX = v.x;
                if (v.x > bMaxX) bMaxX = v.x;
                if (v.z < bMinZ) bMinZ = v.z;
                if (v.z > bMaxZ) bMaxZ = v.z;
                if (v.y < bMinY) bMinY = v.y;
                if (v.y > bMaxY) bMaxY = v.y;
            }

            float coreCx = (bMinX + bMaxX) * 0.5f;
            float coreCz = (bMinZ + bMaxZ) * 0.5f;
            float baseRootY = bMinY;

            Dictionary<int, int> oldToNew = new Dictionary<int, int>();
            List<Vector3> outVerts = new List<Vector3>();
            List<Vector3> outNormals = new List<Vector3>();
            List<Vector4> outTangents = new List<Vector4>();
            List<Vector2> outUvs = new List<Vector2>();

            for (int k = 0; k < bestIdxs.Count; k++)
            {
                int oldIdx = bestIdxs[k];
                Vector3 origV = verts[oldIdx];

                Vector3 centeredV = new Vector3(
                    (origV.x - coreCx),
                    (origV.y - baseRootY) * heightScale,
                    (origV.z - coreCz)
                );

                float r = Mathf.Sqrt(centeredV.x * centeredV.x + centeredV.z * centeredV.z);
                if (r > maxRadius && r > 0.001f)
                {
                    float factor = maxRadius / r;
                    centeredV.x *= factor;
                    centeredV.z *= factor;
                }

                oldToNew[oldIdx] = outVerts.Count;
                outVerts.Add(centeredV);

                if (normals != null && oldIdx < normals.Length) outNormals.Add(normals[oldIdx]);
                if (srcTangents != null && oldIdx < srcTangents.Length) outTangents.Add(srcTangents[oldIdx]);
                if (srcUvs != null && oldIdx < srcUvs.Length) outUvs.Add(srcUvs[oldIdx]);
            }

            List<int> outTris = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t];
                int i1 = tris[t + 1];
                int i2 = tris[t + 2];

                if (bestVertSet.Contains(i0) && bestVertSet.Contains(i1) && bestVertSet.Contains(i2))
                {
                    outTris.Add(oldToNew[i0]);
                    outTris.Add(oldToNew[i1]);
                    outTris.Add(oldToNew[i2]);
                }
            }

            Mesh cleanMesh = new Mesh();
            cleanMesh.name = assetName;
            cleanMesh.vertices = outVerts.ToArray();
            if (outNormals.Count == outVerts.Count) cleanMesh.normals = outNormals.ToArray();
            else cleanMesh.RecalculateNormals();
            if (outTangents.Count == outVerts.Count) cleanMesh.tangents = outTangents.ToArray();
            if (outUvs.Count == outVerts.Count) cleanMesh.uv = outUvs.ToArray();
            cleanMesh.triangles = outTris.ToArray();
            cleanMesh.RecalculateBounds();

#if UNITY_EDITOR
            try
            {
                if (!System.IO.Directory.Exists("Assets/Settings/Vegetation/RadialMeshes"))
                {
                    System.IO.Directory.CreateDirectory("Assets/Settings/Vegetation/RadialMeshes");
                }
                UnityEditor.AssetDatabase.CreateAsset(cleanMesh, assetPath);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[TacticalGrassRenderer] 정제 3D 메쉬 에셋 저장 예외: {ex.Message}");
            }
#endif
            return cleanMesh;
        }

        /// <summary>
        /// 등록된 모든 풀 프로토타입의 메쉬가 지면 밀착형 단일 풀포기(_GroundedTuft 또는 표준 에셋)로 정규화되었는지 확인하고 적용합니다.
        /// </summary>
        [ContextMenu("🌿 모든 식생 메쉬 중심 뿌리 기준 접지 재정규화 (Reset Grounding)")]
        public void EnsureAllPrototypesGrounded()
        {
            EnsureAllPrototypesGrounded(false);
        }

        public void EnsureAllPrototypesGrounded(bool forceReset)
        {
            if (prototypes == null) return;
            if (forceReset)
            {
                _groundedMeshCache.Clear();
            }

            for (int i = 0; i < prototypes.Count; i++)
            {
                var p = prototypes[i];
                if (p == null || p.mesh == null) continue;

                bool needsFlipY = p.heightMultiplier < 0f;
                if (needsFlipY) p.heightMultiplier = Mathf.Abs(p.heightMultiplier);

                bool isAlreadyStandardized = p.mesh.name.EndsWith("_GroundedTuft") || 
                                             p.mesh.name.EndsWith("_Clean3DTuft") || 
                                             p.mesh.name.Contains("MultiDirectionalGrassCluster") || 
                                             p.mesh.name.Contains("FlowerCluster") || 
                                             p.mesh.name.Contains("GrassCluster") || 
                                             p.mesh.name.Contains("RadialFan3D") || 
                                             p.mesh.name.Contains("TallReed3D") || 
                                             p.mesh.name.Contains("ForestFern") || 
                                             p.mesh.name.Contains("GrassSwath");

                if (!isAlreadyStandardized && forceReset)
                {
                    p.mesh = GetOrCreateGroundedTuftMesh(p.mesh, needsFlipY, 0.58f);
                }

                // 법선 정렬 각도가 너무 높게 설정되어 있는 경우 자연스러운 기립을 위해 안전 상한(0.35f) 적용 (단, 예외 바닥덮개는 0.90f 유지)
                if (p.alignToGround > 0.38f && !p.protoName.Contains("예외"))
                {
                    p.alignToGround = 0.30f;
                }
                if (p.groundEmbedOffset < -0.025f && !p.protoName.Contains("예외"))
                {
                    p.groundEmbedOffset = -0.015f;
                }

                if (p.material != null && !p.material.enableInstancing)
                {
                    p.material.enableInstancing = true;
                }
            }
        }

        /// <summary>
        /// 🌾 [토탈워 삼국지 표준 식생 2대 트랙 + 2종 예외 프로토타입 완전 바인딩]
        /// - 난잡한 원시 패치 OBJ(거미줄 파편)를 전면 배제하고 기획서 표준 규격으로 100% 교체합니다:
        /// 1. 하층 2D 지피 융단 (85%): 순수 잔디 70%, 분홍 꽃망울 5%, 연보라 꽃송이 5%, 주황 꽃이삭 5%
        /// 2. 상층 3D 입체 풀포기 (15%): 키 큰 3D 갈대 8%, 두툼한 3D 수풀 7%
        /// 3. 예외 식생: 지면 납작 바닥덮개 5%, 숲속 대나무 그루터기
        /// </summary>
        public void EnsureDefaultPrototypes(bool forceReset = false)
        {
            if (!forceReset && prototypes != null && prototypes.Count >= 6 && prototypes[0].mesh != null && prototypes[0].mesh.name.Contains("MultiDirectionalGrassCluster"))
            {
                EnsureAllPrototypesGrounded();
                return;
            }

            prototypes = new List<TacticalGrassPrototype>();

#if UNITY_EDITOR
            // 🎨 [1. 머티리얼 준비]
            Material pureGrassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_PureSu_Mat.mat");
            if (pureGrassMat == null) pureGrassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_Spring_CarpetGrass_Mat.mat");
            if (pureGrassMat != null) pureGrassMat.enableInstancing = true;

            Material temFlowerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_TemGrassSp_Flower_Mat.mat");
            if (temFlowerMat != null) temFlowerMat.enableInstancing = true;

            Material sbtFlowerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_SbtGrass0Sp_Flower_Mat.mat");
            if (sbtFlowerMat != null) sbtFlowerMat.enableInstancing = true;

            Material tallGrassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_TallReed3D_Spring_Mat.mat");
            if (tallGrassMat != null) tallGrassMat.enableInstancing = true;

            Material springCarpetMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_Spring_CarpetGrass_Mat.mat");
            if (springCarpetMat != null) springCarpetMat.enableInstancing = true;

            Material bambooMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_ForestFern_Sword_Summer_Mat.mat");
            if (bambooMat != null) bambooMat.enableInstancing = true;

            Material autumnGrassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Tem_Autumn_Mat.mat");
            if (autumnGrassMat != null) autumnGrassMat.enableInstancing = true;

            Material clumpSpringMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_RadialFan3D_Spring_Mat.mat");
            if (clumpSpringMat != null) clumpSpringMat.enableInstancing = true;

            Material spikeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_TallReed3D_Spike_Mat.mat");
            if (spikeMat != null) spikeMat.enableInstancing = true;

            Material fernMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_ForestFern_Summer_Mat.mat");
            if (fernMat != null) fernMat.enableInstancing = true;

            // 🌿 [2. 하층 2D 지피 융단 4종 (85% 비중)]
            // [0] 🌾 온대 순수 잔디 다방향 군락 (기본 잔디 70%)
            Mesh pureClusterMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_MultiDirectionalGrassCluster_CustomMesh.asset");
            if (pureClusterMesh != null && pureGrassMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🌾 [하층 2D 지피] 온대 순수 잔디 다방향 군락",
                    mesh = pureClusterMesh,
                    material = pureGrassMat,
                    minScale = 0.95f,
                    maxScale = 1.25f,
                    heightMultiplier = 1.0f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.25f,
                    slopeSinkFactor = 0.15f,
                    densityThreshold = 0.05f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [1] 🌸 분홍 꽃망울 다방향 군락 (5%)
            Mesh flowerPinkMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_FlowerCluster_PinkBuds_Photo1_Mesh.asset");
            if (flowerPinkMesh != null && temFlowerMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🌸 [하층 2D 야생화] 분홍 꽃망울 다방향 군락 (Photo 1)",
                    mesh = flowerPinkMesh,
                    material = temFlowerMat,
                    minScale = 0.90f,
                    maxScale = 1.20f,
                    heightMultiplier = 1.0f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.25f,
                    slopeSinkFactor = 0.15f,
                    densityThreshold = 0.15f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [2] 💜 연보라 꽃송이 다방향 군락 (5%)
            Mesh flowerPurpleMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_FlowerCluster_PurplePetals_Photo2_Mesh.asset");
            if (flowerPurpleMesh != null && temFlowerMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "💜 [하층 2D 야생화] 연보라 꽃송이 다방향 군락 (Photo 2)",
                    mesh = flowerPurpleMesh,
                    material = temFlowerMat,
                    minScale = 0.90f,
                    maxScale = 1.20f,
                    heightMultiplier = 1.0f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.25f,
                    slopeSinkFactor = 0.15f,
                    densityThreshold = 0.15f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [3] 🧡 주황 꽃이삭 다방향 군락 (5%)
            Mesh flowerOrangeMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_FlowerCluster_OrangeSpikes_Photo3_Mesh.asset");
            if (flowerOrangeMesh != null && sbtFlowerMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🧡 [하층 2D 야생화] 주황 꽃이삭 다방향 군락 (Photo 3)",
                    mesh = flowerOrangeMesh,
                    material = sbtFlowerMat,
                    minScale = 0.90f,
                    maxScale = 1.20f,
                    heightMultiplier = 1.0f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.25f,
                    slopeSinkFactor = 0.15f,
                    densityThreshold = 0.15f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [4] 🍂 온대 마른 갈빛 지피 군락 (색감 변조용)
            Mesh autumnGrassMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_GrassCluster_Tem_Autumn_Mesh.asset");
            if (autumnGrassMesh != null && autumnGrassMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🍂 [하층 2D 지피] 온대 마른 풀밭 군락",
                    mesh = autumnGrassMesh,
                    material = autumnGrassMat,
                    minScale = 0.90f,
                    maxScale = 1.25f,
                    heightMultiplier = 0.95f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.25f,
                    slopeSinkFactor = 0.15f,
                    densityThreshold = 0.14f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [5] 🌿 중층 3D 방사형 풀포기
            Mesh clumpMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_RadialFan3D_Clump_Mesh.asset");
            if (clumpMesh != null && clumpSpringMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🌿 [중층 3D 풀포기] 방사형 들풀 포기",
                    mesh = clumpMesh,
                    material = clumpSpringMat,
                    minScale = 0.90f,
                    maxScale = 1.25f,
                    heightMultiplier = 1.05f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.20f,
                    slopeSinkFactor = 0.18f,
                    stealthCoverage = 0.35f,
                    speedMultiplier = 0.90f,
                    densityThreshold = 0.20f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [6] 🎋 상층 키 큰 갈대 (Plume)
            Mesh plumeMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_TallReed3D_Plume_Mesh.asset");
            if (plumeMesh != null && tallGrassMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🎋 [상층 3D 갈대] 키 큰 갈대 이삭",
                    mesh = plumeMesh,
                    material = tallGrassMat,
                    minScale = 0.90f,
                    maxScale = 1.20f,
                    heightMultiplier = 1.05f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.12f,
                    slopeSinkFactor = 0.18f,
                    stealthCoverage = 0.45f,
                    speedMultiplier = 0.88f,
                    densityThreshold = 0.25f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [7] 🌾 상층 직립 이삭 (Spike)
            Mesh spikeMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_TallReed3D_Spike_Mesh.asset");
            if (spikeMesh != null && spikeMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🌾 [상층 3D 이삭] 직립 벼이삭 풀",
                    mesh = spikeMesh,
                    material = spikeMat,
                    minScale = 0.90f,
                    maxScale = 1.20f,
                    heightMultiplier = 1.05f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.12f,
                    slopeSinkFactor = 0.18f,
                    stealthCoverage = 0.40f,
                    speedMultiplier = 0.90f,
                    densityThreshold = 0.26f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }

            // [8] 🌲 저층 관목 검고사리 (Sword Fern)
            Mesh swordFernMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/TotalWar_ForestFern_Sword_Mesh.asset");
            if (swordFernMesh != null && fernMat != null)
            {
                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = "🌲 [관목 고사리] 음지 검고사리",
                    mesh = swordFernMesh,
                    material = fernMat,
                    minScale = 0.95f,
                    maxScale = 1.30f,
                    heightMultiplier = 1.0f,
                    groundEmbedOffset = -0.015f,
                    alignToGround = 0.30f,
                    slopeSinkFactor = 0.18f,
                    stealthCoverage = 0.50f,
                    speedMultiplier = 0.85f,
                    densityThreshold = 0.22f,
                    targetBiome = FoliageBiomeType.PlainsCarpet
                });
            }
#endif
        }

        private static Mesh ResolveRefinedAssetMesh(string modelFile)
        {
#if UNITY_EDITOR
            Mesh direct = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/" + modelFile);
            if (direct != null) return direct;

            string fallbackFile = null;
            if (modelFile.StartsWith("TotalWar_RadialFan3D_") && (modelFile.EndsWith("_Spring_Mesh.asset") || modelFile.EndsWith("_Summer_Mesh.asset") || modelFile.EndsWith("_Autumn_Mesh.asset") || modelFile.EndsWith("_Winter_Mesh.asset") || modelFile.EndsWith("_Arid_Mesh.asset") || modelFile.EndsWith("_Harvest_Mesh.asset")))
                fallbackFile = "TotalWar_RadialFan3D_Clump_Mesh.asset";
            else if (modelFile.StartsWith("TotalWar_TallReed3D_") && (modelFile.EndsWith("_Spring_Mesh.asset") || modelFile.EndsWith("_Summer_Mesh.asset") || modelFile.EndsWith("_Autumn_Mesh.asset") || modelFile.EndsWith("_Arid_Mesh.asset")))
                fallbackFile = "TotalWar_TallReed3D_Plume_Mesh.asset";
            else if (modelFile.StartsWith("TotalWar_ForestFern_Sword"))
                fallbackFile = "TotalWar_ForestFern_Sword_Mesh.asset";
            else if (modelFile.StartsWith("TotalWar_ForestFern_Royal"))
                fallbackFile = "TotalWar_ForestFern_Royal_Mesh.asset";
            else if (modelFile.StartsWith("TotalWar_ForestFern_Wood"))
                fallbackFile = "TotalWar_ForestFern_Wood_Mesh.asset";
            else if (modelFile == "TotalWar_GrassCluster_Ard_Spring_Mesh.asset")
                fallbackFile = "TotalWar_GrassCluster_Ard_Summer_Mesh.asset";
            else if (modelFile == "TotalWar_GrassCluster_Ard_Winter_Mesh.asset")
                fallbackFile = "TotalWar_GrassCluster_Ard_Autumn_Mesh.asset";
            else if (modelFile == "TotalWar_GrassCluster_ArdForest_Autumn_Mesh.asset")
                fallbackFile = "TotalWar_GrassCluster_TemForest_Autumn_Mesh.asset";

            if (!string.IsNullOrEmpty(fallbackFile))
            {
                return AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Settings/Vegetation/RadialMeshes/" + fallbackFile);
            }
#endif
            return null;
        }

        /// <summary>
        /// 🎨 [식생 모델 팔레트 동기화]
        /// TotalWarTerrainManager의 customGrassList 설정을 읽어들여 3D 메쉬와 인스턴싱 머티리얼을 자동 로드 및 프로토타입화합니다.
        /// </summary>
        public void SyncPrototypesFromConfigs(List<RadialGrassConfig> configs)
        {
            if (configs == null || configs.Count == 0)
            {
                EnsureDefaultPrototypes(true);
                return;
            }

            prototypes = new List<TacticalGrassPrototype>();

#if UNITY_EDITOR
            // 사계절 & 생태계별 대표 머티리얼 캐시 로드
            Material defaultSpringMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Tem_Spring_Mat.mat");
            if (defaultSpringMat == null) defaultSpringMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_Spring_CarpetGrass_Mat.mat");
            if (defaultSpringMat != null) defaultSpringMat.enableInstancing = true;

            Material summerMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Tem_Summer_Mat.mat");
            if (summerMat == null) summerMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Sbt_Summer_Mat.mat");
            if (summerMat != null) summerMat.enableInstancing = true;

            Material autumnMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Tem_Autumn_Mat.mat");
            if (autumnMat == null) autumnMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_TallReed3D_Autumn_Mat.mat");
            if (autumnMat != null) autumnMat.enableInstancing = true;

            Material winterMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Cld_Winter_Mat.mat");
            if (winterMat != null) winterMat.enableInstancing = true;

            Material aridMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_Ard_Summer_Mat.mat");
            if (aridMat != null) aridMat.enableInstancing = true;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null || !cfg.isEnabled) continue;

                // 3D OBJ 또는 .asset 메쉬 로드 (RadialMeshes(계절 폴백 포함) -> Shrubs -> Subtropical -> Temperate -> Cold -> Arid -> DetailGrass 탐색)
                Mesh rawMesh = null;
                if (cfg.modelFile.EndsWith(".asset"))
                {
                    rawMesh = ResolveRefinedAssetMesh(cfg.modelFile);
                }
                if (rawMesh == null) rawMesh = LoadMeshSafe("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/" + cfg.modelFile);
                if (rawMesh == null) rawMesh = LoadMeshSafe("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Subtropical/" + cfg.modelFile);
                if (rawMesh == null) rawMesh = LoadMeshSafe("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Temperate/" + cfg.modelFile);
                if (rawMesh == null) rawMesh = LoadMeshSafe("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Cold/" + cfg.modelFile);
                if (rawMesh == null) rawMesh = LoadMeshSafe("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Arid/" + cfg.modelFile);
                if (rawMesh == null) rawMesh = LoadMeshSafe("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/" + cfg.modelFile);

                if (rawMesh == null) continue;

                // 🌿 표준 군락 또는 3D 정제 메쉬 바인딩
                Mesh mesh = rawMesh;
                bool isRefinedProceduralAsset = cfg.modelFile.EndsWith(".asset") ||
                                                rawMesh.name.Contains("MultiDirectionalGrassCluster") ||
                                                rawMesh.name.Contains("FlowerCluster") ||
                                                rawMesh.name.Contains("GrassCluster") ||
                                                rawMesh.name.Contains("RadialFan3D") ||
                                                rawMesh.name.Contains("TallReed3D") ||
                                                rawMesh.name.Contains("ForestFern") ||
                                                rawMesh.name.EndsWith("_Clean3DTuft");

                if (isRefinedProceduralAsset)
                {
                    mesh = rawMesh;
                }
                else if (cfg.displayName.Contains("갈대") || cfg.displayName.Contains("Tall Reed"))
                {
                    mesh = GetOrCreateClean3DTuftMesh(rawMesh, cfg.flipY, 0.35f, 0.75f);
                }
                else if (cfg.displayName.Contains("수풀") || cfg.displayName.Contains("Thick Bush"))
                {
                    mesh = GetOrCreateClean3DTuftMesh(rawMesh, cfg.flipY, 0.40f, 0.50f);
                }
                else if (!cfg.displayName.Contains("예외"))
                {
                    mesh = GetOrCreateGroundedTuftMesh(rawMesh, cfg.flipY, 0.58f);
                }

                // 🎨 [100% 텍스처/계절 기반 정밀 머티리얼 매핑]
                Material mat = null;
                string baseName = System.IO.Path.GetFileNameWithoutExtension(cfg.modelFile);
                string albedoRawName = System.IO.Path.GetFileNameWithoutExtension(cfg.albedoFile);

                // 0순위: 4대 유형 정제 메쉬 전용 1:1 머티리얼 직결 로드
                if (cfg.modelFile.EndsWith(".asset"))
                {
                    if (baseName.Contains("MultiDirectionalGrassCluster"))
                        mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_GrassCluster_PureSu_Mat.mat");
                    else if (baseName.Contains("PinkBuds") || baseName.Contains("PurplePetals"))
                        mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_TemGrassSp_Flower_Mat.mat");
                    else if (baseName.Contains("OrangeSpikes"))
                        mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Vegetation/RadialMaterials/TotalWar_SbtGrass0Sp_Flower_Mat.mat");
                    else if (baseName.EndsWith("_Mesh"))
                    {
                        string directMatPath = "Assets/Settings/Vegetation/RadialMaterials/" + baseName.Substring(0, baseName.Length - 5) + "_Mat.mat";
                        mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(directMatPath);

                        // 공용 기하 메쉬(Clump/Plume/ForestFern)의 계절별 머티리얼 자동 매핑
                        if (mat == null)
                        {
                            string infoLower = (cfg.albedoFile + " " + cfg.displayName).ToLower();
                            string seasonTag = infoLower.Contains("_au_") || infoLower.Contains("가을") ? "Autumn"
                                : (infoLower.Contains("_wi_") || infoLower.Contains("겨울") ? "Winter"
                                : (infoLower.Contains("_su_") || infoLower.Contains("여름") ? "Summer" : "Spring"));

                            if (baseName.Contains("RadialFan3D_Clump"))
                                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Settings/Vegetation/RadialMaterials/TotalWar_RadialFan3D_{seasonTag}_Mat.mat");
                            else if (baseName.Contains("TallReed3D_Plume"))
                                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Settings/Vegetation/RadialMaterials/TotalWar_TallReed3D_{seasonTag}_Mat.mat");
                            else if (baseName.Contains("ForestFern_"))
                                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Settings/Vegetation/RadialMaterials/TotalWar_ForestFern_{seasonTag}_Mat.mat");
                        }
                    }
                }

                if (mat == null && !string.IsNullOrEmpty(albedoRawName))
                {
                    string[] foundGuids = UnityEditor.AssetDatabase.FindAssets($"{baseName}_{albedoRawName} t:Material", new[] { "Assets/Settings/Vegetation/RadialMaterials" });
                    if (foundGuids != null && foundGuids.Length > 0)
                    {
                        mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(UnityEditor.AssetDatabase.GUIDToAssetPath(foundGuids[0]));
                    }
                }

                if (mat == null)
                {
                    string[] foundGuids = UnityEditor.AssetDatabase.FindAssets(baseName + " t:Material", new[] { "Assets/Settings/Vegetation/RadialMaterials" });
                    if (foundGuids != null && foundGuids.Length > 0)
                    {
                        foreach (var g in foundGuids)
                        {
                            string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                            if (cfg.albedoFile.Contains("_au_") && p.Contains("_au_")) { mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p); break; }
                            if (cfg.albedoFile.Contains("_wi_") && p.Contains("_wi_")) { mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p); break; }
                            if (cfg.albedoFile.Contains("_sp_") && p.Contains("_sp_")) { mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p); break; }
                            if (cfg.albedoFile.Contains("_su_") && p.Contains("_su_")) { mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p); break; }
                            if (!cfg.albedoFile.Contains("_au_") && !cfg.albedoFile.Contains("_wi_") && !cfg.albedoFile.Contains("_sp_") && !cfg.albedoFile.Contains("_su_"))
                            {
                                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
                                break;
                            }
                        }
                    }
                }

                if (mat == null)
                {
                    string lowerInfo = (cfg.albedoFile + " " + cfg.displayName).ToLower();
                    if (lowerInfo.Contains("_au_") || lowerInfo.Contains("가을") || lowerInfo.Contains("황금") || lowerInfo.Contains("수확") || lowerInfo.Contains("추수") || lowerInfo.Contains("벼") || lowerInfo.Contains("기장"))
                        mat = autumnMat;
                    else if (lowerInfo.Contains("_wi_") || lowerInfo.Contains("겨울") || lowerInfo.Contains("설원") || lowerInfo.Contains("눈") || lowerInfo.Contains("혹한"))
                        mat = winterMat;
                    else if (lowerInfo.Contains("_su_") || lowerInfo.Contains("여름") || lowerInfo.Contains("윤기"))
                        mat = summerMat;
                    else if (lowerInfo.Contains("base_colour") || lowerInfo.Contains("건조") || lowerInfo.Contains("마른") || lowerInfo.Contains("사막") || lowerInfo.Contains("황무지") || lowerInfo.Contains("암반"))
                        mat = aridMat;
                    else
                        mat = defaultSpringMat;
                }

                if (mat != null) mat.enableInstancing = true;

                // 바이옴 자동 판별
                FoliageBiomeType bType = FoliageBiomeType.PlainsCarpet;
                string lower = (cfg.displayName + " " + cfg.modelFile).ToLower();
                if (lower.Contains("fern") || lower.Contains("고사리") || lower.Contains("forest") || lower.Contains("수림"))
                    bType = FoliageBiomeType.ForestShade;
                else if (lower.Contains("dry") || lower.Contains("건조") || lower.Contains("고지대") || lower.Contains("rock"))
                    bType = FoliageBiomeType.HighlandDry;
                else if (lower.Contains("wetland") || lower.Contains("습지") || lower.Contains("lotus") || lower.Contains("연꽃") || lower.Contains("갈대"))
                    bType = FoliageBiomeType.RiverWetland;

                // 🌾 [높이 배율 및 물리 속성 설정: 정제된 4대 유형 .asset 메쉬는 이미 실제 월드 높이가 정점에 구워져 있으므로 1.0f 비례 유지]
                float hMult = isRefinedProceduralAsset
                    ? Mathf.Clamp(cfg.maxHeight, 0.85f, 1.30f)
                    : Mathf.Clamp(cfg.maxHeight, 0.32f, 0.52f);
                float defaultEmbedOffset = -0.015f;
                float defaultAlign = cfg.alignToGround > 0.01f ? Mathf.Clamp(cfg.alignToGround, 0.08f, 0.40f) : 0.25f;
                float defaultSink = 0.18f;
                float defaultStealth = (baseName.Contains("TallReed") || baseName.Contains("RadialFan3D")) ? 0.35f : 0.0f;
                float defaultSpeedMult = (baseName.Contains("TallReed") || baseName.Contains("RadialFan3D")) ? 0.90f : 0.96f;
                float defaultFlammability = 0.45f;
                float defaultBurnDuration = 4.5f;

                float finalEmbedOffset = Mathf.Abs(cfg.groundOffset) > 0.001f ? Mathf.Min(cfg.groundOffset, -0.01f) : defaultEmbedOffset;

                prototypes.Add(new TacticalGrassPrototype
                {
                    protoName = $"[{i}] {cfg.displayName}",
                    mesh = mesh,
                    material = mat,
                    minScale = Mathf.Max(0.85f, cfg.minWidth),
                    maxScale = Mathf.Max(1.15f, cfg.maxWidth),
                    heightMultiplier = hMult, // flipY는 GetOrCreateGroundedTuftMesh에서 이미 정방향 버텍스로 변환 완료!
                    groundEmbedOffset = finalEmbedOffset,
                    alignToGround = defaultAlign,
                    slopeSinkFactor = defaultSink,
                    stealthCoverage = defaultStealth,
                    speedMultiplier = defaultSpeedMult,
                    flammability = defaultFlammability,
                    burnDuration = defaultBurnDuration,
                    densityThreshold = cfg.noiseThreshold,
                    targetBiome = bType
                });
            }
#endif

            if (prototypes.Count == 0)
            {
                EnsureDefaultPrototypes(true);
            }
        }

        public ChunkFoliageOverride GetOverrideForChunk(Vector2Int cCoord)
        {
            if (chunkOverrides == null) return null;
            for (int i = 0; i < chunkOverrides.Count; i++)
            {
                if (chunkOverrides[i] != null && chunkOverrides[i].chunkCoord == cCoord)
                    return chunkOverrides[i];
            }
            return null;
        }

        /// <summary>
        /// 특정 청크의 바이옴 판별 (커스텀 오버라이드 -> 숲/수계/경사도 자동 판정)
        /// </summary>
        public FoliageBiomeType GetBiomeForChunk(Vector2Int cCoord, Vector3 chunkCenterPos)
        {
            if (chunkOverrides != null)
            {
                for (int i = 0; i < chunkOverrides.Count; i++)
                {
                    if (chunkOverrides[i] != null && chunkOverrides[i].chunkCoord == cCoord)
                    {
                        return chunkOverrides[i].biomeType;
                    }
                }
            }

            // 1. 숲 구역 내부 검사
            if (TacticalForestZone.IsPointInAnyForest(chunkCenterPos))
            {
                return FoliageBiomeType.ForestShade;
            }

            // 2. 하천/워터존 근처 검사 (25m 이내)
            if (TotalWarRiverSpline.IsAnyRiverAt(chunkCenterPos, out _) || TacticalWaterZone.IsAnyWaterZoneAt(chunkCenterPos, out _))
            {
                return FoliageBiomeType.RiverWetland;
            }

            // 3. 고지대 및 경사도 검사
            if (targetTerrain != null && targetTerrain.terrainData != null)
            {
                Vector3 tPos = targetTerrain.transform.position;
                Vector3 tSize = targetTerrain.terrainData.size;
                float normX = Mathf.Clamp01((chunkCenterPos.x - tPos.x) / tSize.x);
                float normZ = Mathf.Clamp01((chunkCenterPos.z - tPos.z) / tSize.z);
                float slope = targetTerrain.terrainData.GetSteepness(normX, normZ);
                if (slope > 18f)
                {
                    return FoliageBiomeType.HighlandDry;
                }
            }

            return FoliageBiomeType.PlainsCarpet;
        }

        public float GetDensityMultiplierForChunk(Vector2Int cCoord)
        {
            if (chunkOverrides != null)
            {
                for (int i = 0; i < chunkOverrides.Count; i++)
                {
                    if (chunkOverrides[i] != null && chunkOverrides[i].chunkCoord == cCoord)
                    {
                        return chunkOverrides[i].densityMultiplier;
                    }
                }
            }
            return 1.0f;
        }

        /// <summary>
        /// 🌿 [경사면 + 볼록 능선 5점 샘플링 접지 및 법선 블렌딩 핵심 알고리즘]
        /// 중심점뿐만 아니라 동서남북 둘레 4점의 지형 높이를 함께 측정하여,
        /// 경사면과 볼록한 언덕 능선(Convex Ridge)에서도 풀포기 가장자리가 절대 허공에 뜨지 않고 지면 곡면을 따라 착 감기도록 계산합니다.
        /// </summary>
        private (Vector3 pos, Quaternion rot, Vector3 scale) CalculateSlopeGroundedTransform(
            TerrainData tData,
            Vector3 terPos,
            Vector3 terSize,
            float sampleX,
            float sampleZ,
            TacticalGrassPrototype proto,
            float defaultAlignToGround = 0.32f,
            float baseSink = 0.015f)
        {
            float normX = Mathf.Clamp01((sampleX - terPos.x) / terSize.x);
            float normZ = Mathf.Clamp01((sampleZ - terPos.z) / terSize.z);

            float centerHeight = tData.GetInterpolatedHeight(normX, normZ);
            Vector3 normal = tData.GetInterpolatedNormal(normX, normZ);
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            else normal.Normalize();

            float slopeAngle = Vector3.Angle(Vector3.up, normal);

            // 1. 스케일 계산 및 급경사 자동 콤팩트화
            float rndScale = UnityEngine.Random.Range(proto.minScale, proto.maxScale);
            if (slopeAngle > 20f)
            {
                float slopeCompactFactor = Mathf.Lerp(1.0f, 0.82f, Mathf.Clamp01((slopeAngle - 20f) / 25f));
                rndScale *= slopeCompactFactor;
            }
            float hMult = Mathf.Abs(proto.heightMultiplier);
            Vector3 finalScale = new Vector3(rndScale, rndScale * hMult, rndScale);

            // 2. 동서남북 둘레 4점(반경 checkR) 지형 높이 다중 샘플링 -> 볼록한 둔덕/능선(Convex Ridge) 들뜸 방지
            float checkR = 0.35f * rndScale;
            float hE = tData.GetInterpolatedHeight(Mathf.Clamp01((sampleX + checkR - terPos.x) / terSize.x), normZ);
            float hW = tData.GetInterpolatedHeight(Mathf.Clamp01((sampleX - checkR - terPos.x) / terSize.x), normZ);
            float hN = tData.GetInterpolatedHeight(normX, Mathf.Clamp01((sampleZ + checkR - terPos.z) / terSize.z));
            float hS = tData.GetInterpolatedHeight(normX, Mathf.Clamp01((sampleZ - checkR - terPos.z) / terSize.z));

            float avgPerimeterHeight = (hE + hW + hN + hS) * 0.25f;
            float minPerimeterHeight = Mathf.Min(Mathf.Min(hE, hW), Mathf.Min(hN, hS));

            // 볼록한 능선일수록 중심 높이가 둘레 평균보다 높으므로, 그 차이만큼 피벗을 자연스럽게 낮추되 최대 1.2cm로 제한
            float convexRidgeDrop = Mathf.Clamp(Mathf.Max(0f, centerHeight - avgPerimeterHeight) * 0.3f, 0f, 0.012f);

            // 3. 지면 접지 계산 (뿌리가 Y=0에 밀착 정규화되었으므로 땅속에 과도하게 파묻히지 않게 지표면 정위치 안착)
            // 🌿 [기립력 보장: 법선 정렬 비율 최대 0.35f 캡]
            // 식물은 굴광성/중력반응으로 경사면에서도 하늘을 향해 서 있어야 합니다.
            // 법선 정렬 비율을 최대 0.35f로 제한하여 비탈길에서도 잎이 지면 아래로 거꾸로 곤두박질치는 현상을 방지합니다.
            float requestedAlign = proto.alignToGround > 0f ? proto.alignToGround : defaultAlignToGround;
            float align = Mathf.Clamp(requestedAlign, 0.15f, 0.35f);

            float sinkFactor = proto.slopeSinkFactor > 0f ? proto.slopeSinkFactor : 0.10f;
            float slopeSink = Mathf.Clamp(Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * (rndScale * sinkFactor * 0.25f), 0f, 0.015f);
            float embedOffset = Mathf.Max(0.002f, proto.groundEmbedOffset); // 땅 밑 과도한 침하 방지 (지표면 정위치 보장)
            float actualBaseSink = Mathf.Clamp(baseSink, 0f, 0.005f);
            float finalY = terPos.y + centerHeight + embedOffset - actualBaseSink - slopeSink - convexRidgeDrop;
            Vector3 finalPos = new Vector3(sampleX, finalY, sampleZ);

            // 4. 지형 법선(Normal) 구면 보간(Slerp) 블렌딩 (35%만 경사 추종, 65%는 수직 하늘 기립)
            Vector3 blendedNormal = Vector3.Slerp(Vector3.up, normal, align).normalized;
            Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, blendedNormal);
            Quaternion yawRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            Quaternion finalRot = slopeRot * yawRot;

            return (finalPos, finalRot, finalScale);
        }

        /// <summary>
        /// 🌾 [터레인 스플랫맵 및 청크 바이옴 기반 풀 데이터 빌드]
        /// 청크별 바이옴(평원/숲그늘/고지대/수변) 및 밀도 배율을 반영하고, 경사면 노멀에 완벽 밀착하여 바닥에 달라붙게 생성
        /// </summary>
        [ContextMenu("🌾 전장 풀 데이터 즉시 생성 (Bake)")]
        public void BakeFoliageData()
        {
            if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                Debug.LogWarning("[TacticalGrassRenderer] 대상 터레인을 찾을 수 없습니다.");
                return;
            }

            if (prototypes == null || prototypes.Count == 0)
            {
                EnsureDefaultPrototypes(false);
            }
            if (prototypes.Count == 0)
            {
                Debug.LogWarning("[TacticalGrassRenderer] 등록된 풀 프로토타입이 없습니다.");
                return;
            }

            // 모든 프로토타입 메쉬를 지면 100% 밀착형 1.15m 개별 풀포기(_GroundedTuft)로 정규화 보장
            EnsureAllPrototypesGrounded();

            _chunks.Clear();
            _runtimeDeltas.Clear();
            _totalInstanceCount = 0;

            TerrainData tData = targetTerrain.terrainData;
            Vector3 terPos = targetTerrain.transform.position;
            Vector3 terSize = tData.size;

            int alphaRes = tData.alphamapResolution;
            int layerCount = tData.terrainLayers != null ? tData.terrainLayers.Length : 0;
            float[,,] splatmap = (alphaRes > 0 && layerCount > 0) ? tData.GetAlphamaps(0, 0, alphaRes, alphaRes) : null;

            int stepsX = Mathf.FloorToInt(terSize.x / sampleSpacing);
            int stepsZ = Mathf.FloorToInt(terSize.z / sampleSpacing);

            Dictionary<Vector2Int, List<(Vector3 pos, Quaternion rot, Vector3 scale, int protoIdx)>> chunkRawData = 
                new Dictionary<Vector2Int, List<(Vector3 pos, Quaternion rot, Vector3 scale, int protoIdx)>>();

            UnityEngine.Random.InitState(1234);

            for (int z = 0; z < stepsZ; z++)
            {
                for (int x = 0; x < stepsX; x++)
                {
                    // 🌿 [기계적 바둑판/점무늬 격자 완전 탈피]: 셀 중심 좌표를 고정 격자에서 ±0.44칸 유기적으로 지터링
                    float jitterStepX = UnityEngine.Random.Range(-0.44f, 0.44f) * sampleSpacing;
                    float jitterStepZ = UnityEngine.Random.Range(-0.44f, 0.44f) * sampleSpacing;
                    float worldX = Mathf.Clamp(terPos.x + ((float)x + 0.5f) / stepsX * terSize.x + jitterStepX, terPos.x + 0.25f, terPos.x + terSize.x - 0.25f);
                    float worldZ = Mathf.Clamp(terPos.z + ((float)z + 0.5f) / stepsZ * terSize.z + jitterStepZ, terPos.z + 0.25f, terPos.z + terSize.z - 0.25f);
                    float normX = (worldX - terPos.x) / terSize.x;
                    float normZ = (worldZ - terPos.z) / terSize.z;

                    // 1. 경사도 검사 (32도 초과 절벽 바위 배제)
                    float slope = tData.GetSteepness(normX, normZ);
                    if (slope > 32f) continue;

                    float height = tData.GetInterpolatedHeight(normX, normZ);
                    Vector3 samplePos = new Vector3(worldX, terPos.y + height, worldZ);

                    // 2. 도로망/하천/워터존/바다 수면 아래 배제 (0%)
                    if (TotalWarRoadSpline.IsOnAnyRoad(samplePos)) continue;
                    if (TotalWarRiverSpline.IsAnyRiverAt(samplePos, out _)) continue;
                    if (TacticalWaterZone.IsAnyWaterZoneAt(samplePos, out _)) continue;
                    if (TotalWarSeaPlane.Instance != null && samplePos.y < TotalWarSeaPlane.Instance.waterLevelY) continue;

                    // 3. 지형 스플랫맵 잔디 가중치 검사 + 경계부 자연스러운 확률 감쇄 (칼 같은 직각 경계 제거)
                    float grassWeight = 1.0f;
                    if (splatmap != null && layerCount > 0)
                    {
                        int ax = Mathf.Clamp(Mathf.RoundToInt(normX * (alphaRes - 1)), 0, alphaRes - 1);
                        int ay = Mathf.Clamp(Mathf.RoundToInt(normZ * (alphaRes - 1)), 0, alphaRes - 1);

                        float gVal = splatmap[ay, ax, 0];
                        for (int l = 1; l < layerCount; l++)
                        {
                            var layer = tData.terrainLayers[l];
                            if (layer != null && layer.name != null && layer.name.ToLower().Contains("grass"))
                            {
                                gVal += splatmap[ay, ax, l];
                            }
                        }
                        grassWeight = Mathf.Clamp01(gVal);
                    }

                    if (grassWeight < 0.08f) continue;
                    float edgeKeepProb = Mathf.SmoothStep(0.05f, 1.0f, (grassWeight - 0.08f) / 0.42f);
                    if (UnityEngine.Random.value > edgeKeepProb) continue;

                    // 자연스러운 대군락(Macro Patch) & 미시 공백(Micro Gap) 페를린 노이즈 모듈레이션
                    float macroPatch = Mathf.PerlinNoise(worldX * 0.032f + 13.7f, worldZ * 0.032f + 7.1f);
                    float microPatch = Mathf.PerlinNoise(worldX * 0.14f + 4.3f, worldZ * 0.14f + 9.2f);
                    float patchDensity = macroPatch * 0.65f + microPatch * 0.35f;
                    if (patchDensity < 0.25f && UnityEngine.Random.value < 0.65f) continue;

                    // 4. 청크 좌표 및 바이옴/밀도 쿼리
                    int cx = Mathf.FloorToInt((worldX - terPos.x) / chunkSize);
                    int cz = Mathf.FloorToInt((worldZ - terPos.z) / chunkSize);
                    Vector2Int cKey = new Vector2Int(cx, cz);

                    Vector3 chunkCenter = new Vector3(terPos.x + (cx + 0.5f) * chunkSize, samplePos.y, terPos.z + (cz + 0.5f) * chunkSize);
                    FoliageBiomeType biome = GetBiomeForChunk(cKey, chunkCenter);
                    if (biome == FoliageBiomeType.BarrenOrClear) continue;

                    float densityMult = GetDensityMultiplierForChunk(cKey);
                    if (densityMult <= 0.01f) continue;

                    // 청크 밀도 배율 확률 검사
                    if (densityMult < 1.0f && UnityEngine.Random.value > densityMult) continue;

                    // 5. 프로토타입 후보군 선별 (청크별 지정 모델 우선, 없으면 바이옴 기준 -> 다층 혼합 보장)
                    List<int> candidateIndices = new List<int>();
                    var chunkOverride = GetOverrideForChunk(cKey);
                    if (chunkOverride != null && chunkOverride.allowedModelIndices != null && chunkOverride.allowedModelIndices.Count > 0)
                    {
                        foreach (int idx in chunkOverride.allowedModelIndices)
                        {
                            if (idx >= 0 && idx < prototypes.Count)
                            {
                                candidateIndices.Add(idx);
                            }
                        }
                    }

                    if (candidateIndices.Count == 0)
                    {
                        for (int p = 0; p < prototypes.Count; p++)
                        {
                            if (prototypes[p].targetBiome == biome) candidateIndices.Add(p);
                        }

                        // 다종 생태계 풍부화를 위해 해당 바이옴에 상층/관목 풀이 부족하면 전체 팔레트 포함
                        if (candidateIndices.Count < 4)
                        {
                            candidateIndices.Clear();
                            for (int p = 0; p < prototypes.Count; p++)
                            {
                                candidateIndices.Add(p);
                            }
                        }
                    }
                    if (candidateIndices.Count == 0)
                    {
                        candidateIndices.Add(0);
                    }

                    if (!chunkRawData.TryGetValue(cKey, out var list))
                    {
                        list = new List<(Vector3 pos, Quaternion rot, Vector3 scale, int protoIdx)>();
                        chunkRawData[cKey] = list;
                    }

                    // 🌿 [핵심: 삼탈워 본편 1:1 고증 복합 다층 생태계 살포 알고리즘]
                    int baseProto = candidateIndices[0];

                    if (enableMultiSpeciesBlending && candidateIndices.Count > 1)
                    {
                        // 1. 후보 프로토타입들을 생태계 카테고리별로 분류 (한국어/영문 키워드 전수 포함)
                        List<int> carpetPool = new List<int>();  // 납작 바닥 지피 잔디
                        List<int> tallPool = new List<int>();    // 키 큰 이삭/벼/갈대/3D 풀포기
                        List<int> flowerPool = new List<int>();  // 야생화 및 꽃류
                        List<int> fernShrubPool = new List<int>(); // 고사리 및 관목 덤불

                        for (int c = 0; c < candidateIndices.Count; c++)
                        {
                            int pIdx = candidateIndices[c];
                            string pName = prototypes[pIdx].protoName.ToLower();

                            if (pName.Contains("flower") || pName.Contains("꽃") || pName.Contains("lotus") || pName.Contains("magnolia") || pName.Contains("rhododendron") || pName.Contains("convallaria"))
                                flowerPool.Add(pIdx);
                            else if (pName.Contains("fern") || pName.Contains("고사리") || pName.Contains("thick") || pName.Contains("bush") || pName.Contains("덤불") || pName.Contains("관목") || pName.Contains("palm") || pName.Contains("peliosanthes"))
                                fernShrubPool.Add(pIdx);
                            else if (pName.Contains("seedhead") || pName.Contains("이삭") || pName.Contains("갈대") || pName.Contains("풀포기") || pName.Contains("방사형") || pName.Contains("tall") || pName.Contains("millet") || pName.Contains("rice") || pName.Contains("grain") || pName.Contains("reed") || pName.Contains("spike") || pName.Contains("clump") || pName.Contains("radialfan"))
                                tallPool.Add(pIdx);
                            else
                                carpetPool.Add(pIdx);
                        }

                        if (carpetPool.Count == 0) carpetPool.AddRange(candidateIndices);

                        // 🌿 [Layer 1: 유기적 가변 밀도 지피 잔디 군락 (고정 3방 링 탈피)]
                        float posX = worldX;
                        float posZ = worldZ;
                        int tuftCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 4f, patchDensity * edgeKeepProb)), 1, 4);
                        float cellRadius = sampleSpacing * 0.68f;
                        float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                        float scalePatchBias = Mathf.Lerp(0.84f, 1.24f, macroPatch);

                        for (int t = 0; t < tuftCount; t++)
                        {
                            // 페를린 + 해시 혼합으로 종별 패치가 넓게 뭉쳐서 자연스럽게 어우러지도록 선택
                            int subCarpetIdx = (UnityEngine.Random.value < 0.35f)
                                ? carpetPool[UnityEngine.Random.Range(0, carpetPool.Count)]
                                : carpetPool[Mathf.Abs(Mathf.FloorToInt(macroPatch * 97f) + cx * 13 + cz * 7 + t) % carpetPool.Count];
                            var protoBase = prototypes[subCarpetIdx];

                            float goldenAngle = baseAngle + t * 2.39996323f + UnityEngine.Random.Range(-0.45f, 0.45f);
                            float rNorm = Mathf.Sqrt(UnityEngine.Random.Range(0.08f, 1.0f));
                            float r = rNorm * cellRadius;

                            float subX = posX + Mathf.Cos(goldenAngle) * r;
                            float subZ = posZ + Mathf.Sin(goldenAngle) * r;

                            var tSub = CalculateSlopeGroundedTransform(tData, terPos, terSize, subX, subZ, protoBase, 0.32f, 0.015f);
                            list.Add((tSub.pos, tSub.rot, tSub.scale * scalePatchBias, subCarpetIdx));
                            _totalInstanceCount++;
                        }

                        // 🌾 [Layer 2: 중·상층 3D 입체 풀포기 & 키 큰 갈대/이삭] (키 높이 및 실루엣 다층화)
                        float effectiveTallChance = Mathf.Max(tallGrassChance, 0.22f) * Mathf.Lerp(0.6f, 1.4f, macroPatch) * edgeKeepProb;
                        if (tallPool.Count > 0 && UnityEngine.Random.value < effectiveTallChance)
                        {
                            int tallIdx = tallPool[UnityEngine.Random.Range(0, tallPool.Count)];
                            var protoTall = prototypes[tallIdx];

                            float sJitterX = posX + UnityEngine.Random.Range(-sampleSpacing * 0.45f, sampleSpacing * 0.45f);
                            float sJitterZ = posZ + UnityEngine.Random.Range(-sampleSpacing * 0.45f, sampleSpacing * 0.45f);

                            var t2 = CalculateSlopeGroundedTransform(tData, terPos, terSize, sJitterX, sJitterZ, protoTall, 0.28f, 0.015f);
                            list.Add((t2.pos, t2.rot, t2.scale * scalePatchBias, tallIdx));
                            _totalInstanceCount++;
                        }

                        // 🌼 [Layer 3: 2D 야생화 군락 레이어] (페를린 노이즈 기반 군락 개화)
                        if (flowerPool.Count > 0)
                        {
                            float flowerNoise = Mathf.PerlinNoise(posX * 0.055f + 3.1f, posZ * 0.055f + 9.4f);
                            if (flowerNoise > (1.0f - wildflowerPatchChance) || UnityEngine.Random.value < 0.08f)
                            {
                                int flowerIdx = flowerPool[Mathf.Abs(Mathf.FloorToInt(flowerNoise * 31f) + cx * 13 + cz * 29) % flowerPool.Count];
                                var protoFlower = prototypes[flowerIdx];

                                float fJitterX = posX + UnityEngine.Random.Range(-sampleSpacing * 0.45f, sampleSpacing * 0.45f);
                                float fJitterZ = posZ + UnityEngine.Random.Range(-sampleSpacing * 0.45f, sampleSpacing * 0.45f);

                                var t3 = CalculateSlopeGroundedTransform(tData, terPos, terSize, fJitterX, fJitterZ, protoFlower, 0.30f, 0.015f);
                                list.Add((t3.pos, t3.rot, t3.scale, flowerIdx));
                                _totalInstanceCount++;
                            }
                        }

                        // 🌲 [Layer 4: 저층 관목·고사리 수풀 레이어]
                        if (fernShrubPool.Count > 0 && UnityEngine.Random.value < 0.12f * Mathf.Lerp(0.7f, 1.5f, macroPatch) * edgeKeepProb)
                        {
                            float fnJitterX = posX + UnityEngine.Random.Range(-sampleSpacing * 0.45f, sampleSpacing * 0.45f);
                            float fnJitterZ = posZ + UnityEngine.Random.Range(-sampleSpacing * 0.45f, sampleSpacing * 0.45f);

                            int fernIdx = fernShrubPool[UnityEngine.Random.Range(0, fernShrubPool.Count)];
                            var protoFern = prototypes[fernIdx];

                            var t4 = CalculateSlopeGroundedTransform(tData, terPos, terSize, fnJitterX, fnJitterZ, protoFern, 0.30f, 0.015f);
                            list.Add((t4.pos, t4.rot, t4.scale * scalePatchBias, fernIdx));
                            _totalInstanceCount++;
                        }
                    }
                    else
                    {
                        // 단일 살포 모드에서도 가변 지터 분산으로 바둑판 눈금 완전 소거
                        var proto = prototypes[baseProto];
                        int tuftCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 4f, patchDensity * edgeKeepProb)), 1, 4);
                        float cellRadius = sampleSpacing * 0.68f;
                        float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                        for (int t = 0; t < tuftCount; t++)
                        {
                            float goldenAngle = baseAngle + t * 2.39996323f + UnityEngine.Random.Range(-0.45f, 0.45f);
                            float rNorm = Mathf.Sqrt(UnityEngine.Random.Range(0.08f, 1.0f));
                            float r = rNorm * cellRadius;
                            float pX = worldX + Mathf.Cos(goldenAngle) * r;
                            float pZ = worldZ + Mathf.Sin(goldenAngle) * r;

                            var tSingle = CalculateSlopeGroundedTransform(tData, terPos, terSize, pX, pZ, proto, 0.32f, 0.015f);
                            list.Add((tSingle.pos, tSingle.rot, tSingle.scale, baseProto));
                            _totalInstanceCount++;
                        }
                    }
                }
            }

            // 7. 청크 인스턴스 행렬 패킹 (1023개 배치 단위)
            int protoCount = prototypes.Count;
            foreach (var kvp in chunkRawData)
            {
                Vector2Int cKey = kvp.Key;
                var rawItems = kvp.Value;

                TacticalGrassChunk chunk = new TacticalGrassChunk
                {
                    chunkCoord = cKey,
                    biomeType = GetBiomeForChunk(cKey, new Vector3(terPos.x + (cKey.x + 0.5f) * chunkSize, terPos.y, terPos.z + (cKey.y + 0.5f) * chunkSize)),
                    densityMultiplier = GetDensityMultiplierForChunk(cKey)
                };
                chunk.InitializeBatches(protoCount);

                Vector3 chunkMin = new Vector3(terPos.x + cKey.x * chunkSize, terPos.y, terPos.z + cKey.y * chunkSize);
                Vector3 chunkMax = chunkMin + new Vector3(chunkSize, terSize.y, chunkSize);
                chunk.bounds = new Bounds((chunkMin + chunkMax) * 0.5f, chunkMax - chunkMin);

                List<Matrix4x4>[] tempBatchList = new List<Matrix4x4>[protoCount];
                for (int p = 0; p < protoCount; p++) tempBatchList[p] = new List<Matrix4x4>();

                float totalStealthScore = 0f;
                float totalSpeedSum = 0f;
                float totalFlammabilitySum = 0f;

                for (int i = 0; i < rawItems.Count; i++)
                {
                    var item = rawItems[i];
                    Matrix4x4 mat = Matrix4x4.TRS(item.pos, item.rot, item.scale);
                    tempBatchList[item.protoIdx].Add(mat);
                    chunk.worldPositions.Add(item.pos);
                    chunk.states.Add(FoliageState.Healthy);

                    if (item.protoIdx >= 0 && item.protoIdx < prototypes.Count)
                    {
                        var proto = prototypes[item.protoIdx];
                        totalStealthScore += proto.stealthCoverage * (item.scale.y / Mathf.Max(0.1f, Mathf.Abs(proto.heightMultiplier)));
                        totalSpeedSum += proto.speedMultiplier;
                        totalFlammabilitySum += proto.flammability;
                    }
                }

                // 🌿 [청크 전술 물리 값 계산]: 은폐율, 이동 저항, 인화성
                if (rawItems.Count > 0)
                {
                    chunk.chunkStealthPower = Mathf.Clamp01((totalStealthScore / 18f) * chunk.densityMultiplier);
                    chunk.chunkSpeedMultiplier = Mathf.Clamp(totalSpeedSum / rawItems.Count, 0.65f, 1.0f);
                    chunk.chunkFlammability = Mathf.Clamp01(totalFlammabilitySum / rawItems.Count);
                }
                else
                {
                    chunk.chunkStealthPower = 0f;
                    chunk.chunkSpeedMultiplier = 1.0f;
                    chunk.chunkFlammability = 0.5f;
                }

                for (int p = 0; p < protoCount; p++)
                {
                    var mList = tempBatchList[p];
                    chunk.protoBatchCounts[p] = mList.Count;

                    for (int start = 0; start < mList.Count; start += 1023)
                    {
                        int batchSize = Mathf.Min(1023, mList.Count - start);
                        Matrix4x4[] batch = new Matrix4x4[batchSize];
                        mList.CopyTo(start, batch, 0, batchSize);
                        chunk.protoBatches[p].Add(batch);
                    }
                }

                _chunks[cKey] = chunk;
            }

            _totalChunkCount = _chunks.Count;
            Debug.Log($"<color=#00FF88><b>[TacticalGrassRenderer] 🌾 전장 풀 데이터 빌드 완료! 총 {_totalChunkCount}개 청크, {_totalInstanceCount:N0}개 개별 풀포기가 지면 곡면에 100% 밀착 장착되었습니다.</b></color>");
        }

        #region 디스크 저장 및 로드 시스템 (Save & Load Pipeline)

        /// <summary>
        /// 💾 [디스크 영구 저장]: Assets/Settings/Vegetation/Data/[MapName]/ 하위에 Manifest.json 및 Chunks.bytes 저장
        /// </summary>
        [ContextMenu("💾 전장 식생 데이터 디스크에 저장 (Save)")]
        public void SaveFoliageDataToDisk()
        {
            if (_chunks.Count == 0)
            {
                Debug.LogWarning("[TacticalGrassRenderer] 저장할 풀 데이터가 없습니다. 먼저 Bake를 수행합니다.");
                BakeFoliageData();
            }

            string targetDir = GetTargetDataDirectory();
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 1. FoliageManifest.json 작성
            FoliageManifestData manifest = new FoliageManifestData
            {
                mapName = GetEffectiveFolderName(),
                saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                terrainPosition = (targetTerrain != null) ? targetTerrain.transform.position : Vector3.zero,
                terrainSize = (targetTerrain != null && targetTerrain.terrainData != null) ? targetTerrain.terrainData.size : Vector3.zero,
                chunkSize = chunkSize,
                sampleSpacing = sampleSpacing,
                totalChunkCount = _chunks.Count,
                totalInstanceCount = _totalInstanceCount
            };

            foreach (var p in prototypes)
            {
                manifest.prototypeNames.Add(p.protoName);
            }

            string manifestPath = Path.Combine(targetDir, "FoliageManifest.json");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

            // 2. FoliageChunks.bytes 초고속 바이너리 압축 저장 (Version 2: 지면 밀착 개별 풀포기 포맷)
            string bytesPath = Path.Combine(targetDir, "FoliageChunks.bytes");
            using (FileStream fs = new FileStream(bytesPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // Header
                bw.Write("TWFG"); // Magic Signature
                bw.Write((int)2); // Version 2 (Grounded Single-Tuft Format)
                bw.Write(_chunks.Count);
                bw.Write(prototypes.Count);

                foreach (var kvp in _chunks)
                {
                    TacticalGrassChunk chunk = kvp.Value;
                    bw.Write(chunk.chunkCoord.x);
                    bw.Write(chunk.chunkCoord.y);

                    bw.Write(chunk.bounds.center.x);
                    bw.Write(chunk.bounds.center.y);
                    bw.Write(chunk.bounds.center.z);
                    bw.Write(chunk.bounds.size.x);
                    bw.Write(chunk.bounds.size.y);
                    bw.Write(chunk.bounds.size.z);

                    bw.Write(chunk.worldPositions.Count);
                    for (int i = 0; i < chunk.worldPositions.Count; i++)
                    {
                        Vector3 wp = chunk.worldPositions[i];
                        bw.Write(wp.x);
                        bw.Write(wp.y);
                        bw.Write(wp.z);
                        bw.Write((byte)chunk.states[i]);
                    }

                    // 프로토타입별 매트릭스 저장
                    for (int p = 0; p < prototypes.Count; p++)
                    {
                        var batchList = chunk.protoBatches[p];
                        int totalMatrices = chunk.protoBatchCounts[p];
                        bw.Write(totalMatrices);

                        if (batchList != null)
                        {
                            for (int b = 0; b < batchList.Count; b++)
                            {
                                Matrix4x4[] batch = batchList[b];
                                for (int m = 0; m < batch.Length; m++)
                                {
                                    Matrix4x4 mat = batch[m];
                                    for (int elem = 0; elem < 16; elem++)
                                    {
                                        bw.Write(mat[elem]);
                                    }
                                }
                            }
                        }
                    }
                }
            }

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            Debug.Log($"<color=#00FF88><b>[TacticalGrassRenderer] 💾 전장 식생 데이터 파일(v2) 저장 완료!\n경로: {targetDir}\n총 {_chunks.Count}개 청크, {_totalInstanceCount:N0}개 풀포기</b></color>");
        }

        /// <summary>
        /// 📂 [디스크에서 로드]: Assets/Settings/Vegetation/Data/[MapName]/ 하위 바이너리 파일에서 0.01초 만에 복원
        /// </summary>
        [ContextMenu("📂 전장 식생 데이터 디스크에서 로드 (Load)")]
        public bool LoadFoliageDataFromDisk()
        {
            string targetDir = GetTargetDataDirectory();
            string bytesPath = Path.Combine(targetDir, "FoliageChunks.bytes");

            if (!File.Exists(bytesPath))
            {
                return false;
            }

            EnsureDefaultPrototypes();
            EnsureAllPrototypesGrounded();

            try
            {
                using (FileStream fs = new FileStream(bytesPath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    string magic = br.ReadString();
                    if (magic != "TWFG")
                    {
                        Debug.LogWarning("[TacticalGrassRenderer] 식생 데이터 바이너리 시그니처가 일치하지 않습니다.");
                        return false;
                    }

                    int version = br.ReadInt32();
                    if (version != 2)
                    {
                        // 구버전(v1: 8m 광역 평판 캐시)인 경우 false를 반환하여 자동으로 v2(지면 밀착 개별 풀포기)로 재베이크 유도
                        return false;
                    }

                    int chunkCount = br.ReadInt32();
                    int savedProtoCount = br.ReadInt32();

                    _chunks.Clear();
                    _totalInstanceCount = 0;

                    int protoCount = prototypes.Count;

                    for (int c = 0; c < chunkCount; c++)
                    {
                        int cx = br.ReadInt32();
                        int cz = br.ReadInt32();
                        Vector2Int cKey = new Vector2Int(cx, cz);

                        Vector3 bCenter = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        Vector3 bSize = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                        TacticalGrassChunk chunk = new TacticalGrassChunk
                        {
                            chunkCoord = cKey,
                            bounds = new Bounds(bCenter, bSize)
                        };
                        chunk.InitializeBatches(protoCount);

                        int posCount = br.ReadInt32();
                        for (int i = 0; i < posCount; i++)
                        {
                            Vector3 wp = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            FoliageState state = (FoliageState)br.ReadByte();
                            chunk.worldPositions.Add(wp);
                            chunk.states.Add(state);
                            _totalInstanceCount++;
                        }

                        // 프로토타입별 매트릭스 로드
                        for (int p = 0; p < savedProtoCount; p++)
                        {
                            int totalMatrices = br.ReadInt32();
                            List<Matrix4x4> mList = new List<Matrix4x4>(totalMatrices);

                            for (int m = 0; m < totalMatrices; m++)
                            {
                                Matrix4x4 mat = new Matrix4x4();
                                for (int elem = 0; elem < 16; elem++)
                                {
                                    mat[elem] = br.ReadSingle();
                                }
                                mList.Add(mat);
                            }

                            if (p < protoCount)
                            {
                                chunk.protoBatchCounts[p] = totalMatrices;
                                for (int start = 0; start < totalMatrices; start += 1023)
                                {
                                    int batchSize = Mathf.Min(1023, totalMatrices - start);
                                    Matrix4x4[] batch = new Matrix4x4[batchSize];
                                    mList.CopyTo(start, batch, 0, batchSize);
                                    chunk.protoBatches[p].Add(batch);
                                }
                            }
                        }

                        _chunks[cKey] = chunk;
                    }

                    _totalChunkCount = _chunks.Count;
                }

                // 런타임 델타 흔적(불탄 잿더미, 짓밟힘)이 있으면 덮어씌움
                ApplyRuntimeDeltaIfExists(targetDir);

                Debug.Log($"<color=#00FF88><b>[TacticalGrassRenderer] 📂 전장 식생 데이터 파일(v2) 로드 완료! (총 {_totalChunkCount}개 청크, {_totalInstanceCount:N0}개 풀포기)</b></color>");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TacticalGrassRenderer] 파일 로드 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 🦶 [전투 변경분 저장]: 부대가 지나가 짓밟았거나 불탄 흔적을 FoliageRuntimeDelta.json으로 저장
        /// </summary>
        public void SaveRuntimeDelta()
        {
            if (_runtimeDeltas.Count == 0) return;

            string targetDir = GetTargetDataDirectory();
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            FoliageDeltaContainer container = new FoliageDeltaContainer
            {
                mapName = GetEffectiveFolderName(),
                updateTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                records = _runtimeDeltas
            };

            string deltaPath = Path.Combine(targetDir, "FoliageRuntimeDelta.json");
            File.WriteAllText(deltaPath, JsonUtility.ToJson(container, true));
        }

        private void ApplyRuntimeDeltaIfExists(string targetDir)
        {
            string deltaPath = Path.Combine(targetDir, "FoliageRuntimeDelta.json");
            if (!File.Exists(deltaPath)) return;

            try
            {
                string json = File.ReadAllText(deltaPath);
                FoliageDeltaContainer container = JsonUtility.FromJson<FoliageDeltaContainer>(json);
                if (container != null && container.records != null)
                {
                    foreach (var rec in container.records)
                    {
                        Vector2Int cKey = new Vector2Int(rec.chunkX, rec.chunkZ);
                        if (_chunks.TryGetValue(cKey, out var chunk))
                        {
                            if (rec.instanceIndex >= 0 && rec.instanceIndex < chunk.states.Count)
                            {
                                chunk.states[rec.instanceIndex] = rec.state;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TacticalGrassRenderer] 델타 적용 실패: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 🎥 [카메라 줌인 실시간 감지 및 GPU 인스턴싱 렌더링 루틴]
        /// 씬 뷰 카메라와 게임 뷰 카메라 각각의 위치와 줌 거리를 기준으로 1:1 정밀 렌더링합니다.
        /// </summary>
        public void RenderGrassBatchesForCamera(Camera cam)
        {
            if (_chunks.Count == 0 || prototypes.Count == 0 || cam == null) return;

            bool isSceneView = (cam.cameraType == CameraType.SceneView);
            float effectiveMaxDist = isSceneView ? sceneViewRenderDistance : maxRenderDistance;

            Vector3 camPos = cam.transform.position;

            // 1. 카메라 고도 검사: 씬 뷰에서 sceneViewIgnoreHeightLimit가 켜져 있으면 고도 컬링 스킵
            bool checkHeight = !(isSceneView && sceneViewIgnoreHeightLimit);
            if (checkHeight)
            {
                float terrainBaseY = (targetTerrain != null) ? targetTerrain.transform.position.y : 0f;
                if (camPos.y - terrainBaseY > effectiveMaxDist * 1.5f)
                {
                    if (isSceneView || cam == Camera.main)
                    {
                        _renderedInstanceCount = 0;
                        _activeDrawCalls = 0;
                    }
                    return;
                }
            }

            _visibleChunks.Clear();

            // 2. 카메라 시야각(Frustum) 및 구형 버퍼 마진 기반 청크 선별 (32m 사각형 타일 끊김 방지)
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
            float renderRadiusWithMargin = effectiveMaxDist + chunkSize * 0.45f;
            float renderRadiusSq = renderRadiusWithMargin * renderRadiusWithMargin;

            foreach (var chunk in _chunks.Values)
            {
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.bounds)) continue;

                float sqrDist = chunk.bounds.SqrDistance(camPos);
                if (sqrDist <= renderRadiusSq)
                {
                    _visibleChunks.Add(chunk);
                }
            }

            int protoCount = prototypes.Count;
            int rCount = 0;
            int dCalls = 0;

            // 3. 선별된 청크들만 GPU 인스턴싱으로 해당 카메라 렌더 패스에 정확히 드로우!
            for (int p = 0; p < protoCount; p++)
            {
                var proto = prototypes[p];
                if (proto == null || proto.mesh == null || proto.material == null) continue;

                Mesh mesh = proto.mesh;
                Material mat = proto.material;

                if (!mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                }

                for (int c = 0; c < _visibleChunks.Count; c++)
                {
                    var chunk = _visibleChunks[c];
                    var batchList = chunk.protoBatches[p];
                    if (batchList == null || batchList.Count == 0) continue;

                    for (int b = 0; b < batchList.Count; b++)
                    {
                        Matrix4x4[] batch = batchList[b];
                        if (batch != null && batch.Length > 0)
                        {
                            try
                            {
                                // 🎥 [빌트인 + URP 파이프라인 호환 드로우콜]: 빌트인은 null로 모든 카메라 출력, URP는 cam 지정
                                Camera targetCam = (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) ? null : cam;
                                Graphics.DrawMeshInstanced(mesh, 0, mat, batch, batch.Length, null, castShadows, receiveShadows, 0, targetCam);
                                rCount += batch.Length;
                                dCalls++;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[TacticalGrassRenderer] DrawMeshInstanced 예외 방어: {ex.Message}");
                                break;
                            }
                        }
                    }
                }
            }

            if (isSceneView || cam == Camera.main)
            {
                _renderedInstanceCount = rCount;
                _activeDrawCalls = dCalls;
            }
        }

        /// <summary>
        /// 📍 [월드 좌표 -> 청크 좌표 변환기]
        /// </summary>
        public bool WorldToChunkCoord(Vector3 worldPos, out int chunkX, out int chunkZ)
        {
            Vector3 tPos = targetTerrain != null ? targetTerrain.transform.position : Vector3.zero;
            chunkX = Mathf.FloorToInt((worldPos.x - tPos.x) / chunkSize);
            chunkZ = Mathf.FloorToInt((worldPos.z - tPos.z) / chunkSize);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showChunkGizmosInSceneView) return;

            Camera cam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

            // 1. 등록된 청크 오버라이드 목록 하이라이트 표시
            if (chunkOverrides != null && chunkOverrides.Count > 0)
            {
                Vector3 tPos = targetTerrain != null ? targetTerrain.transform.position : Vector3.zero;
                foreach (var ov in chunkOverrides)
                {
                    Vector3 center = new Vector3(
                        tPos.x + (ov.chunkCoord.x + 0.5f) * chunkSize,
                        tPos.y + 10f,
                        tPos.z + (ov.chunkCoord.y + 0.5f) * chunkSize
                    );
                    if (targetTerrain != null)
                    {
                        center.y = targetTerrain.SampleHeight(center) + tPos.y + 4f;
                    }

                    // 바이옴별 색상 구분
                    Color ovColor = Color.yellow;
                    switch (ov.biomeType)
                    {
                        case FoliageBiomeType.PlainsCarpet: ovColor = new Color(0.2f, 1f, 0.4f, 0.9f); break;
                        case FoliageBiomeType.ForestShade: ovColor = new Color(0.1f, 0.6f, 1f, 0.9f); break;
                        case FoliageBiomeType.HighlandDry: ovColor = new Color(1f, 0.7f, 0.2f, 0.9f); break;
                        case FoliageBiomeType.RiverWetland: ovColor = new Color(0.3f, 0.9f, 1f, 0.9f); break;
                        case FoliageBiomeType.BarrenOrClear: ovColor = new Color(1f, 0.3f, 0.3f, 0.9f); break;
                    }

                    Gizmos.color = ovColor;
                    Gizmos.DrawWireCube(center, new Vector3(chunkSize, 8f, chunkSize));

                    GUIStyle labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
                    labelStyle.normal.textColor = ovColor;
                    labelStyle.fontSize = 12;
                    UnityEditor.Handles.Label(center + Vector3.up * 5f, $"🌾 오버라이드 [{ov.chunkCoord.x}, {ov.chunkCoord.y}]\n({ov.biomeType} x{ov.densityMultiplier:F1})", labelStyle);
                }
            }

            // 2. 카메라 주변 100m 이내 일반 청크 그리드 및 번호 라벨 표시
            if (_chunks != null && _chunks.Count > 0 && cam != null)
            {
                Gizmos.color = new Color(0.15f, 0.85f, 0.35f, 0.15f);
                foreach (var chunk in _chunks.Values)
                {
                    float distSq = (chunk.bounds.center - camPos).sqrMagnitude;
                    if (distSq > 140f * 140f) continue;

                    Gizmos.DrawWireCube(chunk.bounds.center, chunk.bounds.size);

                    if (distSq < 70f * 70f)
                    {
                        GUIStyle normalStyle = new GUIStyle(UnityEditor.EditorStyles.miniLabel);
                        normalStyle.normal.textColor = new Color(0.8f, 1f, 0.8f, 0.75f);
                        UnityEditor.Handles.Label(chunk.bounds.center + Vector3.up * 1f, $"[{chunk.chunkCoord.x}, {chunk.chunkCoord.y}] ({chunk.worldPositions.Count}개)", normalStyle);
                    }
                }
            }
        }
#endif

        #region 게임플레이 상호작용 API (이동속도 저항, 짓밟힘, 화재)

        /// <summary>
        /// 🚶‍♂️ 특정 월드 좌표에서의 최종 식생 지형 이동 저항 계수 반환 (0.50 ~ 1.00배)
        /// (도로 위 = 1.00, 잿더미 = 1.00, 짓밟힘 = 0.92, 화재 = 0.50, 건강한 수풀 = chunkSpeedMultiplier)
        /// </summary>
        public float GetFoliageResistance(Vector3 worldPos)
        {
            // 1. 도로 위 최우선 판정 (도로는 장애물이 치워진 표준 속도 1.0배 보장)
            if (TotalWarRoadSpline.IsOnAnyRoad(worldPos)) return 1.0f;

            // 2. 수계/여울목 감속
            if (TotalWarRiverSpline.IsAnyRiverAt(worldPos, out _)) return 0.50f;
            if (TacticalWaterZone.IsAnyWaterZoneAt(worldPos, out var zone)) return zone.infantrySpeedMultiplier;

            // 3. 32m 식생 청크 O(1) 조회
            int cx = Mathf.FloorToInt((worldPos.x - (targetTerrain != null ? targetTerrain.transform.position.x : 0f)) / chunkSize);
            int cz = Mathf.FloorToInt((worldPos.z - (targetTerrain != null ? targetTerrain.transform.position.z : 0f)) / chunkSize);

            if (_chunks.TryGetValue(new Vector2Int(cx, cz), out var chunk))
            {
                // 화재 발생 시: 패닉 회피로 감속 (0.50배)
                if (chunk.chunkState == FoliageState.Burning) return 0.50f;

                // 전소 잿더미 시: 장애물이 영구 소멸하여 평탄 도로화 (1.00배 정상 속도 회복!)
                if (chunk.chunkState == FoliageState.AshDecal) return 1.00f;

                // 짓밟힘 구역: 선두가 풀을 눕혀놓았으므로 후속 부대 속도 복구 (0.92배)
                if (chunk.chunkState == FoliageState.Trampled) return 0.92f;

                // 정상 식생: 청크에 심겨진 식생 종류와 밀도에 따른 고유 이동속도 (0.68 ~ 0.96배)
                return chunk.chunkSpeedMultiplier;
            }

            return 1.0f;
        }

        /// <summary>
        /// 🌿 특정 월드 좌표가 부대를 숨겨줄 수 있는 '완전 은신처(Deep Stealth, 은폐력 0.55 이상)'인지 여부 판정
        /// </summary>
        public bool IsPositionInDeepStealth(Vector3 worldPos)
        {
            if (TotalWarRoadSpline.IsOnAnyRoad(worldPos)) return false;

            int cx = Mathf.FloorToInt((worldPos.x - (targetTerrain != null ? targetTerrain.transform.position.x : 0f)) / chunkSize);
            int cz = Mathf.FloorToInt((worldPos.z - (targetTerrain != null ? targetTerrain.transform.position.z : 0f)) / chunkSize);

            if (_chunks.TryGetValue(new Vector2Int(cx, cz), out var chunk))
            {
                if (chunk.chunkState == FoliageState.Burning || chunk.chunkState == FoliageState.AshDecal) return false;
                return chunk.chunkStealthPower >= 0.55f;
            }

            return false;
        }

        /// <summary>
        /// 🌿 특정 월드 좌표의 실효 은폐 지수 반환 (0.0: 완전 노출 ~ 1.0: 최상급 매복)
        /// </summary>
        public float GetFoliageStealthPower(Vector3 worldPos)
        {
            if (TotalWarRoadSpline.IsOnAnyRoad(worldPos)) return 0.0f;

            int cx = Mathf.FloorToInt((worldPos.x - (targetTerrain != null ? targetTerrain.transform.position.x : 0f)) / chunkSize);
            int cz = Mathf.FloorToInt((worldPos.z - (targetTerrain != null ? targetTerrain.transform.position.z : 0f)) / chunkSize);

            if (_chunks.TryGetValue(new Vector2Int(cx, cz), out var chunk))
            {
                if (chunk.chunkState == FoliageState.Burning || chunk.chunkState == FoliageState.AshDecal) return 0.0f;
                return chunk.chunkStealthPower;
            }

            return 0.0f;
        }

        // 🔥 [런타임 연소 탄화 머티리얼 및 파티클 머티리얼 캐시]
        private static readonly Dictionary<int, Material> _burningFoliageMatCache = new Dictionary<int, Material>();
        private static Material _cachedGrassFlameMat;
        private static Material _cachedGrassSmokeMat;
        private static Material _cachedGrassEmberMat;
        private static Material _cachedAshDecalMat;
        private static Texture2D _fallbackFireTex;
        private static Texture2D _fallbackAshTex;

        /// <summary>
        /// 🌿 개별 풀포기 3D 메쉬가 실시간으로 주황색 열기 -> 검은 숯 -> 재로 타들어가도록
        /// Universal Render Pipeline/TotalWarFoliageWind 셰이더 기반 연소 머티리얼을 생성/반환합니다.
        /// </summary>
        private static Material GetOrCreateBurningFoliageMaterial(Material sourceMat)
        {
            int key = sourceMat != null ? sourceMat.GetInstanceID() : 0;
            if (_burningFoliageMatCache.TryGetValue(key, out Material cachedMat) && cachedMat != null)
            {
                return cachedMat;
            }

            Shader burnShader = Shader.Find("Universal Render Pipeline/TotalWarFoliageWind");
            if (burnShader == null && sourceMat != null) burnShader = sourceMat.shader;
            if (burnShader == null) burnShader = Shader.Find("Universal Render Pipeline/Lit");

            Material burnMat = new Material(burnShader)
            {
                name = (sourceMat != null ? sourceMat.name : "Grass") + "_Burning3DMat"
            };

            Texture mainTex = null;
            if (sourceMat != null)
            {
                if (sourceMat.HasProperty("_MainTex")) mainTex = sourceMat.GetTexture("_MainTex");
                if (mainTex == null && sourceMat.HasProperty("_BaseMap")) mainTex = sourceMat.GetTexture("_BaseMap");
            }

            if (mainTex != null)
            {
                if (burnMat.HasProperty("_BaseMap")) burnMat.SetTexture("_BaseMap", mainTex);
                if (burnMat.HasProperty("_MainTex")) burnMat.SetTexture("_MainTex", mainTex);
            }

#if UNITY_EDITOR
            Material charredWoodMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TotalWar_CharredWood.mat");
            if (charredWoodMat != null && charredWoodMat.mainTexture != null && burnMat.HasProperty("_CharredMap"))
            {
                burnMat.SetTexture("_CharredMap", charredWoodMat.mainTexture);
            }
#endif

            if (burnMat.HasProperty("_BaseColor")) burnMat.SetColor("_BaseColor", new Color(0.85f, 0.78f, 0.65f, 1.0f));
            if (burnMat.HasProperty("_Cutoff")) burnMat.SetFloat("_Cutoff", 0.22f);
            if (burnMat.HasProperty("_IsShrubGrass")) burnMat.SetFloat("_IsShrubGrass", 1.0f);
            if (burnMat.HasProperty("_EdgeGlowColor")) burnMat.SetColor("_EdgeGlowColor", new Color(4.5f, 1.35f, 0.15f, 1.0f));
            if (burnMat.HasProperty("_BurnProgress")) burnMat.SetFloat("_BurnProgress", 0.06f);

            _burningFoliageMatCache[key] = burnMat;
            return burnMat;
        }

        private static Texture2D GetFallbackRadialParticleTexture()
        {
            if (_fallbackFireTex != null) return _fallbackFireTex;
            int size = 64;
            _fallbackFireTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x / (float)(size - 1)) * 2f - 1f;
                    float dy = (y / (float)(size - 1)) * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            _fallbackFireTex.SetPixels(cols);
            _fallbackFireTex.Apply();
            return _fallbackFireTex;
        }

        /// <summary>
        /// 🔥 풀포기 3D 메쉬 표면에서 직접 뿜어져 나오는 삼탈워 8x8 플립북 화염, 연기, 불똥 파티클 및 조명을 구성합니다.
        /// (깨진 프리팹 머티리얼 GUID에 의존하지 않고 100% 실제 텍스처와 URP 파티클 셰이더를 동적 바인딩하여 가시성 보장)
        /// </summary>
        /// <summary>
        /// 🔥 [삼국지 토탈워 본편 공식 8x8 플립북 화재 파이프라인]
        /// 나무(TacticalForestZone)의 명품 화재 시스템과 100% 동일한 텍스처, 셰이더 및 24 FPS 플립북 알고리즘을 개별 풀포기에 이식합니다.
        /// 풀포기 3D 메쉬 표면 삼각형(Triangle)에서 직접 24 FPS로 춤추는 삼탈워 화염을 뿜어냅니다!
        /// </summary>
        public static void ConfigureGrassTuftFireVFX(GameObject fireRoot, MeshRenderer burningMesh, float plantWidth, float plantHeight, out Light fireLight)
        {
            fireLight = null;
            float scaleRef = Mathf.Clamp(Mathf.Max(plantWidth * 0.75f, plantHeight * 0.90f), 0.75f, 1.60f);

            // 1. 삼국지 토탈워 원본 VFX 프리셋 머티리얼 및 텍스처 로드 (나무 화재와 100% 동일 에셋 바인딩)
            if (_cachedGrassFlameMat == null || _cachedGrassSmokeMat == null || _cachedGrassEmberMat == null)
            {
                Material presetFlameMat = null;
                Material presetSmokeMat = null;
                Material presetEmberMat = null;

                Texture2D flameTex = null;
                Texture2D smokeTex = null;
                Texture2D emberTex = null;

#if UNITY_EDITOR
                string vfxMatPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/";
                presetFlameMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_CompositeFlames.mat");
                presetSmokeMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_WispySmoke.mat");
                presetEmberMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_Embers.mat");

                string vfxTexPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/";
                flameTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "3k_anim_fire_a_diffuse.png")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_flame_particle.png");
                smokeTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "3k_anim_smoke_a_diffuse.png")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_smoke_particle.png");
                emberTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(vfxTexPath + "totalwar_ember_particle.png");
#endif
                if (presetFlameMat == null) presetFlameMat = Resources.Load<Material>("VFX/M_TotalWar_CompositeFlames");
                if (presetSmokeMat == null) presetSmokeMat = Resources.Load<Material>("VFX/M_TotalWar_WispySmoke");
                if (presetEmberMat == null) presetEmberMat = Resources.Load<Material>("VFX/M_TotalWar_Embers");

                if (flameTex == null) flameTex = Resources.Load<Texture2D>("VFX/Textures/3k_anim_fire_a_diffuse");
                if (smokeTex == null) smokeTex = Resources.Load<Texture2D>("VFX/Textures/3k_anim_smoke_a_diffuse");
                if (emberTex == null) emberTex = Resources.Load<Texture2D>("VFX/Textures/totalwar_ember_particle");

                Texture2D fallbackTex = GetFallbackRadialParticleTexture();

                Shader particleUnlit = Shader.Find("TotalWar/VFX/CompositeFlames")
                                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                    ?? Shader.Find("Particles/Standard Unlit")
                                    ?? Shader.Find("Sprites/Default");

                if (presetFlameMat != null)
                {
                    _cachedGrassFlameMat = new Material(presetFlameMat) { name = "Runtime_GrassFlame_Mat" };
                }
                else
                {
                    _cachedGrassFlameMat = new Material(particleUnlit) { name = "Runtime_GrassFlame_Mat" };
                }
                SetupParticleMaterialBlend(_cachedGrassFlameMat, flameTex != null ? flameTex : fallbackTex, true, new Color(2.0f, 1.35f, 0.45f, 1.0f));

                if (presetSmokeMat != null)
                {
                    _cachedGrassSmokeMat = new Material(presetSmokeMat) { name = "Runtime_GrassSmoke_Mat" };
                }
                else
                {
                    _cachedGrassSmokeMat = new Material(particleUnlit) { name = "Runtime_GrassSmoke_Mat" };
                }
                SetupParticleMaterialBlend(_cachedGrassSmokeMat, smokeTex != null ? smokeTex : fallbackTex, false, new Color(0.48f, 0.44f, 0.40f, 1.0f));

                if (presetEmberMat != null)
                {
                    _cachedGrassEmberMat = new Material(presetEmberMat) { name = "Runtime_GrassEmber_Mat" };
                }
                else
                {
                    _cachedGrassEmberMat = new Material(particleUnlit) { name = "Runtime_GrassEmber_Mat" };
                }
                SetupParticleMaterialBlend(_cachedGrassEmberMat, emberTex != null ? emberTex : fallbackTex, true, new Color(3.5f, 1.8f, 0.5f, 1.0f));
            }

            bool hasFireFlipbook = _cachedGrassFlameMat.mainTexture != null && _cachedGrassFlameMat.mainTexture.width >= 256;
            bool hasSmokeFlipbook = _cachedGrassSmokeMat.mainTexture != null && _cachedGrassSmokeMat.mainTexture.width >= 256;

            Vector3 windVec = Vector3.forward * 1.2f;
            if (TotalWarWindManager.Instance != null)
            {
                windVec = TotalWarWindManager.Instance.GetWindDirectionVector() * 1.4f;
            }

            Transform parentTransform = (burningMesh != null) ? burningMesh.transform : fireRoot.transform;

            // 🔥 [Layer 1: 나무와 100% 동일한 메쉬 표면 8x8 플립북 24 FPS 화염 (CompositeFlames)]
            GameObject flameGo = new GameObject("CompositeFlames_TotalWar");
            flameGo.transform.SetParent(parentTransform, false);
            flameGo.transform.localPosition = Vector3.up * 0.05f;
            flameGo.transform.localRotation = Quaternion.identity;
            flameGo.transform.localScale = Vector3.one;

            var flamePs = flameGo.AddComponent<ParticleSystem>();
            flamePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var flameRen = flameGo.GetComponent<ParticleSystemRenderer>();
            flameRen.sharedMaterial = _cachedGrassFlameMat;
            flameRen.renderMode = ParticleSystemRenderMode.Billboard;
            flameRen.alignment = ParticleSystemRenderSpace.View;
            flameRen.sortMode = ParticleSystemSortMode.Distance;
            flameRen.sortingFudge = 2.0f;
            flameRen.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.UV2,
                ParticleSystemVertexStream.AnimBlend
            });

            var fm = flamePs.main;
            fm.loop = true;
            fm.playOnAwake = false;
            fm.simulationSpace = ParticleSystemSimulationSpace.World;
            fm.startLifetime = 2.67f; // 64프레임 / 24 FPS = 정확히 2.67초 1사이클
            fm.startSpeed = 0f;       // 나무와 동일하게 속도 0으로 풀잎 표면에 달라붙음!
            fm.startSize = new ParticleSystem.MinMaxCurve(0.35f * scaleRef, 0.68f * scaleRef);
            fm.startRotation = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            fm.flipRotation = 0.5f;
            fm.gravityModifier = 0f;
            fm.startColor = Color.white;
            fm.maxParticles = 75;

            var fe = flamePs.emission;
            fe.rateOverTime = 26f * scaleRef;

            var fShape = flamePs.shape;
            fShape.enabled = true;
            fShape.normalOffset = 0.08f;
            if (burningMesh != null)
            {
                fShape.shapeType = ParticleSystemShapeType.MeshRenderer;
                fShape.meshRenderer = burningMesh;
                fShape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                fShape.useMeshColors = false;
            }
            else
            {
                fShape.shapeType = ParticleSystemShapeType.Hemisphere;
                fShape.radius = Mathf.Max(0.35f, plantWidth * 0.4f);
            }

            if (hasFireFlipbook)
            {
                var fTsa = flamePs.textureSheetAnimation;
                fTsa.enabled = true;
                fTsa.numTilesX = 8;
                fTsa.numTilesY = 8;
                fTsa.animation = ParticleSystemAnimationType.WholeSheet;
                fTsa.timeMode = ParticleSystemAnimationTimeMode.FPS;
                fTsa.fps = 24.0f; // 나무와 동일한 초당 24 FPS 춤추는 애니메이션!
                AnimationCurve frameCurve = new AnimationCurve();
                frameCurve.AddKey(0f, 0f);
                frameCurve.AddKey(1f, 1f);
                fTsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);
                fTsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.15f);
                fTsa.cycleCount = 1;
            }

            var fCol = flamePs.colorOverLifetime;
            fCol.enabled = true;
            Gradient flameGrad = new Gradient();
            flameGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(Color.white, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.08f),
                    new GradientAlphaKey(1.0f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            fCol.color = new ParticleSystem.MinMaxGradient(flameGrad);

            // 💨 [Layer 2: 토탈워 원본 8x8 플립북 훈연 연기 (VolumetricSmoke)]
            GameObject smokeGo = new GameObject("VolumetricSmoke_TotalWar");
            smokeGo.transform.SetParent(parentTransform, false);
            smokeGo.transform.localPosition = Vector3.up * (0.22f * scaleRef);
            smokeGo.transform.localRotation = Quaternion.identity;
            smokeGo.transform.localScale = Vector3.one;

            var smokePs = smokeGo.AddComponent<ParticleSystem>();
            smokePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var smokeRen = smokeGo.GetComponent<ParticleSystemRenderer>();
            smokeRen.sharedMaterial = _cachedGrassSmokeMat;
            smokeRen.renderMode = ParticleSystemRenderMode.Billboard;
            smokeRen.alignment = ParticleSystemRenderSpace.View;
            smokeRen.sortMode = ParticleSystemSortMode.Distance;
            smokeRen.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.UV2,
                ParticleSystemVertexStream.AnimBlend
            });

            var sm = smokePs.main;
            sm.loop = true;
            sm.playOnAwake = false;
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            sm.startLifetime = 2.67f;
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.55f * scaleRef, 1.15f * scaleRef);
            sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            sm.gravityModifier = -0.15f;
            sm.maxParticles = 35;

            var sSeasonParams = (_instance != null) ? _instance.GetSeasonalFireParameters()
                : (coreCol: new Color(0.35f, 0.32f, 0.28f, 0.52f), edgeCol: new Color(0.48f, 0.44f, 0.40f, 0f), opacity: 1f, burnProg: 0.52f, stumpHeightScale: 0.42f, spreadProbBonus: 0f, smokeStartCol: new Color(0.88f, 0.88f, 0.85f, 1f), smokeEndCol: new Color(0.76f, 0.76f, 0.74f, 1f), smokeAlpha: 0.75f, smokeRateMult: 1.45f);

            var se = smokePs.emission;
            se.rateOverTime = (8f * scaleRef) * sSeasonParams.smokeRateMult;

            var sShape = smokePs.shape;
            sShape.enabled = true;
            sShape.shapeType = ParticleSystemShapeType.Cone;
            sShape.angle = 18f;
            sShape.radius = Mathf.Max(0.25f, plantWidth * 0.3f);

            var sVel = smokePs.velocityOverLifetime;
            sVel.enabled = true;
            sVel.space = ParticleSystemSimulationSpace.World;
            sVel.x = new ParticleSystem.MinMaxCurve(windVec.x * 0.5f, windVec.x * 1.0f);
            sVel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            sVel.z = new ParticleSystem.MinMaxCurve(windVec.z * 0.5f, windVec.z * 1.0f);

            if (hasSmokeFlipbook)
            {
                var sTsa = smokePs.textureSheetAnimation;
                sTsa.enabled = true;
                sTsa.numTilesX = 8;
                sTsa.numTilesY = 8;
                sTsa.animation = ParticleSystemAnimationType.WholeSheet;
                sTsa.timeMode = ParticleSystemAnimationTimeMode.FPS;
                sTsa.fps = 24.0f;
                AnimationCurve sCurve = new AnimationCurve();
                sCurve.AddKey(0f, 0f);
                sCurve.AddKey(1f, 1f);
                sTsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, sCurve);
                sTsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.2f);
                sTsa.cycleCount = 1;
            }

            var sCol = smokePs.colorOverLifetime;
            sCol.enabled = true;
            Gradient smokeGrad = new Gradient();
            smokeGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(sSeasonParams.smokeStartCol, 0.0f),
                    new GradientColorKey(sSeasonParams.smokeEndCol, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(sSeasonParams.smokeAlpha, 0.20f),
                    new GradientAlphaKey(sSeasonParams.smokeAlpha * 0.65f, 0.70f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            sCol.color = new ParticleSystem.MinMaxGradient(smokeGrad);

            // ✨ [Layer 3: 하늘로 비산하는 토탈워 정품 불티 (Rising_Embers_TotalWar)]
            GameObject emberGo = new GameObject("Rising_Embers_TotalWar");
            emberGo.transform.SetParent(fireRoot.transform, false);
            emberGo.transform.localPosition = Vector3.up * 0.15f;

            var emberPs = emberGo.AddComponent<ParticleSystem>();
            emberPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emberRen = emberGo.GetComponent<ParticleSystemRenderer>();
            emberRen.sharedMaterial = _cachedGrassEmberMat;
            emberRen.renderMode = ParticleSystemRenderMode.Billboard;
            emberRen.alignment = ParticleSystemRenderSpace.View;
            emberRen.sortMode = ParticleSystemSortMode.Distance;

            var em = emberPs.main;
            em.loop = true;
            em.playOnAwake = false;
            em.simulationSpace = ParticleSystemSimulationSpace.World;
            em.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            em.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            em.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            em.startColor = Color.white;
            em.gravityModifier = -0.15f;
            em.maxParticles = 30;

            var ee = emberPs.emission;
            ee.rateOverTime = 10f;

            var eShape = emberPs.shape;
            eShape.enabled = true;
            eShape.shapeType = ParticleSystemShapeType.Cone;
            eShape.angle = 24f;
            eShape.radius = Mathf.Max(0.2f, plantWidth * 0.25f);

            var eNoise = emberPs.noise;
            eNoise.enabled = true;
            eNoise.strength = 0.8f;
            eNoise.frequency = 1.5f;

            // 💡 [Layer 4: 화재 포인트 라이트 (따스한 2200K 화염 광원)]
            GameObject lightGo = new GameObject("Fire_PointLight");
            lightGo.transform.SetParent(fireRoot.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.45f;
            fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1.0f, 0.55f, 0.15f);
            fireLight.range = Mathf.Clamp(scaleRef * 4.2f, 3.8f, 7.0f);
            fireLight.intensity = 2.8f;
            fireLight.shadows = LightShadows.None;

            flamePs.Play(true);
            smokePs.Play(true);
            emberPs.Play(true);
        }

        private static void SetupParticleMaterialBlend(Material mat, Texture2D tex, bool additive, Color? tintColor = null)
        {
            if (mat == null) return;
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

            Color col = tintColor.HasValue ? tintColor.Value : Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);

            mat.SetFloat("_Surface", 1.0f); // Transparent
            mat.SetFloat("_Blend", additive ? 1.0f : 0.0f); // 1 = Additive, 0 = Alpha
            mat.SetInt("_SrcBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + (additive ? 100 : 50);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive)
            {
                mat.EnableKeyword("_SOFTPARTICLES_ON");
                mat.SetFloat("_SoftParticlesEnabled", 1.0f);
                mat.EnableKeyword("_BLENDMODE_ADD");
                mat.DisableKeyword("_BLENDMODE_ALPHA");
                mat.EnableKeyword("_FLIPBOOKBLENDING_ON");
                mat.SetFloat("_FlipbookBlending", 1.0f);
                mat.SetFloat("_FlipbookMode", 1.0f);
            }
            else
            {
                mat.DisableKeyword("_SOFTPARTICLES_ON");
                mat.SetFloat("_SoftParticlesEnabled", 0.0f);
                mat.EnableKeyword("_BLENDMODE_ALPHA");
                mat.DisableKeyword("_BLENDMODE_ADD");
                mat.EnableKeyword("_FLIPBOOKBLENDING_ON");
                mat.SetFloat("_FlipbookBlending", 1.0f);
                mat.SetFloat("_FlipbookMode", 1.0f);
            }
        }

        /// <summary>
        /// 🪵 풀포기가 불탄 자리에 지형의 높낮이 곡면을 100% 따라 덮이는 검은 잿더미/그을음(Ash & Scorch Decal) 메쉬를 생성합니다.
        /// 16각 링 3중 동심원 구조(49개 정점)와 삼탈워 정품 잿더미 텍스처, 월드 기반 UV 매핑을 통해 원형 도장 자국 없이 지면에 부드럽게 녹아듭니다.
        /// </summary>
        public static void SpawnTerrainConformingAshMark(Vector3 centerPos, float radius, Transform parent)
        {
            if (parent == null) return;
            if (parent.Find("Ash_Scorch_Decal") != null) return;

            GameObject ashGo = new GameObject("Ash_Scorch_Decal");
            ashGo.transform.SetParent(parent, false);
            ashGo.transform.position = centerPos;
            ashGo.transform.rotation = Quaternion.identity;
            ashGo.transform.localScale = Vector3.one;

            MeshFilter mf = ashGo.AddComponent<MeshFilter>();
            MeshRenderer mr = ashGo.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            // 16각 링 3중 동심원 (중심 1 + 내측 16 + 중간 16 + 외측 16 = 총 49개 정점)
            // 지형 높낮이를 각 정점마다 1:1로 샘플링하여 비탈길, 둔덕에서도 절대 허공에 뜨지 않고 지면에 100% 밀착!
            const int ringSegments = 16;
            int vertCount = 1 + ringSegments * 3;
            Vector3[] verts = new Vector3[vertCount];
            Color[] colors = new Color[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            float centerGroundY = TerrainHeightManager.SampleHeightFast(centerPos.x, centerPos.z);
            verts[0] = new Vector3(0f, (centerGroundY - centerPos.y) + 0.026f, 0f);

            // 🎨 [사계절 물리 연동 잿더미 색상 (여름: 백회색 재, 겨울: 칠흑 숯, 가을: 갈탄, 봄: 회갈색)]:
            var sFireParams = (_instance != null) ? _instance.GetSeasonalFireParameters()
                : (coreCol: new Color(0.35f, 0.32f, 0.28f, 0.52f), edgeCol: new Color(0.48f, 0.44f, 0.40f, 0f), opacity: 1f, burnProg: 0.52f, stumpHeightScale: 0.42f, spreadProbBonus: 0f, smokeStartCol: new Color(0.88f, 0.88f, 0.85f, 1f), smokeEndCol: new Color(0.76f, 0.76f, 0.74f, 1f), smokeAlpha: 0.75f, smokeRateMult: 1.45f);

            Color coreCol = sFireParams.coreCol;
            Color edgeCol = sFireParams.edgeCol;
            float opacityMul = Mathf.Clamp01(sFireParams.opacity);

            Color innerCol = Color.Lerp(coreCol, edgeCol, 0.32f);
            innerCol.a = coreCol.a * 0.72f * opacityMul;

            Color midCol = Color.Lerp(coreCol, edgeCol, 0.65f);
            midCol.a = coreCol.a * 0.35f * opacityMul;

            coreCol.a *= opacityMul;
            edgeCol.a = 0.0f; // 테두리는 항상 0으로 완전 페이드아웃

            colors[0] = coreCol;
            uvs[0] = new Vector2(centerPos.x * 0.45f, centerPos.z * 0.45f);

            for (int i = 0; i < ringSegments; i++)
            {
                float angle = (i / (float)ringSegments) * Mathf.PI * 2f;
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);

                // 유기적 펄린 노이즈 섭동 (각도별 불규칙 가장자리 형성 -> 원형 스탬프 느낌 완전 박멸!)
                float noiseVal = Mathf.PerlinNoise((centerPos.x + cosA * 2.2f) * 0.4f, (centerPos.z + sinA * 2.2f) * 0.4f);
                float rMod = Mathf.Lerp(0.80f, 1.25f, noiseVal);

                // 1. 내측 링 (반경 35%): 은은한 탄화 그을음 영역
                float rInner = radius * 0.35f * Mathf.Lerp(0.9f, 1.1f, noiseVal);
                float ix = cosA * rInner;
                float iz = sinA * rInner;
                float iy = TerrainHeightManager.SampleHeightFast(centerPos.x + ix, centerPos.z + iz);
                int innerIdx = 1 + i;
                verts[innerIdx] = new Vector3(ix, (iy - centerPos.y) + 0.025f, iz);
                colors[innerIdx] = innerCol;
                uvs[innerIdx] = new Vector2((centerPos.x + ix) * 0.45f, (centerPos.z + iz) * 0.45f);

                // 2. 중간 링 (반경 70%): 부드러운 잿가루 영역
                float rMid = radius * 0.70f * rMod;
                float mx = cosA * rMid;
                float mz = sinA * rMid;
                float my = TerrainHeightManager.SampleHeightFast(centerPos.x + mx, centerPos.z + mz);
                int midIdx = 1 + ringSegments + i;
                verts[midIdx] = new Vector3(mx, (my - centerPos.y) + 0.023f, mz);
                colors[midIdx] = midCol;
                uvs[midIdx] = new Vector2((centerPos.x + mx) * 0.45f, (centerPos.z + mz) * 0.45f);

                // 3. 외측 링 (반경 105%): 부드러운 투명 페더링 (알파 0.0 소멸로 지형과 무결점 블렌딩)
                float rOuter = radius * 1.05f * rMod;
                float ox = cosA * rOuter;
                float oz = sinA * rOuter;
                float oy = TerrainHeightManager.SampleHeightFast(centerPos.x + ox, centerPos.z + oz);
                int outerIdx = 1 + ringSegments * 2 + i;
                verts[outerIdx] = new Vector3(ox, (oy - centerPos.y) + 0.020f, oz);
                colors[outerIdx] = edgeCol;
                uvs[outerIdx] = new Vector2((centerPos.x + ox) * 0.45f, (centerPos.z + oz) * 0.45f);
            }

            // 삼각형 인덱스 빌드 (중심 부채꼴 16개 + 내외측 쿼드 32개 = 총 80개 삼각형)
            List<int> tris = new List<int>(ringSegments * 15);
            for (int i = 0; i < ringSegments; i++)
            {
                int next = (i + 1) % ringSegments;
                int inCur = 1 + i;
                int inNext = 1 + next;
                int midCur = 1 + ringSegments + i;
                int midNext = 1 + ringSegments + next;
                int outCur = 1 + ringSegments * 2 + i;
                int outNext = 1 + ringSegments * 2 + next;

                // 중심 부채꼴 삼각형 (중심 -> 내측)
                tris.Add(0);
                tris.Add(inNext);
                tris.Add(inCur);

                // 내측 -> 중간 쿼드 (2개 삼각형)
                tris.Add(inCur);
                tris.Add(inNext);
                tris.Add(midNext);

                tris.Add(inCur);
                tris.Add(midNext);
                tris.Add(midCur);

                // 중간 -> 외측 쿼드 (2개 삼각형)
                tris.Add(midCur);
                tris.Add(midNext);
                tris.Add(outNext);

                tris.Add(midCur);
                tris.Add(outNext);
                tris.Add(outCur);
            }

            Mesh ashMesh = new Mesh
            {
                name = "TerrainConforming_AshMark_Mesh",
                vertices = verts,
                triangles = tris.ToArray(),
                colors = colors,
                uv = uvs
            };
            ashMesh.RecalculateNormals();
            ashMesh.RecalculateBounds();
            mf.sharedMesh = ashMesh;

            // 잿더미 머티리얼 바인딩 (삼탈워 정품 잿더미 텍스처 및 부드러운 알파 블렌딩)
            if (_cachedAshDecalMat == null)
            {
                Shader decalShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                  ?? Shader.Find("Sprites/Default")
                                  ?? Shader.Find("Particles/Standard Unlit");

                Texture2D ashTex = null;
#if UNITY_EDITOR
                ashTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/3k_cityprop_mines_wood_ashes_base_colour.png");
#endif
                if (ashTex == null) ashTex = Resources.Load<Texture2D>("VFX/Textures/3k_cityprop_mines_wood_ashes_base_colour");
                if (ashTex == null) ashTex = GetFallbackAshTexture();

                _cachedAshDecalMat = new Material(decalShader) { name = "Runtime_AshScorchDecal_Mat" };
                SetupParticleMaterialBlend(_cachedAshDecalMat, ashTex, false, new Color(0.92f, 0.90f, 0.88f, 1.0f));
                _cachedAshDecalMat.renderQueue = 2990;
            }

            mr.sharedMaterial = _cachedAshDecalMat;
        }

        private static Texture2D GetFallbackAshTexture()
        {
            if (_fallbackAshTex != null) return _fallbackAshTex;
            _fallbackAshTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] cols = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    float n2 = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.5f;
                    float grain = Mathf.Clamp01(n1 + n2);
                    float cVal = Mathf.Lerp(0.18f, 0.42f, grain);
                    cols[y * 64 + x] = new Color(cVal, cVal * 0.95f, cVal * 0.90f, 1.0f);
                }
            }
            _fallbackAshTex.SetPixels(cols);
            _fallbackAshTex.Apply();
            return _fallbackAshTex;
        }

        /// <summary>
        /// 🎲 특정 발화점에서 목표 풀포기로 불이 옮겨붙을 최종 전이 확률(0.0 ~ 1.0)을 계산합니다.
        /// 식생 고유 인화성(Flammability), 풍향(순풍/역풍), 풍속을 실시간 합성하여 계산합니다.
        /// </summary>
        public float CalculateSpreadProbability(Vector3 fromPos, Vector3 toPos, Vector3 windDir, int targetProtoIdx)
        {
            float flammability = 0.45f;
            if (targetProtoIdx >= 0 && targetProtoIdx < prototypes.Count)
            {
                flammability = prototypes[targetProtoIdx].flammability;
            }

            // 풍향과 전파 벡터의 내적 (dot: +1 = 순풍, -1 = 역풍)
            Vector3 toDir = toPos - fromPos;
            toDir.y = 0f;
            float dist = toDir.magnitude;
            if (dist > 0.001f) toDir /= dist;
            else toDir = windDir;

            float dot = Vector3.Dot(toDir, windDir.normalized);
            float windFactor = 1.0f;
            if (dot > 0.05f)
            {
                // 순풍: 바람을 타고 불씨가 날아가므로 전이 확률 대폭 상승
                windFactor = Mathf.Lerp(1.0f, downwindSpreadBonus, dot);
            }
            else if (dot < -0.05f)
            {
                // 역풍: 바람을 거스르므로 전이 확률 급감 (불길 역주행 억제)
                windFactor = Mathf.Lerp(1.0f, upwindSpreadPenalty, -dot);
            }

            // 풍속(Wind Speed) 가중치 반영
            float windSpeed = TotalWarWindManager.Instance != null ? TotalWarWindManager.Instance.windSpeed : 1.0f;
            float speedMult = Mathf.Clamp(0.70f + windSpeed * 0.25f, 0.60f, 1.45f);

            // 🍂 [사계절 환경 물리 연동]: 여름(가뭄/건조 +15%), 겨울(습기/추위 -18%), 가을(+8%)
            float seasonBonus = 0f;
            if (_instance != null && _instance.enableSeasonalFireSimulation)
            {
                var sParams = _instance.GetSeasonalFireParameters();
                seasonBonus = sParams.spreadProbBonus;
            }

            // 식생 인화성(마른풀 1.0 -> 2배, 수변풀 0.15 -> 0.3배) 및 풍향/풍속 합성
            float finalChance = (baseSpreadChance + seasonBonus) * (flammability / 0.50f) * windFactor * speedMult;
            return Mathf.Clamp(finalChance, 0.05f, 0.92f);
        }

        /// <summary>
        /// 🔥 불타는 풀포기에서 인접한 부시(TacticalBushZone) 및 숲(TacticalForestZone) 나무로 화재를 전이시킵니다.
        /// </summary>
        private void TrySpreadFireToBushAndForest(Vector3 pos, Vector3 windDir)
        {
            Vector3 checkPos = pos + windDir * 1.5f;

            // 1. 관목 부시 구역(TacticalBushZone) 점화 연동 (확률 45% * 순풍 가중치)
            if (TacticalBushZone.ActiveBushZones != null)
            {
                for (int i = 0; i < TacticalBushZone.ActiveBushZones.Count; i++)
                {
                    var bushZone = TacticalBushZone.ActiveBushZones[i];
                    if (bushZone == null || bushZone.fireController == null) continue;

                    float distXZ = Vector2.Distance(new Vector2(checkPos.x, checkPos.z), new Vector2(bushZone.transform.position.x, bushZone.transform.position.z));
                    if (distXZ <= bushZone.radius + 3.5f)
                    {
                        // 순풍 방향일수록 부시 착화 확률 증가
                        Vector3 toBush = (bushZone.transform.position - pos).normalized;
                        float dot = Vector3.Dot(toBush, windDir.normalized);
                        float bushChance = Mathf.Clamp01(0.45f + dot * 0.25f);
                        if (UnityEngine.Random.value <= bushChance)
                        {
                            bushZone.fireController.IgniteAt(checkPos);
                        }
                    }
                }
            }

            // 2. 숲 구역(TacticalForestZone) 수목 점화 연동 (확률 32% * 순풍 가중치)
            if (TacticalForestZone.ActiveZones != null)
            {
                for (int i = 0; i < TacticalForestZone.ActiveZones.Count; i++)
                {
                    var forest = TacticalForestZone.ActiveZones[i];
                    if (forest == null) continue;

                    float distXZ = Vector2.Distance(new Vector2(checkPos.x, checkPos.z), new Vector2(forest.transform.position.x, forest.transform.position.z));
                    if (distXZ <= forest.radius + 4.5f)
                    {
                        Vector3 toForest = (forest.transform.position - pos).normalized;
                        float dot = Vector3.Dot(toForest, windDir.normalized);
                        float treeChance = Mathf.Clamp01(0.32f + dot * 0.22f);
                        if (UnityEngine.Random.value <= treeChance)
                        {
                            forest.IgniteAt(checkPos, 5.5f);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 🔥 특정 월드 지점의 개별 풀포기를 찾아,
        /// 1) 기존 초록색 인스턴싱 행렬을 숨기고,
        /// 2) 실시간 3D 탄화 메쉬(주황 불꽃 테두리 -> 검은 숯 -> 수축)를 스폰하며,
        /// 3) 8x8 플립북 화염/연기/불똥 파티클을 풀포기 표면에 장착합니다.
        /// checkProbability가 true일 경우, 풍향·풍속 및 식생 인화성 확률 롤을 거쳐 전이 여부를 결정합니다.
        /// </summary>
        public bool IgnitePlantAt(Vector3 worldPos, float searchRadius = 2.8f, bool checkProbability = false, Vector3? fromPos = null, Vector3? windDir = null)
        {
            if (_chunks == null || _chunks.Count == 0) return false;

            EnsureAllPrototypesGrounded();

            Vector3 tPos = (targetTerrain != null ? targetTerrain.transform.position : Vector3.zero);
            int cx = Mathf.FloorToInt((worldPos.x - tPos.x) / chunkSize);
            int cz = Mathf.FloorToInt((worldPos.z - tPos.z) / chunkSize);

            float closestDistSq = searchRadius * searchRadius;
            TacticalGrassChunk bestChunk = null;
            int bestProto = -1;
            int bestBatch = -1;
            int bestInst = -1;
            Vector3 bestPos = Vector3.zero;
            Vector3 bestScale = Vector3.one;
            Quaternion bestRot = Quaternion.identity;

            // 인접 9개 청크 내에서 가장 가까운 건강한 개별 풀포기 검색
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    if (_chunks.TryGetValue(new Vector2Int(cx + ox, cz + oz), out var chunk))
                    {
                        for (int p = 0; p < prototypes.Count; p++)
                        {
                            var bList = chunk.protoBatches[p];
                            if (bList == null) continue;

                            for (int batch = 0; batch < bList.Count; batch++)
                            {
                                var mArr = bList[batch];
                                for (int i = 0; i < mArr.Length; i++)
                                {
                                    // 이미 불타서 숨겨진 인스턴스(스케일 0)는 건너뜀
                                    Vector3 curScale = mArr[i].lossyScale;
                                    if (curScale.sqrMagnitude < 0.01f) continue;

                                    bool alreadyBurning = false;
                                    for (int b = 0; b < _burningPlants.Count; b++)
                                    {
                                        if (_burningPlants[b].chunkCoord == chunk.chunkCoord &&
                                            _burningPlants[b].protoIndex == p &&
                                            _burningPlants[b].batchIndex == batch &&
                                            _burningPlants[b].instanceIndex == i)
                                        {
                                            alreadyBurning = true;
                                            break;
                                        }
                                    }
                                    if (alreadyBurning) continue;

                                    Vector3 instPos = mArr[i].GetColumn(3);
                                    float dSq = (instPos.x - worldPos.x) * (instPos.x - worldPos.x) + (instPos.z - worldPos.z) * (instPos.z - worldPos.z);
                                    if (dSq < closestDistSq)
                                    {
                                        closestDistSq = dSq;
                                        bestChunk = chunk;
                                        bestProto = p;
                                        bestBatch = batch;
                                        bestInst = i;
                                        bestPos = instPos;
                                        bestScale = curScale;
                                        bestRot = mArr[i].rotation;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (bestChunk == null || bestProto == -1) return false;

            // 🎲 [토탈워식 확률 기반 화재 전이 주사위 롤]: 연쇄 전파 시 식생 인화성, 풍향, 풍속에 따른 확률 검증
            if (checkProbability && fromPos.HasValue)
            {
                Vector3 wDir = windDir.HasValue ? windDir.Value : Vector3.forward;
                float spreadProb = CalculateSpreadProbability(fromPos.Value, bestPos, wDir, bestProto);
                if (UnityEngine.Random.value > spreadProb)
                {
                    // 전이 실패: 불길이 번지지 않고 해당 풀포기에서 자연 진화(Self-Extinction)
                    return false;
                }
            }

            // 1. 건강한 초록색 GPU 인스턴싱 행렬을 즉시 숨김 (대신 아래에서 3D 실시간 탄화 메쉬가 그 자리를 대체!)
            bestChunk.protoBatches[bestProto][bestBatch][bestInst] = Matrix4x4.TRS(bestPos, bestRot, Vector3.zero);
            bestChunk.chunkState = FoliageState.Burning;

            _runtimeDeltas.Add(new FoliageDeltaRecord
            {
                chunkX = bestChunk.chunkCoord.x,
                chunkZ = bestChunk.chunkCoord.y,
                instanceIndex = bestInst,
                state = FoliageState.AshDecal
            });

            // 2. 해당 풀포기 본래의 3D 메쉬 크기 계산 (정규화된 단일 풀포기이므로 폭 ~1.15m)
            var proto = prototypes[bestProto];
            Vector3 meshSize = (proto.mesh != null) ? proto.mesh.bounds.size : Vector3.one;
            float actualWidth = Mathf.Clamp(Mathf.Max(meshSize.x, meshSize.z) * bestScale.x, 0.75f, 1.65f);
            float actualHeight = Mathf.Clamp(meshSize.y * Mathf.Abs(bestScale.y), 0.45f, 1.50f);

            // 3. 화염 VFX 및 3D 탄화 메쉬 루트 준비
            if (fireVfxRoot == null)
            {
                var existing = transform.Find("Foliage_FireVFX_Root");
                if (existing != null) fireVfxRoot = existing;
                else
                {
                    var rootGo = new GameObject("Foliage_FireVFX_Root");
                    rootGo.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    rootGo.transform.SetParent(transform, false);
                    fireVfxRoot = rootGo.transform;
                }
            }

            // 🌿 [생성 시점 지표면 정위치 보정]:
            // 연소 완료 시 억지로 위치를 튀어오르게(Popping) 하지 않고, 생성 순간부터 지표면 위 정위치에 완벽 안착!
            float groundY = TerrainHeightManager.SampleHeightFast(bestPos.x, bestPos.z);
            Vector3 elevatedPos = new Vector3(bestPos.x, Mathf.Max(bestPos.y, groundY + 0.025f), bestPos.z);

            GameObject fireGo = new GameObject($"Fire_Plant_{proto.protoName}_{bestChunk.chunkCoord.x}_{bestChunk.chunkCoord.y}_{bestInst}");
            fireGo.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            fireGo.transform.SetParent(fireVfxRoot, false);
            fireGo.transform.position = elevatedPos;
            fireGo.transform.rotation = Quaternion.identity;

            // 4. 풀포기 자체가 불타며 시꺼먼 숯으로 변하는 3D 연소 메쉬(Burning_Plant_Mesh) 생성
            GameObject burningMeshGo = new GameObject("Burning_Plant_Mesh");
            burningMeshGo.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            burningMeshGo.transform.SetParent(fireGo.transform, false);
            burningMeshGo.transform.position = elevatedPos;
            burningMeshGo.transform.rotation = bestRot;
            burningMeshGo.transform.localScale = new Vector3(bestScale.x, Mathf.Abs(bestScale.y), bestScale.z);

            MeshFilter bMf = burningMeshGo.AddComponent<MeshFilter>();
            bMf.sharedMesh = proto.mesh;

            MeshRenderer bMr = burningMeshGo.AddComponent<MeshRenderer>();
            bMr.sharedMaterial = GetOrCreateBurningFoliageMaterial(proto.material);
            bMr.shadowCastingMode = castShadows;
            bMr.receiveShadows = receiveShadows;

            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetFloat("_BurnProgress", 0.06f);
            bMr.SetPropertyBlock(propBlock);

            // 5. 풀포기 3D 메쉬 표면에서 타오르는 8x8 플립북 화염/연기/불똥 및 포인트 라이트 장착
            ConfigureGrassTuftFireVFX(fireGo, bMr, actualWidth, actualHeight, out Light pointLight);

            // 6. 🚫 [점박이 원판 데칼 완전 제거]: 바닥에 인위적인 원형 점박이 데칼을 찍지 않고,
            // 연소 완료 시 3D 숯 풀 잔해가 높이 4~7cm로 바닥에 납작하게 가라앉아 자연스러운 화재 흔적을 형성!

            // 7. 실시간 연소 시뮬레이션 리스트 등록
            var burningPlant = new BurningPlantInstance
            {
                chunkCoord = bestChunk.chunkCoord,
                protoIndex = bestProto,
                batchIndex = bestBatch,
                instanceIndex = bestInst,
                worldPos = bestPos,
                originalScale = new Vector3(bestScale.x, Mathf.Abs(bestScale.y), bestScale.z),
                rotation = bestRot,
                plantWidth = actualWidth,
                plantHeight = actualHeight,
                burnTimer = 0f,
                burnDuration = Mathf.Clamp(proto.burnDuration > 1f ? proto.burnDuration : 4.5f, 3.5f, 6.0f),
                fireVfxInstance = fireGo,
                burningMeshRenderer = bMr,
                burningMeshTransform = burningMeshGo.transform,
                burnPropBlock = propBlock,
                fireLight = pointLight,
                hasSpread = false,
                spreadStep = 0
            };

            _burningPlants.Add(burningPlant);

            // 주변 부시/숲 나무가 맞닿아 있으면 즉시 상호 점화 시도
            Vector3 activeWind = windDir.HasValue ? windDir.Value : (TotalWarWindManager.Instance != null ? TotalWarWindManager.Instance.GetWindDirectionVector() : Vector3.forward);
            TrySpreadFireToBushAndForest(bestPos, activeWind);

            Debug.Log($"<color=orange><b>[TacticalGrassRenderer] 🔥 '{proto.protoName}' 개별 풀포기 발화! (폭: {actualWidth:F2}m, 높이: {actualHeight:F2}m)</b></color>");
            return true;
        }

        /// <summary>
        /// 🔥 특정 월드 좌표의 개별 풀포기를 발화시킵니다 (불화살/투석기/화공 공격 연동)
        /// 영역 전체가 한꺼번에 타버리지 않도록 타겟 지점의 개별 풀포기만 점화하여 바람을 타고 순차 번지게 합니다.
        /// </summary>
        public void IgniteAt(Vector3 worldPos, float radius = 3.2f)
        {
            float searchR = Mathf.Clamp(radius, 2.2f, 4.5f);
            IgnitePlantAt(worldPos, searchR);
        }

        /// <summary>
        /// 🔥 [식생 통합 발화 시스템]
        /// 지정 월드 좌표 주변의 모든 식생(풀포기, 관목 부시, 수목)에 즉시 100% 확정으로 불을 붙입니다.
        /// </summary>
        public void IgniteAllVegetationAt(Vector3 targetPos, float radius = 5.0f)
        {
            int grassCount = 0;
            int bushCount = 0;
            int treeCount = 0;

            // 1. 개별 풀포기 (TacticalGrassRenderer) 100% 확정 발화
            if (IgnitePlantAt(targetPos, radius, false))
            {
                grassCount++;
            }

            // 2. 관목 부시 (TacticalBushZone) 즉시 발화
            if (TacticalBushZone.ActiveBushZones != null)
            {
                for (int i = 0; i < TacticalBushZone.ActiveBushZones.Count; i++)
                {
                    var bZone = TacticalBushZone.ActiveBushZones[i];
                    if (bZone == null) continue;

                    float distXZ = Vector2.Distance(new Vector2(targetPos.x, targetPos.z), new Vector2(bZone.transform.position.x, bZone.transform.position.z));
                    if (bZone.IsPositionInside(targetPos) || distXZ <= (bZone.radius + radius + 2.0f))
                    {
                        if (bZone.fireController != null)
                        {
                            bZone.fireController.IgniteAt(targetPos);
                        }
                        else
                        {
                            bZone.Ignite(targetPos);
                        }
                        bushCount++;
                    }
                }
            }

            // 3. 숲 수목 (TacticalForestZone) 즉시 발화
            if (TacticalForestZone.ActiveZones != null)
            {
                for (int i = 0; i < TacticalForestZone.ActiveZones.Count; i++)
                {
                    var fZone = TacticalForestZone.ActiveZones[i];
                    if (fZone == null) continue;

                    if (fZone.IgniteAt(targetPos, radius + 4.5f))
                    {
                        treeCount++;
                    }
                }
            }

            // 4. 만약 풀이 없고 부시/나무도 없는 지점이라도 인근 풀포기 탐색 반경 확장 재시도
            if (grassCount == 0 && bushCount == 0 && treeCount == 0)
            {
                if (IgnitePlantAt(targetPos, radius + 3.0f, false))
                {
                    grassCount++;
                }
            }

            Debug.Log($"<color=orange><b>[TacticalGrassRenderer] 🔥 [식생 통합 발화] 위치({targetPos.x:F1}, {targetPos.z:F1}) 발화 완료! (풀: {grassCount}개, 부시: {bushCount}개, 수목: {treeCount}개)</b></color>");
        }

        /// <summary>
        /// 🔥 현재 활성 카메라(씬 뷰 또는 메인 카메라)가 바라보는 화면 중심 지점의 식생(풀/부시/나무)에 화재를 점화합니다.
        /// </summary>
        public void IgniteAtCameraFocus()
        {
            Camera cam = Camera.main;
#if UNITY_EDITOR
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                cam = SceneView.lastActiveSceneView.camera;
            }
#endif
            if (cam == null)
            {
                Debug.LogWarning("[TacticalGrassRenderer] 활성화된 카메라를 찾을 수 없습니다.");
                return;
            }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 1500f))
            {
                IgniteAllVegetationAt(hit.point, 5.0f);
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPt = ray.GetPoint(enter);
                    hitPt.y = TerrainHeightManager.SampleHeightFast(hitPt.x, hitPt.z);
                    IgniteAllVegetationAt(hitPt, 5.0f);
                }
            }
        }

        /// <summary>
        /// 🧯 전장의 모든 화재를 진압하고 식생을 원래의 건강한 상태로 즉시 복구합니다.
        /// </summary>
        public void ExtinguishAndRestoreAll()
        {
            // 1. 모든 개별 연소 풀포기 화염 파괴
            for (int i = 0; i < _burningPlants.Count; i++)
            {
                if (_burningPlants[i].fireVfxInstance != null)
                {
                    var psArr = _burningPlants[i].fireVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
                    for (int p = 0; p < psArr.Length; p++)
                    {
                        if (psArr[p] != null) psArr[p].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                    if (Application.isPlaying) Destroy(_burningPlants[i].fireVfxInstance);
                    else DestroyImmediate(_burningPlants[i].fireVfxInstance);
                }
            }
            _burningPlants.Clear();

            // 2. 화재 루트 하위 잔여물 정리
            if (fireVfxRoot != null)
            {
                for (int i = fireVfxRoot.childCount - 1; i >= 0; i--)
                {
                    var child = fireVfxRoot.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }

            // 3. 디스크 원본 파일에서 청크 형상 복원
            LoadFoliageDataFromDisk();

            Debug.Log("<color=#00FF88><b>[TacticalGrassRenderer] 🧯 전장의 모든 화재가 완전히 진압되었으며, 식생이 정상 복구되었습니다!</b></color>");
        }

        public void ApplyTrample(Vector3 worldPos, float radius)
        {
            float rSq = radius * radius;
            int cx = Mathf.FloorToInt((worldPos.x - (targetTerrain != null ? targetTerrain.transform.position.x : 0f)) / chunkSize);
            int cz = Mathf.FloorToInt((worldPos.z - (targetTerrain != null ? targetTerrain.transform.position.z : 0f)) / chunkSize);

            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    Vector2Int cKey = new Vector2Int(cx + ox, cz + oz);
                    if (_chunks.TryGetValue(cKey, out var chunk))
                    {
                        for (int p = 0; p < prototypes.Count; p++)
                        {
                            var bList = chunk.protoBatches[p];
                            if (bList == null) continue;

                            for (int b = 0; b < bList.Count; b++)
                            {
                                var batch = bList[b];
                                for (int i = 0; i < batch.Length; i++)
                                {
                                    Vector3 pos = batch[i].GetColumn(3);
                                    float dx = pos.x - worldPos.x;
                                    float dz = pos.z - worldPos.z;
                                    if (dx * dx + dz * dz <= rSq)
                                    {
                                        Vector3 scale = batch[i].lossyScale;
                                        scale.y = Mathf.Min(scale.y, 0.22f);
                                        Quaternion rot = batch[i].rotation;
                                        batch[i] = Matrix4x4.TRS(pos, rot, scale);

                                        // 델타 레코드 추가
                                        _runtimeDeltas.Add(new FoliageDeltaRecord
                                        {
                                            chunkX = cKey.x,
                                            chunkZ = cKey.y,
                                            instanceIndex = i,
                                            state = FoliageState.Trampled
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [ContextMenu("🗑️ 풀 데이터 초기화 (Clear)")]
        public void ClearFoliageData()
        {
            _chunks.Clear();
            _runtimeDeltas.Clear();
            _totalChunkCount = 0;
            _totalInstanceCount = 0;
            _renderedInstanceCount = 0;
            _activeDrawCalls = 0;
        }

        #region 🖌️ 씬 뷰 유기적 식생 브러시 페인터 시스템 (Scene Foliage Brush Tool)

        /// <summary>
        /// 🖌️ 지정 월드 중심점(centerPos)과 반경(radius) 내에 풀을 유기적 포아송-황금각 나선으로 칠합니다.
        /// - spacing: 풀포기간 목표 이격 간격 (조밀 0.6m ~ 성김 2.2m)
        /// - specificProtoIdx: -1이면 현재 바이옴 복합 혼합 살포, 0 이상이면 특정 프로토타입 지정 살포
        /// - falloff: 외곽 감쇄 계수 (0.0: 딱딱한 경계, 1.0: 부드러운 유기적 가장자리)
        /// </summary>
        public int PaintGrassAt(Vector3 centerPos, float radius = 6.0f, float spacing = 0.95f, int specificProtoIdx = -1, float falloff = 0.65f)
        {
            if (prototypes == null || prototypes.Count == 0) return 0;
            if (targetTerrain == null) targetTerrain = GetComponent<Terrain>() ?? Terrain.activeTerrain;
            if (targetTerrain == null || targetTerrain.terrainData == null) return 0;

            EnsureAllPrototypesGrounded();

            TerrainData tData = targetTerrain.terrainData;
            Vector3 terPos = targetTerrain.transform.position;
            Vector3 terSize = tData.size;

            float rClamped = Mathf.Clamp(radius, 0.5f, 50.0f);
            float sClamped = Mathf.Clamp(spacing, 0.5f, 3.5f);
            float area = Mathf.PI * rClamped * rClamped;
            int pointCount = Mathf.Clamp(Mathf.RoundToInt(area / (sClamped * sClamped * 0.82f)), 1, 500);

            // 해당 반경에 영향을 받는 청크별 신규 행렬 목록 수집
            Dictionary<Vector2Int, Dictionary<int, List<Matrix4x4>>> newMatricesPerChunk = 
                new Dictionary<Vector2Int, Dictionary<int, List<Matrix4x4>>>();

            int addedCount = 0;

            // 식생 후보군 준비
            List<int> carpetPool = new List<int>();
            List<int> tallPool = new List<int>();
            List<int> flowerPool = new List<int>();
            List<int> fernShrubPool = new List<int>();

            for (int p = 0; p < prototypes.Count; p++)
            {
                string pName = prototypes[p].protoName.ToLower();
                if (pName.Contains("flower") || pName.Contains("꽃") || pName.Contains("lotus") || pName.Contains("magnolia"))
                    flowerPool.Add(p);
                else if (pName.Contains("fern") || pName.Contains("고사리") || pName.Contains("thick") || pName.Contains("bush") || pName.Contains("덤불"))
                    fernShrubPool.Add(p);
                else if (pName.Contains("seedhead") || pName.Contains("이삭") || pName.Contains("tall") || pName.Contains("reeds"))
                    tallPool.Add(p);
                else
                    carpetPool.Add(p);
            }
            if (carpetPool.Count == 0)
            {
                for (int p = 0; p < prototypes.Count; p++) carpetPool.Add(p);
            }

            for (int i = 0; i < pointCount; i++)
            {
                // 1. 황금각(137.50776도) 나선 극좌표 분포
                float rNorm = Mathf.Sqrt((i + 0.45f) / (float)pointCount);
                float r = rNorm * rClamped;
                float goldenAngle = i * 2.39996323f;
                float jitter = UnityEngine.Random.Range(-sClamped * 0.25f, sClamped * 0.25f);

                float pX = centerPos.x + Mathf.Cos(goldenAngle) * r + jitter;
                float pZ = centerPos.z + Mathf.Sin(goldenAngle) * r + jitter;

                // 2. 외곽 소프트 감쇄 (Soft Edge Falloff)
                if (falloff > 0.01f && rNorm > (1.0f - falloff))
                {
                    float keepProb = (1.0f - rNorm) / Mathf.Max(0.001f, falloff);
                    if (UnityEngine.Random.value > keepProb) continue;
                }

                // 3. 지형 범위 및 높이 검사 (터레인 로컬 변환으로 부모 계층 회전/오프셋 완벽 대응)
                Vector3 localPos = targetTerrain.transform.InverseTransformPoint(new Vector3(pX, centerPos.y, pZ));
                float normX = localPos.x / terSize.x;
                float normZ = localPos.z / terSize.z;
                if (normX < 0.001f || normX > 0.999f || normZ < 0.001f || normZ > 0.999f) continue;

                float slope = tData.GetSteepness(normX, normZ);
                if (slope > 45f) continue; // 45도 초과 절벽 배제

                float localHeight = tData.GetInterpolatedHeight(normX, normZ);
                Vector3 samplePos = targetTerrain.transform.TransformPoint(new Vector3(localPos.x, localHeight, localPos.z));

                // 도로 및 수계 배제
                if (TotalWarRoadSpline.IsOnAnyRoad(samplePos)) continue;
                if (TotalWarRiverSpline.IsAnyRiverAt(samplePos, out _)) continue;
                if (TacticalWaterZone.IsAnyWaterZoneAt(samplePos, out _)) continue;
                if (TotalWarSeaPlane.Instance != null && samplePos.y < TotalWarSeaPlane.Instance.waterLevelY) continue;

                // 4. 프로토타입 인덱스 결정
                int selectedProto = 0;
                if (specificProtoIdx >= 0 && specificProtoIdx < prototypes.Count)
                {
                    selectedProto = specificProtoIdx;
                }
                else
                {
                    // 복합 자연 살포 (지피 70%, 장초 15%, 꽃 8%, 덤불 7%)
                    float dice = UnityEngine.Random.value;
                    if (dice < 0.70f || (tallPool.Count == 0 && flowerPool.Count == 0 && fernShrubPool.Count == 0))
                    {
                        selectedProto = carpetPool[UnityEngine.Random.Range(0, carpetPool.Count)];
                    }
                    else if (dice < 0.85f && tallPool.Count > 0)
                    {
                        selectedProto = tallPool[UnityEngine.Random.Range(0, tallPool.Count)];
                    }
                    else if (dice < 0.93f && flowerPool.Count > 0)
                    {
                        selectedProto = flowerPool[UnityEngine.Random.Range(0, flowerPool.Count)];
                    }
                    else if (fernShrubPool.Count > 0)
                    {
                        selectedProto = fernShrubPool[UnityEngine.Random.Range(0, fernShrubPool.Count)];
                    }
                    else
                    {
                        selectedProto = carpetPool[0];
                    }
                }

                // 5. 지면 1:1 곡면 밀착 트랜스폼 계산
                var proto = prototypes[selectedProto];
                var groundedTransform = CalculateSlopeGroundedTransform(tData, terPos, terSize, pX, pZ, proto, 0.32f, 0.015f);
                Matrix4x4 mat = Matrix4x4.TRS(groundedTransform.pos, groundedTransform.rot, groundedTransform.scale);

                // 6. 대상 청크 키 및 배치 수집
                int cx = Mathf.FloorToInt((pX - terPos.x) / chunkSize);
                int cz = Mathf.FloorToInt((pZ - terPos.z) / chunkSize);
                Vector2Int cKey = new Vector2Int(cx, cz);

                if (!newMatricesPerChunk.TryGetValue(cKey, out var protoDict))
                {
                    protoDict = new Dictionary<int, List<Matrix4x4>>();
                    newMatricesPerChunk[cKey] = protoDict;
                }
                if (!protoDict.TryGetValue(selectedProto, out var mList))
                {
                    mList = new List<Matrix4x4>();
                    protoDict[selectedProto] = mList;
                }
                mList.Add(mat);
                addedCount++;
            }

            // 청크들에 신규 행렬 병합 및 1023개 배치 재구성
            int totalProtoCount = prototypes.Count;
            foreach (var kvp in newMatricesPerChunk)
            {
                Vector2Int cKey = kvp.Key;
                if (!_chunks.TryGetValue(cKey, out var chunk))
                {
                    chunk = new TacticalGrassChunk
                    {
                        chunkCoord = cKey,
                        biomeType = GetBiomeForChunk(cKey, new Vector3(terPos.x + (cKey.x + 0.5f) * chunkSize, terPos.y, terPos.z + (cKey.y + 0.5f) * chunkSize)),
                        densityMultiplier = 1.0f
                    };
                    chunk.InitializeBatches(totalProtoCount);
                    Vector3 chunkMin = new Vector3(terPos.x + cKey.x * chunkSize, terPos.y, terPos.z + cKey.y * chunkSize);
                    Vector3 chunkMax = chunkMin + new Vector3(chunkSize, terSize.y, chunkSize);
                    chunk.bounds = new Bounds((chunkMin + chunkMax) * 0.5f, chunkMax - chunkMin);
                    _chunks[cKey] = chunk;
                }

                foreach (var pKvp in kvp.Value)
                {
                    int pIdx = pKvp.Key;
                    var existing = chunk.GetMatrixList(pIdx);
                    existing.AddRange(pKvp.Value);
                    chunk.RebuildBatchesFromList(pIdx, existing);
                }
            }

            _totalInstanceCount += addedCount;
            _totalChunkCount = _chunks.Count;
            if (addedCount > 0)
            {
                Debug.Log($"<color=#00FF88><b>[TacticalGrassRenderer] 🌿 브러시 풀 심기 완료: {addedCount}개 풀포기 추가됨! (현재 총 풀: {_totalInstanceCount:N0}개)</b></color>");
            }
            return addedCount;
        }

        /// <summary>
        /// 🧹 지정 월드 중심점(centerPos)과 반경(radius) 내의 풀포기들을 깔끔하게 지웁니다.
        /// </summary>
        public int EraseGrassAt(Vector3 centerPos, float radius = 6.0f)
        {
            if (_chunks == null || _chunks.Count == 0) return 0;

            float rClamped = Mathf.Clamp(radius, 0.5f, 50.0f);
            float rSq = rClamped * rClamped;

            Vector3 terPos = (targetTerrain != null) ? targetTerrain.transform.position : Vector3.zero;
            int minCx = Mathf.FloorToInt((centerPos.x - rClamped - terPos.x) / chunkSize);
            int maxCx = Mathf.FloorToInt((centerPos.x + rClamped - terPos.x) / chunkSize);
            int minCz = Mathf.FloorToInt((centerPos.z - rClamped - terPos.z) / chunkSize);
            int maxCz = Mathf.FloorToInt((centerPos.z + rClamped - terPos.z) / chunkSize);

            int removedCount = 0;
            List<Vector2Int> emptyChunks = new List<Vector2Int>();

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    Vector2Int cKey = new Vector2Int(cx, cz);
                    if (!_chunks.TryGetValue(cKey, out var chunk)) continue;

                    bool chunkModified = false;
                    for (int p = 0; p < prototypes.Count; p++)
                    {
                        var list = chunk.GetMatrixList(p);
                        if (list.Count == 0) continue;

                        int beforeCount = list.Count;
                        list.RemoveAll(m =>
                        {
                            Vector3 pos = m.GetColumn(3);
                            float dx = pos.x - centerPos.x;
                            float dz = pos.z - centerPos.z;
                            return (dx * dx + dz * dz <= rSq);
                        });

                        int diff = beforeCount - list.Count;
                        if (diff > 0)
                        {
                            removedCount += diff;
                            chunk.RebuildBatchesFromList(p, list);
                            chunkModified = true;
                        }
                    }

                    if (chunkModified && chunk.GetTotalTuftCount() == 0)
                    {
                        emptyChunks.Add(cKey);
                    }
                }
            }

            // 완전히 빈 청크는 딕셔너리에서 정리
            for (int i = 0; i < emptyChunks.Count; i++)
            {
                _chunks.Remove(emptyChunks[i]);
            }

            _totalInstanceCount = Mathf.Max(0, _totalInstanceCount - removedCount);
            _totalChunkCount = _chunks.Count;
            if (removedCount > 0)
            {
                Debug.Log($"<color=#FF5555><b>[TacticalGrassRenderer] 🧹 브러시 풀 지우기 완료: {removedCount}개 풀포기 제거됨! (현재 총 풀: {_totalInstanceCount:N0}개)</b></color>");
            }
            return removedCount;
        }

        #endregion

        #endregion
    }
}
