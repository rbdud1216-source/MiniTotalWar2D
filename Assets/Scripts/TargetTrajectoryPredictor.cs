using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 부대 마우스 오버(Hovering) 시, 예상 정지 사격 위치(Stand Position)와
/// 실제 물리 기반 3D 포물선 탄도 궤적선 및 지형 차폐(Blocked by Terrain) 경고를 실시간 시각화하는 컴포넌트입니다.
/// </summary>
public class TargetTrajectoryPredictor : MonoBehaviour
{
    [Header("궤적 라인 렌더러 설정")]
    [Tooltip("3D 포물선 탄도 궤적을 그릴 라인 렌더러")]
    [SerializeField] private LineRenderer trajectoryLine;
    [Tooltip("부대 위치에서 정지 지점까지 바닥에 이어질 이동 경로 라인 렌더러")]
    [SerializeField] private LineRenderer pathLine;


    [Header("차폐 충돌 X 마커 설정")]
    [Tooltip("언덕 등에 탄도가 부딪힐 때 차폐 지점에 띄울 충돌 마커 라인 렌더러")]
    [SerializeField] private LineRenderer collisionMarkerLine;

    [Header("궤적 품질 및 물리 파라미터")]
    [Tooltip("탄도 궤적 샘플 포인트 수 (기본: 32)")]
    [SerializeField] private int trajectorySamples = 32;
    [Tooltip("궤적 라인 두께 (기본: 0.22m)")]
    [SerializeField] private float trajectoryWidth = 0.22f;

    // 통일 표준 색상
    public static readonly Color ColorClear = new Color(0.18f, 0.83f, 0.45f, 0.90f);    // 에메랄드 그린 (#2ED573)
    public static readonly Color ColorBlocked = new Color(1.00f, 0.13f, 0.13f, 0.95f);  // 크림슨 레드 (#FF2222)
    public static readonly Color ColorPath = new Color(1.00f, 0.90f, 0.40f, 0.65f);     // 이동 경로선 (연노랑/골드)

    private Vector3[] trajectoryPoints;

    private void Awake()
    {
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (trajectoryPoints == null || trajectoryPoints.Length != trajectorySamples)
        {
            trajectoryPoints = new Vector3[trajectorySamples];
        }

        if (trajectoryLine == null)
        {
            GameObject trajObj = new GameObject("TrajectoryLine");
            trajObj.transform.SetParent(transform, false);
            trajectoryLine = trajObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(trajectoryLine, trajectoryWidth);
        }

        if (pathLine == null)
        {
            GameObject pathObj = new GameObject("PathLine");
            pathObj.transform.SetParent(transform, false);
            pathLine = pathObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(pathLine, 0.12f);
        }

        if (collisionMarkerLine == null)
        {
            GameObject markerObj = new GameObject("CollisionMarker");
            markerObj.transform.SetParent(transform, false);
            collisionMarkerLine = markerObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(collisionMarkerLine, 0.28f);
        }
    }

