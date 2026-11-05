using Microsoft.Xna.Framework;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 使用真实方块与弹幕伤害路径比较玩家和代理的视线判定。
internal static class LivingWallCollisionCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var oldTiles=Main.tile;int oldMaxX=Main.maxTilesX,oldMaxY=Main.maxTilesY;bool stoneSolid=Main.tileSolid[TileID.Stone];
        try
        {
            Main.maxTilesX=200;Main.maxTilesY=200;Main.tile=(Tilemap)Activator.CreateInstance(typeof(Tilemap),
                BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{(ushort)200,(ushort)200},null);
            Main.tileSolid[TileID.Stone]=true;
            foreach(var p in Main.projectile)p.active=false;
            foreach(var n in Main.npc)n.active=false;
            Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.gameMenu=false;
            Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
            Main.screenPosition=Vector2.Zero;Main.screenWidth=1920;Main.screenHeight=1080;
            var owner=new Player{whoAmI=0,active=true,direction=1,position=new Vector2(960,800)};Main.player[0]=owner;owner.ResetEffects();
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.LegacyReleased=63;
            soul.Hosts[0]=new Item(ItemID.CopperShortsword);soul.Hosts[1]=new Item(ItemID.EnchantedSword);
            owner.inventory[0]=soul.Item;owner.selectedItem=0;soul.RefreshStats();
            var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
            target.life=target.lifeMax=100000;target.HideStrikeDamage=true;target.defense=0;target.noTileCollide=false;target.knockBackResist=0;
            target.Center=new Vector2(1280,821);Main.npc[20]=target;
            for(int y=35;y<70;y++){var t=Main.tile[70,y];t.HasTile=true;t.TileType=TileID.Stone;}
            Main.mouseX=400;Main.mouseY=400;
            owner.GetModPlayer<LivingArmamentPlayer>().PostUpdate();
            Projectile summon=Main.projectile.First(p=>p.active&&p.ModProjectile is LivingArmament&&(int)p.ai[0]==0);
            summon.Center=target.Center-new Vector2(48,0);
            Projectile shot=null;
            for(int tick=0;tick<80&&shot==null;tick++)
            {
                summon.ModProjectile.AI();
                shot=Main.projectile.FirstOrDefault(p=>p.active&&p.owner==0&&p.type==ProjectileID.CopperShortswordStab);
            }
            check(shot!=null && shot.ownerHitCheck,"铜短剑从实际代理发射并执行持有者视线判定");
            var f=typeof(LivingChannelProjectile).GetField("Proxy",BindingFlags.Instance|BindingFlags.NonPublic);
            var g=shot.GetGlobalProjectile<LivingChannelProjectile>();object proxy=f.GetValue(g);
            check(proxy!=null,"短剑绑定其代理玩家");
            var ghost=(Player)proxy.GetType().GetProperty("Player").GetValue(proxy);
            check(!Collision.CanHit(owner.position,owner.width,owner.height,target.position,target.width,target.height),"真实玩家到目标之间的石墙确实挡住视线");
            check(Collision.CanHit(ghost.position,ghost.width,ghost.height,target.position,target.width,target.height),"代理与目标位于墙的同侧且视线通畅");
            check(shot.CanHitWithMeleeWeapon(target),"代理弹幕的原版视线检查通过");
            shot.Center=target.Center;int oldLife=target.life;target.immune[0]=0;
            shot.Damage();check(target.life<oldLife,"真实弹幕伤害在代理侧视线通畅时命中");
            check(ReferenceEquals(Main.player[0],owner),"实际伤害结束恢复真实玩家");
            for(int y=35;y<70;y++){var t=Main.tile[78,y];t.HasTile=true;t.TileType=TileID.Stone;}
            check(!Collision.CanHit(ghost.position,ghost.width,ghost.height,target.position,target.width,target.height),"代理与目标间新增石墙后视线受阻");
            check(!shot.CanHitWithMeleeWeapon(target),"代理与目标间墙体阻止原版视线检查");
            target.immune[0]=0;shot.localNPCImmunity[20]=0;oldLife=target.life;
            shot.Damage();check(target.life==oldLife,"真实弹幕伤害在代理与目标间有墙时被阻挡");
            check(ReferenceEquals(Main.player[0],owner),"视线受阻后仍恢复真实玩家");
        }
        finally {Main.tile=oldTiles;Main.maxTilesX=oldMaxX;Main.maxTilesY=oldMaxY;Main.tileSolid[TileID.Stone]=stoneSolid;}
    }
}
