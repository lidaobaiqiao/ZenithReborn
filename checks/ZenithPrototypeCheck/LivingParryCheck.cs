using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 对照真人与剑灵代理的真实近战命中和一次性招架增益。
internal static class LivingParryCheck
{
    private static (int first,int second,bool flag,bool buff,bool restored) Trial(bool useProxy,bool boost,bool invalid)
    {
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
        Main.rand=new UnifiedRandom(166);Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.gameMenu=false;
        Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
        Main.screenPosition=Vector2.Zero;Main.screenWidth=1920;Main.screenHeight=1080;
        Main.mouseX=800;Main.mouseY=600;
        var owner=new Player{whoAmI=0,active=true,direction=1,position=new Vector2(1000,800)};
        Main.player[0]=owner;owner.ResetEffects();owner.GetCritChance(DamageClass.Melee)=-100;
        if(boost){owner.buffType[0]=BuffID.ParryDamageBuff;owner.buffTime[0]=600;owner.parryDamageBuff=true;}
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;target.immortal=invalid;
        target.life=target.lifeMax=100000;target.HideStrikeDamage=true;target.defense=0;target.noTileCollide=true;target.knockBackResist=0;
        target.Center=owner.Center+new Vector2(180,0);Main.npc[20]=target;
        LivingArmament summon=null;
        var field=typeof(LivingArmament).GetField("proxy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        if(useProxy)
        {
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.LegacyReleased=63;
            soul.Hosts[0]=new Item(ItemID.CopperBroadsword);soul.Hosts[1]=new Item(ItemID.EnchantedSword);
            owner.inventory[0]=soul.Item;owner.selectedItem=0;soul.RefreshStats();
            owner.GetModPlayer<LivingArmamentPlayer>().PostUpdate();
            Projectile p=Main.projectile.First(q=>q.active&&q.ModProjectile is LivingArmament&&(int)q.ai[0]==0);
            p.Center=target.Center-new Vector2(48,0);summon=(LivingArmament)p.ModProjectile;
        }
        else
        {
            owner.inventory[0]=new Item(ItemID.CopperBroadsword);owner.selectedItem=0;
            owner.Center=target.Center-new Vector2(48,0);owner.lastVisualizedSelectedItem=owner.HeldItem.Clone();
            owner.controlUseItem=true;owner.releaseUseItem=true;Main.mouseX=(int)target.Center.X;Main.mouseY=(int)target.Center.Y;
        }
        int oldX=Main.mouseX,oldY=Main.mouseY,first=0,second=0;
        for(int tick=0;tick<150;tick++)
        {
            object instance=useProxy?field.GetValue(summon):null;
            Player actor=useProxy?(instance==null?null:(Player)instance.GetType().GetProperty("Player").GetValue(instance)):owner;
            if(actor!=null)
            {
                // 专用服务器跳过玩家挥击姿势；夹具调用同一原版函数补齐命中矩形。
                Main.dedServ=false;
                try{actor.ItemCheck_ApplyUseStyle(0,actor.HeldItem,new Rectangle(0,0,32,32));}
                finally{Main.dedServ=true;}
            }
            int before=target.life;target.immune[0]=0;
            if(useProxy)summon.AI();else{owner.releaseUseItem=true;owner.ItemCheck();}
            int damage=before-target.life;
            if(damage>0){if(first==0)first=damage;else{second=damage;break;}}
        }
        return(first,second,owner.parryDamageBuff,owner.HasBuff(BuffID.ParryDamageBuff),
            ReferenceEquals(Main.player[0],owner)&&Main.mouseX==oldX&&Main.mouseY==oldY);
    }
    internal static void Run(Action<bool,string> check)
    {
        // 无窗口服务器没有浮字字体；不改变伤害和招架结算。
        var oldText=Main.combatText;var oldTiles=Main.tile;int oldMaxX=Main.maxTilesX,oldMaxY=Main.maxTilesY;
        Main.combatText=Enumerable.Range(0,100).Select(_=>new CombatText{active=true}).ToArray();
        try
        {
            // 独立创建有效世界，避免依赖其他夹具留下的瓦片状态。
            Main.maxTilesX=200;Main.maxTilesY=200;
            Main.tile=(Tilemap)Activator.CreateInstance(typeof(Tilemap),System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,new object[]{(ushort)200,(ushort)200},null);
            var normal=Trial(false,false,false);var actual=Trial(false,true,false);
            var unboosted=Trial(true,false,false);var proxy=Trial(true,true,false);var invalid=Trial(true,true,true);
            check(normal.first>0&&actual.first>=normal.first*3&&!actual.flag&&!actual.buff,"真人招架命中增伤且消费状态");
            check(unboosted.first>0&&proxy.first>=unboosted.first*3&&!proxy.flag&&!proxy.buff,"代理招架命中增伤并消费真实玩家状态");
            check(proxy.second>0&&proxy.first>=proxy.second*3,"代理招架加成只应用一次");
            check(invalid.first==0&&invalid.flag&&invalid.buff,"没有有效命中时保留招架状态");
            check(proxy.restored&&invalid.restored,"代理执行后恢复真人与鼠标");
        }
        finally{Main.combatText=oldText;Main.tile=oldTiles;Main.maxTilesX=oldMaxX;Main.maxTilesY=oldMaxY;}
    }
}
