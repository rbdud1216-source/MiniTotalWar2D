using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 카메라의 전장 시야(Camera Viewport Frustum)를 미니맵 상에 실시간 사다리꼴 폴리곤으로 투영하는 UI 그래픽 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MinimapCameraFrustum : MaskableGraphic
{
    [Header("시야각 비주얼 설정")]
    [SerializeField] private Color frustumFillColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color frustumOutlineColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField] private float outlineThickness = 1.5f;

    private Camera targetCamera;
    private MinimapManager minimapManager;

    private Vector3 lastCamPos;
    private Quaternion lastCamRot;
    private float lastCamFov;

    public void Initialize(MinimapManager manager, Camera cam = null)
    {
        minimapManager = manager;
        targetCamera = (cam != null) ? cam : Camera.main;
        raycastTarget = false; // 마우스 클릭 방해 방지
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        Transform camT = targetCamera.transform;
        if (camT.position != lastCamPos || camT.rotation != lastCamRot || targetCamera.fieldOfView != lastCamFov)
        {
            lastCamPos = camT.position;
            lastCamRot = camT.rotation;
            lastCamFov = targetCamera.fieldOfView;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (minimapManager == null)
        {
            minimapManager = GetComponentInParent<MinimapManager>();
            if (minimapManager == null) return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        // 지면(Y = 0) 평면과의 교차점 4곳 계산
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        Vector3[] viewportCorners = new Vector3[4]
        {
            new Vector3(0f, 0f, 0f), // 좌하 (Bottom-Left)
            new Vector3(1f, 0f, 0f), // 우하 (Bottom-Right)
            new Vector3(1f, 1f, 0f), // 우상 (Top-Right)
            new Vector3(0f, 1f, 0f)  // 좌상 (Top-Left)
        };

        Vector2[] minimapLocalPoints = new Vector2[4];

        for (int i = 0; i < 4; i++)
        {
            Ray ray = targetCamera.ViewportPointToRay(viewportCorners[i]);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 worldHit = ray.GetPoint(enter);
                minimapLocalPoints[i] = minimapManager.WorldToMinimapLocal(worldHit);
            }
            else
            {
                // 지평선 너머를 바라보는 경우 카메라 전방 원거리 투영
                Vector3 fallbackWorld = targetCamera.transform.position + (ray.direction * 300f);
                fallbackWorld.y = 0f;
                minimapLocalPoints[i] = minimapManager.WorldToMinimapLocal(fallbackWorld);
            }
        }

        // 1. 내부 반투명 사다리꼴 면 렌더링 (Triangles: 0-1-2, 0-2-3)
        int baseIdx = vh.currentVertCount;
        for (int i = 0; i < 4; i++)
        {
            UIVertex vert = UIVertex.simpleVert;
            vert.color = frustumFillColor;
            vert.position = minimapLocalPoints[i];
            vh.AddVert(vert);
        }
        vh.AddTriangle(baseIdx + 0, baseIdx + 1, baseIdx + 2);
        vh.AddTriangle(baseIdx + 0, baseIdx + 2, baseIdx + 3);

        // 2. 외곽 테두리 선 렌더링
        for (int i = 0; i < 4; i++)
        {
            Vector2 p1 = minimapLocalPoints[i];
            Vector2 p2 = minimapLocalPoints[(i + 1) % 4];
            AddLineSegment(vh, p1, p2, frustumOutlineColor, outlineThickness);
        }
    }

    private void AddLineSegment(VertexHelper vh, Vector2 p1, Vector2 p2, Color col, float thickness)
    {
        Vector2 dir = (p2 - p1).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        int idx = vh.currentVertCount;

        UIVertex v0 = UIVertex.simpleVert; v0.color = col; v0.position = p1 - normal;
        UIVertex v1 = UIVertex.simpleVert; v1.color = col; v1.position = p1 + normal;
        UIVertex v2 = UIVertex.simpleVert; v2.color = col; v2.position = p2 + normal;
        UIVertex v3 = UIVertex.simpleVert; v3.color = col; v3.position = p2 - normal;

        vh.AddVert(v0);
        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);

        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 0, idx + 2, idx + 3);
    }
}
