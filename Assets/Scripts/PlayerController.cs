using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("레이어 설정")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask unitLayer;

    [Header("프리뷰어 연결")]
    [SerializeField] private FormationPreviewer previewer;

    [Header("선택된 부대 및 유닛")]
    private List<Squad> selectedSquads = new List<Squad>();

    public bool IsSquadSelected(Squad squad) => squad != null && selectedSquads.Contains(squad);
    public IReadOnlyList<Squad> SelectedSquads => selectedSquads;

    private Vector3 formationStartWorldPos;
    public List<Unit> selectedUnits = new List<Unit>();
    public List<Unity.Entities.Entity> selectedECSEntities = new List<Unity.Entities.Entity>();
    public IReadOnlyList<Unity.Entities.Entity> SelectedECSEntities => selectedECSEntities;

    // 라인 렌더러
    private LineRenderer activeDragLine;

    // 부대 지정 (Ctrl / Shift + 1~0 저장 / 1~0 선택)
    private readonly Dictionary<int, (List<Squad> squads, List<Unit> units, List<Unity.Entities.Entity> ecsEntities)> controlGroups
        = new Dictionary<int, (List<Squad>, List<Unit>, List<Unity.Entities.Entity>)>();

    // 전술 그룹화 (Locked Group / 진형 고정 군단)
    private readonly HashSet<int> lockedControlGroups = new HashSet<int>();
    private readonly List<List<Squad>> adHocLockedGroups = new List<List<Squad>>();

    public bool IsCurrentSelectionLocked()
    {
        if (selectedSquads.Count < 2) return false;
        foreach (var kvp in controlGroups)
        {
            if (lockedControlGroups.Contains(kvp.Key) && IsSelectionIdenticalToGroup(kvp.Key)) return true;
        }
        foreach (var group in adHocLockedGroups)
        {
            if (group.Count == selectedSquads.Count && group.TrueForAll(s => selectedSquads.Contains(s))) return true;
        }
        return false;
    }

    public void ToggleLockCurrentGroup()
    {
        if (selectedSquads.Count < 2)
        {
            Debug.LogWarning("[PlayerController] ⚠️ 2개 이상의 부대를 선택해야 군단 그룹화(G)를 잠글 수 있습니다.");
            return;
        }

        bool currentlyLocked = IsCurrentSelectionLocked();
        if (currentlyLocked)
        {
            foreach (var kvp in controlGroups)
            {
                if (IsSelectionIdenticalToGroup(kvp.Key)) lockedControlGroups.Remove(kvp.Key);
            }
            adHocLockedGroups.RemoveAll(group => group.Count == selectedSquads.Count && group.TrueForAll(s => selectedSquads.Contains(s)));
            Debug.Log($"[PlayerController] 🔓 [G] 군단 잠금 해제됨 (부대 {selectedSquads.Count}개 개별 기동)");
        }
        else
        {
            bool foundControlGroup = false;
            foreach (var kvp in controlGroups)
            {
                if (IsSelectionIdenticalToGroup(kvp.Key))
                {
                    lockedControlGroups.Add(kvp.Key);
                    foundControlGroup = true;
                    Debug.Log($"[PlayerController] 🔒 [G] {kvp.Key}번 부대 그룹이 '진형 고정 군단(Locked Group)'으로 잠겼습니다.");
                }
            }
            if (!foundControlGroup)
            {
                adHocLockedGroups.Add(new List<Squad>(selectedSquads));
                Debug.Log($"[PlayerController] 🔒 [G] 선택된 {selectedSquads.Count}개 부대가 '진형 고정 군단(Locked Group)'으로 잠겼습니다.");
            }
        }

        UpdateCommandUI();
    }

    public void SelectAllPlayerSquads()
    {
        DeselectAll();
        if (BattleManager.Instance != null)
        {
            var squads = BattleManager.Instance.GetAllSquads();
            foreach (Squad s in squads)
            {
                if (s != null && s.isPlayer)
                {
                    SelectSquad(s);
                }
            }
        }
        Debug.Log($"[PlayerController] 🚩 [Ctrl+A] 전장의 모든 아군 부대({selectedSquads.Count}개)를 일괄 선택했습니다.");
    }

    // 자유 유닛 곡선 드래그용 궤적 점 목록
    private readonly List<Vector3> freeDrawPath = new List<Vector3>();

    // 카메라 이동 및 연타 감지
    private int lastPressedGroupSlot = -1;
    private float lastGroupClickTime = 0f;
    private const float DOUBLE_CLICK_TIME = 0.35f;

    // 드래그 상태 변수
    private Vector3 dragStartPosition;
    private bool isSelecting = false;

    public bool isAttackTargetingMode = false;
    private SquadFormationType currentFormationType = SquadFormationType.Line;
    private bool isDraggingFormation = false;
    private bool isCtrlLeftDragMove = false;   // Ctrl + 좌클릭 드래그 이동
    private bool isCtrlRightDragRotate = false; // Ctrl + 우클릭 드래그 회전
    private bool isAltLeftDragBend = false;    // Alt + 좌클릭 드래그 곡선 꺾기 / 쐐기 길이 조절 / 방진 크기 조절 (열 수 자동 연동)
    private bool isAltRightDragResize = false; // Alt + 우클릭 드래그 행/열 조절 / 쐐기 가로 산개폭 조절 / 방진 층간(열끼리) 거리 조절
    private float currentPreviewCurvature = 0f;
    private float initialFormationWidth = 1.0f;  // 쐐기/마름모 가로폭(밀집도) 초기값
    private float initialFormationLength = 1.0f; // 쐐기/마름모 전후 길이 / 방진 내부 크기 초기값
    private int initialFormationLayers = 2;      // 사각방진/원형진 겹수 초기값
    private float initialFormationLayersSpacing = 1.0f; // 방진 층간(열끼리) 거리 초기값

    private Camera mainCamera;

    public static PlayerController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        mainCamera = Camera.main;
        EnsureLineRenderer();
    }

    private void EnsureLineRenderer()
    {
        if (activeDragLine == null)
        {
            GameObject lineObj = new GameObject("DragLineRenderer");
            lineObj.transform.SetParent(transform);
            activeDragLine = lineObj.AddComponent<LineRenderer>();
            activeDragLine.startWidth = 0.15f;
            activeDragLine.endWidth = 0.15f;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) activeDragLine.material = new Material(shader);

            activeDragLine.startColor = Color.green;
            activeDragLine.endColor = Color.cyan;
            activeDragLine.positionCount = 0;
            activeDragLine.enabled = false;
        }
    }

    private void Update()
    {
        HandleSquadCreationAndDisband();
        HandleControlGroups();
        SyncLockedGroupSpeeds();
        HandleSelectionInput();
        HandleCommandInput();
        HandleHotkeyInput();
        HandleAttackTargetingInput();
    }

    private void SyncLockedGroupSpeeds()
    {
        // 부대 지정(Control Groups) 중 잠금된 그룹의 속도 동기화
        foreach (int groupIdx in lockedControlGroups)
        {
            if (controlGroups.TryGetValue(groupIdx, out var group) && group.squads != null && group.squads.Count > 1)
            {
                SyncSquadSpeedList(group.squads);
            }
        }

        // 임시 전술 그룹(Ad-hoc Locked Groups) 속도 동기화
        for (int i = adHocLockedGroups.Count - 1; i >= 0; i--)
        {
            var list = adHocLockedGroups[i];
            list.RemoveAll(s => s == null || s.members.Count == 0);
            if (list.Count > 1)
            {
                SyncSquadSpeedList(list);
            }
            else
            {
                adHocLockedGroups.RemoveAt(i);
            }
        }
    }

    private void SyncSquadSpeedList(List<Squad> squads)
    {
        float minSpeed = float.MaxValue;
        foreach (var s in squads)
        {
            if (s != null && s.members.Count > 0)
            {
                float sp = s.isRunning ? s.runSpeed : s.walkSpeed;
                if (sp < minSpeed) minSpeed = sp;
            }
        }

        if (minSpeed < float.MaxValue)
        {
            foreach (var s in squads)
            {
                if (s != null)
                {
                    s.targetSpeed = minSpeed;
                    foreach (var u in s.members)
                    {
                        if (u != null) u.SetRunMode(s.isRunning, minSpeed);
                    }
                }
            }
        }
    }

    #region Squad Creation & Disband

    private void HandleSquadCreationAndDisband()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            // 부대 해체: 부대를 해체한 후 부대원들을 자유 유닛 선택 상태로 유지
            if (selectedSquads.Count > 0)
            {
                List<Squad> toDisband = new List<Squad>(selectedSquads);
                List<Unit> releasedUnits = new List<Unit>();
                List<Unity.Entities.Entity> releasedECSEntities = new List<Unity.Entities.Entity>();
                HashSet<int> disbandedSquadIds = new HashSet<int>();

                foreach (Squad squad in toDisband)
                {
                    if (squad != null)
                    {
                        disbandedSquadIds.Add(squad.GetInstanceID());
                        foreach (Unit u in squad.members)
                        {
                            if (u != null) releasedUnits.Add(u);
                        }
                    }
                }

                // 순수 ECS 모드: 해체 대상 부대의 모든 엔티티를 수집
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated && disbandedSquadIds.Count > 0)
                {
                    var em = world.EntityManager;
                    var query = em.CreateEntityQuery(typeof(MiniTotalWar.ECS.UnitEntityTag));
                    using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                    {
                        for (int i = 0; i < entities.Length; i++)
                        {
                            var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(entities[i]);
                            if (disbandedSquadIds.Contains(tag.SquadId) && tag.IsAlive == 1)
                            {
                                releasedECSEntities.Add(entities[i]);
                            }
                        }
                    }
                }

                // 부대 실제 해체 및 소속 제거 실행
                foreach (Squad squad in toDisband)
                {
                    if (squad != null)
                    {
                        squad.DisbandSquad();
                    }
                }

                selectedSquads.Clear();
                selectedUnits.Clear();
                selectedECSEntities.Clear();

                // 해체된 유닛들을 자유 유닛 선택 상태로 즉시 유지
                if (releasedUnits.Count > 0)
                {
                    foreach (Unit u in releasedUnits)
                    {
                        if (u != null)
                        {
                            selectedUnits.Add(u);
                            u.SetSelected(true);
                        }
                    }
                }

                if (releasedECSEntities.Count > 0)
                {
                    foreach (var ent in releasedECSEntities)
                    {
                        selectedECSEntities.Add(ent);
                    }
                }

                UpdateCommandUI();
                Debug.Log($"[PlayerController] ⚡ 선택된 부대 {toDisband.Count}개가 해체되었으며, (GameObject: {releasedUnits.Count}명, ECS: {releasedECSEntities.Count}명)이 자유 유닛 선택 상태로 유지됩니다.");
            }
            // 부대 창설: 자유 유닛들을 모아 새 부대 창설
            else if (selectedUnits.Count > 0 || selectedECSEntities.Count > 0)
            {
                BattleManager battleManager = BattleManager.Instance;
                if (battleManager != null)
                {
                    if (selectedUnits.Count > 0)
                    {
                        Vector3 centerPos = Vector3.zero;
                        foreach (Unit u in selectedUnits) centerPos += u.transform.position;
                        centerPos /= selectedUnits.Count;

                        Squad newSquad = battleManager.SpawnSquad(true, 0, centerPos);
                        List<Unit> unitsToAdd = new List<Unit>(selectedUnits);

                        foreach (Unit u in unitsToAdd)
                        {
                            if (u != null)
                            {
                                if (u.mySquad != null) u.mySquad.DetachUnit(u);
                                u.mySquad = newSquad;
                                newSquad.members.Add(u);
                            }
                        }

                        newSquad.RebuildGridStructure();
                        DeselectAll();
                        SelectSquad(newSquad);
                        Debug.Log($"[PlayerController] 🛡️ GameObject 자유 유닛 {unitsToAdd.Count}명으로 새 부대가 창설되었습니다.");
                    }
                    else if (selectedECSEntities.Count > 0)
                    {
                        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                        if (world != null && world.IsCreated)
                        {
                            var em = world.EntityManager;
                            Vector3 centerPos = Vector3.zero;
                            int validCount = 0;

                            for (int i = 0; i < selectedECSEntities.Count; i++)
                            {
                                var ent = selectedECSEntities[i];
                                if (em.Exists(ent))
                                {
                                    var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                                    centerPos += (Vector3)mov.Position;
                                    validCount++;
                                }
                            }

                            if (validCount > 0)
                            {
                                centerPos /= validCount;
                                Squad newSquad = battleManager.SpawnSquad(true, 0, centerPos);
                                int newSquadId = newSquad.GetInstanceID();
                                newSquad.initialUnitCount = validCount;

                                for (int i = 0; i < selectedECSEntities.Count; i++)
                                {
                                    var ent = selectedECSEntities[i];
                                    if (em.Exists(ent))
                                    {
                                        var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(ent);
                                        tag.SquadId = newSquadId;
                                        tag.IsFreeUnit = 0;
                                        tag.SlotIndex = i;
                                        em.SetComponentData(ent, tag);
                                    }
                                }

                                DeselectAll();
                                SelectSquad(newSquad);
                                Debug.Log($"[PlayerController] ⚡ 순수 ECS 자유 유닛 {validCount}명으로 새 부대가 창설되었습니다.");
                            }
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Control Groups

    private void HandleControlGroups()
    {
        for (int i = 0; i <= 9; i++)
        {
            KeyCode alphaKey = (i == 0) ? KeyCode.Alpha0 : (KeyCode.Alpha0 + i);
            KeyCode keypadKey = (i == 0) ? KeyCode.Keypad0 : (KeyCode.Keypad0 + i);

            if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
            {
                bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                // Ctrl + 숫자키 또는 Shift + 숫자키: 현재 선택을 해당 부대 번호에 저장/해제
                if (isCtrlPressed || isShiftPressed)
                {
                    if (selectedSquads.Count == 0 && selectedUnits.Count == 0 && selectedECSEntities.Count == 0)
                    {
                        controlGroups.Remove(i);
                        Debug.Log($"[PlayerController] 🗑️ 부대 지정 {i}번 슬롯이 해제되었습니다.");
                    }
                    else if (isShiftPressed && IsSelectionIdenticalToGroup(i))
                    {
                        controlGroups.Remove(i);
                        Debug.Log($"[PlayerController] 🗑️ 부대 지정 {i}번 슬롯이 해제되었습니다.");
                    }
                    else
                    {
                        List<Squad> sqs = new List<Squad>(selectedSquads);
                        List<Unit> uns = new List<Unit>(selectedUnits);
                        List<Unity.Entities.Entity> ecsEnts = new List<Unity.Entities.Entity>(selectedECSEntities);
                        controlGroups[i] = (sqs, uns, ecsEnts);
                        Debug.Log($"[PlayerController] 🎯 부대 지정 {i}번 슬롯 저장 완료! (부대: {sqs.Count}개, 개별 유닛: {uns.Count}명, 자유 ECS 유닛: {ecsEnts.Count}명)");
                    }
                }
                // 단독 숫자키: 부대 번호 선택
                else
                {
                    if (controlGroups.TryGetValue(i, out var group))
                    {
                        DeselectAll();
                        foreach (var s in group.squads) { if (s != null && s.MemberCount > 0) SelectSquad(s); }
                        foreach (var u in group.units) { if (u != null) SelectUnit(u); }

                        // ⚡ 순수 ECS 자유 유닛 선택 복원
                        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                        if (world != null && world.IsCreated && group.ecsEntities != null)
                        {
                            var em = world.EntityManager;
                            foreach (var ent in group.ecsEntities)
                            {
                                if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitEntityTag>(ent))
                                {
                                    var tag = em.GetComponentData<MiniTotalWar.ECS.UnitEntityTag>(ent);
                                    if (tag.IsAlive == 1 && !selectedECSEntities.Contains(ent))
                                    {
                                        selectedECSEntities.Add(ent);
                                    }
                                }
                            }
                        }

                        UpdateCommandUI();

                        if (lastPressedGroupSlot == i && (Time.time - lastGroupClickTime) <= DOUBLE_CLICK_TIME)
                        {
                            FocusCameraOnSelection();
                        }

                        lastPressedGroupSlot = i;
                        lastGroupClickTime = Time.time;
                        Debug.Log($"[PlayerController] 🎯 {i}번 부대 선택 완료! (부대: {selectedSquads.Count}개, 자유 유닛: {selectedUnits.Count + selectedECSEntities.Count}명)");
                    }
                }
            }
        }
    }

    private bool IsSelectionIdenticalToGroup(int i)
    {
        if (!controlGroups.TryGetValue(i, out var group)) return false;

        if (selectedSquads.Count != group.squads.Count) return false;
        if (selectedUnits.Count != group.units.Count) return false;
        if (selectedECSEntities.Count != (group.ecsEntities != null ? group.ecsEntities.Count : 0)) return false;

        foreach (var s in selectedSquads) if (!group.squads.Contains(s)) return false;
        foreach (var u in selectedUnits) if (!group.units.Contains(u)) return false;
        if (group.ecsEntities != null)
        {
            foreach (var ent in selectedECSEntities) if (!group.ecsEntities.Contains(ent)) return false;
        }

        return true;
    }

    private void FocusCameraOnSelection()
    {
        Vector3 sumPos = Vector3.zero;
        int count = 0;

        foreach (Squad s in selectedSquads)
        {
            if (s != null) { sumPos += s.transform.position; count++; }
        }
        foreach (Unit u in selectedUnits)
        {
            if (u != null) { sumPos += u.transform.position; count++; }
        }

        // ⚡ 순수 ECS 자유 유닛 위치 포함
        if (selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                foreach (var ent in selectedECSEntities)
                {
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitMovementData>(ent))
                    {
                        sumPos += (Vector3)em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent).Position;
                        count++;
                    }
                }
            }
        }

        if (count > 0 && mainCamera != null)
        {
            Vector3 targetCenter = sumPos / count;
            CameraController camCtrl = mainCamera.GetComponent<CameraController>();
            if (camCtrl != null)
            {
                camCtrl.FocusOnPosition(targetCenter);
            }
            else
            {
                Vector3 currentCamPos = mainCamera.transform.position;
                Vector3 camForward = mainCamera.transform.forward;
                float distance = Mathf.Abs(currentCamPos.y / Mathf.Max(0.001f, Mathf.Abs(camForward.y)));
                mainCamera.transform.position = targetCenter - (camForward * distance);
            }
        }
    }

    #endregion

    #region Selection Input

    private void HandleSelectionInput()
    {
        // [Ctrl + A] 전장의 모든 아군 부대 일괄 선택
        if (Input.GetKeyDown(KeyCode.A) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            SelectAllPlayerSquads();
            return;
        }

        // ESC 키로 드래그 선택 및 Alt 곡선 조작 취소
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSelecting || isCtrlLeftDragMove || isAltLeftDragBend)
            {
                isSelecting = false;
                isCtrlLeftDragMove = false;
                isAltLeftDragBend = false;
                if (activeDragLine != null) activeDragLine.enabled = false;
                if (previewer != null) previewer.HidePreview();
                Debug.Log("[PlayerController] ❌ [ESC] 좌클릭 드래그 조작이 취소되었습니다.");
                return;
            }
        }

        // [Alt + 좌클릭] 드래그 곡선 꺾기 (Curve Bending / Grand Army Arc) 시작
        if (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (selectedSquads.Count > 0 && GetMouseWorldPosition(out Vector3 hitPoint))
            {
                formationStartWorldPos = hitPoint;
                isAltLeftDragBend = true;
                Squad primarySquad = selectedSquads[0];
                initialFormationLength = (primarySquad.currentFormationType == SquadFormationType.Square || primarySquad.currentFormationType == SquadFormationType.Circle) 
                    ? primarySquad.hollowRadiusMultiplier 
                    : primarySquad.formationLengthMultiplier;
                initialFormationLayers = primarySquad.formationLayers;
                currentPreviewCurvature = primarySquad.customCurvature;
                activeDragLine.enabled = true;
            }
            return;
        }

        // [Alt + 좌클릭] 드래그 중 실시간 프리뷰
        if (Input.GetMouseButton(0) && isAltLeftDragBend)
        {
            if (GetMouseWorldPosition(out Vector3 currentWorldPos))
            {
                Squad primarySquad = (selectedSquads.Count > 0) ? selectedSquads[0] : null;
                if (primarySquad == null) return;

                bool isSquareOrCircle = (primarySquad.currentFormationType == SquadFormationType.Square || primarySquad.currentFormationType == SquadFormationType.Circle);
                bool isWedgeOrDiamond = (primarySquad.currentFormationType == SquadFormationType.Wedge || primarySquad.currentFormationType == SquadFormationType.Diamond);

                if (isSquareOrCircle)
                {
                    // 🛡️ 사각방진 / 원형진: 바깥으로 당기면 1열 단위로 부드럽게 얇아지면서 넓어짐 (유닛 간격 1.0m 초밀착 방패벽 100% 고정)
                    Vector3 center = primarySquad.transform.position;
                    float initialDist = Vector3.Distance(formationStartWorldPos, center);
                    float currentDist = Vector3.Distance(currentWorldPos, center);
                    float deltaDist = currentDist - initialDist;

                    // 마우스 1.5m 당길 때마다 1열씩 16 -> 15 -> 14 -> 13 ... 1열로 극도로 부드럽게 감소
                    int maxLayers = Mathf.Clamp(primarySquad.MemberCount / 4, 1, 16);
                    int targetLayers = Mathf.Clamp(Mathf.RoundToInt(initialFormationLayers - (deltaDist * 0.35f)), 1, maxLayers);

                    int oldLay = primarySquad.formationLayers;
                    primarySquad.formationLayers = targetLayers;

                    if (previewer != null)
                    {
                        previewer.ShowPreview(primarySquad, primarySquad.transform.position, primarySquad.transform.rotation, primarySquad.currentColumns);
                    }

                    primarySquad.formationLayers = oldLay;

                    activeDragLine.positionCount = 2;
                    activeDragLine.SetPositions(new Vector3[] { center + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });
                }
                else if (isWedgeOrDiamond)
                {
                    // 🔺 쐐기/마름모: 마우스 앞뒤(전후) 드래그로 종심 깊이/길이 조절
                    Vector3 dragVec = currentWorldPos - formationStartWorldPos;
                    float longitudinalDot = Vector3.Dot(dragVec, primarySquad.transform.forward);
                    float targetLength = Mathf.Clamp(initialFormationLength + (longitudinalDot * 0.15f), 0.25f, 3.5f);

                    float oldLen = primarySquad.formationLengthMultiplier;
                    primarySquad.formationLengthMultiplier = targetLength;

                    if (previewer != null)
                    {
                        previewer.ShowPreview(primarySquad, primarySquad.transform.position, primarySquad.transform.rotation, primarySquad.currentColumns);
                    }

                    primarySquad.formationLengthMultiplier = oldLen;

                    Vector3 frontPoint = primarySquad.transform.position + (primarySquad.transform.forward * 4.0f * targetLength);
                    Vector3 backPoint = primarySquad.transform.position - (primarySquad.transform.forward * 4.0f * targetLength);
                    activeDragLine.positionCount = 2;
                    activeDragLine.SetPositions(new Vector3[] { backPoint + Vector3.up * 0.1f, frontPoint + Vector3.up * 0.1f });
                }
                else
                {
                    // 🟦 일자진 / 산개진: 기존 초승달 곡률 조절
                    bool isLocked = IsCurrentSelectionLocked();

                    if (selectedSquads.Count == 1)
                    {
                        Vector3 dragVec = currentWorldPos - formationStartWorldPos;
                        float bendAmount = Vector3.Dot(dragVec, primarySquad.transform.forward);
                        currentPreviewCurvature = bendAmount;

                        if (previewer != null)
                        {
                            previewer.ShowCurvedPreview(primarySquad, primarySquad.transform.position, primarySquad.transform.rotation, primarySquad.currentColumns, currentPreviewCurvature);
                        }

                        activeDragLine.positionCount = 2;
                        activeDragLine.SetPositions(new Vector3[] { primarySquad.transform.position + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });
                    }
                    else if (selectedSquads.Count > 1)
                    {
                        Vector3 centroid = GetSquadsCentroid(selectedSquads);
                        Vector3 armyForward = selectedSquads[0].transform.forward;
                        Vector3 dragVec = currentWorldPos - formationStartWorldPos;
                        float bendAmount = Vector3.Dot(dragVec, armyForward);
                        currentPreviewCurvature = bendAmount;

                        if (isLocked)
                        {
                            // 🔒 [전술 그룹화 G 상태]: 부대가 하나가 된 듯이 전체 유닛들이 거대한 1개의 곡선 전선(Grand Army Continuous Arc)으로 굽어짐!
                            var grandArcConfigs = CalculateGrandContinuousArcConfigs(bendAmount);
                            if (previewer != null)
                            {
                                previewer.ShowMultiSquadPreview(grandArcConfigs);
                            }
                        }
                        else
                        {
                            // 🔓 [일반 다중 선택 상태]: 각 부대의 곡률이 각자 제자리에서 동시에 변함!
                            List<(Squad squad, Vector3 destination, Quaternion rotation, int columns, float curve)> individualCurved = new List<(Squad squad, Vector3 destination, Quaternion rotation, int columns, float curve)>();
                            foreach (var s in selectedSquads)
                            {
                                if (s != null)
                                {
                                    individualCurved.Add((s, s.transform.position, s.transform.rotation, s.currentColumns, bendAmount));
                                }
                            }
                            if (previewer != null)
                            {
                                previewer.ShowMultiSquadCurvedPreview(individualCurved);
                            }
                        }

                        activeDragLine.positionCount = 2;
                        activeDragLine.SetPositions(new Vector3[] { centroid + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });
                    }
                }
            }
            return;
        }

        // [Alt + 좌클릭] 드래그 종료 후 적용
        if (Input.GetMouseButtonUp(0) && isAltLeftDragBend)
        {
            isAltLeftDragBend = false;
            activeDragLine.enabled = false;
            if (previewer != null) previewer.HidePreview();

            Squad primarySquad = (selectedSquads.Count > 0) ? selectedSquads[0] : null;
            if (primarySquad == null) return;

            bool isSquareOrCircle = (primarySquad.currentFormationType == SquadFormationType.Square || primarySquad.currentFormationType == SquadFormationType.Circle);
            bool isWedgeOrDiamond = (primarySquad.currentFormationType == SquadFormationType.Wedge || primarySquad.currentFormationType == SquadFormationType.Diamond);

            if (isSquareOrCircle)
            {
                if (GetMouseWorldPosition(out Vector3 endPos))
                {
                    Vector3 center = primarySquad.transform.position;
                    float initialDist = Vector3.Distance(formationStartWorldPos, center);
                    float currentDist = Vector3.Distance(endPos, center);
                    float deltaDist = currentDist - initialDist;

                    int maxLayers = Mathf.Clamp(primarySquad.MemberCount / 4, 1, 16);
                    int targetLayers = Mathf.Clamp(Mathf.RoundToInt(initialFormationLayers - (deltaDist * 0.35f)), 1, maxLayers);

                    foreach (var s in selectedSquads)
                    {
                        if (s != null)
                        {
                            s.formationLayers = targetLayers;
                            s.CommandMoveWithFormation(s.transform.position, s.transform.rotation, s.currentColumns, forceSort: false);
                        }
                    }

                    string formName = (primarySquad.currentFormationType == SquadFormationType.Square) ? "사각방진" : "원형진";
                    Debug.Log($"[PlayerController] 🛡️ {formName} 겹수 및 크기 1열 단위 변경 완료! ({targetLayers}열 방패벽)");
                }
            }
            else if (isWedgeOrDiamond)
            {
                if (GetMouseWorldPosition(out Vector3 endPos))
                {
                    Vector3 dragVec = endPos - formationStartWorldPos;
                    float longitudinalDot = Vector3.Dot(dragVec, primarySquad.transform.forward);
                    float targetLength = Mathf.Clamp(initialFormationLength + (longitudinalDot * 0.15f), 0.25f, 3.5f);

                    foreach (var s in selectedSquads)
                    {
                        if (s != null)
                        {
                            s.formationLengthMultiplier = targetLength;
                            s.CommandMoveWithFormation(s.transform.position, s.transform.rotation, s.currentColumns, forceSort: false);
                        }
                    }

                    string formName = (primarySquad.currentFormationType == SquadFormationType.Wedge) ? "쐐기진" : "마름모";
                    Debug.Log($"[PlayerController] 📐 {formName} 전후 종심 길이 변경 완료! (길이 비율: {targetLength:F2}x)");
                }
            }
            else
            {
                bool isLocked = IsCurrentSelectionLocked();

                if (selectedSquads.Count == 1)
                {
                    selectedSquads[0].CommandCurvedFormation(currentPreviewCurvature);
                    Debug.Log($"[PlayerController] 🏹 부대 곡선 배치 완료! (곡률: {currentPreviewCurvature:F2}m)");
                }
                else if (selectedSquads.Count > 1)
                {
                    if (isLocked)
                    {
                        ApplyGrandContinuousArcFormation(currentPreviewCurvature);
                        Debug.Log($"[PlayerController] 🔒 [G] 군단 일체형 거대 초승달 전선(Grand Army Arc) 배치 완료! (곡률: {currentPreviewCurvature:F2}m)");
                    }
                    else
                    {
                        foreach (var s in selectedSquads)
                        {
                            if (s != null) s.CommandCurvedFormation(currentPreviewCurvature);
                        }
                        Debug.Log($"[PlayerController] 🏹 선택된 {selectedSquads.Count}개 부대 개별 곡률 동시 변경 완료! (곡률: {currentPreviewCurvature:F2}m)");
                    }
                }
            }
            return;
        }

        // [Ctrl + 좌클릭] 드래그 이동 시작
        if (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (selectedSquads.Count > 0 && GetMouseWorldPosition(out Vector3 hitPoint))
            {
                formationStartWorldPos = hitPoint;
                isCtrlLeftDragMove = true;
                activeDragLine.enabled = true;
            }
            return;
        }

        // [Ctrl + 좌클릭] 드래그 중 프리뷰 표시 (단일 및 다중 부대 평행이동)
        if (Input.GetMouseButton(0) && isCtrlLeftDragMove)
        {
            if (GetMouseWorldPosition(out Vector3 currentWorldPos))
            {
                Vector3 moveVector = currentWorldPos - formationStartWorldPos;

                if (selectedSquads.Count == 1)
                {
                    Squad primarySquad = selectedSquads[0];
                    Vector3 targetCenter = primarySquad.transform.position + moveVector;
                    Quaternion targetRot = primarySquad.transform.rotation;

                    if (previewer != null)
                    {
                        previewer.ShowPreview(primarySquad, targetCenter, targetRot, primarySquad.currentColumns);
                    }
                }
                else if (selectedSquads.Count > 1)
                {
                    List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> multiConfigs = new List<(Squad, Vector3, Quaternion, int)>();
                    foreach (Squad s in selectedSquads)
                    {
                        if (s != null)
                        {
                            multiConfigs.Add((s, s.transform.position + moveVector, s.transform.rotation, s.currentColumns));
                        }
                    }
                    if (previewer != null)
                    {
                        previewer.ShowMultiSquadPreview(multiConfigs);
                    }
                }

                activeDragLine.positionCount = 2;
                activeDragLine.SetPositions(new Vector3[] { formationStartWorldPos + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });
            }
            return;
        }

        // [Ctrl + 좌클릭] 드래그 종료 후 평행 이동 명령 실행
        if (Input.GetMouseButtonUp(0) && isCtrlLeftDragMove)
        {
            isCtrlLeftDragMove = false;
            activeDragLine.enabled = false;
            if (previewer != null) previewer.HidePreview();

            if (GetMouseWorldPosition(out Vector3 endWorldPos))
            {
                if (Vector3.Distance(formationStartWorldPos, endWorldPos) > 0.3f)
                {
                    Vector3 moveVector = endWorldPos - formationStartWorldPos;

                    if (selectedSquads.Count == 1)
                    {
                        Squad primarySquad = selectedSquads[0];
                        Vector3 targetCenter = primarySquad.transform.position + moveVector;
                        ExecuteSquadCommand(targetCenter, primarySquad.transform.rotation, primarySquad.currentColumns, false);
                    }
                    else if (selectedSquads.Count > 1)
                    {
                        foreach (Squad s in selectedSquads)
                        {
                            if (s != null)
                            {
                                Vector3 targetPos = s.transform.position + moveVector;
                                s.ClearWaypoints();
                                s.CommandMoveWithFormation(targetPos, s.transform.rotation, s.currentColumns, forceSort: false);
                            }
                        }
                        Debug.Log($"[PlayerController] 🛡️ [Ctrl+좌클릭] 군단 평행 이동 명령 완료! ({selectedSquads.Count}개 부대 대형 유지 이동)");
                    }
                }
            }
            return;
        }

        // 일반 드래그 선택 처리
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            dragStartPosition = Input.mousePosition;
            isSelecting = true;
        }

        if (Input.GetMouseButtonUp(0) && isSelecting)
        {
            isSelecting = false;

            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                DeselectAll();
            }

            Vector2 dragEndPosition = Input.mousePosition;
            if (Vector2.Distance(dragStartPosition, dragEndPosition) < 10f)
            {
                SelectSingleUnitOrSquad();
            }
            else
            {
                SelectUnitsInScreenRect(dragStartPosition, dragEndPosition);
            }
        }
    }

    #endregion

    #region Command Input

    private void HandleCommandInput()
    {
        if (selectedSquads.Count == 0 && selectedUnits.Count == 0 && selectedECSEntities.Count == 0) return;

        // ESC 키로 우클릭 드래그, 회전 및 Alt 곡선 행/열 조절 취소
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isDraggingFormation || isCtrlRightDragRotate || isAltRightDragResize)
            {
                isDraggingFormation = false;
                isCtrlRightDragRotate = false;
                isAltRightDragResize = false;
                if (activeDragLine != null) activeDragLine.enabled = false;
                if (previewer != null) previewer.HidePreview();
                freeDrawPath.Clear();
                Debug.Log("[PlayerController] ❌ [ESC] 우클릭 드래그 배치가 취소되었습니다.");
                return;
            }
        }

        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool isAltPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        // 1. 마우스 우클릭 시작
        if (Input.GetMouseButtonDown(1))
        {
            // 🎯 마우스 아래에 적군 유닛/부대가 있는지 정밀 검사 (UI 아이콘, 방진 사각 영역, 물리 콜라이더, 순수 ECS 픽셀 거리)
            if (TryGetEnemyUnderMouse(out Squad clickedEnemySquad, out Vector3 enemyWorldPos))
            {
                if (clickedEnemySquad != null)
                {
                    CommandAttackTargetSquad(clickedEnemySquad);
                }
                else
                {
                    CommandAttackTargetPosition(enemyWorldPos);
                }
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (GetMouseWorldPosition(out Vector3 hitPoint))
            {
                formationStartWorldPos = hitPoint;
                freeDrawPath.Clear();
                freeDrawPath.Add(hitPoint);

                if (selectedSquads.Count > 0)
                {
                    if (isCtrlPressed)
                    {
                        isCtrlRightDragRotate = true;
                    }
                    else if (isAltPressed)
                    {
                        Squad primarySquad = selectedSquads[0];
                        initialFormationWidth = primarySquad.formationWidthMultiplier;
                        initialFormationLayers = primarySquad.formationLayers;
                        initialFormationLayersSpacing = primarySquad.formationLayersSpacingMultiplier;
                        isAltRightDragResize = true;
                    }
                    else
                    {
                        isDraggingFormation = true;
                    }
                }
                else if (selectedUnits.Count > 0 || selectedECSEntities.Count > 0)
                {
                    isDraggingFormation = true;
                }

                activeDragLine.enabled = true;
            }
        }

        // 2. 마우스 우클릭 드래그 중
        if (Input.GetMouseButton(1) && (isDraggingFormation || isCtrlRightDragRotate || isAltRightDragResize))
        {
            if (GetMouseWorldPosition(out Vector3 currentWorldPos))
            {
                Squad primarySquad = (selectedSquads.Count > 0) ? selectedSquads[0] : null;

                if (selectedSquads.Count > 0)
                {
                    if (isCtrlRightDragRotate && primarySquad != null)
                    {
                        if (selectedSquads.Count == 1)
                        {
                            Vector3 dir = currentWorldPos - primarySquad.transform.position;
                            dir.y = 0f;
                            Quaternion targetRot = (dir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(dir) : primarySquad.transform.rotation;

                            if (previewer != null) previewer.ShowPreview(primarySquad, primarySquad.transform.position, targetRot, primarySquad.currentColumns);

                            activeDragLine.positionCount = 2;
                            activeDragLine.SetPositions(new Vector3[] { primarySquad.transform.position + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });
                        }
                        else
                        {
                            var rotateConfigs = CalculateMultiSquadRotateConfigs(currentWorldPos);
                            if (previewer != null) previewer.ShowMultiSquadPreview(rotateConfigs);

                            Vector3 centroid = GetSquadsCentroid(selectedSquads);
                            activeDragLine.positionCount = 2;
                            activeDragLine.SetPositions(new Vector3[] { centroid + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });
                        }
                    }
                    // [Alt + 우클릭] 부대 중심(Center)을 유지한 상태로 좌우 대칭 크기/날카로움/겹수 조절 프리뷰
                    else if (isAltRightDragResize && primarySquad != null)
                    {
                        Vector3 centerPos = primarySquad.transform.position;
                        Quaternion targetRot = primarySquad.transform.rotation;
                        Vector3 delta = currentWorldPos - formationStartWorldPos;
                        float lateralDot = Vector3.Dot(delta, targetRot * Vector3.right);

                        bool isSquareOrCircle = (primarySquad.currentFormationType == SquadFormationType.Square || primarySquad.currentFormationType == SquadFormationType.Circle);
                        bool isWedgeOrDiamond = (primarySquad.currentFormationType == SquadFormationType.Wedge || primarySquad.currentFormationType == SquadFormationType.Diamond);

                        if (isSquareOrCircle)
                        {
                            // 🛡️ 사각방진 / 원형진: 내부 공간은 그대로 두고, 열(Layers)끼리의 거리를 벌림 (층간 거리 조절)
                            float targetSpacing = Mathf.Clamp(initialFormationLayersSpacing + (lateralDot * 0.15f), 0.3f, 4.0f);
                            float oldSpacing = primarySquad.formationLayersSpacingMultiplier;
                            primarySquad.formationLayersSpacingMultiplier = targetSpacing;

                            if (previewer != null)
                            {
                                previewer.ShowPreview(primarySquad, centerPos, targetRot, primarySquad.currentColumns);
                            }

                            primarySquad.formationLayersSpacingMultiplier = oldSpacing;

                            float previewWidth = Mathf.Max(1.0f, 4.0f * targetSpacing);
                            Vector3 leftEdge = centerPos - (targetRot * Vector3.right * previewWidth * 0.5f);
                            Vector3 rightEdge = centerPos + (targetRot * Vector3.right * previewWidth * 0.5f);

                            activeDragLine.positionCount = 2;
                            activeDragLine.SetPositions(new Vector3[] { leftEdge + Vector3.up * 0.1f, rightEdge + Vector3.up * 0.1f });
                        }
                        else if (isWedgeOrDiamond)
                        {
                            // 쐐기/마름모: 좌우 드래그로 횡방향 산개/밀집(날카로움) 조절
                            float targetWidth = Mathf.Clamp(initialFormationWidth + (lateralDot * 0.15f), 0.15f, 3.0f);
                            float oldWid = primarySquad.formationWidthMultiplier;
                            primarySquad.formationWidthMultiplier = targetWidth;

                            if (previewer != null)
                            {
                                previewer.ShowPreview(primarySquad, centerPos, targetRot, primarySquad.currentColumns);
                            }

                            primarySquad.formationWidthMultiplier = oldWid;

                            float previewWidth = Mathf.Max(1.0f, 6.0f * targetWidth);
                            Vector3 leftEdge = centerPos - (targetRot * Vector3.right * previewWidth * 0.5f);
                            Vector3 rightEdge = centerPos + (targetRot * Vector3.right * previewWidth * 0.5f);

                            activeDragLine.positionCount = 2;
                            activeDragLine.SetPositions(new Vector3[] { leftEdge + Vector3.up * 0.1f, rightEdge + Vector3.up * 0.1f });
                        }
                        else
                        {
                            // 일자진 / 산개진: 열 수(Columns) 조절
                            float currentSpacing = (primarySquad.currentFormationType == SquadFormationType.Loose || primarySquad.isLooseFormation) ? (primarySquad.spacing * 2.0f) : primarySquad.spacing;
                            float initialSquadWidth = (primarySquad.currentColumns - 1) * currentSpacing;

                            float targetWidth = Mathf.Max(currentSpacing, initialSquadWidth + (lateralDot * 2.0f));
                            int cols = Mathf.Clamp(Mathf.RoundToInt(targetWidth / currentSpacing) + 1, 2, primarySquad.MemberCount);

                            if (previewer != null)
                            {
                                previewer.ShowCurvedPreview(primarySquad, centerPos, targetRot, cols, primarySquad.customCurvature);
                            }

                            float halfWidth = (cols - 1) * currentSpacing * 0.5f;
                            Vector3 leftEdge = centerPos - (targetRot * Vector3.right * halfWidth);
                            Vector3 rightEdge = centerPos + (targetRot * Vector3.right * halfWidth);

                            activeDragLine.positionCount = 2;
                            activeDragLine.SetPositions(new Vector3[] { leftEdge + Vector3.up * 0.1f, rightEdge + Vector3.up * 0.1f });
                        }
                    }
                    else if (isDraggingFormation)
                    {
                        if (isShiftPressed)
                        {
                            if (freeDrawPath.Count > 0 && Vector3.Distance(freeDrawPath[freeDrawPath.Count - 1], currentWorldPos) > 0.2f)
                            {
                                freeDrawPath.Add(currentWorldPos);
                            }

                            activeDragLine.positionCount = freeDrawPath.Count;
                            for (int i = 0; i < freeDrawPath.Count; i++)
                            {
                                activeDragLine.SetPosition(i, freeDrawPath[i] + Vector3.up * 0.1f);
                            }
                        }
                        else
                        {
                            if (selectedSquads.Count == 1)
                            {
                                CalculateFormationParams(formationStartWorldPos, currentWorldPos, out Vector3 centerPos, out Quaternion targetRot, out int cols);
                                if (previewer != null && primarySquad != null) previewer.ShowPreview(primarySquad, centerPos, targetRot, cols);
                            }
                            else if (selectedSquads.Count > 1)
                            {
                                float dragDist = Vector3.Distance(formationStartWorldPos, currentWorldPos);
                                if (dragDist >= 1.0f)
                                {
                                    var lineConfigs = CalculateMultiSquadLineDeployment(formationStartWorldPos, currentWorldPos);
                                    if (previewer != null) previewer.ShowMultiSquadPreview(lineConfigs);
                                }
                                else
                                {
                                    // 1m 미만이면 상대적 오프셋 유지 이동 프리뷰
                                    Vector3 centroid = GetSquadsCentroid(selectedSquads);
                                    Vector3 moveVec = currentWorldPos - centroid;
                                    List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> moveConfigs = new List<(Squad, Vector3, Quaternion, int)>();
                                    foreach (var s in selectedSquads)
                                    {
                                        if (s != null) moveConfigs.Add((s, s.transform.position + moveVec, s.transform.rotation, s.currentColumns));
                                    }
                                    if (previewer != null) previewer.ShowMultiSquadPreview(moveConfigs);
                                }
                            }

                            activeDragLine.positionCount = 2;
                            activeDragLine.SetPositions(new Vector3[] { formationStartWorldPos + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });
                        }
                    }
                }
                else if ((selectedUnits.Count > 0 || selectedECSEntities.Count > 0) && isDraggingFormation)
                {
                    int freeCount = (selectedUnits.Count > 0) ? selectedUnits.Count : selectedECSEntities.Count;

                    if (isShiftPressed)
                    {
                        activeDragLine.positionCount = 2;
                        activeDragLine.SetPositions(new Vector3[] { formationStartWorldPos + Vector3.up * 0.1f, currentWorldPos + Vector3.up * 0.1f });

                        if (previewer != null)
                        {
                            var (straightSlots, straightRots) = CalculateStraightSlotPositions(formationStartWorldPos, currentWorldPos, freeCount);
                            previewer.ShowFreeUnitPreview(straightSlots, straightRots);
                        }
                    }
                    else
                    {
                        if (freeDrawPath.Count > 0 && Vector3.Distance(freeDrawPath[freeDrawPath.Count - 1], currentWorldPos) > 0.2f)
                        {
                            freeDrawPath.Add(currentWorldPos);
                        }

                        activeDragLine.positionCount = freeDrawPath.Count;
                        for (int i = 0; i < freeDrawPath.Count; i++)
                        {
                            activeDragLine.SetPosition(i, freeDrawPath[i] + Vector3.up * 0.1f);
                        }

                        if (previewer != null)
                        {
                            var (curveSlots, curveRots) = CalculateCurveSlotPositions(freeDrawPath, freeCount);
                            previewer.ShowFreeUnitPreview(curveSlots, curveRots);
                        }
                    }
                }
            }
        }

        // 3. 마우스 우클릭 종료
        if (Input.GetMouseButtonUp(1) && (isDraggingFormation || isCtrlRightDragRotate || isAltRightDragResize))
        {
            bool wasDraggingFormation = isDraggingFormation;
            bool wasCtrlRightDragRotate = isCtrlRightDragRotate;
            bool wasAltRightDragResize = isAltRightDragResize;

            isDraggingFormation = false;
            isCtrlRightDragRotate = false;
            isAltRightDragResize = false;

            activeDragLine.enabled = false;
            if (previewer != null) previewer.HidePreview();

            if (GetMouseWorldPosition(out Vector3 endWorldPos))
            {
                Squad primarySquad = (selectedSquads.Count > 0) ? selectedSquads[0] : null;

                if (selectedSquads.Count > 0)
                {
                    if (wasCtrlRightDragRotate && primarySquad != null)
                    {
                        if (selectedSquads.Count == 1)
                        {
                            Vector3 dir = endWorldPos - primarySquad.transform.position;
                            dir.y = 0f;
                            Quaternion targetRot = (dir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(dir) : primarySquad.transform.rotation;
                            primarySquad.CommandRotateInPlace(targetRot);
                        }
                        else if (selectedSquads.Count > 1)
                        {
                            var rotateConfigs = CalculateMultiSquadRotateConfigs(endWorldPos);
                            foreach (var c in rotateConfigs)
                            {
                                if (c.squad != null)
                                {
                                    c.squad.ClearWaypoints();
                                    c.squad.CommandMoveWithFormation(c.destination, c.rotation, c.columns, forceSort: false);
                                }
                            }
                            Debug.Log($"[PlayerController] 🔄 [Ctrl+우클릭] 군단 중심 회전 완료! ({selectedSquads.Count}개 부대 대형 유지 회전)");
                        }
                    }
                    else if (wasAltRightDragResize && primarySquad != null)
                    {
                        Vector3 delta = endWorldPos - formationStartWorldPos;
                        float lateralDot = Vector3.Dot(delta, primarySquad.transform.rotation * Vector3.right);
                        bool isSquareOrCircle = (primarySquad.currentFormationType == SquadFormationType.Square || primarySquad.currentFormationType == SquadFormationType.Circle);
                        bool isWedgeOrDiamond = (primarySquad.currentFormationType == SquadFormationType.Wedge || primarySquad.currentFormationType == SquadFormationType.Diamond);

                        if (isSquareOrCircle)
                        {
                            float targetSpacing = Mathf.Clamp(initialFormationLayersSpacing + (lateralDot * 0.15f), 0.3f, 4.0f);
                            foreach (Squad s in selectedSquads)
                            {
                                if (s != null)
                                {
                                    s.formationLayersSpacingMultiplier = targetSpacing;
                                    s.CommandMoveWithFormation(s.transform.position, s.transform.rotation, s.currentColumns, forceSort: false);
                                }
                            }
                            string formName = (primarySquad.currentFormationType == SquadFormationType.Square) ? "사각방진" : "원형진";
                            Debug.Log($"[PlayerController] 🛡️ {formName} 층간(열끼리) 거리 변경 완료! (간격 배율: {targetSpacing:F2}x)");
                        }
                        else if (isWedgeOrDiamond)
                        {
                            float targetWidth = Mathf.Clamp(initialFormationWidth + (lateralDot * 0.15f), 0.15f, 3.0f);
                            foreach (Squad s in selectedSquads)
                            {
                                if (s != null)
                                {
                                    s.formationWidthMultiplier = targetWidth;
                                    s.CommandMoveWithFormation(s.transform.position, s.transform.rotation, s.currentColumns, forceSort: false);
                                }
                            }
                            string formName = (primarySquad.currentFormationType == SquadFormationType.Wedge) ? "쐐기진" : "마름모";
                            Debug.Log($"[PlayerController] 📐 {formName} 횡방향 밀집도/날카로움 변경 완료! (가로폭: {targetWidth:F2}x, 세로길이: {primarySquad.formationLengthMultiplier:F2}x)");
                        }
                        else
                        {
                            float currentSpacing = (primarySquad.currentFormationType == SquadFormationType.Loose || primarySquad.isLooseFormation) ? (primarySquad.spacing * 2.0f) : primarySquad.spacing;
                            float initialSquadWidth = (primarySquad.currentColumns - 1) * currentSpacing;

                            float targetWidth = Mathf.Max(currentSpacing, initialSquadWidth + (lateralDot * 2.0f));
                            int targetCols = Mathf.Clamp(Mathf.RoundToInt(targetWidth / currentSpacing) + 1, 2, primarySquad.MemberCount);

                            foreach (Squad s in selectedSquads)
                            {
                                if (s != null)
                                {
                                    s.CommandMoveWithFormation(s.transform.position, s.transform.rotation, targetCols, forceSort: false);
                                }
                            }
                            Debug.Log($"[PlayerController] 🏹 부대 중심 유지 제자리 행/열 크기 변경 완료! (열: {targetCols}, 곡률: {primarySquad.customCurvature:F2}m)");
                        }
                    }
                    else if (wasDraggingFormation)
                    {
                        float dragDist = Vector3.Distance(formationStartWorldPos, endWorldPos);

                        if (selectedSquads.Count == 1)
                        {
                            if (dragDist < 0.5f)
                            {
                                CalculateFormationParams(formationStartWorldPos, endWorldPos, out Vector3 centerPos, out Quaternion targetRot, out int cols);
                                ExecuteSquadCommand(centerPos, targetRot, cols, isShiftPressed);
                            }
                            else if (isShiftPressed)
                            {
                                ExecuteSquadCurveWaypoints(freeDrawPath);
                            }
                            else
                            {
                                CalculateFormationParams(formationStartWorldPos, endWorldPos, out Vector3 centerPos, out Quaternion targetRot, out int cols);
                                ExecuteSquadCommand(centerPos, targetRot, cols, false);
                            }
                        }
                        else if (selectedSquads.Count > 1)
                        {
                            if (dragDist >= 1.0f && !isShiftPressed)
                            {
                                var lineConfigs = CalculateMultiSquadLineDeployment(formationStartWorldPos, endWorldPos);
                                foreach (var c in lineConfigs)
                                {
                                    if (c.squad != null)
                                    {
                                        c.squad.ClearWaypoints();
                                        c.squad.CommandMoveWithFormation(c.destination, c.rotation, c.columns, forceSort: false);
                                    }
                                }
                                Debug.Log($"[PlayerController] ⚔️ 다중 부대 횡대 전열 분할 배치 완료! ({lineConfigs.Count}개 부대 일렬 배치)");
                            }
                            else
                            {
                                // 단순 우클릭 및 Shift+우클릭: 상대적 오프셋 유지 이동
                                ExecuteSquadCommand(endWorldPos, Quaternion.identity, 0, isShiftPressed);
                            }
                        }
                    }
                }
                else if (selectedUnits.Count > 0)
                {
                    float dragDist = Vector3.Distance(formationStartWorldPos, endWorldPos);

                    if (dragDist < 0.5f)
                    {
                        ExecuteFreeUnitsRelativeMove(endWorldPos, isShiftPressed);
                    }
                    else if (isShiftPressed)
                    {
                        var (straightSlots, straightRots) = CalculateStraightSlotPositions(formationStartWorldPos, endWorldPos, selectedUnits.Count);
                        AssignUnitsToSlotsClosest(straightSlots, straightRots, isShiftPressed);
                    }
                    else
                    {
                        var (curveSlots, curveRots) = CalculateCurveSlotPositions(freeDrawPath, selectedUnits.Count);
                        AssignUnitsToSlotsClosest(curveSlots, curveRots, isShiftPressed);
                    }
                }
                else if (selectedECSEntities.Count > 0)
                {
                    float dragDist = Vector3.Distance(formationStartWorldPos, endWorldPos);
                    List<Vector3> slots;
                    List<Quaternion> rots;

                    if (dragDist < 0.5f)
                    {
                        var (clSlots, clRots) = CalculateClusterSlotPositions(endWorldPos, Quaternion.identity, selectedECSEntities.Count);
                        slots = clSlots;
                        rots = clRots;
                    }
                    else
                    {
                        var (stSlots, stRots) = CalculateStraightSlotPositions(formationStartWorldPos, endWorldPos, selectedECSEntities.Count);
                        slots = stSlots;
                        rots = stRots;
                    }

                    var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                    if (world != null && world.IsCreated)
                    {
                        var em = world.EntityManager;
                        for (int i = 0; i < selectedECSEntities.Count; i++)
                        {
                            var ent = selectedECSEntities[i];
                            if (em.Exists(ent))
                            {
                                var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                                var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                                Vector3 targetSlot = (i < slots.Count) ? slots[i] : endWorldPos;
                                Quaternion targetRot = (i < rots.Count) ? rots[i] : Quaternion.identity;

                                mov.TargetPosition = targetSlot;
                                mov.TargetRotation = targetRot;
                                float spd = (mov.MoveSpeed >= 2.0f) ? 2.8f : 1.2f;
                                mov.MoveSpeed = spd;
                                mov.Acceleration = (spd >= 2.0f) ? 8.0f : 4.5f;
                                combat.CurrentState = (int)UnitCommandState.Move;

                                em.SetComponentData(ent, mov);
                                em.SetComponentData(ent, combat);
                            }
                        }
                    }
                    Debug.Log($"[PlayerController] ⚡ 자유 ECS 유닛 {selectedECSEntities.Count}명에게 이동 명령 하달 완료!");
                }
            }

            isDraggingFormation = false;
            isCtrlRightDragRotate = false;
            freeDrawPath.Clear();
        }
    }

    #endregion

    #region Helper Calculations & Movement Execution

    private void AssignUnitsToSlotsClosest(List<Vector3> slots, List<Quaternion> rotations, bool isShift)
    {
        if (slots == null || slots.Count == 0 || selectedUnits.Count == 0) return;

        List<Unit> availableUnits = new List<Unit>();
        foreach (var u in selectedUnits) if (u != null) availableUnits.Add(u);

        List<Vector3> remainingSlots = new List<Vector3>(slots);
        List<Quaternion> remainingRotations = new List<Quaternion>(rotations);

        while (availableUnits.Count > 0 && remainingSlots.Count > 0)
        {
            int bestUnitIdx = -1;
            int bestSlotIdx = -1;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < availableUnits.Count; i++)
            {
                for (int j = 0; j < remainingSlots.Count; j++)
                {
                    float sqrDist = (availableUnits[i].transform.position - remainingSlots[j]).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        bestUnitIdx = i;
                        bestSlotIdx = j;
                    }
                }
            }

            if (bestUnitIdx != -1 && bestSlotIdx != -1)
            {
                Unit targetUnit = availableUnits[bestUnitIdx];
                Vector3 targetPos = remainingSlots[bestSlotIdx];
                Quaternion targetRot = remainingRotations[bestSlotIdx];

                SendUnitMoveCommand(targetUnit, targetPos, targetRot, isShift);

                availableUnits.RemoveAt(bestUnitIdx);
                remainingSlots.RemoveAt(bestSlotIdx);
                remainingRotations.RemoveAt(bestSlotIdx);
            }
            else break;
        }
    }

    private void ExecuteFreeUnitsRelativeMove(Vector3 targetCenterPos, bool isShift)
    {
        Vector3 currentCenter = Vector3.zero;
        int count = 0;
        foreach (Unit u in selectedUnits)
        {
            if (u != null) { currentCenter += u.transform.position; count++; }
        }
        if (count == 0) return;
        currentCenter /= count;

        foreach (Unit u in selectedUnits)
        {
            if (u == null) continue;
            Vector3 offset = u.transform.position - currentCenter;
            SendUnitMoveCommand(u, targetCenterPos + offset, u.transform.rotation, isShift);
        }
    }

    private void SendUnitMoveCommand(Unit unit, Vector3 destination, Quaternion rotation, bool isShift)
    {
        if (unit == null) return;

        if (isShift)
        {
            unit.MoveTo(destination);
        }
        else
        {
            unit.MoveTo(destination);
        }
    }

    private (List<Vector3> slots, List<Quaternion> rots) CalculateStraightSlotPositions(Vector3 start, Vector3 end, int unitCount)
    {
        List<Vector3> slots = new List<Vector3>();
        List<Quaternion> rots = new List<Quaternion>();

        if (unitCount <= 0) return (slots, rots);

        Vector3 dragVec = end - start;
        dragVec.y = 0f;
        float dragDist = dragVec.magnitude;
        Vector3 facing = Vector3.Cross(dragVec.normalized, Vector3.up);
        Quaternion targetRot = (facing.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(facing) : Quaternion.identity;

        // 너무 좁은 일자진(슬롯 간격 < 0.5m)인 경우 억지 배치를 포기하고 중심 클러스터(대강 서기)로 전환
        const float minSlotSpacing = 0.5f;
        if (unitCount > 1 && (dragDist / (unitCount - 1)) < minSlotSpacing)
        {
            return CalculateClusterSlotPositions((start + end) * 0.5f, targetRot, unitCount);
        }

        for (int i = 0; i < unitCount; i++)
        {
            float t = (unitCount == 1) ? 0.5f : (float)i / (unitCount - 1);
            slots.Add(Vector3.Lerp(start, end, t));
            rots.Add(targetRot);
        }

        return (slots, rots);
    }

    private (List<Vector3> slots, List<Quaternion> rots) CalculateCurveSlotPositions(List<Vector3> path, int unitCount)
    {
        List<Vector3> slots = new List<Vector3>();
        List<Quaternion> rots = new List<Quaternion>();

        if (path == null || path.Count == 0 || unitCount <= 0) return (slots, rots);

        if (unitCount == 1 || path.Count == 1)
        {
            slots.Add(path[0]);
            rots.Add(Quaternion.identity);
            return (slots, rots);
        }

        float totalLength = 0f;
        List<float> segmentLengths = new List<float>();
        for (int i = 0; i < path.Count - 1; i++)
        {
            float seg = Vector3.Distance(path[i], path[i + 1]);
            segmentLengths.Add(seg);
            totalLength += seg;
        }

        float interval = totalLength / Mathf.Max(1, unitCount - 1);

        // 너무 좁은 곡선진(슬롯 간격 < 0.5m)인 경우 중심 클러스터(대강 서기)로 전환
        const float minSlotSpacing = 0.5f;
        if (unitCount > 1 && interval < minSlotSpacing)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in path) center += p;
            center /= path.Count;
            return CalculateClusterSlotPositions(center, Quaternion.identity, unitCount);
        }

        slots.Add(path[0]);
        Vector3 firstDir = (path.Count > 1) ? (path[1] - path[0]).normalized : Vector3.forward;
        rots.Add(Quaternion.LookRotation(firstDir));

        float currentTargetDist = interval;
        float accumulatedDist = 0f;
        int currentSegIdx = 0;

        for (int u = 1; u < unitCount - 1; u++)
        {
            while (currentSegIdx < segmentLengths.Count && accumulatedDist + segmentLengths[currentSegIdx] < currentTargetDist)
            {
                accumulatedDist += segmentLengths[currentSegIdx];
                currentSegIdx++;
            }

            if (currentSegIdx >= segmentLengths.Count)
            {
                slots.Add(path[path.Count - 1]);
                rots.Add(rots[rots.Count - 1]);
                continue;
            }

            float segProgress = (segmentLengths[currentSegIdx] > 0f)
                ? (currentTargetDist - accumulatedDist) / segmentLengths[currentSegIdx]
                : 0f;

            Vector3 slotPos = Vector3.Lerp(path[currentSegIdx], path[currentSegIdx + 1], segProgress);
            Vector3 segDir = (path[currentSegIdx + 1] - path[currentSegIdx]).normalized;

            slots.Add(slotPos);
            rots.Add((segDir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(segDir) : Quaternion.identity);

            currentTargetDist += interval;
        }

        slots.Add(path[path.Count - 1]);
        Vector3 lastDir = (path.Count >= 2) ? (path[path.Count - 1] - path[path.Count - 2]).normalized : Vector3.forward;
        rots.Add((lastDir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(lastDir) : Quaternion.identity);

        return (slots, rots);
    }

    private (List<Vector3> slots, List<Quaternion> rots) CalculateClusterSlotPositions(Vector3 center, Quaternion facingRot, int unitCount)
    {
        List<Vector3> slots = new List<Vector3>();
        List<Quaternion> rots = new List<Quaternion>();

        if (unitCount <= 0) return (slots, rots);

        // 첫 번째 유닛은 중심점 배치
        slots.Add(center);
        rots.Add(facingRot);

        // 나머지 유닛들은 피보나치 나선(황금각)으로 서로 겹치지 않는 자연스러운 군중 산개 슬롯(안전 간격 0.65m) 생성
        const float goldenAngle = 137.507764f * Mathf.Deg2Rad;
        const float spreadFactor = 0.65f;

        for (int i = 1; i < unitCount; i++)
        {
            float r = spreadFactor * Mathf.Sqrt(i);
            float theta = i * goldenAngle;

            Vector3 offset = new Vector3(Mathf.Cos(theta) * r, 0f, Mathf.Sin(theta) * r);
            Vector3 slotPos = center + offset;

            // 네비메시 유효성 샘플링 방어
            if (UnityEngine.AI.NavMesh.SamplePosition(slotPos, out UnityEngine.AI.NavMeshHit hit, 1.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                slotPos = hit.position;
            }

            slots.Add(slotPos);
            rots.Add(facingRot);
        }

        return (slots, rots);
    }

    private void ExecuteSquadCurveWaypoints(List<Vector3> path)
    {
        if (path == null || path.Count < 2) return;
        
        List<Vector3> sampledWaypoints = new List<Vector3>();
        float sampleDist = 1.0f;
        Vector3 lastAdded = path[0];
        
        for (int i = 1; i < path.Count; i++)
        {
            if (Vector3.Distance(lastAdded, path[i]) >= sampleDist)
            {
                sampledWaypoints.Add(path[i]);
                lastAdded = path[i];
            }
        }
        
        if (sampledWaypoints.Count == 0 || Vector3.Distance(lastAdded, path[path.Count - 1]) > 0.2f)
        {
            sampledWaypoints.Add(path[path.Count - 1]);
        }
        
        foreach (Squad squad in selectedSquads)
        {
            if (squad == null) continue;
            
            for (int i = 0; i < sampledWaypoints.Count; i++)
            {
                Vector3 wp = sampledWaypoints[i];
                Vector3 dir = (i < sampledWaypoints.Count - 1) ? (sampledWaypoints[i+1] - wp).normalized : Vector3.forward;
                if (i == sampledWaypoints.Count - 1 && sampledWaypoints.Count > 1) 
                {
                    dir = (wp - sampledWaypoints[i-1]).normalized;
                }
                Quaternion rot = (dir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(dir) : squad.transform.rotation;
                
                squad.AddWaypoint(wp, rot, squad.currentColumns);
            }
        }
    }

    public Vector3 GetSquadsCentroid(List<Squad> squads)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var s in squads)
        {
            if (s != null) { sum += s.transform.position; count++; }
        }
        return count > 0 ? (sum / count) : Vector3.zero;
    }

    private List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> CalculateMultiSquadLineDeployment(Vector3 start, Vector3 end)
    {
        List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> results = new List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)>();
        if (selectedSquads.Count == 0) return results;

        Vector3 dragVec = end - start;
        dragVec.y = 0f;
        float totalDragDist = dragVec.magnitude;
        if (totalDragDist < 0.1f) return results;

        Vector3 dragDir = dragVec.normalized;
        Vector3 facing = Vector3.Cross(dragDir, Vector3.up);
        Quaternion targetRot = (facing.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(facing) : Quaternion.identity;

        List<Squad> sortedSquads = new List<Squad>(selectedSquads);
        sortedSquads.RemoveAll(s => s == null);
        sortedSquads.Sort((a, b) =>
        {
            float projA = Vector3.Dot(a.transform.position, dragDir);
            float projB = Vector3.Dot(b.transform.position, dragDir);
            return projA.CompareTo(projB);
        });

        int squadCount = sortedSquads.Count;

        // 부대 간 간격 = 산개 정도와 동일 (밀집 시 1.1m, 산개 시 2.2m)
        float avgSpacing = 1.1f;
        foreach (var s in sortedSquads)
        {
            float sp = (s.currentFormationType == SquadFormationType.Loose || s.isLooseFormation) ? (s.spacing * 2.0f) : s.spacing;
            avgSpacing = sp;
            break;
        }

        float squadGap = avgSpacing; // 부대 사이 여유 공간을 유닛 1칸 간격으로 밀착!
        float totalGaps = Mathf.Max(0, squadCount - 1) * squadGap;
        float availableWidth = Mathf.Max(squadCount * avgSpacing * 2f, totalDragDist - totalGaps);
        float widthPerSquad = availableWidth / squadCount;

        float currentDist = 0f;
        for (int i = 0; i < squadCount; i++)
        {
            Squad s = sortedSquads[i];
            float spX = (s.currentFormationType == SquadFormationType.Loose || s.isLooseFormation) ? (s.spacing * 2.0f) : s.spacing;
            int cols = Mathf.Clamp(Mathf.RoundToInt(widthPerSquad / Mathf.Max(0.4f, spX)) + 1, 2, s.MemberCount);
            float actualSquadWidth = (cols - 1) * spX;

            float centerDistOnLine = currentDist + (actualSquadWidth * 0.5f);
            Vector3 targetPos = start + (dragDir * centerDistOnLine);

            results.Add((s, targetPos, targetRot, cols));
            currentDist += actualSquadWidth + squadGap;
        }

        return results;
    }

    private List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> CalculateMultiSquadRotateConfigs(Vector3 mouseWorldPos)
    {
        List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> results = new List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)>();
        if (selectedSquads.Count == 0) return results;

        Vector3 centroid = GetSquadsCentroid(selectedSquads);
        Vector3 currentDir = mouseWorldPos - centroid;
        currentDir.y = 0f;
        Quaternion targetRot = (currentDir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(currentDir) : Quaternion.identity;

        Vector3 initialDir = formationStartWorldPos - centroid;
        initialDir.y = 0f;
        Quaternion initialRot = (initialDir.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(initialDir) : Quaternion.identity;
        Quaternion deltaRot = targetRot * Quaternion.Inverse(initialRot);

        foreach (Squad s in selectedSquads)
        {
            if (s != null)
            {
                Vector3 relPos = s.transform.position - centroid;
                Vector3 rotatedPos = centroid + (deltaRot * relPos);
                Quaternion rotatedRot = deltaRot * s.transform.rotation;
                results.Add((s, rotatedPos, rotatedRot, s.currentColumns));
            }
        }

        return results;
    }

    private List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> CalculateGrandContinuousArcConfigs(float bendAmount)
    {
        List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)> results = new List<(Squad squad, Vector3 destination, Quaternion rotation, int columns)>();
        if (selectedSquads.Count == 0) return results;

        Vector3 centroid = GetSquadsCentroid(selectedSquads);
        Vector3 armyForward = selectedSquads[0].transform.forward;
        Vector3 armyRight = selectedSquads[0].transform.right;

        List<Squad> sorted = new List<Squad>(selectedSquads);
        sorted.RemoveAll(s => s == null);
        sorted.Sort((a, b) => Vector3.Dot(a.transform.position - centroid, armyRight).CompareTo(Vector3.Dot(b.transform.position - centroid, armyRight)));

        int count = sorted.Count;

        // 부대별 가로 폭 및 부대 간 간격 계산
        float avgSpacing = 1.1f;
        float[] squadWidths = new float[count];
        float totalWidth = 0f;

        for (int i = 0; i < count; i++)
        {
            Squad s = sorted[i];
            float spX = (s.currentFormationType == SquadFormationType.Loose || s.isLooseFormation) ? (s.spacing * 2.0f) : s.spacing;
            avgSpacing = spX;
            squadWidths[i] = Mathf.Max(spX, (s.currentColumns - 1) * spX);
            totalWidth += squadWidths[i];
        }

        float squadGap = avgSpacing;
        totalWidth += Mathf.Max(0, count - 1) * squadGap;
        float halfTotalWidth = Mathf.Max(1.0f, totalWidth * 0.5f);

        float currentX = -halfTotalWidth;

        for (int i = 0; i < count; i++)
        {
            Squad s = sorted[i];
            float squadMidX = currentX + (squadWidths[i] * 0.5f);
            float u = Mathf.Clamp(squadMidX / halfTotalWidth, -1f, 1f); // [-1: 좌익 끝, 0: 중앙, +1: 우익 끝]

            // 거대 군단 포물선 곡률 깊이
            float curveZ = (1.0f - (u * u)) * bendAmount;
            Vector3 arcPos = centroid + (armyRight * squadMidX) + (armyForward * curveZ);

            // 포물선 접선에 따른 날개 안쪽 회전 (좌익/우익이 곡선의 안쪽을 감싸도록 회전)
            float rotAngle = u * Mathf.Clamp(bendAmount / halfTotalWidth, -1f, 1f) * 28f;
            Quaternion arcRot = selectedSquads[0].transform.rotation * Quaternion.Euler(0f, rotAngle, 0f);

            results.Add((s, arcPos, arcRot, s.currentColumns));
            currentX += squadWidths[i] + squadGap;
        }

        return results;
    }

    private void ApplyGrandContinuousArcFormation(float bendAmount)
    {
        var configs = CalculateGrandContinuousArcConfigs(bendAmount);
        foreach (var c in configs)
        {
            if (c.squad != null)
            {
                c.squad.ClearWaypoints();
                c.squad.customCurvature = 0f; // 부대 전체가 이미 거대 호 상에 배치되므로 내부 왜곡 제거
                c.squad.CommandMoveWithFormation(c.destination, c.rotation, c.columns, forceSort: false);
            }
        }
    }

    private void ExecuteSquadCommand(Vector3 centerPos, Quaternion targetRot, int cols, bool isShift)
    {
        if (selectedSquads.Count == 1)
        {
            Squad squad = selectedSquads[0];
            if (squad != null)
            {
                if (isShift)
                {
                    squad.AddWaypoint(centerPos, targetRot, cols);
                }
                else
                {
                    squad.ClearWaypoints();
                    squad.CommandMoveWithFormation(centerPos, targetRot, cols, forceSort: false);
                }
            }
        }
        else if (selectedSquads.Count > 1)
        {
            Vector3 centroid = GetSquadsCentroid(selectedSquads);
            bool isLocked = IsCurrentSelectionLocked();

            Quaternion deltaRot = Quaternion.identity;
            if (targetRot != Quaternion.identity && selectedSquads.Count > 0 && selectedSquads[0] != null)
            {
                deltaRot = targetRot * Quaternion.Inverse(selectedSquads[0].transform.rotation);
            }

            foreach (Squad squad in selectedSquads)
            {
                if (squad == null) continue;

                Vector3 relOffset = squad.transform.position - centroid;
                Vector3 finalOffset = (isLocked && targetRot != Quaternion.identity) ? (deltaRot * relOffset) : relOffset;
                Vector3 targetSquadPos = centerPos + finalOffset;
                Quaternion finalSquadRot = (isLocked && targetRot != Quaternion.identity) ? (deltaRot * squad.transform.rotation) : squad.transform.rotation;

                if (isShift)
                {
                    squad.AddWaypoint(targetSquadPos, finalSquadRot, squad.currentColumns);
                }
                else
                {
                    squad.ClearWaypoints();
                    squad.CommandMoveWithFormation(targetSquadPos, finalSquadRot, squad.currentColumns, forceSort: false);
                }
            }
        }

        foreach (Unit unit in selectedUnits)
        {
            if (unit == null) continue;
            if (unit.mySquad != null) unit.mySquad.DetachUnit(unit);

            SendUnitMoveCommand(unit, centerPos, targetRot, isShift);
        }
    }

    /// <summary>
    /// 미니맵 우클릭 시 현재 선택된 부대들에게 원격 이동/공격 명령을 즉시 하달합니다.
    /// </summary>
    public void ExecuteMinimapCommand(Vector3 targetWorldPos, bool isAttackMove = false)
    {
        bool isShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        UnitCommandState cmdState = isAttackMove ? UnitCommandState.AttackMove : UnitCommandState.Move;

        foreach (Squad squad in selectedSquads)
        {
            if (squad == null) continue;

            if (isShift)
            {
                squad.AddWaypoint(targetWorldPos, squad.transform.rotation, squad.currentColumns);
            }
            else
            {
                squad.ClearWaypoints();
                squad.CommandMoveWithFormation(targetWorldPos, squad.transform.rotation, squad.currentColumns, forceSort: false, cmdState: cmdState);
            }
        }

        foreach (Unit unit in selectedUnits)
        {
            if (unit == null) continue;
            if (unit.mySquad != null) unit.mySquad.DetachUnit(unit);

            SendUnitMoveCommand(unit, targetWorldPos, unit.transform.rotation, isShift);
        }
    }

    private void CalculateFormationParams(Vector3 start, Vector3 end, out Vector3 centerPos, out Quaternion targetRot, out int cols)
    {
        Vector3 dragVector = end - start;
        dragVector.y = 0f;
        float dragDistance = dragVector.magnitude;

        Squad primarySquad = (selectedSquads.Count > 0) ? selectedSquads[0] : null;
        int totalUnits = primarySquad != null ? primarySquad.MemberCount : 10;

        if (dragDistance < 0.8f)
        {
            centerPos = start;
            targetRot = (primarySquad != null) ? primarySquad.transform.rotation : Quaternion.identity;
            cols = (primarySquad != null) ? Mathf.Clamp(primarySquad.currentColumns, 1, totalUnits) : 5;
        }
        else
        {
            centerPos = (start + end) * 0.5f;
            Vector3 frontVector = Vector3.Cross(dragVector.normalized, Vector3.up);
            targetRot = Quaternion.LookRotation(frontVector);

            float curSpacing = (primarySquad != null) ? primarySquad.spacingX : 1.1f;
            cols = Mathf.Clamp(Mathf.RoundToInt(dragDistance / curSpacing), 1, totalUnits);
        }
    }

    private bool GetMouseWorldPosition(out Vector3 hitPoint)
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            hitPoint = hit.point;
            return true;
        }
        hitPoint = Vector3.zero;
        return false;
    }

    #endregion

    #region Selection Utilities & GUI

    private void SelectSingleUnitOrSquad()
    {
        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // ⚡ 1순위: 기존 GameObject Unit 콜라이더 검사 (RaycastAll로 바닥에 가려진 유닛도 관통 검출)
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        Unit clickedUnit = null;
        Vector3 clickWorldPos = Vector3.zero;

        foreach (var h in hits)
        {
            Unit u = h.collider.GetComponent<Unit>();
            if (u != null && u.isPlayer)
            {
                clickedUnit = u;
                break;
            }
            if (clickWorldPos == Vector3.zero) clickWorldPos = h.point;
        }

        if (clickedUnit != null)
        {
            if (clickedUnit.mySquad != null)
            {
                if (isShiftPressed) ToggleSquadSelection(clickedUnit.mySquad);
                else SelectSquadDirectly(clickedUnit.mySquad, addSelection: false);
                return;
            }
            else
            {
                if (isShiftPressed && selectedUnits.Contains(clickedUnit)) DeselectUnit(clickedUnit);
                else SelectUnit(clickedUnit);
                return;
            }
        }

        // ⚡ 2순위: 순수 ECS 엔티티 2D Screen-Space Pixel 거리(45px) 100% 정밀 검사
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                typeof(MiniTotalWar.ECS.UnitEntityTag),
                typeof(MiniTotalWar.ECS.UnitMovementData)
            );

            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
            using (var movs = query.ToComponentDataArray<MiniTotalWar.ECS.UnitMovementData>(Unity.Collections.Allocator.Temp))
            {
                Vector2 mousePos = Input.mousePosition;
                float minScreenDist = 60f; // 마우스 커서 60픽셀 이내 최단거리 유닛 정밀 선택
                int bestIdx = -1;

                for (int i = 0; i < tags.Length; i++)
                {
                    if (tags[i].Faction == 1 && tags[i].IsAlive == 1)
                    {
                        Vector3 screenPt = mainCamera.WorldToScreenPoint((Vector3)movs[i].Position);
                        if (screenPt.z > 0f)
                        {
                            float dPix = Vector2.Distance(mousePos, new Vector2(screenPt.x, screenPt.y));
                            if (dPix < minScreenDist)
                            {
                                minScreenDist = dPix;
                                bestIdx = i;
                            }
                        }
                    }
                }

                if (bestIdx >= 0)
                {
                    int squadId = tags[bestIdx].SquadId;
                    bool isFree = (squadId == -1 || tags[bestIdx].IsFreeUnit == 1);

                    if (isFree) // ⚡ 순수 ECS 자유 유닛인 경우
                    {
                        Unity.Entities.Entity ent = entities[bestIdx];
                        if (isShiftPressed)
                        {
                            if (selectedECSEntities.Contains(ent)) selectedECSEntities.Remove(ent);
                            else selectedECSEntities.Add(ent);
                        }
                        else
                        {
                            DeselectAll();
                            selectedECSEntities.Add(ent);
                        }
                        UpdateCommandUI();
                        Debug.Log($"[PlayerController] 🎯 자유 ECS 유닛 단일 선택 성공! (Entity Index: {ent.Index}, 총 선택: {selectedECSEntities.Count}명)");
                        return;
                    }
                    else // ⚡ 부대 소속 유닛인 경우 해당 부대 선택
                    {
                        bool squadFound = false;
                        if (BattleManager.Instance != null)
                        {
                            var squads = BattleManager.Instance.GetAllSquads();
                            foreach (var s in squads)
                            {
                                if (s != null && s.GetInstanceID() == squadId)
                                {
                                    squadFound = true;
                                    if (isShiftPressed) ToggleSquadSelection(s);
                                    else SelectSquadDirectly(s, addSelection: false);
                                    return;
                                }
                            }
                        }

                        // ⚡ 매칭되는 부대 오브젝트가 없으면 독립 자유 유닛으로 간주하여 즉시 단일 선택
                        if (!squadFound)
                        {
                            Unity.Entities.Entity ent = entities[bestIdx];
                            if (isShiftPressed)
                            {
                                if (selectedECSEntities.Contains(ent)) selectedECSEntities.Remove(ent);
                                else selectedECSEntities.Add(ent);
                            }
                            else
                            {
                                DeselectAll();
                                selectedECSEntities.Add(ent);
                            }
                            UpdateCommandUI();
                            Debug.Log($"[PlayerController] 🎯 독립 ECS 유닛 단일 선택 성공! (Entity Index: {ent.Index})");
                            return;
                        }
                    }
                }
            }
        }

        // ⚡ 3순위: 부대 중심점 근처(반경 3.0m) 클릭 판정
        if (BattleManager.Instance != null && clickWorldPos != Vector3.zero)
        {
            var squads = BattleManager.Instance.GetAllSquads();
            Squad closestSquad = null;
            float minClickDist = 3.0f;

            foreach (Squad s in squads)
            {
                if (s == null || !s.isPlayer) continue;
                float dist = Vector3.Distance(clickWorldPos, s.GetVisualCenter());
                if (dist < minClickDist)
                {
                    minClickDist = dist;
                    closestSquad = s;
                }
            }

            if (closestSquad != null)
            {
                if (isShiftPressed) ToggleSquadSelection(closestSquad);
                else SelectSquadDirectly(closestSquad, addSelection: false);
                return;
            }
        }
    }

    private void SelectUnitsInScreenRect(Vector3 start, Vector3 end)
    {
        Rect selectionRect = GetScreenRect(start, end);
        HashSet<Squad> squadsToAdd = new HashSet<Squad>();

        // ⚡ 1. 부대(Squad) 실시간 무게중심(VisualCenter) 스크린 박스 검사 (순수 ECS 100% 호환!)
        if (BattleManager.Instance != null)
        {
            var squads = BattleManager.Instance.GetAllSquads();
            foreach (Squad squad in squads)
            {
                if (squad == null || !squad.isPlayer) continue;

                Vector3 squadScreenPos = mainCamera.WorldToScreenPoint(squad.GetVisualCenter());
                if (squadScreenPos.z > 0 && selectionRect.Contains(squadScreenPos))
                {
                    var lockedGroup = GetLockedGroupContaining(squad);
                    if (lockedGroup != null && lockedGroup.Count > 1)
                    {
                        foreach (var s in lockedGroup) if (s != null) squadsToAdd.Add(s);
                    }
                    else
                    {
                        squadsToAdd.Add(squad);
                    }
                }
            }
        }

        // ⚡ 2. 순수 ECS 자유 유닛(SquadId == -1 || IsFreeUnit == 1) 스크린 박스 검사
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                typeof(MiniTotalWar.ECS.UnitEntityTag),
                typeof(MiniTotalWar.ECS.UnitMovementData)
            );

            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
            using (var movs = query.ToComponentDataArray<MiniTotalWar.ECS.UnitMovementData>(Unity.Collections.Allocator.Temp))
            {
                for (int i = 0; i < tags.Length; i++)
                {
                    if (tags[i].Faction == 1 && (tags[i].SquadId == -1 || tags[i].IsFreeUnit == 1) && tags[i].IsAlive == 1)
                    {
                        Vector3 screenPos = mainCamera.WorldToScreenPoint((Vector3)movs[i].Position);
                        if (screenPos.z > 0 && selectionRect.Contains(screenPos))
                        {
                            if (!selectedECSEntities.Contains(entities[i]))
                            {
                                selectedECSEntities.Add(entities[i]);
                            }
                        }
                    }
                }
            }
        }

        // 3. 기존 GameObject Unit 호환 검사
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit unit in allUnits)
        {
            if (unit == null || !unit.isPlayer) continue;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);

            if (screenPos.z > 0 && selectionRect.Contains(screenPos))
            {
                if (unit.mySquad != null)
                {
                    var lockedGroup = GetLockedGroupContaining(unit.mySquad);
                    if (lockedGroup != null && lockedGroup.Count > 1)
                    {
                        foreach (var s in lockedGroup) if (s != null && s.MemberCount > 0) squadsToAdd.Add(s);
                    }
                    else
                    {
                        squadsToAdd.Add(unit.mySquad);
                    }
                }
                else
                {
                    SelectUnit(unit);
                }
            }
        }

        foreach (Squad squad in squadsToAdd) SelectSquad(squad);
        if (selectedECSEntities.Count > 0)
        {
            Debug.Log($"[PlayerController] 🎯 드래그 박스로 자유 ECS 유닛 {selectedECSEntities.Count}명 선택 완료!");
        }
        UpdateCommandUI();
    }

    private Rect GetScreenRect(Vector3 screenPos1, Vector3 screenPos2)
    {
        Vector3 min = Vector3.Min(screenPos1, screenPos2);
        Vector3 max = Vector3.Max(screenPos1, screenPos2);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public List<Squad> GetSelectedSquads() => selectedSquads;

    private void SelectSquad(Squad squad)
    {
        if (squad == null || selectedSquads.Contains(squad)) return;
        selectedSquads.Add(squad);
        squad.SelectAllMembers(true);
        UpdateCommandUI();
    }

    private void DeselectSquad(Squad squad)
    {
        if (squad == null || !selectedSquads.Contains(squad)) return;
        selectedSquads.Remove(squad);
        squad.SelectAllMembers(false);
        UpdateCommandUI();
    }

    private void SelectUnit(Unit unit)
    {
        if (unit == null || selectedUnits.Contains(unit)) return;
        selectedUnits.Add(unit);
        unit.SetSelected(true);
        UpdateCommandUI();
    }

    private void DeselectUnit(Unit unit)
    {
        if (unit == null || !selectedUnits.Contains(unit)) return;
        selectedUnits.Remove(unit);
        unit.SetSelected(false);
        UpdateCommandUI();
    }

    public void DeselectAll()
    {
        foreach (Squad squad in selectedSquads) { if (squad != null) squad.SelectAllMembers(false); }
        selectedSquads.Clear();
        foreach (Unit unit in selectedUnits) { if (unit != null) unit.SetSelected(false); }
        selectedUnits.Clear();
        selectedECSEntities.Clear();
        UpdateCommandUI();
    }

    public List<Squad> GetLockedGroupContaining(Squad squad)
    {
        if (squad == null) return null;

        foreach (int groupIdx in lockedControlGroups)
        {
            if (controlGroups.TryGetValue(groupIdx, out var group) && group.squads != null && group.squads.Contains(squad))
            {
                return group.squads;
            }
        }

        foreach (var group in adHocLockedGroups)
        {
            if (group != null && group.Contains(squad))
            {
                return group;
            }
        }

        return null;
    }

    public bool IsSquadInLockedGroup(Squad squad)
    {
        return GetLockedGroupContaining(squad) != null;
    }

    public void SelectSquadDirectly(Squad squad, bool addSelection = false)
    {
        if (squad == null) return;

        var lockedGroup = GetLockedGroupContaining(squad);
        if (lockedGroup != null && lockedGroup.Count > 1)
        {
            if (!addSelection) DeselectAll();
            foreach (var s in lockedGroup)
            {
                if (s != null && s.MemberCount > 0) SelectSquad(s);
            }
        }
        else
        {
            if (!addSelection) DeselectAll();
            SelectSquad(squad);
        }
        UpdateCommandUI();
    }

    public void ToggleSquadSelection(Squad squad)
    {
        if (squad == null) return;

        var lockedGroup = GetLockedGroupContaining(squad);
        if (lockedGroup != null && lockedGroup.Count > 1)
        {
            bool anySelected = lockedGroup.Exists(s => selectedSquads.Contains(s));
            if (anySelected)
            {
                foreach (var s in lockedGroup) if (s != null) DeselectSquad(s);
            }
            else
            {
                foreach (var s in lockedGroup) if (s != null && s.MemberCount > 0) SelectSquad(s);
            }
        }
        else
        {
            if (selectedSquads.Contains(squad)) DeselectSquad(squad);
            else SelectSquad(squad);
        }
        UpdateCommandUI();
    }

    private void OnGUI()
    {
        if (isSelecting)
        {
            Rect rect = GetScreenRect(dragStartPosition, Input.mousePosition);
            rect.y = Screen.height - rect.y - rect.height;
            DrawScreenRect(rect, new Color(0f, 0.5f, 1f, 0.25f));
            DrawScreenRectBorder(rect, 2, new Color(0f, 0.8f, 1f, 0.8f));
        }
    }

    private static Texture2D whiteTexture;
    private static Texture2D WhiteTexture
    {
        get
        {
            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1);
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
            }
            return whiteTexture;
        }
    }

    private static void DrawScreenRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, WhiteTexture);
        GUI.color = Color.white;
    }

    private static void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }

    #endregion

    #region Command Input & UI

    private void HandleHotkeyInput()
    {
        if (selectedUnits.Count == 0 && selectedSquads.Count == 0 && selectedECSEntities.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.Z)) 
        { 
            isAttackTargetingMode = true; 
            Debug.Log("[PlayerController] ⚔️ [Z] 어택땅(Attack) 모드 활성화! 이동/공격할 목표 지점을 좌클릭하세요. (취소: 마우스 우클릭)"); 
        }
        if (Input.GetKeyDown(KeyCode.X)) 
        { 
            StopSelected(); 
        }
        if (Input.GetKeyDown(KeyCode.C)) 
        { 
            ToggleStance(UnitStance.HoldPosition); 
        }
        if (Input.GetKeyDown(KeyCode.V)) 
        { 
            ToggleAutoAttack(); 
        }
        if (Input.GetKeyDown(KeyCode.B)) 
        { 
            ToggleLooseFormation(); 
        }
        if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKey(KeyCode.LeftBracket))
        {
            foreach (Squad s in selectedSquads)
            {
                if (s != null) s.AdjustSpacing(-0.04f);
            }
        }
        if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKey(KeyCode.RightBracket))
        {
            foreach (Squad s in selectedSquads)
            {
                if (s != null) s.AdjustSpacing(0.04f);
            }
        }
        if (Input.GetKeyDown(KeyCode.R)) 
        { 
            ToggleRunMode(); 
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            ToggleLockCurrentGroup();
        }
        if (Input.GetKeyDown(KeyCode.Slash)) 
        { 
            SetFormation(SquadFormationType.Normal); 
        }
        if (Input.GetKeyDown(KeyCode.N)) 
        { 
            SetFormation(SquadFormationType.Diamond); 
            if (selectedSquads.Count > 1) ArrangeGrandDiamondFormation();
        }
        if (Input.GetKeyDown(KeyCode.M)) 
        { 
            SetFormation(SquadFormationType.Wedge); 
            if (selectedSquads.Count > 1) ArrangeGrandWedgeFormation();
        }
        if (Input.GetKeyDown(KeyCode.Comma)) // , (<) 사각방진
        {
            SetFormation(SquadFormationType.Square);
            if (selectedSquads.Count > 1) ArrangeGrandSquareFormation();
        }
        if (Input.GetKeyDown(KeyCode.Period)) // . (>) 원형진
        {
            SetFormation(SquadFormationType.Circle);
            if (selectedSquads.Count > 1) ArrangeGrandCircleFormation();
        }
    }

    private void ArrangeGrandWedgeFormation()
    {
        if (selectedSquads.Count < 2) return;

        Vector3 centroid = GetSquadsCentroid(selectedSquads);
        Vector3 forward = selectedSquads[0].transform.forward;
        Vector3 right = selectedSquads[0].transform.right;

        List<Squad> sorted = new List<Squad>(selectedSquads);
        sorted.RemoveAll(s => s == null);
        sorted.Sort((a, b) => Vector3.Dot(a.transform.position, right).CompareTo(Vector3.Dot(b.transform.position, right)));

        int midIdx = sorted.Count / 2;
        float spacingX = 10.0f;
        float spacingZ = 6.0f;

        for (int i = 0; i < sorted.Count; i++)
        {
            Squad s = sorted[i];
            int offsetFromMid = i - midIdx;
            float posX = offsetFromMid * spacingX;
            float posZ = -Mathf.Abs(offsetFromMid) * spacingZ;

            Vector3 targetPos = centroid + (right * posX) + (forward * posZ);
            s.CommandMoveWithFormation(targetPos, s.transform.rotation, s.currentColumns, forceSort: false);
        }
        Debug.Log($"[PlayerController] 🔺 [M] 군단 쐐기 화살촉 진형(Grand Wedge) 배치 완료!");
    }

    private void ArrangeGrandDiamondFormation()
    {
        if (selectedSquads.Count < 2) return;

        Vector3 centroid = GetSquadsCentroid(selectedSquads);
        Vector3 forward = selectedSquads[0].transform.forward;
        Vector3 right = selectedSquads[0].transform.right;

        List<Squad> sorted = new List<Squad>(selectedSquads);
        sorted.RemoveAll(s => s == null);
        sorted.Sort((a, b) => Vector3.Dot(a.transform.position, right).CompareTo(Vector3.Dot(b.transform.position, right)));

        int midIdx = sorted.Count / 2;
        float spacingX = 12.0f;
        float spacingZ = 5.0f;

        for (int i = 0; i < sorted.Count; i++)
        {
            Squad s = sorted[i];
            int offsetFromMid = i - midIdx;
            float posX = offsetFromMid * spacingX;
            float posZ = (Mathf.Abs(offsetFromMid) % 2 == 1) ? -spacingZ : 0f;

            Vector3 targetPos = centroid + (right * posX) + (forward * posZ);
            s.CommandMoveWithFormation(targetPos, s.transform.rotation, s.currentColumns, forceSort: false);
        }
        Debug.Log($"[PlayerController] 🔷 [N] 다이아몬드 연계 기동 대형(Diamond Fleet) 배치 완료!");
    }

    private void ArrangeGrandSquareFormation()
    {
        if (selectedSquads.Count < 2) return;

        Vector3 centroid = GetSquadsCentroid(selectedSquads);
        Quaternion baseRot = selectedSquads[0].transform.rotation;
        Vector3 forward = baseRot * Vector3.forward;
        Vector3 right = baseRot * Vector3.right;

        int count = selectedSquads.Count;
        float sideDistance = 14.0f;

        for (int i = 0; i < count; i++)
        {
            Squad s = selectedSquads[i];
            if (s == null) continue;

            int side = i % 4;
            Vector3 dest = centroid;
            Quaternion squadRot = baseRot;

            if (side == 0) // 북쪽 변 (전방 0도)
            {
                dest = centroid + (forward * sideDistance);
                squadRot = baseRot;
            }
            else if (side == 1) // 동쪽 변 (우측 90도)
            {
                dest = centroid + (right * sideDistance);
                squadRot = baseRot * Quaternion.Euler(0f, 90f, 0f);
            }
            else if (side == 2) // 남쪽 변 (후방 180도)
            {
                dest = centroid - (forward * sideDistance);
                squadRot = baseRot * Quaternion.Euler(0f, 180f, 0f);
            }
            else if (side == 3) // 서쪽 변 (좌측 -90도)
            {
                dest = centroid - (right * sideDistance);
                squadRot = baseRot * Quaternion.Euler(0f, -90f, 0f);
            }

            s.ClearWaypoints();
            s.CommandMoveWithFormation(dest, squadRot, s.currentColumns, forceSort: false);
        }
        Debug.Log($"[PlayerController] 🛡️ [,] 거대 성채 사각방진(Grand Fortress Square) 배치 완료! ({count}개 부대 4방위 연계)");
    }

    private void ArrangeGrandCircleFormation()
    {
        if (selectedSquads.Count < 2) return;

        Vector3 centroid = GetSquadsCentroid(selectedSquads);
        int count = selectedSquads.Count;
        float radius = Mathf.Max(12.0f, count * 5.0f);
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            Squad s = selectedSquads[i];
            if (s == null) continue;

            float deg = i * angleStep;
            float rad = deg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
            Vector3 dest = centroid + offset;
            Quaternion squadRot = Quaternion.Euler(0f, deg, 0f);

            s.ClearWaypoints();
            s.CommandMoveWithFormation(dest, squadRot, s.currentColumns, forceSort: false);
        }
        Debug.Log($"[PlayerController] 🔵 [.] 거대 원형 포진(Grand Fortress Orb) 배치 완료! ({count}개 부대 360도 연계)");
    }

    private void HandleAttackTargetingInput()
    {
        if (!isAttackTargetingMode) return;

        // 우클릭 또는 ESC 키 입력 시 타겟팅 모드 취소
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            isAttackTargetingMode = false;
            Debug.Log("[PlayerController] ❌ 어택땅 타겟팅 모드가 취소되었습니다.");
            return;
        }

        // 좌클릭 시 어택땅 또는 특정 적 부대 타겟 공격 실행
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // 🎯 1. 마우스 아래에 특정 적군 부대/유닛이 있는지 먼저 정밀 검사
            if (TryGetEnemyUnderMouse(out Squad clickedEnemySquad, out Vector3 enemyWorldPos))
            {
                if (clickedEnemySquad != null)
                {
                    CommandAttackTargetSquad(clickedEnemySquad);
                }
                else
                {
                    CommandAttackTargetPosition(enemyWorldPos);
                }
                return;
            }

            // 🎯 2. 빈 바닥을 클릭한 경우: 어택땅(AttackMove) 실행
            if (GetMouseWorldPosition(out Vector3 targetPos))
            {
                int squadCount = selectedSquads.Count;
                int unitCount = 0;

                if (squadCount == 1)
                {
                    Squad s = selectedSquads[0];
                    if (s != null)
                    {
                        s.ClearWaypoints();
                        s.CommandMoveWithFormation(targetPos, s.transform.rotation, s.currentColumns, false, UnitCommandState.AttackMove);
                    }
                }
                else if (squadCount > 1)
                {
                    Vector3 centroid = GetSquadsCentroid(selectedSquads);
                    foreach (Squad s in selectedSquads)
                    {
                        if (s != null)
                        {
                            Vector3 relOffset = s.transform.position - centroid;
                            Vector3 squadDest = targetPos + relOffset;
                            s.ClearWaypoints();
                            s.CommandMoveWithFormation(squadDest, s.transform.rotation, s.currentColumns, false, UnitCommandState.AttackMove);
                        }
                    }
                }

                // 자유 유닛 어택땅: 1점으로 겹치지 않도록 분산 슬롯 할당
                List<Unit> freeUnits = new List<Unit>();
                foreach (Unit u in selectedUnits)
                {
                    if (u != null && u.mySquad == null) freeUnits.Add(u);
                }

                if (freeUnits.Count == 1)
                {
                    freeUnits[0].AttackMoveTo(targetPos);
                    unitCount = 1;
                }
                else if (freeUnits.Count > 1)
                {
                    Vector3 currentCenter = Vector3.zero;
                    foreach (Unit u in freeUnits) currentCenter += u.transform.position;
                    currentCenter /= freeUnits.Count;

                    float avgDist = 0f;
                    foreach (Unit u in freeUnits) avgDist += Vector3.Distance(u.transform.position, currentCenter);
                    avgDist /= freeUnits.Count;

                    if (avgDist < 0.5f)
                    {
                        var (clusterSlots, _) = CalculateClusterSlotPositions(targetPos, Quaternion.identity, freeUnits.Count);
                        for (int i = 0; i < freeUnits.Count && i < clusterSlots.Count; i++)
                        {
                            freeUnits[i].AttackMoveTo(clusterSlots[i]);
                            unitCount++;
                        }
                    }
                    else
                    {
                        foreach (Unit u in freeUnits)
                        {
                            Vector3 offset = u.transform.position - currentCenter;
                            Vector3 dest = targetPos + offset;
                            if (UnityEngine.AI.NavMesh.SamplePosition(dest, out UnityEngine.AI.NavMeshHit hit, 1.0f, UnityEngine.AI.NavMesh.AllAreas))
                            {
                                dest = hit.position;
                            }
                            u.AttackMoveTo(dest);
                            unitCount++;
                        }
                    }
                }

                // ⚡ 순수 ECS 자유 유닛 어택땅
                if (selectedECSEntities.Count > 0)
                {
                    var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                    if (world != null && world.IsCreated)
                    {
                        var em = world.EntityManager;
                        var (clSlots, _) = CalculateClusterSlotPositions(targetPos, Quaternion.identity, selectedECSEntities.Count);

                        for (int i = 0; i < selectedECSEntities.Count; i++)
                        {
                            var ent = selectedECSEntities[i];
                            if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitMovementData>(ent) && em.HasComponent<MiniTotalWar.ECS.UnitCombatData>(ent))
                            {
                                var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                                var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                                Vector3 slot = (i < clSlots.Count) ? clSlots[i] : targetPos;
                                mov.TargetPosition = slot;
                                float spd = (mov.MoveSpeed >= 2.0f) ? 2.8f : 1.2f;
                                mov.MoveSpeed = spd;
                                mov.Acceleration = (spd >= 2.0f) ? 8.0f : 4.5f;

                                combat.CurrentState = (int)UnitCommandState.AttackMove;
                                combat.AutoAttackEnabled = 1;
                                combat.ChargeImpactReady = 1;

                                em.SetComponentData(ent, mov);
                                em.SetComponentData(ent, combat);
                                unitCount++;
                            }
                        }
                    }
                }

                Debug.Log($"[PlayerController] ⚔️ 어택땅 명령 전송 완료! (목표 좌표: {targetPos:F1}, 대상: 부대 {squadCount}개 / 자유유닛 {unitCount}명)");
            }
            isAttackTargetingMode = false;
        }
    }

    private void StopSelected()
    {
        isAttackTargetingMode = false;
        int stoppedUnits = 0;
        int stoppedSquads = 0;

        foreach (Unit u in selectedUnits)
        {
            if (u != null)
            {
                u.currentState = UnitCommandState.Idle;
                u.hasSquadCommand = false;
                var agent = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                    agent.velocity = Vector3.zero;
                }

                if (UnitJobSimulationManager.Instance != null)
                {
                    UnitJobSimulationManager.Instance.UpdateUnitTargetPosition(u, u.transform.position, u.transform.rotation, UnitCommandState.Idle);
                }
                if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
                {
                    MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateEntityTarget(u, u.transform.position, u.transform.rotation, UnitCommandState.Idle);
                }

                stoppedUnits++;
            }
        }

        // ⚡ 순수 ECS 자유 유닛 정지 동기화
        if (selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    var ent = selectedECSEntities[i];
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitMovementData>(ent) && em.HasComponent<MiniTotalWar.ECS.UnitCombatData>(ent))
                    {
                        var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                        var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                        mov.TargetPosition = mov.Position;
                        mov.CurrentSpeed = 0f;
                        combat.CurrentState = (int)UnitCommandState.Idle;
                        combat.TargetEntity = Unity.Entities.Entity.Null;

                        em.SetComponentData(ent, mov);
                        em.SetComponentData(ent, combat);
                        stoppedUnits++;
                    }
                }
            }
        }

        foreach (Squad s in selectedSquads)
        {
            if (s != null)
            {
                s.CommandStop();
                stoppedSquads++;
            }
        }

        Debug.Log($"[PlayerController] 🛑 [X] 정지(Stop) 명령 완료! (부대 {stoppedSquads}개, 유닛 {stoppedUnits}명 제자리 정지)");
    }

    private void ToggleStance(UnitStance targetStance)
    {
        UnitStance newStance = UnitStance.Aggressive;
        int affectedCount = 0;

        if (selectedUnits.Count > 0 && selectedUnits[0] != null)
        {
            newStance = (selectedUnits[0].currentStance == targetStance) ? UnitStance.Aggressive : targetStance;
        }
        else if (selectedSquads.Count > 0 && selectedSquads[0] != null)
        {
            newStance = (selectedSquads[0].currentStance == targetStance) ? UnitStance.Aggressive : targetStance;
        }

        foreach (Unit u in selectedUnits)
        {
            if (u != null)
            {
                u.currentStance = newStance;
                if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
                {
                    MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateEntityTarget(u, u.FixedTargetPos, u.TargetRotation, u.currentState);
                }
                affectedCount++;
            }
        }

        // ⚡ 순수 ECS 자유 유닛 스탠스 동기화
        if (selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    var ent = selectedECSEntities[i];
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitCombatData>(ent))
                    {
                        var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);
                        combat.AutoAttackEnabled = (newStance == UnitStance.HoldPosition) ? 0 : 1;
                        em.SetComponentData(ent, combat);
                        affectedCount++;
                    }
                }
            }
        }

        foreach (Squad s in selectedSquads)
        {
            if (s != null)
            {
                s.SetStance(newStance);
                affectedCount += s.MemberCount;
            }
        }

        string stanceName = (newStance == UnitStance.HoldPosition) ? "🛡️ [위치 사수 (대형 유지 및 제자리 공격)]" : "⚔️ [자유 추격 (사거리 밖 적 추격)]";
        Debug.Log($"[PlayerController] 🛡️ [C] 전투 태세 변경 -> {stanceName} (적용 유닛: {affectedCount}명)");
        UpdateCommandUI();
    }

    private void ToggleAutoAttack()
    {
        bool newState = true;
        int affectedCount = 0;

        if (selectedUnits.Count > 0 && selectedUnits[0] != null)
        {
            newState = !selectedUnits[0].autoAttackEnabled;
        }
        else if (selectedSquads.Count > 0 && selectedSquads[0] != null)
        {
            newState = !selectedSquads[0].autoAttackEnabled;
        }

        foreach (Unit u in selectedUnits)
        {
            if (u != null)
            {
                u.autoAttackEnabled = newState;
                if (MiniTotalWar.ECS.SquadECSSimulationBridge.Instance != null)
                {
                    MiniTotalWar.ECS.SquadECSSimulationBridge.Instance.UpdateEntityTarget(u, u.FixedTargetPos, u.TargetRotation, u.currentState);
                }
                affectedCount++;
            }
        }

        // ⚡ 순수 ECS 자유 유닛 선제 돌격 요격 vs 접촉 방어 동기화
        if (selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    var ent = selectedECSEntities[i];
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitCombatData>(ent))
                    {
                        var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);
                        combat.AutoAttackEnabled = newState ? 1 : 0;
                        em.SetComponentData(ent, combat);
                        affectedCount++;
                    }
                }
            }
        }

        foreach (Squad s in selectedSquads)
        {
            if (s != null)
            {
                s.SetAutoAttack(newState);
                affectedCount += s.MemberCount;
            }
        }

        string stateText = newState ? "⚔️ [선제 돌격 요격 (적 접근 시 자동 돌격)]" : "🛡️ [접촉 방어 모드 (돌격하지 않고 몸이 붙었을 때만 교전)]";
        Debug.Log($"[PlayerController] 🎯 [V] 근접 교전 태세 -> {stateText} (적용 유닛: {affectedCount}명)");
        UpdateCommandUI();
    }

    private void ToggleLooseFormation()
    {
        bool newState = false;
        int changedSquads = 0;

        foreach (Squad s in selectedSquads)
        {
            if (s != null)
            {
                s.ToggleLooseFormation();
                newState = s.isLooseFormation;
                changedSquads++;
            }
        }

        string stateDesc = newState ? "🟢 [활성화 (간격 2배 확대)]" : "🔴 [비활성화 (기본 밀집 간격)]";
        Debug.Log($"[PlayerController] 💨 [B] 산개 모드 토글 -> {stateDesc} (적용 부대: {changedSquads}개)");
    }

    private void ToggleRunMode()
    {
        bool anyRunning = false;
        foreach (Squad s in selectedSquads)
        {
            if (s != null && s.isRunning) { anyRunning = true; break; }
        }
        if (!anyRunning)
        {
            foreach (Unit u in selectedUnits)
            {
                if (u != null && u.isRunning) { anyRunning = true; break; }
            }
        }

        bool targetRunState = !anyRunning;

        int affectedSquads = 0;
        int affectedUnits = 0;

        foreach (Squad s in selectedSquads)
        {
            if (s != null)
            {
                s.SetRunMode(targetRunState);
                affectedSquads++;
            }
        }

        foreach (Unit u in selectedUnits)
        {
            if (u != null)
            {
                u.SetRunMode(targetRunState);
                affectedUnits++;
            }
        }

        // ⚡ 순수 ECS 자유 유닛 이동 속도 동기화
        if (selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    var ent = selectedECSEntities[i];
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitMovementData>(ent) && em.HasComponent<MiniTotalWar.ECS.UnitCombatData>(ent))
                    {
                        var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                        var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                        mov.MoveSpeed = targetRunState ? 2.8f : 1.2f;
                        mov.Acceleration = targetRunState ? 8.0f : 5.0f;
                        combat.ChargeSpeed = 4.8f;

                        em.SetComponentData(ent, mov);
                        em.SetComponentData(ent, combat);
                        affectedUnits++;
                    }
                }
            }
        }

        string stateText = targetRunState ? "🏃 [달리기 (Run) 모드 ON - 속도 2.4/2.8 m/s]" : "🚶 [걷기 (Walk) 모드 ON - 속도 1.0/1.2 m/s]";
        Debug.Log($"[PlayerController] ⚡ [R] 이동 모드 토글 -> {stateText} (부대: {affectedSquads}개, 개별 유닛: {affectedUnits}명)");

        UpdateCommandUI();
    }

    private void SetFormation(SquadFormationType formType)
    {
        currentFormationType = formType;
        int changedSquads = 0;

        foreach (Squad s in selectedSquads)
        {
            if (s != null)
            {
                s.SetFormationType(formType);
                changedSquads++;

                string desc = formType switch
                {
                    SquadFormationType.Normal => "🟦 [/] 일자진 (Normal)",
                    SquadFormationType.Diamond => "🔷 [N] 마름모 대형 (Diamond)",
                    SquadFormationType.Wedge => "🔺 [M] 쐐기진 (Wedge)",
                    SquadFormationType.Square => "🛡️ [,] 사각방진 (Hollow Square)",
                    SquadFormationType.Circle => "🔵 [.] 원형진 (Hollow Circle)",
                    _ => $"📐 [{formType}] 진형"
                };
                Debug.Log($"[PlayerController] {desc} 변경 완료! ({s.name}, 총원: {s.members.Count}명, 산개모드: {(s.isLooseFormation ? "ON" : "OFF")})");
            }
        }

        if (changedSquads == 0 && selectedUnits.Count > 0)
        {
            Debug.Log($"[PlayerController] 📐 자유 유닛 {selectedUnits.Count}명은 대열 드래그(우클릭 드래그)를 통해 원하는 대형으로 즉시 배치할 수 있습니다.");
        }
    }

    private void UpdateCommandUI()
    {
        if (SquadCardUIManager.Instance != null)
        {
            SquadCardUIManager.Instance.UpdateSelectionUI(selectedSquads);
        }

        if (CommandUIManager.Instance == null) return;

        if (selectedUnits.Count == 0 && selectedSquads.Count == 0 && selectedECSEntities.Count == 0)
        {
            CommandUIManager.Instance.ClearButtons();
            return;
        }

        bool isAnyRunning = false;
        foreach (Squad s in selectedSquads) if (s != null && s.isRunning) { isAnyRunning = true; break; }
        if (!isAnyRunning) foreach (Unit u in selectedUnits) if (u != null && u.isRunning) { isAnyRunning = true; break; }
        if (!isAnyRunning && selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    var ent = selectedECSEntities[i];
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitMovementData>(ent))
                    {
                        var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                        if (mov.MoveSpeed >= 2.0f) { isAnyRunning = true; break; }
                    }
                }
            }
        }

        bool isAnyAutoAttack = true;
        if (selectedUnits.Count > 0 && selectedUnits[0] != null) isAnyAutoAttack = selectedUnits[0].autoAttackEnabled;
        else if (selectedSquads.Count > 0 && selectedSquads[0] != null) isAnyAutoAttack = selectedSquads[0].autoAttackEnabled;

        bool isAnyHold = false;
        if (selectedUnits.Count > 0 && selectedUnits[0] != null) isAnyHold = (selectedUnits[0].currentStance == UnitStance.HoldPosition);
        else if (selectedSquads.Count > 0 && selectedSquads[0] != null) isAnyHold = (selectedSquads[0].currentStance == UnitStance.HoldPosition);

        bool isAnyLoose = false;
        foreach (Squad s in selectedSquads) if (s != null && s.isLooseFormation) { isAnyLoose = true; break; }

        List<CommandButtonData> cmds = new List<CommandButtonData>();

        // 1. [다중 부대 전용] 군단 잠금 (부대가 2개 이상 선택되었을 때만 노출)
        if (selectedSquads.Count >= 2)
        {
            bool isLocked = IsCurrentSelectionLocked();
            string lockTitle = isLocked ? "군단 해제" : "군단 잠금";
            cmds.Add(new CommandButtonData
            {
                commandId = "LockGroup",
                buttonName = lockTitle,
                hotkeyText = "G",
                onClickAction = () => { ToggleLockCurrentGroup(); }
            });
        }

        // 2. [공통 명령] 자유 유닛 & 부대 모두 지원하는 기본 전투/기동 명령
        cmds.Add(new CommandButtonData { commandId = "Attack", buttonName = "공격", hotkeyText = "Z", onClickAction = () => { isAttackTargetingMode = true; Debug.Log("[PlayerController] ⚔️ [Z] 어택땅(Attack) 버튼 클릭!"); } });
        cmds.Add(new CommandButtonData { commandId = "Stop", buttonName = "정지", hotkeyText = "X", onClickAction = () => { StopSelected(); } });
        cmds.Add(new CommandButtonData { commandId = "Hold", buttonName = isAnyHold ? "자유 추격" : "위치 사수", hotkeyText = "C", onClickAction = () => { ToggleStance(UnitStance.HoldPosition); } });
        cmds.Add(new CommandButtonData { commandId = "RunToggle", buttonName = isAnyRunning ? "걷기" : "달리기", hotkeyText = "R", onClickAction = () => { ToggleRunMode(); } });
        cmds.Add(new CommandButtonData { commandId = "AutoAttack", buttonName = isAnyAutoAttack ? "돌격 요격" : "접촉 방어", hotkeyText = "V", onClickAction = () => { ToggleAutoAttack(); } });

        // 3. [부대 전용 명령] 부대(Squad)가 1개 이상 선택되었을 때만 노출되는 대형 진형 명령
        if (selectedSquads.Count > 0)
        {
            cmds.Add(new CommandButtonData { commandId = "Loose", buttonName = isAnyLoose ? "밀집 방진" : "전군 산개", hotkeyText = "B", onClickAction = () => { ToggleLooseFormation(); } });
            cmds.Add(new CommandButtonData { commandId = "Normal", buttonName = "일자진", hotkeyText = "/", onClickAction = () => { SetFormation(SquadFormationType.Normal); } });
            cmds.Add(new CommandButtonData { commandId = "Diamond", buttonName = "마름모", hotkeyText = "N", onClickAction = () => { SetFormation(SquadFormationType.Diamond); if (selectedSquads.Count > 1) ArrangeGrandDiamondFormation(); } });
            cmds.Add(new CommandButtonData { commandId = "Wedge", buttonName = "쐐기진", hotkeyText = "M", onClickAction = () => { SetFormation(SquadFormationType.Wedge); if (selectedSquads.Count > 1) ArrangeGrandWedgeFormation(); } });
            cmds.Add(new CommandButtonData { commandId = "Square", buttonName = "사각방진", hotkeyText = ",", onClickAction = () => { SetFormation(SquadFormationType.Square); if (selectedSquads.Count > 1) ArrangeGrandSquareFormation(); } });
            cmds.Add(new CommandButtonData { commandId = "Circle", buttonName = "원형진", hotkeyText = ".", onClickAction = () => { SetFormation(SquadFormationType.Circle); if (selectedSquads.Count > 1) ArrangeGrandCircleFormation(); } });
        }

        CommandUIManager.Instance.ShowCommands(cmds);
    }

    /// <summary>
    /// 선택된 아군 부대/유닛들에게 특정 적 부대를 향해 대형을 유지하며 정면 돌격 교전 명령을 하달합니다.
    /// </summary>
    public void CommandAttackTargetSquad(Squad enemySquad)
    {
        if (enemySquad == null || (enemySquad.members.Count == 0 && enemySquad.initialUnitCount == 0)) return;

        Vector3 enemyFrontPos = enemySquad.GetFrontLineCenter();
        Vector3 enemyVisualCenter = enemySquad.GetVisualCenter();

        int attackedSquads = 0;

        foreach (Squad mySquad in selectedSquads)
        {
            if (mySquad == null || (mySquad.members.Count == 0 && mySquad.initialUnitCount == 0)) continue;

            // 즉시 1프레임에 적 시선 축 투영 완료 및 전원 일제 포위 돌격
            mySquad.CommandAttackSquad(enemySquad);
            attackedSquads++;
        }

        // 자유 유닛들도 적 부대 중심을 향해 AttackMove
        foreach (Unit u in selectedUnits)
        {
            if (u != null && u.mySquad == null)
            {
                u.AttackMoveTo(enemyVisualCenter);
            }
        }

        // ⚡ 순수 ECS 자유 유닛들도 AttackMove
        // ⚡ 순수 ECS 자유 유닛들도 AttackMove
        if (selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    Unity.Entities.Entity ent = selectedECSEntities[i];
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitMovementData>(ent) && em.HasComponent<MiniTotalWar.ECS.UnitCombatData>(ent))
                    {
                        var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                        var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                        mov.TargetPosition = enemyVisualCenter;
                        float spd = (mov.MoveSpeed >= 2.0f) ? 2.8f : 1.2f;
                        mov.MoveSpeed = spd;
                        mov.Acceleration = (spd >= 2.0f) ? 8.0f : 4.5f;
                        combat.CurrentState = (int)UnitCommandState.AttackMove;
                        combat.AutoAttackEnabled = 1;
                        combat.ChargeImpactReady = 1;

                        em.SetComponentData(ent, mov);
                        em.SetComponentData(ent, combat);
                    }
                }
            }
        }

        isAttackTargetingMode = false;
        Debug.Log($"[PlayerController] ⚔️ 적 부대({enemySquad.name})를 목표로 일제 정면 돌격 교전 명령 하달! (돌격 부대 수: {attackedSquads})");
    }

    /// <summary>
    /// 마우스 커서 위치에 있는 적군 부대 또는 적군 유닛을 정밀하게 검출합니다. (물리 콜라이더, 순수 ECS 픽셀 거리, 부대 중심점)
    /// </summary>
    private bool TryGetEnemyUnderMouse(out Squad targetSquad, out Vector3 targetPos)
    {
        targetSquad = null;
        targetPos = Vector3.zero;

        Vector2 mouseScreenPos = Input.mousePosition;

        // 0. EventSystem UI 레이캐스트로 머리 위 적 부대 아이콘(SquadIconUI) 직접 검출
        if (EventSystem.current != null)
        {
            var pointerData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current)
            {
                position = mouseScreenPos
            };
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            foreach (var r in results)
            {
                SquadIconUI iconUI = r.gameObject.GetComponentInParent<SquadIconUI>();
                if (iconUI != null)
                {
                    Squad s = iconUI.GetSquad();
                    if (s != null && !s.isPlayer && s.MemberCount > 0)
                    {
                        targetSquad = s;
                        targetPos = s.GetVisualCenter();
                        return true;
                    }
                }
            }
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 1. RaycastAll 물리 콜라이더 검사
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        foreach (var h in hits)
        {
            Unit u = h.collider.GetComponent<Unit>();
            if (u != null && !u.isPlayer)
            {
                if (u.mySquad != null)
                {
                    targetSquad = u.mySquad;
                    targetPos = targetSquad.GetVisualCenter();
                    return true;
                }
                else
                {
                    targetPos = u.transform.position;
                    return true;
                }
            }
        }

        // 2. 적 부대 방진 사각 영역(Bounding Rect) 정밀 검사
        if (BattleManager.Instance != null && GetMouseWorldPosition(out Vector3 worldHit))
        {
            var squads = BattleManager.Instance.GetAllSquads();
            foreach (Squad s in squads)
            {
                if (s == null || s.isPlayer || s.MemberCount <= 0) continue;

                Vector3 center = s.GetVisualCenter();
                Vector3 relPos = worldHit - center;
                relPos.y = 0f;

                // 적 부대의 로컬 X, Z 좌표로 회전 투영
                Vector3 right = s.transform.right;
                Vector3 forward = s.transform.forward;
                float localX = Vector3.Dot(relPos, right);
                float localZ = Vector3.Dot(relPos, forward);

                int cols = Mathf.Max(1, s.currentColumns);
                int rows = Mathf.Max(1, Mathf.CeilToInt((float)s.MemberCount / cols));
                float halfWidth = (cols * s.spacing * 0.5f) + 0.5f; // 0.5m 정밀 반경
                float halfDepth = (rows * s.spacing * 0.5f) + 0.5f;

                if (Mathf.Abs(localX) <= halfWidth && Mathf.Abs(localZ) <= halfDepth)
                {
                    targetSquad = s;
                    targetPos = center;
                    return true;
                }
            }
        }

        // 3. 순수 ECS 엔티티 2D Screen-Space 픽셀 정밀 거리(25px) 검사 (적군: Faction == 0)
        var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                typeof(MiniTotalWar.ECS.UnitEntityTag),
                typeof(MiniTotalWar.ECS.UnitMovementData)
            );

            using (var tags = query.ToComponentDataArray<MiniTotalWar.ECS.UnitEntityTag>(Unity.Collections.Allocator.Temp))
            using (var movs = query.ToComponentDataArray<MiniTotalWar.ECS.UnitMovementData>(Unity.Collections.Allocator.Temp))
            {
                float minScreenDist = 25f; // 25px 정밀 피킹
                int bestIdx = -1;

                for (int i = 0; i < tags.Length; i++)
                {
                    if (tags[i].Faction == 0 && tags[i].IsAlive == 1) // 적군 유닛
                    {
                        Vector3 screenPt = mainCamera.WorldToScreenPoint((Vector3)movs[i].Position);
                        if (screenPt.z > 0f)
                        {
                            float dPix = Vector2.Distance(mouseScreenPos, new Vector2(screenPt.x, screenPt.y));
                            if (dPix < minScreenDist)
                            {
                                minScreenDist = dPix;
                                bestIdx = i;
                            }
                        }
                    }
                }

                if (bestIdx >= 0)
                {
                    int squadId = tags[bestIdx].SquadId;
                    if (squadId != -1 && BattleManager.Instance != null)
                    {
                        var squads = BattleManager.Instance.GetAllSquads();
                        foreach (var s in squads)
                        {
                            if (s != null && s.GetInstanceID() == squadId && !s.isPlayer)
                            {
                                targetSquad = s;
                                targetPos = s.GetVisualCenter();
                                return true;
                            }
                        }
                    }
                    targetPos = movs[bestIdx].Position;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 특정 위치(적 유닛 위치 등)를 향해 선택된 부대 및 자유 유닛들에게 일제 돌격 공격을 하달합니다.
    /// </summary>
    public void CommandAttackTargetPosition(Vector3 targetWorldPos)
    {
        foreach (Squad mySquad in selectedSquads)
        {
            if (mySquad != null && mySquad.MemberCount > 0)
            {
                mySquad.ClearWaypoints();
                mySquad.CommandMoveWithFormation(targetWorldPos, mySquad.transform.rotation, mySquad.currentColumns, false, UnitCommandState.AttackMove);
            }
        }

        foreach (Unit u in selectedUnits)
        {
            if (u != null && u.mySquad == null)
            {
                u.AttackMoveTo(targetWorldPos);
            }
        }

        if (selectedECSEntities.Count > 0)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var em = world.EntityManager;
                for (int i = 0; i < selectedECSEntities.Count; i++)
                {
                    Unity.Entities.Entity ent = selectedECSEntities[i];
                    if (em.Exists(ent) && em.HasComponent<MiniTotalWar.ECS.UnitMovementData>(ent) && em.HasComponent<MiniTotalWar.ECS.UnitCombatData>(ent))
                    {
                        var mov = em.GetComponentData<MiniTotalWar.ECS.UnitMovementData>(ent);
                        var combat = em.GetComponentData<MiniTotalWar.ECS.UnitCombatData>(ent);

                        mov.TargetPosition = targetWorldPos;
                        float spd = (mov.MoveSpeed >= 2.0f) ? 2.8f : 1.2f;
                        mov.MoveSpeed = spd;
                        mov.Acceleration = (spd >= 2.0f) ? 8.0f : 4.5f;
                        combat.CurrentState = (int)UnitCommandState.AttackMove;
                        combat.AutoAttackEnabled = 1;
                        combat.ChargeImpactReady = 1;

                        em.SetComponentData(ent, mov);
                        em.SetComponentData(ent, combat);
                    }
                }
            }
        }

        isAttackTargetingMode = false;
        Debug.Log($"[PlayerController] ⚔️ 목표 위치({targetWorldPos:F1})를 향해 일제 돌격 공격 명령 하달 완료!");
    }

    #endregion
}