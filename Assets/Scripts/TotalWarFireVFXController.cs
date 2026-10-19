using UnityEngine;

/// <summary>
/// 🔥 [삼국지 토탈워 전장 화재 및 수목 전소 컨트롤러 (Total War Fire VFX Controller)]
/// 나무에 부착되어 화염(Flames), 연기(Smoke), 불티(Embers), 지면 불씨(Ground Embers) 파티클을 방출하며,
/// 실제 삼탈워 화재 시스템과 동일하게 시간에 따른 나뭇잎 알파 디졸브(Dissolve),
/// 이글거리는 불씨(Ember Glow), 그리고 최종 전소(Complete Burnout) 라이프사이클을 구동합니다.
/// 나무 기둥(Trunk)은 원래 수피를 유지하여 전소 시 앙상한 겨울 나뭇가지 골격이 그대로 남습니다.
/// </summary>
[ExecuteAlways]
public class TotalWarFireVFXController : MonoBehaviour
{
    [Header("⏳ 삼탈워 전소 수명주기 (Burn Lifecycle)")]
    [Tooltip("실시간 나무 연소 및 나뭇잎 디졸브 진행 활성화")]
    public bool enableBurnProgression = true;

    [Tooltip("완전 전소까지 소요되는 총 연소 시간 (단위: 초, 기본: 40초)")]
    [Range(5f, 120f)]
    public float burnDuration = 40.0f;

    [Tooltip("현재 나무 연소 진행도 (0.0 = 정상 수목, 1.0 = 잎사귀 완전 소멸)")]
    [Range(0f, 1f)]
    public float burnProgress = 0.0f;

    [Header("🌲 연소 대상 나무 (Target Tree Renderers)")]
    [Tooltip("나뭇잎 디졸브가 적용될 타겟 나무의 렌더러 목록")]
    public Renderer[] targetRenderers;

    [Header("🔥 화염 조명 (Fire Light Flicker)")]
    public bool enableFireLight = true;
    public float baseLightIntensity = 3.5f;
    public float flickerSpeed = 7.0f;
    public float flickerRange = 0.95f;

    [Header("💨 바람 연동 (Wind Reaction)")]
    public bool reactToWind = true;
    public float windInfluenceOnSmoke = 1.4f;
    public float windInfluenceOnEmbers = 2.2f;

    // 내부 컴포넌트 캐시
    private Light fireLight;
    private ParticleSystem psFlames;
    private readonly System.Collections.Generic.List<ParticleSystem> psFlamesList = new System.Collections.Generic.List<ParticleSystem>();
    private readonly System.Collections.Generic.List<float> initialFlamesRates = new System.Collections.Generic.List<float>();
    private ParticleSystem psSmoke;
    private ParticleSystem psEmbers;
    private ParticleSystem psGroundEmbers;
    private MaterialPropertyBlock mpb;

    // 파티클 기본 방출량 캐시
    private float initialFlamesRate = 42f;
    private float initialSmokeRate = 18f;
    private float initialEmbersRate = 22f;
    private float initialGroundEmbersRate = 14f;

    private static readonly int BurnProgressPropId = Shader.PropertyToID("_BurnProgress");

    private void Awake()
    {
        InitComponents();
    }

    private void OnEnable()
    {
        InitComponents();
    }

