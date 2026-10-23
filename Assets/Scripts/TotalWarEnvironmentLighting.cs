using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ☀️ [삼국지 토탈워 환경 조명 및 스카이박스 시스템 (Total War Environment Lighting)]
/// 삼탈워 본편 라이팅 스펙(고도각 40도, 방위각 90도, 6방향 지면 반사광)과
/// 4K 파노라마 푸른 하늘 스카이박스(3k_sky_blue_1_diffuse.dds) 및
/// 실시간 동적 태양 광구 & 대기 코로나 후광(sun_shader_sun),
/// 그리고 영화적 감성의 시네마틱 후처리(ACES 톤매핑, 소프트 블룸, SMAA)를 전장에 자동 적용합니다.
/// </summary>
[ExecuteAlways]
public class TotalWarEnvironmentLighting : MonoBehaviour
{
    [Header("🎥 삼탈워 시네마틱 후처리 (Post-Processing & Atmosphere)")]
    [Tooltip("카메라 후처리(ACES 톤매핑, 소프트 블룸, 안티앨리어싱) 자동 적용")]
    public bool enablePostProcessing = true;

    [Tooltip("ACES 필름 톤매핑 활성화 (형광 쨍함 억제 및 실사 색감)")]
    public bool enableAcesTonemapping = true;

    [Tooltip("부드러운 대기 화염 블룸 강도")]
    [Range(0.0f, 2.0f)]
    public float bloomIntensity = 0.85f;

    [Tooltip("불꽃 대기 산란 반경 (Scatter)")]
    [Range(0.2f, 0.95f)]
    public float bloomScatter = 0.78f;

    [Header("☀️ 삼탈워 태양광 설정 (Sun Light)")]
    [Tooltip("태양 고도각 (Elevation, 38도: 전장 지형 그림자 길이를 단축하고 골고루 비춤)")]
    [Range(5f, 85f)]
    public float sunElevation = 38.0f;

    [Tooltip("태양 방위각 (Azimuth, 245도: 카메라 쪽을 향해 화사한 순광/사광 유도)")]
    [Range(0f, 360f)]
    public float sunAzimuth = 245.0f;

    [Tooltip("스카이박스 구름 햇살과 태양 광원 방향 자동 결속")]
    public bool lockSunToSkyRotation = false;

    [Tooltip("태양광 밝기 (Intensity)")]
    [Range(0.5f, 3.0f)]
    public float sunIntensity = 1.35f;

    [Tooltip("태양광 그림자 강도 (Shadow Strength: 0.70~0.75로 낮춰 암부 뭉개짐 방지)")]
    [Range(0.3f, 1.0f)]
    public float sunShadowStrength = 0.72f;

    [Tooltip("태양광 색상 (따뜻한 햇살 골드 화이트)")]
    public Color sunColor = new Color(1.0f, 0.96f, 0.88f);

    [Header("☀️ 삼탈워 태양 광구 & 대기 코로나 (Sun Disk & Corona)")]
    [Tooltip("태양 광구 디스크 활성화")]
    public bool enableSunDisk = true;

    [Tooltip("태양 광구 원반 크기 (Angular Radius, 기본: 0.005)")]
    [Range(0.002f, 0.08f)]
    public float sunDiskSize = 0.005f;

    [Tooltip("태양 광구 색상 (HDR 온백색 찬란한 골드)")]
    public Color sunDiskColor = new Color(2.5f, 2.3f, 1.8f, 1f);

    [Tooltip("태양 대기 코로나 후광 색상 (Atmospheric Corona)")]
    public Color sunCoronaColor = new Color(1.0f, 0.82f, 0.55f, 1f);

    [Tooltip("태양 대기 후광 퍼짐 지수 (수치가 높을수록 중심에 집중)")]
    [Range(1f, 32f)]
    public float sunCoronaFalloff = 5.0f;

    [Header("🌌 삼탈워 스카이박스 설정 (Skybox)")]
    [Tooltip("스카이박스 노출도 (Exposure)")]
    [Range(0.5f, 2.5f)]
    public float skyExposure = 1.05f;

    [Tooltip("스카이박스 수평 회전각 (Yaw 0~360도, 텍스처 햇살과 광원 방향 일치)")]
    [Range(0f, 360f)]
    public float skyRotation = 0.0f;

    [Header("🌫️ 삼탈워 지평선 대기 안개 (Horizon Fog)")]
    [Tooltip("수평선 높이 오프셋 (지평선 위치 조절)")]
    [Range(-0.3f, 0.3f)]
    public float horizonOffset = 0.0f;

