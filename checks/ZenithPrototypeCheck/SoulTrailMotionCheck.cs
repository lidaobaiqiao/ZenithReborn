using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 对照实际飞剑AI在主人静止与匀速移动下的剑影位置和碰撞。
internal static class SoulTrailMotionCheck
{
    internal static void Run(Action<bool,string> check)
    {
        bool oldServer=Main.dedServ,oldMenu=Main.gameMenu;int oldMode=Main.netMode;
        try
        {
            Main.dedServ=true;Main.gameMenu=false;Main.netMode=NetmodeID.SinglePlayer;
            foreach(Vector2 velocity in new[]{new Vector2(5,0),new Vector2(-5,0),new Vector2(2,-4)})
            {
                foreach(var p in Main.projectile)p.active=false;
                var origin=new Vector2(1600,1000);
                var a=new Player{whoAmI=0,active=true,position=origin};
                var b=new Player{whoAmI=1,active=true,position=origin};
                Main.player[0]=a;Main.player[1]=b;a.ResetEffects();b.ResetEffects();
                int x=Projectile.NewProjectile(a.GetSource_FromThis(),a.Center,Vector2.Zero,ModContent.ProjectileType<SoulBlade>(),100,0,0,ItemID.TerraBlade);
                int y=Projectile.NewProjectile(b.GetSource_FromThis(),b.Center,Vector2.Zero,ModContent.ProjectileType<SoulBlade>(),100,0,1,ItemID.TerraBlade);
                var left=(SoulBlade)Main.projectile[x].ModProjectile;var right=(SoulBlade)Main.projectile[y].ModProjectile;
                left.TargetOffset=right.TargetOffset=new Vector2(600,60);left.Vertical=right.Vertical=60;
                int compared=0;
                for(int i=0;i<75;i++)
                {
                    b.position+=velocity;left.AI();right.AI();
                    Vector2 offset=b.MountedCenter-a.MountedCenter;
                    check(Vector2.Distance(right.Projectile.Center-left.Projectile.Center,offset)<.01f,"移动与静止飞剑的主体相对路径一致");
                    for(int echo=1;echo<4;echo++)
                    {
                        bool l=left.TryGetEchoPose(echo,out var lc,out var lr,out _);
                        bool r=right.TryGetEchoPose(echo,out var rc,out var rr,out _);
                        check(l==r,"移动不改变剑影可见时间");
                        if(!l)continue;
                        compared++;
                        check(Vector2.Distance(rc-lc,offset)<.02f&&Math.Abs(lr-rr)<.0001f,"剑影随主人平移且形状和朝向保持");
                        var target=new Rectangle((int)lc.X-3,(int)lc.Y-3,6,6);
                        var moved=new Rectangle((int)(lc.X+offset.X)-3,(int)(lc.Y+offset.Y)-3,6,6);
                        check(left.ResolveHitUnit(target)==right.ResolveHitUnit(moved),"平移前后的剑影碰撞一致");
                    }
                }
                check(compared>0,"测试覆盖可见剑影");
                b.position+=new Vector2(400,0);right.AI();
                check(!right.TryGetEchoPose(1,out _,out _,out _),"大距离传送清除旧轨迹");
            }
        }
        finally{foreach(var p in Main.projectile)p.active=false;Main.dedServ=oldServer;Main.gameMenu=oldMenu;Main.netMode=oldMode;}
    }
}
