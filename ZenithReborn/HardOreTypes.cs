using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public enum HardOreKind { Cobalt, Palladium, Mythril, Orichalcum, Adamantite, Titanium }

public static class HardOreTypes
{
    public static readonly int[] Types = [ItemID.CobaltSword, ItemID.PalladiumSword, ItemID.MythrilSword,
        ItemID.OrichalcumSword, ItemID.AdamantiteSword, ItemID.TitaniumSword];
    public static readonly string[] Names = ["钴剑", "钯金剑", "秘银剑", "山铜剑", "精金剑", "钛金剑"];
    public static int Index(int type) => Array.IndexOf(Types, type);
    public static bool IsOre(int type) => Index(type) >= 0;
    public static IEnumerable<HardOreKind> Traits(SoulItem soul, int sourceWeapon)
    {
        int own = Index(sourceWeapon);
        if (own >= 0) yield return (HardOreKind)own;
        foreach (int kind in soul.Supports.Select(i => Index(i.type)).Where(i => i >= 0 && i != own).Distinct())
            yield return (HardOreKind)kind;
    }
    public static bool Enabled(SoulItem soul, int source, HardOreKind kind) =>
        soul.Active.Any(i => i.type == source) && Traits(soul, source).Contains(kind);
    public static string Description(int type) => Index(type) switch {
        0 => "快淬：同目标3次合法主命中发出40%基础伤害轻刃，60帧触发间隔",
        1 => "续锻：同目标5次合法主命中尝试回复0.5%最大生命（1至3点），180帧间隔；受治疗额度、吸血资源与禁疗限制",
        2 => "导刃：同目标4次合法主命中留下3秒印记，下一把其他主剑消费后本击增伤12%；单主剑允许下一趟消费",
        3 => "绽金：首次合法主命中发出3花瓣，合计30%基础伤害，120帧触发间隔",
        4 => "硬解：对防御至少30的目标，该来源主剑条件增伤12%",
        5 => "环钛：同目标4次合法主命中生成30%基础伤害钛片，随来源剑影环绕至多30帧，每来源只存1片",
        _ => ""
    };
}

public class HardOreTooltip : GlobalItem
{
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!HardOreTypes.IsOre(item.type)) return;
        tooltips.Add(new(Mod, "HardOreSoul", "剑魂寄宿·肉后矿石：" + HardOreTypes.Description(item.type)));
        tooltips.Add(new(Mod, "HardOreSource", "可放有效普通或支援槽；支援时按实际主剑分别计数与取伤害。\n连击每目标主体、每来源30帧计一次，3秒未合法命中清空；附属攻击不充能。"));
    }
}
