using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 좌측 하단에 전체 전장을 한눈에 조망할 수 있는 정사각형 전술 미니맵(Tactical Minimap)의 총괄 관리자입니다.
/// </summary>
public class MinimapManager : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private static MinimapManager _instance;
    public static MinimapManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<MinimapManager>();
                if (_instance == null)
                {
                    Canvas canvas = FindAnyObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        GameObject go = new GameObject("TacticalMinimap", typeof(RectTransform));
                        go.transform.SetParent(canvas.transform, false);
                        _instance = go.AddComponent<MinimapManager>();
                    }
                }
            }
            return _instance;
        }
    }

    [Header("전장 월드 경계 (World Bounds X-Z, 중심 = 0,0)")]
    [SerializeField] private Vector2 worldMinXZ = new Vector2(-500f, -500f);
    [SerializeField] private Vector2 worldMaxXZ = new Vector2(500f, 500f);

    [Header("UI 계층 참조")]
    [SerializeField] private RectTransform minimapRect;
    [SerializeField] private RectTransform markerContainer;
    [SerializeField] private MinimapCameraFrustum cameraFrustum;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderFrameImage;

    private readonly Dictionary<Squad, MinimapSquadMarker> activeSquadMarkers = new Dictionary<Squad, MinimapSquadMarker>();
    private CameraController cameraController;
    private PlayerController playerController;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        AutoDetectTerrainBounds();
        InitializeMinimapUI();
    }

    private void Start()
    {
        cameraController = FindAnyObjectByType<CameraController>();
        playerController = FindAnyObjectByType<PlayerController>();

        AutoDetectTerrainBounds();

        if (cameraFrustum != null)
        {
            cameraFrustum.Initialize(this);
        }

        // 씬 내 이미 존재하는 모든 부대 등록
        Squad[] existingSquads = FindObjectsByType<Squad>(FindObjectsSortMode.None);
        foreach (Squad squad in existingSquads)
        {
            RegisterSquad(squad);
        }
    }

    /// <summary>
    /// 씬 내 Terrain을 자동 감지하여 실제 지형의 월드 위치와 크기를 1:1로 일치시킵니다.
    /// </summary>
    [ContextMenu("Auto Detect Terrain Bounds")]
    public void AutoDetectTerrainBounds()
    {
        Terrain terrain = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;

            worldMinXZ = new Vector2(tPos.x, tPos.z);
            worldMaxXZ = new Vector2(tPos.x + tSize.x, tPos.z + tSize.z);
        }
        else
        {
            if (cameraController == null) cameraController = FindAnyObjectByType<CameraController>();
            if (cameraController != null)
            {
                worldMinXZ = cameraController.MinXZ;
                worldMaxXZ = cameraController.MaxXZ;
            }
            else
            {
                worldMinXZ = new Vector2(-500f, -500f);
                worldMaxXZ = new Vector2(500f, 500f);
            }
        }
    }

    /// <summary>
    /// 인스펙터 또는 씬에 미니맵 UI 계층이 없을 경우, 런타임에 안전하게 자동 생성합니다.
    /// 하이어라키에 이미 설정된 RectTransform 크기/위치는 100% 보존합니다.
    /// </summary>
    private void InitializeMinimapUI()
    {
        if (minimapRect == null) minimapRect = GetComponent<RectTransform>();

        // 1. 루트 RectTransform 설정 (하이어라키에 설정된 크기를 존중)
        if (minimapRect == null)
        {
            minimapRect = gameObject.AddComponent<RectTransform>();
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = FindAnyObjectByType<Canvas>();
            if (parentCanvas != null)
            {
                transform.SetParent(parentCanvas.transform, false);
            }
        }

        // 크기가 아예 지정되지 않은 신규 생성 시에만 기본 배치 설정
        if (minimapRect.sizeDelta == Vector2.zero && minimapRect.rect.width <= 1f)
        {
            minimapRect.anchorMin = new Vector2(0f, 0f);
            minimapRect.anchorMax = new Vector2(0f, 0f);
            minimapRect.pivot = new Vector2(0f, 0f);
            minimapRect.anchoredPosition = new Vector2(18f, 18f);
            minimapRect.sizeDelta = new Vector2(210f, 210f);
        }

        // 2. 미니맵 배경 이미지 및 마스크 설정
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.11f, 0.15f, 0.94f); // 짙은 다크 슬레이트 톤
            backgroundImage.raycastTarget = true;
        }

        RectMask2D mask = GetComponent<RectMask2D>();
        if (mask == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }

        // 3. 카메라 Frustum 레이어
        if (cameraFrustum == null)
        {
            Transform frustumTr = transform.Find("CameraFrustum");
            if (frustumTr == null)
            {
                GameObject frustumObj = new GameObject("CameraFrustum", typeof(RectTransform));
                frustumObj.transform.SetParent(transform, false);
                frustumTr = frustumObj.transform;
            }
            cameraFrustum = frustumTr.GetComponent<MinimapCameraFrustum>();
            if (cameraFrustum == null) cameraFrustum = frustumTr.gameObject.AddComponent<MinimapCameraFrustum>();

            RectTransform frustumRect = frustumTr.GetComponent<RectTransform>();
            frustumRect.anchorMin = Vector2.zero;
            frustumRect.anchorMax = Vector2.one;
            frustumRect.pivot = new Vector2(0.5f, 0.5f);
            frustumRect.sizeDelta = Vector2.zero;
            frustumRect.anchoredPosition = Vector2.zero;
        }

        // 4. 부대 마커 컨테이너
        if (markerContainer == null)
        {
            Transform markerTr = transform.Find("MarkerContainer");
            if (markerTr == null)
            {
                GameObject markerObj = new GameObject("MarkerContainer", typeof(RectTransform));
                markerObj.transform.SetParent(transform, false);
                markerTr = markerObj.transform;
            }
            markerContainer = markerTr.GetComponent<RectTransform>();
            markerContainer.anchorMin = Vector2.zero;
            markerContainer.anchorMax = Vector2.one;
            markerContainer.pivot = new Vector2(0.5f, 0.5f);
            markerContainer.sizeDelta = Vector2.zero;
            markerContainer.anchoredPosition = Vector2.zero;
        }

        // 5. 세련된 토탈워 스타일 외곽 테두리 프레임
        if (borderFrameImage == null)
        {
            Transform borderTr = transform.Find("BorderFrame");
            if (borderTr == null)
            {
                GameObject borderObj = new GameObject("BorderFrame", typeof(RectTransform), typeof(Image));
                borderObj.transform.SetParent(transform, false);
                borderTr = borderObj.transform;
            }
            borderFrameImage = borderTr.GetComponent<Image>();
            borderFrameImage.color = new Color(0.42f, 0.48f, 0.55f, 0.95f); // 고급스러운 청회색 황동 프레임
            borderFrameImage.raycastTarget = false;
            borderFrameImage.maskable = false;

            RectTransform borderRect = borderTr.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(4f, 4f); // 2px 두께 외곽선
            borderRect.anchoredPosition = Vector2.zero;
            borderTr.SetAsLastSibling();
        }
    }

    #region 좌표 변환 수학 (Coordinate Transformation)

    /// <summary>
    /// 3D 월드 좌표(X-Z)를 미니맵 중심(0, 0) 기준 로컬 RectTransform 좌표로 변환합니다.
    /// (미니맵의 중심은 월드 X,Z = 0, 0에 정확히 대응)
    /// </summary>
    public Vector2 WorldToMinimapLocal(Vector3 worldPos)
    {
        float u = Mathf.InverseLerp(worldMinXZ.x, worldMaxXZ.x, worldPos.x);
        float v = Mathf.InverseLerp(worldMinXZ.y, worldMaxXZ.y, worldPos.z);

        Rect rect = minimapRect.rect;
        float w = rect.width;
        float h = rect.height;

        // 미니맵 중심(0, 0)을 원점으로 하는 로컬 좌표 반환 (-w/2 ~ +w/2, -h/2 ~ +h/2)
        float localX = (u - 0.5f) * w;
        float localY = (v - 0.5f) * h;

        return new Vector2(localX, localY);
    }

    /// <summary>
    /// 미니맵 중심(0, 0) 기준 로컬 좌표를 3D 월드 좌표(Y = 0)로 역변환합니다.
    /// </summary>
    public Vector3 MinimapLocalToWorld(Vector2 localPos)
    {
        Rect rect = minimapRect.rect;
        float w = rect.width;
        float h = rect.height;

        float u = (w > 0f) ? ((localPos.x / w) + 0.5f) : 0.5f;
        float v = (h > 0f) ? ((localPos.y / h) + 0.5f) : 0.5f;

        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        float worldX = Mathf.Lerp(worldMinXZ.x, worldMaxXZ.x, u);
        float worldZ = Mathf.Lerp(worldMinXZ.y, worldMaxXZ.y, v);

        return new Vector3(worldX, 0f, worldZ);
    }

    /// <summary>
    /// 스크린 마우스 클릭 좌표를 3D 월드 좌표(Y = 0)로 변환합니다.
    /// 피벗 및 앵커 설정과 무관하게 Rect 영역 내에서 정밀한 0.0 ~ 1.0 정규화 비율을 계산합니다.
    /// </summary>
    public bool ScreenPointToWorldPosition(Vector2 screenPoint, Camera eventCamera, out Vector3 worldPos)
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Camera camToUse = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                          ? (eventCamera ?? parentCanvas.worldCamera)
                          : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(minimapRect, screenPoint, camToUse, out Vector2 localPoint))
        {
            Rect rect = minimapRect.rect;
            float u = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
            float v = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            float worldX = Mathf.Lerp(worldMinXZ.x, worldMaxXZ.x, u);
            float worldZ = Mathf.Lerp(worldMinXZ.y, worldMaxXZ.y, v);

            worldPos = new Vector3(worldX, 0f, worldZ);
            return true;
        }

        worldPos = Vector3.zero;
        return false;
    }

    #endregion

    #region 부대 등록 및 해제 (Squad Registration)

    public void RegisterSquad(Squad squad)
    {
        if (squad == null || activeSquadMarkers.ContainsKey(squad)) return;

        if (markerContainer == null)
        {
            InitializeMinimapUI();
        }

        GameObject markerObj = new GameObject($"Marker_{squad.name}", typeof(RectTransform));
        markerObj.transform.SetParent(markerContainer, false);

        MinimapSquadMarker marker = markerObj.AddComponent<MinimapSquadMarker>();
        marker.Initialize(squad, this);

        activeSquadMarkers[squad] = marker;
    }

    public void UnregisterSquad(Squad squad)
    {
        if (squad == null) return;

        if (activeSquadMarkers.TryGetValue(squad, out MinimapSquadMarker marker))
        {
            activeSquadMarkers.Remove(squad);
            if (marker != null)
            {
                Destroy(marker.gameObject);
            }
        }
    }

    #endregion

    #region 마우스 인터랙션 (Mouse Interaction & Commands)

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            HandleCameraPan(eventData);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            HandleSquadCommand(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            HandleCameraPan(eventData);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 드래그 종료
    }

    private void HandleCameraPan(PointerEventData eventData)
    {
        if (ScreenPointToWorldPosition(eventData.position, eventData.pressEventCamera, out Vector3 worldPos))
        {
            if (cameraController == null) cameraController = FindAnyObjectByType<CameraController>();
            if (cameraController != null)
            {
                cameraController.PanToWorldXZ(new Vector2(worldPos.x, worldPos.z), smooth: false);
            }
        }
    }

    private void HandleSquadCommand(PointerEventData eventData)
    {
        if (ScreenPointToWorldPosition(eventData.position, eventData.pressEventCamera, out Vector3 worldPos))
        {
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                bool isAttackMove = Input.GetKey(KeyCode.A);
                playerController.ExecuteMinimapCommand(worldPos, isAttackMove);
            }
        }
    }

    #endregion
}
