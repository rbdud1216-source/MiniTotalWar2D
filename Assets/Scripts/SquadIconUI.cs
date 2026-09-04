using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 월드 상의 Squad 부대 중심 좌표를 Screen 좌표로 추적하여 UI 부대 클릭 버튼을 표시해 주는 클래스입니다.
/// </summary>
public class SquadIconUI : MonoBehaviour, IPointerClickHandler
{
    private Squad targetSquad;
    private Button button;
    private Image iconImage;
    private CanvasGroup canvasGroup;
    private Camera mainCamera;
    private RectTransform rectTransform;
    private PlayerController playerController;

    [Header("UI 위치 오프셋 (스크린 좌표)")]
    public Vector3 offset = new Vector3(0, 40f, 0);

    [Header("부대 조직력(체력) 표시 이미지")]
    [SerializeField] private Image organizationImage;

    private void Awake()
    {
        button = GetComponent<Button>();
        iconImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        mainCamera = Camera.main;

        if (organizationImage == null)
        {
            Transform orgTransform = transform.Find("SquadOrganization");
            if (orgTransform != null)
            {
                organizationImage = orgTransform.GetComponent<Image>();
            }
        }

        if (organizationImage != null)
        {
            organizationImage.type = Image.Type.Filled;
            organizationImage.fillMethod = Image.FillMethod.Vertical;
            organizationImage.fillOrigin = (int)Image.OriginVertical.Bottom; // 아래쪽 기준 (위에서 아래로 줄어듦)
        }

        if (button != null)
        {
            button.onClick.AddListener(OnSquadButtonClicked);
        }
    }

    private void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnSquadButtonClicked);
        }
    }

    public void Initialize(Squad squad)
    {
        targetSquad = squad;
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
        if (organizationImage == null)
        {
            Transform orgTransform = transform.Find("SquadOrganization");
            if (orgTransform != null)
            {
                organizationImage = orgTransform.GetComponent<Image>();
            }
        }
        if (organizationImage != null)
        {
            organizationImage.type = Image.Type.Filled;
            organizationImage.fillMethod = Image.FillMethod.Vertical;
            organizationImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            organizationImage.fillAmount = 1f;
        }
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        // 배경 아이콘은 고급스러운 다크 반투명 프레임으로 유지
        if (iconImage != null)
        {
            iconImage.color = new Color(0.12f, 0.12f, 0.14f, 0.75f);
        }

        // 내부 조직력(SquadOrganization) 게이지에 아군(푸른색) / 적군(붉은색) 색상 적용
        if (organizationImage != null && targetSquad != null && targetSquad.members.Count > 0 && targetSquad.members[0] != null)
        {
            bool isEnemy = !targetSquad.members[0].isPlayer;
            organizationImage.color = isEnemy 
                ? new Color(0.95f, 0.25f, 0.25f, 0.95f)  // 적군: 선명한 레드
                : new Color(0.22f, 0.65f, 1.0f, 0.95f);  // 아군: 선명한 블루
        }
    }

    public Squad GetSquad()
    {
        return targetSquad;
    }

    private void LateUpdate()
    {
        UpdateIconPosition();
        UpdateOrganizationBar();
    }

    private void UpdateOrganizationBar()
    {
        if (organizationImage == null || targetSquad == null) return;

        float total = (targetSquad.initialUnitCount > 0) ? targetSquad.initialUnitCount : Mathf.Max(1, targetSquad.members.Count);
        float current = targetSquad.MemberCount;

        float fillRatio = Mathf.Clamp01(current / total);
        organizationImage.fillAmount = fillRatio;

        // 선택 여부에 따른 동적 색상 업데이트 (선택 시: 유닛과 동일한 Cyan, 미선택 시: 기본 Blue, 적군: Red)
        bool isEnemy = !targetSquad.isPlayer;
        if (isEnemy)
        {
            organizationImage.color = new Color(0.95f, 0.25f, 0.25f, 0.95f); // 적군: 선명한 레드
        }
        else
        {
            bool isSelected = (playerController != null) && playerController.IsSquadSelected(targetSquad);
            organizationImage.color = isSelected
                ? new Color(0.15f, 0.92f, 1.0f, 0.98f) // 선택 시: 유닛과 동일한 밝은 Cyan (형광 청록)
                : new Color(0.18f, 0.50f, 0.95f, 0.92f); // 미선택 시: 유닛과 동일한 기본 Blue
        }
    }

    private void UpdateIconPosition()
    {
        // 부대가 없거나 전멸한 경우 아이콘 숨김
        if (targetSquad == null)
        {
            SetUIVisible(false);
            return;
        }

        int livingCount = targetSquad.MemberCount;
        if (livingCount <= 0)
        {
            SetUIVisible(false);
            return;
        }

        // 부대 전체 살아있는 유닛들의 기하학적 정중앙(VisualCenter)을 추적하여 방진 머리 위에 정확히 표시
        Vector3 realCenter = targetSquad.GetVisualCenter();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindAnyObjectByType<Camera>();
            }
        }

        if (mainCamera != null && rectTransform != null)
        {
            Vector3 screenPos = mainCamera.WorldToScreenPoint(realCenter);

            // 화면 안에 있는지 체크 (카메라 앞쪽이고 스크린 영역 내)
            bool isVisible = screenPos.z > 0 && 
                             screenPos.x >= -100f && screenPos.x <= Screen.width + 100f && 
                             screenPos.y >= -100f && screenPos.y <= Screen.height + 100f;

            SetUIVisible(isVisible);

            if (isVisible)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    rectTransform.position = screenPos + offset;
                }
                else if (canvas != null)
                {
                    RectTransform parentRect = rectTransform.parent as RectTransform;
                    if (parentRect != null)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            parentRect, screenPos, canvas.worldCamera ?? mainCamera, out Vector2 localPoint);
                        rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0) + offset;
                    }
                }
            }
        }
    }

    private void SetUIVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
        else
        {
            if (iconImage != null) iconImage.enabled = visible;
            if (button != null) button.enabled = visible;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetSquad == null) return;
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();

        bool isEnemy = !targetSquad.isPlayer;

        // ⚔️ 마우스 우클릭 시: 적 부대 아이콘이면 즉시 일제 돌격 명령 발동!
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (isEnemy && playerController != null)
            {
                Debug.Log($"<color=#00FFFF><b>[SquadIconUI] ⚔️ 적 부대({targetSquad.name}) 아이콘 [우클릭] -> 즉시 일제 돌격 명령 하달!</b></color>");
                playerController.CommandAttackTargetSquad(targetSquad);
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnSquadButtonClicked();
        }
    }

    private void OnSquadButtonClicked()
    {
        if (targetSquad == null) return;

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        bool isEnemy = !targetSquad.isPlayer;

        if (isEnemy)
        {
            // 적 부대 아이콘 좌클릭 시: 선택된 아군 부대/유닛이 해당 적 부대를 향해 돌격 및 교전 명령 실행
            Debug.Log($"[SquadIconUI] ⚔️ 적 부대({targetSquad.name}) 아이콘 좌클릭 -> 돌격 공격 명령 하달!");
            if (playerController != null)
            {
                playerController.CommandAttackTargetSquad(targetSquad);
            }
        }
        else
        {
            // 아군 부대 아이콘 클릭 시: 부대 선택
            Debug.Log($"[SquadIconUI] 🛡️ 아군 부대({targetSquad.name}) 아이콘 클릭 -> 부대 선택");
            if (playerController != null)
            {
                bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                playerController.SelectSquadDirectly(targetSquad, isShiftPressed);
            }
        }
    }
}