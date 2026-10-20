using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public enum OreMaterial
{
    Copper, Tin, Iron, Lead, Silver, Tungsten, Gold, Platinum,
    Cobalt, Palladium, Mythril, Orichalcum, Adamantite, Titanium
}

public static class OreFamily
{
    public const int MaterialCount = 14;

    // One representative type per material, used for the flying-sword sprite.
    public static readonly int[] DefaultType =
    [
        ItemID.CopperBroadsword, ItemID.TinBroadsword, ItemID.IronBroadsword, ItemID.LeadBroadsword,
        ItemID.SilverBroadsword, ItemID.TungstenBroadsword, ItemID.GoldBroadsword, ItemID.PlatinumBroadsword,
        ItemID.CobaltSword, ItemID.PalladiumSword, ItemID.MythrilSword, ItemID.OrichalcumSword,
        ItemID.AdamantiteSword, ItemID.TitaniumSword
    ];

    public static readonly string[] Names =
    [
        "铜", "锡", "铁", "铅", "银", "钨", "金", "铂金",
        "钴", "钯金", "秘银", "山铜", "精金", "钛金"
    ];

    public static readonly Color[] Tints =
    [
        new(196, 112, 72), new(178, 178, 184), new(176, 150, 130), new(128, 140, 160),
        new(214, 214, 222), new(154, 190, 142), new(232, 196, 72), new(186, 206, 224),
        new(76, 128, 220), new(220, 150, 70), new(120, 196, 150), new(206, 112, 180),
        new(210, 100, 92), new(180, 196, 208)
    ];

    private static readonly int[][] MaterialSwords =
    [
        [ItemID.CopperShortsword, ItemID.CopperBroadsword],
        [ItemID.TinShortsword, ItemID.TinBroadsword],
        [ItemID.IronShortsword, ItemID.IronBroadsword],
        [ItemID.LeadShortsword, ItemID.LeadBroadsword],
        [ItemID.SilverShortsword, ItemID.SilverBroadsword],
        [ItemID.TungstenShortsword, ItemID.TungstenBroadsword],
        [ItemID.GoldShortsword, ItemID.GoldBroadsword],
        [ItemID.PlatinumShortsword, ItemID.PlatinumBroadsword],
        [ItemID.CobaltSword],
        [ItemID.PalladiumSword],
        [ItemID.MythrilSword],
        [ItemID.OrichalcumSword],
        [ItemID.AdamantiteSword],
        [ItemID.TitaniumSword]
    ];

    public static int MaterialOf(int type)
    {
        for (int i = 0; i < MaterialSwords.Length; i++)
            if (Array.IndexOf(MaterialSwords[i], type) >= 0) return i;
        return -1;
    }

    public static bool IsOre(int type) => MaterialOf(type) >= 0;

    public static int Mask(IEnumerable<Item> items)
    {
        int mask = 0;
        foreach (Item item in items)
        {
            if (item.IsAir) continue;
            int material = MaterialOf(item.type);
            if (material >= 0) mask |= 1 << material;
        }
        return mask;
    }

    public static int PopCount(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);

    // Slot expansion was not approved. Only the original active 17-slot
    // layout contributes; legacy recovery storage is deliberately excluded.
    public static int Count(SoulItem soul)
    {
        var items = soul.Hosts.Take(soul.Capacity)
            .Concat(soul.SupportHosts.Take(soul.SupportCapacity))
            .Concat(soul.ForestHosts.Take(soul.BonusCapacity));
        return PopCount(Mask(items));
    }

    public static int[] Materials(SoulItem soul)
    {
        int mask = Mask(soul.Hosts.Take(soul.Capacity)
            .Concat(soul.SupportHosts.Take(soul.SupportCapacity))
            .Concat(soul.ForestHosts.Take(soul.BonusCapacity)));
        return Enumerable.Range(0, MaterialCount).Where(i => (mask & (1 << i)) != 0).ToArray();
    }

