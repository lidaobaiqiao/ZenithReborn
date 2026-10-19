using System.Linq;
using Terraria;

namespace ZenithReborn;

// HEROsMod's prefix editor replaces the item with
//   new Item(); netDefaults(id); Prefix(pre); itemSlot.item = val2.Clone();
// so all ModItem data is discarded and PreReforge/PostReforge never run.
// Cache the last soul the player held on the cursor (the one being placed into
// that editor), then restore it inside ApplyPrefix on the rebuilt empty soul.
internal static class HeroPrefixCompat
{
    private static Item[] hosts;
    private static Item[] supports, forest;
    private static bool awakened;
    private static string soulId;
    private static int kills, cores;
    private static bool has;
    private static bool attached, memory;

    internal static void Remember(SoulItem soul)
    {
        hosts = soul.Hosts.Select(i => i.Clone()).ToArray();
        supports = soul.SupportHosts.Select(i => i.Clone()).ToArray();
        forest = soul.ForestHosts.Select(i => i.Clone()).ToArray();
        awakened = soul.Awakened;
        soulId = soul.SoulId;
        kills = soul.Kills;
        cores = soul.Cores;
        attached = soul.Attached; memory = soul.KingMemory;
        has = true;
    }

    internal static bool TryRestore(SoulItem soul)
    {
        if (!has) return false;
        // Only fill a freshly rebuilt, empty soul. Never overwrite real data.
        if (soul.Stored.Any(h => !h.IsAir) || soul.Kills != 0 || soul.Cores != 0) return false;
        for (int i = 0; i < soul.Hosts.Length && i < hosts.Length; i++)
            if (soul.Hosts[i].IsAir && !hosts[i].IsAir)
                soul.Hosts[i] = hosts[i].Clone();
        if (!string.IsNullOrEmpty(soulId)) soul.SoulId = soulId;
        soul.Kills = kills & SoulProgress.All;
        soul.Cores = cores & 63;
        soul.Attached = attached; soul.KingMemory = memory;
        soul.SupportHosts = supports.Select(i => i.Clone()).ToArray();
        soul.ForestHosts = forest.Select(i => i.Clone()).ToArray();
        soul.AwakeningMemory = awakened;
        return true;
    }

    internal static void Clear()
    {
        hosts = null;
        supports = forest = null;
        soulId = null;
        has = false;
    }
}
