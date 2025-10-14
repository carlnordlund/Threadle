using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Utilities
{
    public static class CompressedJsonSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy= JsonNamingPolicy.CamelCase,
            IncludeFields=true
        };

        public static void SaveToFile<T>(T obj, string filepath)
        {
            using var fileStream = File.Create(filepath);
            using var gzipStream = new GZipStream(fileStream,CompressionLevel.Optimal);
            JsonSerializer.Serialize(gzipStream, obj, Options);
        }

        public static T? LoadFromFile<T>(string filepath)
        {
            using var fileStream = File.OpenRead(filepath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<T>(gzipStream, Options);
        }
    }
}
