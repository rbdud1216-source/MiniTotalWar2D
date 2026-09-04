using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 씬에 존재하는 부대 머리 위 아이콘 UI들을 관리하고 생성/파괴를 총괄하는 매니저 클래스입니다.
/// </summary>
public class SquadIconUIManager : MonoBehaviour
{
    private static SquadIconUIManager _instance;
    public static SquadIconUIManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<SquadIconUIManager>();
            return _instance;
        }
    }

    [Header("UI References")]
    public GameObject squadIconPrefab;

    private RectTransform dedicatedContainer;
    private List<SquadIconUI> activeIcons = new List<SquadIconUI>();
    private PlayerController playerController;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }

        EnsureContainer();
    }

    private void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
    }

    private void EnsureContainer()
    {
        if (dedicatedContainer != null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = FindAnyObjectByType<Canvas>();
        }

        if (parentCanvas != null)
        {
            Transform existing = parentCanvas.transform.Find("SquadIcon_Container");
            if (existing != null)
            {
                dedicatedContainer = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject containerObj = new GameObject("SquadIcon_Container");
                containerObj.transform.SetParent(parentCanvas.transform, false);
                dedicatedContainer = containerObj.AddComponent<RectTransform>();
                
                dedicatedContainer.anchorMin = Vector2.zero;
                dedicatedContainer.anchorMax = Vector2.one;
                dedicatedContainer.offsetMin = Vector2.zero;
                dedicatedContainer.offsetMax = Vector2.zero;
                dedicatedContainer.localScale = Vector3.one;
                
                dedicatedContainer.SetAsFirstSibling();
            }
        }
    }

    public void RegisterSquad(Squad squad)
    {
        if (squad == null) return;
        
        foreach (var icon in activeIcons)
        {
            if (icon != null && icon.GetSquad() == squad) return;
        }

        EnsureContainer();

        Transform parentTransform = (dedicatedContainer != null) ? dedicatedContainer : transform;

        if (squadIconPrefab == null)
        {
            Debug.LogWarning("[SquadIconUIManager] squadIconPrefab이 할당되지 않았습니다!");
            return;
        }

        GameObject iconObj = Instantiate(squadIconPrefab, parentTransform);
        SquadIconUI iconUI = iconObj.GetComponent<SquadIconUI>();
        
        if (iconUI == null)
        {
            // 프리팹에 SquadIconUI 스크립트가 누락된 경우 방어적으로 추가
            iconUI = iconObj.AddComponent<SquadIconUI>();
        }

        iconUI.Initialize(squad);
        activeIcons.Add(iconUI);
    }

    public void UnregisterSquad(Squad squad)
    {
        if (squad == null) return;

        for (int i = activeIcons.Count - 1; i >= 0; i--)
        {
            var icon = activeIcons[i];
            if (icon != null && icon.GetSquad() == squad)
            {
                Destroy(icon.gameObject);
                activeIcons.RemoveAt(i);
            }
        }
    }
}
