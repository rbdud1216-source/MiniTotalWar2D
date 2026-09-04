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
        public float AttackCooldown;
        public float LastAttackTime;
        public float DetectRange;
        public float AttackRange;
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
        public int TargetSquadId;         // 부대 지휘관이 지정한 목표 적 부대 InstanceID (-1 = 없음/자유)
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
