using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 0.3.129 木质族 V2：名册缩为 8 把原版木剑；仙人掌剑退出（物品仍在，自动归属 Swift 族）。
// 王朝/阴森/仙灵三把新增剑的 ModItem 类已彻底删除，旧存档中对应物品退化为 UnloadedItem 占位。
public enum WoodKind { Forest, Boreal, Palm, Mahogany, Ebon, Shade, Ash, Pearl }

// Crown 为圣林归冠标记，由 WoodTypes.Evaluate 与 SoulItem.CombatWood 构造点实时传入
// WoodCrown.Active(soul)（内部含防循环重入护盾）；其余构造点未传时默认 false，不影响既有行为。
public readonly record struct WoodLoadout(int Mask, int SupportCount, bool Crown = false)
{
    public int Count => System.Numerics.BitOperations.PopCount((uint)Mask);
    public bool Full => Mask == 0xFF;
    public bool Has(WoodKind kind) => (Mask & (1 << (int)kind)) != 0;
    // 5 档强度表：1/2/4/6/8 种 → 1.10/1.20/1.35/1.50/2.00；圣林归冠时覆盖为 3.00。
    public float Strength => Crown ? 3 : Count >= 8 ? 2 : Count >= 6 ? 1.5f :
        Count >= 4 ? 1.35f : Count >= 2 ? 1.2f : Count >= 1 ? 1.1f : 0;
    // 生命加成 5 档：1/2/4/6/8 种 → +5/+10/+15/+25/+40；圣林归冠时覆盖为 +100。
    public int Life => Crown ? 100 : Count >= 8 ? 40 : Count >= 6 ? 25 : Count >= 4 ? 15 : Count >= 2 ? 10 : Count >= 1 ? 5 : 0;
}

public static class WoodTypes
{
    private static int[] types;
    public static int[] Types => types ??= [ItemID.WoodenSword, ItemID.BorealWoodSword, ItemID.PalmWoodSword,
        ItemID.RichMahoganySword, ItemID.EbonwoodSword, ItemID.ShadewoodSword,
        ItemID.AshWoodSword, ItemID.PearlwoodSword];
    // 手写译名：新增条目必须核对原版 zh-Hans 译名。Ash Wood Sword 为「白蜡木剑」，
    // 「灰烬」是地狱地形/AshBlock 的译名，不是木材名。
    public static readonly string[] Names = ["木剑", "针叶木剑", "棕榈木剑", "红木剑", "乌木剑",
        "暗影木剑", "白蜡木剑", "珍珠木剑"];
    public static readonly string[] Effects = ["森息", "霜寂", "盐蚀", "瘴毒", "蚀枯", "血馥", "烬燃", "净辉"];
    public static int Index(int type) => Array.IndexOf(Types, type);
    public static bool IsWood(int type) => Index(type) >= 0;
    // 0.3.129 起仅 8 把原版木剑为木质族成员；旧新增剑退出后不再有专属剑影着色，恒返回白色。
    public static Color SwordTint(int type) => Color.White;
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
        // 归冠判定在构造处完成，供 Strength/Life 覆盖读取；Evaluate 与 CombatWood 构造点均接入 WoodCrown.Active 实时判定。
        // 族内计数＝主攻槽＋已生效支援槽＋森林/圣林奖励槽＋传奇位（FamilyItems）；奖励位不设例外。
        return new(Mask(SoulSupport.FamilyItems(soul)), soul.SupportCapacity, WoodCrown.Active(soul));
    }
    // 保留给既有调用方的兼容入口。
    public static bool CanSupport(Item item) => SoulSupport.CanSupport(item);
    public static string Description(int type)
    {
        int i = Index(type);
        return i switch {
            0 => "森息：主攻命中后额外回复0.25×S生命/秒，持续6秒",
            1 => "霜寂：4秒标记，普通敌人减速4%×S，主攻增伤1%×S；Boss不减速",
            2 => "盐蚀：4秒标记，主攻固定穿甲＋2×S",
            3 => "瘴毒：4秒毒伤，每秒(1＋0.02P)×S；遵循中毒免疫",
            4 => "蚀枯：4秒标记，自有持续伤害＋2%×S",
            5 => "血馥：6秒内剑魂直接治疗效果＋4%×S；不增加治疗额度",
            6 => "烬燃：4秒火伤，每秒(1＋0.025P)×S；遵循着火免疫",
            7 => "净辉：4秒标记，主攻伤害＋1.5%×S",
            _ => HardOreTypes.IsOre(type) ? HardOreTypes.Description(type) : WeaponFamilies.Description(type)
        };
    }
}

// 三把新增木剑已彻底删除（不再注册物品，无法创造菜单/作弊获取）；
// 旧存档中的对应物品由 tModLoader 转为 UnloadedItem 占位，取出后无法再放回。
public class WoodSwordTooltips : GlobalItem
{
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        int index = WoodTypes.Index(item.type);
        if (index < 0) return;
        tooltips.Add(new(Mod, "WoodSoul", "剑魂寄宿·木质族：" + WoodTypes.Description(item.type)));
        tooltips.Add(new(Mod, "WoodScale", "S随木剑种类提高：1/2/4/6/8把＝1.10/1.20/1.35/1.50/2.00倍；圣林归冠时3.00倍；P为实际主剑影伤害"));
    }
}
