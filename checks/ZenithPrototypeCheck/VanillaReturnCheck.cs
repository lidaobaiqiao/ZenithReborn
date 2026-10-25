using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class VanillaReturnCheck
{
    internal static void Run(Action<bool,string> check)
    {
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        foreach(int direction in new[]{-1,1})foreach(float gravity in new[]{-1f,1f})
        foreach(int distance in new[]{50,240,800})foreach(float vertical in new[]{-60f,60f})
        {
            foreach(var p in Main.projectile)p.active=false;
            var player=new Player{whoAmI=0,active=true,direction=direction,position=new(1000,1000)};
            Main.player[0]=player;player.ResetEffects();player.gravDir=gravity;
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Hosts[0]=new Item(ItemID.CopperBroadsword);soul.RefreshStats();player.inventory[0]=soul.Item;
            int id=Projectile.NewProjectile(player.GetSource_ItemUse(soul.Item),player.Center,Vector2.Zero,ModContent.ProjectileType<SoulBlade>(),100,0,0,ItemID.CopperBroadsword);
            var projectile=Main.projectile[id];var blade=(SoulBlade)projectile.ModProjectile;
            blade.TargetOffset=new Vector2(distance*direction,distance*.2f);blade.Vertical=vertical;
            int nativeId=Projectile.NewProjectile(player.GetSource_ItemUse(soul.Item),player.Center,blade.TargetOffset*.5f,ProjectileID.FinalFractal,100,0,0,vertical,ItemID.CopperBroadsword);
            var native=Main.projectile[nativeId];native.localAI[1]=1;
            check(projectile.extraUpdates==native.extraUpdates,"Native and soul projectiles have identical sub-update frequency");
            for(int frame=0;frame<120;frame++)
            {
                player.position+=new Vector2(1.5f,-.25f);
                projectile.Update(id);native.Update(nativeId);
                check(projectile.active==native.active,"Unmodified native flight and soul expire on the same game frame");
                if(!projectile.active)break;
                check(Math.Abs(blade.MotionPhase-native.localAI[0])<.0001f && Vector2.Distance(projectile.Center,native.Center)<.003f,
                    "Actual Projectile.Update matches native phase and position without time rescaling");
                check(Math.Abs(MathHelper.WrapAngle(projectile.rotation+MathHelper.PiOver4-native.rotation))<.0001f && projectile.spriteDirection==native.spriteDirection,
                    "Native orientation matches for both signed curves and moving player");
                float expectedRotation=native.rotation-MathHelper.PiOver4*native.spriteDirection;
                check(Math.Abs(MathHelper.WrapAngle(blade.DrawRotation-expectedRotation))<.0001f &&
                    (blade.DrawEffects==SpriteEffects.FlipHorizontally)==(native.spriteDirection==-1),"Native item-art rotation and flip are preserved");
                float visibility=Utils.GetLerpValue(0,20,native.localAI[0],true)*Utils.GetLerpValue(68,60,native.localAI[0],true)*native.Opacity;
                check(Math.Abs(blade.BladeOpacity-visibility)<.0001f && Math.Abs(blade.DisplayScale-1.6f)<.0001f,"Native fade retains constant model scale without absorption");
                if(blade.MotionPhase>=60)check(!blade.BodyCanDamage,"本体阶段六十后保持停伤，仅可见剑影另行判定");
            }
            check(!projectile.active && !native.active,"Both complete the native lifetime");
        }
        check(typeof(SoulBlade).Assembly.GetType("ZenithReborn.SoulSheathingLayer")==null && typeof(SoulBlade).Assembly.GetType("ZenithReborn.SoulAbsorbSpark")==null,"Custom absorption remains removed");
        foreach(var p in Main.projectile)p.active=false;
    }
}
