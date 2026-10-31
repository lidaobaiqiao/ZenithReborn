using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 种族名册与战斗触发分开；珍珠木剑同时保留木质身份。
public static class HolyHeavenFamily
{
    public static readonly int[] Weapons=[ItemID.PearlwoodSword,ItemID.Starfury,ItemID.Excalibur,
        ItemID.TrueExcalibur,ItemID.PiercingStarlight,ItemID.StarWrath,ItemID.SolarEruption,ItemID.DayBreak];
    public static bool IsMember(int type)=>Array.IndexOf(Weapons,type)>=0;
    public static int Count(SoulItem soul)=>soul.Attached?soul.TraitItems.Where(i=>IsMember(i.type)).Select(i=>i.type).Distinct().Count():0;
    public static bool Enabled(SoulItem soul)=>soul.Attached && soul.TraitMains.Any() && Count(soul)>0;
}
