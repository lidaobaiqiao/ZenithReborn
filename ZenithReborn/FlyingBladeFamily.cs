using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 飞刃族名册：91项剑范围内、引擎核验无弹幕的纯挥砍剑（31把）。
// 动机：给寄宿后没有远程手段的剑补上属于自己的远程攻击方式，弹幕剑不入族。
// 名单依据 artifacts/flying-blade-candidates.json（引擎普查），非常规武器留置待特殊处理。
public static class FlyingBladeFamily
{
    public static readonly int[] Weapons=[
        // 核心四把：启刃／叠刃／通刃／印刃，各自掌一层族级开关（见 FlyingBladeCores）。
        ModContent.ItemType<FlyingBladeSeed>(),ModContent.ItemType<FlyingBladeTally>(),
        ModContent.ItemType<FlyingBladePass>(),ModContent.ItemType<FlyingBladeSigil>(),
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
    public static bool IsMember(int type)=>Array.IndexOf(Weapons,type)>=0;
    public static int Count(SoulItem soul)=>soul.Attached?soul.TraitItems.Where(i=>IsMember(i.type)).Select(i=>i.type).Distinct().Count():0;
    public static bool Enabled(SoulItem soul)=>soul.Attached && soul.TraitMains.Any() && FlyingBladeCores.Seeded(soul);
}
