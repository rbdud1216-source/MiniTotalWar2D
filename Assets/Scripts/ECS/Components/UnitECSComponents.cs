using Unity.Entities;
using Unity.Mathematics;

namespace MiniTotalWar.ECS
{
    /// <summary>
    /// 유닛의 진영 및 소속 정보를 담는 태그 컴포넌트
    /// </summary>
    public struct UnitEntityTag : IComponentData
    {
        public int Faction;       // 1 = 아군(Player), 0 = 적군(Enemy)
        public int SquadId;       // 소속 부대 ID (-1 = 자유유닛)
        public int IsFreeUnit;    // 1 = 자유유닛 (반경 1.15m 비정규 난전), 0 = 부대유닛 (반경 1.00m 정규 방패벽)
        public int IsAlive;       // 1 = 생존, 0 = 사망
        public int SlotIndex;     // 부대 내 고유 슬롯 인덱스 (0 ~ N-1)
        public int Row;           // 현재 행(Row)
        public int Col;           // 현재 열(Col)
    }

    /// <summary>
    /// 유닛의 이동 및 좌표, 속도, 목표 위치 데이터
    /// </summary>
    public struct UnitMovementData : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 Velocity;
        public float3 TargetPosition;
        public quaternion TargetRotation;
        public float MoveSpeed;
        public float CurrentSpeed;
        public float Acceleration;
        public float StoppingDistance;
        public int IsCharging;        // 1 = 돌격 중(1.8m/s), 0 = 일반 이동
        public int IsCombatRunning;   // 1 = 교전 중 이격 시 달리기(1.2m/s)
    }

    /// <summary>
    /// 유닛의 전투 능력치, 체력, 넉백 및 교전 상태 데이터
    /// </summary>
    public struct UnitCombatData : IComponentData
    {
        public float CurrentHp;
        public float MaxHp;
        public float Damage;
        public int Armor;                      // 기본 방어력 (0 ~ 10000, 10000 = 100.00% 완전 방어)
        public float AttackCooldown;
        public float LastAttackTime;
        public float DetectRange;
        public float AttackRange;
        public float MinAttackRange;           // 주무기 최소 사거리 (m, 0이면 제한 없음)
        public float OptimalRangeMin;          // 주무기 최적 사거리 최소 기준거리 (m, 0이면 감쇠 없음)
        public float CloseRangeDamageRatio;    // 최적 사거리 미만 피해량 비율 (0.1 ~ 1.0)
        public float KnockbackPower;           // 주무기 넉백 세기 배율 (기본: 1.0)
        public int UseSidearm;                 // 1 = 보조무기(단검) 자동 전환 활성, 0 = 비활성
        public float SidearmSwitchDistance;    // 보조무기 전환 기준 거리 (m)
        public float SidearmAttackRange;       // 보조무기 공격 사거리 (m)
        public float SidearmDamage;            // 보조무기 공격력
        public float SidearmAttackCooldown;    // 보조무기 공격 쿨다운 (초)
        public float SidearmKnockbackPower;    // 보조무기 넉백 세기 배율 (기본: 0.1)
        public float CombatStoppingDistance; // 교전(백병전) 시 발을 멈추는 정지 거리 (m)
        public float EngagementOffset;       // 적을 향해 접근할 때 적 중심으로부터의 목표 교전 간격/위치 (m)
        public int CurrentState;          // 0 = Idle, 1 = Move, 2 = AttackMove, 3 = MeleeEngaged
        public Entity TargetEntity;       // 현재 타겟팅된 적 엔티티 (Entity.Null = 없음) - 1.45m 내 근접 교전 중인 고정(Lock-on) 타겟용
        public Entity RadarTargetEntity;  // 시야(DetectRange) 내 가장 가까운 적 레이더 감지용 타겟 (UnitTargetSearchSystem 전용)
        public float3 KnockbackVelocity;  // 넉백 충격 물리 속도
        public float Mass;                // 질량 (kg)
        public float ChargeSpeed;         // 돌격 최대 속도
        public float ChargeBonus;         // 돌격 충격 보너스
        public float MaxChargeDamage;     // 최대 돌격 데미지
        public int ChargeImpactReady;     // 1 = 첫 충돌 넉백 펄스 장전, 0 = 소진
        public float EngagementStartTime; // 교전 시작 시간 (0f = 비교전)
        public int AutoAttackEnabled;     // 1 = 자동 선제 돌격 On, 0 = 근접 접촉 방어 Off (V)
        public int CanReflectCharge;       // 1 = 돌격 반사 가능(장창병), 0 = 불가능(검병)
        public float KnockdownThreshold;   // 넘어짐 판정 넉백 속도 임계값 (m/s)
        public float KnockdownDuration;    // 무력화 유지 시간 (초, 기본 3.0초)
        public float KnockdownTimer;       // 현재 남은 무력화 시간 (초)
        public int IsImmuneToKnockdown;    // 1 = 넘어짐/무력화 면역(불굴 특수능력), 0 = 넘어짐 가능
        public float BaseMeleeKnockback;   // 평타 넉백 기본 세기 (기본: 0.45m/s)
        public float MaxMeleeKnockbackCap; // 다대일 평타 넉백 상한 속도 (기본: 1.2m/s)
        public float SidearmBaseKnockback; // 보조무기 평타 넉백 기본 세기 (기본: 0.2m/s)
        public float SidearmMaxKnockbackCap; // 보조무기 다대일 넉백 상한 속도 (기본: 0.8m/s)
        public int TargetSquadId;         // 부대 지휘관이 지정한 목표 적 부대 InstanceID (-1 = 없음/자유)
        public float3 CachedEnemyPos;     // 0.1초 주기로 갱신되는 타겟 적의 위치 캐시 (최적화)
        public float TargetSearchTimer;   // 적 위치/타겟 탐색 주기 타이머 (0.1초 주기 분산)
    }

    /// <summary>
    /// 유닛 간 겹침 방지 및 척력(Separation) 데이터
    /// </summary>
    public struct UnitSeparationData : IComponentData
    {
        public float PersonalRadius;      // 충돌 척력 반경 (부대원 1.0m, 자유유닛 1.15m)
        public float3 SeparationForce;    // 누적 밀어내기 척력 벡터
    }

    /// <summary>
    /// 공간 분할(Spatial Partitioning) 해시 셀 인덱스
    /// </summary>
    public struct SpatialGridCell : IComponentData
    {
        public int CellHash;
        public int3 GridCoord;
    }
}
