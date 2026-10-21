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
        // Original ingredients use the actual native palette and lengths.
        // Extra hosts use the SAME native dust delegate with their own color.
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
        for(int i=0;i<count;i++)
            profile.dustMethod(p.Center+axis*profile.trailWidth*blade.DisplayScale*MathHelper.Lerp(.5f,1,Main.rand.NextFloat()),rotation,player.velocity);
        Vector3 tint=profile.trailColor.ToVector3();
        Lighting.AddLight(p.Center,tint*.5f*p.Opacity);
        Lighting.AddLight(player.MountedCenter,Vector3.Lerp(Vector3.One,tint,.7f)*p.Opacity*.15f);
    }

    // Native Main.DrawPrettyStarSparkle is private: same Extra_98 sprite,
    // four draws, and native 15/30/30/45 phase envelope.
    public static float Flare(float phase)=>Utils.GetLerpValue(15,30,phase,true)*Utils.GetLerpValue(45,30,phase,true);
    public static void DrawTip(SoulBlade blade)
    {
        float envelope=Flare(blade.MotionPhase);
        if(envelope<=0 || blade.BladeOpacity<=0)return;
        var p=blade.Projectile;var profile=Profile((int)p.ai[0]);
        var texture=TextureAssets.Extra[ExtrasID.SharpTears].Value;
        Vector2 origin=texture.Size()*.5f;
        Vector2 pos=p.Center-Main.screenPosition+(p.rotation-MathHelper.PiOver4).ToRotationVector2()*profile.trailWidth*blade.DisplayScale;
        Color shine=profile.trailColor;shine.A/=2;
        shine*=p.Opacity*.5f;shine.A=0;shine*=SoulBlade.SpriteVisibility(blade.MotionPhase)*envelope;
        Color core=Color.White*(.5f*SoulBlade.SpriteVisibility(blade.MotionPhase)*envelope);
        Vector2 a=new Vector2(.5f,5)*envelope*blade.DisplayScale,b=new Vector2(.5f,2)*envelope*blade.DisplayScale;
        Main.EntitySpriteDraw(texture,pos,null,shine,MathHelper.PiOver2,origin,a,blade.DrawEffects);
        Main.EntitySpriteDraw(texture,pos,null,shine,0,origin,b,blade.DrawEffects);
        Main.EntitySpriteDraw(texture,pos,null,core,MathHelper.PiOver2,origin,a*.6f,blade.DrawEffects);
        Main.EntitySpriteDraw(texture,pos,null,core,0,origin,b*.6f,blade.DrawEffects);
    }
}
