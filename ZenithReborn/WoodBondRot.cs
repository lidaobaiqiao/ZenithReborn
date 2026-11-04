using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 腐林凋蚀 · 凋蚀释放（组合 B 机制，V2）。
// 派发入口：SoulBlade.OnHitNPC → WoodBondCore.ActiveRot 已确认激活后，
// 以魔光剑（ItemID.Muramasa）或永夜刃（ItemID.NightsEdge）来源主剑影调用本方法。
// 机制规格（V2）：
//  - 触发条件：主剑影命中带“本剑魂”施加的瘴毒（JungleToxin，红木木效果，WoodCombat 贡献通道 index 3）的目标；
//  - 数值：命中时提前结算该剑魂剩余自有 DoT 预算（瘴毒+烬燃通道的未结算总量）的 25%，
//    单次结算不超过 50%×D（D 为 WoodBondHit.D 本次命中实际伤害快照），结算量从该剑魂未来的预算中扣除；
//  - 冷却：同目标 180 帧冷却（WoodPlayer.Ready，键含目标根NPC Id）；
//  - 限制：结算不暴击、不产生新的 DoT、不产生新的主命中事件（不进入 SoulBlade 命中管道，防递归）；
//    目标瘴毒免疫（Poisoned buffImmune）不可绕过——免疫时 UpdateLifeRegen 已把通道清零，预算天然为 0，直接跳过。
// 多人执行模型：主剑影碰撞只在拥有者客户端发生，客户端只探测条件并上报（WoodBondRelay，action 12），
// 服务端凭自身权威贡献表重新校验并结算；客户端绝不本地结算。
internal static class WoodBondRot
{
    // 规格常量：结算25%、单次上限50%×D、同目标180帧冷却。
    private const float SettleRatio = 0.25f, SettleCap = 0.50f;
    private const int CooldownFrames = 180;

    public static void OnPrimaryHit(WoodBondHit hit, int identity)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            // 多人：拥有者客户端只探测条件并上报，不调 Ready、不结算（冷却不在此侧记账）。
            if (hit.Player.whoAmI != Main.myPlayer || !CanTrigger(hit)) return;
            WoodBondRelay.Send(WoodBondRelay.KindRot, hit, identity);
            return;
        }
        // 服务端：只认中继包入口（WoodBondRelay.Receive），本地命中不结算，避免双结算。
        if (Main.netMode == NetmodeID.Server) return;
        // 单人：直接本地执行。
        Execute(hit);
    }

    // 客户端条件探测：组合激活＋本剑魂瘴毒仍在（客户端贡献表由 SendExtraAI 同步，仅决定是否上报）。
    private static bool CanTrigger(WoodBondHit hit) =>
        WoodBondCore.ActiveRot(hit.Soul) &&
        WoodCombat.HasToxin(hit.Target, hit.Player.whoAmI, hit.Soul.SoulId);

    // 权威执行：单人本地与服务端中继共用。冷却记在执行侧（MP＝服务端，SP＝本地）的 WoodPlayer.Ready，
    // 客户端不调用 Ready，故不会出现两端各记一份导致冷却翻倍。
    public static void Execute(WoodBondHit hit)
    {
        string soul = hit.Soul.SoulId;
        if (!WoodBondCore.ActiveRot(hit.Soul)) return;
        // 触发条件：目标带本剑魂瘴毒（贡献表 index 3 持续中）。
        if (!WoodCombat.HasToxin(hit.Target, hit.Player.whoAmI, soul)) return;
        // 剩余自有 DoT 预算＝瘴毒 Poison×Time[3] + 烬燃 Fire×Time[6]（速率×剩余帧＝未结算总量，不含转移通道）。
        float budget = WoodCombat.OwnDotBudget(hit.Target, hit.Player.whoAmI, soul);
        // 结算量＝预算的 25%，单次不超过 50%×D（D 为本次主命中实际伤害快照）。
        float settle = MathF.Min(budget * SettleRatio, hit.D * SettleCap);
        if (settle <= 0) return;
        // 同目标 180 帧冷却：Ready 首次通过即开启窗口，窗口内后续命中直接短路。
        if (!hit.Player.GetModPlayer<WoodPlayer>().Ready(soul + ":bondrot:" +
                WoodCombat.Root(hit.Target).GetGlobalNPC<HardOreTarget>().Id, CooldownFrames)) return;
        // 结算后从两通道按预算占比等比扣除（折算算法见 WoodCombat.ConsumeOwnDot 中文注释）。
        WoodCombat.ConsumeOwnDot(hit.Target, hit.Player.whoAmI, soul, settle);
        // 直伤结算：StrikeNPC 直调（HitInfo 无参构造已默认不暴击/零击退/零方向），不产生新 DoT、不进 SoulBlade 命中管道，无递归。
        hit.Target.StrikeNPC(new NPC.HitInfo { Damage = Math.Max(1, (int)settle) });
    }
}
