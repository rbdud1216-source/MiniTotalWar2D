using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string packPath = @"C:\Program Files (x86)\Steam\steamapps\common\Total War THREE KINGDOMS\data\vegetation.pack";
        using (var fs = File.OpenRead(packPath))
        using (var br = new BinaryReader(fs))
        {
            br.ReadBytes(16);
            int fileCount = br.ReadInt32();
            int indexSize = br.ReadInt32();
            br.ReadInt32();

            for (int i = 0; i < fileCount; i++)
            {
                int size = br.ReadInt32();
                byte comp = br.ReadByte();
                List<byte> strBytes = new List<byte>();
                byte b;
                while ((b = br.ReadByte()) != 0) strBytes.Add(b);
                string path = Encoding.UTF8.GetString(strBytes.ToArray());

                string pathLower = path.ToLower();
                if (pathLower.Contains("snow") || pathLower.Contains("winter") || pathLower.Contains("_wi_") || pathLower.Contains("frost"))
                {
                    Console.WriteLine($"{path} ({size} bytes)");
                }
            }
        }
    }
}