    [Tooltip("지평선 대기 안개 강도 (지형과 하늘 경계 융합)")]
    [Range(0.0f, 1.0f)]
    public float horizonFogIntensity = 0.55f;

    [Tooltip("지평선 안개 퍼짐 높이")]
    [Range(0.05f, 0.5f)]
    public float horizonFogHeight = 0.22f;

    [Tooltip("지평선 안개 색상 (대기 원경 연무빛)")]
    public Color horizonFogColor = new Color(0.72f, 0.75f, 0.78f, 1f);

    [Tooltip("사용할 삼탈워 스카이박스 텍스처 파일명")]
    public string skyboxTextureName = "3k_sky_blue_1_diffuse.dds";

    [Header("🌍 삼탈워 환경광 (Ambient Trilight - 지면 반사광)")]
    [Tooltip("하늘 방향 환경광 (Sky Ambient - 맑고 화사한 스카이라이트)")]
    public Color ambientSky = new Color(0.55f, 0.68f, 0.86f);

    [Tooltip("수평선 방향 대기 산란광 (Equator Ambient - 부드러운 대기광)")]
    public Color ambientEquator = new Color(0.62f, 0.62f, 0.58f);

    [Tooltip("지면 반사광 (Ground Bounce - 나뭇잎 아래쪽을 비추는 따뜻한 대지 바운스)")]
    public Color ambientGround = new Color(0.52f, 0.48f, 0.38f);

    [Tooltip("환경광 전체 강도 배율")]
    [Range(0.5f, 2.5f)]
    public float ambientIntensity = 1.55f;

    private static Material skyboxMaterialInstance;
    private Light currentSunLight;

    private void Awake()
    {
        ApplyLightingAndSkybox();
    }

