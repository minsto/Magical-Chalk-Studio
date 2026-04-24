using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Media;

namespace MagicalChalkStudio
{
    public static class StructureJson
    {
        public const double GridSize = 50;

        public static string Escape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        public static string BuildLegacy(string structureName, double sceneWidth, double sceneHeight, IReadOnlyList<PlacedBlock> blocks)
        {
            var parts = new List<string>();
            foreach (var r in blocks)
            {
                int startX = (int)(r.X / GridSize);
                int startZ = (int)(r.Y / GridSize);
                int w = (int)(r.Width / GridSize);
                int h = (int)(r.Height / GridSize);
                for (int dx = 0; dx < w; dx++)
                for (int dz = 0; dz < h; dz++)
                {
                    int bx = startX + dx;
                    int by = r.Layer;
                    int bz = startZ + dz;
                    parts.Add("{\"x\":" + bx + ",\"y\":" + by + ",\"z\":" + bz +
                              ",\"blockId\":\"" + Escape(r.BlockId) +
                              "\",\"blockStateString\":\"" + Escape(r.BlockState) + "\"}");
                }
            }

            int sizeX = (int)Math.Ceiling(sceneWidth / GridSize);
            int sizeZ = (int)Math.Ceiling(sceneHeight / GridSize);
            int maxLayer = blocks.Count == 0 ? 0 : blocks.Max(b => b.Layer);
            int sizeY = Math.Max(256, maxLayer + 1);

            return "{\"name\":\"" + Escape(structureName) + "\",\"sizeX\":" + sizeX + ",\"sizeY\":" + sizeY + ",\"sizeZ\":" + sizeZ +
                   ",\"blocks\":[" + string.Join(",", parts) + "]}";
        }

        public static string BuildCompact(string structureName, double sceneWidth, double sceneHeight, IReadOnlyList<PlacedBlock> blocks)
        {
            var paletteIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var palette = new List<string>();
            var positions = new List<int>();
            var states = new List<int>();

            foreach (var r in blocks)
            {
                string key = r.BlockId + "|" + r.BlockState;
                if (!paletteIndex.TryGetValue(key, out int index))
                {
                    index = palette.Count;
                    paletteIndex[key] = index;
                    palette.Add(key);
                }

                int startX = (int)(r.X / GridSize);
                int startZ = (int)(r.Y / GridSize);
                int w = (int)(r.Width / GridSize);
                int h = (int)(r.Height / GridSize);
                for (int dx = 0; dx < w; dx++)
                for (int dz = 0; dz < h; dz++)
                {
                    positions.Add(startX + dx);
                    positions.Add(r.Layer);
                    positions.Add(startZ + dz);
                    states.Add(index);
                }
            }

            int sizeX = (int)Math.Ceiling(sceneWidth / GridSize);
            int sizeZ = (int)Math.Ceiling(sceneHeight / GridSize);
            int maxLayer = blocks.Count == 0 ? 0 : blocks.Max(b => b.Layer);
            int sizeY = Math.Max(256, maxLayer + 1);

            var sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(Escape(structureName)).Append("\",\"sizeX\":").Append(sizeX)
                .Append(",\"sizeY\":").Append(sizeY).Append(",\"sizeZ\":").Append(sizeZ)
                .Append(",\"format\":2,\"palette\":[");
            for (int i = 0; i < palette.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(palette[i])).Append('"');
            }
            sb.Append("],\"positions\":[").Append(string.Join(",", positions)).Append("],\"states\":[")
                .Append(string.Join(",", states)).Append("]}");
            return sb.ToString();
        }

        public static string BuildWrapper(string structureName, double sceneWidth, double sceneHeight, IReadOnlyList<PlacedBlock> blocks)
        {
            string inner = BuildCompact(structureName, sceneWidth, sceneHeight, blocks);
            string key = structureName.ToLowerInvariant();
            return "{\"structures\":{\"" + Escape(key) + "\":" + inner + "}}";
        }

        public static List<PlacedBlock> TryLoadLegacy(string json, out string? name, out double sceneW, out double sceneH)
        {
            name = null;
            sceneW = 50_000_000;
            sceneH = 50_000_000;
            var list = new List<PlacedBlock>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var n)) name = n.GetString();
                if (root.TryGetProperty("sizeX", out var sx)) sceneW = Math.Max(GridSize, sx.GetInt32() * GridSize);
                if (root.TryGetProperty("sizeZ", out var sz)) sceneH = Math.Max(GridSize, sz.GetInt32() * GridSize);
                if (!root.TryGetProperty("blocks", out var blocksEl) || blocksEl.ValueKind != JsonValueKind.Array)
                    return list;

                var cellMap = new Dictionary<(int x, int y, int z), (string id, string st)>();
                foreach (var el in blocksEl.EnumerateArray())
                {
                    int x = el.GetProperty("x").GetInt32();
                    int y = el.GetProperty("y").GetInt32();
                    int z = el.GetProperty("z").GetInt32();
                    string bid = el.TryGetProperty("blockId", out var bidEl) ? bidEl.GetString() ?? "minecraft:stone" : "minecraft:stone";
                    string st = el.TryGetProperty("blockStateString", out var stEl) ? stEl.GetString() ?? "" : "";
                    cellMap[(x, y, z)] = (bid, st);
                }

                foreach (var kv in cellMap)
                {
                    list.Add(new PlacedBlock
                    {
                        X = kv.Key.x * GridSize,
                        Y = kv.Key.z * GridSize,
                        Width = GridSize,
                        Height = GridSize,
                        Layer = kv.Key.y,
                        BlockId = kv.Value.id,
                        BlockState = kv.Value.st,
                        Fill = ColorFromBlockId(kv.Value.id)
                    });
                }
            }
            catch
            {
                // laisser list vide
            }
            return list;
        }

        private static Color ColorFromBlockId(string id)
        {
            int h = id?.GetHashCode() ?? 0;
            unchecked
            {
                byte r = (byte)(128 + (h & 0x3F));
                byte g = (byte)(128 + ((h >> 6) & 0x3F));
                byte b = (byte)(128 + ((h >> 12) & 0x3F));
                return Color.FromRgb(r, g, b);
            }
        }

        public static void WriteFileAtomic(string path, string contents)
        {
            string dir = System.IO.Path.GetDirectoryName(path) ?? ".";
            string tmp = System.IO.Path.Combine(dir, System.IO.Path.GetRandomFileName());
            System.IO.File.WriteAllText(tmp, contents, new UTF8Encoding(false));
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            System.IO.File.Move(tmp, path);
        }
    }
}
