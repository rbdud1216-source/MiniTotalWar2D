using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MiniTotalWar.ECS
{
    /// <summary>
    /// GameObject를 0개로 만들고, 수만 개의 Entity를 단 1~3개의 Draw Call로 일괄 렌더링하는 순수 ECS GPU Instanced 렌더러
    /// 선택된 부대의 유닛은 밝은 Cyan(형광 청록색)으로 즉각 동적 렌더링합니다.
    /// </summary>
    public class PureECSRenderer : MonoBehaviour
    {
        public static PureECSRenderer Instance { get; private set; }

        [Header("렌더링 메시 및 머티리얼")]
        [SerializeField] private Mesh unitMesh;
        [SerializeField] private Mesh missileMesh; // 🎯 신규 궁병 전용 구형(Sphere) 메쉬
        [SerializeField] private Material playerMaterial;
        [SerializeField] private Material playerSelectedMaterial;
        [SerializeField] private Material enemyMaterial;

        [Header("유닛 크기")]
        public Vector3 unitScale = new Vector3(0.78f, 0.78f, 0.78f);

        private EntityManager entityManager;
        private EntityQuery unitQuery;
        private bool isInitialized = false;

        // 보병(Cube) 버퍼
        private Matrix4x4[] playerMatrices = new Matrix4x4[4096];
        private Matrix4x4[] playerSelectedMatrices = new Matrix4x4[4096];
        private Matrix4x4[] enemyMatrices = new Matrix4x4[4096];

        // 궁병(Sphere) 버퍼
        private Matrix4x4[] playerArcherMatrices = new Matrix4x4[4096];
        private Matrix4x4[] playerSelectedArcherMatrices = new Matrix4x4[4096];
        private Matrix4x4[] enemyArcherMatrices = new Matrix4x4[4096];

        private readonly Matrix4x4[] sharedBatchBuffer = new Matrix4x4[1023];
        private MaterialPropertyBlock propBlock;

        private readonly HashSet<int> selectedSquadIds = new HashSet<int>();

        private void Awake()
        {
            if (Instance == null) Instance = this;

            if (unitMesh == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                unitMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Destroy(cube);
            }

            if (missileMesh == null)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                missileMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
                Destroy(sphere);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Sprites/Default");

            if (playerMaterial == null)
            {
                playerMaterial = new Material(shader);
                if (playerMaterial.HasProperty("_BaseColor")) playerMaterial.SetColor("_BaseColor", new Color(0.12f, 0.50f, 0.95f));
                playerMaterial.color = new Color(0.12f, 0.50f, 0.95f);
                playerMaterial.enableInstancing = true;
            }

            if (playerSelectedMaterial == null)
            {
                playerSelectedMaterial = new Material(shader);
                if (playerSelectedMaterial.HasProperty("_BaseColor")) playerSelectedMaterial.SetColor("_BaseColor", new Color(0.15f, 0.92f, 1.0f));
                playerSelectedMaterial.color = new Color(0.15f, 0.92f, 1.0f);
                playerSelectedMaterial.enableInstancing = true;
            }

            if (enemyMaterial == null)
            {
                enemyMaterial = new Material(shader);
                if (enemyMaterial.HasProperty("_BaseColor")) enemyMaterial.SetColor("_BaseColor", new Color(0.95f, 0.22f, 0.22f));
                enemyMaterial.color = new Color(0.95f, 0.22f, 0.22f);
                enemyMaterial.enableInstancing = true;
            }

            propBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                entityManager = world.EntityManager;
                unitQuery = entityManager.CreateEntityQuery(
                    typeof(UnitEntityTag),
                    typeof(UnitMovementData)
                );
                isInitialized = true;
            }
        }

        private void LateUpdate()
        {
            if (!isInitialized || unitQuery.IsEmpty) return;
            RenderAllEntities();
        }

        private void RenderAllEntities()
        {
            if (!isInitialized) return;

            // 현재 선택된 부대들의 Squad ID 및 개별 선택된 Entity 수집
            selectedSquadIds.Clear();
            HashSet<Entity> selectedEntitySet = null;

            if (PlayerController.Instance != null)
            {
                var selected = PlayerController.Instance.GetSelectedSquads();
                if (selected != null)
                {
                    foreach (var s in selected)
                    {
                        if (s != null) selectedSquadIds.Add(s.GetInstanceID());
                    }
                }

                if (PlayerController.Instance.SelectedECSEntities.Count > 0)
                {
                    selectedEntitySet = new HashSet<Entity>(PlayerController.Instance.SelectedECSEntities);
                }
            }

            bool hasSelectedEntities = (selectedEntitySet != null && selectedEntitySet.Count > 0);
            NativeArray<Entity> entities = default;
            if (hasSelectedEntities)
            {
                entities = unitQuery.ToEntityArray(Allocator.TempJob);
            }

            using (var tags = unitQuery.ToComponentDataArray<UnitEntityTag>(Allocator.TempJob))
            using (var movements = unitQuery.ToComponentDataArray<UnitMovementData>(Allocator.TempJob))
            {
                int totalEntities = tags.Length;
                if (totalEntities == 0)
                {
                    if (hasSelectedEntities && entities.IsCreated) entities.Dispose();
                    return;
                }

                int playerCount = 0;
                int playerSelectedCount = 0;
                int enemyCount = 0;

                int playerArcherCount = 0;
                int playerSelectedArcherCount = 0;
                int enemyArcherCount = 0;

                for (int i = 0; i < totalEntities; i++)
                {
                    if (tags[i].IsAlive == 0) continue;

                    float3 pos = movements[i].Position;
                    quaternion rot = movements[i].Rotation;
                    float lenSq = math.lengthsq(rot.value);
                    if (lenSq < 0.0001f || !math.all(math.isfinite(rot.value)))
                    {
                        rot = quaternion.identity;
                    }
                    else
                    {
                        rot = math.normalize(rot);
                    }

                    Matrix4x4 mat = Matrix4x4.TRS(pos, rot, unitScale);
                    bool isArcher = (tags[i].UnitType == 2);

                    if (tags[i].Faction == 1) // 아군
                    {
                        bool isSelected = selectedSquadIds.Contains(tags[i].SquadId) || (hasSelectedEntities && selectedEntitySet.Contains(entities[i]));
                        if (isSelected)
                        {
                            if (isArcher)
                            {
                                if (playerSelectedArcherCount >= playerSelectedArcherMatrices.Length)
                                    System.Array.Resize(ref playerSelectedArcherMatrices, playerSelectedArcherMatrices.Length * 2);
                                playerSelectedArcherMatrices[playerSelectedArcherCount++] = mat;
                            }
                            else
                            {
                                if (playerSelectedCount >= playerSelectedMatrices.Length)
                                    System.Array.Resize(ref playerSelectedMatrices, playerSelectedMatrices.Length * 2);
                                playerSelectedMatrices[playerSelectedCount++] = mat;
                            }
                        }
                        else
                        {
                            if (isArcher)
                            {
                                if (playerArcherCount >= playerArcherMatrices.Length)
                                    System.Array.Resize(ref playerArcherMatrices, playerArcherMatrices.Length * 2);
                                playerArcherMatrices[playerArcherCount++] = mat;
                            }
                            else
                            {
                                if (playerCount >= playerMatrices.Length)
                                    System.Array.Resize(ref playerMatrices, playerMatrices.Length * 2);
                                playerMatrices[playerCount++] = mat;
                            }
                        }
                    }
                    else // 적군
                    {
                        if (isArcher)
                        {
                            if (enemyArcherCount >= enemyArcherMatrices.Length)
                                System.Array.Resize(ref enemyArcherMatrices, enemyArcherMatrices.Length * 2);
                            enemyArcherMatrices[enemyArcherCount++] = mat;
                        }
                        else
                        {
                            if (enemyCount >= enemyMatrices.Length)
                                System.Array.Resize(ref enemyMatrices, enemyMatrices.Length * 2);
                            enemyMatrices[enemyCount++] = mat;
                        }
                    }
                }

                if (hasSelectedEntities && entities.IsCreated)
                {
                    entities.Dispose();
                }

                // 1. 보병 (Cube 직육면체) GPU Instancing 렌더링
                RenderBatches(unitMesh, playerMaterial, playerMatrices, playerCount);
                RenderBatches(unitMesh, playerSelectedMaterial, playerSelectedMatrices, playerSelectedCount);
                RenderBatches(unitMesh, enemyMaterial, enemyMatrices, enemyCount);

                // 2. 궁병 (Sphere 동그란 구형) GPU Instancing 렌더링
                RenderBatches(missileMesh, playerMaterial, playerArcherMatrices, playerArcherCount);
                RenderBatches(missileMesh, playerSelectedMaterial, playerSelectedArcherMatrices, playerSelectedArcherCount);
                RenderBatches(missileMesh, enemyMaterial, enemyArcherMatrices, enemyArcherCount);
            }
        }

        private void RenderBatches(Mesh targetMesh, Material mat, Matrix4x4[] matrices, int totalCount)
        {
            if (totalCount == 0 || mat == null || targetMesh == null) return;

            const int batchSize = 1023;
            int offset = 0;

            while (offset < totalCount)
            {
                int count = Mathf.Min(batchSize, totalCount - offset);
                System.Array.Copy(matrices, offset, sharedBatchBuffer, 0, count);

                Graphics.DrawMeshInstanced(
                    targetMesh,
                    0,
                    mat,
                    sharedBatchBuffer,
                    count,
                    propBlock,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: true
                );

                offset += count;
            }
        }
    }
}
