using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public static class SetupAmmoBarPrefabs
{
    [MenuItem("MiniTotalWar/Setup Ammo Bar In Prefabs")]
    public static void Execute()
    {
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        SetupSquadIconPrefab("Assets/Prefabs/SquadIconUI.prefab", uiSprite);
        SetupSquadIconPrefab("Assets/Resources/SquadIconUI.prefab", uiSprite);
        SetupSquadCardPrefab("Assets/Prefabs/SquadCardButtonPrefab.prefab", uiSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green><b>[SetupAmmoBarPrefabs] 부대 아이콘 및 부대 카드 프리팹에 탄약 잔량 바 영구 탑재 완료! (Layer: UI, Default Hidden)</b></color>");
    }

    private static void SetupSquadIconPrefab(string prefabPath, Sprite uiSprite)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogWarning($"[SetupAmmoBarPrefabs] 프리팹 로드 실패: {prefabPath}");
            return;
        }

        try
        {
            SquadIconUI iconUI = root.GetComponent<SquadIconUI>();

            // 기존 AmmoBarRoot가 있다면 정리
            Transform existing = root.transform.Find("AmmoBarRoot");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            // 1. AmmoBarRoot (배경 바) 생성
            GameObject barRootGo = new GameObject("AmmoBarRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barRootGo.layer = 5; // UI Layer
            barRootGo.transform.SetParent(root.transform, false);

            RectTransform barRect = barRootGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -4f); // 40x40 아이콘 바로 아래 4px
            barRect.sizeDelta = new Vector2(0f, 5f); // 높이 5px

            Image bgImage = barRootGo.GetComponent<Image>();
            if (uiSprite != null) bgImage.sprite = uiSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
            bgImage.raycastTarget = false;

            // 2. AmmoFill (전경 게이지) 생성
            GameObject fillGo = new GameObject("AmmoFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.layer = 5; // UI Layer
            fillGo.transform.SetParent(barRootGo.transform, false);

            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;

            Image fillImage = fillGo.GetComponent<Image>();
            if (uiSprite != null) fillImage.sprite = uiSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(1.0f, 0.72f, 0.05f, 0.95f); // 앰버 골드
            fillImage.raycastTarget = false;

            // 3. 토탈워 삼국: 기본 상태는 비활성화 (첫 사격 시 활성화)
            barRootGo.SetActive(false);

            // 4. SquadIconUI 컴포넌트 직렬화 연결
            if (iconUI != null)
            {
                SerializedObject so = new SerializedObject(iconUI);
                so.Update();
                SerializedProperty rootProp = so.FindProperty("ammoBarRoot");
                SerializedProperty imgProp = so.FindProperty("ammoBarImage");
                if (rootProp != null) rootProp.objectReferenceValue = barRootGo;
                if (imgProp != null) imgProp.objectReferenceValue = fillImage;
                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[SetupAmmoBarPrefabs] {prefabPath} 탄약 바 구성 완료!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupSquadCardPrefab(string prefabPath, Sprite uiSprite)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogWarning($"[SetupAmmoBarPrefabs] 프리팹 로드 실패: {prefabPath}");
            return;
        }

        try
        {
            SquadCardUI cardUI = root.GetComponent<SquadCardUI>();

            // 기존 AmmoBarRoot가 있다면 정리
            Transform existing = root.transform.Find("AmmoBarRoot");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            // 1. AmmoBarRoot (배경 바) 생성
            GameObject barRootGo = new GameObject("AmmoBarRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barRootGo.layer = 5; // UI Layer
            barRootGo.transform.SetParent(root.transform, false);

            RectTransform barRect = barRootGo.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.05f, 0f);
            barRect.anchorMax = new Vector2(0.95f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 3f); // 카드 밑단 3px
            barRect.sizeDelta = new Vector2(0f, 5f); // 높이 5px

            Image bgImage = barRootGo.GetComponent<Image>();
            if (uiSprite != null) bgImage.sprite = uiSprite;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
            bgImage.raycastTarget = false;

            // 2. AmmoFill (전경 게이지) 생성
            GameObject fillGo = new GameObject("AmmoFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.layer = 5; // UI Layer
            fillGo.transform.SetParent(barRootGo.transform, false);

            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.anchoredPosition = Vector2.zero;

            Image fillImage = fillGo.GetComponent<Image>();
            if (uiSprite != null) fillImage.sprite = uiSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(1.0f, 0.72f, 0.05f, 0.95f); // 앰버 골드
            fillImage.raycastTarget = false;

            // 3. 토탈워 삼국: 기본 상태는 비활성화 (첫 사격 시 활성화)
            barRootGo.SetActive(false);

            // 4. SquadCardUI 컴포넌트 직렬화 연결
            if (cardUI != null)
            {
                SerializedObject so = new SerializedObject(cardUI);
                so.Update();
                SerializedProperty rootProp = so.FindProperty("ammoBarRoot");
                SerializedProperty imgProp = so.FindProperty("ammoBarImage");
                if (rootProp != null) rootProp.objectReferenceValue = barRootGo;
                if (imgProp != null) imgProp.objectReferenceValue = fillImage;
                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[SetupAmmoBarPrefabs] {prefabPath} 탄약 바 구성 완료!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
