using System.Collections.Generic;
using UnityEngine;
using MiniTotalWar;

/// <summary>
/// 🔥 [풀·관목 초고속 화재 시뮬레이터 및 화염 고속도로 (Shrub Fire Controller)]
/// 나무(40초 전소)와 달리, 풀/관목은 1초 만에 폭발적 착화(Flashover)가 발생하고 5~6초 내에 완전 전소됩니다.
/// 바람(Wind)을 타고 초당 8~12m 속도로 들판을 질주하여 인접한 숲(TacticalForestZone)으로 불을 옮겨붙이는
/// 화재 도화선(Wildfire Highway) 역할을 실시간 수행하며, 전소 시 바닥에 검은 잿더미 데칼(Ash Decal)을 남깁니다.
/// </summary>
[DisallowMultipleComponent]
public class ShrubFireController : MonoBehaviour
{
    [Header("🔥 연소 타이밍 설정")]
    [Tooltip("착화 지연 시간 (초) - 풀은 0.5~1초 내 즉각 발화")]
    public float ignitionDelay = 0.8f;

    [Tooltip("완전 전소 시간 (초) - 5~6초 내 메시 소멸")]
    public float totalBurnDuration = 5.5f;

    [Header("🌬️ 화재 고속도로 (Wildfire Highway) 전파")]
    [Tooltip("기본 전파 반경 (m)")]
    public float baseSpreadRadius = 4.5f;

    [Tooltip("풍향 순풍 전파 가중치 (m)")]
    public float windSpreadBonus = 4.0f;

    [Tooltip("전파 주기 (초)")]
    public float spreadInterval = 1.2f;

    [Header("⚔️ 전술 디버프")]
    [Tooltip("불타는 수풀 내부 유닛 초당 화염 피해 비율")]
    public float burnDamagePerSecond = 0.035f;

    [Tooltip("들불 패닉 사기 감소량")]
    public float wildfireMoralePenalty = -12.0f;

    [Header("상태 모니터링")]
    public float burnProgress = 0f;
    public bool isIgnited = false;
    public bool isFullyBurnt = false;

    private TacticalBushZone bushZone;
    private float burnTimer = 0f;
    private float spreadTimer = 0f;
    private float damageTickTimer = 0f;

    private GameObject activeFireVfxRoot;
    private Light activeFireLight;

    public void Init(TacticalBushZone zone)
    {
        bushZone = zone;
    }

    public void IgniteAt(Vector3 hitPoint)
    {
        Ignite(hitPoint);
    }

    /// <summary>
    /// 불화살 명중 또는 인접 화염원에 의한 착화 개시
    /// </summary>
    public void Ignite(Vector3 hitPoint)
    {
        if (isIgnited || isFullyBurnt) return;

        isIgnited = true;
        burnTimer = 0f;
        spreadTimer = 0f;

        if (bushZone != null)
        {
            bushZone.isBurning = true;
        }

        // 경량 화염 VFX 연동
        SpawnFireVFX(hitPoint);
    }

    private void Update()
    {
        if (!isIgnited || isFullyBurnt) return;

        burnTimer += Time.deltaTime;
        burnProgress = Mathf.Clamp01(burnTimer / totalBurnDuration);

        if (bushZone != null)
        {
            bushZone.burnProgress = burnProgress;
        }

        // 1. 바람 타고 인접 수풀 및 숲으로 화재 고속도로 전파
        spreadTimer += Time.deltaTime;
        if (spreadTimer >= spreadInterval && burnProgress > 0.2f && burnProgress < 0.85f)
        {
            spreadTimer = 0f;
            SpreadFireToNeighbors();
        }

        // 2. 내부 유닛 틱 피해 및 사기 패닉 부여
        damageTickTimer += Time.deltaTime;
        if (damageTickTimer >= 0.5f)
        {
            damageTickTimer = 0f;
            ApplyWildfireDebuffs();
        }

        // 3. 완전 전소 시 소멸 처리
        if (burnTimer >= totalBurnDuration)
        {
            CompleteBurn();
        }
    }

