using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 🔥 [삼국지 토탈워 화재/연기/불티 파티클 및 머티리얼 자동 빌더]
/// REA로 추출된 삼탈워 원본 DDS 텍스처들을 URP 머티리얼로 바인딩하고,
/// 바람(TotalWarWindManager)과 연동되는 전장 화재 파티클 프리팹을 자동 생성합니다.
/// </summary>
[InitializeOnLoad]
public static class TotalWarVFXMaterialBuilder
{
    private const string TexturePath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/";
    private const string MaterialPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/";
    private const string PrefabPath = "Assets/Prefabs/VFX/";

    static TotalWarVFXMaterialBuilder()
    {
        EditorApplication.delayCall += TriggerInitialBuild;
    }

    private static void TriggerInitialBuild()
    {
        ImportAllDDSTextures();
        BuildVFXAssets(force: false);
    }

    private static void ImportAllDDSTextures()
    {
        if (!Directory.Exists(TexturePath)) return;
        string[] files = Directory.GetFiles(TexturePath, "*.png");
        foreach (var f in files)
        {
            string unityPath = f.Replace("\\", "/");
            int idx = unityPath.IndexOf("Assets/");
            if (idx >= 0)
            {
                AssetDatabase.ImportAsset(unityPath.Substring(idx), ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }

    [MenuItem("MiniTotalWar/🔥 삼탈워 화재·연기 VFX 에셋 및 머티리얼 빌드 (Build Fire VFX)", false, 60)]
    public static void ManualBuild()
    {
        BuildVFXAssets(force: true);
    }

    public static void BuildVFXAssets(bool force = false)
    {
        if (Application.isPlaying) return;

        // 1. 디렉터리 확인 및 생성
        if (!Directory.Exists(MaterialPath)) Directory.CreateDirectory(MaterialPath);
        if (!Directory.Exists(PrefabPath)) Directory.CreateDirectory(PrefabPath);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 2. 표준 PNG 텍스처 로드
        Texture2D smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "wispy_smoke_diffuse.png");
        Texture2D flamesTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "rising_flames_diffuse.png");
        Texture2D embersTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "embers_diffuse.png");
        Texture2D fireTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "vfx_fire_diffuse.png");

