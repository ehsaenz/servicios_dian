using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace eFacturacionColombia_NETCore.Util
{
    public static class ZipHelper
    {
        public static byte[] Compress(byte[] data, string entryName)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry(entryName);
                    using (var entryStream = entry.Open())
                    {
                        using (var dataStream = new MemoryStream(data))
                        {
                            dataStream.CopyTo(entryStream);
                        }
                    }
                }

                return memoryStream.ToArray();
            }
        }

        public static byte[] Compress(Dictionary<string, byte[]> files)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in files)
                    {
                        var entry = archive.CreateEntry(file.Key);
                        using (var entryStream = entry.Open())
                        {
                            using (var dataStream = new MemoryStream(file.Value))
                            {
                                dataStream.CopyTo(entryStream);
                            }
                        }
                    }
                }

                return memoryStream.ToArray();
            }
        }
    }
}
