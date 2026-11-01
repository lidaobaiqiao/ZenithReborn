using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 飞刃族名册：91项剑范围内、引擎核验无弹幕的纯挥砍剑（31把）。
// 动机：给寄宿后没有远程手段的剑补上属于自己的远程攻击方式，弹幕剑不入族。
// 名单依据 artifacts/flying-blade-candidates.json（引擎普查），非常规武器留置待特殊处理。
//
// 阶梯：名册前四把各带一层族级特殊效果（按名册顺序，改动顺序会让效果跟着漂移）。
//   第1把 蓝相位剑：激活飞刃族的掷刃攻击。没有它，族成员寄宿也不掷刃。
//   第2把 红相位剑：族内每多一把有效武器，飞刃伤害再提高一档（有上限，避免全收集失衡）。
//   第3把 绿相位剑：允许非本族主剑也使用飞刃攻击方式。
//   第4把 紫相位剑：族特殊效果生效。当前接管"收回期保留一半伤害"，印记定稿后并入此处。
public static class FlyingBladeFamily
{
    public static readonly int[] Weapons=[
        // 可合成组：陨石光剑7色、晶光刃7色、血腥屠刀、火山、飞虫剑
        ItemID.BluePhaseblade,ItemID.RedPhaseblade,ItemID.GreenPhaseblade,ItemID.PurplePhaseblade,
        ItemID.WhitePhaseblade,ItemID.YellowPhaseblade,ItemID.OrangePhaseblade,
        ItemID.BluePhasesaber,ItemID.RedPhasesaber,ItemID.GreenPhasesaber,ItemID.PurplePhasesaber,
        ItemID.WhitePhasesaber,ItemID.YellowPhasesaber,ItemID.OrangePhasesaber,
        ItemID.BloodButcherer,ItemID.FieryGreatsword,ItemID.Flymeal,
        // 仅购买/掉落组：常规刀剑14把
        ItemID.AntlionClaw,ItemID.BoneSword,ItemID.CandyCaneSword,ItemID.Katana,ItemID.Muramasa,
        ItemID.DyeTradersScimitar,ItemID.BeeKeeper,ItemID.FalconBlade,ItemID.DD2SquireDemonSword,
        ItemID.BreakerBlade,ItemID.Cutlass,ItemID.PsychoKnife,ItemID.Keybrand,ItemID.Bladetongue];

    // 阶梯四把，必须与名册前四位一一对应（夹具里有断言守着）。
    public static readonly int[] Tiers=[
        ItemID.BluePhaseblade,ItemID.RedPhaseblade,ItemID.GreenPhaseblade,ItemID.PurplePhaseblade];

    // 第2把的加成幅度与计入上限：+3%/把，最多计 12 把（最高 +33%）。
    public const int StepPercent=3;
    public const int StepCap=12;

    public static bool IsMember(int type)=>Array.IndexOf(Weapons,type)>=0;
    public static int Count(SoulItem soul)=>soul.Attached?soul.TraitItems.Where(i=>IsMember(i.type)).Select(i=>i.type).Distinct().Count():0;

    // 某一把阶梯剑是否有效寄宿（主槽与支援槽都算，与矿石族阶梯口径一致）。
    public static bool TierHeld(SoulItem soul,int tier)=>
        soul.Attached && tier>=0 && tier<Tiers.Length && soul.TraitItems.Any(i=>i.type==Tiers[tier]);

    public static bool Activated(SoulItem soul)=>TierHeld(soul,0);   // 第1把：激活族
    public static bool Tally(SoulItem soul)=>TierHeld(soul,1);       // 第2把：族内数量加成
    public static bool Pass(SoulItem soul)=>TierHeld(soul,2);        // 第3把：跨族授权
    public static bool Sigil(SoulItem soul)=>TierHeld(soul,3);       // 第4把：族特殊效果

    // 第2把提供的伤害倍率；未寄宿时为 1，不改变原有数值。
    public static float DamageBonus(SoulItem soul)
    {
        if(!Tally(soul))return 1;
        int extra=Math.Clamp(Count(soul)-1,0,StepCap-1);
        return 1+StepPercent/100f*extra;
    }

    public static bool Enabled(SoulItem soul)=>soul.Attached && soul.TraitMains.Any() && Activated(soul);
}
