using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 对照原版蝙蝠棍命中回血；仅用于独立服务端夹具。
internal static class LivingBatHealCheck
{
    private static (int damage,int healed,int sameSwing,int lifeMax) Trial(bool proxyMode,bool full,bool invalid)
    {
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.gameMenu=false;
        Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
        Main.screenPosition=Vector2.Zero;Main.screenWidth=1920;Main.screenHeight=1080;
        var owner=new Player{whoAmI=0,active=true,direction=1,position=new Vector2(1000,800)};
        Main.player[0]=owner;owner.ResetEffects();owner.statLifeMax=owner.statLifeMax2=100;owner.statLife=full?100:50;
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;target.immortal=invalid;
        target.life=target.lifeMax=100000;target.HideStrikeDamage=true;target.defense=0;target.noTileCollide=true;target.knockBackResist=0;
        target.Center=owner.Center+new Vector2(180,0);Main.npc[20]=target;
        Main.mouseX=(int)target.Center.X;Main.mouseY=(int)target.Center.Y;
        LivingArmament summon=null;var field=typeof(LivingArmament).GetField("proxy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        if(proxyMode)
        {
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.LegacyReleased=63;
            soul.Hosts[0]=new Item(ItemID.BatBat);soul.Hosts[1]=new Item(ItemID.EnchantedSword);
            owner.inventory[0]=soul.Item;owner.selectedItem=0;soul.RefreshStats();
            owner.GetModPlayer<LivingArmamentPlayer>().PostUpdate();
            Projectile shot=Main.projectile.First(p=>p.active&&p.ModProjectile is LivingArmament&&(int)p.ai[0]==0);
            shot.Center=target.Center-new Vector2(48,0);summon=(LivingArmament)shot.ModProjectile;
        }
        else
        {
            owner.inventory[0]=new Item(ItemID.BatBat);owner.selectedItem=0;owner.Center=target.Center-new Vector2(48,0);
            owner.lastVisualizedSelectedItem=owner.HeldItem.Clone();owner.controlUseItem=true;owner.releaseUseItem=true;
        }
        int before=owner.statLife,beforeTarget=target.life,first=-1,firstAnimation=-1,sameSwing=0;
        for(int tick=0;tick<150;tick++)
        {
            object instance=proxyMode?field.GetValue(summon):null;
            Player actor=proxyMode?(instance==null?null:(Player)instance.GetType().GetProperty("Player").GetValue(instance)):owner;
            if(actor!=null)
            {
                // 服务端不计算姿势；调用相同原版姿势函数以补齐命中矩形。
                Main.dedServ=false;
                try{actor.ItemCheck_ApplyUseStyle(0,actor.HeldItem,new Rectangle(0,0,32,32));}
                finally{Main.dedServ=true;}
            }
            target.immune[0]=0;
            if(proxyMode)summon.AI();else{owner.releaseUseItem=true;owner.ItemCheck();}
            if(first<0 && target.life<beforeTarget){first=owner.statLife;firstAnimation=actor?.itemAnimation??0;}
            else if(first>=0)
            {
                if(actor==null || actor.itemAnimation>firstAnimation || actor.itemAnimation<=0)break;
                sameSwing=owner.statLife-first;firstAnimation=actor.itemAnimation;
            }
        }
        return(beforeTarget-target.life,(first<0?owner.statLife:first)-before,sameSwing,owner.statLifeMax2);
    }
    internal static void Run(Action<bool,string> check)
    {
        // 独立服务器无字体资源；让浮字槽暂时占满，只屏蔽绘制，不改变 Heal 逻辑。
        var oldText=Main.combatText;
        Main.combatText=Enumerable.Range(0,100).Select(_=>new CombatText{active=true}).ToArray();
        try
        {
        var vanilla=Trial(false,false,false);var proxy=Trial(true,false,false);
        check(vanilla.damage>0&&vanilla.healed==1,"真人蝙蝠棍实际命中与回血基准");
        check(proxy.damage>0&&proxy.healed==1,"代理蝙蝠棍实际命中后真实玩家回血");
        check(proxy.sameSwing==0,"代理蝙蝠棍同一次挥击不重复回血");
        var full=Trial(true,true,false);check(full.damage>0&&full.healed==0,"满血时命中不超出生命上限");
        var invalid=Trial(true,false,true);check(invalid.damage==0&&invalid.healed==0,"无效目标不触发伤害或回血");
        var vanillaAgain=Trial(false,false,false);check(vanillaAgain.damage>0&&vanillaAgain.healed==1,"真实玩家普通使用不受代理转发影响");
        }
        finally{Main.combatText=oldText;}
    }
}
