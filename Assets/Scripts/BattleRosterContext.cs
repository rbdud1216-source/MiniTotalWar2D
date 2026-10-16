using System.Collections.Generic;
using UnityEngine;

namespace MiniTotalWar.Roster
{
    /// <summary>
    /// 개별 부대 카드의 편성 및 생명주기 상태 데이터를 보관하는 직렬화 DTO 클래스입니다.
    /// 캠페인 및 자유 전투 로스터에서 전투 씬으로 전달됩니다.
    /// </summary>
    [System.Serializable]
    public class UnitCardData
    {
        [Tooltip("병종 식별자 (예: CHN_Infantry, CHN_Gunner, JPN_Tank_HaGo)")]
        public string unitTypeId = "CHN_Infantry";

        [Tooltip("플레이어가 지정한 부대 명칭")]
        public string customSquadName = "제1 보병대";

        [Tooltip("현재 생존 병력 수 (완편: 60명, 손실 상태: 35명 등)")]
        public int currentUnitCount = 60;

        [Tooltip("완편 기준 최대 병력 수")]
        public int maxUnitCount = 60;

        [Tooltip("가로 열(Columns) 수")]
        public int columns = 15;

        [Tooltip("초기 진형 형태")]
        public SquadFormationType formationType = SquadFormationType.Normal;

        [Tooltip("시작 시 탄약 잔량 비율 (0.0 ~ 1.0, 기본: 1.0)")]
        [Range(0f, 1f)]
        public float startingAmmoRatio = 1.0f;

        [Tooltip("숙련도/베테랑 랭크 (0~9단계)")]
        public int veteranRank = 0;

        [Tooltip("지휘관 부대 여부 (사기 오라 35m 발산)")]
        public bool isCommander = false;

        [Tooltip("소속 상위 제대 번호 (1: F1, 2: F2, 3: F3, 4: F4, 5: F5, 0: 미지정)")]
        public int assignedEchelon = 1;

        /// <summary>
        /// 인원 완편 비율 (0.0 ~ 1.0)
        /// </summary>
        public float StrengthRatio => (maxUnitCount > 0) ? Mathf.Clamp01((float)currentUnitCount / maxUnitCount) : 1f;

        public UnitCardData Clone()
        {
            return new UnitCardData
            {
                unitTypeId = this.unitTypeId,
                customSquadName = this.customSquadName,
                currentUnitCount = this.currentUnitCount,
                maxUnitCount = this.maxUnitCount,
                columns = this.columns,
                formationType = this.formationType,
                startingAmmoRatio = this.startingAmmoRatio,
                veteranRank = this.veteranRank,
                isCommander = this.isCommander,
                assignedEchelon = this.assignedEchelon
            };
        }
    }

