using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어 부대 카드들을 관리하고, 선택 상태 동기화 및 생명주기를 총괄하는 매니저 클래스입니다.
/// </summary>
public class SquadCardUIManager : MonoBehaviour
{
    private static SquadCardUIManager _instance;
    public static SquadCardUIManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<SquadCardUIManager>();
            return _instance;
        }
    }

    [Header("UI References")]
    public Transform cardContainer;
    public GameObject squadCardPrefab;

    private List<SquadCardUI> activeCards = new List<SquadCardUI>();
    private PlayerController playerController;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (cardContainer == null)
        {
            Transform panel = transform.Find("SquadCardPannel") ?? transform.Find("CardContainer");
            if (panel != null) cardContainer = panel;
            else cardContainer = transform;
        }
    }

    private void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        // 매 프레임 병력 수 변동 등을 UI에 갱신
        for (int i = activeCards.Count - 1; i >= 0; i--)
        {
            if (activeCards[i] != null)
            {
                activeCards[i].UpdateUI();
            }
            else
            {
                activeCards.RemoveAt(i);
            }
        }
    }

    public void RegisterSquad(Squad squad)
    {
        if (squad == null) return;
        
        // 이미 등록된 부대인지 확인
        foreach (var card in activeCards)
        {
            if (card != null && card.GetSquad() == squad) return;
        }

        if (cardContainer == null)
        {
            cardContainer = transform;
        }

        GameObject cardObj = null;
        if (squadCardPrefab != null)
        {
            cardObj = Instantiate(squadCardPrefab, cardContainer);
        }
        else
        {
            Debug.LogWarning("[SquadCardUIManager] squadCardPrefab이 할당되지 않았습니다!");
            return;
        }

        SquadCardUI cardUI = cardObj.GetComponent<SquadCardUI>();
        if (cardUI == null)
        {
            // 프리팹에 SquadCardUI 스크립트가 누락된 경우 방어적으로 추가
            cardUI = cardObj.AddComponent<SquadCardUI>();
        }

        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        cardUI.Initialize(squad, playerController);
        activeCards.Add(cardUI);
    }

    public void UnregisterSquad(Squad squad)
    {
        if (cardContainer == null || squad == null) return;

        for (int i = activeCards.Count - 1; i >= 0; i--)
        {
            var card = activeCards[i];
            if (card != null && card.GetSquad() == squad)
            {
                Destroy(card.gameObject);
                activeCards.RemoveAt(i);
            }
        }
    }

    public void UpdateSelectionUI(List<Squad> selectedSquads)
    {
        foreach (var card in activeCards)
        {
            if (card != null)
            {
                bool isSelected = selectedSquads != null && selectedSquads.Contains(card.GetSquad());
                card.SetHighlight(isSelected);
            }
        }
    }
}
