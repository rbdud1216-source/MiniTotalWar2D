using UnityEngine;
using UnityEditor;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🛠️ [삼국지 토탈워 전장 터레인 잔디 상태 진단기]
    /// </summary>
    public static class TotalWarTerrainDiagnoser
    {

        [MenuItem("TotalWar/🔍 터레인 잔디 상태 정밀 진단", false, 99)]
        public static void Diagnose()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) terrain = Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("[진단] Active Terrain이 없습니다!");
                return;
            }

            TerrainData tData = terrain.terrainData;
            Debug.Log($"<color=cyan>[진단] 터레인: {terrain.name}, 크기: {tData.size}, detailResolution: {tData.detailResolution}, scatterMode: {tData.detailScatterMode}</color>");

            DetailPrototype[] protos = tData.detailPrototypes;
            int protoCount = protos != null ? protos.Length : 0;
            Debug.Log($"<color=yellow>[진단] 등록된 DetailPrototype 개수: {protoCount}</color>");

            // 각 레이어별 실제 심어진 잔디 개수 및 최대값 계산
            long totalGrassCount = 0;
            for (int layer = 0; layer < protoCount; layer++)
            {
                int[,] map = tData.GetDetailLayer(0, 0, tData.detailResolution, tData.detailResolution, layer);
                long layerCount = 0;
                int maxVal = 0;
                int nonZeroCount = 0;

                for (int y = 0; y < tData.detailResolution; y++)
                {
                    for (int x = 0; x < tData.detailResolution; x++)
                    {
                        int v = map[y, x];
                        layerCount += v;
                        if (v > maxVal) maxVal = v;
                        if (v > 0) nonZeroCount++;
                    }
                }
                Debug.Log($"<color=lime>[진단] Layer #{layer} ({protos[layer].prototypeTexture?.name}) 총합: {layerCount:N0}, 최대값: {maxVal}, 칠해진 픽셀수: {nonZeroCount:N0}</color>");
                totalGrassCount += layerCount;
            }

            Debug.Log($"<color=white><b>[진단] 모든 레이어에 심어진 총 잔디 밀도 합계: {totalGrassCount:N0}</b></color>");
        }
    }
}
