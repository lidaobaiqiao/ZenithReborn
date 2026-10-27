using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ZenithReborn;

// 只增强矿石飞剑自身的像素亮度，不改主剑刀光、不另加轮廓或外扩光晕。
public sealed class OreStackGlow
{
    public const float MaxGlow=.40f, EnvironmentLightGain=.35f;
    public const int BrightenFrames=12;
    public float Brightness {get;private set;}
    public float LightMultiplier=>1+Brightness*EnvironmentLightGain;
    public void Reset()=>Brightness=0;
    public void Update(float progress)
    {
        // 向当前层数缓动；收到网络更新后也从已有亮度继续过渡，不跳亮。
        progress=Math.Clamp(progress,0,1);
        Brightness+=(progress-Brightness)*(1-MathF.Pow(.05f,1f/BrightenFrames));
        if(Math.Abs(progress-Brightness)<.001f)Brightness=progress;
    }
    public static void Draw(Texture2D texture,Vector2 at,float rotation,float scale,Color tint,float alpha,float brightness)
    {
        if(brightness<=0)return;
        Color light=tint*(MaxGlow*brightness*alpha);light.A=0;
        Main.spriteBatch.Draw(texture,at-Main.screenPosition,null,light,rotation+MathHelper.PiOver4,
            texture.Size()*.5f,scale,SpriteEffects.None,0);
    }
}
