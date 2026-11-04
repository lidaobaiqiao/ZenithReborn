using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 圣林归冠判定（木质族 V2 组合 D）：8 种木剑全部有效寄宿（基础普通槽＋已生效支援槽＋森林/圣林奖励槽＋传奇位；
// 奖励位同样计入，不设例外）且泰拉刃有效寄宿于非圣林槽（CrownHosts 是归冠的奖励，
// 不得参与自身解锁——防自举；其余有效位均可）。
// 激活后 WoodLoadout.Crown 置位，Strength/Life 覆盖为 3.00 倍 / +100 生命，
// 并经 SoulItem.CrownCapacity 开启 35/36 两个圣林之冠真实主攻剑位。
internal static class WoodCrown
{
    // 防循环重入护盾：激活判定途中会经 SoulSupport.SetItems → Capacity → SoulItem.CrownCapacity
    // 再次进入本方法（圣林槽可贡献矿石名册从而影响支援容量，支援槽又影响木质计数）。
    // 重入时按未激活处理——恰好实现 7.4“圣林槽不得自举归冠”的语义：
    // 靠圣林槽矿石撑起的支援槽里放的木剑，不计入归冠自身的判定。
    // 游戏主循环单线程，静态标志无并发风险；try/finally 保证异常下也能复位。
    private static bool evaluating;

    public static bool Active(SoulItem soul)
    {
        if (!soul.Attached) return false;
        if (evaluating) return false;
        evaluating = true;
        try
        {
            // 木质遮罩：主攻槽＋有效支援槽＋传奇位（FamilyItems 语义），不含森林/圣林/冠位奖励槽。
            // 不调用 WoodTypes.Evaluate——Evaluate 会回调本方法形成循环；用 Mask/FamilyItems 直接算。
            if (WoodTypes.Mask(SoulSupport.FamilyItems(soul)) != 0xFF) return false;
            // 泰拉刃须寄宿于当前生效的非圣林槽：普通主槽、有效支援槽、
            // 森林奖励槽（矿石圆满开启时）或传奇槽。CrownHosts 被显式排除（防自举）。
            // 寄宿规则限定每种武器仅一把，存在性检查即完整语义。
            return soul.Hosts.Take(soul.Capacity).Concat(soul.Supports)
                .Concat(soul.ForestHosts.Take(soul.BonusCapacity))
                .Concat(soul.LegendaryOn && !soul.LegendaryHost.IsAir ? [soul.LegendaryHost] : [])
                .Any(i => !i.IsAir && i.type == ItemID.TerraBlade);
        }
        finally { evaluating = false; }
    }
}
