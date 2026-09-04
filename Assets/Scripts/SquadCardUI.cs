using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 하단 UI 패널에 표시되는 개별 부대 카드 UI 클래스입니다.
/// 부대 이름, 부대원 수 표시 및 클릭 시 부대 선택 기능을 제공합니다.
/// </summary>
public class SquadCardUI : MonoBehaviour, IPointerClickHandler
{
    private Squad targetSquad;
    private PlayerController playerController;

    [Header("UI References")]
    public TMPro.TextMeshProUGUI squadNameText;
    public TMPro.TextMeshProUGUI unitCountText;
    public Image highlightImage; // 활성화 시 테두리나 배경색 변경용
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
        AutoFindReferences();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void AutoFindReferences()
    {
        if (squadNameText == null || unitCountText == null)
        {
            TMPro.TextMeshProUGUI[] texts = GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                string objName = t.gameObject.name.ToLower();
                if (squadNameText == null && (objName.Contains("name") || objName.Contains("title")))
                {
                    squadNameText = t;
                }
                else if (unitCountText == null && (objName.Contains("count") || objName.Contains("mumber") || objName.Contains("member") || objName.Contains("num")))
                {
                    unitCountText = t;
                }
            }

            // 이름을 찾지 못한 경우 인덱스로 fallback
            if (texts.Length > 0 && squadNameText == null && unitCountText != texts[0]) squadNameText = texts[0];
            if (texts.Length > 1 && unitCountText == null && squadNameText != texts[1]) unitCountText = texts[1];
        }

        if (highlightImage == null)
        {
            Transform hl = transform.Find("Highlight") ?? transform.Find("Selected");
            if (hl != null) highlightImage = hl.GetComponent<Image>();
        }
    }

    public void Initialize(Squad squad, PlayerController pc)
    {
        targetSquad = squad;
        playerController = pc;

        AutoFindReferences();
        UpdateUI();
        SetHighlight(false);
    }

    public Squad GetSquad()
    {
        return targetSquad;
    }

    public void UpdateUI()
    {
        if (targetSquad == null) return;

        if (playerController == null) playerController = Object.FindAnyObjectByType<PlayerController>();

        if (squadNameText != null)
        {
            string rawName = !string.IsNullOrEmpty(targetSquad.squadName)
                ? targetSquad.squadName
                : targetSquad.gameObject.name;

            bool isLocked = playerController != null && playerController.IsSquadInLockedGroup(targetSquad);
            squadNameText.text = isLocked ? $"[G] {rawName}" : rawName;
        }

        if (unitCountText != null)
        {
            unitCountText.text = targetSquad.MemberCount.ToString();
        }
    }

    public void SetHighlight(bool isSelected)
    {
        if (highlightImage != null)
        {
            highlightImage.enabled = isSelected;
        }
    }

    private void OnButtonClicked()
    {
        HandleSquadSelection();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // UI 버튼 클릭과 포인터 클릭 중복 방지
    }

    private void HandleSquadSelection()
    {
        if (targetSquad == null) return;

        if (playerController == null)
        {
            playerController = Object.FindAnyObjectByType<PlayerController>();
        }

        if (playerController != null)
        {
            bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (isCtrlPressed)
            {
                playerController.ToggleSquadSelection(targetSquad);
            }
            else if (isShiftPressed)
            {
                playerController.SelectSquadDirectly(targetSquad, addSelection: true);
            }
            else
            {
                playerController.SelectSquadDirectly(targetSquad, addSelection: false);
            }
        }
    }
}
