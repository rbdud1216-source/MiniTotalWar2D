using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using MiniTotalWar.Roster;

namespace MiniTotalWar.OpenWorld
{
    /// <summary>
    /// 부대 지휘 권한 모드 (직접 조작 vs AI 부관 위임)
    /// </summary>
    public enum SquadAuthorityMode
    {
        PlayerDirect = 0,
        AIDelegated = 1
    }

    /// <summary>
    /// 타 지역 원거리 화력 지원 유형 (야포, 중포, 함포 일제사격)
    /// </summary>
    public enum OffMapSupportType
    {
        FieldArtillery = 0,
        HeavyArtillery = 1,
        NavalBroadside = 2
    }

    /// <summary>
    /// 대륙 절대 좌표 구조체 (SectorX/Z + 섹터 내 로컬 좌표)
    /// 호스트 시뮬레이션, 캠페인 저장 데이터, 네트워크 패킷 및 오프맵 포격 좌표는
    /// 항상 클라이언트의 Floating Origin과 무관한 이 절대 좌표를 단일 진실 원천으로 사용합니다.
    /// </summary>
    [Serializable]
    public struct WorldCoord : IEquatable<WorldCoord>
    {
        public const float DefaultSectorSize = 512f;

        public int sectorX;
        public int sectorZ;
        public float localX;
        public float localZ;

        public WorldCoord(int sectorX, int sectorZ, float localX = 0f, float localZ = 0f)
        {
            this.sectorX = sectorX;
            this.sectorZ = sectorZ;
            this.localX = localX;
            this.localZ = localZ;
        }

        /// <summary>
        /// 로컬 좌표가 [0, sectorSize) 범위를 벗어난 경우 섹터 인덱스로 정규화합니다.
        /// </summary>
        public WorldCoord Normalized(float sectorSize = DefaultSectorSize)
        {
            if (sectorSize <= 0.001f) sectorSize = DefaultSectorSize;

            int shiftX = Mathf.FloorToInt(localX / sectorSize);
            int shiftZ = Mathf.FloorToInt(localZ / sectorSize);

            return new WorldCoord(
                sectorX + shiftX,
                sectorZ + shiftZ,
                localX - shiftX * sectorSize,
                localZ - shiftZ * sectorSize
            );
        }

        /// <summary>
        /// 배정밀도(double2) 절대 미터 좌표로 변환합니다. (초대형 대륙에서도 오차 0 보장)
        /// </summary>
        public double2 ToDouble2(float sectorSize = DefaultSectorSize)
        {
            return new double2(
                (double)sectorX * sectorSize + localX,
                (double)sectorZ * sectorSize + localZ
            );
        }

        /// <summary>
        /// 배정밀도(double2) 절대 미터 좌표에서 WorldCoord를 복원합니다.
        /// </summary>
        public static WorldCoord FromDouble2(double2 absPos, float sectorSize = DefaultSectorSize)
        {
            if (sectorSize <= 0.001f) sectorSize = DefaultSectorSize;

            int sx = (int)math.floor(absPos.x / sectorSize);
            int sz = (int)math.floor(absPos.y / sectorSize);
            float lx = (float)(absPos.x - (double)sx * sectorSize);
            float lz = (float)(absPos.y - (double)sz * sectorSize);

            return new WorldCoord(sx, sz, lx, lz);
        }

        /// <summary>
        /// 지정된 원점(origin)을 기준으로 Unity 로컬 3D 월드 좌표로 변환합니다.
        /// 섹터 차분을 먼저 정수 연산으로 상쇄해 부동소수점 정밀도 손실을 원천 차단합니다.
        /// </summary>
        public Vector3 ToUnityLocal(WorldCoord origin, float y = 0f, float sectorSize = DefaultSectorSize)
        {
            double dx = ((double)(sectorX - origin.sectorX)) * sectorSize + (localX - origin.localX);
            double dz = ((double)(sectorZ - origin.sectorZ)) * sectorSize + (localZ - origin.localZ);
            return new Vector3((float)dx, y, (float)dz);
        }

        /// <summary>
        /// Unity 로컬 3D 좌표를 기준 원점(origin)에 더해 절대 대륙 좌표로 역변환합니다.
        /// </summary>
        public static WorldCoord FromUnityLocal(Vector3 localPos, WorldCoord origin, float sectorSize = DefaultSectorSize)
        {
            WorldCoord raw = new WorldCoord(
                origin.sectorX,
                origin.sectorZ,
                origin.localX + localPos.x,
                origin.localZ + localPos.z
            );
            return raw.Normalized(sectorSize);
        }