        Texture2D ashesBase = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "3k_cityprop_mines_wood_ashes_base_colour.png");
        Texture2D ashesNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "3k_cityprop_mines_wood_ashes_normal.png");

        Shader particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 3. 머티리얼 생성 및 텍스처 바인딩
        Material smokeMat = CreateOrGetParticleMaterial(MaterialPath + "M_TotalWar_WispySmoke.mat", particleUnlit, smokeTex, isAdditive: false, new Color(0.85f, 0.85f, 0.88f, 0.65f), force);
        Material flamesMat = CreateOrGetParticleMaterial(MaterialPath + "M_TotalWar_RisingFlames.mat", particleUnlit, flamesTex, isAdditive: true, new Color(2.5f, 1.2f, 0.35f, 1.0f), force);
        Material embersMat = CreateOrGetParticleMaterial(MaterialPath + "M_TotalWar_Embers.mat", particleUnlit, embersTex, isAdditive: true, new Color(3.5f, 1.4f, 0.2f, 1.0f), force);
        Material fireMat = CreateOrGetParticleMaterial(MaterialPath + "M_TotalWar_Fire.mat", particleUnlit, fireTex, isAdditive: true, new Color(2.8f, 1.1f, 0.3f, 1.0f), force);

        // 4. 탄화 숯 목재 머티리얼 (Lit)
        string charredMatPath = MaterialPath + "M_TotalWar_CharredWood.mat";
        Material charredMat = AssetDatabase.LoadAssetAtPath<Material>(charredMatPath);
        if (charredMat == null || force)
        {
            charredMat = new Material(litShader);
            if (ashesBase != null) charredMat.SetTexture("_BaseMap", ashesBase);
            if (ashesNormal != null)
            {
                charredMat.SetTexture("_BumpMap", ashesNormal);
                charredMat.EnableKeyword("_NORMALMAP");
            }
            charredMat.SetColor("_BaseColor", new Color(0.12f, 0.11f, 0.11f, 1f));
            charredMat.SetFloat("_Smoothness", 0.08f);
            AssetDatabase.CreateAsset(charredMat, charredMatPath);
        }

        // 5. 삼탈워 공식 화재 복합 파티클 프리팹 (TotalWar_TreeFire_VFX.prefab) 조립
        string prefabFullPath = PrefabPath + "TotalWar_TreeFire_VFX.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabFullPath) == null || force)
        {
            GameObject rootGo = new GameObject("TotalWar_TreeFire_VFX");

            // (1) 화염 서브시스템 (Rising Flames)
            GameObject flamesGo = new GameObject("Flames_Core");
            flamesGo.transform.SetParent(rootGo.transform, false);
            ParticleSystem psFlames = flamesGo.AddComponent<ParticleSystem>();
            SetupFlamesParticle(psFlames, flamesMat);

            // (2) 불티 서브시스템 (Embers - 바람 비산)
            GameObject embersGo = new GameObject("Embers_Sparks");
            embersGo.transform.SetParent(rootGo.transform, false);
            ParticleSystem psEmbers = embersGo.AddComponent<ParticleSystem>();
            SetupEmbersParticle(psEmbers, embersMat);

            // (3) 연기 서브시스템 (Wispy Smoke)
            GameObject smokeGo = new GameObject("Smoke_Plume");
            smokeGo.transform.SetParent(rootGo.transform, false);
            ParticleSystem psSmoke = smokeGo.AddComponent<ParticleSystem>();
            SetupSmokeParticle(psSmoke, smokeMat);

            PrefabUtility.SaveAsPrefabAsset(rootGo, prefabFullPath);
            Object.DestroyImmediate(rootGo);
            Debug.Log("<color=#FF5500><b>[TotalWarVFXMaterialBuilder] 🔥 삼탈워 원본 화재 파티클 프리팹(TotalWar_TreeFire_VFX.prefab)이 성공적으로 조립되었습니다!</b></color>");
        }

        AssetDatabase.SaveAssets();
    }

    private static Material CreateOrGetParticleMaterial(string path, Shader shader, Texture2D tex, bool isAdditive, Color tint, bool force)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null || force)
        {
            mat = new Material(shader);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint);

            if (isAdditive)
            {
                mat.SetFloat("_Surface", 1.0f); // Transparent
                mat.SetFloat("_Blend", 1.0f);   // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 1.0f); // Transparent
                mat.SetFloat("_Blend", 0.0f);   // Alpha Blend
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static void SetupFlamesParticle(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.1f, 3.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.75f, 1.15f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        main.flipRotation = 0.5f;
        main.gravityModifier = 0f;
        main.startColor = Color.white;
        main.maxParticles = 25;

        var emission = ps.emission;
        emission.rateOverTime = 4.5f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.8f;

        var texSheet = ps.textureSheetAnimation;
        texSheet.enabled = true;
        texSheet.numTilesX = 8;
        texSheet.numTilesY = 8;
        texSheet.animation = ParticleSystemAnimationType.WholeSheet;
        texSheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        AnimationCurve frameCurve = new AnimationCurve();
        frameCurve.AddKey(0f, 0f);
        frameCurve.AddKey(1f, 63f);
        texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);
        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 63f);
        texSheet.cycleCount = 1;

        var noise = ps.noise;
        noise.enabled = false;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = false;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0f), 
                new GradientColorKey(Color.white, 1f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0f, 0f), 
                new GradientAlphaKey(1.0f, 0.08f), 
                new GradientAlphaKey(1.0f, 0.85f), 
                new GradientAlphaKey(0f, 1f) 
            }
        );
        colorOverLifetime.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;

        var streams = new System.Collections.Generic.List<ParticleSystemVertexStream>()
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.UV2,
            ParticleSystemVertexStream.AnimBlend
        };
        renderer.SetActiveVertexStreams(streams);

        ps.Play();
    }

    private static void SetupEmbersParticle(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 25f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.35f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;

        ps.Play();
    }

    private static void SetupSmokeParticle(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.30f);
        main.startColor = new Color(0.25f, 0.25f, 0.28f, 0.45f);
        main.maxParticles = 50;

        var emission = ps.emission;
        emission.rateOverTime = 12f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 1.5f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.4f);
        curve.AddKey(1f, 1.8f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.4f, 0.45f), 0f), new GradientColorKey(new Color(0.15f, 0.15f, 0.18f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.4f, 0.25f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
    }
}
