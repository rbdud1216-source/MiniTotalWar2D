using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이동 및 대열 배치 명령 시 지형 위에 예상 도착 지점을 도트 형태로 시각화하는 프리뷰 클래스입니다.
/// </summary>
public class FormationPreviewer : MonoBehaviour
{
    [Header("프리뷰 도트 프리팹")]
    [SerializeField] private GameObject previewDotPrefab;

    [Header("3D 지형 묻힘 방지 높이 (Y Offset)")]
    [SerializeField] private float yOffset = 0.05f;

    private readonly List<GameObject> activeDots = new List<GameObject>();
    private Transform dotPoolContainer;

    private void Awake()
    {
        GameObject containerObj = new GameObject("PreviewDots_Pool");
        dotPoolContainer = containerObj.transform;
        dotPoolContainer.SetParent(transform);
    }

    /// <summary>
    /// 자유 유닛들의 슬롯 위치 및 바라보는 회전값에 맞춰 프리뷰 도트를 표시합니다.
    /// </summary>
    public void ShowFreeUnitPreview(List<Vector3> slotPositions, List<Quaternion> slotRotations)
    {
        if (slotPositions == null || slotPositions.Count == 0)
        {
            HidePreview();
            return;
        }

        EnsureDotPoolCount(slotPositions.Count);

        for (int i = 0; i < activeDots.Count; i++)
        {
            if (activeDots[i] == null) continue;

            if (i < slotPositions.Count)
            {
                Vector3 targetPos = slotPositions[i];
                targetPos.y += yOffset;

                activeDots[i].transform.position = targetPos;
                activeDots[i].transform.rotation = (slotRotations != null && i < slotRotations.Count) ? slotRotations[i] : Quaternion.identity;
                activeDots[i].SetActive(true);
            }
            else
            {
                activeDots[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// 부대(Squad) 선택 시 대형 프리뷰를 표시합니다.
    /// </summary>
    /// <summary>
    /// 부대(Squad) 선택 시 대형 프리뷰를 표시합니다 (사각방진 4방향 및 원형진 방사형 시선 방향 반영).
    /// </summary>
    public void ShowPreview(Squad squad, Vector3 destination, Quaternion rotation, int columns)
    {
        if (squad == null)
        {
            HidePreview();
            return;
        }

        var slotTransforms = squad.GetPreviewSlotTransforms(destination, rotation, columns);
        EnsureDotPoolCount(slotTransforms.Count);

        for (int i = 0; i < activeDots.Count; i++)
        {
            if (activeDots[i] == null) continue;

            if (i < slotTransforms.Count)
            {
                Vector3 targetPos = slotTransforms[i].position;
                targetPos.y += yOffset;

                activeDots[i].transform.position = targetPos;
                activeDots[i].transform.rotation = slotTransforms[i].rotation;
                activeDots[i].SetActive(true);
            }
            else
            {
                activeDots[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// 부대(Squad) 곡선 배치 프리뷰 도트를 표시합니다.
    /// </summary>
    public void ShowCurvedPreview(Squad squad, Vector3 destination, Quaternion rotation, int columns, float curvatureHeight)
    {
        if (squad == null)
        {
            HidePreview();
            return;
        }

        List<Vector3> slotPositions = squad.GetCurvedPreviewSlotPositions(destination, rotation, columns, curvatureHeight);
        EnsureDotPoolCount(slotPositions.Count);

        for (int i = 0; i < activeDots.Count; i++)
        {
            if (activeDots[i] == null) continue;

            if (i < slotPositions.Count)
            {
                Vector3 targetPos = slotPositions[i];
                targetPos.y += yOffset;

                activeDots[i].transform.position = targetPos;
                activeDots[i].transform.rotation = rotation;
                activeDots[i].SetActive(true);
            }
            else
            {
                activeDots[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// 여러 부대(Multi-Squad)의 동시 배치 및 이동 프리뷰 도트를 표시합니다.
    /// </summary>
    public void ShowMultiSquadPreview(List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> squadConfigs)
    {
        if (squadConfigs == null || squadConfigs.Count == 0)
        {
            HidePreview();
            return;
        }

        List<Vector3> allPositions = new List<Vector3>();
        List<Quaternion> allRotations = new List<Quaternion>();

        foreach (var config in squadConfigs)
        {
            if (config.squad == null) continue;
            var slotTransforms = config.squad.GetPreviewSlotTransforms(config.destination, config.rotation, config.columns);
            for (int i = 0; i < slotTransforms.Count; i++)
            {
                allPositions.Add(slotTransforms[i].position);
                allRotations.Add(slotTransforms[i].rotation);
            }
        }

        EnsureDotPoolCount(allPositions.Count);

        for (int i = 0; i < activeDots.Count; i++)
        {
            if (activeDots[i] == null) continue;

            if (i < allPositions.Count)
            {
                Vector3 targetPos = allPositions[i];
                targetPos.y += yOffset;

                activeDots[i].transform.position = targetPos;
                activeDots[i].transform.rotation = allRotations[i];
                activeDots[i].SetActive(true);
            }
            else
            {
                activeDots[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// 군단 단위 곡선(Grand Army Arc) 프리뷰 도트를 표시합니다.
    /// </summary>
    public void ShowMultiSquadCurvedPreview(List<(Squad squad, Vector3 destination, Quaternion rotation, int columns, float curve)> squadConfigs)
    {
        if (squadConfigs == null || squadConfigs.Count == 0)
        {
            HidePreview();
            return;
        }

        List<Vector3> allPositions = new List<Vector3>();
        List<Quaternion> allRotations = new List<Quaternion>();

        foreach (var config in squadConfigs)
        {
            if (config.squad == null) continue;
            List<Vector3> slots = (Mathf.Abs(config.curve) > 0.001f)
                ? config.squad.GetCurvedPreviewSlotPositions(config.destination, config.rotation, config.columns, config.curve)
                : config.squad.GetPreviewSlotPositions(config.destination, config.rotation, config.columns);

            for (int i = 0; i < slots.Count; i++)
            {
                allPositions.Add(slots[i]);
                allRotations.Add(config.rotation);
            }
        }

        EnsureDotPoolCount(allPositions.Count);

        for (int i = 0; i < activeDots.Count; i++)
        {
            if (activeDots[i] == null) continue;

            if (i < allPositions.Count)
            {
                Vector3 targetPos = allPositions[i];
                targetPos.y += yOffset;

                activeDots[i].transform.position = targetPos;
                activeDots[i].transform.rotation = allRotations[i];
                activeDots[i].SetActive(true);
            }
            else
            {
                activeDots[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// 요구되는 수량만큼 도트 오브젝트 풀을 유지 및 확장합니다.
    /// </summary>
    private void EnsureDotPoolCount(int targetCount)
    {
        // 씬 전환/외부 파괴로 인한 Null 요소 제거
        activeDots.RemoveAll(dot => dot == null);

        while (activeDots.Count < targetCount)
        {
            GameObject newDot;
            if (previewDotPrefab != null)
            {
                newDot = Instantiate(previewDotPrefab, dotPoolContainer);
            }
            else
            {
                // 프리팹 미할당 시 대체용 기본 구체(Sphere) 생성
                newDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                newDot.transform.SetParent(dotPoolContainer);
                newDot.transform.localScale = Vector3.one * 0.3f;

                Collider col = newDot.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer ren = newDot.GetComponent<Renderer>();
                if (ren != null && ren.material != null)
                {
                    ren.material.color = Color.cyan;
                }
            }
            newDot.SetActive(false);
            activeDots.Add(newDot);
        }
    }

    /// <summary>
    /// 모든 프리뷰 도트를 화면에서 숨깁니다.
    /// </summary>
    public void HidePreview()
    {
        for (int i = 0; i < activeDots.Count; i++)
        {
            if (activeDots[i] != null)
            {
                activeDots[i].SetActive(false);
            }
        }
    }
}