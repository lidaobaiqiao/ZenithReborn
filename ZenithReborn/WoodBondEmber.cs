using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 灰烬繁生 · 焚叶（组合 C 机制，V2）。
// 派发入口：SoulBlade.OnHitNPC → WoodBondCore.ActiveEmber 已确认激活后，
// 以火山（ItemID.FieryGreatsword，本项目火山剑常量约定，见 FlyingBladeFamily）来源主剑影调用本方法。
// 机制规格（V2）：
//  - 触发条件：主剑影命中同时带“本剑魂”瘴毒（JungleToxin，通道 index 3）与烬燃
//    （EmberBurn，白蜡木效果，通道 index 6）的目标；
//  - 数值：触发 64 像素半径爆发，对范围内敌人造成 30%×D（D 为 WoodBondHit.D 本次命中实际伤害快照），
//    每次爆发最多命中 3 个不同敌人、每个敌人仅命中一次；
//  - 冷却：同剑魂 180 帧冷却（按玩家+剑魂记，WoodPlayer.Ready 键 "{SoulId}:bondember"）；
//  - 限制：爆发为直伤（StrikeNPC 单独结算），不重新施加瘴毒/烬燃，
//    不发射 SoulBlade 或派生弹幕、不产生新的主命中事件（不进入 SoulBlade 命中管道，防递归）。
// 多人执行模型：主剑影碰撞只在拥有者客户端发生，客户端只探测条件并上报（WoodBondRelay，action 12），
// 服务端用自身的 NPC 列表与 SoulItem 状态重判后爆发；客户端绝不本地结算。
internal static class WoodBondEmber
{
    // 规格常量：64像素半径、30%×D、最多3个目标、同剑魂180帧冷却。
    private const float Radius = 64f, DamageRatio = 0.30f;
    private const int MaxTargets = 3, CooldownFrames = 180;

    public static void OnPrimaryHit(WoodBondHit hit, int identity)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            // 多人：拥有者客户端只探测条件并上报，不调 Ready、不结算（冷却不在此侧记账）。
            if (hit.Player.whoAmI != Main.myPlayer || !CanTrigger(hit)) return;
            WoodBondRelay.Send(WoodBondRelay.KindEmber, hit, identity);
            return;
        }
        // 服务端：只认中继包入口（WoodBondRelay.Receive），本地命中不结算，避免双结算。
        if (Main.netMode == NetmodeID.Server) return;
        // 单人：直接本地执行。
        Execute(hit);
    }

    // 客户端条件探测：组合激活＋本剑魂瘴毒与烬燃同时在（客户端贡献表由 SendExtraAI 同步，仅决定是否上报）。
    private static bool CanTrigger(WoodBondHit hit) =>
        WoodBondCore.ActiveEmber(hit.Soul) &&
        WoodCombat.HasToxinAndEmber(hit.Target, hit.Player.whoAmI, hit.Soul.SoulId);

    // 权威执行：单人本地与服务端中继共用。冷却记在执行侧（MP＝服务端，SP＝本地）的 WoodPlayer.Ready，
    // 客户端不调用 Ready，故不会出现两端各记一份导致冷却翻倍。
    public static void Execute(WoodBondHit hit)
    {
        string soul = hit.Soul.SoulId;
        if (!WoodBondCore.ActiveEmber(hit.Soul)) return;
        // 触发条件：目标同带本剑魂瘴毒（index 3）与烬燃（index 6）。
        if (!WoodCombat.HasToxinAndEmber(hit.Target, hit.Player.whoAmI, soul)) return;
        // 同剑魂 180 帧触发冷却（按玩家+剑魂记，键不含目标）：Ready 首次通过即开启窗口。
        if (!hit.Player.GetModPlayer<WoodPlayer>().Ready(soul + ":bondember", CooldownFrames)) return;
        int damage = Math.Max(1, (int)(hit.D * DamageRatio));
        // 64 像素半径爆发：触发目标必入选（含触发目标，每个敌人每次爆发至多一跳）；
        // 其余按 Main.npc 顺序补满 3 个名额，按根NPC的 HardOreTarget.Id 去重——蠕虫多节段视为同一敌人。
        long rootId = WoodCombat.Root(hit.Target).GetGlobalNPC<HardOreTarget>().Id;
        List<NPC> victims = [hit.Target];
        foreach (NPC npc in Main.npc)
        {
            if (npc == hit.Target) continue;
            if (!HardOrePlayer.ValidTarget(npc)) continue;
            if (WoodCombat.Root(npc).GetGlobalNPC<HardOreTarget>().Id == rootId) continue;
            if (Vector2.Distance(npc.Center, hit.Target.Center) > Radius) continue;
            victims.Add(npc);
            if (victims.Count >= MaxTargets) break;
        }
        // 直伤路径：StrikeNPC 逐目标直调（HitInfo 无参构造已默认不暴击/零击退/零方向），不重施加瘴毒/烬燃、不发射弹幕，无递归。
        foreach (NPC victim in victims) victim.StrikeNPC(new NPC.HitInfo { Damage = damage });
    }
}
