using System.Collections;
using UnityEngine;

/// <summary>
/// RTS/FPS(FlyCam) 스타일의 카메라 이동(Q/E 높이 조절 포함), 회전, Zoom, 맵 경계 제한 및 포커싱 제어 클래스입니다.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("이동 속도 설정")]
    [SerializeField] private float moveSpeed = 40f;
    [SerializeField] private float fastMoveMultiplier = 2f;
    [SerializeField] private float zoomSpeed = 20f;
    [SerializeField] private float rotationSpeed = 100f;

    [Header("시야 높이(Zoom) 제한 - 3D 전장 시점")]
    [SerializeField] private float minHeight3D = 5f;
    [SerializeField] private float maxHeight3D = 150f;

    [Header("시야 높이(Zoom) 제한 - 2D 전술지도(Tactical Map) 시점")]
    [SerializeField] private float tacticalMinHeight = 20f;
    [SerializeField] private float tacticalMaxHeight = 350f;
    [SerializeField] private float tacticalDefaultHeight = 130f;

    public float MinHeight => isTopDownMode ? tacticalMinHeight : minHeight3D;
    public float MaxHeight => isTopDownMode ? tacticalMaxHeight : maxHeight3D;

    [Header("이동 제한 영역 (1000x1000 바닥 기준)")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minXZ = new Vector2(-450f, -450f);
    [SerializeField] private Vector2 maxXZ = new Vector2(450f, 450f);

    public Vector2 MinXZ => minXZ;
    public Vector2 MaxXZ => maxXZ;

    [Header("부대 포커스(더블클릭) 설정")]
    [SerializeField] private float defaultFocusDistance = 10f; // 기본 포커스 거리
    [SerializeField] private float focusDuration = 0.25f;      // 포커싱 이동 시간 (초)

    [Header("2D / 3D 뷰 모드 설정")]
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
    /// WASD(평면 이동) 및 Q/E(높이 조절) 키 입력 제어를 처리합니다.
    /// </summary>
    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

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
            forward.Normalize();
            right.Normalize();
        }

        // [수정] WASD 평면 이동 + Q/E 수직 Y축 이동 조합
        Vector3 moveDirection = (forward * v + right * h).normalized;
        Vector3 verticalDirection = Vector3.up * upDown;

        transform.position += (moveDirection + verticalDirection) * (currentSpeed * Time.deltaTime);
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

                // X축 회전 (상하 피치 제한)
                float newXAngle = transform.eulerAngles.x - rotY;
                if (newXAngle > 180f) newXAngle -= 360f;
                newXAngle = Mathf.Clamp(newXAngle, 10f, 85f);

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

        float currentMinH = isTopDownMode ? tacticalMinHeight : minHeight3D;
        float currentMaxH = isTopDownMode ? tacticalMaxHeight : maxHeight3D;
        pos.y = Mathf.Clamp(pos.y, currentMinH, currentMaxH);

        if (useBounds)
        {
            pos.x = Mathf.Clamp(pos.x, minXZ.x, maxXZ.x);
            pos.z = Mathf.Clamp(pos.z, minXZ.y, maxXZ.y);
        }

        transform.position = pos;
    }
}