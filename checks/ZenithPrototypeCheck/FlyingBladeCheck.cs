using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 飞刃族夹具：名册边界、名册-普查一致性、激活条件、蓄能节奏、齐射扇形与弹幕三阶段。
// 独立服务端运行，不改玩家存档；实际画面、自然操作与多人不在本轮范围。
internal static class FlyingBladeCheck
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var p in Main.projectile)p.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.dayTime=true;
        var player=new Player{active=true,whoAmI=0,position=new Vector2(1000,1000)};Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        // 四把族核心可以放支援槽，因此夹具需解除全部诅咒，否则切换诅咒只放行首把主剑的特性。
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Seals=SoulCurses.All;
        player.inventory[0]=soul.Item;player.selectedItem=0;

        // 名册边界：抽验正反例——弹幕剑与非常规不入族。
        check(FlyingBladeFamily.IsMember(ItemID.BluePhaseblade)&&FlyingBladeFamily.IsMember(ItemID.OrangePhasesaber)&&
            FlyingBladeFamily.IsMember(ItemID.BloodButcherer)&&FlyingBladeFamily.IsMember(ItemID.FieryGreatsword)&&
            FlyingBladeFamily.IsMember(ItemID.Flymeal)&&FlyingBladeFamily.IsMember(ItemID.AntlionClaw)&&
            FlyingBladeFamily.IsMember(ItemID.Muramasa)&&FlyingBladeFamily.IsMember(ItemID.Keybrand),
            "可合成与掉落两组名册成员就位");
        check(!FlyingBladeFamily.IsMember(ItemID.LightsBane)&&!FlyingBladeFamily.IsMember(ItemID.EnchantedSword)&&
            !FlyingBladeFamily.IsMember(ItemID.Meowmere)&&!FlyingBladeFamily.IsMember(ItemID.IceSickle)&&
            !FlyingBladeFamily.IsMember(ItemID.Umbrella)&&!FlyingBladeFamily.IsMember(ItemID.FlyingKnife)&&
            FlyingBladeFamily.Weapons.Length==31,"弹幕剑与非常规不入族，名册31把");

        // 名册-普查一致性：名册每把在引擎中无弹幕且可寄宿，防手滑加错。
        var survey=new Item();bool clean=true;string bad="";
        foreach(int type in FlyingBladeFamily.Weapons)
        {
            survey.SetDefaults(type);
            if(survey.shoot!=0){clean=false;bad=$"{type} shoot={survey.shoot}";break;}
        }
        check(clean,$"名册全部无引擎弹幕{bad}");

        // 阶梯：名册前四把各带一层族级效果，顺序必须与名册前四位一致，否则四层效果会漂移。
        check(FlyingBladeFamily.Tiers.SequenceEqual(FlyingBladeFamily.Weapons.Take(4)),"阶梯四把与名册前四位一致");
        // 激活条件：族激活权交给名册第1把（蓝相位剑），只有族成员不再自动激活。
        // 注：阶梯剑放主槽即可；支援槽要木质/矿石族激活才开，本夹具没有那些族成员。
        soul.Hosts[0]=new Item(ItemID.BloodButcherer);soul.RefreshStats();
        check(FlyingBladeFamily.Count(soul)>=1&&!FlyingBladeFamily.Enabled(soul),"仅族成员、未寄宿名册第1把时不激活");
        soul.Hosts[0]=new Item(ItemID.BluePhaseblade);soul.RefreshStats();
        check(FlyingBladeFamily.Activated(soul)&&FlyingBladeFamily.Enabled(soul),"寄宿蓝相位剑后族激活");

        // 蓄能节奏：0.3秒（18帧）一把，弹容量=独行祝福上限10。虚拟时钟注入，不伪造全局状态。
        var hand=player.GetModPlayer<FlyingBladeHand>();
        uint tick=100;hand.Clock=()=>tick;
        hand.PostUpdate();
        check(hand.Loaded==1&&FlyingBladeHand.StackInterval==18,"首个蓄能周期叠出第一把");
        hand.PostUpdate();check(hand.Loaded==1,"同一周期内不重复叠层");
        tick=118;hand.PostUpdate();check(hand.Loaded==2,"间隔18帧后叠出第二把");
        for(tick=119;tick<=400;tick++)hand.PostUpdate();
        check(hand.Loaded==SoulCurses.Blades(soul),"蓄能至独行上限后待命");

        // 齐射：掷出手中全部飞刃，相邻间隔5°，速度一致，掷后清空。
        hand.Loaded=5;hand.AimOverride=player.Center+Vector2.UnitX*200;
        hand.Launch(soul);
        var shots=Main.projectile.Where(p=>p.active&&p.ModProjectile is FlyingBladeShot).ToArray();
        check(shots.Length==5,"齐射掷出手中全部飞刃");
        check(hand.Loaded==0&&hand.NextStack==0,"掷出后清空重新蓄能");
        bool fan=true;
        for(int i=1;i<shots.Length;i++)
        {
            float a0=(float)Math.Atan2(shots[i-1].velocity.Y,shots[i-1].velocity.X);
            float a1=(float)Math.Atan2(shots[i].velocity.Y,shots[i].velocity.X);
            if(Math.Abs(MathHelper.ToDegrees(a1-a0)-FlyingBladeHand.SpreadDegrees)>0.01f)fan=false;
        }
        check(fan&&shots.All(p=>p.velocity.Length()==FlyingBladeShot.LaunchSpeed),"相邻飞刃相隔5°且速度一致");

        // 三阶段：直飞满射程转旋转，45帧后转入无伤害收回。
        // 手动调用 AI() 只跑行为逻辑，位置积分由真实 Update() 完成；夹具中手动补积分模拟位移。
        var shot=(FlyingBladeShot)shots[0].ModProjectile;
        for(int i=0;i<60;i++){shot.AI();shots[0].position+=shots[0].velocity;}
        check(shots[0].velocity==Vector2.Zero,"直飞600像素后进入旋转");
        for(int i=0;i<FlyingBladeShot.RotationFrames;i++){shot.AI();shots[0].position+=shots[0].velocity;}
        check(shots[0].ModProjectile is FlyingBladeShot,"旋转45帧后弹幕存活");
        bool returning=false;
        for(int i=0;i<3;i++){shot.AI();shots[0].position+=shots[0].velocity;returning|=shots[0].velocity.Length()>0;}
        check(returning,$"旋转结束转入收回阶段[{shot.DebugState}]");
        check(shot.CanHitNPC(null)==false,"收回途中不再造成伤害");
        check(FlyingBladeShot.MaxRange==600f&&FlyingBladeShot.RotationFrames==45&&
            FlyingBladeShot.RotationDamage==.5f&&FlyingBladeShot.LaunchSpeed==12f,"射程、旋转周期、持续伤害与速度为暂定初值");

        // 阶梯四层：名册第1把已在激活段验证，其余三把逐条验证。
        // 第2把（红相位剑）：族内武器越多飞刃伤害越高，且有上限。
        soul.Hosts[1]=new Item(ItemID.RedPhaseblade);soul.RefreshStats();
        int tallyCount=FlyingBladeFamily.Count(soul);
        float expected=1+FlyingBladeFamily.StepPercent/100f*Math.Min(tallyCount-1,FlyingBladeFamily.StepCap-1);
        check(FlyingBladeFamily.Tally(soul)&&Math.Abs(FlyingBladeFamily.DamageBonus(soul)-expected)<0.0001f,
            $"红相位剑按族内武器数放大飞刃伤害（{tallyCount}把→×{expected:F2}）");

        // 第3把（绿相位剑）：非本族主剑也能作为飞刃载体。
        foreach(var p in Main.projectile)p.active=false;
        soul.Hosts[2]=new Item(ItemID.GreenPhaseblade);
        soul.Hosts[4]=new Item(ItemID.WoodenSword);soul.RefreshStats();
        hand.Loaded=6;hand.Launch(soul);
        var hosts=Main.projectile.Where(p=>p.active&&p.ModProjectile is FlyingBladeShot).Select(p=>(int)p.ai[0]).ToArray();
        check(FlyingBladeFamily.Pass(soul)&&hosts.Contains(ItemID.WoodenSword),"绿相位剑让非本族主剑也能掷出飞刃");

        // 第4把（紫相位剑）：把收回期伤害开关写进弹幕，并让收回期仍可命中。
        foreach(var p in Main.projectile)p.active=false;
        soul.Hosts[3]=new Item(ItemID.PurplePhaseblade);soul.RefreshStats();
        hand.Loaded=1;hand.Launch(soul);
        var sigilShot=Main.projectile.First(p=>p.active&&p.ModProjectile is FlyingBladeShot);
        var sigilMod=(FlyingBladeShot)sigilShot.ModProjectile;
        check(FlyingBladeFamily.Sigil(soul)&&sigilShot.ai[1]==1f,"紫相位剑把收回期伤害开关写进弹幕");
        for(int i=0;i<60+FlyingBladeShot.RotationFrames+3;i++){sigilMod.AI();sigilShot.position+=sigilShot.velocity;}
        check(sigilShot.damage>0&&sigilMod.CanHitNPC(null)!=false,"紫相位剑让收回期保留伤害并可命中");
    }
}
