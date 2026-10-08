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
    [Header("🏹 원거리 탄약(화살) 잔량 게이지")]
    public Image ammoBarImage;
    public GameObject ammoBarRoot;
    private bool hasFiredAmmo = false; // 🏹 토탈워 삼국: 첫 사격을 시작할 때부터 게이지 표시
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

        if (ammoBarRoot == null)
        {
            Transform ab = transform.Find("AmmoBarRoot") ?? transform.Find("AmmoBar");
            if (ab != null)
            {
                ammoBarRoot = ab.gameObject;
                ammoBarImage = ab.Find("AmmoFill")?.GetComponent<Image>() ?? ab.GetComponent<Image>();
            }
        }
    }

    public void Initialize(Squad squad, PlayerController pc)
    {
        targetSquad = squad;
        playerController = pc;

        AutoFindReferences();
        hasFiredAmmo = (squad != null && squad.IsRangedSquad && squad.AmmoRatio < 0.999f);
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

        UpdateAmmoBar();
    }

    /// <summary>
    /// 프리팹 내 구현된 탄약 게이지 바(AmmoBarRoot, AmmoFill)를 바인딩합니다.
    /// </summary>
    private void EnsureAmmoBarUI()
    {
        if (targetSquad == null || !targetSquad.IsRangedSquad)
        {
            if (ammoBarRoot != null && ammoBarRoot.activeSelf) ammoBarRoot.SetActive(false);
            return;
        }

        if (ammoBarRoot == null)
        {
            Transform existing = transform.Find("AmmoBarRoot") ?? transform.Find("AmmoBar");
            if (existing != null)
            {
                ammoBarRoot = existing.gameObject;
                ammoBarImage = existing.Find("AmmoFill")?.GetComponent<Image>() ?? existing.GetComponent<Image>();
            }
            else
            {
                // 🎨 프리팹 누락 시 방어적 자동 복원
                GameObject rootGo = new GameObject("AmmoBarRoot", typeof(RectTransform));
                rootGo.transform.SetParent(transform, false);
                RectTransform rootRect = rootGo.GetComponent<RectTransform>();
                rootRect.anchorMin = new Vector2(0.05f, 0f);
                rootRect.anchorMax = new Vector2(0.95f, 0f);
                rootRect.pivot = new Vector2(0.5f, 0f);
                rootRect.anchoredPosition = new Vector2(0f, 2f);
                rootRect.sizeDelta = new Vector2(0f, 4f);

                Image bgImage = rootGo.AddComponent<Image>();
                bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

                GameObject fillGo = new GameObject("AmmoFill", typeof(RectTransform));
                fillGo.transform.SetParent(rootGo.transform, false);
                RectTransform fillRect = fillGo.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.sizeDelta = Vector2.zero;
                fillRect.anchoredPosition = Vector2.zero;

                ammoBarImage = fillGo.AddComponent<Image>();
                ammoBarImage.type = Image.Type.Filled;
                ammoBarImage.fillMethod = Image.FillMethod.Horizontal;
                ammoBarImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                ammoBarImage.fillAmount = 1f;
                ammoBarImage.color = new Color(1.0f, 0.72f, 0.05f, 0.95f);

                ammoBarRoot = rootGo;
            }
        }
    }

    /// <summary>
    /// 🎯 [토탈워 삼국 스타일] 탄을 쏘기 전(100%)에는 숨겼다가, 첫 사격을 시작한 순간부터 게이지를 표시합니다.
    /// </summary>
    private void UpdateAmmoBar()
    {
        if (targetSquad == null || !targetSquad.IsRangedSquad)
        {
            if (ammoBarRoot != null && ammoBarRoot.activeSelf) ammoBarRoot.SetActive(false);
            return;
        }

        if (ammoBarRoot == null || ammoBarImage == null)
        {
            EnsureAmmoBarUI();
        }

        float ratio = targetSquad.AmmoRatio;

        // 🏹 첫 화살을 쏘는 순간(100% 미만으로 감소) 게이지 표시 활성화!
        if (ratio < 0.999f)
        {
            hasFiredAmmo = true;
        }

        bool shouldShow = hasFiredAmmo;

        if (ammoBarRoot != null && ammoBarRoot.activeSelf != shouldShow)
        {
            ammoBarRoot.SetActive(shouldShow);
        }

        if (shouldShow && ammoBarImage != null)
        {
            ammoBarImage.fillAmount = ratio;

            // 탄약 전량 소진 시 어두운 붉은색, 잔여 시 선명한 골드
            if (ratio <= 0.001f)
            {
                ammoBarImage.color = new Color(0.6f, 0.2f, 0.2f, 0.7f);
            }
            else
            {
                ammoBarImage.color = new Color(1.0f, 0.72f, 0.05f, 0.95f);
            }
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
