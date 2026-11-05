using Microsoft.Xna.Framework;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 仅隔离服务端显式设置报告路径时启动本检查。
public class LivingChildSightEntry : ModSystem
{
    public override void PostAddRecipes()
    {
        string path=Environment.GetEnvironmentVariable("ZENITH_CHILD_SIGHT_REPORT");
        if(!Main.dedServ || string.IsNullOrEmpty(path))return;
        int count=0;
        try
        {
            CurseFixture.NeutralDefaults=true;
            LivingChildSightCheck.Run((ok,message)=>{if(!ok)throw new Exception(message);count++;});
            System.IO.File.WriteAllText(path,$"PASS: {count} child sight checks; isolated server only.\n");
            Environment.Exit(0);
        }
        catch(Exception e)
        {
            System.IO.File.WriteAllText(path,$"FAIL: {e}\n");
            Environment.Exit(1);
        }
    }
}

// 检查命中回调以玩家为来源创建子弹幕的路径。
internal static class LivingChildSightCheck
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
            var owner=new Player{whoAmI=0,active=true,direction=1,position=new Vector2(960,800),statLife=50,statLifeMax2=100};
            Main.player[0]=owner;owner.ResetEffects();
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
            check(shot!=null,"代理实际发射父弹幕");
            var binding=shot.GetGlobalProjectile<LivingChannelProjectile>();
            var origin=typeof(LivingChannelProjectile).GetField("Origin",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(binding);
            var ghost=(Player)origin.GetType().GetProperty("Player").GetValue(origin);
            LivingChildSightSpawn.Parent=shot;LivingChildSightSpawn.Owner=owner;LivingChildSightSpawn.Child=null;
            shot.Center=target.Center;target.immune[0]=0;shot.Damage();
            var child=LivingChildSightSpawn.Child;
            check(child!=null,"父弹幕命中后以玩家来源创建子弹幕");
            check(LivingChildSightSpawn.RealOwnerDuringHit && owner.statLife==51,"命中回调读取并修改真实玩家状态");
            check(ReferenceEquals(Main.player[0],owner),"父弹幕伤害结束恢复真实玩家");
            check(!Collision.CanHit(owner.position,owner.width,owner.height,target.position,target.width,target.height) &&
                Collision.CanHit(ghost.position,ghost.width,ghost.height,target.position,target.width,target.height),"真人隔墙且代理视线通畅");
            check(child.CanHitWithMeleeWeapon(target),"玩家来源子弹幕使用代理视线");
            target.immune[0]=0;int life=target.life;child.Damage();
            check(target.life<life,"代理视线通畅时子弹幕实际造成伤害");
            owner.Center=target.Center+new Vector2(32,0);
            for(int y=35;y<70;y++){var t=Main.tile[78,y];t.HasTile=true;t.TileType=TileID.Stone;}
            check(Collision.CanHit(owner.position,owner.width,owner.height,target.position,target.width,target.height) &&
                !Collision.CanHit(ghost.position,ghost.width,ghost.height,target.position,target.width,target.height),"真人视线通畅且代理隔墙");
            target.immune[0]=0;life=target.life;child.Damage();
            check(!child.CanHitWithMeleeWeapon(target) && target.life==life,"代理隔墙时子弹幕不能命中");
            check(ReferenceEquals(Main.player[0],owner),"子弹幕伤害结束恢复真实玩家");

            // 同一玩家的普通弹幕不得沿用前一次代理来源。
            Projectile ordinary=Projectile.NewProjectileDirect(owner.GetSource_FromThis(),target.Center,Vector2.Zero,
                ModContent.ProjectileType<LivingChildSightProjectile>(),10,0,owner.whoAmI);
            var ordinaryOrigin=typeof(LivingChannelProjectile).GetField("Origin",BindingFlags.Instance|BindingFlags.NonPublic)
                .GetValue(ordinary.GetGlobalProjectile<LivingChannelProjectile>());
            check(ordinaryOrigin==null && ordinary.CanHitWithMeleeWeapon(target),"真人弹幕未继承代理来源且使用真人视线");

            // 模拟同一真人的两把武器嵌套触发伤害，并检查异常后的来源栈。
            var originField=typeof(LivingChannelProjectile).GetField("Origin",BindingFlags.Instance|BindingFlags.NonPublic);
            var system=typeof(LivingChannelSystem);
            var damageField=system.GetField("DamageOrigin",BindingFlags.Static|BindingFlags.NonPublic);
            var track=system.GetMethod("TrackDamageOrigin",BindingFlags.Static|BindingFlags.NonPublic);
            var otherOrigin=Activator.CreateInstance(origin.GetType(),owner,new Item(ItemID.CopperShortsword),summon);
            originField.SetValue(ordinary.GetGlobalProjectile<LivingChannelProjectile>(),otherOrigin);
            bool outer=false,inner=false,restored=false,playerUnchanged=false;
            On_Projectile.orig_Damage innerDamage=p=>
            {
                inner=ReferenceEquals(damageField.GetValue(null),otherOrigin);
                playerUnchanged=ReferenceEquals(Main.player[0],owner);
                throw new InvalidOperationException("检查嵌套异常恢复");
            };
            On_Projectile.orig_Damage outerDamage=p=>
            {
                outer=ReferenceEquals(damageField.GetValue(null),origin);
                try {track.Invoke(null,new object[]{innerDamage,ordinary});}
                catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException) { }
                restored=ReferenceEquals(damageField.GetValue(null),origin);
            };
            track.Invoke(null,new object[]{outerDamage,shot});
            check(outer && inner && restored,"不同代理的嵌套伤害各用自身来源且异常后恢复上一层");
            check(playerUnchanged && damageField.GetValue(null)==null && ReferenceEquals(Main.player[0],owner),
                "嵌套伤害及异常后恢复来源上下文并保持真实玩家");
        }
        finally
        {
            LivingChildSightSpawn.Parent=null;LivingChildSightSpawn.Owner=null;LivingChildSightSpawn.Child=null;
            Main.tile=oldTiles;Main.maxTilesX=oldMaxX;Main.maxTilesY=oldMaxY;Main.tileSolid[TileID.Stone]=stoneSolid;
        }
    }
}

// 模拟外部模组在父弹幕命中后使用真实玩家来源创建剑气。
public class LivingChildSightSpawn : GlobalProjectile
{
    internal static Projectile Parent,Child;
    internal static Player Owner;
    internal static bool RealOwnerDuringHit;
    public override void OnHitNPC(Projectile projectile,NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(!ReferenceEquals(projectile,Parent))return;
        RealOwnerDuringHit=ReferenceEquals(Main.player[projectile.owner],Owner);
        var player=Main.player[projectile.owner];player.statLife++;
        Child=Projectile.NewProjectileDirect(player.GetSource_FromThis(),target.Center,Vector2.Zero,
            ModContent.ProjectileType<LivingChildSightProjectile>(),10,0,player.whoAmI);
    }
}

public class LivingChildSightProjectile : ModProjectile
{
    public override string Texture=>"Terraria/Images/Projectile_1";
    public override void SetDefaults()
    {
        Projectile.width=30;Projectile.height=30;Projectile.friendly=true;Projectile.penetrate=-1;
        Projectile.ownerHitCheck=true;Projectile.DamageType=DamageClass.Melee;
    }
}
