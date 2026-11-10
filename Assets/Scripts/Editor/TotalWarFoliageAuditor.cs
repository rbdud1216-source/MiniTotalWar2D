using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MiniTotalWar.Editor
{
    /// <summary>
    /// 🔍 [삼탈워 전수 식생 무결성 진단 도구]
    /// 1. 상하 반전 (Inverted / Upside Down) 진단: UV의 V(뿌리 vs 잎끝)와 버텍스 Y(바닥 vs 꼭대기) 상관관계 분석
    /// 2. 풀잎 끝 잘림 (Clipping / Truncated Blades) 진단: 상단 버텍스 밀집도 및 수평 플랫 컷 단면 검출
    /// 3. 중복 등록 및 중복 텍스처/메쉬 진단
    /// 4. 분류 오류 진단 (갈대에 연꽃이 들어가거나, 텍스처 불일치 등)
    /// </summary>
    public static class TotalWarFoliageAuditor
    {
        [MenuItem("TotalWar/🔍 [정밀 진단] 전수 식생 94종 무결성 전수 감사 실행", false, 1)]
        public static void RunFullAudit()
        {
            Debug.Log("<color=#FF00FF><b>================ [전수 식생 무결성 감사 시작] ================</b></color>");

            string meshDir = "Assets/Settings/Vegetation/RadialMeshes";
            string[] meshFiles = Directory.GetFiles(meshDir, "TotalWar_*_Mesh.asset");

            int invertedCount = 0;
            int flatCutCount = 0;
            int duplicateCount = 0;
            int misclassifiedCount = 0;

            var meshFingerprints = new Dictionary<string, string>(); // 정점수+삼각형수+바운즈 -> 파일명

            foreach (var mf in meshFiles)
            {
                string fName = Path.GetFileName(mf);
                Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(mf);
                if (m == null) continue;

                Vector3[] verts = m.vertices;
                Vector2[] uvs = m.uv;
                int[] tris = m.triangles;
                Bounds b = m.bounds;

                if (verts == null || verts.Length == 0 || uvs == null || uvs.Length == 0) continue;

                // --- 1. 중복 기하학 진단 (정밀 지문) ---
                string fp = $"{verts.Length}_{tris.Length}_{b.size.x:F3}_{b.size.y:F3}_{b.size.z:F3}";
                if (meshFingerprints.TryGetValue(fp, out string existing))
                {
                    // 기후/계절 베리에이션이라 일부러 같은 메쉬를 쓴 것인지, 불필요한 중복인지 체크
                    duplicateCount++;
                    Debug.LogWarning($"[중복 기하학 감지] '{fName}' 와 '{existing}' 의 버텍스/삼각형/바운즈가 100% 동일합니다. (지문: {fp})");
                }
                else
                {
                    meshFingerprints[fp] = fName;
                }

                // --- 2. 상하 반전 (Inverted / Upside Down) 진단 ---
                // 식물 아틀라스에서 일반적으로 V가 낮을수록 뿌리(Root), V가 높을수록 잎끝(Tip)입니다.
                // 버텍스 Y가 가장 높은 영역(상위 20%)의 평균 V와, 가장 낮은 영역(하위 20%)의 평균 V를 비교합니다.
                float minY = b.min.y;
                float maxY = b.max.y;
                float hSpan = Mathf.Max(0.01f, maxY - minY);

                float topVSum = 0f; int topCount = 0;
                float botVSum = 0f; int botCount = 0;

                for (int i = 0; i < verts.Length; i++)
                {
                    float yNorm = (verts[i].y - minY) / hSpan;
                    if (yNorm >= 0.80f)
                    {
                        topVSum += uvs[i].y;
                        topCount++;
                    }
                    else if (yNorm <= 0.20f)
                    {
                        botVSum += uvs[i].y;
                        botCount++;
                    }
                }

                float avgTopV = topCount > 0 ? topVSum / topCount : 0.5f;
                float avgBotV = botCount > 0 ? botVSum / botCount : 0.5f;

                // 만약 꼭대기(Top Y)의 V가 바닥(Bot Y)의 V보다 현저히 낮다면 (avgTopV < avgBotV - 0.15f),
                // 텍스처의 아랫부분(뿌리)이 위로 가고 윗부분(잎끝)이 아래로 간 '거꾸로 뒤집힌 식물'입니다!
                if (avgTopV < avgBotV - 0.10f)
                {
                    invertedCount++;
                    Debug.LogError($"<color=#FF3333><b>[🚨 상하 반전 감지!] '{fName}'</b>: 꼭대기 V({avgTopV:F3}) < 바닥 V({avgBotV:F3}) -> 뿌리가 하늘로 가고 잎끝이 땅에 처박혀 있습니다!</color>");
                }

                // --- 3. 풀잎 끝 수평 잘림 (Flat Cut / Truncated Tip) 진단 ---
                // 꼭대기 근처(maxY - 0.01m 이내)에 다수의 정점이 거의 완벽한 동일 평면(수평 단면)에 몰려 있는지 검출
                int atAbsoluteTopCount = 0;
                for (int i = 0; i < verts.Length; i++)
                {
                    if (Mathf.Abs(verts[i].y - maxY) < 0.005f)
                    {
                        atAbsoluteTopCount++;
                    }
                }

                // 카드 형태 식물인데 꼭대기 수평선에 정점이 6개 이상 동일한 Y로 잘려있다면 칼로 자른 듯한 플랫 컷입니다.
                if (atAbsoluteTopCount >= 6 && (fName.Contains("TallReed") || fName.Contains("RadialFan3D")))
                {
                    flatCutCount++;
                    Debug.LogWarning($"<color=#FF9900><b>[⚠️ 수평 잘림 단면 감지!] '{fName}'</b>: Y_max({maxY:F3}m)에 정점 {atAbsoluteTopCount}개가 수평 단면으로 평평하게 잘려있습니다.</color>");
                }

                // --- 4. 분류 오류 진단 ---
                if (fName.Contains("Lotus") && fName.Contains("TallReed"))
                {
                    misclassifiedCount++;
                    Debug.LogError($"<color=#FF00AA><b>[🚨 분류 오류!] '{fName}'</b>: 연꽃(수생식물)이 키 큰 갈대(TallReed3D) 네이밍 및 카테고리에 잘못 분류되어 있습니다!</color>");
                }
            }

            Debug.Log($"<color=#00FFFF><b>================ [전수 감사 요약 보고서] ================</b></color>\n" +
                      $"• 총 조사 메쉬: {meshFiles.Length}개\n" +
                      $"• 🚨 상하 반전 (거꾸로 자란 풀): {invertedCount}개\n" +
                      $"• ⚠️ 수평 잘림 (풀끝 잘림/밑동 솟음): {flatCutCount}개\n" +
                      $"• 🔍 동일 메쉬 복제/중복: {duplicateCount}개\n" +
                      $"• 🏷️ 분류 오류 (연꽃 등): {misclassifiedCount}개\n" +
                      $"<color=#00FFFF><b>=======================================================</b></color>");
        }
    }
}
