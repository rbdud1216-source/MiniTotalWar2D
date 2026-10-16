using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ☀️ [삼국지 토탈워 환경 조명 및 스카이박스 시스템 (Total War Environment Lighting)]
/// 삼탈워 본편 라이팅 스펙(고도각 40도, 방위각 90도, 6방향 지면 반사광)과
/// 4K 파노라마 푸른 하늘 스카이박스(3k_sky_blue_1_diffuse.dds) 및
/// 실시간 동적 태양 광구 & 대기 코로나 후광(sun_shader_sun)을 전장에 자동 적용합니다.
/// </summary>
[ExecuteAlways]
public class TotalWarEnvironmentLighting : MonoBehaviour
{
    [Header("☀️ 삼탈워 태양광 설정 (Sun Light)")]
    [Tooltip("태양 고도각 (Elevation, 구름 텍스처 햇살 고도: 약 24도)")]
    [Range(5f, 85f)]
    public float sunElevation = 24.0f;

    [Tooltip("태양 방위각 (Azimuth, 0~360도)")]
    [Range(0f, 360f)]
    public float sunAzimuth = 90.0f;

    [Tooltip("스카이박스 구름 햇살과 태양 광원 방향 자동 결속")]
    public bool lockSunToSkyRotation = false;

    [Tooltip("태양광 밝기 (Intensity)")]
    [Range(0.5f, 3.0f)]
    public float sunIntensity = 1.35f;

    [Tooltip("태양광 색상 (따뜻한 햇살 골드 화이트)")]
    public Color sunColor = new Color(1.0f, 0.96f, 0.88f);

    [Header("☀️ 삼탈워 태양 광구 & 대기 코로나 (Sun Disk & Corona)")]
    [Tooltip("태양 광구 디스크 활성화")]
    public bool enableSunDisk = true;

    [Tooltip("태양 광구 원반 크기 (Angular Radius, 기본: 0.016)")]
    [Range(0.005f, 0.08f)]
    public float sunDiskSize = 0.016f;

    [Tooltip("태양 광구 색상 (HDR 온백색 찬란한 골드)")]
    public Color sunDiskColor = new Color(2.5f, 2.3f, 1.8f, 1f);

    [Tooltip("태양 대기 코로나 후광 색상 (Atmospheric Corona)")]
    public Color sunCoronaColor = new Color(1.0f, 0.82f, 0.55f, 1f);

    [Tooltip("태양 대기 후광 퍼짐 지수 (수치가 높을수록 중심에 집중)")]
    [Range(1f, 32f)]
    public float sunCoronaFalloff = 6.0f;

    [Header("🌌 삼탈워 스카이박스 설정 (Skybox)")]
    [Tooltip("스카이박스 노출도 (Exposure)")]
    [Range(0.5f, 2.5f)]
    public float skyExposure = 1.0f;

    [Tooltip("스카이박스 수평 회전각 (Yaw 0~360도, 텍스처 햇살과 광원 방향 일치)")]
    [Range(0f, 360f)]
    public float skyRotation = 0.0f;

    [Header("🌫️ 삼탈워 지평선 대기 안개 (Horizon Fog)")]
    [Tooltip("수평선 높이 오프셋 (지평선 위치 조절)")]
    [Range(-0.3f, 0.3f)]
    public float horizonOffset = 0.0f;

    [Tooltip("지평선 대기 안개 강도 (지형과 하늘 경계 융합)")]
    [Range(0.0f, 1.0f)]
    public float horizonFogIntensity = 0.65f;

    [Tooltip("지평선 안개 퍼짐 높이")]
    [Range(0.05f, 0.5f)]
    public float horizonFogHeight = 0.22f;

    [Tooltip("지평선 안개 색상 (대기 원경 연무빛)")]
    public Color horizonFogColor = new Color(0.72f, 0.75f, 0.78f, 1f);

    [Tooltip("사용할 삼탈워 스카이박스 텍스처 파일명")]
    public string skyboxTextureName = "3k_sky_blue_1_diffuse.dds";

    [Header("🌍 삼탈워 환경광 (Ambient Trilight - 지면 반사광)")]
    [Tooltip("하늘 방향 환경광 (Sky Ambient)")]
    public Color ambientSky = new Color(0.40f, 0.52f, 0.68f);

    [Tooltip("수평선 방향 대기 산란광 (Equator Ambient)")]
    public Color ambientEquator = new Color(0.52f, 0.50f, 0.44f);

    [Tooltip("지면 반사광 (Ground Bounce - 나뭇잎 아래쪽을 비추는 흙빛)")]
    public Color ambientGround = new Color(0.48f, 0.42f, 0.28f);

    [Tooltip("환경광 전체 강도 배율")]
    [Range(0.5f, 2.5f)]
    public float ambientIntensity = 1.2f;

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

            if (skyboxMaterialInstance != null)
            {
                Vector3 sunDir = -currentSunLight.transform.forward;
                skyboxMaterialInstance.SetVector("_SunDirection", sunDir);
            }
        }

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

        // 4. 메인 카메라 Clear Flags 보정 (Solid Color로 인한 게임 뷰 스카이박스 누락 방지)
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = FindFirstObjectByType<Camera>();
        }
        if (mainCam != null && mainCam.clearFlags != CameraClearFlags.Skybox)
        {
            mainCam.clearFlags = CameraClearFlags.Skybox;
        }

        DynamicGI.UpdateEnvironment();

        Debug.Log("<color=#FFCC00><b>[TotalWarEnvironmentLighting] ☀️ 삼국지 토탈워 공식 태양(Sun Disk & Corona)과 4K 스카이박스가 실시간 합성되었습니다!</b></color>");
    }
}