    /// <summary>
    /// 군단 전체의 진영 및 무제한 부대 카드 목록을 담는 직렬화 DTO 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class ArmyRosterData
    {
        [Tooltip("진영 식별자 (예: Republic_Of_China, Empire_Of_Japan)")]
        public string factionId = "Republic_Of_China";

        [Tooltip("군단장 또는 사령관 이름")]
        public string commanderName = "군단 사령관";

        [Tooltip("편성된 부대 카드 목록 (개수 무제한 지원)")]
        public List<UnitCardData> cards = new List<UnitCardData>();

        /// <summary>
        /// 특정 상위 제대(Echelon)에 소속된 부대 카드 목록을 필터링하여 반환합니다.
        /// </summary>
        public List<UnitCardData> GetCardsByEchelon(int echelonIndex)
        {
            List<UnitCardData> result = new List<UnitCardData>();
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].assignedEchelon == echelonIndex)
                {
                    result.Add(cards[i]);
                }
            }
            return result;
        }

        public ArmyRosterData Clone()
        {
            ArmyRosterData clone = new ArmyRosterData
            {
                factionId = this.factionId,
                commanderName = this.commanderName,
                cards = new List<UnitCardData>(this.cards.Count)
            };
            foreach (var card in this.cards)
            {
                if (card != null) clone.cards.Add(card.Clone());
            }
            return clone;
        }
    }

    /// <summary>
    /// 씬 전환 시에도 군단 로스터 데이터를 안전하게 유지하는 전역 컨텍스트 클래스입니다.
    /// 캠페인 또는 자유 전투 로비에서 설정한 데이터가 전투 씬의 BattleManager와 DeploymentZoneManager로 전달됩니다.
    /// </summary>
    public static class BattleRosterContext
    {
        public static ArmyRosterData PlayerArmy;
        public static ArmyRosterData EnemyArmy;
        public static string SelectedMapId = "Hulao_Pass";
        public static bool IsCustomBattle = true;

        /// <summary>
        /// 컨텍스트에 군단 데이터가 없거나 비어있는 경우(에디터 직접 실행 등), 기본 테스트 군단을 자동 세팅합니다.
        /// </summary>
        public static void EnsureDefaultFallback()
        {
            if (PlayerArmy == null || PlayerArmy.cards == null || PlayerArmy.cards.Count == 0)
            {
                PlayerArmy = new ArmyRosterData
                {
                    factionId = "Republic_Of_China",
                    commanderName = "플레이어 사령관",
                    cards = new List<UnitCardData>
                    {
                        new UnitCardData { unitTypeId = "CHN_Infantry", customSquadName = "제1 보병대", currentUnitCount = 60, maxUnitCount = 60, columns = 15, assignedEchelon = 1 },
                        new UnitCardData { unitTypeId = "CHN_Gunner", customSquadName = "제2 총병대", currentUnitCount = 40, maxUnitCount = 40, columns = 20, assignedEchelon = 1 },
                        new UnitCardData { unitTypeId = "CHN_Archer", customSquadName = "제3 궁병대", currentUnitCount = 40, maxUnitCount = 40, columns = 10, assignedEchelon = 1 },
                        new UnitCardData { unitTypeId = "CHN_Infantry", customSquadName = "제2연대 보병대", currentUnitCount = 60, maxUnitCount = 60, columns = 15, assignedEchelon = 2 },
                        new UnitCardData { unitTypeId = "CHN_Gunner", customSquadName = "제2연대 지원사격대", currentUnitCount = 40, maxUnitCount = 40, columns = 20, assignedEchelon = 2 }
                    }
                };
            }

            if (EnemyArmy == null || EnemyArmy.cards == null || EnemyArmy.cards.Count == 0)
            {
                EnemyArmy = new ArmyRosterData
                {
                    factionId = "Empire_Of_Japan",
                    commanderName = "적군 사령관",
                    cards = new List<UnitCardData>
                    {
                        new UnitCardData { unitTypeId = "JPN_Infantry", customSquadName = "적 좌익 보병대", currentUnitCount = 60, maxUnitCount = 60, columns = 15, assignedEchelon = 1 },
                        new UnitCardData { unitTypeId = "JPN_Infantry", customSquadName = "적 우익 보병대", currentUnitCount = 60, maxUnitCount = 60, columns = 15, assignedEchelon = 1 },
                        new UnitCardData { unitTypeId = "JPN_Gunner", customSquadName = "적 중앙 총병대", currentUnitCount = 40, maxUnitCount = 40, columns = 20, assignedEchelon = 1 },
                        new UnitCardData { unitTypeId = "JPN_Archer", customSquadName = "적 후방 궁병대", currentUnitCount = 40, maxUnitCount = 40, columns = 10, assignedEchelon = 1 }
                    }
                };
            }
        }

        /// <summary>
        /// 전투 종료 후 컨텍스트를 초기화합니다.
        /// </summary>
        public static void Clear()
        {
            PlayerArmy = null;
            EnemyArmy = null;
        }
    }
}
