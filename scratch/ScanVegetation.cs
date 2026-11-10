using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class ScanVegetation
{
    class PackEntry
    {
        public string Path;
        public int Size;
        public byte Compressed;
        public long Offset;
    }

    static void Main()
    {
        string twDir = @"C:\Program Files (x86)\Steam\steamapps\common\Total War THREE KINGDOMS\data";
        string[] targetPacks = new[] { "vegetation.pack", "models.pack", "models2.pack", "terrain.pack", "data.pack" };

        foreach (var packName in targetPacks)
        {
            string packPath = Path.Combine(twDir, packName);
            if (!File.Exists(packPath)) continue;

            Console.WriteLine($"=== Scanning {packName} ===");
            using (var fs = File.OpenRead(packPath))
            using (var br = new BinaryReader(fs))
            {
                byte[] magic = br.ReadBytes(4); // PFH5
                br.ReadBytes(12);
                int fileCount = br.ReadInt32();
                int indexSize = br.ReadInt32();
                br.ReadInt32();

                List<PackEntry> entries = new List<PackEntry>();
                for (int i = 0; i < fileCount; i++)
                {
                    int size = br.ReadInt32();
                    byte comp = br.ReadByte();
                    List<byte> strBytes = new List<byte>();
                    byte b;
                    while ((b = br.ReadByte()) != 0) strBytes.Add(b);
                    string path = Encoding.UTF8.GetString(strBytes.ToArray());
                    entries.Add(new PackEntry { Path = path, Size = size, Compressed = comp });
                }

                // Search for shrubs, grass, plants, weeds, crops, etc.
                int modelCount = 0;
                foreach (var e in entries)
                {
                    string pLower = e.Path.ToLowerInvariant();
                    if (pLower.Contains("rigid_model_v2") || pLower.Contains(".variantmeshdefinition") || pLower.Contains(".wsmodel"))
                    {
                        if (pLower.Contains("shrub") || pLower.Contains("grass") || pLower.Contains("bush") || 
                            pLower.Contains("fern") || pLower.Contains("reed") || pLower.Contains("crop") ||
                            pLower.Contains("flora") || pLower.Contains("weed") || pLower.Contains("flower") ||
                            pLower.Contains("plant") || pLower.Contains("stump") || pLower.Contains("bamboo"))
                        {
                            modelCount++;
                            if (modelCount <= 50)
                            {
                                Console.WriteLine($"[{packName}] {e.Path} ({e.Size} bytes)");
                            }
                        }
                    }
                }
                Console.WriteLine($"Total matching models in {packName}: {modelCount}");
            }
        }
    }
}
