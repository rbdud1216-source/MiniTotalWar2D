#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 삼국지 토탈워 본편 일치화:
/// - 4x4 (16프레임) 플립북 애니메이션 화염
/// - 나뭇가지 끝 잎사귀 뭉치들(5개 클러스터: Center, Right, Left, Forward, Top)마다 화염 에미터 배치
/// - 상공 황갈색 훈연 연기(Wispy Smoke) 및 바람을 타는 불티(Airborne Embers)
/// - TotalWar_TreeFire_VFX.prefab 에셋 자동 빌드 및 갱신 툴
[InitializeOnLoad]
public static class TotalWarTreeFirePrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/VFX/TotalWar_TreeFire_VFX.prefab";
    private const string VfxMatPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/";
    private const string VfxTexPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/";
    private static bool hasTriggered = false;

    static TotalWarTreeFirePrefabBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!hasTriggered)
            {
                hasTriggered = true;
                BuildPrefab();
            }
        };
    }

    [MenuItem("TotalWar/VFX/🌲 5대 나뭇가지 플립북 화재 프리팹 생성 (Build Multi-Branch Flipbook Fire Prefab)")]
    public static void BuildPrefab()
    {
        Material flamesMat = AssetDatabase.LoadAssetAtPath<Material>(VfxMatPath + "M_TotalWar_RisingFlames.mat");
        Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>(VfxMatPath + "M_TotalWar_WispySmoke.mat");
        Material embersMat = AssetDatabase.LoadAssetAtPath<Material>(VfxMatPath + "M_TotalWar_Embers.mat");

        Texture2D flameFlipbookTex = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxTexPath + "totalwar_flame_flipbook.png");
        if (flameFlipbookTex != null && flamesMat != null)
        {
            flamesMat.SetTexture("_BaseMap", flameFlipbookTex);
            flamesMat.SetTexture("_MainTex", flameFlipbookTex);
            EditorUtility.SetDirty(flamesMat);
        }

        // 루트 게임오브젝트 생성
        GameObject root = new GameObject("TotalWar_TreeFire_VFX");

        // 기본 황산소나무 모델의 메쉬 로드 및 나뭇잎 서브메시(1) 바인딩
        GameObject pinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees/Huangshan_Pine_A.obj");
        Mesh pineMesh = null;
        if (pinePrefab != null)
        {
            MeshFilter pineMf = pinePrefab.GetComponentInChildren<MeshFilter>();
            if (pineMf != null) pineMesh = pineMf.sharedMesh;
        }

        // 복합 화염 머티리얼 로드 (삼탈워 3종 화염 A, B, H 1:1 합성)
        string vfxMatPath = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Materials/";
        Material flameMatComposite = AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_CompositeFlames.mat");
        if (flameMatComposite == null)
        {
            flameMatComposite = AssetDatabase.LoadAssetAtPath<Material>(vfxMatPath + "M_TotalWar_RisingFlames.mat");
        }

        uint syncSeed = 12345;

        // [핵심: 삼탈워 3중 화염(A, B, H) 일체형 복합 화염 시스템]
        // 개별 파티클 시스템을 따로 돌려 나무 여기저기에 A 따로 B 따로 H 따로 흩어지던 현상을 완전히 원천 차단하고,
        // 피어오르는 "모든 불꽃 덩어리(개별 파티클) 하나하나"에 A(코어), B(너울), H(팁) 3종 애니메이션이 
        // 1:1로 완벽하게 포개어져 하나의 생생한 화염 덩어리를 이루도록 구현!
        GameObject flamesObj = new GameObject("Flames_Composite_TotalWar");
        flamesObj.transform.SetParent(root.transform, false);
        flamesObj.transform.localPosition = Vector3.zero;
        ParticleSystem psFlames = flamesObj.AddComponent<ParticleSystem>();
        SetupFlamesPS(psFlames, flameMatComposite, pineMesh, 0, syncSeed);

        // [빛 연출 강화] 화재 포인트 라이트
        GameObject lightObj = new GameObject("Fire_PointLight");
        lightObj.transform.SetParent(root.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        Light fireLight = lightObj.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.range = 16f;
        fireLight.color = new Color(1.0f, 0.52f, 0.18f);
        fireLight.intensity = 3.5f;
        fireLight.shadows = LightShadows.None;

        // 2. 연기 서브시스템 (Wispy Smoke)
        GameObject smokeObj = new GameObject("Smoke_Plume");
        smokeObj.transform.SetParent(root.transform, false);
        smokeObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        ParticleSystem psSmoke = smokeObj.AddComponent<ParticleSystem>();
        SetupSmokePS(psSmoke, smokeMat);

        // 3. 불티 서브시스템 (Airborne Embers)
        GameObject embersObj = new GameObject("Embers_Sparks");
        embersObj.transform.SetParent(root.transform, false);
        embersObj.transform.localPosition = Vector3.zero;
        ParticleSystem psEmbers = embersObj.AddComponent<ParticleSystem>();
        SetupEmbersPS(psEmbers, embersMat);

        // 4. 화재 수명주기 컨트롤러 부착
        var fireCtrl = root.AddComponent<TotalWarFireVFXController>();
        fireCtrl.enableBurnProgression = true;
        fireCtrl.burnDuration = 40.0f; // 40초 연소 시간
        fireCtrl.baseLightIntensity = 3.5f;

        // 프리팹 에셋 저장
        System.IO.Directory.CreateDirectory("Assets/Prefabs/VFX");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#FF8800><b>[TotalWar VFX]</b> 삼국지 토탈워 본편 일치 나뭇잎 서브메시 표면 화재 프리팹 생성 완료: {PrefabPath}</color>");
    }

    private static void SetupFlamesPS(ParticleSystem ps, Material flameMat, Mesh defaultLeavesMesh, int layerIndex = 0, uint syncSeed = 0)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (syncSeed != 0)
        {
            ps.useAutoRandomSeed = false;
            ps.randomSeed = syncSeed;
        }

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // 초당 24프레임 (24 FPS): 64프레임 / 24fps = 정확히 2.67초
        main.startLifetime = 2.67f;
        main.startSpeed = 0f;

        // [사용자 피드백 반영] 불 크기 대폭 축소 (0.35m ~ 0.65m)
        float minSize = (layerIndex == 0) ? 0.35f : ((layerIndex == 1) ? 0.40f : 0.30f);
        float maxSize = (layerIndex == 0) ? 0.55f : ((layerIndex == 1) ? 0.65f : 0.50f);

        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        main.flipRotation = 0.5f;
        main.gravityModifier = 0f;
        main.startColor = Color.white;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 4.2f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.normalOffset = (layerIndex == 2) ? 0.30f : 0.20f;
        if (defaultLeavesMesh != null)
        {
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.mesh = defaultLeavesMesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            if (defaultLeavesMesh.subMeshCount >= 2)
            {
                shape.useMeshMaterialIndex = true;
                shape.meshMaterialIndex = 1;
            }
            else
            {
                shape.useMeshMaterialIndex = false;
                shape.meshMaterialIndex = 0;
            }
            shape.useMeshColors = false;
        }
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = Vector3.one;

        // 삼탈워 공식 8x8 (64프레임) 플립북 애니메이션 (24 FPS)
        var texSheet = ps.textureSheetAnimation;
        texSheet.enabled = true;
        texSheet.numTilesX = 8;
        texSheet.numTilesY = 8;
        texSheet.animation = ParticleSystemAnimationType.WholeSheet;
        texSheet.timeMode = ParticleSystemAnimationTimeMode.FPS;
        texSheet.fps = 24.0f; // 초당 24 FPS
        AnimationCurve frameCurve = new AnimationCurve();
        frameCurve.AddKey(0f, 0f);
        frameCurve.AddKey(1f, 1f); // 0.0f ~ 1.0f
        texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);
        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.15f);
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
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        if (flameMat != null) renderer.material = flameMat;

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
    }

    private static void SetupSmokePS(ParticleSystem ps, Material smokeMat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-3.14f, 3.14f);
        main.gravityModifier = -0.12f;
        main.startColor = new Color(0.24f, 0.18f, 0.12f, 0.60f);
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 1.2f;
        shape.angle = 14f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.35f);
        curve.AddKey(0.4f, 1.0f);
        curve.AddKey(1f, 1.6f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.32f, 0.22f, 0.14f), 0f), new GradientColorKey(new Color(0.20f, 0.15f, 0.10f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.65f, 0.2f), new GradientAlphaKey(0.45f, 0.7f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        if (smokeMat != null) renderer.material = smokeMat;
    }

    private static void SetupEmbersPS(ParticleSystem ps, Material emberMat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.gravityModifier = -0.15f;
        main.startColor = new Color(3.5f, 1.8f, 0.5f, 1.0f);
        main.maxParticles = 160;

        var emission = ps.emission;
        emission.rateOverTime = 28f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.0f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        if (emberMat != null) renderer.material = emberMat;
    }
}
#endif
