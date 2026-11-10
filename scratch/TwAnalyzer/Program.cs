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
        string packPath = Path.Combine(twDir, "terrain.pack");

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

            // Inspect some grass models
            foreach (var e in entries)
            {
                if (e.Path.Contains(@"terrain\vegetation\battle\grass\ard_grass0_1.rigid_model_v2"))
                {
                    fs.Seek(e.Offset, SeekOrigin.Begin);
                    byte[] data = br.ReadBytes(e.Size);
                    string magic = Encoding.ASCII.GetString(data, 0, 4);
                    ushort lodCount = BitConverter.ToUInt16(data, 8);
                    string skeleton = Encoding.UTF8.GetString(data, 12, 128).Trim('\0');
                    Console.WriteLine($"Grass Model: {e.Path}");
                    Console.WriteLine($"Magic: {magic}, Size: {e.Size}, LOD Count: {lodCount}, Skeleton: '{skeleton}'");

                    int lod0MeshCount = BitConverter.ToInt32(data, 140);
                    Console.WriteLine($"LOD0 Mesh Count: {lod0MeshCount}");

                    int cur = 140 + 28 * lodCount;
                    ushort matType = BitConverter.ToUInt16(data, cur);
                    int sectionSize = BitConverter.ToInt32(data, cur + 4);
                    int vOffset = BitConverter.ToInt32(data, cur + 8);
                    int vCount = BitConverter.ToInt32(data, cur + 12);
                    int iOffset = BitConverter.ToInt32(data, cur + 16);
                    int iCount = BitConverter.ToInt32(data, cur + 20);
                    Console.WriteLine($"MatType: {matType}, VCount: {vCount}, ICount: {iCount}");
                    break;
                }
            }
        }
    }

}

