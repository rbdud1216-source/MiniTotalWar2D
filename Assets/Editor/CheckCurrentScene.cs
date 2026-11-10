using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class CheckCurrentScene
{
    [MenuItem("Tools/MiniTotalWar/Check Scene")]
    public static void Check()
    {
        var scene = EditorSceneManager.GetActiveScene();
        Debug.Log($"[CheckScene] Active Scene: {scene.name}, Path: {scene.path}, IsLoaded: {scene.isLoaded}");
        var terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Debug.Log($"[CheckScene] Active Terrain: {terrain.name}, Size: {terrain.terrainData.size}");
            var renderer = terrain.GetComponent<MiniTotalWar.TacticalGrassRenderer>();
            if (renderer != null)
            {
                Debug.Log($"[CheckScene] TacticalGrassRenderer found! Prototypes: {renderer.prototypes?.Count}, Chunks: {renderer.ActiveChunkCount}");
            }
        }
    }
}

