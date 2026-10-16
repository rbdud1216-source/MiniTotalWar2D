using System.Collections;
using UnityEngine;

/// <summary>
/// RTS/FPS(FlyCam) 스타일의 카메라 이동(Q/E 높이 조절 포함), 회전, Zoom, 맵 경계 제한 및 포커싱 제어 클래스입니다.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("이동 및 회전 속도 설정")]
    [KoreanLabel("기본 이동 속도", "카메라의 기본 수평/수직 이동 속도입니다. (단위: m/s, 기본: 40m/s)")]
    [SerializeField] private float moveSpeed = 40f;

    [KoreanLabel("고속 이동 가속 배율", "LeftShift 키를 눌렀을 때의 가속 배율입니다. (단위: 배, 기본: 2.0배)")]
    [SerializeField] private float fastMoveMultiplier = 2f;

    [KoreanLabel("줌 속도", "마우스 휠 스크롤 줌 인/아웃 속도입니다. (기본: 20.0)")]
    [SerializeField] private float zoomSpeed = 20f;

    [KoreanLabel("시점 회전 속도", "마우스 휠 클릭 드래그 시 3D 시점 회전 속도입니다. (기본: 100.0)")]
    [SerializeField] private float rotationSpeed = 100f;

    [Header("시야 높이(Zoom) 제한 - 3D 전장 시점")]
    [KoreanLabel("3D 최소 시야 높이", "3D 모드에서 카메라가 지면으로 내려갈 수 있는 최소 고도입니다. (단위: m, 기본: 5m)")]
    [SerializeField] private float minHeight3D = 5f;

    [KoreanLabel("3D 최대 시야 높이", "3D 모드에서 카메라가 하늘로 올라갈 수 있는 최대 고도입니다. (단위: m, 기본: 150m)")]
    [SerializeField] private float maxHeight3D = 150f;

    [Header("시점 회전(피치) 각도 제한 - 3D 전장 시점")]
    [KoreanLabel("최소 상향 각도 (피치)", "최소 상향 각도입니다. 음수면 위쪽 하늘이나 언덕 고지대를 올려다볼 수 있습니다. (단위: 도, 기본: -45도)")]
    [SerializeField] private float minPitch = -45f;

    [KoreanLabel("최대 하향 각도 (피치)", "최대 하향 각도입니다. 양수면 지면을 수직으로 내려다봅니다. (단위: 도, 기본: 88도)")]
    [SerializeField] private float maxPitch = 88f;

    [Header("시야 높이(Zoom) 제한 - 2D 전술지도(Tactical Map) 시점")]
    [KoreanLabel("2D 전술지도 최소 높이", "2D 탑다운 전술지도 모드에서의 최소 고도입니다. (단위: m, 기본: 20m)")]
    [SerializeField] private float tacticalMinHeight = 20f;

    [KoreanLabel("2D 전술지도 최대 높이", "2D 탑다운 전술지도 모드에서의 최대 고도입니다. (단위: m, 기본: 350m)")]
    [SerializeField] private float tacticalMaxHeight = 350f;

    [KoreanLabel("2D 전술지도 기본 높이", "2D 모드 전환 시 기본으로 복원되는 고도입니다. (단위: m, 기본: 130m)")]
    [SerializeField] private float tacticalDefaultHeight = 130f;

    public float MinHeight => isTopDownMode ? tacticalMinHeight : minHeight3D;
    public float MaxHeight => isTopDownMode ? tacticalMaxHeight : maxHeight3D;

    [Header("이동 제한 영역 (평면 X-Z 및 수직 상하 Y축)")]
    [KoreanLabel("이동 제한 영역 사용", "체크 시 카메라가 아래 지정된 X, Z 평면 및 Y 수직 상하 경계 영역을 벗어나지 못하도록 제한합니다.")]
    [SerializeField] private bool useBounds = true;

    [KoreanLabel("수직 상하(Y축) 제한 적용", "체크 시 minY, maxY로 카메라의 상하 고도를 제한합니다.")]
    [SerializeField] private bool useHeightBounds = true;

    [KoreanLabel("수직 상하 최소 높이 (Y축)", "카메라의 지면 최저 고도 한계입니다. (단위: m, 기본: 5m)")]
    [SerializeField] private float minY = 5f;

    [KoreanLabel("수직 상하 최대 높이 (Y축)", "카메라의 상공 최고 고도 한계입니다. (단위: m, 기본: 300m)")]
    [SerializeField] private float maxY = 300f;

    [KoreanLabel("평면 최소 경계 (서쪽 X / 남쪽 Z)", "평면 이동 최소 경계 (X: 서쪽 좌측 한계, Y: 남쪽 후방 한계)입니다. (단위: m)")]
    [SerializeField] private Vector2 minXZ = new Vector2(-450f, -450f);

    [KoreanLabel("평면 최대 경계 (동쪽 X / 북쪽 Z)", "평면 이동 최대 경계 (X: 동쪽 우측 한계, Y: 북쪽 전방 한계)입니다. (단위: m)")]
    [SerializeField] private Vector2 maxXZ = new Vector2(450f, 450f);

    [Header("화면 모서리 마우스 이동 (Screen Edge Pan)")]
    [KoreanLabel("화면 모서리 마우스 이동 활성화", "마우스 커서를 화면 가장자리로 가져가면 카메라가 해당 방향으로 스크롤 이동합니다.")]
    [SerializeField] private bool enableEdgePanning = true;
    [KoreanLabel("화면 가장자리 감지 영역 (px)", "마우스 화면 모서리 감지 픽셀 두께입니다. (단위: px, 기본: 15px)")]
    [SerializeField] private float edgePanBorder = 15f;

    [Header("터레인 크기 자동 감지 (Auto Bounds)")]
    [KoreanLabel("시작 시 터레인 크기 자동 감지", "체크 시 게임 시작할 때 활성 터레인의 크기와 위치를 자동 감지하여 카메라 경계를 덮어씁니다. (기본: 해제 - 인스펙터 경계 우선)")]
    [SerializeField] private bool autoDetectBoundsOnStart = false;

    public bool UseBounds => useBounds;
    public bool UseHeightBounds => useHeightBounds;
    public float MinY => minY;
    public float MaxY => maxY;
    public Vector2 MinXZ => minXZ;
    public Vector2 MaxXZ => maxXZ;

    [Header("부대 포커스(더블클릭) 설정")]
    [KoreanLabel("기본 포커스 거리", "부대 더블클릭 시 카메라가 접근하는 목표 거리입니다. (단위: m, 기본: 10m)")]
    [SerializeField] private float defaultFocusDistance = 10f;

    [KoreanLabel("포커싱 이동 시간", "포커스 시 카메라가 부드럽게 이동하는 시간입니다. (단위: 초, 기본: 0.25초)")]
    [SerializeField] private float focusDuration = 0.25f;

    [Header("2D / 3D 뷰 모드 설정")]
    [KoreanLabel("시점 전환 토글 키", "3D 자유 시점과 2D 전술지도 시점을 전환하는 단축키입니다. (기본: Tab)")]
    [SerializeField] private KeyCode toggleViewKey = KeyCode.Tab;
    private bool isTopDownMode = false;

    private Vector3 last3DRotation = new Vector3(50f, 0f, 0f);
    private float last3DHeight = 30f;
    private float lastTacticalHeight = 130f;

    private Camera cam;
    private Coroutine focusCoroutine;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        // 터레인이 있고 옵션이 켜져 있을 때만 자동 경계 감지 (기본은 인스펙터 설정값 우선 보존)
        if (autoDetectBoundsOnStart)
        {
            AutoDetectTerrainBounds();
        }

        last3DRotation = transform.eulerAngles;
        last3DHeight = Mathf.Clamp(transform.position.y, minHeight3D, maxHeight3D);
        lastTacticalHeight = tacticalDefaultHeight;
    }

    private void Update()
    {
        HandleViewToggle();
        HandleMovement();
        HandlePanOrRotate();
        HandleZoom();
        ClampPosition();
    }

    /// <summary>
    /// 지정한 월드 좌표(유닛/부대 중심 위치)로 시점을 부드럽게 포커스하며 확대합니다.
    /// </summary>
    public void FocusOnPosition(Vector3 targetPosition, float targetDistance = -1f, bool smooth = true)
    {
        if (targetDistance <= 0f) targetDistance = defaultFocusDistance;

        StopFocusCoroutine();

        Vector3 camForward = isTopDownMode ? Vector3.down : transform.forward;
        Vector3 targetCamPos = targetPosition - (camForward * targetDistance);

        float currentMinH = isTopDownMode ? tacticalMinHeight : minHeight3D;
        float currentMaxH = isTopDownMode ? tacticalMaxHeight : maxHeight3D;
        if (useBounds && useHeightBounds)
        {
            currentMinH = Mathf.Max(currentMinH, minY);
            currentMaxH = Mathf.Min(currentMaxH, maxY);
        }
        targetCamPos.y = Mathf.Clamp(targetCamPos.y, currentMinH, currentMaxH);

        if (useBounds)
        {
            targetCamPos.x = Mathf.Clamp(targetCamPos.x, minXZ.x, maxXZ.x);
            targetCamPos.z = Mathf.Clamp(targetCamPos.z, minXZ.y, maxXZ.y);
        }

        if (smooth && focusDuration > 0f)
        {
            focusCoroutine = StartCoroutine(Co_FocusOn(targetCamPos));
        }
        else
        {
            transform.position = targetCamPos;
        }
    }

    /// <summary>
    /// 미니맵 클릭/드래그 시 현재 카메라 높이와 회전을 유지하며 지정한 지상 XZ 좌표가 화면 중심에 오도록 카메라를 이동합니다.
    /// </summary>
    public void PanToWorldXZ(Vector2 targetXZ, bool smooth = false)
    {
        StopFocusCoroutine();

        Vector3 camForward = isTopDownMode ? Vector3.down : transform.forward;
        float currentHeight = transform.position.y;
        float distToGround = (camForward.y < -0.01f) ? (currentHeight / -camForward.y) : 35f;

        Vector3 targetGround = new Vector3(targetXZ.x, 0f, targetXZ.y);
        Vector3 targetCamPos = targetGround - (camForward * distToGround);
        targetCamPos.y = currentHeight;

        if (useBounds && useHeightBounds)
        {
            targetCamPos.y = Mathf.Clamp(targetCamPos.y, minY, maxY);
        }

        if (useBounds)
        {
            targetCamPos.x = Mathf.Clamp(targetCamPos.x, minXZ.x, maxXZ.x);
            targetCamPos.z = Mathf.Clamp(targetCamPos.z, minXZ.y, maxXZ.y);
        }

        if (smooth && focusDuration > 0f)
        {
            focusCoroutine = StartCoroutine(Co_FocusOn(targetCamPos));
        }
        else
        {
            transform.position = targetCamPos;
        }
    }

    private IEnumerator Co_FocusOn(Vector3 targetCamPos)
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < focusDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / focusDuration);
            float smoothT = 1f - Mathf.Pow(1f - t, 3);

            transform.position = Vector3.Lerp(startPos, targetCamPos, smoothT);
            yield return null;
        }

        transform.position = targetCamPos;
        focusCoroutine = null;
    }

    private void StopFocusCoroutine()
    {
        if (focusCoroutine != null)
        {
            StopCoroutine(focusCoroutine);
            focusCoroutine = null;
        }
    }

    private void HandleViewToggle()
    {
        if (Input.GetKeyDown(toggleViewKey))
        {
            StopFocusCoroutine();
            isTopDownMode = !isTopDownMode;

            if (isTopDownMode)
            {
                // 3D 상태 저장
                last3DRotation = transform.eulerAngles;
                last3DHeight = transform.position.y;

                // 2D 전술지도(Tactical Map) 모드로 전환 (완전 수직 90도 및 전술지도 최적 고도 복원)
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Vector3 pos = transform.position;
                pos.y = Mathf.Clamp(lastTacticalHeight, tacticalMinHeight, tacticalMaxHeight);
                transform.position = pos;
            }
            else
            {
                // 2D 전술지도 상태 저장
                lastTacticalHeight = transform.position.y;

                // 3D 자유 시점으로 복귀 (이전 3D 회전각 및 3D 높이 복원)
                transform.rotation = Quaternion.Euler(last3DRotation.x, last3DRotation.y, 0f);
                Vector3 pos = transform.position;
                pos.y = Mathf.Clamp(last3DHeight, minHeight3D, maxHeight3D);
                transform.position = pos;
            }
        }
    }

    /// <summary>
    /// WASD(평면 이동), 화면 모서리 마우스 이동(Edge Pan) 및 Q/E(높이 조절) 키 입력 제어를 처리합니다.
    /// </summary>
    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 🖱️ 화면 모서리 마우스 이동 (Screen Edge Pan)
        if (enableEdgePanning && !Input.GetMouseButton(2))
        {
            Vector3 mousePos = Input.mousePosition;
            if (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height)
            {
                if (mousePos.x <= edgePanBorder) h -= 1f;
                else if (mousePos.x >= Screen.width - edgePanBorder) h += 1f;

                if (mousePos.y <= edgePanBorder) v -= 1f;
                else if (mousePos.y >= Screen.height - edgePanBorder) v += 1f;
            }
        }

        h = Mathf.Clamp(h, -1f, 1f);
        v = Mathf.Clamp(v, -1f, 1f);

        // [수정] Q/E 입력 감지 (Q: Y축 상승(+1), E: Y축 하강(-1))
        float upDown = 0f;
        if (Input.GetKey(KeyCode.Q)) upDown += 1f;
        if (Input.GetKey(KeyCode.E)) upDown -= 1f;

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f || Mathf.Abs(upDown) > 0.01f)
        {
            StopFocusCoroutine();
        }

        // 고도에 비례한 이동 속도 보정 (2D 고고도에서도 답답하지 않게 쾌적한 이동)
        float heightSpeedMultiplier = Mathf.Clamp(transform.position.y / 30f, 0.8f, 3.0f);
        float currentSpeed = moveSpeed * (isTopDownMode ? heightSpeedMultiplier : 1f);
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= fastMoveMultiplier;
        }

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        if (isTopDownMode)
        {
            forward = Vector3.forward;
            right = Vector3.right;
        }
        else
        {
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude > 0.001f) forward.Normalize();
            else forward = Vector3.forward; // 수직 시점 시 전방 폴백
            if (right.sqrMagnitude > 0.001f) right.Normalize();
            else right = Vector3.right;
        }

        // [수정] WASD 평면 이동 + Q/E 수직 Y축 이동 조합
        Vector3 moveDirection = (forward * v + right * h).normalized;
        Vector3 verticalDirection = Vector3.up * upDown;

        // 🛑 [중요]: Time.deltaTime -> Time.unscaledDeltaTime 변경하여 일시정지(Pause) 중에도 카메라 이동 100% 보장!
        float dt = Time.unscaledDeltaTime;
        if (dt > 0.1f) dt = 0.1f; // 프레임 드랍 시 폭주 방지
        transform.position += (moveDirection + verticalDirection) * (currentSpeed * dt);
    }

    /// <summary>
    /// 마우스 휠 클릭 드래그 시 2D 모드에서는 화면 이동(Pan), 3D 모드에서는 시점 회전을 처리합니다.
    /// </summary>
    private void HandlePanOrRotate()
    {
        if (Input.GetMouseButton(2))
        {
            StopFocusCoroutine();

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (isTopDownMode)
            {
                // [2D 전술지도 탑다운 모드] 마우스 드래그로 화면 끌어당기기 (Pan)
                // 감도를 적절히 맞추기 위해 높이(y)에 비례해서 이동량을 보정합니다.
                float panSensitivity = transform.position.y * 0.05f;

                // 마우스를 끈 반대 방향으로 카메라가 이동해야 바닥을 '잡고 끄는' 느낌이 납니다.
                Vector3 moveDelta = new Vector3(-mouseX, 0f, -mouseY) * panSensitivity;

                transform.Translate(moveDelta, Space.World);
            }
            else
            {
                // [3D 자유 뷰 모드] 기존처럼 휠 드래그 시 시점 회전
                float rotX = mouseX * rotationSpeed * 0.05f;
                float rotY = mouseY * rotationSpeed * 0.05f;

                // Y축 회전 (좌우)
                transform.Rotate(Vector3.up, rotX, Space.World);

                // X축 회전 (상하 피치 제한: 음수 각도로 위쪽 언덕과 하늘을 시원하게 올려다봄)
                float newXAngle = transform.eulerAngles.x - rotY;
                if (newXAngle > 180f) newXAngle -= 360f;
                newXAngle = Mathf.Clamp(newXAngle, minPitch, maxPitch);

                Vector3 currentEuler = transform.eulerAngles;
                transform.eulerAngles = new Vector3(newXAngle, currentEuler.y, 0f);
            }
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            StopFocusCoroutine();

            // 고도(Y)에 비례하여 고고도에서도 부드럽고 시원하게 줌 인/아웃되도록 보정
            float heightFactor = Mathf.Clamp(transform.position.y / 30f, 0.6f, 3.5f);
            Vector3 zoomDir = isTopDownMode ? Vector3.down : transform.forward;
            transform.position += zoomDir * (scroll * zoomSpeed * 10f * heightFactor);
        }
    }

    private void ClampPosition()
    {
        Vector3 pos = transform.position;

        // 기본 시점 모드별(3D/2D) 높이 클램프
        float currentMinH = isTopDownMode ? tacticalMinHeight : minHeight3D;
        float currentMaxH = isTopDownMode ? tacticalMaxHeight : maxHeight3D;

        // ↕️ 이동 제한 영역(useBounds) 활성화 시 상하(Y) 고도 한계 적용
        if (useBounds && useHeightBounds)
        {
            float realMinY = Mathf.Min(minY, maxY);
            float realMaxY = Mathf.Max(minY, maxY);
            if (realMaxY - realMinY >= 5f)
            {
                currentMinH = Mathf.Max(currentMinH, realMinY);
                currentMaxH = Mathf.Min(currentMaxH, realMaxY);
            }
        }

        if (currentMaxH > currentMinH)
        {
            pos.y = Mathf.Clamp(pos.y, currentMinH, currentMaxH);
        }

        // 🏔️ 지형 고도(언덕)가 존재할 경우 카메라가 지면 속으로 파고들지 않도록 안전 최소 고도 보장
        if (!isTopDownMode && TerrainHeightManager.HasInstance)
        {
            float terrainH = TerrainHeightManager.SampleHeightFast(pos.x, pos.z);
            pos.y = Mathf.Max(pos.y, terrainH + 1.5f);
        }

        // ↔️ 평면 X-Z (동서남북 및 전후 화면상하) 경계 제한
        if (useBounds)
        {
            float realMinX = Mathf.Min(minXZ.x, maxXZ.x);
            float realMaxX = Mathf.Max(minXZ.x, maxXZ.x);
            float realMinZ = Mathf.Min(minXZ.y, maxXZ.y);
            float realMaxZ = Mathf.Max(minXZ.y, maxXZ.y);

            // 유효한 경계 영역(최소 20m 폭)이 설정되어 있을 때만 클램프 적용 (고정/프리징 버그 원천 방지)
            if (realMaxX - realMinX >= 20f)
            {
                pos.x = Mathf.Clamp(pos.x, realMinX, realMaxX);
            }
            if (realMaxZ - realMinZ >= 20f)
            {
                pos.z = Mathf.Clamp(pos.z, realMinZ, realMaxZ);
            }
        }

        transform.position = pos;
    }

    /// <summary>
    /// 씬 내 Terrain을 자동 감지하여 카메라의 평면 XZ 및 수직 상하 Y축 이동 제한 경계를 1:1로 맞춥니다.
    /// 에디터 인스펙터 우클릭 메뉴 또는 런타임 시작 시 호출됩니다.
    /// </summary>
    [ContextMenu("Auto Detect Terrain Bounds")]
    public void AutoDetectTerrainBounds()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            terrain = FindFirstObjectByType<Terrain>();
        }

        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;

            if (tSize.x >= 100f && tSize.z >= 100f)
            {
                float x1 = tPos.x + 30f;
                float x2 = tPos.x + tSize.x - 30f;
                float z1 = tPos.z + 30f;
                float z2 = tPos.z + tSize.z - 30f;

                minXZ = new Vector2(Mathf.Min(x1, x2), Mathf.Min(z1, z2));
                maxXZ = new Vector2(Mathf.Max(x1, x2), Mathf.Max(z1, z2));

                minY = Mathf.Max(5f, tPos.y + 5f);
                maxY = Mathf.Max(300f, tPos.y + tSize.y + 150f);

                maxHeight3D = Mathf.Max(maxHeight3D, maxY);
                tacticalMaxHeight = Mathf.Max(tacticalMaxHeight, maxY + 100f);

                Debug.Log($"<color=#00FFFF><b>[CameraController] 🗺️ 터레인 경계 자동 감지 완료: XZ [{minXZ} ~ {maxXZ}], Y [{minY:F1}m ~ {maxY:F1}m]</b></color>");
            }
        }
    }
}