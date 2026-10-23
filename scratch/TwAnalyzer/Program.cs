using System;
using System.IO;
using System.Globalization;

class CheckLeavesY
{
    static void Main()
    {
        string[] testModels = new[] { "Pine_A.obj", "Huangshan_Pine_A.obj", "Pine_Broken_H.obj", "Pine_Broken_K.obj" };
        string baseDir = @"c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Trees";

        foreach (var m in testModels)
        {
            string path = Path.Combine(baseDir, m);
            if (!File.Exists(path)) continue;

            float minY = float.MaxValue, maxY = float.MinValue;
            bool inLeaves = false;
            int leafCount = 0;

            // In OBJ files, let's see how groups/materials are organized
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("usemtl ") || line.StartsWith("g ") || line.StartsWith("o "))
                {
                    if (line.Contains("leave", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("leaf", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("sub1", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("subobject 1", StringComparison.OrdinalIgnoreCase))
                    {
                        inLeaves = true;
                    }
                }
            }

            // Let's inspect the entire vertex distribution
            int totalV = 0;
            float sumY = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("v "))
                {
                    var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    {
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        sumY += y;
                        totalV++;
                    }
                }
            }

            Console.WriteLine($"{m}: Total Vertices: {totalV}, MinY: {minY:F2}, MaxY: {maxY:F2}, AvgY: {sumY/totalV:F2}");
        }
    }
}