    private void Update()
    {
        // 1. 태양광 (Directional Light) 실시간 회전 및 조명 동기화
        if (currentSunLight != null)
        {
            if (lockSunToSkyRotation)
            {
                sunAzimuth = skyRotation;
            }

            currentSunLight.transform.rotation = Quaternion.Euler(sunElevation, sunAzimuth, 0f);
            currentSunLight.intensity = sunIntensity;
            currentSunLight.color = sunColor;
            currentSunLight.shadowStrength = sunShadowStrength;

            if (skyboxMaterialInstance != null)
            {
                Vector3 sunDir = -currentSunLight.transform.forward;
                skyboxMaterialInstance.SetVector("_SunDirection", sunDir);
            }
        }

        // 앰비언트 라이트 실시간 동기화
        RenderSettings.ambientSkyColor = ambientSky * ambientIntensity;
        RenderSettings.ambientEquatorColor = ambientEquator * ambientIntensity;
        RenderSettings.ambientGroundColor = ambientGround * ambientIntensity;
        RenderSettings.ambientIntensity = ambientIntensity;

        // 2. 스카이박스 파라미터 실시간 동기화
        if (skyboxMaterialInstance != null)
        {
            skyboxMaterialInstance.SetFloat("_SkyRotation", skyRotation);
            skyboxMaterialInstance.SetFloat("_HorizonOffset", horizonOffset);
            skyboxMaterialInstance.SetFloat("_HorizonFogIntensity", horizonFogIntensity);
            skyboxMaterialInstance.SetFloat("_HorizonFogHeight", horizonFogHeight);
            skyboxMaterialInstance.SetColor("_HorizonFogColor", horizonFogColor);
            skyboxMaterialInstance.SetFloat("_Exposure", skyExposure);
            skyboxMaterialInstance.SetFloat("_EnableSun", enableSunDisk ? 1.0f : 0.0f);
            skyboxMaterialInstance.SetColor("_SunColor", sunDiskColor);
            skyboxMaterialInstance.SetFloat("_SunSize", sunDiskSize);
            skyboxMaterialInstance.SetColor("_SunGlowColor", sunCoronaColor);
            skyboxMaterialInstance.SetFloat("_SunGlowExponent", sunCoronaFalloff);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoApplyOnSceneLoad()
    {
        var existing = FindFirstObjectByType<TotalWarEnvironmentLighting>();
        if (existing == null)
        {
            GameObject lightingGo = new GameObject("TotalWar_EnvironmentLighting");
            existing = lightingGo.AddComponent<TotalWarEnvironmentLighting>();
        }
        existing.ApplyLightingAndSkybox();
    }

    [ContextMenu("☀️ 삼탈워 라이팅 및 스카이박스 즉시 적용")]
    public void ApplyLightingAndSkybox()
    {
        // 1. 태양광 (Directional Light) 탐색 또는 신규 생성
        Light sunLight = RenderSettings.sun;
        if (sunLight == null)
        {
            Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    break;
                }
            }
        }

        if (sunLight == null)
        {
            GameObject sunGo = new GameObject("Directional Light (Sun - Total War)");
            sunLight = sunGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
        }

        currentSunLight = sunLight;

        // 삼탈워 공식 태양광 각도 및 강도 적용
        sunLight.transform.rotation = Quaternion.Euler(sunElevation, sunAzimuth, 0f);
        sunLight.color = sunColor;
        sunLight.intensity = sunIntensity;
        sunLight.shadows = LightShadows.Soft;
        sunLight.shadowStrength = sunShadowStrength;

        // 2. 삼탈워 4K 파노라마 + 동적 태양 셰이더 (Skybox/TotalWarPanoramicWithSun) 적용
        Shader skyShader = Shader.Find("Skybox/TotalWarPanoramicWithSun") ?? Shader.Find("Skybox/Panoramic");
        if (skyShader != null)
        {
            if (skyboxMaterialInstance == null || skyboxMaterialInstance.shader != skyShader)
            {
                skyboxMaterialInstance = new Material(skyShader)
                {
                    name = "TotalWar_Skybox_Sun_Material"
                };
            }

#if UNITY_EDITOR
            string texPath = "Assets/TotalWar_ThreeKingdoms_Extracted/Environment/Skyboxes/" + skyboxTextureName;
            Texture2D skyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (skyTex != null)
            {
                skyboxMaterialInstance.SetTexture("_MainTex", skyTex);
                skyboxMaterialInstance.SetFloat("_Exposure", skyExposure);
                skyboxMaterialInstance.SetColor("_Tint", Color.white);
                skyboxMaterialInstance.SetFloat("_SkyRotation", skyRotation);
                skyboxMaterialInstance.SetFloat("_HorizonOffset", horizonOffset);
                skyboxMaterialInstance.SetFloat("_HorizonFogIntensity", horizonFogIntensity);
                skyboxMaterialInstance.SetFloat("_HorizonFogHeight", horizonFogHeight);
                skyboxMaterialInstance.SetColor("_HorizonFogColor", horizonFogColor);

                // 삼탈워 sun_shader_sun 태양 합성 파라미터 전달
                Vector3 sunDir = -sunLight.transform.forward;
                skyboxMaterialInstance.SetFloat("_EnableSun", enableSunDisk ? 1.0f : 0.0f);
                skyboxMaterialInstance.SetVector("_SunDirection", sunDir);
                skyboxMaterialInstance.SetColor("_SunColor", sunDiskColor);
                skyboxMaterialInstance.SetFloat("_SunSize", sunDiskSize);
                skyboxMaterialInstance.SetFloat("_SunEdgeSoftness", 0.008f);
                skyboxMaterialInstance.SetColor("_SunGlowColor", sunCoronaColor);
                skyboxMaterialInstance.SetFloat("_SunGlowExponent", sunCoronaFalloff);
                skyboxMaterialInstance.SetFloat("_SunGlowIntensity", 1.25f);

                RenderSettings.skybox = skyboxMaterialInstance;
            }
#endif
        }

        // 3. 삼탈워 6방향 큐브 반사광 기반 앰비언트 라이트 설정
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky * ambientIntensity;
        RenderSettings.ambientEquatorColor = ambientEquator * ambientIntensity;
        RenderSettings.ambientGroundColor = ambientGround * ambientIntensity;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.sun = sunLight;

        // 4. 메인 카메라 Clear Flags 및 URP 시네마틱 후처리 활성화
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = FindFirstObjectByType<Camera>();
        }
        if (mainCam != null)
        {
            if (mainCam.clearFlags != CameraClearFlags.Skybox)
            {
                mainCam.clearFlags = CameraClearFlags.Skybox;
            }

            var camData = mainCam.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = enablePostProcessing;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camData.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        // 5. 삼국지 토탈워 시네마틱 글로벌 볼륨 (ACES Tonemapping & Soft Atmospheric Bloom) 구성
        SetupPostProcessVolume();

        DynamicGI.UpdateEnvironment();

        Debug.Log("<color=#FFCC00><b>[TotalWarEnvironmentLighting] ☀️ 삼국지 토탈워 공식 태양(Sun Disk & Corona), 4K 스카이박스 및 시네마틱 후처리(ACES+Bloom)가 적용되었습니다!</b></color>");
    }

