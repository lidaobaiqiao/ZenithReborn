using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 仅编排动作伴随的雷光、蓄力流线与角色流光；不画屏幕遮罩，不改现有材质。
public static class MartialActionVisuals
{
    public static void Draw(MartialGiant giant,Player player)
    {
        if(Main.dedServ)return;
        Color tint=giant.Tier==3?MartialVisual.Violet:MartialVisual.Cyan;
        int age=giant.Age;
        if(giant.Phase==0 && age>0)
        {
            if(age<MartialMotion.ThunderImpact+12)Thunder(player.Center,age,tint);
            if(age>=MartialMotion.ThunderImpact)
            {
                float charge=Math.Clamp((age-MartialMotion.ThunderImpact)/(float)MartialMotion.ChargeFrames,0,1);
                Vector2 axis=giant.Projectile.rotation.ToRotationVector2(),side=axis.RotatedBy(MathHelper.PiOver2);
                // 聚拢的双股流线沿真实巨剑扭转，越接近满蓄越收紧，而非静止光圈。
                for(int strand=0;strand<2;strand++)
                {
                    Vector2 previous=player.Center;
                    for(int i=1;i<=20;i++)
                    {
                        float t=i/20f;
                        float curl=MathF.Sin(t*10-age*.13f+strand*MathHelper.Pi)*(1-charge*.55f)*(18+MathF.Sin(t*MathHelper.Pi)*22);
                        Vector2 next=player.Center+axis*(t*290)+side*curl;
                        MartialVisual.Line(previous,next,tint*((.12f+charge*.3f)*MathF.Sin(t*MathHelper.Pi)),1.2f+charge*1.4f);
                        previous=next;
                    }
                }
                float pulse=.5f+.5f*MathF.Sin(age*.16f);
                OreSwordEffects.Star(player.Center+axis*285,tint,.3f+charge*.6f,(.2f+charge*.55f)*pulse,age*.015f);
            }
        }
        if(giant.Phase==2 || giant.Phase==3 && age<18)
        {
            float fade=giant.Phase==2?1:1-age/18f;
            var path=giant.DashPath;
            for(int i=1;i<path.Count;i++)
            {
                float weight=i/(float)path.Count;
                MartialVisual.Line(path[i-1],path[i],tint*(fade*weight*.35f),6+weight*18);
                MartialVisual.Line(path[i-1],path[i],Color.White*(fade*weight*.8f),2+weight*3);
            }
            if(giant.Phase==2)
            {
                Vector2 axis=giant.Projectile.rotation.ToRotationVector2();
                // 人物此时由专用绘制钩子隐藏，亮芯和拖迹即是化光后的角色。
                MartialVisual.Line(player.Center-axis*32,player.Center+axis*21,tint*.8f,17);
                MartialVisual.Line(player.Center-axis*20,player.Center+axis*24,Color.White*.95f,4);
                OreSwordEffects.Star(player.Center+axis*18,tint,.8f,.9f,giant.Projectile.rotation);
            }
        }
        if(giant.Phase==3)
        {
            var axis=giant.Projectile.rotation.ToRotationVector2();
            float t=age/(float)MartialTuning.HoldFrames;
            OreSwordEffects.Star(player.Center+axis*285,tint,.45f+.22f*t,.35f+.4f*t,age*.018f);
        }
    }
    public static void Thunder(Vector2 impact,int age,Color tint)
    {
        if(age<=0)return;
        float reveal=Math.Clamp(age/(float)MartialMotion.ThunderImpact,0,1);
        float fade=age<=MartialMotion.ThunderImpact?1:Math.Clamp(1-(age-MartialMotion.ThunderImpact)/12f,0,1);
        Vector2 start=impact-new Vector2(0,560),previous=start;
        for(int i=1;i<=22;i++)
        {
            float t=Math.Min(i/22f,reveal);
            float jag=MathF.Sin(i*11.71f)*MathF.Sin(i*3.1f)*28*MathF.Sin(t*MathHelper.Pi);
            Vector2 next=Vector2.Lerp(start,impact,t)+new Vector2(jag,0);
            MartialVisual.Line(previous,next,tint*(fade*.35f),12);
            MartialVisual.Line(previous,next,Color.White*(fade*.95f),3);
            if(i%5==0 && t<reveal)
                MartialVisual.Line(next,next+new Vector2((i%2==0?1:-1)*34,38),tint*(fade*.6f),2);
            previous=next;if(t>=reveal)break;
        }
        if(age>=MartialMotion.ThunderImpact)OreSwordEffects.Star(impact,tint,1.2f,fade,age*.04f);
    }
}