    public static Item Representative(SoulItem soul, int material)
    {
        foreach (var item in soul.Hosts.Take(soul.Capacity).Concat(soul.ForestHosts.Take(soul.BonusCapacity))
            .Concat(soul.SupportHosts.Take(soul.SupportCapacity)))
            if (!item.IsAir && MaterialOf(item.type) == material) return item;
        return new Item(DefaultType[material]);
    }

    public static int PerSwordDamage(int sourceDamage, int count) =>
        Math.Max(1, (int)(sourceDamage * .08f * (1 + .05f * Math.Max(0, count - 1))));

    public static void OnMainHit(Projectile projectile, NPC target, int damageDone)
    {
        if (Main.netMode == NetmodeID.Server || projectile.owner != Main.myPlayer ||
            projectile.owner < 0 || projectile.owner >= Main.maxPlayers || projectile.ModProjectile is not SoulBlade ||
            damageDone <= 0 || !HardOrePlayer.ValidTarget(target, false)) return;
        var source = projectile.GetGlobalProjectile<SoulSource>();
        if (!IsOre(source.SourceWeapon)) return;
        var soul = WoodCombat.FindSoul(Main.player[projectile.owner], source.SoulId);
        if (soul == null || !soul.Attached || Main.player[projectile.owner].dead ||
            !ReferenceEquals(Main.player[projectile.owner].HeldItem.ModItem, soul) ||
            !soul.Active.Any(i => i.type == source.SourceWeapon)) return;

        int count = Count(soul);
        if (count <= 0) return;

        uint now = Main.GameUpdateCount;
        if (OreSystem.LastGlobal.TryGetValue(soul.SoulId, out uint global) && now - global < 30) return;
        NPC root = WoodCombat.Root(target);
        var key = (soul.SoulId, root.GetGlobalNPC<HardOreTarget>().Id);
        if (OreSystem.LastTarget.TryGetValue(key, out uint last) && now - last < 60) return;
        OreSystem.LastGlobal[soul.SoulId] = now;
        OreSystem.LastTarget[key] = now;

        int[] materials = Materials(soul);
        for (int i = 0; i < materials.Length; i++)
        {
            int material = materials[i];
            int per = PerSwordDamage(projectile.damage, count);
            int index = Projectile.NewProjectile(projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<OreFlyingSword>(), per, 0f, projectile.owner,
                root.whoAmI, material, count);
            if (index >= Main.maxProjectiles) continue;
            Projectile sword = Main.projectile[index];
            var mod = (OreFlyingSword)sword.ModProjectile;
            mod.BindTarget(root);
            mod.VisualType = Representative(soul, material).type;
            mod.Material = material;
            mod.Total = materials.Length;
            mod.RingIndex = i;
            mod.SourceSoulId = soul.SoulId;
            sword.CritChance = projectile.CritChance;
            sword.GetGlobalProjectile<SoulSource>().SoulId = soul.SoulId;
            sword.GetGlobalProjectile<SoulSource>().SourceWeapon = source.SourceWeapon;
            sword.netUpdate = true;
        }
    }
}

public class OreSystem : ModSystem
{
    internal static readonly Dictionary<string, uint> LastGlobal = [];
    internal static readonly Dictionary<(string Soul, long Target), uint> LastTarget = [];

    public override void PostUpdateEverything()
    {
        uint now=Main.GameUpdateCount;
        if(now%120!=0) return;
        foreach(var key in LastGlobal.Where(p=>now-p.Value>=30).Select(p=>p.Key).ToArray()) LastGlobal.Remove(key);
        foreach(var key in LastTarget.Where(p=>now-p.Value>=60).Select(p=>p.Key).ToArray()) LastTarget.Remove(key);
    }

    public override void OnWorldUnload()
    {
        LastGlobal.Clear();
        LastTarget.Clear();
    }

    public override void Unload()
    {
        LastGlobal.Clear();
        LastTarget.Clear();
    }
}