        /// <summary>
        /// 두 절대 좌표 사이의 수평 직선 거리(미터)를 계산합니다.
        /// </summary>
        public static float Distance(WorldCoord a, WorldCoord b, float sectorSize = DefaultSectorSize)
        {
            double2 da = a.ToDouble2(sectorSize);
            double2 db = b.ToDouble2(sectorSize);
            return (float)math.distance(da, db);
        }

        /// <summary>
        /// 시작 좌표(from)에서 목표 좌표(to)로 향하는 방향 방위각(도, 0=북 +Z, 90=동 +X)을 반환합니다.
        /// 타 지역 포격 궤적 및 증원군 진입 방위 산출에 사용됩니다.
        /// </summary>
        public static float BearingDegrees(WorldCoord from, WorldCoord to, float sectorSize = DefaultSectorSize)
        {
            double2 a = from.ToDouble2(sectorSize);
            double2 b = to.ToDouble2(sectorSize);
            double dx = b.x - a.x;
            double dz = b.y - a.y;
            if (dx * dx + dz * dz < 1e-8) return 0f;

            float angle = Mathf.Atan2((float)dx, (float)dz) * Mathf.Rad2Deg;
            return Mathf.Repeat(angle, 360f);
        }

        public bool Equals(WorldCoord other)
        {
            return sectorX == other.sectorX
                && sectorZ == other.sectorZ
                && Mathf.Approximately(localX, other.localX)
                && Mathf.Approximately(localZ, other.localZ);
        }

