using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🌾 [TacticalGrassRenderer 커스텀 인스펙터 에디터]
    /// 원클릭 풀 데이터 빌드, Assets/Settings/Vegetation/Data/ 파일 영구 저장/로드 및 실시간 스트리밍 대시보드 제공
    /// </summary>
    [CustomEditor(typeof(TacticalGrassRenderer))]
    public class TacticalGrassRendererEditor : UnityEditor.Editor
    {
        [MenuItem("TotalWar/🌾 데이터 기반 방사형 풀 렌더러 장착 및 빌드", false, 2)]
        public static void SetupAndBakeMenu()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) terrain = Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("경고", "씬에 활성화된 Terrain이 없습니다.", "확인");
                return;
            }

            TacticalGrassRenderer renderer = terrain.GetComponent<TacticalGrassRenderer>();
            if (renderer == null)
            {
                renderer = terrain.gameObject.AddComponent<TacticalGrassRenderer>();
                Undo.RegisterCreatedObjectUndo(renderer, "Add TacticalGrassRenderer");
            }

            renderer.targetTerrain = terrain;
            renderer.EnsureDefaultPrototypes(true);
            renderer.BakeFoliageData();
            renderer.SaveFoliageDataToDisk();
            
            EditorUtility.SetDirty(renderer);
            if (!Application.isPlaying && renderer.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
                EditorSceneManager.SaveScene(renderer.gameObject.scene);
            }
            SceneView.RepaintAll();

            EditorUtility.DisplayDialog("완료", $"🌾 [데이터 기반 방사형 풀 시스템 구축 & 파일 저장 완료]\n\n• 저장 경로: {renderer.GetTargetDataDirectory()}\n• 총 {renderer.TotalInstanceCount:N0}개의 풀포기 데이터가 초경량 바이너리로 저장되었습니다!\n• 씬 뷰에서 카메라를 250m 이내로 접근하면 3D 잔디가 나타납니다.\n• 줌아웃 상태에서는 청크 그리드 기즈모가 표시됩니다.", "확인");
        }

        [MenuItem("TotalWar/🔥 씬 뷰 중심에 들불 착화 테스트 (Ignite Wildfire)", false, 3)]
        public static void IgniteAtSceneViewMenu()
        {
            var renderer = Object.FindAnyObjectByType<TacticalGrassRenderer>();
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("경고", "씬에 TacticalGrassRenderer가 없습니다.", "확인");
                return;
            }
            renderer.IgniteAtCameraFocus();
            SceneView.RepaintAll();
        }

        [MenuItem("TotalWar/🧯 전장 화재 전면 진압 및 식생 복구 (Extinguish All)", false, 4)]
        public static void ExtinguishAllMenu()
        {
            var renderer = Object.FindAnyObjectByType<TacticalGrassRenderer>();
            if (renderer == null) return;
            renderer.ExtinguishAndRestoreAll();
            SceneView.RepaintAll();
        }

        public enum FoliageBrushMode
        {
            Off = 0,
            Paint = 1,
            Erase = 2
        }

        private static FoliageBrushMode _brushMode = FoliageBrushMode.Off;
        private static float _brushRadius = 7.0f;
        private static float _brushSpacing = 0.95f;
        private static float _brushFalloff = 0.65f;
        private static int _selectedProtoIndex = -1; // -1: 복합 자연 혼합 살포

        // 📂 인스펙터 섹션별 접기/펼치기 상태 관리
        private static bool _foldTerrain = true;
        private static bool _foldPrototypes = true;
        private static bool _foldStreaming = true;
        private static bool _foldBlending = true;
        private static bool _foldOverrides = false;
        private static bool _foldSceneView = true;
        private static bool _foldFire = true;
        private static bool _foldAsh = true;
        private static bool _foldRendering = true;
        private static readonly Dictionary<int, bool> _protoFoldouts = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> _protoTacticalFoldouts = new Dictionary<int, bool>();

        public override void OnInspectorGUI()
        {
            TacticalGrassRenderer tgt = (TacticalGrassRenderer)target;
            serializedObject.Update();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            GUI.color = new Color(0.2f, 1.0f, 0.4f, 1.0f);
            EditorGUILayout.LabelField("🌾 데이터 기반 방사형 잔디 스트리밍 렌더러", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.HelpBox($"📁 저장 경로: {tgt.GetTargetDataDirectory()}\n• 풀을 씬에 무겁게 심지 않고 위 폴더의 바이너리 파일로 영구 보관합니다.\n• 카메라를 확대(80m 이내)할 때만 GPU DrawMeshInstanced로 실시간 렌더링됩니다.", MessageType.Info);
            EditorGUILayout.EndVertical();

            // 🖌️ [씬 뷰 식생 브러시 페인터 툴바]
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            GUI.color = new Color(0.3f, 0.95f, 1f, 1f);
            EditorGUILayout.LabelField("🖌️ 씬 뷰 유기적 식생 브러시 페인터 (Scene Brush Tool)", EditorStyles.boldLabel);
            GUI.color = Color.white;

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = (_brushMode == FoliageBrushMode.Off) ? new Color(0.7f, 0.7f, 0.7f) : Color.white;
            if (GUILayout.Button("선택 모드 (Off)", GUILayout.Height(30))) _brushMode = FoliageBrushMode.Off;

            GUI.backgroundColor = (_brushMode == FoliageBrushMode.Paint) ? new Color(0.2f, 1f, 0.4f) : Color.white;
            if (GUILayout.Button("🌿 풀 심기 (Paint)", GUILayout.Height(30))) _brushMode = FoliageBrushMode.Paint;

            GUI.backgroundColor = (_brushMode == FoliageBrushMode.Erase) ? new Color(1f, 0.35f, 0.35f) : Color.white;
            if (GUILayout.Button("🧹 풀 지우기 (Erase)", GUILayout.Height(30))) _brushMode = FoliageBrushMode.Erase;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_brushMode != FoliageBrushMode.Off)
            {
                EditorGUILayout.HelpBox(
                    _brushMode == FoliageBrushMode.Paint 
                        ? "• [심기 모드]: 씬 뷰에서 좌클릭 드래그로 원하는 위치에 풀을 칠합니다.\n• [단축키]: Shift 키를 누르고 드래그하면 즉시 지우개(Erase)로 전환됩니다." 
                        : "• [지우기 모드]: 씬 뷰에서 좌클릭 드래그로 브러시 반경 내의 풀을 지웁니다.",
                    MessageType.Info);

                _brushRadius = EditorGUILayout.Slider(new GUIContent("브러시 반경", "Brush Radius: 식생을 심거나 지우는 원형 브러시 반경 (단위: m)"), _brushRadius, 1.0f, 35.0f);
                _brushSpacing = EditorGUILayout.Slider(new GUIContent("풀포기 밀도 간격", "Brush Spacing: 식생 브러시 살포 시 풀포기 간 거리 간격 (단위: m)"), _brushSpacing, 0.6f, 2.5f);
                _brushFalloff = EditorGUILayout.Slider(new GUIContent("외곽 부드러움 감쇄", "Soft Falloff: 브러시 경계면에서 밀도가 부드럽게 감쇄하는 비율"), _brushFalloff, 0.0f, 1.0f);

                // 식생 프로토타입 선택 드롭다운
                List<string> protoOptions = new List<string> { "🌿 [복합 자연 혼합] (지피+장초+들꽃+덤불)" };
                if (tgt.prototypes != null)
                {
                    for (int p = 0; p < tgt.prototypes.Count; p++)
                    {
                        protoOptions.Add($"[{p}] {tgt.prototypes[p].protoName}");
                    }
                }
                int currentOption = _selectedProtoIndex + 1;
                int newOption = EditorGUILayout.Popup(new GUIContent("살포 식생 종류", "Selected Prototype: 브러시로 직접 칠할 개별 풀 종류 또는 복합 자연 혼합 선택"), currentOption, protoOptions.ToArray());
                _selectedProtoIndex = newOption - 1;
            }
            EditorGUILayout.EndVertical();

            // =========================================================================
            // 🗺️ 1. 대상 터레인 및 저장 경로
            // =========================================================================
            EditorGUILayout.Space(6);
            _foldTerrain = EditorGUILayout.Foldout(_foldTerrain, "🗺️ 대상 터레인 및 저장 경로 (Terrain & Storage)", true, EditorStyles.foldoutHeader);
            if (_foldTerrain)
            {
                EditorGUI.indentLevel++;
                SerializedProperty propTerrain = serializedObject.FindProperty("targetTerrain");
                SerializedProperty propMapFolder = serializedObject.FindProperty("mapFolderName");

                EditorGUILayout.PropertyField(propTerrain, new GUIContent("대상 터레인 참조", "Target Terrain: 풀을 생성하고 지형 높이 및 법선을 추종할 Unity Terrain 컴포넌트"));
                EditorGUILayout.PropertyField(propMapFolder, new GUIContent("데이터 파일 저장 폴더", "Map Folder Name: Assets/Settings/Vegetation/Data/ 하위에 생성될 전장 폴더 명칭 (비어있으면 현재 씬 이름 자동 사용)"));
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // 🌾 2. 3D 풀 프로토타입 목록
            // =========================================================================
            EditorGUILayout.Space(6);
            SerializedProperty propPrototypes = serializedObject.FindProperty("prototypes");
            int protoCount = propPrototypes.arraySize;
            _foldPrototypes = EditorGUILayout.Foldout(_foldPrototypes, $"🌾 3D 풀 프로토타입 목록 ({protoCount}종 등록됨)", true, EditorStyles.foldoutHeader);
            if (_foldPrototypes)
            {
                EditorGUI.indentLevel++;
                int newProtoCount = EditorGUILayout.IntField(new GUIContent("프로토타입 등록 수", "Prototypes Count: 렌더링할 식생 종류의 총 개수"), protoCount);
                if (newProtoCount != protoCount && newProtoCount >= 0)
                {
                    propPrototypes.arraySize = newProtoCount;
                }

                for (int i = 0; i < propPrototypes.arraySize; i++)
                {
                    SerializedProperty elem = propPrototypes.GetArrayElementAtIndex(i);
                    SerializedProperty pName = elem.FindPropertyRelative("protoName");
                    SerializedProperty pMesh = elem.FindPropertyRelative("mesh");
                    SerializedProperty pMat = elem.FindPropertyRelative("material");
                    SerializedProperty pMinScale = elem.FindPropertyRelative("minScale");
                    SerializedProperty pMaxScale = elem.FindPropertyRelative("maxScale");
                    SerializedProperty pHeightMult = elem.FindPropertyRelative("heightMultiplier");
                    SerializedProperty pGroundEmbed = elem.FindPropertyRelative("groundEmbedOffset");
                    SerializedProperty pAlignToGround = elem.FindPropertyRelative("alignToGround");
                    SerializedProperty pSlopeSink = elem.FindPropertyRelative("slopeSinkFactor");
                    SerializedProperty pDensityThreshold = elem.FindPropertyRelative("densityThreshold");
                    SerializedProperty pTargetBiome = elem.FindPropertyRelative("targetBiome");
                    
                    SerializedProperty pStealth = elem.FindPropertyRelative("stealthCoverage");
                    SerializedProperty pSpeedMult = elem.FindPropertyRelative("speedMultiplier");
                    SerializedProperty pFlammability = elem.FindPropertyRelative("flammability");
                    SerializedProperty pBurnDuration = elem.FindPropertyRelative("burnDuration");

                    if (!_protoFoldouts.ContainsKey(i)) _protoFoldouts[i] = (i == 0);
                    if (!_protoTacticalFoldouts.ContainsKey(i)) _protoTacticalFoldouts[i] = false;

                    string foldoutTitle = $"[{i}] {pName.stringValue}";
                    _protoFoldouts[i] = EditorGUILayout.Foldout(_protoFoldouts[i], foldoutTitle, true);
                    if (_protoFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(pName, new GUIContent("식생 명칭", "Proto Name: 식생의 고유 명칭"));
                        EditorGUILayout.PropertyField(pMesh, new GUIContent("3D 메쉬 모델", "Mesh: 인스턴싱으로 렌더링할 3D 식생 메쉬"));
                        EditorGUILayout.PropertyField(pMat, new GUIContent("인스턴싱 머티리얼", "Material: GPU DrawMeshInstanced를 지원하는 식생 머티리얼"));
                        EditorGUILayout.PropertyField(pMinScale, new GUIContent("최소 크기 배율", "Min Scale: 풀포기의 최소 무작위 스케일 (0.3 ~ 3.0)"));
                        EditorGUILayout.PropertyField(pMaxScale, new GUIContent("최대 크기 배율", "Max Scale: 풀포기의 최대 무작위 스케일 (0.3 ~ 3.0)"));
                        EditorGUILayout.PropertyField(pHeightMult, new GUIContent("높이(Y) 압축 배율", "Height Multiplier: Y축 높이 스케일 압축 계수 (기본 0.42로 솟구치지 않고 바닥에 납작한 융단 형성)"));
                        EditorGUILayout.PropertyField(pGroundEmbed, new GUIContent("지면 파묻힘 오프셋", "Ground Embed Offset: 바닥 파묻힘 오프셋 (단위: m, -0.4 ~ 0.1, 피벗을 지면에 밀착)"));
                        EditorGUILayout.PropertyField(pAlignToGround, new GUIContent("지면 경사면 추종률", "Align To Ground: 지형 경사면 법선 정렬 반영 비율 (0 = 수직 기립, 1 = 지표면 100% 일치)"));
                        EditorGUILayout.PropertyField(pSlopeSink, new GUIContent("경사지 추가 함몰 계수", "Slope Sink Factor: 경사면 추가 침하 계수 (경사도 비례 피벗 파묻힘으로 잎 허공 부유 방지)"));
                        EditorGUILayout.PropertyField(pDensityThreshold, new GUIContent("군락 밀도 임계값", "Density Threshold: 스플랫맵 텍스처 가중치 배치 임계값 (0.0 ~ 1.0)"));
                        EditorGUILayout.PropertyField(pTargetBiome, new GUIContent("서식 바이옴 분류", "Target Biome: 풀이 주로 분포하는 서식 바이옴 (PlainsCarpet, ForestShade, HighlandDry 등)"));

                        // 서브 접이식: 전술 은폐, 이동 저항 & 인화성
                        _protoTacticalFoldouts[i] = EditorGUILayout.Foldout(_protoTacticalFoldouts[i], "⚔️ 전술 은폐, 이동 저항 & 인화성 (Tactical Properties)", true);
                        if (_protoTacticalFoldouts[i])
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(pStealth, new GUIContent("은신 은폐율", "Stealth Coverage: 유닛 은폐 기여도 (0: 지피잔디 노출 -> 1.0: 빽빽한 이삭/고사리 완전 은신)"));
                            EditorGUILayout.PropertyField(pSpeedMult, new GUIContent("이동속도 배율", "Speed Multiplier: 통과 유닛 이동속도 계수 (0.68: 거대 수풀 헤침 -> 0.98: 지피잔디 저항 미미)"));
                            EditorGUILayout.PropertyField(pFlammability, new GUIContent("화재 인화성", "Flammability: 착화 및 화염 전이 민감도 (0.1: 수분잔디 -> 1.0: 폭발착화 마른이삭/벼밭)"));
                            EditorGUILayout.PropertyField(pBurnDuration, new GUIContent("연소 지속 시간", "Burn Duration: 화재 발생 시 완전히 전소되어 잿더미로 변할 때까지 걸리는 시간 (초)"));
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // 📐 3. 공간 분할 및 카메라 스트리밍
            // =========================================================================
            EditorGUILayout.Space(6);
            _foldStreaming = EditorGUILayout.Foldout(_foldStreaming, "📐 카메라 스트리밍 & 공간 분할 (Streaming & Grid)", true, EditorStyles.foldoutHeader);
            if (_foldStreaming)
            {
                EditorGUI.indentLevel++;
                SerializedProperty propMaxRenderDist = serializedObject.FindProperty("maxRenderDistance");
                SerializedProperty propChunkSize = serializedObject.FindProperty("chunkSize");
                SerializedProperty propSampleSpacing = serializedObject.FindProperty("sampleSpacing");

                EditorGUILayout.PropertyField(propMaxRenderDist, new GUIContent("카메라 가시 거리 (최대)", "Max Render Distance: 풀이 보이기 시작하는 카메라와 지면 사이의 최대 유효 거리 (단위: m, 기본: 80m). 이 거리보다 멀어지면 렌더링 연산 0!"));
                EditorGUILayout.PropertyField(propChunkSize, new GUIContent("공간 분할 청크 크기", "Chunk Size: 전장을 격자로 분할하는 청크 한 변의 크기 (단위: m, 기본: 32m)"));
                EditorGUILayout.PropertyField(propSampleSpacing, new GUIContent("풀포기 살포 간격", "Sample Spacing: 지면에 풀을 심는 간격 (단위: m, 1.5~6.0m, 기본: 3.8m로 흙과 자연스럽게 혼합)"));
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // 🌿 4. 복합 식생 다종 혼합 살포
            // =========================================================================
            EditorGUILayout.Space(6);
            _foldBlending = EditorGUILayout.Foldout(_foldBlending, "🌿 복합 식생 다종 혼합 살포 (Multi-Species Blending)", true, EditorStyles.foldoutHeader);
            if (_foldBlending)
            {
                EditorGUI.indentLevel++;
                SerializedProperty propEnableBlending = serializedObject.FindProperty("enableMultiSpeciesBlending");
                SerializedProperty propTallGrassChance = serializedObject.FindProperty("tallGrassChance");
                SerializedProperty propWildflowerChance = serializedObject.FindProperty("wildflowerPatchChance");
                SerializedProperty propFernShrubChance = serializedObject.FindProperty("fernAndShrubChance");

                EditorGUILayout.PropertyField(propEnableBlending, new GUIContent("다종 혼합 살포 활성화", "Enable Multi Species Blending: 지피 잔디 + 키 큰 이삭 + 야생화 + 고사리 등이 한 청크에 자연스럽게 섞여 피어나도록 활성화"));
                EditorGUILayout.PropertyField(propTallGrassChance, new GUIContent("키 큰 이삭/풀포기 출현 확률", "Tall Grass Chance: 다종 혼합 시 키 큰 이삭/장초가 섞여 나올 확률 (0.1 ~ 0.9, 기본: 55%)"));
                EditorGUILayout.PropertyField(propWildflowerChance, new GUIContent("야생화(꽃) 군락 패치 출현 빈도", "Wildflower Patch Chance: 다종 혼합 시 야생화 꽃 무리가 피어날 확률 (0.0 ~ 0.6, 기본: 25%)"));
                EditorGUILayout.PropertyField(propFernShrubChance, new GUIContent("고사리 및 관목 덤불 출현 빈도", "Fern And Shrub Chance: 다종 혼합 시 고사리 및 관목이 섞여 나올 확률 (0.0 ~ 0.5, 기본: 20%)"));
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // 🏷️ 5. 청크별 커스텀 바이옴/밀도 오버라이드
            // =========================================================================
            EditorGUILayout.Space(6);
            SerializedProperty propChunkOverrides = serializedObject.FindProperty("chunkOverrides");
            _foldOverrides = EditorGUILayout.Foldout(_foldOverrides, $"🏷️ 청크별 커스텀 바이옴/밀도 오버라이드 ({propChunkOverrides.arraySize}개 지정됨)", true, EditorStyles.foldoutHeader);
            if (_foldOverrides)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propChunkOverrides, new GUIContent("청크 오버라이드 목록", "Chunk Overrides: 특정 좌표 청크에 바이옴 종류나 식생 밀도를 개별 지정하는 목록"), true);
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // 🎬 6. 씬 뷰(Scene View) 전용 렌더링 옵션
            // =========================================================================
            EditorGUILayout.Space(6);
            _foldSceneView = EditorGUILayout.Foldout(_foldSceneView, "🎬 씬 뷰(Scene View) 전용 렌더링 옵션", true, EditorStyles.foldoutHeader);
            if (_foldSceneView)
            {
                EditorGUI.indentLevel++;
                SerializedProperty propSceneViewDist = serializedObject.FindProperty("sceneViewRenderDistance");
                SerializedProperty propSceneViewHeight = serializedObject.FindProperty("sceneViewIgnoreHeightLimit");
                SerializedProperty propShowGizmos = serializedObject.FindProperty("showChunkGizmosInSceneView");

                EditorGUILayout.PropertyField(propSceneViewDist, new GUIContent("씬 뷰 렌더링 가시 거리", "Scene View Render Distance: 에디터 씬 뷰에서 줌아웃 시에도 풀을 넓게 조망할 수 있는 가시 거리 (단위: m, 기본: 450m)"));
                EditorGUILayout.PropertyField(propSceneViewHeight, new GUIContent("씬 뷰 고도 제한 해제", "Scene View Ignore Height Limit: 씬 뷰에서 카메라 고도(Y축 높이) 제한을 해제하여 높은 각도나 쿼터뷰에서도 풀이 보이도록 허용"));
                EditorGUILayout.PropertyField(propShowGizmos, new GUIContent("씬 뷰 청크 기즈모 표시", "Show Chunk Gizmos In Scene View: 풀 데이터가 생성된 청크 영역을 직관적인 와이어 그리드 기즈모로 표시"));
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // 🔥 7. 화재 연소 및 바람 영향 시뮬레이션
            // =========================================================================
            EditorGUILayout.Space(6);
            _foldFire = EditorGUILayout.Foldout(_foldFire, "🔥 화재 VFX 프리팹 및 바람 확산 물리 (Fire & Wind)", true, EditorStyles.foldoutHeader);
            if (_foldFire)
            {
                EditorGUI.indentLevel++;
                SerializedProperty propFireVfx = serializedObject.FindProperty("fireVfxPrefab");
                SerializedProperty propBaseSpread = serializedObject.FindProperty("baseSpreadChance");
                SerializedProperty propDownwindBonus = serializedObject.FindProperty("downwindSpreadBonus");
                SerializedProperty propUpwindPenalty = serializedObject.FindProperty("upwindSpreadPenalty");

                EditorGUILayout.PropertyField(propFireVfx, new GUIContent("화재 연소 VFX 프리팹", "Fire Vfx Prefab: 청크 연소 시 불타오르는 화염, 연기, 불티, 화재 조명 VFX 프리팹"));
                EditorGUILayout.PropertyField(propBaseSpread, new GUIContent("기본 화재 전이 확률", "Base Spread Chance: 풀포기 간 기본 화재 전이 확률 (0.1 ~ 0.9, 기본: 38% - 무조건 번지지 않고 자연 진화 발생)"));
                EditorGUILayout.PropertyField(propDownwindBonus, new GUIContent("순풍 방향 확산 보너스", "Downwind Spread Bonus: 순풍 방향 화재 전이 확률 가중 배율 (기본: 1.6배 - 바람 부는 쪽으로 불씨 비산)"));
                EditorGUILayout.PropertyField(propUpwindPenalty, new GUIContent("역풍 방향 확산 저항 계수", "Upwind Spread Penalty: 역풍 방향 화재 전이 저항 계수 (기본: 0.20배 - 바람을 거스르면 불길 차단)"));
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // 🪵 8. 연소 잿더미 & 그을음 흔적 튜닝
            // =========================================================================
            EditorGUILayout.Space(6);
            _foldAsh = EditorGUILayout.Foldout(_foldAsh, "🪵 연소 잿더미 & 그을음 흔적 튜닝 (Ash & Scorch Tuning)", true, EditorStyles.foldoutHeader);
            if (_foldAsh)
            {
                EditorGUI.indentLevel++;
                SerializedProperty propAshCore = serializedObject.FindProperty("ashCoreColor");
                SerializedProperty propAshEdge = serializedObject.FindProperty("ashEdgeColor");
                SerializedProperty propAshOpacity = serializedObject.FindProperty("ashOpacity");
                SerializedProperty propAshRadius = serializedObject.FindProperty("ashRadiusScale");
                SerializedProperty propCharredProgress = serializedObject.FindProperty("charredBurnProgress");
                SerializedProperty propSeasonalFire = serializedObject.FindProperty("enableSeasonalFireSimulation");

                EditorGUILayout.PropertyField(propAshCore, new GUIContent("잿더미 중심부 코어 색상", "Ash Core Color: 불탄 자리 잿더미의 중심 코어 색상 (기본: 은은한 회갈색 잿빛 투과형)"));
                EditorGUILayout.PropertyField(propAshEdge, new GUIContent("잿더미 외곽 페더링 색상", "Ash Edge Color: 불탄 자리 잿더미의 외곽 페더링 색상 (RGB 색감 유지, 알파는 0으로 페이드아웃)"));
                EditorGUILayout.PropertyField(propAshOpacity, new GUIContent("잿더미 전체 불투명도", "Ash Opacity: 잿더미 전체 불투명도 배율 (낮을수록 지형 바닥 텍스처가 더 투명하게 비침, 기본 1.0)"));
                EditorGUILayout.PropertyField(propAshRadius, new GUIContent("잿더미 번짐 반경 배율", "Ash Radius Scale: 풀포기 크기 대비 잿더미 번짐 반경 배율 (기본: 1.35배)"));
                EditorGUILayout.PropertyField(propCharredProgress, new GUIContent("3D 숯 밑동 탄화 진행도", "Charred Burn Progress: 연소 완료 후 남는 3D 숯 풀 밑동의 탄화 진행도 (0.2 ~ 0.8, 기본 0.52)"));
                EditorGUILayout.PropertyField(propSeasonalFire, new GUIContent("사계절 화재 물리 자동 동기화", "Enable Seasonal Fire Simulation: 현재 계절(봄/여름/가을/겨울/설원)에 따라 잿더미 색상, 잔해 형태, 확산율을 실시간 자동 동기화"));
                EditorGUI.indentLevel--;
            }

            // =========================================================================
            // ⚙️ 9. 렌더링 최적화 & 그림자
            // =========================================================================
            EditorGUILayout.Space(6);
            _foldRendering = EditorGUILayout.Foldout(_foldRendering, "⚙️ 렌더링 최적화 & 그림자 (Rendering & Shadows)", true, EditorStyles.foldoutHeader);
            if (_foldRendering)
            {
                EditorGUI.indentLevel++;
                SerializedProperty propCastShadows = serializedObject.FindProperty("castShadows");
                SerializedProperty propReceiveShadows = serializedObject.FindProperty("receiveShadows");

                EditorGUILayout.PropertyField(propCastShadows, new GUIContent("그림자 생성 모드", "Cast Shadows: 식생의 실시간 그림자 캐스팅 모드 (Off, On, TwoSided, ShadowsOnly)"));
                EditorGUILayout.PropertyField(propReceiveShadows, new GUIContent("그림자 수신 여부", "Receive Shadows: 다른 물체나 지형의 그림자를 풀잎에 받을지 여부"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            // =========================================================================
            // 📊 10. 실시간 스트리밍 대시보드 (읽기 전용)
            // =========================================================================
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📊 실시간 스트리밍 대시보드 (읽기 전용)", EditorStyles.boldLabel);

            string statusText = $"• 생성된 총 청크 수: {tgt.GetChunks().Count:N0} 개\n" +
                               $"• 총 데이터 풀포기: {tgt.TotalInstanceCount:N0} 개\n" +
                               $"• 현재 렌더링 인스턴스: {tgt.RenderedInstanceCount:N0} 개\n" +
                               $"• 활성 GPU 드로우콜: {tgt.ActiveDrawCalls} 회";

            if (tgt.RenderedInstanceCount > 0)
            {
                EditorGUILayout.HelpBox(statusText + "\n[상태] 🟢 카메라 근접: 실시간 3D 풀 렌더링 활성 중!", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(statusText + "\n[상태] ⚪ 카메라 원거리: 렌더링 연산 0 (비용 없음)", MessageType.None);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 0열: 식생 메쉬 접지 재정규화
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.8f, 1f);
            if (GUILayout.Button("🌿 식생 메쉬 중심 뿌리 기준 접지 재정규화 (Reset Grounding)", GUILayout.Height(30)))
            {
                tgt.EnsureAllPrototypesGrounded(true);
                EditorUtility.SetDirty(tgt);
                SceneView.RepaintAll();
                EditorUtility.DisplayDialog("완료", "모든 식생 메쉬가 '중심 뿌리 기준 접지(Y=0.00m)'로 재정규화되었습니다!\n비탈길에서도 풀이 뒤집히지 않고 하늘을 향해 올바르게 기립합니다.", "확인");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(4);

            // 1열: 생성 및 초기화
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f, 1f);
            if (GUILayout.Button("🌾 전장 풀 데이터 즉시 생성 (Bake)", GUILayout.Height(34)))
            {
                tgt.BakeFoliageData();
                tgt.SaveFoliageDataToDisk();
                EditorUtility.SetDirty(tgt);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.3f, 1f);
            if (GUILayout.Button("🗑️ 초기화 (Clear)", GUILayout.Height(34), GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("초기화", "보관된 풀 데이터를 모두 비우시겠습니까?", "예", "아니오"))
                {
                    tgt.ClearFoliageData();
                    EditorUtility.SetDirty(tgt);
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            // 2열: 파일 영구 저장 및 로드
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 1f);
            if (GUILayout.Button("💾 디스크 파일로 저장 (Save)", GUILayout.Height(30)))
            {
                tgt.SaveFoliageDataToDisk();
            }

            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f, 1f);
            if (GUILayout.Button("📂 디스크에서 파일 로드 (Load)", GUILayout.Height(30)))
            {
                if (tgt.LoadFoliageDataFromDisk())
                {
                    EditorUtility.SetDirty(tgt);
                    SceneView.RepaintAll();
                }
                else
                {
                    EditorUtility.DisplayDialog("알림", "저장된 식생 데이터 파일이 없습니다. 먼저 Bake를 수행해주세요.", "확인");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // 3열: 씬 뷰 화면 강제 새로고침
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.85f, 0.85f, 1f, 1f);
            if (GUILayout.Button("🎬 씬 뷰 화면 즉시 새로고침 (Scene View Repaint)", GUILayout.Height(28)))
            {
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            // 4열: 🔥 화재 실험 및 진압
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.45f, 0.1f, 1f);
            if (GUILayout.Button("🔥 화면 중심 들불 착화 테스트 (Ignite)", GUILayout.Height(32)))
            {
                tgt.IgniteAtCameraFocus();
                EditorUtility.SetDirty(tgt);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.9f, 1f);
            if (GUILayout.Button("🧯 화재 진압 및 식생 복구 (Extinguish)", GUILayout.Height(32), GUILayout.Width(130)))
            {
                tgt.ExtinguishAndRestoreAll();
                EditorUtility.SetDirty(tgt);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
        }

        private static Vector3 _lastPaintPos = Vector3.negativeInfinity;

        private void OnSceneGUI()
        {
            TacticalGrassRenderer tgt = (TacticalGrassRenderer)target;
            if (tgt == null || _brushMode == FoliageBrushMode.Off) return;

            Event current = Event.current;

            // 1. 카메라 조작(Alt 키) 시 브러시 우선권 양보
            if (current.alt) return;

            // 2. 에디터 컨트롤 ID 획득 및 레이아웃 단계 등록 (씬 뷰 기본 선택 박스 방어)
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            // 3. 대상 터레인 유효성 보장
            if (tgt.targetTerrain == null)
            {
                tgt.targetTerrain = tgt.GetComponent<Terrain>() ?? Terrain.activeTerrain;
            }

            // 4. 지형 표면 타격점 검출 (Physics.Raycast + 수치 해석적 폴백)
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 hitPoint = Vector3.zero;
            Vector3 hitNormal = Vector3.up;
            bool hasHit = false;

            // 물리 레이캐스트 (모든 레이어 대상)
            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                hasHit = true;
            }
            else if (tgt.targetTerrain != null && tgt.targetTerrain.terrainData != null)
            {
                // 지형 수평면 교차 후 정밀 높이/법선 샘플링 폴백
                Vector3 tPos = tgt.targetTerrain.transform.position;
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0, tPos.y, 0));
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 p = ray.GetPoint(enter);
                    float sampleY = tgt.targetTerrain.SampleHeight(p) + tPos.y;
                    hitPoint = new Vector3(p.x, sampleY, p.z);
                    hitNormal = Vector3.up;
                    hasHit = true;
                }
            }

            if (!hasHit) return;

            // 5. Shift 키 누름 감지 (즉시 지우개 전환)
            bool isShift = current.shift;
            bool isErase = (_brushMode == FoliageBrushMode.Erase) || isShift;

            // 6. 브러시 디스크 기즈모 렌더링
            Color discColor = isErase 
                ? new Color(1f, 0.22f, 0.22f, 0.32f) 
                : new Color(0.18f, 1f, 0.45f, 0.32f);
            Color wireColor = isErase 
                ? new Color(1f, 0.15f, 0.15f, 0.95f) 
                : new Color(0.12f, 1f, 0.35f, 0.95f);

            Handles.color = discColor;
            Handles.DrawSolidDisc(hitPoint, hitNormal, _brushRadius);

            Handles.color = wireColor;
            Handles.DrawWireDisc(hitPoint, hitNormal, _brushRadius);

            // 소프트 감쇄 코어 반경 보조선
            if (_brushFalloff > 0.05f)
            {
                Handles.color = new Color(wireColor.r, wireColor.g, wireColor.b, 0.45f);
                Handles.DrawWireDisc(hitPoint, hitNormal, _brushRadius * Mathf.Clamp01(1.0f - _brushFalloff));
            }

            // 7. 마우스 클릭 & 드래그 인터랙션 (HotControl 완벽 제어)
            if (current.button == 0)
            {
                if (current.type == EventType.MouseDown)
                {
                    GUIUtility.hotControl = controlId;
                    ExecuteBrushAction(tgt, hitPoint, isErase);
                    _lastPaintPos = hitPoint;
                    current.Use();
                }
                else if (current.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
                {
                    // 마우스 이동 거리가 최소 간격의 40% 이상일 때 연속 살포
                    if (Vector3.Distance(hitPoint, _lastPaintPos) >= _brushSpacing * 0.42f)
                    {
                        ExecuteBrushAction(tgt, hitPoint, isErase);
                        _lastPaintPos = hitPoint;
                    }
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    _lastPaintPos = Vector3.negativeInfinity;
                    current.Use();
                }
            }
            else if (current.type == EventType.MouseMove)
            {
                SceneView.RepaintAll();
            }
        }

        private static void ExecuteBrushAction(TacticalGrassRenderer tgt, Vector3 hitPoint, bool isErase)
        {
            Undo.RecordObject(tgt, isErase ? "Erase Grass" : "Paint Grass");

            if (isErase)
            {
                tgt.EraseGrassAt(hitPoint, _brushRadius);
            }
            else
            {
                tgt.PaintGrassAt(hitPoint, _brushRadius, _brushSpacing, _selectedProtoIndex, _brushFalloff);
            }

            EditorUtility.SetDirty(tgt);
            SceneView.RepaintAll();
        }
    }
}
