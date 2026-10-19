using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Headless probe: tModLoader -server, no graphics window. Reports exactly which
// gate blocks PrefixID.Legendary on the soul.
public class PrefixProbe : ModSystem
{
    public override void PostAddRecipes()
    {
        if (!Main.dedServ) return;
        string path = Environment.GetEnvironmentVariable("ZENITH_PREFIX_REPORT");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var soul = (SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Hosts[0] = new Item(ItemID.CopperShortsword);
            soul.RefreshStats();
            bool melee = soul.MeleePrefix();
            var cats = soul.Item.GetPrefixCategories();
            bool canRoll = soul.Item.CanRollPrefix(PrefixID.Legendary);
            ItemLoader.PreReforge(soul.Item);
            int damage = soul.Item.damage;
            bool canApply = soul.Item.CanApplyPrefix(PrefixID.Legendary);
            bool applied = soul.Item.Prefix(PrefixID.Legendary);
            int prefix = soul.Item.prefix;
            File.WriteAllText(path,
                $"melee={melee} cats={string.Join(",", cats)} damage={damage} " +
                $"canRoll={canRoll} canApply={canApply} applied={applied} prefix={prefix}");
        }
        catch (Exception error)
        {
            File.WriteAllText(path, "FAIL: " + error);
        }
        Console.WriteLine("PREFIX_PROBE_DONE");
        Environment.Exit(0);
    }
}
