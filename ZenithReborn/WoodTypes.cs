using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public enum WoodKind { Forest, Boreal, Palm, Mahogany, Cactus, Ebon, Shade, Ash, Pearl, Dynasty, Spooky, Fey }

public readonly record struct WoodLoadout(int Mask, int SupportCount)
{
    public int Count => System.Numerics.BitOperations.PopCount((uint)Mask);
    public bool Full => Mask == 4095;
    public bool Has(WoodKind kind) => (Mask & (1 << (int)kind)) != 0;
    public float Strength => Full ? 3 : Count >= 8 ? 1.75f : Count >= 6 ? 1.5f :
        Count >= 4 ? 1.35f : Count >= 2 ? 1.2f : Count >= 1 ? 1.1f : 0;
    public int Life => Full ? 100 : Count >= 8 ? 30 : Count >= 6 ? 20 : Count >= 4 ? 15 : Count >= 2 ? 10 : Count >= 1 ? 5 : 0;
}

public static class WoodTypes
{
    private static int[] types;
    public static int[] Types => types ??= [ItemID.WoodenSword, ItemID.BorealWoodSword, ItemID.PalmWoodSword,
        ItemID.RichMahoganySword, ItemID.CactusSword, ItemID.EbonwoodSword, ItemID.ShadewoodSword,
        ItemID.AshWoodSword, ItemID.PearlwoodSword, ModContent.ItemType<DynastyWoodSword>(),
        ModContent.ItemType<SpookyWoodSword>(), ModContent.ItemType<FeyWoodSword>()];
    public static readonly string[] Names = ["木剑", "针叶木剑", "棕榈木剑", "红木剑", "仙人掌剑", "乌木剑",
        "暗影木剑", "灰烬木剑", "珍珠木剑", "王朝木剑", "阴森木剑", "仙灵木剑"];
    public static readonly string[] Effects = ["森息", "霜寂", "盐蚀", "瘴毒", "裂伤", "蚀枯", "血馥", "烬燃", "净辉", "御令", "悼魂", "孢光"];
    public static int Index(int type) => Array.IndexOf(Types, type);
    public static bool IsWood(int type) => Index(type) >= 0;
    public static Color SwordTint(int type) => Index(type) switch {
        9 => new Color(240, 160, 100), 10 => new Color(185, 125, 240), 11 => new Color(125, 235, 210), _ => Color.White
    };
    public static int Mask(IEnumerable<Item> items)
    {
        int mask = 0;
        foreach (Item item in items)
        {
            int i = item.IsAir ? -1 : Index(item.type);
            if (i >= 0) mask |= 1 << i;
        }
        return mask;
    }
    public static WoodLoadout Evaluate(SoulItem soul)
    {
        if (!soul.Attached) return default;
        int seed = Mask(soul.Hosts.Take(soul.BaseCapacity)), slots = 0, mask = seed;
        for (int attempt = 0; attempt <= 5; attempt++)
        {
            mask = seed | Mask(soul.SupportHosts.Take(slots));
            int count = System.Numerics.BitOperations.PopCount((uint)mask);
            int next = new[] { 1, 2, 4, 6, 8 }.Count(n => count >= n);
            if (next == slots) return new(mask, slots);
            slots = next;
        }
        return new(mask, slots);
    }
    public static bool CanSupport(Item item) => SoulItem.CanHost(item) &&
        (IsWood(item.type) || HardOreTypes.IsOre(item.type) || WeaponFamilies.Of(item.type) != BladeFamily.Plain || SoulProgress.ZenithWeapons.Contains(item.type));
    public static string Description(int type)
    {
        int i = Index(type);
        return i switch {
            0 => "森息：主攻命中后额外回复0.25×S生命/秒，持续6秒",
            1 => "霜寂：4秒标记，普通敌人减速4%×S，主攻增伤1%×S；Boss不减速",
            2 => "盐蚀：4秒标记，主攻固定穿甲＋2×S",
            3 => "瘴毒：4秒毒伤，每秒(1＋0.02P)×S；遵循中毒免疫",
            4 => "裂伤：5秒流血，每层每秒(0.5＋0.015P)×S，最多3层",
            5 => "蚀枯：4秒标记，自有持续伤害＋2%×S",
            6 => "血馥：6秒内剑魂直接治疗效果＋4%×S；不增加治疗额度",
            7 => "烬燃：4秒火伤，每秒(1＋0.025P)×S；遵循着火免疫",
            8 => "净辉：4秒标记，主攻伤害＋1.5%×S",
            9 => "御令：6秒内新受指定减益持续时间缩短4%×S",
            10 => "悼魂：6秒自有持续伤害＋10%×S；死亡传播剩余预算25%×S，360像素内至多2敌平分",
            11 => "孢光：新施加或刷新的自有持续伤害延长0.5×S秒，上限8秒",
            _ => HardOreTypes.IsOre(type) ? HardOreTypes.Description(type) : WeaponFamilies.Description(type)
        };
    }
}

public abstract class NewWoodSword : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.WoodenSword;
    protected abstract int BaseDamage { get; }
    protected virtual int Interval => 20;
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.WoodenSword);
        Item.damage = BaseDamage; Item.useTime = Item.useAnimation = Interval;
        Item.knockBack = this is SpookyWoodSword ? 6 : this is FeyWoodSword ? 3 : 4;
        Item.color = this is SpookyWoodSword ? new Color(185, 125, 240) : this is FeyWoodSword ? new Color(125, 235, 210) : new Color(240, 160, 100);
        Item.rare = this is SpookyWoodSword ? ItemRarityID.Yellow : ItemRarityID.White;
    }
}
public class DynastyWoodSword : NewWoodSword
{
    protected override int BaseDamage => 12;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.DynastyWood, 7).AddTile(TileID.WorkBenches).Register();
}
public class SpookyWoodSword : NewWoodSword
{
    protected override int BaseDamage => 90;
    protected override int Interval => 18;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.SpookyWood, 7).AddTile(TileID.WorkBenches).Register();
}
public class FeyWoodSword : NewWoodSword
{
    protected override int BaseDamage => 10;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Wood, 7).AddIngredient(ItemID.Mushroom).AddTile(TileID.LivingLoom).Register();
}
public class WoodSwordTooltips : GlobalItem
{
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        int index = WoodTypes.Index(item.type);
        if (index < 0) return;
        tooltips.Add(new(Mod, "WoodSoul", "剑魂寄宿·木质族：" + WoodTypes.Description(item.type)));
        tooltips.Add(new(Mod, "WoodScale", "S随木剑种类提高：1/2/4/6/8/全套＝1.10/1.20/1.35/1.50/1.75/3倍；P为实际主剑影伤害"));
    }
}
