using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 木质族 V2 复合羁绊：组合求值与命中上下文。
// 求值规则（V2 规格，每次调用实时直算，无缓存——槽位/寄宿变动即时反映）：
//  - 木质计数（各组合门槛的分子）＝主攻槽＋已生效支援槽＋森林/圣林奖励槽＋传奇位
//    （SoulSupport.FamilyItems 语义，等价于 Active∪Supports）；奖励位本身是给玩家的回报，不设例外；
//  - “有效寄宿”与计数同源：武器的存在性判定同样看这一整套有效位（普通主槽、有效支援槽、
//    森林奖励槽、圣林槽或传奇槽）。两者外延一致，不再区分「计数」与「寄宿面」。
//    D 圣林归冠是唯一例外：圣林槽不能参与它自己解锁所需的 8/8（那是 CrownCapacity 的门槛规则，
//    见 WoodCrown 的重入护盾，不是计数口径的例外）。
//  - 同一武器重复寄宿不叠加：寄宿规则本身限定每种武器仅一把（SoulItem.TrySwapHost 校验），
//    故各判定只做存在性检查，天然不叠加。
internal static class WoodBondCore
{
    // 木质遮罩：与 WoodTypes.Evaluate 同源的主攻＋有效支援＋森林/圣林奖励＋传奇位计数，奖励位不设例外。
    private static int WoodMask(SoulItem soul) => soul.Attached ? WoodTypes.Mask(SoulSupport.FamilyItems(soul)) : 0;

    // 有效寄宿存在性：当前生效的任意槽位（普通/支援/森林/圣林/传奇）中出现该武器即成立。
    // 与 WoodMask 同源（FamilyItems＝Active∪Supports），故这里直接复用同一集合。
    // Active＝生效普通槽＋森林奖励＋圣林槽＋传奇槽，Supports＝生效支援槽，两者并集即全部有效槽。
    private static bool Hosted(SoulItem soul, int type) =>
        soul.Active.Concat(soul.Supports).Any(i => !i.IsAir && i.type == type);

    // A 锻根共生：木质≥第三阶段（≥4种木剑，基础＋支援槽计数）且含棕榈木剑，
    // 铜阔剑与铁阔剑同时有效寄宿（任意有效槽均可，含森林/圣林奖励槽——7.4 只禁圣林槽自举归冠）。
    public static bool ActiveForge(SoulItem soul)
    {
        if (!soul.Attached) return false;
        int mask = WoodMask(soul);
        return System.Numerics.BitOperations.PopCount((uint)mask) >= 4 &&
            (mask & (1 << (int)WoodKind.Palm)) != 0 &&
            Hosted(soul, ItemID.CopperBroadsword) && Hosted(soul, ItemID.IronBroadsword);
    }

    // B 腐林凋蚀：木质≥第四阶段（≥6种）且含红木剑与乌木剑，魔光剑与永夜刃同时有效寄宿。
    public static bool ActiveRot(SoulItem soul)
    {
        if (!soul.Attached) return false;
        int mask = WoodMask(soul);
        return System.Numerics.BitOperations.PopCount((uint)mask) >= 6 &&
            (mask & (1 << (int)WoodKind.Mahogany)) != 0 && (mask & (1 << (int)WoodKind.Ebon)) != 0 &&
            Hosted(soul, ItemID.Muramasa) && Hosted(soul, ItemID.NightsEdge);
    }

    // C 灰烬繁生：木质≥第四阶段（≥6种）且含红木剑与白蜡木剑，火山有效寄宿。
    public static bool ActiveEmber(SoulItem soul)
    {
        if (!soul.Attached) return false;
        int mask = WoodMask(soul);
        return System.Numerics.BitOperations.PopCount((uint)mask) >= 6 &&
            (mask & (1 << (int)WoodKind.Mahogany)) != 0 && (mask & (1 << (int)WoodKind.Ash)) != 0 &&
            Hosted(soul, ItemID.FieryGreatsword);
    }

    // D 圣林归冠：8木 Full＋泰拉刃有效寄宿于非圣林槽（CrownHosts 不得参与自身解锁，防自举）。
    // 与槽位引擎共用 WoodCrown.Active 单一判定源；防循环：WoodCrown 用 FamilyItems/mask 直接算，
    // 不回调本类，也不调用 WoodTypes.Evaluate（Evaluate 会调 WoodCrown.Active）。
    public static bool ActiveCrown(SoulItem soul) => WoodCrown.Active(soul);
}

// 木质羁绊命中上下文：由 SoulBlade.OnHitNPC 在 WoodCombat.FinishPrimaryHit 之后构造并派发。
// 7.5 统一来源规则：
//  - 来源武器、槽位、主剑影序号一律读取弹幕携带的 SoulSource（发射时由 SoulItem.EmitBlade 写入），
//    不从当前持物或寄宿快照反推，保证命中与效果同源；
//  - D 为本次命中实际造成的伤害快照（OnHitNPC 的 damageDone），衍生补斩/爆发按比例取 D，不回读面板伤害；
//  - 仅 PrimaryOrdinal>=0 的真主命中进入派发；附属弹幕（ReleaseSpecial 生成的子弹等）不参与；
//  - 衍生伤害（补斩/爆发/提前结算）必须走直伤路径，不产生新的 SoulBlade 命中事件，杜绝递归。
public readonly record struct WoodBondHit(
    Player Player,       // 触发玩家（弹幕 owner）
    SoulItem Soul,      // 来源剑魂
    int SourceWeapon,    // 来源武器 ItemID（SoulSource.SourceWeapon）
    int HostSlot,        // 来源主攻槽位编号（SoulSource.HostSlot，-1 表示未知）
    int PrimaryOrdinal,  // 主剑影序号（>=0 为真主命中）
    NPC Target,          // 命中目标
    float D,             // 本次命中实际伤害快照
    bool IsPrimary)      // 是否主命中（派发入口恒为 true；衍生伤害不得以主命中身份回流）
{ }
