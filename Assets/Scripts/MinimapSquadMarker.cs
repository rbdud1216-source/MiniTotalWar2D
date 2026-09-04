using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니맵 상에 개별 부대의 위치, 전열 폭(Columns), 정면 방향 및 선택 상태를 실시간 시각화하는 마커 컴포넌트입니다.
/// </summary>
public class MinimapSquadMarker : MonoBehaviour
{
    private Squad targetSquad;
    private MinimapManager minimapManager;
    private RectTransform rectTransform;

    private Image mainBarImage;
    private Image directionArrowImage;
    private Image selectionBorderImage;

    private static readonly Color PlayerColor = new Color(0.12f, 0.82f, 1f, 0.95f);       // 아군: 선명한 청록/블루
    private static readonly Color EnemyColor = new Color(0.96f, 0.24f, 0.24f, 0.95f);      // 적군: 선명한 레드
    private static readonly Color SelectionColor = new Color(1f, 0.88f, 0.2f, 1f);          // 선택: 황금색 하이라이트

    public Squad TargetSquad => targetSquad;

    public void Initialize(Squad squad, MinimapManager manager)
    {
        targetSquad = squad;
        minimapManager = manager;
        rectTransform = GetComponent<RectTransform>();

        BuildMarkerVisuals();
        UpdateMarkerVisuals();
    }

    private void BuildMarkerVisuals()
    {
        rectTransform.sizeDelta = new Vector2(16f, 6f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // 1. 선택 테두리 (Selection Border)
        GameObject borderObj = new GameObject("SelectionBorder", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(transform, false);
        selectionBorderImage = borderObj.GetComponent<Image>();
        selectionBorderImage.color = SelectionColor;
        selectionBorderImage.raycastTarget = false;
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = new Vector2(4f, 4f);
        borderRect.anchoredPosition = Vector2.zero;
        borderObj.SetActive(false);

        // 2. 메인 전열 바 (Main Formation Bar)
        GameObject barObj = new GameObject("MainBar", typeof(RectTransform), typeof(Image));
        barObj.transform.SetParent(transform, false);
        mainBarImage = barObj.GetComponent<Image>();
        mainBarImage.color = (targetSquad != null && targetSquad.isPlayer) ? PlayerColor : EnemyColor;
        mainBarImage.raycastTarget = false;
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = Vector2.one;
        barRect.sizeDelta = Vector2.zero;
        barRect.anchoredPosition = Vector2.zero;

        // 3. 정면 방향 핍 (Direction Indicator Pip)
        GameObject dirObj = new GameObject("DirPointer", typeof(RectTransform), typeof(Image));
        dirObj.transform.SetParent(transform, false);
        directionArrowImage = dirObj.GetComponent<Image>();
        directionArrowImage.color = Color.white;
        directionArrowImage.raycastTarget = false;
        RectTransform dirRect = dirObj.GetComponent<RectTransform>();
        dirRect.sizeDelta = new Vector2(4f, 3f);
        dirRect.anchoredPosition = new Vector2(0f, 4.5f); // 전방(위쪽)에 배치
    }

    private void Update()
    {
        if (targetSquad == null)
        {
            Destroy(gameObject);
            return;
        }

        int livingCount = targetSquad.MemberCount;
        if (livingCount <= 0)
        {
            Destroy(gameObject);
            return;
        }

        UpdateMarkerVisuals();
    }

    private void UpdateMarkerVisuals()
    {
        if (minimapManager == null || targetSquad == null) return;

        // 1. 위치 동기화 (부대의 시각적 중심 좌표)
        Vector3 worldPos = targetSquad.GetVisualCenter();
        rectTransform.anchoredPosition = minimapManager.WorldToMinimapLocal(worldPos);

        // 2. 회전 동기화 (3D 월드 Y축 회전 -> UI 2D Z축 회전)
        float squadYaw = targetSquad.transform.eulerAngles.y;
        rectTransform.localEulerAngles = new Vector3(0f, 0f, -squadYaw);

        // 3. 전열 폭(Columns)에 비례한 가로폭 조절
        int cols = Mathf.Max(1, targetSquad.currentColumns);
        float width = Mathf.Clamp(cols * 1.6f, 10f, 32f);
        rectTransform.sizeDelta = new Vector2(width, 5.5f);

        // 4. 선택 상태 하이라이트 동기화
        bool isSelected = targetSquad.isSelected;
        if (selectionBorderImage != null && selectionBorderImage.gameObject.activeSelf != isSelected)
        {
            selectionBorderImage.gameObject.SetActive(isSelected);
        }
    }
}
