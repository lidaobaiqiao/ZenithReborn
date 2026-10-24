using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 蒸发小圆圈的生成参数：位置、向上初速、初始尺寸、寿命（帧）、染色。
public readonly record struct EvapCircleSpawn(Vector2 Position,Vector2 Velocity,Color Tint,float Size,int Lifetime);

// 武技巨剑挥砍结束时，月牙弧面被不规则蚕食后脱落的小圆圈。
// 复用原版 Dust 池与 Cloud 粉尘贴图，但更新交给这里：向上飘、边飘边缩小、
// 最后淡尽，不做下落、不加光。与月牙 shader 的 uErode 侵蚀配对。
public class EvapCircle : ModDust
{
    // 直接绘制原版 Dust 图集，frame 在生成时取 Cloud 粉尘的固定帧。
    public override string Texture=>"Terraria/Images/Dust";
    private static Rectangle? cloudFrame;
    public static Dust Spawn(EvapCircleSpawn spawn)
    {
        if(Main.dedServ)return null;
        // 首次生成时向原版 Cloud 粉尘借一次帧矩形，之后一直复用。
        if(!cloudFrame.HasValue)
        {
            var probe=Dust.NewDustPerfect(Vector2.Zero,DustID.Cloud,Vector2.Zero,0,Color.White,1f);
            cloudFrame=probe.frame;probe.active=false;
        }
        Dust dust=Dust.NewDustPerfect(spawn.Position,ModContent.DustType<EvapCircle>(),spawn.Velocity,0,spawn.Tint,spawn.Size);
        if(dust.active)
        {
            dust.noGravity=true;dust.noLight=true;dust.fadeIn=0;dust.frame=cloudFrame.Value;
            dust.customData=spawn;
        }
        return dust;
    }
    public static void Step(Dust dust)
    {
        if(dust.customData is not EvapCircleSpawn state){dust.active=false;return;}
        dust.fadeIn++;
        if(dust.fadeIn>=state.Lifetime){dust.active=false;return;}
        // 速度缓慢衰减并整体向上，位置随速度推进；缩小在绘制阶段完成。
        dust.velocity*=.96f;
        dust.position+=dust.velocity;
        dust.rotation=0f;
    }
    public override bool Update(Dust dust){Step(dust);return false;}
    public static void Draw(SpriteBatch batch,Dust dust)
    {
        if(!dust.active || dust.customData is not EvapCircleSpawn state)return;
        float t=Math.Clamp(dust.fadeIn/(float)state.Lifetime,0,1);
        // 出现时快速淡入，之后随尺寸缩小一起淡尽。
        float alpha=MathF.Pow(1-t,1.2f)*Math.Min(1,(dust.fadeIn+1)/2f);
        Color color=state.Tint*alpha;color.A=0;
        float scale=state.Size*(1f-t);
        Texture2D texture=TextureAssets.Dust.Value;
        Vector2 origin=new Vector2(dust.frame.Width,dust.frame.Height)*.5f;
        batch.Draw(texture,dust.position-Main.screenPosition,dust.frame,color,0f,origin,scale,SpriteEffects.None,0f);
    }
    public override bool PreDraw(Dust dust){Draw(Main.spriteBatch,dust);return false;}
}
