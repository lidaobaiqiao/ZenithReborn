using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 验证真实出战槽驱动召唤体和原版玩家攻击入口。
internal static class LivingArmamentCheck
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.gameMenu=false;
        Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
        Main.screenPosition=Vector2.Zero;Main.screenWidth=1920;Main.screenHeight=1080;
        Main.mouseX=400;Main.mouseY=400;
        var owner=new Player{whoAmI=0,active=true,direction=1,position=new Vector2(1000,800)};
        Main.player[0]=owner;owner.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.LegacyReleased=63;
        soul.Hosts[0]=new Item(ItemID.EnchantedSword);soul.Hosts[1]=new Item(ItemID.Seedler);
        soul.Hosts[2]=new Item(ItemID.Starfury);soul.Hosts[3]=new Item(ItemID.CopperBroadsword);
        soul.Hosts[4]=new Item(ItemID.WoodenSword);soul.Hosts[5]=new Item(ItemID.FlyingKnife);
        owner.inventory[0]=soul.Item;owner.selectedItem=0;soul.RefreshStats();
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
        target.life=target.lifeMax=100000;target.HideStrikeDamage=true;target.defense=0;target.noTileCollide=true;
        target.Center=owner.Center+new Vector2(180,0);Main.npc[20]=target;
        check(LivingArmamentFamily.Enabled(soul)&&HolyHeavenFamily.Enabled(soul)&&OreFamily.Count(soul)>0&&soul.CombatWood.Count>0,
            "新种族与神圣、矿石、木质同时生效");
        var controller=owner.GetModPlayer<LivingArmamentPlayer>();controller.PostUpdate();
        var summons=Main.projectile.Where(p=>p.active&&p.ModProjectile is LivingArmament).ToArray();
        check(summons.Length==soul.TraitMains.Count(),"每个有效出战槽各有召唤体");
        controller.PostUpdate();check(Main.projectile.Count(p=>p.active&&p.ModProjectile is LivingArmament)==summons.Length,"重复更新不重复召唤");
        int beforeX=Main.mouseX,beforeY=Main.mouseY;var original=owner.HeldItem;Vector2 originalPos=owner.position;
        var field=typeof(LivingArmament).GetField("proxy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        for(int i=0;i<120;i++)foreach(var p in summons)if(p.active)
        {
            object instance=field.GetValue(p.ModProjectile);
            if(instance!=null)
            {
                var ghost=(Player)instance.GetType().GetProperty("Player").GetValue(instance);
                // 专用服务端会跳过玩家挥击姿势；夹具调用同一原版姿势函数补齐位置。
                Main.dedServ=false;
                try{ghost.ItemCheck_ApplyUseStyle(0,ghost.HeldItem,new Rectangle(0,0,32,32));}
                finally{Main.dedServ=true;}
            }
            p.ModProjectile.AI();
        }
        check(target.life<100000,"出战武器通过玩家近战流程实际命中目标");
        check(Main.projectile.Any(p=>p.active&&p.type==ProjectileID.SeedlerNut&&p.owner==0),"种子弯刀通过原版物品检查发射");
        check(Main.projectile.Any(p=>p.active&&p.type==ProjectileID.Starfury&&p.owner==0),"星怒通过原版物品检查发射");
        var knife=Main.projectile.FirstOrDefault(p=>p.active&&p.type==ProjectileID.FlyingKnife&&p.owner==0);
        check(knife!=null,"飞刀从玩家通道发射");
        knife.AI();
        check(knife.active&&knife.ai[0]<=0&&ReferenceEquals(Main.player[0],owner),"飞刀继续由代理持有并恢复真实玩家");
        check(Main.mouseX==beforeX&&Main.mouseY==beforeY&&ReferenceEquals(owner.HeldItem,original)&&owner.position==originalPos,
            "代理结束后保留真实玩家物品、位置与鼠标");
        soul.Hosts[0].TurnToAir();soul.Hosts[5].TurnToAir();
        foreach(var p in summons)if(p.active)p.ModProjectile.AI();
        check(!LivingArmamentFamily.Enabled(soul)&&summons.All(p=>!p.active),"失去激活成员时召唤体退出");
        knife.AI();check(knife.ai[0]>0&&ReferenceEquals(Main.player[0],owner),"召唤体消失后飞刀退出代理操控并恢复真实玩家");

    }
}
