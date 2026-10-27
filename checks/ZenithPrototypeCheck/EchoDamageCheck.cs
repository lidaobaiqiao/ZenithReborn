using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class EchoDamageCheck
{
    internal static void Run(Action<bool,string> check)
    {
        bool server=Main.dedServ,menu=Main.gameMenu;
        try
        {
            // 先在无显卡的服务器路径记录历史；之后由原版Damage处理真实NPC命中与免疫。
            Main.dedServ=true;Main.gameMenu=false;Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;
            foreach(var p in Main.projectile)p.active=false;
            foreach(var n in Main.npc)n.active=false;
            var player=new Player{whoAmI=0,active=true,position=new(1000,1000)};
            Main.player[0]=player;player.ResetEffects();
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.LegacyReleased=63;soul.Hosts[0]=new Item(ItemID.CopperBroadsword);soul.RefreshStats();
            player.inventory[0]=soul.Item;
            int id=soul.EmitBlade(player,soul.Hosts[0],0,1);
            var projectile=Main.projectile[id];var blade=(SoulBlade)projectile.ModProjectile;
            projectile.damage=projectile.originalDamage=blade.SpecialDamage=100;
            blade.TargetOffset=new(360,0);blade.Vertical=60;projectile.CritChance=0;
            while(blade.MotionPhase<77)blade.AI();
            check(!blade.BodyCanDamage&&blade.CanDamage()!=false,"本体停伤后可见剑影仍有攻击资格");
            var centers=new Vector2[4];
            for(int echo=1;echo<4;echo++)
                check(blade.TryGetEchoPose(echo,out centers[echo],out _,out _),"无显卡路径记录第"+echo+"个实际剑影");
            Main.netMode=NetmodeID.Server;
            check(blade.CanDamage()!=false&&Enumerable.Range(1,3).All(i=>blade.TryGetEchoPose(i,out _,out _,out _)),
                "专用服务端模式能够查询相同的三个剑影攻击姿态");
            Main.netMode=NetmodeID.SinglePlayer;
            int fullDamage=0;
            var actual=new List<object>();
            NPC Target(int index,Vector2 center)
            {
                var npc=new NPC();npc.SetDefaults(NPCID.Zombie);npc.active=true;npc.whoAmI=index;
                npc.width=npc.height=12;npc.Center=center;npc.life=npc.lifeMax=100000;npc.defense=0;
                npc.knockBackResist=0;npc.HideStrikeDamage=true;Main.npc[index]=npc;return npc;
            }
            void ClearImmunity(NPC npc){projectile.localNPCImmunity[npc.whoAmI]=0;Array.Clear(npc.immune);}
            foreach(var stage in new[]{CurseStage.Blessed,CurseStage.Released,CurseStage.Cursed,CurseStage.Weakened})
            {
                soul.LegacyReleased=63&~(1<<(int)SoulCurse.Bluntness);
                soul.Seals=stage switch {CurseStage.Blessed=>SoulCurses.Flesh|SoulCurses.Queen|SoulCurses.Empress,
                    CurseStage.Released=>SoulCurses.Flesh|SoulCurses.Queen,CurseStage.Weakened=>SoulCurses.Flesh,_=>0};
                for(int echo=0;echo<4;echo++)
                {
                    projectile.localAI[0]=echo==0?40:77;
                    var target=Target(20,echo==0?projectile.Center:centers[echo]);ClearImmunity(target);
                    check(blade.ResolveHitUnit(target.Hitbox)==echo,"实际碰撞单元对应本体或第"+echo+"个剑影");
                    Main.rand=new UnifiedRandom(7351);projectile.Damage();int dealt=100000-target.life;
                    if(fullDamage==0)fullDamage=dealt;
                    float expected=echo==0||stage==CurseStage.Blessed?1:stage==CurseStage.Released?MathF.Pow(.8f,echo):.2f;
                    check(dealt>0 && Math.Abs(dealt-fullDamage*expected)<=1,"原生Damage验证"+stage+"第"+echo+"单元比例"+expected);
                    actual.Add(new {stage=stage.ToString(),unit=echo,damage=dealt,expectedMultiplier=expected,fullDamage});
                    int after=target.life;target.Center=centers[echo==3?1:echo+1];projectile.Damage();
                    check(target.life==after&&projectile.localNPCImmunity[20]>0,"切换剑影不绕过同飞剑命中冷却");
                }
            }
            string report=Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
            if(!string.IsNullOrEmpty(report))System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report),"echo-native-damage-0.3.105.json"),
                System.Text.Json.JsonSerializer.Serialize(actual,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            projectile.localAI[0]=77;
            var gap=Target(20,(centers[1]+centers[2])*.5f);ClearImmunity(gap);
            check(blade.Colliding(projectile.Hitbox,gap.Hitbox)==false,"剑影之间的空隙没有整片拖尾伤害");
            check(Enumerable.Range(0,4).All(i=>!blade.TryConsumeOreHit(i)),"本体与三个剑影各自已经消耗矿石首击资格");
            soul.LegacyReleased=0;soul.Kills=0;soul.Seals=0;
            var capped=Target(21,centers[1]);
            for(int i=0;i<3;i++){ClearImmunity(capped);projectile.Damage();}
            int cappedLife=capped.life;capped.Center=centers[2];ClearImmunity(capped);projectile.Damage();
            check(cappedLife<100000&&capped.life==cappedLife,"三个剑影共用锋钝诅咒的每目标命中上限");
            projectile.localAI[0]=114;
            check(blade.CanDamage()==false&&blade.Colliding(projectile.Hitbox,capped.Hitbox)==false,
                "全部剑影熄灭后剩余消散材质没有伤害");
            projectile.localAI[0]=77;blade.Retire();
            check(blade.CanDamage()==false&&blade.Colliding(projectile.Hitbox,capped.Hitbox)==false,
                "提前归鞘立即关闭本体与所有剑影伤害");
        }
        finally
        {
            foreach(var p in Main.projectile)p.active=false;
            foreach(var n in Main.npc)n.active=false;
            Main.dedServ=server;Main.gameMenu=menu;
        }
    }
}
