using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 从生产召唤槽发射真实原版弹幕，核对代理身份与还原。
internal static class LivingProjectileOwnershipCheck
{
    internal static void Run(Action<bool,string> check)
    {
        // 隔离服务端缺省无光照引擎，原版剑气更新仍会请求光照。
        Lighting.Initialize();
        foreach(int itemType in new[]{ItemID.CopperShortsword,ItemID.Spear,ItemID.EnchantedBoomerang,ItemID.TerraBlade,ItemID.FlyingKnife,ItemID.VampireKnives})
        {
            foreach(var p in Main.projectile)p.active=false;
            foreach(var n in Main.npc)n.active=false;
            Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.gameMenu=false;
            Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
            Main.screenPosition=Vector2.Zero;Main.screenWidth=1920;Main.screenHeight=1080;Main.mouseX=400;Main.mouseY=400;
            var owner=new Player{whoAmI=0,active=true,direction=1,position=new Vector2(1000,800)};Main.player[0]=owner;owner.ResetEffects();
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.LegacyReleased=63;
            soul.Hosts[0]=new Item(itemType);soul.Hosts[1]=new Item(ItemID.EnchantedSword);
            owner.inventory[0]=soul.Item;owner.selectedItem=0;soul.RefreshStats();
            var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
            target.life=target.lifeMax=100000;target.HideStrikeDamage=true;target.defense=0;target.noTileCollide=true;target.knockBackResist=0;
            target.Center=owner.Center+new Vector2(180,0);Main.npc[20]=target;
            owner.GetModPlayer<LivingArmamentPlayer>().PostUpdate();
            Projectile summon=Main.projectile.First(p=>p.active&&p.ModProjectile is LivingArmament&&(int)p.ai[0]==0);
            summon.Center=target.Center-new Vector2(48,0);
            int shotType=soul.Hosts[0].shoot;
            Projectile shot=null;
            for(int t=0;t<100&&shot==null;t++)
            {
                summon.ModProjectile.AI();
                shot=Main.projectile.FirstOrDefault(p=>p.active&&p.type==shotType&&p.owner==owner.whoAmI);
            }
            check(shot!=null,$"武器 {itemType} 经召唤体实际生成原版弹幕 {shotType}");
            var field=typeof(LivingChannelProjectile).GetField("Proxy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var bound=field.GetValue(shot.GetGlobalProjectile<LivingChannelProjectile>());
            bool shouldBind=itemType!=ItemID.VampireKnives;
            check((bound!=null)==shouldBind,$"武器 {itemType} 依附型身份判定");
            int ox=Main.mouseX,oy=Main.mouseY;Vector2 realPosition=owner.Center;
            shot.AI();
            check(ReferenceEquals(Main.player[0],owner)&&owner.Center==realPosition&&Main.mouseX==ox&&Main.mouseY==oy,$"武器 {itemType} AI后恢复真实玩家和鼠标");
            if(itemType is ItemID.CopperShortsword or ItemID.Spear)
            {
                var actor=(Player)bound.GetType().GetProperty("Player").GetValue(bound);
                check(Vector2.Distance(shot.Center,actor.Center)<100&&Vector2.Distance(shot.Center,realPosition)>60,
                    $"武器 {itemType} 弹幕位置依附远处代理而非真实玩家");
            }
            if(itemType==ItemID.FlyingKnife)
            {
                check(shot.active&&shot.ai[0]<=0,"飞刀在代理持有时未转入回收");
                summon.Kill();shot.AI();
                check(ReferenceEquals(Main.player[0],owner)&&shot.ai[0]>0,"召唤体结束后飞刀恢复真实玩家回收");
            }
        }
    }
}
