using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 暂定候选名册；激活后作用于所有有效出战槽，而非仅作用于本族成员。
public static class LivingArmamentFamily
{
    private static readonly int[] Members=[ItemID.EnchantedSword,ItemID.FlyingKnife,ItemID.InfluxWaver,ItemID.TheHorsemansBlade];
    public static bool IsMember(int type)=>Array.IndexOf(Members,type)>=0;
    public static int Count(SoulItem soul)=>soul.Attached?soul.TraitItems.Where(i=>IsMember(i.type)).Select(i=>i.type).Distinct().Count():0;
    public static bool Enabled(SoulItem soul)=>soul.Attached && soul.TraitMains.Any() && Count(soul)>0;
}
