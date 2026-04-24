using System;
using System.Collections.Generic;
using System.Linq;

namespace MagicalChalkStudio
{
    /// <summary>Liste étendue d’ids vanilla pour compléter blocks.txt.</summary>
    public static class DefaultBlockPalette
    {
        public static readonly string[] Ids = BuildIds();

        private static string[] BuildIds()
        {
            var set = new LinkedHashSet();

            void Add(string id) => set.Add(id);

            // Pierre & bâtiment
            foreach (var id in new[]
            {
                "minecraft:stone", "minecraft:granite", "minecraft:polished_granite", "minecraft:diorite",
                "minecraft:polished_diorite", "minecraft:andesite", "minecraft:polished_andesite",
                "minecraft:deepslate", "minecraft:cobbled_deepslate", "minecraft:polished_deepslate",
                "minecraft:reinforced_deepslate", "minecraft:tuff", "minecraft:calcite", "minecraft:dripstone_block",
                "minecraft:cobblestone", "minecraft:mossy_cobblestone", "minecraft:stone_bricks",
                "minecraft:mossy_stone_bricks", "minecraft:cracked_stone_bricks", "minecraft:chiseled_stone_bricks",
                "minecraft:bricks", "minecraft:packed_mud", "minecraft:mud_bricks", "minecraft:nether_bricks",
                "minecraft:cracked_nether_bricks", "minecraft:chiseled_nether_bricks", "minecraft:red_nether_bricks",
                "minecraft:quartz_block", "minecraft:smooth_quartz", "minecraft:chiseled_quartz_block",
                "minecraft:sandstone", "minecraft:cut_sandstone", "minecraft:chiseled_sandstone", "minecraft:smooth_sandstone",
                "minecraft:red_sandstone", "minecraft:cut_red_sandstone", "minecraft:chiseled_red_sandstone", "minecraft:smooth_red_sandstone",
                "minecraft:end_stone", "minecraft:end_stone_bricks", "minecraft:purpur_block", "minecraft:purpur_pillar",
                "minecraft:prismarine", "minecraft:prismarine_bricks", "minecraft:dark_prismarine", "minecraft:sea_lantern",
                "minecraft:blackstone", "minecraft:polished_blackstone", "minecraft:polished_blackstone_bricks",
                "minecraft:gilded_blackstone", "minecraft:basalt", "minecraft:polished_basalt", "minecraft:smooth_basalt",
                "minecraft:netherrack", "minecraft:crimson_nylium", "minecraft:warped_nylium", "minecraft:magma_block",
                "minecraft:soul_sand", "minecraft:soul_soil", "minecraft:glowstone", "minecraft:shroomlight",
                "minecraft:ancient_debris", "minecraft:crying_obsidian", "minecraft:respawn_anchor", "minecraft:lodestone"
            }) Add(id);

            // Bois (bambou : *_block au lieu de *_log)
            foreach (var w in new[] { "oak", "spruce", "birch", "jungle", "acacia", "dark_oak", "mangrove", "cherry" })
            {
                Add($"minecraft:{w}_planks");
                Add($"minecraft:{w}_log");
                Add($"minecraft:stripped_{w}_log");
                Add($"minecraft:{w}_wood");
                Add($"minecraft:stripped_{w}_wood");
            }
            Add("minecraft:bamboo_planks");
            Add("minecraft:bamboo_block");
            Add("minecraft:stripped_bamboo_block");
            Add("minecraft:bamboo_mosaic");

            // Couleurs (concrete, wool, terracotta, stained_glass, candle, bed)
            string[] cols =
            {
                "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
                "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black"
            };
            foreach (var c in cols)
            {
                Add($"minecraft:{c}_concrete");
                Add($"minecraft:{c}_concrete_powder");
                Add($"minecraft:{c}_wool");
                Add($"minecraft:{c}_terracotta");
                Add($"minecraft:{c}_stained_glass");
                Add($"minecraft:{c}_stained_glass_pane");
                Add($"minecraft:{c}_candle");
                Add($"minecraft:{c}_bed");
                Add($"minecraft:{c}_banner");
                Add($"minecraft:{c}_shulker_box");
            }

            // Minerais & métaux
            foreach (var id in new[]
            {
                "minecraft:coal_ore", "minecraft:deepslate_coal_ore", "minecraft:iron_ore", "minecraft:deepslate_iron_ore",
                "minecraft:copper_ore", "minecraft:deepslate_copper_ore", "minecraft:gold_ore", "minecraft:deepslate_gold_ore",
                "minecraft:redstone_ore", "minecraft:deepslate_redstone_ore", "minecraft:lapis_ore", "minecraft:deepslate_lapis_ore",
                "minecraft:diamond_ore", "minecraft:deepslate_diamond_ore", "minecraft:emerald_ore", "minecraft:deepslate_emerald_ore",
                "minecraft:raw_iron_block", "minecraft:raw_copper_block", "minecraft:raw_gold_block",
                "minecraft:iron_block", "minecraft:copper_block", "minecraft:exposed_copper", "minecraft:weathered_copper",
                "minecraft:oxidized_copper", "minecraft:waxed_copper_block", "minecraft:gold_block", "minecraft:diamond_block",
                "minecraft:emerald_block", "minecraft:lapis_block", "minecraft:redstone_block", "minecraft:coal_block",
                "minecraft:netherite_block", "minecraft:amethyst_block", "minecraft:budding_amethyst", "minecraft:small_amethyst_bud",
                "minecraft:medium_amethyst_bud", "minecraft:large_amethyst_bud", "minecraft:amethyst_cluster"
            }) Add(id);

            // Nature & sol
            foreach (var id in new[]
            {
                "minecraft:dirt", "minecraft:grass_block", "minecraft:podzol", "minecraft:rooted_dirt", "minecraft:mud",
                "minecraft:mycelium", "minecraft:farmland", "minecraft:dirt_path", "minecraft:clay", "minecraft:gravel",
                "minecraft:sand", "minecraft:red_sand", "minecraft:ice", "minecraft:packed_ice", "minecraft:blue_ice",
                "minecraft:snow_block", "minecraft:snow", "minecraft:powder_snow", "minecraft:sponge", "minecraft:wet_sponge",
                "minecraft:obsidian", "minecraft:bedrock", "minecraft:glass", "minecraft:tinted_glass"
            }) Add(id);

            // Végétation
            foreach (var id in new[]
            {
                "minecraft:oak_leaves", "minecraft:spruce_leaves", "minecraft:birch_leaves", "minecraft:jungle_leaves",
                "minecraft:acacia_leaves", "minecraft:dark_oak_leaves", "minecraft:mangrove_leaves", "minecraft:cherry_leaves",
                "minecraft:azalea_leaves", "minecraft:flowering_azalea_leaves", "minecraft:oak_sapling", "minecraft:spruce_sapling",
                "minecraft:birch_sapling", "minecraft:jungle_sapling", "minecraft:acacia_sapling", "minecraft:dark_oak_sapling",
                "minecraft:short_grass", "minecraft:tall_grass", "minecraft:fern", "minecraft:large_fern", "minecraft:dead_bush",
                "minecraft:dandelion", "minecraft:poppy", "minecraft:blue_orchid", "minecraft:allium", "minecraft:azure_bluet",
                "minecraft:red_tulip", "minecraft:orange_tulip", "minecraft:white_tulip", "minecraft:pink_tulip",
                "minecraft:oxeye_daisy", "minecraft:cornflower", "minecraft:lily_of_the_valley", "minecraft:sunflower",
                "minecraft:lilac", "minecraft:rose_bush", "minecraft:peony", "minecraft:vine", "minecraft:glow_lichen",
                "minecraft:hanging_roots", "minecraft:big_dripleaf", "minecraft:small_dripleaf", "minecraft:spore_blossom",
                "minecraft:crimson_stem", "minecraft:warped_stem", "minecraft:stripped_crimson_stem", "minecraft:stripped_warped_stem",
                "minecraft:crimson_hyphae", "minecraft:warped_hyphae", "minecraft:nether_wart_block", "minecraft:warped_wart_block",
                "minecraft:shroomlight", "minecraft:hay_block", "minecraft:melon", "minecraft:pumpkin", "minecraft:carved_pumpkin",
                "minecraft:jack_o_lantern", "minecraft:cactus", "minecraft:sugar_cane", "minecraft:bamboo", "minecraft:bamboo_block"
            }) Add(id);

            // Utilitaires & redstone
            foreach (var id in new[]
            {
                "minecraft:crafting_table", "minecraft:furnace", "minecraft:blast_furnace", "minecraft:smoker",
                "minecraft:chest", "minecraft:trapped_chest", "minecraft:ender_chest", "minecraft:barrel", "minecraft:shulker_box",
                "minecraft:bookshelf", "minecraft:chiseled_bookshelf", "minecraft:lectern", "minecraft:composter", "minecraft:cauldron",
                "minecraft:stonecutter", "minecraft:grindstone", "minecraft:smithing_table", "minecraft:cartography_table",
                "minecraft:fletching_table", "minecraft:loom", "minecraft:bee_nest", "minecraft:beehive", "minecraft:honeycomb_block",
                "minecraft:honey_block", "minecraft:slime_block", "minecraft:target", "minecraft:tnt", "minecraft:spawner",
                "minecraft:redstone_lamp", "minecraft:note_block", "minecraft:jukebox", "minecraft:observer", "minecraft:dispenser",
                "minecraft:dropper", "minecraft:hopper", "minecraft:piston", "minecraft:sticky_piston", "minecraft:lever",
                "minecraft:stone_button", "minecraft:oak_button", "minecraft:redstone_torch", "minecraft:repeater", "minecraft:comparator",
                "minecraft:daylight_detector", "minecraft:tripwire_hook", "minecraft:iron_door", "minecraft:oak_door",
                "minecraft:iron_trapdoor", "minecraft:oak_trapdoor", "minecraft:ladder", "minecraft:scaffolding", "minecraft:chain",
                "minecraft:lantern", "minecraft:soul_lantern", "minecraft:torch", "minecraft:soul_torch", "minecraft:end_rod",
                "minecraft:sea_pickle", "minecraft:turtle_egg", "minecraft:sniffer_egg", "minecraft:frogspawn"
            }) Add(id);

            return set.ToArray();
        }

        public static void MergeInto(List<string> target)
        {
            var have = new HashSet<string>(target, StringComparer.Ordinal);
            foreach (var id in Ids)
            {
                if (have.Add(id))
                    target.Add(id);
            }
        }

        /// <summary>Ensemble ordonné simple (première occurrence conservée).</summary>
        private sealed class LinkedHashSet
        {
            private readonly List<string> _order = new List<string>();
            private readonly HashSet<string> _set = new HashSet<string>(StringComparer.Ordinal);

            public void Add(string id)
            {
                if (string.IsNullOrWhiteSpace(id)) return;
                if (_set.Add(id))
                    _order.Add(id);
            }

            public string[] ToArray() => _order.ToArray();
        }
    }
}
