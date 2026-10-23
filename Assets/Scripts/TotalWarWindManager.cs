using UnityEngine;

/// <summary>
/// 🍃 [삼국지 토탈워 전장 바람 시스템 (Total War Wind System)]
/// 전장의 풍향(Wind Direction), 풍속(Wind Strength), 돌풍(Gust)을 전역 시뮬레이션하고,
/// 모든 3D 초목(나무, 솔잎, 대나무, 수풀) 셰이더에 실시간으로 바람 변위를 전달합니다.
/// </summary>
[ExecuteAlways]
public class TotalWarWindManager : MonoBehaviour
{
    private static TotalWarWindManager instance;
    public static TotalWarWindManager Instance => instance;

    [Header("🌬️ 풍향 및 풍속 설정 (Wind Settings)")]
    [Tooltip("풍향 (바람이 불어가는 방향, 0도 = 북, 90도 = 동, 180도 = 남, 270도 = 서)")]
    [Range(0f, 360f)]
    public float windAngle = 45.0f;

    [Tooltip("기본 풍속 (Wind Speed - 초목이 흔들리는 주기 속도)")]
    [Range(0.2f, 5.0f)]
    public float windSpeed = 1.35f;

    [Tooltip("바람 강도 (Wind Strength - 가지와 잎이 휘어지는 진폭)")]
    [Range(0.05f, 2.0f)]
    public float windStrength = 0.35f;

    [Header("💨 돌풍 시뮬레이션 (Gustiness)")]
    [Tooltip("돌풍 활성화")]
    public bool enableGust = true;

    [Tooltip("돌풍 빈도 (Gust Frequency)")]
    [Range(0.1f, 2.0f)]
    public float gustFrequency = 0.45f;

    [Tooltip("돌풍 강도 배율 (Gust Multiplier)")]
    [Range(1.0f, 3.0f)]
    public float gustMultiplier = 1.6f;

    // 전역 셰이더 프로퍼티 ID 캐싱
    private static readonly int WindVectorID = Shader.PropertyToID("_TotalWarWindVector");
    private static readonly int WindParamsID = Shader.PropertyToID("_TotalWarWindParams");


    private void Awake()
    {
        instance = this;
        UpdateWindShaderGlobals();
    }

    private void OnEnable()
    {
        instance = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoApplyOnSceneLoad()
    {
        var existing = FindFirstObjectByType<TotalWarWindManager>();
        if (existing == null)
        {
            GameObject windGo = new GameObject("TotalWar_WindManager");
            existing = windGo.AddComponent<TotalWarWindManager>();
        }
        existing.UpdateWindShaderGlobals();
    }

    private void Update()
    {
        UpdateWindShaderGlobals();
    }

    /// <summary>
    /// 현재 바람이 불어가는 월드 3D 방향 벡터(단위 벡터)를 반환합니다. (XZ 평면)
    /// </summary>
    public Vector3 GetWindDirectionVector()
    {
        float rad = windAngle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    /// <summary>
    /// 현재 바람이 불어가는 2D 방향 벡터를 반환합니다. (X, Y)
    /// </summary>
    public Vector2 GetWindDirectionVector2()
    {
        float rad = windAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)).normalized;
    }

    /// <summary>
    /// 전역 셰이더 프로퍼티로 실시간 바람 벡터 및 매개변수를 브로드캐스트합니다.
    /// </summary>
    public void UpdateWindShaderGlobals()
    {
        float rad = windAngle * Mathf.Deg2Rad;
        Vector2 windDir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

        // 돌풍 펄스 (Perlin Noise 기반 자연스러운 바람 변화)
        float gustFactor = 1.0f;
        if (enableGust)
        {
            float timeVal = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup;
            float noise = Mathf.PerlinNoise(timeVal * gustFrequency, 0.5f);
            gustFactor = Mathf.Lerp(0.7f, gustMultiplier, noise);
        }

        float currentStrength = windStrength * gustFactor;

        // _TotalWarWindVector: (DirX * Strength, DirY * Strength, WindSpeed, Time)
        float curTime = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup;
        Vector4 windVector = new Vector4(
            windDir.x * currentStrength,
            windDir.y * currentStrength,
            windSpeed,
            curTime * windSpeed
        );

        // _TotalWarWindParams: (WindStrength, GustFactor, 0, 0)
        Vector4 windParams = new Vector4(currentStrength, gustFactor, 0f, 0f);

        Shader.SetGlobalVector(WindVectorID, windVector);
        Shader.SetGlobalVector(WindParamsID, windParams);
    }

    private void OnDrawGizmosSelected()
    {
        // 에디터 기즈모로 풍향 화살표 시각화
        Gizmos.color = new Color(0.2f, 0.9f, 1.0f, 0.85f);
        Vector3 pos = transform.position + Vector3.up * 5.0f;
        float rad = windAngle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

        Gizmos.DrawRay(pos, dir * 10f);
        Gizmos.DrawSphere(pos + dir * 10f, 0.6f);
    }
}