    private void ConfigureLineRenderer(LineRenderer lr, float width)
    {
        lr.useWorldSpace = true;
        lr.startWidth = width;
        lr.endWidth = width * 0.85f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null) lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        lr.material = new Material(lineShader);
    }

    /// <summary>
    /// 원거리 부대와 목표 적 부대 사이의 예상 정지 위치, 3D 탄도 궤적 및 지형 차폐 여부를 실시간 계산하여 시각화합니다.
    /// </summary>
    public bool UpdatePrediction(
        Vector3 shooterCenter,
        Squad enemySquad,
        float maxRange,
        TrajectoryMode trajMode,
        float arcAngle,
        bool isFullCircle = false)
    {
        if (enemySquad == null || enemySquad.MemberCount <= 0)
        {
            Hide();
            return false;
        }

        // 👁️ [삼국지 토탈워식 시야 규칙]: 미발각 적 부대는 마우스 궤적 예측선도 100% 숨김
        if (!enemySquad.isPlayer && !enemySquad.isSpotted)
        {
            bool showDebug = (VisionDetectionManager.Instance != null && VisionDetectionManager.Instance.isFullVisionDebug);
            if (!showDebug)
            {
                Hide();
                return false;
            }
        }

        Vector3 targetCenter = enemySquad.GetVisualCenter();
        targetCenter.y = TerrainHeightManager.SampleHeightFast(targetCenter.x, targetCenter.z) + 1.0f; // 가슴 높이 1.0m
        return UpdatePredictionInternal(shooterCenter, targetCenter, maxRange, trajMode, arcAngle, isFullCircle);
    }

    /// <summary>
    /// 어택땅(Attack-Move) 모드 등에서 지면 좌표를 향한 가상 탄도 궤적 및 지형 차폐 여부를 실시간 계산하여 시각화합니다.
    /// </summary>
    public bool UpdateGroundPrediction(
        Vector3 shooterCenter,
        Vector3 targetGroundPos,
        float maxRange,
        TrajectoryMode trajMode,
        float arcAngle,
        bool isFullCircle = false)
    {
        Vector3 targetCenter = targetGroundPos;
        targetCenter.y = TerrainHeightManager.SampleHeightFast(targetCenter.x, targetCenter.z) + 0.3f;
        return UpdatePredictionInternal(shooterCenter, targetCenter, maxRange, trajMode, arcAngle, isFullCircle);
    }

    private bool UpdatePredictionInternal(
        Vector3 shooterCenter,
        Vector3 targetCenter,
        float maxRange,
        TrajectoryMode trajMode,
        float arcAngle,
        bool isFullCircle)
    {
        EnsureComponents();

        Vector3 toEnemy = targetCenter - shooterCenter;
        toEnemy.y = 0f;
        float distToEnemy = toEnemy.magnitude;
        if (distToEnemy < 1.0f)
        {
            Hide();
            return false;
        }

        Vector3 dirToEnemy = toEnemy.normalized;
        Quaternion faceRot = Quaternion.LookRotation(dirToEnemy);

        // 1. 예상 정지 사격 위치(Stand Position) 산출
        // 최대 사거리의 85% 밖이거나 가려진 경우 최적 사거리(70% 지점)로 전진 정지 위치 계산
        bool needsAdvance = distToEnemy > (maxRange * 0.85f);
        Vector3 standPos = shooterCenter;

        if (needsAdvance)
        {
            float engageDist = maxRange * 0.70f;
            standPos = targetCenter - (dirToEnemy * engageDist);
            standPos.y = TerrainHeightManager.SampleHeightFast(standPos.x, standPos.z);

            // 부대 -> 정지 위치 이동 경로선 표시 (지형 밀착 16포인트)
            int pathSamples = 16;
            pathLine.gameObject.SetActive(true);
            pathLine.positionCount = pathSamples;
            for (int p = 0; p < pathSamples; p++)
            {
                float tPath = (float)p / (pathSamples - 1);
                float px = Mathf.Lerp(shooterCenter.x, standPos.x, tPath);
                float pz = Mathf.Lerp(shooterCenter.z, standPos.z, tPath);
                float py = TerrainHeightManager.SampleHeightFast(px, pz) + 0.20f;
                pathLine.SetPosition(p, new Vector3(px, py, pz));
            }
            pathLine.startColor = ColorPath;
            pathLine.endColor = ColorPath;
        }
        else
        {
            pathLine.gameObject.SetActive(false);
        }

        // 2. 3D 물리 기반 탄도 궤적 계산 (정지 위치의 어깨 높이 1.4m -> 목표 적 가슴 높이 1.0m)
        Vector3 launchPos = standPos;
        launchPos.y = TerrainHeightManager.SampleHeightFast(launchPos.x, launchPos.z) + 1.4f;

        Vector3 launchToTarget = targetCenter - launchPos;
        float horizDist = Mathf.Sqrt(launchToTarget.x * launchToTarget.x + launchToTarget.z * launchToTarget.z);
        float dy = targetCenter.y - launchPos.y;

        // 탄도 정점 높이 Harc 산출
        float hArc;
        if (trajMode == TrajectoryMode.HighArc)
        {
            // 곡사: 거리에 비례하는 유려한 포물선 앙각 높이 (거리의 25~35%)
            hArc = Mathf.Max(2.5f, horizDist * 0.28f);
        }
        else
        {
            // 직사: 거의 직선에 가까운 미세 앙각 (거리의 3~4%)
            hArc = Mathf.Max(0.15f, horizDist * 0.035f);
        }

        // 3. 지형 충돌(차폐) 실시간 검사
        bool isBlocked = false;
        Vector3 blockedPoint = Vector3.zero;
        float blockedT = 1.0f;
        int blockedSampleIndex = -1;

        for (int i = 0; i < trajectorySamples; i++)
        {
            float t = (float)i / (trajectorySamples - 1);
            float curX = launchPos.x + launchToTarget.x * t;
            float curZ = launchPos.z + launchToTarget.z * t;
            float curY = launchPos.y + dy * t + 4.0f * hArc * t * (1.0f - t);

            Vector3 pt = new Vector3(curX, curY, curZ);
            float groundY = TerrainHeightManager.SampleHeightFast(curX, curZ);

            // 지형 높이 대조 (출발점과 도착점 부근 8% 구간 제외하고 중간 지형 검사)
            if (!isBlocked && t > 0.08f && t < 0.92f)
            {
                if (curY <= groundY + 0.12f)
                {
                    isBlocked = true;
                    blockedPoint = new Vector3(curX, groundY, curZ);
                    blockedT = t;
                    blockedSampleIndex = i;
                }
            }

            // 🎯 차폐된 이후의 포인트는 언덕 땅속으로 파묻혀 사라지지 않도록 최소 지형 고도 + 0.18m 위로 올려 붉은 궤적이 선명하게 드러나도록 처리
            if (isBlocked && i >= blockedSampleIndex)
            {
                pt.y = Mathf.Max(curY, groundY + 0.18f);
            }

            trajectoryPoints[i] = pt;
        }

        // 4. 궤적선 렌더링 및 충돌 지점 색상 그라데이션 전환
        trajectoryLine.gameObject.SetActive(true);
        trajectoryLine.positionCount = trajectorySamples;
        trajectoryLine.SetPositions(trajectoryPoints);

        if (isBlocked)
        {
            // 🎯 부딪혀서 쏠 수 없는 지점(blockedT)에서 에메랄드 그린 -> 크림슨 레드로 즉각 전환!
            // 사용자의 요구사항: "부딪혀서 쏘질 못하는 지점에서 선의 색이 빨간색 등으로 바뀌어 제대로 쏠 수 없음을 표시"
            Gradient grad = new Gradient();
            float tSplit1 = Mathf.Clamp01(blockedT - 0.015f);
            float tSplit2 = Mathf.Clamp01(blockedT);

            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(ColorClear, 0.0f),
                    new GradientColorKey(ColorClear, tSplit1),
                    new GradientColorKey(ColorBlocked, tSplit2),
                    new GradientColorKey(ColorBlocked, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(ColorClear.a, 0.0f),
                    new GradientAlphaKey(ColorClear.a, tSplit1),
                    new GradientAlphaKey(ColorBlocked.a, tSplit2),
                    new GradientAlphaKey(ColorBlocked.a, 1.0f)
                }
            );
            trajectoryLine.colorGradient = grad;
        }
        else
        {
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(ColorClear, 0.0f),
                    new GradientColorKey(ColorClear, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(ColorClear.a, 0.0f),
                    new GradientAlphaKey(ColorClear.a, 1.0f)
                }
            );
            trajectoryLine.colorGradient = grad;
        }

        // 5. 차폐 X 마커 및 수직 경고 기둥 렌더링
        if (isBlocked)
        {
            collisionMarkerLine.gameObject.SetActive(true);
            DrawCrossMarker(blockedPoint, 1.4f, ColorBlocked);
        }
        else
        {
            collisionMarkerLine.gameObject.SetActive(false);
        }

        return !isBlocked;
    }

    private void DrawCrossMarker(Vector3 center, float size, Color col)
    {
        // 🎯 굴곡진 지형에 묻히지 않도록 각 버텍스마다 실시간 지형 고도 개별 샘플링!
        // + 언덕 능선 뒤에서도 즉시 식별할 수 있도록 공중으로 2.5m 솟구치는 수직 경고 기둥 일체형 렌더링
        collisionMarkerLine.positionCount = 9;
        collisionMarkerLine.startColor = col;
        collisionMarkerLine.endColor = col;

        float groundCenterY = TerrainHeightManager.SampleHeightFast(center.x, center.z);
        Vector3 pCenter = new Vector3(center.x, groundCenterY + 0.30f, center.z);
        Vector3 pPillarTop = new Vector3(center.x, groundCenterY + 2.50f, center.z); // 공중 2.5m 수직 핀

        // X자 4방향 날개 (각 지형 고도 + 0.30m 지형 추종)
        Vector3 p1 = new Vector3(center.x - size, TerrainHeightManager.SampleHeightFast(center.x - size, center.z - size) + 0.30f, center.z - size);
        Vector3 p2 = new Vector3(center.x + size, TerrainHeightManager.SampleHeightFast(center.x + size, center.z + size) + 0.30f, center.z + size);
        Vector3 p3 = new Vector3(center.x - size, TerrainHeightManager.SampleHeightFast(center.x - size, center.z + size) + 0.30f, center.z + size);
        Vector3 p4 = new Vector3(center.x + size, TerrainHeightManager.SampleHeightFast(center.x + size, center.z - size) + 0.30f, center.z - size);

        // 경로: p1 -> pCenter -> p2 -> pCenter -> pPillarTop -> pCenter -> p3 -> pCenter -> p4
        collisionMarkerLine.SetPosition(0, p1);
        collisionMarkerLine.SetPosition(1, pCenter);
        collisionMarkerLine.SetPosition(2, p2);
        collisionMarkerLine.SetPosition(3, pCenter);
        collisionMarkerLine.SetPosition(4, pPillarTop);
        collisionMarkerLine.SetPosition(5, pCenter);
        collisionMarkerLine.SetPosition(6, p3);
        collisionMarkerLine.SetPosition(7, pCenter);
        collisionMarkerLine.SetPosition(8, p4);
    }

    public void Hide()
    {
        if (trajectoryLine != null) trajectoryLine.gameObject.SetActive(false);
        if (pathLine != null) pathLine.gameObject.SetActive(false);
        if (collisionMarkerLine != null) collisionMarkerLine.gameObject.SetActive(false);
    }
}
