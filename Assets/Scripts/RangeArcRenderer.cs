using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토탈워(Total War) 스타일의 원거리 사격 호(Firing Arc)를
/// 지형 굴곡을 완벽히 추적하는 고해상도 지면 밀착 라인(Outer Arc & Border Lines) 및
/// 선택적 반투명 내부 면(Procedural Mesh)으로 정밀하게 렌더링하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RangeArcRenderer : MonoBehaviour
{
    [Header("사격 호 선(Line) 설정 - 토탈워 표준")]
    [Tooltip("외곽 사격 호 라인의 분할 포인트 수 (높을수록 산악 지형에 매끄럽게 밀착, 기본: 120)")]
    [SerializeField] private int arcLineSamples = 120;
    [Tooltip("부채꼴 좌/우 경계선의 분할 포인트 수 (기본: 30)")]
    [SerializeField] private int borderLineSamples = 30;
    [Tooltip("지형 표면 위 띄움 높이 (Z-Fighting 및 파고듦 방지, 단위: m, 기본: 0.28m)")]
    [SerializeField] private float lineYOffset = 0.28f;
    [Tooltip("사격 호 테두리 선 두께 (단위: m, 기본: 0.26m)")]
    [SerializeField] private float lineWidth = 0.26f;

    [Header("내부 면 메쉬 설정 (선택적)")]
    [Tooltip("부채꼴 각도 방향 분할 세그먼트 수 (기본: 48)")]
    [SerializeField] private int radialSegments = 48;
    [Tooltip("거리 방향 분할 링 수 (기본: 10)")]
    [SerializeField] private int distanceRings = 10;
    [Tooltip("메쉬 내부 면 지형 표면 위 띄움 높이 (단위: m, 기본: 0.20m)")]
    [SerializeField] private float meshYOffset = 0.20f;

    private LineRenderer outerArcLine;
    private LineRenderer leftBorderLine;
    private LineRenderer rightBorderLine;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh arcMesh;
    private MaterialPropertyBlock propBlock;

    private static readonly int ColorPropId = Shader.PropertyToID("_Color");
    private static readonly int RimColorPropId = Shader.PropertyToID("_RimColor");

    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();
    private List<int> triangles = new List<int>();

    private Vector3[] arcLinePoints;
    private Vector3[] leftBorderPoints;
    private Vector3[] rightBorderPoints;

    private bool isVisible = true;
    private Color currentRimColor = new Color(0.18f, 0.83f, 0.45f, 0.95f);
    private Color currentMainColor = new Color(0.18f, 0.83f, 0.45f, 0.0f);

    private void Awake()
    {
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (arcMesh == null && meshFilter != null)
        {
            arcMesh = new Mesh
            {
                name = "RangeArc_ProceduralMesh"
            };
            arcMesh.MarkDynamic();
            meshFilter.mesh = arcMesh;
        }

        propBlock ??= new MaterialPropertyBlock();

        if (meshRenderer != null && meshRenderer.sharedMaterial == null)
        {
            Shader overlayShader = Shader.Find("Custom/RangeArcOverlay");
            if (overlayShader != null)
            {
                meshRenderer.material = new Material(overlayShader);
            }
        }

        // 🌟 고해상도 지형 밀착 외곽 호(Outer Arc Line) 생성
        if (outerArcLine == null)
        {
            GameObject arcObj = new GameObject("OuterArcLine");
            arcObj.transform.SetParent(transform, false);
            outerArcLine = arcObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(outerArcLine, lineWidth);
        }

        // 🌟 좌측 경계선(Left Border Line) 생성
        if (leftBorderLine == null)
        {
            GameObject leftObj = new GameObject("LeftBorderLine");
            leftObj.transform.SetParent(transform, false);
            leftBorderLine = leftObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(leftBorderLine, lineWidth * 0.85f);
        }

        // 🌟 우측 경계선(Right Border Line) 생성
        if (rightBorderLine == null)
        {
            GameObject rightObj = new GameObject("RightBorderLine");
            rightObj.transform.SetParent(transform, false);
            rightBorderLine = rightObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(rightBorderLine, lineWidth * 0.85f);
        }

        if (arcLinePoints == null || arcLinePoints.Length != arcLineSamples + 1)
        {
            arcLinePoints = new Vector3[arcLineSamples + 1];
        }
        if (leftBorderPoints == null || leftBorderPoints.Length != borderLineSamples + 1)
        {
            leftBorderPoints = new Vector3[borderLineSamples + 1];
        }
        if (rightBorderPoints == null || rightBorderPoints.Length != borderLineSamples + 1)
        {
            rightBorderPoints = new Vector3[borderLineSamples + 1];
        }
    }

    private void ConfigureLineRenderer(LineRenderer lr, float width)
    {
        lr.useWorldSpace = true;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.TransformZ; // 카메라 또는 상방 정렬로 지면에 깨끗하게 투영

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
        lr.material = new Material(unlitShader);
    }

    /// <summary>
    /// 사격 호의 색상(내부 면 및 테두리 선 색상)을 설정합니다.
    /// </summary>
    public void SetColors(Color mainColor, Color rimColor)
    {
        EnsureComponents();
        currentMainColor = mainColor;
        currentRimColor = rimColor;

        if (meshRenderer != null)
        {
            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(ColorPropId, mainColor);
            propBlock.SetColor(RimColorPropId, rimColor);
            meshRenderer.SetPropertyBlock(propBlock);
        }

        Color lineCol = new Color(rimColor.r, rimColor.g, rimColor.b, 0.95f);
        if (outerArcLine != null)
        {
            outerArcLine.startColor = lineCol;
            outerArcLine.endColor = lineCol;
        }
        if (leftBorderLine != null)
        {
            leftBorderLine.startColor = lineCol;
            leftBorderLine.endColor = lineCol;
        }
        if (rightBorderLine != null)
        {
            rightBorderLine.startColor = lineCol;
            rightBorderLine.endColor = lineCol;
        }
    }

    /// <summary>
    /// 원점, 회전각, 사격 각도(총병 60도, 궁병 120도, 원형 360도), 사거리 범위에 맞춰
    /// 지형 밀착 선(Line) 및 선택적 내부 면(Mesh)을 실시간으로 갱신합니다.
    /// </summary>
    public void UpdateArc(Vector3 origin, Quaternion rotation, float arcAngle, float maxRange, float minRange = 5.0f, bool isFullCircle = false)
    {
        EnsureComponents();

        if (!isVisible)
        {
            SetVisible(false);
            return;
        }

        float halfAngle = isFullCircle ? 180f : (arcAngle * 0.5f);
        float rMin = Mathf.Max(0.5f, minRange);
        float rMax = Mathf.Max(rMin + 1.0f, maxRange);

        Vector3 forward = rotation * Vector3.forward;
        Vector3 right = rotation * Vector3.right;

        // =========================================================
        // 1. 🌟 [외곽 사격 호 라인 렌더링] (120포인트 정밀 지형 밀착)
        // =========================================================
        int arcSamples = isFullCircle ? (arcLineSamples + 1) : (arcLineSamples + 1);
        if (arcLinePoints.Length < arcSamples) arcLinePoints = new Vector3[arcSamples];

        float arcAngleSpan = halfAngle * 2.0f;
        for (int i = 0; i < arcSamples; i++)
        {
            float t = (float)i / (arcSamples - 1);
            float currentAngle = -halfAngle + (t * arcAngleSpan);
            float rad = currentAngle * Mathf.Deg2Rad;

            float sinA = Mathf.Sin(rad);
            float cosA = Mathf.Cos(rad);
            Vector3 localDir = (right * sinA) + (forward * cosA);

            Vector3 worldPos = origin + (localDir * rMax);
            worldPos.y = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z) + lineYOffset;
            arcLinePoints[i] = worldPos;
        }

        outerArcLine.gameObject.SetActive(true);
        outerArcLine.loop = isFullCircle;
        outerArcLine.positionCount = arcSamples;
        outerArcLine.SetPositions(arcLinePoints);

        // =========================================================
        // 2. 🌟 [부채꼴 좌/우 경계선 렌더링] (30포인트 정밀 지형 밀착)
        // =========================================================
        if (!isFullCircle)
        {
            leftBorderLine.gameObject.SetActive(true);
            rightBorderLine.gameObject.SetActive(true);

            Vector3 leftOuter = arcLinePoints[0];
            Vector3 rightOuter = arcLinePoints[arcSamples - 1];

            int bSamples = borderLineSamples + 1;
            if (leftBorderPoints.Length < bSamples) leftBorderPoints = new Vector3[bSamples];
            if (rightBorderPoints.Length < bSamples) rightBorderPoints = new Vector3[bSamples];

            for (int i = 0; i < bSamples; i++)
            {
                float t = (float)i / (bSamples - 1);

                // 좌측 경계선: origin -> leftOuter
                float lx = Mathf.Lerp(origin.x, leftOuter.x, t);
                float lz = Mathf.Lerp(origin.z, leftOuter.z, t);
                float ly = TerrainHeightManager.SampleHeightFast(lx, lz) + lineYOffset;
                leftBorderPoints[i] = new Vector3(lx, ly, lz);

                // 우측 경계선: origin -> rightOuter
                float rx = Mathf.Lerp(origin.x, rightOuter.x, t);
                float rz = Mathf.Lerp(origin.z, rightOuter.z, t);
                float ry = TerrainHeightManager.SampleHeightFast(rx, rz) + lineYOffset;
                rightBorderPoints[i] = new Vector3(rx, ry, rz);
            }

            leftBorderLine.positionCount = bSamples;
            leftBorderLine.SetPositions(leftBorderPoints);

            rightBorderLine.positionCount = bSamples;
            rightBorderLine.SetPositions(rightBorderPoints);
        }
        else
        {
            leftBorderLine.gameObject.SetActive(false);
            rightBorderLine.gameObject.SetActive(false);
        }

        // =========================================================
        // 3. 🌟 [선택적 내부 면 메쉬 렌더링]
        // =========================================================
        // 알파값이 거의 없으면(0.005 이하) 메쉬 렌더러를 꺼서 지형 깨짐 및 연산 낭비 원천 차단
        if (currentMainColor.a <= 0.005f)
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            return;
        }

        if (meshRenderer != null) meshRenderer.enabled = true;
        BuildProceduralMesh(origin, forward, right, halfAngle, rMin, rMax);
    }

    private void BuildProceduralMesh(Vector3 origin, Vector3 forward, Vector3 right, float halfAngle, float rMin, float rMax)
    {
        if (arcMesh == null) return;

        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();

        int segs = Mathf.Max(16, radialSegments);
        int rings = Mathf.Max(4, distanceRings);
        float angleStep = (halfAngle * 2.0f) / segs;

        for (int ring = 0; ring <= rings; ring++)
        {
            float ringT = (float)ring / rings;
            float r = Mathf.Lerp(rMin, rMax, ringT);

            for (int seg = 0; seg <= segs; seg++)
            {
                float segT = (float)seg / segs;
                float currentAngle = -halfAngle + (seg * angleStep);
                float rad = currentAngle * Mathf.Deg2Rad;

                float sinA = Mathf.Sin(rad);
                float cosA = Mathf.Cos(rad);
                Vector3 localDir = (right * sinA) + (forward * cosA);

                Vector3 worldPos = origin + (localDir * r);
                worldPos.y = TerrainHeightManager.SampleHeightFast(worldPos.x, worldPos.z) + meshYOffset;

                vertices.Add(worldPos);
                uvs.Add(new Vector2(segT, ringT));
                colors.Add(Color.white);
            }
        }

        int stride = segs + 1;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segs; seg++)
            {
                int current = ring * stride + seg;
                int next = current + stride;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);

                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        arcMesh.Clear();
        arcMesh.SetVertices(vertices);
        arcMesh.SetUVs(0, uvs);
        arcMesh.SetColors(colors);
        arcMesh.SetTriangles(triangles, 0);
        arcMesh.RecalculateBounds();
    }

    /// <summary>
    /// 사격 호 컴포넌트 전체(라인 3종 및 메쉬)의 활성화 여부를 전환합니다.
    /// </summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        EnsureComponents();

        if (outerArcLine != null) outerArcLine.gameObject.SetActive(visible);
        if (leftBorderLine != null) leftBorderLine.gameObject.SetActive(visible);
        if (rightBorderLine != null) rightBorderLine.gameObject.SetActive(visible);
        if (meshRenderer != null) meshRenderer.enabled = visible && (currentMainColor.a > 0.005f);
    }
}
