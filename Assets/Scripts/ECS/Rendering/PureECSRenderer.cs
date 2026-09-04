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
        [SerializeField] private Material playerMaterial;
        [SerializeField] private Material playerSelectedMaterial;
        [SerializeField] private Material enemyMaterial;

        [Header("유닛 크기")]
        public Vector3 unitScale = new Vector3(0.78f, 0.78f, 0.78f);

        private EntityManager entityManager;
        private EntityQuery unitQuery;
        private bool isInitialized = false;

        private Matrix4x4[] playerMatrices = new Matrix4x4[1023];
        private Matrix4x4[] playerSelectedMatrices = new Matrix4x4[1023];
        private Matrix4x4[] enemyMatrices = new Matrix4x4[1023];
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

            using (var entities = unitQuery.ToEntityArray(Allocator.TempJob))
            using (var tags = unitQuery.ToComponentDataArray<UnitEntityTag>(Allocator.TempJob))
            using (var movements = unitQuery.ToComponentDataArray<UnitMovementData>(Allocator.TempJob))
            {
                int totalEntities = tags.Length;
                if (totalEntities == 0) return;

                int playerCount = 0;
                int playerSelectedCount = 0;
                int enemyCount = 0;

                for (int i = 0; i < totalEntities; i++)
                {
                    if (tags[i].IsAlive == 0) continue;

                    float3 pos = movements[i].Position;
                    quaternion rot = movements[i].Rotation;

                    Matrix4x4 mat = Matrix4x4.TRS(pos, rot, unitScale);

                    if (tags[i].Faction == 1) // 아군
                    {
                        bool isSelected = selectedSquadIds.Contains(tags[i].SquadId) || (selectedEntitySet != null && selectedEntitySet.Contains(entities[i]));
                        if (isSelected)
                        {
                            if (playerSelectedCount >= playerSelectedMatrices.Length)
                            {
                                System.Array.Resize(ref playerSelectedMatrices, playerSelectedMatrices.Length * 2);
                            }
                            playerSelectedMatrices[playerSelectedCount++] = mat;
                        }
                        else
                        {
                            if (playerCount >= playerMatrices.Length)
                            {
                                System.Array.Resize(ref playerMatrices, playerMatrices.Length * 2);
                            }
                            playerMatrices[playerCount++] = mat;
                        }
                    }
                    else // 적군
                    {
                        if (enemyCount >= enemyMatrices.Length)
                        {
                            System.Array.Resize(ref enemyMatrices, enemyMatrices.Length * 2);
                        }
                        enemyMatrices[enemyCount++] = mat;
                    }
                }

                // GPU Instancing으로 1023개 단위 일괄 렌더링 (Draw Call 1~3개로 압축!)
                RenderBatches(playerMaterial, playerMatrices, playerCount);
                RenderBatches(playerSelectedMaterial, playerSelectedMatrices, playerSelectedCount);
                RenderBatches(enemyMaterial, enemyMatrices, enemyCount);
            }
        }

        private void RenderBatches(Material mat, Matrix4x4[] matrices, int totalCount)
        {
            if (totalCount == 0 || mat == null || unitMesh == null) return;

            int batchSize = 1023;
            int offset = 0;

            Matrix4x4[] batch = new Matrix4x4[batchSize];

            while (offset < totalCount)
            {
                int count = Mathf.Min(batchSize, totalCount - offset);
                System.Array.Copy(matrices, offset, batch, 0, count);

                Graphics.DrawMeshInstanced(
                    unitMesh,
                    0,
                    mat,
                    batch,
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
