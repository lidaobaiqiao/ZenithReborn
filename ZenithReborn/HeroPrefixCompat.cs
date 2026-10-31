using System.Linq;
using Terraria;

namespace ZenithReborn;

// 英雄模组的前缀编辑器会用以下步骤替换物品：
//   新建物品、载入默认值、应用前缀，再用克隆结果替换槽位。
// 原物品数据因此被丢弃，普通重铸前后回调也不会运行。
// 缓存鼠标上准备放入编辑器的剑魂，
// 随后在新建空剑魂应用前缀时恢复数据。
internal static class HeroPrefixCompat
{
    private static Item[] hosts;
    private static Item[] supports, forest;
    private static List<Item> additional;
    private static Item legendary;
    private static bool awakened;
    private static string soulId;
    private static int kills, cores, oreCollection;
    private static int seals,legacy,yujian;
    private static int martialCharge,martialTier;
    private static int environments,environmentBlessings;
    private static SwordEnvironment domain;
    private static bool has;
    private static bool attached, memory;

    internal static void Remember(SoulItem soul)
    {
        soul.RegisterOreCollection();
        soul.RegisterEnvironments();environments=soul.EnvironmentCollection;environmentBlessings=soul.EnvironmentBlessings;domain=soul.DomainEnvironment;
        hosts = soul.Hosts.Select(i => i.Clone()).ToArray();
        supports = soul.SupportHosts.Select(i => i.Clone()).ToArray();
        forest = soul.ForestHosts.Select(i => i.Clone()).ToArray();
        additional=soul.AdditionalSupports.Select(i=>i.Clone()).ToList();
        legendary=soul.LegendaryHost.Clone();
        awakened = soul.Awakened;
        soulId = soul.SoulId;
        kills = soul.Kills;
        cores = soul.Cores;
        oreCollection=soul.OreCollectionMask;
        seals=soul.Seals;legacy=soul.LegacyReleased;yujian=soul.YujianCharge;
        martialCharge=soul.LegacyMartialCharge;martialTier=soul.LegacyMartialTier;
        attached = soul.Attached; memory = soul.KingMemory;
        has = true;
    }

    internal static bool TryRestore(SoulItem soul)
    {
        if (!has) return false;
        // 只恢复刚重建的空剑魂，不能覆盖已有数据。
        if (soul.Stored.Any(h => !h.IsAir) || soul.Kills != 0 || soul.Cores != 0 || soul.OreCollectionMask!=0 || soul.Seals!=0 || soul.EnvironmentCollection!=0) return false;
        for (int i = 0; i < soul.Hosts.Length && i < hosts.Length; i++)
            if (soul.Hosts[i].IsAir && !hosts[i].IsAir)
                soul.Hosts[i] = hosts[i].Clone();
        if (!string.IsNullOrEmpty(soulId)) soul.SoulId = soulId;
        soul.Kills = kills & SoulProgress.All;
        soul.Cores = cores & 63;
        soul.Attached = attached; soul.KingMemory = memory;
        soul.SupportHosts = supports.Select(i => i.Clone()).ToArray();
        soul.ForestHosts = forest.Select(i => i.Clone()).ToArray();
        soul.AdditionalSupports=additional.Select(i=>i.Clone()).ToList();
        soul.LegendaryHost=legendary.Clone();
        soul.AwakeningMemory = awakened;
        soul.OreCollectionMask=oreCollection & OreFamily.AllMaterials;
        soul.Seals=seals;soul.LegacyReleased=legacy;soul.YujianCharge=yujian;
        soul.LegacyMartialCharge=martialCharge;soul.LegacyMartialTier=martialTier;
        soul.EnvironmentCollection=environments;soul.EnvironmentBlessings=environmentBlessings;soul.DomainEnvironment=domain;soul.NormalizeEnvironments();
        return true;
    }

    internal static void Clear()
    {
        hosts = null;
        supports = forest = null;
        additional=null;
        legendary=null;
        soulId = null;
        has = false;
    }
}