        public override bool Equals(object obj) => obj is WorldCoord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = sectorX * 397 ^ sectorZ;
                hash = (hash * 397) ^ localX.GetHashCode();
                hash = (hash * 397) ^ localZ.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"Sector({sectorX}, {sectorZ}) + ({localX:F1}, {localZ:F1})";
        }
    }

    /// <summary>
    /// 클라이언트 로컬 원점 시프트(Floating Origin) 관리자입니다.
    /// 전선이 원점에서 1,024m 이상 멀어지면 섹터 정수 배수 단위로 로컬 좌표계를 재정렬해
    /// 대륙 크기(8~18km 이상)와 무관하게 부동소수점 떨림 없이 100% 정밀한 렌더링을 유지합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingOriginManager : MonoBehaviour
    {
        private static FloatingOriginManager _instance;
        public static FloatingOriginManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FloatingOriginManager>();
                }
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        [Header("원점 시프트(Floating Origin) 규격")]
        [Tooltip("대륙 단일 섹터 크기 (단위: m, 기본: 512m)")]
        public float sectorSize = WorldCoord.DefaultSectorSize;

        [Tooltip("로컬 원점 재정렬을 트리거하는 한계 반경 (단위: m, 기본: 1024m)")]
        public float shiftThresholdMeters = 1024f;

        [Header("현재 활성 기준 원점 섹터")]
        [SerializeField] private int originSectorX = 0;
        [SerializeField] private int originSectorZ = 0;

        public int OriginSectorX => originSectorX;
        public int OriginSectorZ => originSectorZ;
        public WorldCoord CurrentOrigin => new WorldCoord(originSectorX, originSectorZ, 0f, 0f);

        /// <summary>
        /// 원점이 재정렬될 때 로컬 오브젝트/엔티티들에 가산할 이동 델타(-shiftDelta)를 전파합니다.
        /// </summary>
        public event Action<Vector3> OnOriginShifted;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 기준 원점 섹터를 지정된 섹터로 설정합니다. (전투 진입 초기화용)
        /// </summary>
        public void SetOrigin(int sectorX, int sectorZ, bool notify = false)
        {
            int deltaSX = sectorX - originSectorX;
            int deltaSZ = sectorZ - originSectorZ;

            originSectorX = sectorX;
            originSectorZ = sectorZ;

            if (notify && (deltaSX != 0 || deltaSZ != 0))
            {
                Vector3 shiftDelta = new Vector3(-deltaSX * sectorSize, 0f, -deltaSZ * sectorSize);
                OnOriginShifted?.Invoke(shiftDelta);
            }
        }

        /// <summary>
        /// 절대 대륙 좌표를 현재 클라이언트 로컬 Unity 좌표로 변환합니다.
        /// </summary>
        public Vector3 WorldToLocal(WorldCoord coord, float y = 0f)
        {
            return coord.ToUnityLocal(CurrentOrigin, y, sectorSize);
        }

        /// <summary>
        /// 현재 클라이언트 로컬 Unity 좌표를 절대 대륙 좌표로 변환합니다.
        /// </summary>
        public WorldCoord LocalToWorld(Vector3 localPos)
        {
            return WorldCoord.FromUnityLocal(localPos, CurrentOrigin, sectorSize);
        }

        /// <summary>
        /// 주시 대상(카메라 또는 주력 전선 중심)이 한계 반경을 벗어났다면
        /// 섹터 정수 배수 단위로 로컬 원점을 즉시 재정렬합니다.
        /// </summary>
        public bool TryShiftOrigin(Vector3 focusLocalPos)
        {
            float effSector = Mathf.Max(1f, sectorSize);
            if (Mathf.Abs(focusLocalPos.x) < shiftThresholdMeters && Mathf.Abs(focusLocalPos.z) < shiftThresholdMeters)
            {
                return false;
            }

            int deltaSX = Mathf.RoundToInt(focusLocalPos.x / effSector);
            int deltaSZ = Mathf.RoundToInt(focusLocalPos.z / effSector);
            if (deltaSX == 0 && deltaSZ == 0)
            {
                return false;
            }

            originSectorX += deltaSX;
            originSectorZ += deltaSZ;

            Vector3 localOffsetDelta = new Vector3(-deltaSX * effSector, 0f, -deltaSZ * effSector);
            OnOriginShifted?.Invoke(localOffsetDelta);
            return true;
        }
    }

    /// <summary>
    /// 전투 씬에 등록되는 타 지역 포병대·함대 원거리 화력 지원 데이터
    /// </summary>
    [Serializable]
    public class OffMapFireSupportData
    {
        public string supportId;
        public string displayName = "아군 포병 지원";
        public bool isPlayerSide = true;
        public byte ownerPlayerId = 0;
        public OffMapSupportType supportType = OffMapSupportType.FieldArtillery;
        public WorldCoord sourceCoord;
        public float maxRangeMeters = 1500f;
        public int shellsPerSalvo = 12;
        public float cooldownSeconds = 25f;
        public float blastRadius = 7.5f;
        public float damagePerShell = 55f;
        [Range(0f, 1f)] public float igniteFireChance = 0.45f;
        public int remainingSalvos = 5;
    }

    /// <summary>
    /// 전투 중 인접 섹터에서 행군해 합류하거나 플레이어 난입으로 도착하는 증원 제대 데이터
    /// </summary>
    [Serializable]
    public class ReinforcementWaveData
    {
        public string armyId;
        public string displayName = "제2지원군";
        public bool isPlayerSide = true;
        public byte ownerPlayerId = 0;
        public WorldCoord sourceCoord;
        public float entryBearingDegrees = 180f;
        public float delaySeconds = 15f;
        public List<UnitCardData> cards = new List<UnitCardData>();
    }

    /// <summary>
    /// 캠페인 지도 씬과 심리스 오픈월드 전투 씬 사이의 데이터 전달 컨텍스트
    /// 교전 좌표, 진입 방위, 계절·기상·풍향, 타 지역 포격 및 증원 제대 목록을 보관합니다.
    /// </summary>
    [Serializable]
    public class BattleTransitionContext
    {
        public static BattleTransitionContext ActiveContext;

        public string continentId = "ThreeKingdoms_China";
        public WorldCoord encounterCoord = new WorldCoord(8, 7, 256f, 256f);
        public float approachAngleDegrees = 0f;

        public TacticalForestZone.Season season = TacticalForestZone.Season.Summer;
        public float windAngle = 45f;
        public float windStrength = 0.45f;
        public float snowAmount = 0f;

        public bool isMultiplayer = false;
        public byte localPlayerId = 0;
        public bool returnToCampaignOnFinish = false;

        public List<OffMapFireSupportData> offMapFireSupports = new List<OffMapFireSupportData>();
        public List<ReinforcementWaveData> reinforcements = new List<ReinforcementWaveData>();

        public static void Clear()
        {
            ActiveContext = null;
        }
    }
}
