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
            int? damage=null;
            NPC Target(int index,Vector2 center)
            {
                var npc=new NPC();npc.SetDefaults(NPCID.Zombie);npc.active=true;npc.whoAmI=index;
                npc.width=npc.height=12;npc.Center=center;npc.life=npc.lifeMax=100000;npc.defense=0;
                npc.knockBackResist=0;npc.HideStrikeDamage=true;Main.npc[index]=npc;return npc;
            }
            void ClearImmunity(NPC npc){projectile.localNPCImmunity[npc.whoAmI]=0;Array.Clear(npc.immune);}
            for(int echo=1;echo<4;echo++)
            {
                var target=Target(20,centers[echo]);ClearImmunity(target);
                check(!blade.BodyCollides(target.Hitbox)&&blade.Colliding(projectile.Hitbox,target.Hitbox)==true,
                    "第"+echo+"个剑影位置在本体外且独立参与碰撞");
                Main.rand=new UnifiedRandom(7351);projectile.Damage();int dealt=100000-target.life;
                check(dealt>0,"原版Damage在第"+echo+"个剑影处造成实际伤害");
                damage??=dealt;
                check(dealt==damage,"剑影伤害不按亮度折扣，保留所属武器的完整伤害");
                int after=target.life;target.Center=centers[echo==3?1:echo+1];projectile.Damage();
                check(target.life==after&&projectile.localNPCImmunity[20]>0,"切换剑影不绕过同飞剑命中冷却");
            }
            var gap=Target(20,(centers[1]+centers[2])*.5f);ClearImmunity(gap);
            check(blade.Colliding(projectile.Hitbox,gap.Hitbox)==false,"剑影之间的空隙没有整片拖尾伤害");
            check(!blade.TryConsumeOreHit(),"本体和全部剑影共用已经消耗的矿石首击资格");
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
