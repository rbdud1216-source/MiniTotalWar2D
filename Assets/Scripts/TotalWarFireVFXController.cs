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

            // 단독 프리팹 테스트 환경을 위한 체적/수종별 burnDuration 자동 보정
            if (targetRenderers != null && targetRenderers.Length > 0 && Mathf.Approximately(burnDuration, 40.0f))
            {
                Bounds b = targetRenderers[0].bounds;
                for (int i = 1; i < targetRenderers.Length; i++)
                {
                    if (targetRenderers[i] != null) b.Encapsulate(targetRenderers[i].bounds);
                }

                float r = Mathf.Max(b.extents.x, b.extents.z);
                float h = b.size.y;
                float vol = Mathf.Clamp((r / 2.5f) * (r / 2.5f) * (h / 5.0f), 0.35f, 3.2f);
                float duration = 40.0f * Mathf.Pow(vol, 0.40f);

                string nameLower = treeObj.name.ToLower();
                if (nameLower.Contains("bamboo")) duration *= 0.65f;
                else if (nameLower.Contains("pine")) duration *= 0.95f;
                else duration *= 1.15f;

                burnDuration = Mathf.Clamp(duration, 20.0f, 65.0f);
            }
        }
    }



    /// <summary>
    /// 외부에서 생성된 연기 파티클 시스템 등록
    /// </summary>
    public void RegisterSmokeParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        psSmoke = ps;
        initialSmokeRate = ps.emission.rateOverTime.constant;
    }

    /// <summary>
    /// 외부에서 생성된 불티 파티클 시스템 등록
    /// </summary>
    public void RegisterEmbersParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        psEmbers = ps;
        initialEmbersRate = ps.emission.rateOverTime.constant;
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
    /// <summary>
    /// 외부에서 나뭇잎 표면에 생성된 화염 파티클 시스템을 명시적으로 등록
    /// </summary>
    public void RegisterFlameParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        if (!psFlamesList.Contains(ps))
        {
            if (psFlames == null) psFlames = ps;
            psFlamesList.Add(ps);
            float rate = ps.emission.rateOverTime.constant;
            initialFlamesRates.Add(rate > 0.01f ? rate : 42f);
            initialFlamesRate = rate > 0.01f ? rate : 42f;
        }
    }


    /// <summary>
    /// 삼국지 토탈워 본편 공식 화재 진화 5단계 수명주기 (나뭇잎 연소 생동감 극대화):
    /// 1. [착화 전 훈연 (0.0 ~ 0.04, 약 1초)]: 불꽃 0개! 나무에서 불은 안 보이고 연기만 먼저 스멀스멀 피어오름
    /// 2. [초기 발화 (0.04 ~ 0.10, 약 1~3초)]: 연기 속에서 첫 불씨 2~4개가 톡톡 발화
    /// 3. [불길 증식 및 급격 확산 (0.10 ~ 0.35, 약 3~10초)]: 불의 숫자가 폭발적으로 늘어나며 수관 전역이 맹렬한 불바다로 변모
    /// 4. [절정 연소 및 디졸브 (0.35 ~ 0.65, 약 10~18초)]: 수관 전체 화염 최대치 + 나뭇잎이 불꽃 속에서 자연스럽게 전소
    /// 5. [전소 및 소멸 (0.65 ~ 0.90)]: 나뭇잎 소멸(0.65) 시 화염 즉시 0 차단, 앙상한 숯 가지만 남고 잔여 실연기 후 완전 종료
    /// </summary>
    private void UpdateParticleLifecycle()
    {
        // 1. 화염 (Flames) 제어: 착화 전 연기 선행 -> 점진적 개수 증식 -> 전소 즉시 0 차단
        for (int i = 0; i < psFlamesList.Count; i++)
        {
            var flamePs = psFlamesList[i];
            if (flamePs == null) continue;
            float baseRate = (i < initialFlamesRates.Count) ? initialFlamesRates[i] : initialFlamesRate;

            var emission = flamePs.emission;
            if (burnProgress >= 0.65f)
            {
                // [5단계: 전소] 나뭇잎 소멸 시점에 허공 불꽃 100% 즉시 차단
                emission.rateOverTime = 0f;
                if (flamePs.isPlaying) flamePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (burnProgress >= 0.35f)
            {
                // [4단계: 절정 연소] 수관 전체를 집어삼킨 맹렬한 화염 (기본의 1.45배)
                emission.rateOverTime = baseRate * 1.45f;
            }
            else if (burnProgress >= 0.10f)
            {
                // [3단계: 불의 숫자가 폭발적으로 늘어남!] 0.4배 -> 1.45배로 나뭇잎 전역에 불꽃 급증식
                float t = (burnProgress - 0.10f) / 0.25f;
                emission.rateOverTime = Mathf.Lerp(baseRate * 0.40f, baseRate * 1.45f, t);
            }
            else if (burnProgress >= 0.04f)
            {
                // [2단계: 최초 발화] 연기 속에서 첫 불씨들이 톡톡 튀어나오며 착화 개시
                float t = (burnProgress - 0.04f) / 0.06f;
                emission.rateOverTime = Mathf.Lerp(baseRate * 0.15f, baseRate * 0.40f, t);
            }
            else
            {
                // [1단계: 착화 전 훈연 (약 1초)] 🔥 불은 전혀 없고 연기만 먼저 남!
                emission.rateOverTime = 0f;
            }
        }

        // 2. 불티 (Embers) 제어: 불이 붙는 0.05부터 발생, 0.65에서 차단
        if (psEmbers != null)
        {
            var emission = psEmbers.emission;
            if (burnProgress >= 0.65f || burnProgress < 0.05f)
            {
                emission.rateOverTime = 0f;
                if (burnProgress >= 0.65f && psEmbers.isPlaying) 
                    psEmbers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (burnProgress >= 0.40f)
            {
                float decay = 1.0f - ((burnProgress - 0.40f) / 0.25f);
                emission.rateOverTime = Mathf.Max(0f, initialEmbersRate * decay);
            }
            else if (burnProgress >= 0.10f)
            {
                float grow = (burnProgress - 0.10f) / 0.30f;
                emission.rateOverTime = Mathf.Lerp(initialEmbersRate * 0.30f, initialEmbersRate, grow);
            }
            else
            {
                float grow = (burnProgress - 0.05f) / 0.05f;
                emission.rateOverTime = Mathf.Lerp(0f, initialEmbersRate * 0.30f, grow);
            }
        }

        // 3. 연기 (Smoke) 제어: 불 나기 전(0.0~0.12)부터 선행 훈연 -> 화재 절정 -> 전소 후 실연기 -> 완전 소멸
        if (psSmoke != null)
        {
            var emission = psSmoke.emission;
            if (burnProgress >= 0.90f)
            {
                // 완전 전소 완료 시 연기 완전 차단 및 정지
                emission.rateOverTime = 0f;
                if (psSmoke.isPlaying) psSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (burnProgress >= 0.68f)
            {
                // 앙상하게 탄 숯가지만 남은 단계: 실낱같이 가느다랗게 피어오르는 미세 잔여 훈연
                float remain = 1.0f - ((burnProgress - 0.68f) / 0.22f);
                emission.rateOverTime = Mathf.Max(0.5f, 2.0f * remain);
            }
            else if (burnProgress >= 0.50f)
            {
                // 나뭇잎 소진에 따라 연기 발생량이 서서히 잦아듦
                float decay = 1.0f - ((burnProgress - 0.50f) / 0.18f);
                emission.rateOverTime = Mathf.Lerp(2.0f, initialSmokeRate * 1.25f, decay);
            }
            else if (burnProgress >= 0.10f)
            {
                // 불길 확산 및 절정 단계: 풍성한 훈연 구름
                emission.rateOverTime = initialSmokeRate * 1.25f;
            }
            else
            {
                // [착화 전 훈연 및 초기 발화 단계]: 불은 없거나 작은데 연기부터 뭉게뭉게 피어오름!
                emission.rateOverTime = initialSmokeRate * 0.95f;
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
    /// 삼국지 토탈워 본편 공식: 풍향에 따라 연기 기둥이 순풍 쪽으로 비스듬히 눕고,
    /// 불티(Embers)가 돌풍(Gust)을 타고 공중으로 흩날리는 전장 동역학 구현
    /// </summary>
    private void UpdateWindForces()
    {
        if (!reactToWind || TotalWarWindManager.Instance == null) return;

        var wind = TotalWarWindManager.Instance;
        float windAngleRad = wind.windAngle * Mathf.Deg2Rad;
        // 월드 공간 기준 풍향 벡터 (Sin: X, Cos: Z)
        Vector3 windDir = new Vector3(Mathf.Sin(windAngleRad), 0.0f, Mathf.Cos(windAngleRad));

        // 돌풍(Gust) 및 실효 풍력 계수 계산
        float gustFactor = 1.0f;
        if (wind.enableGust)
        {
            float timeVal = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup;
            float noise = Mathf.PerlinNoise(timeVal * wind.gustFrequency, 0.5f);
            gustFactor = Mathf.Lerp(0.75f, wind.gustMultiplier, noise);
        }
        float effectiveWindPower = wind.windSpeed * wind.windStrength * gustFactor;

        // 1. 연기 구름(Smoke): 바람에 순응하여 순풍 방향으로 기울어지면서도 열기 상승 부력 유지
        if (psSmoke != null)
        {
            var forceModule = psSmoke.forceOverLifetime;
            if (!forceModule.enabled) forceModule.enabled = true;
            forceModule.space = ParticleSystemSimulationSpace.World; // [핵심] 월드 풍향 1:1 일치 보장

            // [핵심 해결: 수직 치솟음 차단] 수직 상승 외력을 1.35f -> 0.25f로 대폭 완화
            // 연기가 위로 솟구치지 않고, 나뭇잎에서 스멀스멀 흘러나와 순풍 방향으로 낮게 드리우며 흐르도록 유도
            Vector3 force = windDir * (effectiveWindPower * windInfluenceOnSmoke * 1.8f) + new Vector3(0f, 0.25f, 0f);
            forceModule.x = new ParticleSystem.MinMaxCurve(force.x);
            forceModule.y = new ParticleSystem.MinMaxCurve(force.y);
            forceModule.z = new ParticleSystem.MinMaxCurve(force.z);
        }

        // 2. 불티(Embers): 질량이 가벼워 바람을 타며 돌풍에 민감하게 사선 상공으로 비산
        if (psEmbers != null)
        {
            var forceModule = psEmbers.forceOverLifetime;
            if (!forceModule.enabled) forceModule.enabled = true;
            forceModule.space = ParticleSystemSimulationSpace.World; // [핵심] 월드 풍향 1:1 일치 보장

            Vector3 force = windDir * (effectiveWindPower * windInfluenceOnEmbers * 2.4f) + new Vector3(0f, 0.35f, 0f);
            forceModule.x = new ParticleSystem.MinMaxCurve(force.x);
            forceModule.y = new ParticleSystem.MinMaxCurve(force.y);
            forceModule.z = new ParticleSystem.MinMaxCurve(force.z);
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