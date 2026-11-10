using UnityEngine;
using UnityEditor;

// [InitializeOnLoad] // 🚨 에디터 리로드 시마다 구버전 테스트 오브젝트 자동 소환 방지를 위해 비활성화
public static class TestSpawnSbtGrass
{
    // static TestSpawnSbtGrass()
    // {
    //     EditorApplication.delayCall += Execute;
    // }

    [MenuItem("TotalWar/🌸 [원작 1:1 꽃 잔디 3종 & 순수 잔디 원본] 다방향 군락 비교 소환", false, 0)]
    public static void Execute()
    {
        // 🌸 사진 1(분홍 꽃망울), 사진 2(연보라 꽃송이), 사진 3(주황 꽃이삭) 및 사진 4(순수 잔디 원본 보존) 다방향 군락 동시 소환
        MiniTotalWar.Editor.TotalWarProceduralFoliageDemo.SpawnFlowerAndPureGrassClustersDemo();

        Debug.Log("<color=green>[TotalWar] 🌸 원작 꽃 잔디 3종(분홍·연보라·주황) 및 순수 잔디 원본(사진 4) 다방향 군락이 소환되었습니다!</color>");
    }

    [MenuItem("TotalWar/🌿 [원작 1:1 순수 잔디 원본 보존] 꽃 없는 다방향 잔디 군락 단독 소환 (사진 4)", false, 1)]
    public static void ExecutePureGrassOnly()
    {
        // 🌾 사용자가 만족한 사진 4 원본 (꽃 완전 배제, 10개 열 다방향 교차 순수 잔디 군락) 단독 소환
        MiniTotalWar.Editor.TotalWarProceduralFoliageDemo.SpawnPure2DGrassSwathDemo();

        Debug.Log("<color=green>[TotalWar] 🌟 원작 순수 다방향 잔디 군락 (사진 4 원본)이 단독 소환되었습니다!</color>");
    }
}
