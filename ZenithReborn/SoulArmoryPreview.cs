using Terraria;

namespace ZenithReborn;

// 只对完整克隆尝试寄宿，复用真实校验与容量规则；预览不能登记正式收藏或改写玩家物品。
public sealed record SoulArmoryPreview(bool Allowed,string Reason,int DamageDelta,int CritDelta,int MainCount,int MainCapacity,int SupportCapacity)
{
    public static SoulArmoryPreview Evaluate(SoulItem source,int slot,Item candidate)
    {
        var copy=(SoulItem)source.Item.Clone().ModItem;
        Item incoming=candidate.Clone(),outgoing=source.ItemAt(slot);
        bool allowed=copy.TrySwapHost(slot,ref incoming,out string reason);
        return new(allowed,reason,candidate.damage-(outgoing.IsAir?0:outgoing.damage),
            candidate.crit-(outgoing.IsAir?0:outgoing.crit),copy.Active.Count(),
            copy.Capacity+copy.BonusCapacity+(copy.LegendaryOn?1:0),copy.SupportCapacity);
    }
}
