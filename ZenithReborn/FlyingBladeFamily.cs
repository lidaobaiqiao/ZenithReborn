using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 飞刃族名册：91项剑范围内、引擎核验无弹幕的纯挥砍剑（31把）。
// 动机：给寄宿后没有远程手段的剑补上属于自己的远程攻击方式，弹幕剑不入族。
// 名单依据 artifacts/flying-blade-candidates.json（引擎普查），非常规武器留置待特殊处理。
//
// 族级成长按"族内有效武器数量"分阶，门槛 1/2/4/6/8/10：
//   1 把：激活飞刃族的掷刃攻击。
//   2 把：族内每多一把，飞刃伤害再提高一档（有上限，避免全收集失衡）。
//   4 把：允许非本族主剑也使用飞刃攻击方式。
//   6 把：族特殊效果生效。当前只接管"收回期保留一半伤害"。
//   8 把、10 把：内容尚未定案，门槛先保留，暂不挂效果。
// 激活只看数量，不绑定任何特定武器，也不新增按键或独立操作。
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

    // 阶级门槛：族内有效武器数量达到这些值即进入下一阶（1/2/4/6/8/10）。
    // 激活只看数量，不绑定任何特定武器，也不新增按键或独立操作。
    public static readonly int[] Stages=[1,2,4,6,8,10];

    // 第2阶的加成幅度与计入上限：+3%/把，最多计 12 把（最高 +33%）。
    public const int StepPercent=3;
    public const int StepCap=12;

    public static bool IsMember(int type)=>Array.IndexOf(Weapons,type)>=0;
    public static int Count(SoulItem soul)=>soul.Attached?soul.TraitItems.Where(i=>IsMember(i.type)).Select(i=>i.type).Distinct().Count():0;

    // 当前阶级：0 表示族内还没有有效武器。主槽与支援槽都计入，与矿石族阶梯口径一致。
    public static int Stage(SoulItem soul)
    {
        int count=Count(soul),stage=0;
        for(int i=0;i<Stages.Length;i++) if(count>=Stages[i]) stage=i+1;
        return stage;
    }

    public static bool Activated(SoulItem soul)=>Stage(soul)>=1;   // 1 把：激活族
    public static bool Tally(SoulItem soul)=>Stage(soul)>=2;       // 2 把：族内数量加成
    public static bool Pass(SoulItem soul)=>Stage(soul)>=3;        // 4 把：跨族授权
    public static bool Sigil(SoulItem soul)=>Stage(soul)>=4;       // 6 把：族特殊效果

    // 第2把提供的伤害倍率；未寄宿时为 1，不改变原有数值。
    public static float DamageBonus(SoulItem soul)
    {
        if(!Tally(soul))return 1;
        int extra=Math.Clamp(Count(soul)-1,0,StepCap-1);
        return 1+StepPercent/100f*extra;
    }

    public static bool Enabled(SoulItem soul)=>soul.Attached && soul.TraitMains.Any() && Activated(soul);
}
