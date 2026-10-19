using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program
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
        string packPath = @"C:\Program Files (x86)\Steam\steamapps\common\Total War THREE KINGDOMS\data\data.pack";
        using (var fs = File.OpenRead(packPath))
        using (var br = new BinaryReader(fs))
        {
            br.ReadBytes(16);
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

            long currentOffset = fs.Position;
            foreach (var e in entries)
            {
                e.Offset = currentOffset;
                currentOffset += e.Size;
            }

            string outDir = @"c:\unityProject\MiniTotalWar2D\scratch\extracted_tw_vfx";
            foreach (var e in entries)
            {
                if (e.Path.Equals(@"vfx\textures\3k_anim_fire_h_diffuse.dds", StringComparison.OrdinalIgnoreCase))
                {
                    fs.Seek(e.Offset, SeekOrigin.Begin);
                    byte[] data = br.ReadBytes(e.Size);
                    string outPath = Path.Combine(outDir, "3k_anim_fire_H_diffuse.dds");
                    File.WriteAllBytes(outPath, data);
                    Console.WriteLine("Extracted 3k_anim_fire_H_diffuse.dds: " + data.Length + " bytes");
                }
            }
        }
    }
}