    /// <summary>
    /// 바람(Wind)을 타고 인접 관목 및 숲으로 불길 전파 (Wildfire Highway)
    /// </summary>
    private void SpreadFireToNeighbors()
    {
        Vector3 windVector = Vector3.forward;
        float windSpeed = 1.0f;

        if (TotalWarWindManager.Instance != null)
        {
            windVector = TotalWarWindManager.Instance.GetWindDirectionVector();
            windSpeed = TotalWarWindManager.Instance.windSpeed;
        }

        Vector3 myPos = transform.position;

        // 1) 인접한 다른 관목 구역 (TacticalBushZone) 탐색
        var otherBushZones = TacticalBushZone.ActiveBushZones;
        for (int i = 0; i < otherBushZones.Count; i++)
        {
            var neighbor = otherBushZones[i];
            if (neighbor == null || neighbor == bushZone || neighbor.isBurning) continue;

            Vector3 toNeighbor = (neighbor.transform.position - myPos);
            float dist = toNeighbor.magnitude;
            Vector3 dir = dist > 0.001f ? (toNeighbor / dist) : Vector3.forward;

            // 순풍 방향일수록 전파 거리 급증 공식
            float windDot = Vector3.Dot(dir, windVector.normalized);
            float effectiveRadius = baseSpreadRadius + (bushZone != null ? bushZone.radius : 15f) + Mathf.Max(0f, windDot) * windSpreadBonus * windSpeed;

            if (dist <= effectiveRadius)
            {
                neighbor.Ignite(neighbor.transform.position);
            }
        }

        // 2) 인접한 거목 숲 (TacticalForestZone)으로 화재 전달 (들판 -> 숲 화공 연결)
        var forestZones = TacticalForestZone.ActiveZones;
        for (int i = 0; i < forestZones.Count; i++)
        {
            var forest = forestZones[i];
            if (forest == null) continue;

            Vector3 toForest = (forest.transform.position - myPos);
            float dist = toForest.magnitude;
            Vector3 dir = dist > 0.001f ? (toForest / dist) : Vector3.forward;

            float windDot = Vector3.Dot(dir, windVector.normalized);
            float effectiveRadius = baseSpreadRadius + forest.radius * 0.7f + Mathf.Max(0f, windDot) * windSpreadBonus * windSpeed;

            if (dist <= effectiveRadius)
            {
                if (!forest.IgniteAt(myPos + windVector * 2.5f, 8.0f) && !forest.enableFireVFX)
                {
                    forest.IgniteForestFire();
                }
            }
        }

        // 3) 인접한 잔디 풀포기 (TacticalGrassRenderer)로 화재 전파 (🎲 확률 검사 통과 시에만 발화)
        if (TacticalGrassRenderer.Instance != null)
        {
            Vector3 grassTarget = myPos + windVector.normalized * Random.Range(1.8f, 3.8f);
            grassTarget.y = TerrainHeightManager.SampleHeightFast(grassTarget.x, grassTarget.z);
            TacticalGrassRenderer.Instance.IgnitePlantAt(grassTarget, 3.5f, true, myPos, windVector);
        }
    }

    /// <summary>
    /// 불타는 수풀 내부 부대에 지속 화염 틱 피해 및 [들불 패닉: 사기 -12] 부여
    /// </summary>
    private void ApplyWildfireDebuffs()
    {
        if (bushZone == null) return;

        // Squad 목록 순회
        var squads = FindObjectsByType<Squad>(FindObjectsSortMode.None);
        for (int i = 0; i < squads.Length; i++)
        {
            var sq = squads[i];
            if (sq == null) continue;

            if (bushZone.IsPositionInside(sq.transform.position))
            {
                // 화재 발생 시 은신 즉각 강제 해제
                sq.isSpotted = true;
                sq.isHiddenInForest = false;

                // 사기 패닉 충격 (Morale Shock)
                sq.currentMorale = Mathf.Max(0f, sq.currentMorale + wildfireMoralePenalty * 0.5f);
            }
        }
    }

    /// <summary>
    /// 완전 전소 완료: 화염 파티클 정지 및 토탈워식 3D 숯 잔해 안착 (점박이 원판 데칼 방지)
    /// </summary>
    private void CompleteBurn()
    {
        isFullyBurnt = true;
        if (bushZone != null)
        {
            bushZone.isBurning = false;
            bushZone.burnProgress = 1.0f;
            bushZone.providesStealth = false; // 은신처 완전 소멸
            bushZone.cavalrySpeedPenalty = 0f; // 잔해가 납작해져 이동 저항 해제
        }

        if (activeFireVfxRoot != null)
        {
            var psArr = activeFireVfxRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < psArr.Length; i++)
            {
                if (psArr[i] != null) psArr[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        if (activeFireLight != null)
        {
            activeFireLight.enabled = false;
        }

        // 토탈워식 3D 숯 잔해 안착
        SpawnAshDecal();
    }

    private void SpawnFireVFX(Vector3 point)
    {
        if (activeFireVfxRoot != null) return;

        Vector3 spawnPos = point.sqrMagnitude > 0.01f ? point : transform.position;
        spawnPos.y = TerrainHeightManager.SampleHeightFast(spawnPos.x, spawnPos.z);

        activeFireVfxRoot = new GameObject("Bush_Fire_VFX_Root");
        activeFireVfxRoot.transform.SetParent(transform, false);
        activeFireVfxRoot.transform.position = spawnPos;

        MeshRenderer targetMr = GetComponentInChildren<MeshRenderer>();
        float bushRadius = bushZone != null ? Mathf.Clamp(bushZone.radius * 0.35f, 1.2f, 2.2f) : 1.5f;
        TacticalGrassRenderer.ConfigureGrassTuftFireVFX(activeFireVfxRoot, targetMr, bushRadius, 1.35f, out activeFireLight);
    }

    private void SpawnAshDecal()
    {
        // 🪵 부시 3D 메쉬의 높이를 지상 15~20cm로 낮추어 납작한 숯 잔해로 보존
        var renderers = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var mr = renderers[i];
            if (mr == null) continue;
            var t = mr.transform;
            Vector3 s = t.localScale;
            t.localScale = new Vector3(s.x * 0.88f, s.y * 0.22f, s.z * 0.88f);
        }

        // 🪵 지형 곡면 밀착 흑색 잿더미 및 그을음(Ash Scorch) 메쉬 생성!
        float ashRadius = bushZone != null ? Mathf.Max(1.8f, bushZone.radius * 0.75f) : 2.5f;
        MiniTotalWar.TacticalGrassRenderer.SpawnTerrainConformingAshMark(transform.position, ashRadius, transform);
    }
}
