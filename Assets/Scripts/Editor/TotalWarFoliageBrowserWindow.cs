using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌾 [삼국지 토탈워 260여 종 공식 3D 식생 전체 브라우저]
    /// 추출된 수백 종의 원작 3D 풀 모델을 검색하고 클릭 한 번으로 내 터레인 풀 목록에 추가하는 통합 브라우저 창입니다.
    /// </summary>
    public class TotalWarFoliageBrowserWindow : EditorWindow
    {
        private string searchKeyword = "";
        private int selectedCategoryTab = 0;
        private Vector2 scrollPos;

        private static readonly string[] CategoryTabs = new[]
        {
            "전체 보기 (All)",
            "🌿 초원/평야 (Temperate)",
            "🌾 꽃대/갈대/곡초 (Seedheads/Reeds)",
            "🍂 서량 건조 (Arid)",
            "❄️ 북방 한랭 (Cold)",
            "🌲 숲속 고사리 (Ferns)",
            "🌼 야생화 (Flowers)",
            "🌴 남만 아열대 (Tropical)",
            "🪵 고목/그루터기 (Stumps)",
            "🍃 관목/덤불 (Shrubs)"
        };

        public struct FoliageItem
        {
            public string displayName;
            public string modelFile;
            public string albedoFile;
            public string normalFile;
            public string category;
            public string fullPath;
            public float defaultWidth;
            public float defaultHeight;
            public bool growOnGrass;
            public bool growOnDirt;
            public bool growOnRock;
        }

        private static List<FoliageItem> allItems = new List<FoliageItem>();

        [MenuItem("TotalWar/🔍 삼탈워 260여 종 3D 풀 전체 브라우저 열기...", false, 2)]
        public static void OpenWindow()
        {
            var window = GetWindow<TotalWarFoliageBrowserWindow>("삼탈워 3D 식생 브라우저");
            window.minSize = new Vector2(650, 500);
            window.ScanAllFoliage();
            window.Show();
        }

        private void OnEnable()
        {
            ScanAllFoliage();
        }

        /// <summary>
        /// 추출된 삼탈워 원작 식생 폴더(Shrubs + DetailGrass)의 260여 종 3D OBJ 모델 전수 스캔
        /// </summary>
        public void ScanAllFoliage()
        {
            allItems.Clear();

            // 1. Shrubs 폴더 스캔
            string shrubDir = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs";
            if (Directory.Exists(shrubDir))
            {
                string[] files = Directory.GetFiles(shrubDir, "*.obj");
                foreach (var f in files)
                {
                    string name = Path.GetFileName(f);
                    string lower = name.ToLower();

                    // 나무 줄기/그루터기/가지 제외, 순수 풀/초목만 선별
                    if (lower.Contains("stump") || lower.Contains("brokenbranch")) continue;

                    string cat = "🌿 초원/평야 (Temperate)";
                    string albedo = "shrub_grassclump_su_base_colour.png";
                    string normal = "shrub_grassclump_normal.png";
                    float defW = 1.0f;
                    float defH = 0.9f;
                    bool dirt = false, rock = false;

                    if (lower.Contains("seedhead"))
                    {
                        cat = "🌾 꽃대/갈대/곡초 (Seedheads/Reeds)";
                        albedo = "shrub_grassclump_au_base_colour.png";
                        defW = 1.1f; defH = 1.3f;
                    }
                    else if (lower.Contains("elephantgrass"))
                    {
                        cat = "🌾 꽃대/갈대/곡초 (Seedheads/Reeds)";
                        albedo = "shrub_elephantgrass_a_billboard_base_colour.png";
                        normal = "shrub_elephantgrass_a_billboard_normal.png";
                        defW = 1.3f; defH = 1.5f;
                    }
                    else if (lower.Contains("millet") || lower.Contains("rice"))
                    {
                        cat = "🌾 꽃대/갈대/곡초 (Seedheads/Reeds)";
                        albedo = "shrub_grassclump_au_base_colour.png";
                        defW = 1.2f; defH = 1.4f;
                    }
                    else if (lower.Contains("dry") || lower.Contains("semidry"))
                    {
                        cat = "🍂 서량 건조 (Arid)";
                        albedo = "shrub_grassclump_base_colour.png";
                        dirt = true;
                    }
                    else if (lower.Contains("flower") || lower.Contains("convallaria"))
                    {
                        cat = "🌼 야생화 (Flowers)";
                        albedo = "shrub_flower_a_billboard_base_colour.png";
                        normal = "shrub_flower_a_billboard_normal.png";
                        defW = 0.8f; defH = 0.7f;
                    }
                    else if (lower.Contains("fern"))
                    {
                        cat = "🌲 숲속 고사리 (Ferns)";
                        albedo = "shrubs_ferns_su_base_colour.png";
                        normal = "shrubs_ferns_normal.png";
                        defW = 1.0f; defH = 0.9f;
                    }
                    else if (lower.Contains("flat"))
                    {
                        cat = "🌿 초원/평야 (Temperate)";
                        defW = 1.1f; defH = 0.5f;
                        dirt = true;
                    }
                    else if (lower.Contains("tropical") || lower.Contains("palm") || lower.Contains("ficus"))
                    {
                        cat = "🌴 남만 아열대 (Tropical)";
                        albedo = "shrub_grassclump_su_base_colour.png";
                        defW = 1.3f; defH = 1.4f;
                    }
                    else if (lower.Contains("stump") || lower.Contains("branch"))
                    {
                        cat = "🪵 고목/그루터기 (Stumps)";
                        albedo = "shrub_grassclump_su_base_colour.png";
                        dirt = true;
                        defW = 1.2f; defH = 0.8f;
                    }
                    else if (lower.Contains("bush") || lower.Contains("pine") || lower.Contains("tea"))
                    {
                        cat = "🍃 관목/덤불 (Shrubs)";
                        albedo = "shrub_grassclump_su_base_colour.png";
                        defW = 1.1f; defH = 1.1f;
                    }

                    allItems.Add(new FoliageItem()
                    {
                        displayName = CleanDisplayName(name),
                        modelFile = name,
                        albedoFile = albedo,
                        normalFile = normal,
                        category = cat,
                        fullPath = f,
                        defaultWidth = defW,
                        defaultHeight = defH,
                        growOnGrass = true, // 기본 잔디 평야 서식 보장
                        growOnDirt = dirt,
                        growOnRock = rock
                    });
                }
            }

            // 2. 🌾 완벽 복원된 238종 삼탈워 터레인 지피 디테일 잔디 (DetailGrass) 전수 스캔
            ScanDetailSubdir("Climate_Temperate", "🌿 초원/평야 (Temperate)", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png", true, false);
            ScanDetailSubdir("Climate_Arid", "🍂 서량 건조 (Arid)", "shrub_grassclump_base_colour.png", "shrub_grassclump_normal.png", true, true);
            ScanDetailSubdir("Climate_Cold", "❄️ 북방 한랭 (Cold)", "shrub_grassclump_sp_base_colour.png", "shrub_grassclump_normal.png", true, true);
            ScanDetailSubdir("Climate_Subtropical", "🌴 남만 아열대 (Tropical)", "shrub_grassclump_su_base_colour.png", "shrub_grassclump_normal.png", true, false);
        }

        private void ScanDetailSubdir(string folderName, string catName, string albedo, string normal, bool grass, bool dirt)
        {
            string dir = $"Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/{folderName}";
            if (!Directory.Exists(dir)) return;

            string[] files = Directory.GetFiles(dir, "*.obj");
            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                allItems.Add(new FoliageItem()
                {
                    displayName = CleanDisplayName(name) + " (지피 잔디)",
                    modelFile = name,
                    albedoFile = albedo,
                    normalFile = normal,
                    category = catName,
                    fullPath = f,
                    defaultWidth = 1.2f,
                    defaultHeight = 0.8f,
                    growOnGrass = grass,
                    growOnDirt = dirt,
                    growOnRock = false
                });
            }
        }

        private string CleanDisplayName(string raw)
        {
            return raw.Replace(".obj", "").Replace("Shrub_", "").Replace("_", " ");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"🌾 삼국지 토탈워 공식 3D 식생 전체 브라우저 (총 {allItems.Count}종)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("삼탈워 원작 게임에서 추출된 모든 3D 풀 모델 목록입니다. 검색하거나 카테고리별로 둘러본 뒤 [+ 내 목록에 추가] 버튼을 누르면 현재 씬의 터레인 매니저에 즉시 등록됩니다.", MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔍 풀 검색:", GUILayout.Width(65));
            searchKeyword = EditorGUILayout.TextField(searchKeyword);
            if (GUILayout.Button("새로고침", GUILayout.Width(75)))
            {
                ScanAllFoliage();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            selectedCategoryTab = GUILayout.Toolbar(selectedCategoryTab, CategoryTabs);

            EditorGUILayout.Space(6);

            // 필터링된 아이템 리스트 추출
            string currentCat = CategoryTabs[selectedCategoryTab];
            List<FoliageItem> filtered = new List<FoliageItem>();
            foreach (var item in allItems)
            {
                if (selectedCategoryTab != 0 && item.category != currentCat) continue;
                if (!string.IsNullOrEmpty(searchKeyword))
                {
                    if (!item.displayName.ToLower().Contains(searchKeyword.ToLower()) &&
                        !item.modelFile.ToLower().Contains(searchKeyword.ToLower()))
                    {
                        continue;
                    }
                }
                filtered.Add(item);
            }

            EditorGUILayout.LabelField($"표시 중인 풀: 총 {filtered.Count}개", EditorStyles.miniBoldLabel);

            TotalWarTerrainManager mgr = Object.FindAnyObjectByType<TotalWarTerrainManager>();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < filtered.Count; i++)
            {
                var item = filtered[i];

                EditorGUILayout.BeginHorizontal("box");
                
                // 정보 텍스트
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"<b>{item.displayName}</b> ({item.modelFile})", new GUIStyle(EditorStyles.label) { richText = true });
                EditorGUILayout.LabelField($"카테고리: {item.category} | 텍스처: {item.albedoFile}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // 서식지 뱃지
                string habitat = (item.growOnGrass ? "🌱잔디 " : "") + (item.growOnDirt ? "🪨흙길 " : "");
                EditorGUILayout.LabelField(habitat, EditorStyles.miniBoldLabel, GUILayout.Width(80));

                // 추가 버튼
                GUI.backgroundColor = new Color(0.2f, 1f, 0.4f);
                if (GUILayout.Button("➕ 추가", GUILayout.Width(70), GUILayout.Height(28)))
                {
                    if (mgr != null)
                    {
                        Undo.RecordObject(mgr, "Add Foliage From Browser");
                        if (mgr.customGrassList == null) mgr.customGrassList = new List<RadialGrassConfig>();

                        mgr.customGrassList.Add(new RadialGrassConfig()
                        {
                            displayName = item.displayName,
                            modelFile = item.modelFile,
                            albedoFile = item.albedoFile,
                            normalFile = item.normalFile,
                            isEnabled = true,
                            minWidth = item.defaultWidth * 0.9f,
                            maxWidth = item.defaultWidth * 1.35f,
                            minHeight = item.defaultHeight * 0.9f,
                            maxHeight = item.defaultHeight * 1.35f,
                            noiseThreshold = 0.25f,
                            minDensity = 30,
                            maxDensity = 80,
                            growOnGrass = item.growOnGrass,
                            growOnDirt = item.growOnDirt,
                            growOnRock = item.growOnRock,
                            alignToGround = item.displayName.Contains("지피 잔디") ? 1.0f : 0.45f,
                            groundOffset = item.displayName.Contains("지피 잔디") ? 0.05f : 0.02f
                        });

                        EditorUtility.SetDirty(mgr);
                        ShowNotification(new GUIContent($"✅ '{item.displayName}' 추가 완료!"));
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("경고", "씬에 TotalWar_TerrainManager가 활성화되어 있지 않습니다!", "확인");
                    }
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(1);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
