using Terraria;
using Terraria.ID;

namespace ZenithReborn;

public enum BladeFamily : byte { Plain, Swift, Solid, Frost, Nature, Arcane }

public static class WeaponFamilies
{
    private static readonly HashSet<int> Swift = [ItemID.CopperShortsword,ItemID.TinShortsword,
        ItemID.IronShortsword,ItemID.LeadShortsword,ItemID.SilverShortsword,ItemID.TungstenShortsword,
        ItemID.GoldShortsword,ItemID.PlatinumShortsword,ItemID.Terragrim,ItemID.Arkhalis];
    private static readonly HashSet<int> Solid = [ItemID.CopperBroadsword,ItemID.TinBroadsword,
        ItemID.IronBroadsword,ItemID.LeadBroadsword,ItemID.SilverBroadsword,ItemID.TungstenBroadsword,
        ItemID.GoldBroadsword,ItemID.PlatinumBroadsword,ItemID.BoneSword,ItemID.WoodenSword,
        ItemID.BorealWoodSword,ItemID.PalmWoodSword,ItemID.RichMahoganySword,ItemID.EbonwoodSword,
        ItemID.ShadewoodSword,ItemID.CactusSword];
    public static BladeFamily Of(int type) => WoodTypes.IsWood(type) ? BladeFamily.Plain : Swift.Contains(type) ? BladeFamily.Swift :
        Solid.Contains(type) ? BladeFamily.Solid : type == ItemID.IceBlade ? BladeFamily.Frost :
        type == ItemID.BladeofGrass ? BladeFamily.Nature :
        type is ItemID.EnchantedSword or ItemID.Starfury ? BladeFamily.Arcane : BladeFamily.Plain;
    public static string Name(int type) => WoodTypes.IsWood(type) ? "木质族" : HardOreTypes.IsOre(type) ? "锻金族·肉后三矿" : Of(type) switch {
        BladeFamily.Swift => "迅刃", BladeFamily.Solid => "实锋", BladeFamily.Frost => "寒霜",
        BladeFamily.Nature => "自然", BladeFamily.Arcane => "灵能", _ => "基础寄宿" };
    public static string Description(int type) => Of(type) switch {
        BladeFamily.Swift => "使用间隔缩短15%，御剑距离缩短15%",
        BladeFamily.Solid => "命中宽度提高15%，击退提高20%",
        BladeFamily.Frost => "命中附加2秒霜火，尊重目标免疫",
        BladeFamily.Nature => "命中附加3秒中毒，尊重目标免疫",
        BladeFamily.Arcane => "飞行中释放30%伤害的剑气或落星，每种剑独立45帧间隔，最远400像素",
        _ => "继承武器伤害、攻速、暴击与击退" };
    public static float Interval(int type) => Of(type) == BladeFamily.Swift ? .85f : 1;
    public static float Range(int type) => Of(type) == BladeFamily.Swift ? .85f : 1;
}