    /// <summary>
    /// 삼탈워 특유의 묵직하고 사실적인 필름 색감(ACES)과 화염 대기 산란(Soft Bloom) 볼륨 구성
    /// </summary>
    private void SetupPostProcessVolume()
    {
        if (!enablePostProcessing) return;

        Volume volume = FindFirstObjectByType<Volume>();
        if (volume == null)
        {
            GameObject volGo = new GameObject("TotalWar_PostProcess_Volume");
            volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
        }

        if (volume.profile == null)
        {
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile.name = "TotalWar_PostProcess_Profile";
        }

        // 1. ACES Tonemapping: 1.0 초과의 HDR 화염이 형광색으로 튀지 않고 부드러운 고휘도 롤오프로 필름 감성 연출
        if (enableAcesTonemapping)
        {
            Tonemapping tonemapping;
            if (!volume.profile.TryGet(out tonemapping))
            {
                tonemapping = volume.profile.Add<Tonemapping>(true);
            }
            tonemapping.mode.Override(TonemappingMode.ACES);
        }

        // 2. Soft Atmospheric Bloom: 화염 주변 공기로 열기와 빛이 은은하게 퍼져 쨍한 그림 느낌 완전 해소
        Bloom bloom;
        if (!volume.profile.TryGet(out bloom))
        {
            bloom = volume.profile.Add<Bloom>(true);
        }
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(bloomIntensity);
        bloom.scatter.Override(bloomScatter);
        bloom.tint.Override(new Color(1.0f, 0.96f, 0.90f));

        // 3. Color Adjustments: 삼탈워 실사 톤 (부드러운 대비로 암부 뭉개짐 해소 및 선명한 디테일 복원)
        ColorAdjustments colorAdj;
        if (!volume.profile.TryGet(out colorAdj))
        {
            colorAdj = volume.profile.Add<ColorAdjustments>(true);
        }
        colorAdj.postExposure.Override(0.20f);
        colorAdj.contrast.Override(-2.0f); // 기존 +8.0으로 암부를 짓누르던 것을 완화하여 음영부 디테일 복원
        colorAdj.saturation.Override(0.0f);

        // 4. Shadows Midtones Highlights: 그림자(암부) 영역 톤 부드럽게 리프트
        ShadowsMidtonesHighlights smh;
        if (!volume.profile.TryGet(out smh))
        {
            smh = volume.profile.Add<ShadowsMidtonesHighlights>(true);
        }
        smh.shadows.Override(new Vector4(1.05f, 1.05f, 1.02f, 0.12f)); // 암부 섀도우 12% 리프트하여 나뭇잎/줄기 디테일 선명화
    }

    /// <summary>
    /// 전장의 사계절(봄, 여름, 가을, 겨울)에 맞춰 태양광 색감, 안개 틴트, 스카이박스 분위기를 자동 동기화합니다.
    /// </summary>
    public void ApplySeasonAtmosphere(TacticalForestZone.Season season)
    {
        switch (season)
        {
            case TacticalForestZone.Season.Spring:
                // 🌸 봄: 화사하고 부드러운 파스텔톤 햇살과 맑은 대기
                sunColor = new Color(1.0f, 0.98f, 0.93f);
                sunIntensity = 1.30f;
                sunCoronaColor = new Color(1.0f, 0.88f, 0.65f, 1f);
                horizonFogColor = new Color(0.74f, 0.78f, 0.82f, 1f);
                horizonFogIntensity = 0.50f;
                break;

            case TacticalForestZone.Season.Summer:
                // ☀️ 여름: 삼탈워 기본 찬란한 정오의 황금 백색광
                sunColor = new Color(1.0f, 0.96f, 0.88f);
                sunIntensity = 1.35f;
                sunCoronaColor = new Color(1.0f, 0.82f, 0.55f, 1f);
                horizonFogColor = new Color(0.72f, 0.75f, 0.78f, 1f);
                horizonFogIntensity = 0.55f;
                break;

            case TacticalForestZone.Season.Autumn:
                // 🍁 가을: 따뜻하고 깊은 앰버 황금빛 석양빛 햇살과 황혼 연무
                sunColor = new Color(1.05f, 0.90f, 0.72f);
                sunIntensity = 1.32f;
                sunCoronaColor = new Color(1.1f, 0.75f, 0.40f, 1f);
                horizonFogColor = new Color(0.78f, 0.72f, 0.68f, 1f);
                horizonFogIntensity = 0.58f;
                break;

            case TacticalForestZone.Season.Winter:
                // ❄️ 겨울: 차갑고 시린 은빛 백색광과 차가운 서리 안개
                sunColor = new Color(0.92f, 0.95f, 1.0f);
                sunIntensity = 1.25f;
                sunCoronaColor = new Color(0.85f, 0.90f, 1.0f, 1f);
                horizonFogColor = new Color(0.80f, 0.84f, 0.90f, 1f);
                horizonFogIntensity = 0.65f;
                break;
        }

        ApplyLightingAndSkybox();
    }
}
