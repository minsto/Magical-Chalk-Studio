using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MagicalChalkStudio
{
    /// <summary>Parse / génère des états Minecraft (comme main.lua : Block{id}[facing=…]) + rotation + glyphe.</summary>
    public static class BlockStateHelper
    {
        private static readonly Regex RBlock = new(
            @"Block\{([^}]*)\}(?:\s*\[([^\]]*)\])?\s*",
            RegexOptions.CultureInvariant);

        public enum PresetType { None, Stairs, Log, AxisBlock, Piston, Door, RedstoneWire, Furnace, Chest }

        public static PresetType GetPresetType(string? blockId)
        {
            if (string.IsNullOrEmpty(blockId)) return PresetType.None;
            if (blockId.Contains("_stairs", StringComparison.Ordinal)) return PresetType.Stairs;
            if (blockId.Contains("_log", StringComparison.Ordinal) || blockId.Contains("stem", StringComparison.Ordinal) || blockId.Contains("hyphae", StringComparison.Ordinal))
                return PresetType.Log;
            if (IsAxisPillarBlock(blockId))
                return PresetType.AxisBlock;
            if (blockId is "minecraft:piston" or "minecraft:sticky_piston") return PresetType.Piston;
            if (blockId.Contains("_door", StringComparison.Ordinal)) return PresetType.Door;
            if (blockId is "minecraft:redstone_wire") return PresetType.RedstoneWire;
            if (blockId is "minecraft:furnace" or "minecraft:smoker" or "minecraft:blast_furnace") return PresetType.Furnace;
            if (blockId is "minecraft:chest" or "minecraft:trapped_chest" or "minecraft:ender_chest" or "minecraft:barrel") return PresetType.Chest;
            return PresetType.None;
        }

        public static Dictionary<string, string> ParseToProps(string? blockId, string? stateString)
        {
            var props = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(stateString)) return props;
            var m = RBlock.Match(stateString.Trim());
            if (!m.Success) return TryParseLooseKeyVal(stateString, props);
            string? inside = m.Groups.Count > 2 ? m.Groups[2].Value : null;
            if (string.IsNullOrEmpty(inside)) return props;
            foreach (string part in inside.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = part.IndexOf('=');
                if (eq > 0)
                    props[part[..eq].Trim()] = part[(eq + 1)..].Trim();
            }
            return props;
        }

        private static Dictionary<string, string> TryParseLooseKeyVal(string s, Dictionary<string, string> into)
        {
            foreach (string part in s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = part.IndexOf('=');
                if (eq > 0)
                    into[part[..eq].Trim()] = part[(eq + 1)..].Trim();
            }
            return into;
        }

        /// <summary>Bûches / piliers (<c>axis</c>) + escaliers (<c>facing</c>/<c>half</c> via les mêmes boutons).</summary>
        public static bool SupportsAxis(string? blockId) =>
            GetPresetType(blockId) is PresetType.Log or PresetType.AxisBlock or PresetType.Stairs;

        private static bool IsAxisPillarBlock(string blockId) => blockId is
            "minecraft:basalt" or "minecraft:polished_basalt" or
            "minecraft:quartz_pillar" or "minecraft:purpur_pillar" or
            "minecraft:hay_block" or "minecraft:bone_block" or
            "minecraft:bamboo_block";

        public static string BuildState(string? blockId, IReadOnlyDictionary<string, string> props)
        {
            blockId ??= "minecraft:air";
            var t = GetPresetType(blockId);
            if (t == PresetType.None)
                return "Block{" + blockId + "}";

            var keys = t switch
            {
                PresetType.Stairs => new[] { "facing", "half", "shape", "waterlogged" },
                PresetType.Log or PresetType.AxisBlock => new[] { "axis" },
                PresetType.Piston => new[] { "facing", "extended" },
                PresetType.Door => new[] { "facing", "half", "hinge", "open", "powered" },
                PresetType.RedstoneWire => new[] { "east", "north", "south", "west", "power" },
                PresetType.Furnace => new[] { "facing", "lit" },
                PresetType.Chest => new[] { "facing", "type", "waterlogged" },
                _ => Array.Empty<string>()
            };
            var defaults = GetDefaults(t);
            var parts = new List<string>();
            foreach (string k in keys)
            {
                string? v = props.TryGetValue(k, out var pv) ? pv : defaults.GetValueOrDefault(k);
                if (v != null) parts.Add(k + "=" + v);
            }
            if (parts.Count == 0) return "Block{" + blockId + "}";
            return "Block{" + blockId + "}[" + string.Join(",", parts) + "]";
        }

        public static string GetDefaultStateForBlockId(string? blockId)
        {
            var t = GetPresetType(blockId);
            if (t == PresetType.None) return "Block{" + (blockId ?? "minecraft:stone") + "}";
            return BuildState(blockId, GetDefaults(t));
        }

        /// <summary>Reconstruit <c>Block{id}[…]</c> pour <paramref name="newBlockId"/>, en reprenant les propriétés reconnues depuis l’ancienne chaîne (le nom dans <c>Block{…}</c> peut être obsolète).</summary>
        public static string RealignStateToBlockId(string? newBlockId, string? stateString)
        {
            if (string.IsNullOrWhiteSpace(newBlockId)) newBlockId = "minecraft:stone";
            newBlockId = newBlockId.Trim();
            if (string.IsNullOrWhiteSpace(stateString)) return GetDefaultStateForBlockId(newBlockId);
            var t = GetPresetType(newBlockId);
            var parsed = ParseToProps(newBlockId, stateString);
            if (t == PresetType.None) return "Block{" + newBlockId + "}";
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in GetDefaults(t)) d[kv.Key] = kv.Value;
            foreach (var kv in parsed) d[kv.Key] = kv.Value;
            return BuildState(newBlockId, d);
        }

        private static Dictionary<string, string> GetDefaults(PresetType t) => t switch
        {
            PresetType.Stairs => new Dictionary<string, string>(StringComparer.Ordinal) { ["facing"] = "north", ["half"] = "bottom", ["shape"] = "straight", ["waterlogged"] = "false" },
            PresetType.Log or PresetType.AxisBlock => new Dictionary<string, string>(StringComparer.Ordinal) { ["axis"] = "y" },
            PresetType.Piston => new Dictionary<string, string>(StringComparer.Ordinal) { ["facing"] = "north", ["extended"] = "false" },
            PresetType.Door => new Dictionary<string, string>(StringComparer.Ordinal) { ["facing"] = "north", ["half"] = "lower", ["hinge"] = "left", ["open"] = "false", ["powered"] = "false" },
            PresetType.RedstoneWire => new Dictionary<string, string>(StringComparer.Ordinal) { ["east"] = "none", ["north"] = "none", ["south"] = "none", ["west"] = "none", ["power"] = "0" },
            PresetType.Furnace => new Dictionary<string, string>(StringComparer.Ordinal) { ["facing"] = "north", ["lit"] = "false" },
            PresetType.Chest => new Dictionary<string, string>(StringComparer.Ordinal) { ["facing"] = "north", ["type"] = "single", ["waterlogged"] = "false" },
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
        };

        private static string? NextFacing4(string? v) => (v?.ToLowerInvariant()) switch
        {
            "north" => "east", "east" => "south", "south" => "west", "west" => "north",
            _ => v
        };

        private static string? PrevFacing4(string? v) => (v?.ToLowerInvariant()) switch
        {
            "north" => "west", "west" => "south", "south" => "east", "east" => "north",
            _ => v
        };

        private static readonly string[] Facing6 = { "north", "east", "south", "west", "up", "down" };

        private static string? NextFacing6(string? v)
        {
            int i = Array.FindIndex(Facing6, f => f.Equals(v, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return "north";
            return Facing6[(i + 1) % Facing6.Length];
        }

        private static string? PrevFacing6(string? v)
        {
            int i = Array.FindIndex(Facing6, f => f.Equals(v, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return "north";
            return Facing6[(i - 1 + Facing6.Length) % Facing6.Length];
        }

        public static string RotateY90(string blockId, string? stateString, bool positive)
        {
            var t = GetPresetType(blockId);
            if (t == PresetType.None)
                return string.IsNullOrEmpty(stateString) ? "Block{" + (blockId ?? "minecraft:stone") + "}" : stateString!;
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            var parsed = ParseToProps(blockId, stateString);
            var defaults = GetDefaults(t);
            foreach (var kv in defaults) if (!parsed.ContainsKey(kv.Key)) d[kv.Key] = kv.Value;
            foreach (var kv in parsed) d[kv.Key] = kv.Value;

            if (t is PresetType.Piston)
            {
                d["facing"] = positive
                    ? NextFacing6(d.GetValueOrDefault("facing", "north"))!
                    : PrevFacing6(d.GetValueOrDefault("facing", "north"))!;
            }
            else if (d.ContainsKey("facing"))
            {
                d["facing"] = positive
                    ? NextFacing4(d["facing"]) ?? d["facing"]
                    : PrevFacing4(d["facing"]) ?? d["facing"];
            }

            if (t == PresetType.RedstoneWire)
            {
                string? e = d["east"], s = d["south"], w = d["west"], n = d["north"];
                if (positive) { d["east"] = n!; d["south"] = e!; d["west"] = s!; d["north"] = w!; }
                else { d["east"] = s!; d["south"] = w!; d["west"] = n!; d["north"] = e!; }
            }
            if (d.Count == 0) return "Block{" + blockId + "}";
            return BuildState(blockId, d);
        }

        public static string SetAxisXyz(string blockId, string? stateString, char axis) // 'x' 'y' or 'z'
        {
            axis = char.ToLowerInvariant(axis);
            if (axis is not ('x' or 'y' or 'z')) return stateString ?? GetDefaultStateForBlockId(blockId);
            var t = GetPresetType(blockId);

            // Escaliers : pas de propriété axis en vanilla ; on mappe X/Z → facing est↔ouest / nord↔sud, Y → half bas↔haut.
            if (t == PresetType.Stairs)
            {
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in GetDefaults(PresetType.Stairs)) d[kv.Key] = kv.Value;
                foreach (var kv in ParseToProps(blockId, stateString)) d[kv.Key] = kv.Value;
                string f = (d.GetValueOrDefault("facing", "north") ?? "north").ToLowerInvariant();
                if (axis == 'x')
                {
                    d["facing"] = f is "east" or "west"
                        ? (f == "east" ? "west" : "east")
                        : "east";
                }
                else if (axis == 'z')
                {
                    d["facing"] = f is "north" or "south"
                        ? (f == "south" ? "north" : "south")
                        : "south";
                }
                else
                {
                    string h = (d.GetValueOrDefault("half", "bottom") ?? "bottom").ToLowerInvariant();
                    d["half"] = h == "top" ? "bottom" : "top";
                }
                d["shape"] = "straight";
                return BuildState(blockId, d);
            }

            if (t is not (PresetType.Log or PresetType.AxisBlock)) return stateString ?? GetDefaultStateForBlockId(blockId);
            var d2 = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in GetDefaults(t)) d2[kv.Key] = kv.Value;
            foreach (var kv in ParseToProps(blockId, stateString)) d2[kv.Key] = kv.Value;
            d2["axis"] = axis.ToString();
            return BuildState(blockId, d2);
        }

        public static string GetDirectionGlyph(string blockId, string? stateString)
        {
            var t = GetPresetType(blockId);
            var p = ParseToProps(blockId, stateString);
            string? G(string k) => p.GetValueOrDefault(k) ?? GetDefaults(t).GetValueOrDefault(k);

            if (t == PresetType.Stairs)
            {
                return G("facing") switch { "north" => "↑", "south" => "↓", "east" => "→", "west" => "←", _ => "S" };
            }
            if (t is PresetType.Log or PresetType.AxisBlock)
            {
                return G("axis") switch { "x" => "—", "y" => "│", "z" => "╱", _ => "L" };
            }
            if (t is PresetType.Piston or PresetType.Furnace or PresetType.Chest)
            {
                return G("facing") switch
                {
                    "north" => "↑", "south" => "↓", "east" => "→", "west" => "←", "up" => "U", "down" => "D", _ => "·"
                };
            }
            if (t == PresetType.Door) return "◫";
            if (t == PresetType.RedstoneWire) return "⎈";
            var name = (blockId ?? "").Replace("minecraft:", "", StringComparison.Ordinal);
            if (name.Length == 0) return "?";
            return char.ToUpperInvariant(name[0]).ToString();
        }
    }
}