    private void OnValidate()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            FindNearbyTreeRenderers();
        }
        UpdateTreeMaterialProperties();
        UpdateParticleLifecycle();
    }

    /// <summary>
    /// 외부(TacticalForestZone 등)에서 타겟 나무의 렌더러를 명시적으로 등록
    /// </summary>
    public void SetTargetTree(GameObject treeObj)
    {
        if (treeObj != null)
        {
            targetRenderers = treeObj.GetComponentsInChildren<Renderer>(true);
            UpdateTreeMaterialProperties();
        }
    }

    private void InitComponents()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();

        psFlamesList.Clear();
        initialFlamesRates.Clear();

        // 1. 자식 파티클 시스템 탐색 및 캐싱 (다중 나뭇가지 화염 클러스터 지원)
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            string psName = ps.gameObject.name.ToLower();
            if (psName.Contains("flame"))
            {
                if (psFlames == null) psFlames = ps;
                psFlamesList.Add(ps);
                initialFlamesRates.Add(ps.emission.rateOverTime.constant);
                initialFlamesRate = ps.emission.rateOverTime.constant;
            }
            else if (psName.Contains("smoke"))
            {
                psSmoke = ps;
                initialSmokeRate = ps.emission.rateOverTime.constant;
            }
            else if (psName.Contains("ground"))
            {
                psGroundEmbers = ps;
                initialGroundEmbersRate = ps.emission.rateOverTime.constant;
            }
            else if (psName.Contains("ember"))
            {
                psEmbers = ps;
                initialEmbersRate = ps.emission.rateOverTime.constant;
            }
        }

        // 2. 화염 포인트 라이트 생성 및 초기화
        if (enableFireLight)
        {
            fireLight = GetComponentInChildren<Light>();
            if (fireLight == null)
            {
                GameObject lightGo = new GameObject("Fire_PointLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                fireLight = lightGo.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.range = 10f;
                fireLight.color = new Color(1.0f, 0.45f, 0.12f);
                fireLight.intensity = baseLightIntensity;
                fireLight.shadows = LightShadows.None;
            }
        }

        // 3. 타겟 나무 렌더러 자동 탐색 (미지정 시 주변 2.5m 이내 가장 가까운 나무 렌더러 수집)
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            FindNearbyTreeRenderers();
        }

        UpdateTreeMaterialProperties();
    }

    private void FindNearbyTreeRenderers()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 7.5f);
        foreach (var hit in hits)
        {
            if (hit.gameObject.name.Contains("Tree") || (hit.transform.parent != null && hit.transform.parent.name.Contains("Tree")))
            {
                Transform treeRoot = hit.gameObject.name.Contains("Tree") ? hit.transform : hit.transform.parent;
                targetRenderers = treeRoot.GetComponentsInChildren<Renderer>(true);
                return;
            }
        }

        // 부모 트랜스폼 계층 구조에서도 검색
        Transform current = transform.parent;
        while (current != null)
        {
            if (current.name.Contains("Tree"))
            {
                targetRenderers = current.GetComponentsInChildren<Renderer>(true);
                return;
            }
            current = current.parent;
        }
    }

    private void Update()
    {
        // 1. 연소 수명주기 시간 갱신 (Play 모드에서 시간 비례 진행)
        if (enableBurnProgression && burnDuration > 0.01f)
        {
            if (Application.isPlaying)
            {
                burnProgress = Mathf.Clamp01(burnProgress + Time.deltaTime / burnDuration);
            }
        }

        // 2. 나무 렌더러에 탄화/디졸브 파라미터 실시간 주입
        UpdateTreeMaterialProperties();

        // 3. 연소 진행도에 따른 파티클 방출량 및 화염 상태 제어
        UpdateParticleLifecycle();

        // 4. 화염 조명 깜빡임 (Flickering Light)
        UpdateFireLight();

        // 5. 전장 풍향/풍속 동기화
        UpdateWindForces();
    }

    /// <summary>
    /// MaterialPropertyBlock을 통해 타겟 나무에 _BurnProgress를 즉시 주입하여
    /// 나뭇잎 알파 디졸브를 실시간으로 렌더링
    /// </summary>
    private void UpdateTreeMaterialProperties()
    {
        if (targetRenderers == null || targetRenderers.Length == 0) return;

        if (mpb == null) mpb = new MaterialPropertyBlock();

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer ren = targetRenderers[i];
            if (ren == null) continue;

            ren.GetPropertyBlock(mpb);
            mpb.SetFloat(BurnProgressPropId, burnProgress);
            ren.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// 삼탈워 공식 화재 수명주기 파티클 제어 (2026-10-19 13-14-08 영상 일치화):
    /// - 0.0 ~ 0.35: 나뭇잎 착화 및 화염 치환 단계 (나뭇잎이 불로 대체되듯이 불꽃이 맹렬히 커짐)
    /// - 0.35 ~ 0.70: 절정 연소 및 기둥 숯 탄화 단계 (나뭇잎 소멸 디졸브 + 기둥/가지가 검은 숯으로 탄화)
    /// - 0.70 ~ 0.95: 연료(잎사귀) 소진 단계 (화염이 잦아들고 연기가 지배)
    /// - 0.98 ~ 1.0: 완전 전소 (화염 소멸, 앙상하게 탄화된 잿더미 나무 골격 완성)
    /// </summary>
    private void UpdateParticleLifecycle()
    {
        // 화염 (Flames) 제어: 나뭇잎이 불로 대체되듯이 피크를 이루다 잎이 사라지면 감쇠
        for (int i = 0; i < psFlamesList.Count; i++)
        {
            var flamePs = psFlamesList[i];
            if (flamePs == null) continue;
            float baseRate = (i < initialFlamesRates.Count) ? initialFlamesRates[i] : initialFlamesRate;

            var emission = flamePs.emission;
            if (burnProgress >= 0.96f)
            {
                emission.rateOverTime = 0f;
                if (flamePs.isPlaying) flamePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (burnProgress >= 0.70f)
            {
                float decay = 1.0f - ((burnProgress - 0.70f) / 0.26f);
                emission.rateOverTime = Mathf.Max(0f, baseRate * 1.35f * decay);
            }
            else if (burnProgress >= 0.35f)
            {
                // 절정 연소 피크 (나뭇잎 층 전체를 집어삼키는 화염)
                emission.rateOverTime = baseRate * 1.35f;
            }
            else
            {
                // 초반: 나무 착화 즉시 나뭇잎 층 전체를 불꽃으로 대체하며 격렬히 점화
                float grow = Mathf.Lerp(1.1f, 1.45f, burnProgress / 0.35f);
                emission.rateOverTime = baseRate * grow;
            }
        }

        // 불티 (Embers) 제어
        if (psEmbers != null)
        {
            var emission = psEmbers.emission;
            if (burnProgress >= 0.95f)
            {
                emission.rateOverTime = 0f;
                if (psEmbers.isPlaying) psEmbers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (burnProgress >= 0.65f)
            {
                float decay = 1.0f - ((burnProgress - 0.65f) / 0.30f);
                emission.rateOverTime = Mathf.Max(0f, initialEmbersRate * decay);
            }
            else
            {
                emission.rateOverTime = initialEmbersRate;
            }
        }

        // 연기 (Smoke) 제어
        if (psSmoke != null)
        {
            var emission = psSmoke.emission;
            if (burnProgress >= 1.0f)
            {
                // 전소 후 앙상한 가지에서 가느다랗게 피어오르는 잔여 연기
                emission.rateOverTime = Mathf.Min(3.0f, initialSmokeRate * 0.25f);
            }
            else if (burnProgress >= 0.50f)
            {
                emission.rateOverTime = initialSmokeRate * 1.25f;
            }
            else
            {
                emission.rateOverTime = initialSmokeRate;
            }
        }

        // 지면 불씨 (Ground Embers) 제어
        if (psGroundEmbers != null)
        {
            var emission = psGroundEmbers.emission;
            if (burnProgress >= 0.98f)
            {
                emission.rateOverTime = 0f;
                if (psGroundEmbers.isPlaying) psGroundEmbers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (burnProgress >= 0.70f)
            {
                float decay = 1.0f - ((burnProgress - 0.70f) / 0.28f);
                emission.rateOverTime = Mathf.Max(0f, initialGroundEmbersRate * decay);
            }
            else
            {
                emission.rateOverTime = initialGroundEmbersRate;
            }
        }
    }

    /// <summary>
    /// 화염 조명 깜빡임 및 전소 시 페이드아웃
    /// </summary>
    private void UpdateFireLight()
    {
        if (fireLight == null || !enableFireLight) return;

        if (burnProgress >= 0.98f)
        {
            fireLight.enabled = false;
            return;
        }

        fireLight.enabled = true;

        // [빛 연출 강화] 다층 퍼린 노이즈(저주파 호흡 + 고주파 불티)로 자연스러운 모닥불 일렁임 구현
        float t = Time.time * flickerSpeed;
        float noiseLow = Mathf.PerlinNoise(t * 0.6f, 0.0f);
        float noiseHigh = Mathf.PerlinNoise(t * 1.8f, 13.5f);
        float combinedNoise = (noiseLow * 0.7f + noiseHigh * 0.3f);

        float currentBase = baseLightIntensity;

        // 전소 막바지에는 조명이 어두워짐
        if (burnProgress >= 0.60f)
        {
            currentBase *= (1.0f - (burnProgress - 0.60f) / 0.38f);
        }

        fireLight.intensity = Mathf.Max(0.2f, currentBase + (combinedNoise - 0.5f) * flickerRange * 2.5f);
    }

    /// <summary>
    /// 전장 풍향/풍속에 따른 파티클 외력(Force over Lifetime) 실시간 동기화
    /// </summary>
    private void UpdateWindForces()
    {
        if (!reactToWind || TotalWarWindManager.Instance == null) return;

        float windAngleRad = TotalWarWindManager.Instance.windAngle * Mathf.Deg2Rad;
        float speed = TotalWarWindManager.Instance.windSpeed;
        Vector3 windDir = new Vector3(Mathf.Sin(windAngleRad), 0.0f, Mathf.Cos(windAngleRad));

        if (psSmoke != null)
        {
            var forceModule = psSmoke.forceOverLifetime;
            if (!forceModule.enabled) forceModule.enabled = true;
            Vector3 force = windDir * (speed * windInfluenceOnSmoke) + new Vector3(0f, 1.0f, 0f);
            forceModule.x = force.x;
            forceModule.y = force.y;
            forceModule.z = force.z;
        }

        if (psEmbers != null)
        {
            var forceModule = psEmbers.forceOverLifetime;
            if (!forceModule.enabled) forceModule.enabled = true;
            Vector3 force = windDir * (speed * windInfluenceOnEmbers) + new Vector3(0f, 0.4f, 0f);
            forceModule.x = force.x;
            forceModule.y = force.y;
            forceModule.z = force.z;
        }
    }

    /// <summary>
    /// 화재를 초기화하여 정상 수목으로 원복
    /// </summary>
    [ContextMenu("🧯 화재 초기화 및 수목 복원 (Reset Fire)")]
    public void ResetFireState()
    {
        burnProgress = 0f;
        UpdateTreeMaterialProperties();

        if (psFlames != null)
        {
            var emission = psFlames.emission;
            emission.rateOverTime = initialFlamesRate;
            psFlames.Play();
        }
        if (psSmoke != null)
        {
            var emission = psSmoke.emission;
            emission.rateOverTime = initialSmokeRate;
            psSmoke.Play();
        }
        if (psEmbers != null)
        {
            var emission = psEmbers.emission;
            emission.rateOverTime = initialEmbersRate;
            psEmbers.Play();
        }
        if (psGroundEmbers != null)
        {
            var emission = psGroundEmbers.emission;
            emission.rateOverTime = initialGroundEmbersRate;
            psGroundEmbers.Play();
        }
        if (fireLight != null)
        {
            fireLight.enabled = enableFireLight;
        }
    }
}