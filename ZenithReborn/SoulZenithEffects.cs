using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.ID;

namespace ZenithReborn;

public static class SoulZenithEffects
{
    public static FinalFractalHelper.FinalFractalProfile Profile(int type)
    {
        var native=FinalFractalHelper.GetFinalFractalProfile(type);
        // 原版合成材料沿用实际原版配色和长度，额外宿主复用同一粒子委托并使用自己的颜色。
        return SoulProgress.ZenithWeapons.Contains(type)?native:
            new FinalFractalHelper.FinalFractalProfile(native.trailWidth*2,SoulVisuals.Tint(type));
    }

    public static int DustAttempts(float phase,float halfDistance)
    {
        float t=Utils.GetLerpValue(0,1,phase/60,true);
        return Math.Max(1,(int)((1+(int)(halfDistance/100))*Utils.GetLerpValue(0,.5f,t,true)*Utils.GetLerpValue(1,.5f,t,true)));
    }

    public static void EmitFlying(SoulBlade blade,Player player)
    {
        if(blade.MotionPhase>=60)return;
        var p=blade.Projectile;
        var profile=Profile((int)p.ai[0]);
        Vector2 axis=(p.rotation-MathHelper.PiOver4).ToRotationVector2();
        float rotation=p.rotation-MathHelper.PiOver4+MathHelper.PiOver2*p.spriteDirection;
        int count=DustAttempts(blade.MotionPhase,blade.TargetOffset.Length()*.5f);
        if(VfxIsolation.FlyingDust&&!VfxIsolation.PureCodeOnly)
            for(int i=0;i<count;i++)
                profile.dustMethod(p.Center+axis*profile.trailWidth*blade.DisplayScale*MathHelper.Lerp(.5f,1,Main.rand.NextFloat()),rotation,player.velocity);
        Vector3 tint=profile.trailColor.ToVector3();
        // 两处光照各自独立：剑身亮度与玩家身上亮度分开测
        if(VfxIsolation.FlyingBladeLight)Lighting.AddLight(p.Center,tint*.5f*p.Opacity);
        if(VfxIsolation.FlyingPlayerLight)Lighting.AddLight(player.MountedCenter,Vector3.Lerp(Vector3.One,tint,.7f)*p.Opacity*.15f);
    }

    // 原版星芒绘制为私有方法；这里沿用同一贴图、四次绘制及15/30/30/45阶段包络。
    public static float Flare(float phase)=>Utils.GetLerpValue(15,30,phase,true)*Utils.GetLerpValue(45,30,phase,true);
    public static void DrawTip(SoulBlade blade)
    {
        float envelope=Flare(blade.MotionPhase);
        // 脉动包络关掉时全程常亮，方便单独观察光晕与核心的形状
        if(!VfxIsolation.TipPulse)envelope=1f;
        if(envelope<=0 || blade.BladeOpacity<=0)return;
        var p=blade.Projectile;var profile=Profile((int)p.ai[0]);
        var texture=TextureAssets.Extra[ExtrasID.SharpTears].Value;
        Vector2 origin=texture.Size()*.5f;
        Vector2 pos=p.Center-Main.screenPosition;
        // 偏移项：关掉则星芒贴在剑心而非剑尖
        if(VfxIsolation.TipOffset)pos+=(p.rotation-MathHelper.PiOver4).ToRotationVector2()*profile.trailWidth*blade.DisplayScale;
        Color shine=profile.trailColor;shine.A/=2;
        shine*=p.Opacity*.5f;shine.A=0;shine*=SoulBlade.SpriteVisibility(blade.MotionPhase)*envelope;
        Color core=Color.White*(.5f*SoulBlade.SpriteVisibility(blade.MotionPhase)*envelope);
        Vector2 a=new Vector2(.5f,5)*envelope*blade.DisplayScale,b=new Vector2(.5f,2)*envelope*blade.DisplayScale;
        if(VfxIsolation.TipHalo)
        {
            Main.EntitySpriteDraw(texture,pos,null,shine,MathHelper.PiOver2,origin,a,blade.DrawEffects);
            Main.EntitySpriteDraw(texture,pos,null,shine,0,origin,b,blade.DrawEffects);
        }
        if(VfxIsolation.TipCore)
        {
            Main.EntitySpriteDraw(texture,pos,null,core,MathHelper.PiOver2,origin,a*.6f,blade.DrawEffects);
            Main.EntitySpriteDraw(texture,pos,null,core,0,origin,b*.6f,blade.DrawEffects);
        }
    }
}
