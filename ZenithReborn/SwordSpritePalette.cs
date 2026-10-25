using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ZenithReborn;

// 只供主飞剑材质使用：从当前剑身帧读取颜色，不改变矿石H4或其他系统的既有调色表。
[Autoload(Side=ModSide.Client)]
public sealed class SwordSpritePalette : ModSystem
{
    private static readonly Dictionary<(int,Rectangle),Sample> cache=new();
    private sealed record Sample(Texture2D Texture,Color Color);

    public static Color Resolve(int itemType,Color fallback)
    {
        if(Main.dedServ||itemType<=0||itemType>=ItemLoader.ItemCount)return fallback;
        Main.instance.LoadItem(itemType);
        var texture=TextureAssets.Item[itemType].Value;
        var frame=Main.itemAnimations[itemType]?.GetFrame(texture)??texture.Frame();
        var key=(itemType,frame);
        if(cache.TryGetValue(key,out var found)&&ReferenceEquals(found.Texture,texture))return found.Color;
        var pixels=new Color[frame.Width*frame.Height];
        texture.GetData(0,frame,pixels,0,pixels.Length);
        Color color=Extract(pixels,frame.Width,frame.Height,fallback);
        cache[key]=new(texture,color);
        return color;
    }

    // 原版剑体沿左下到右上放置；采样上半刃身，排除透明边、暗描边和多数下方剑柄。
    // 有色剑取加权主色簇，避免金色剑柄把蓝刃染黄；真正无彩的钢刃保留中性色。
    public static Color Extract(Color[] pixels,int width,int height,Color fallback)
    {
        var weights=new float[36];var colors=new Vector3[36];
        Vector3 neutral=Vector3.Zero;float total=0,colored=0;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            var p=pixels[y*width+x];
            float along=(x/(float)Math.Max(1,width-1)+1-y/(float)Math.Max(1,height-1))*.5f;
            if(p.A<96||along<.46f)continue;
            Vector3 rgb=p.ToVector3();float hi=Math.Max(rgb.X,Math.Max(rgb.Y,rgb.Z)),lo=Math.Min(rgb.X,Math.Min(rgb.Y,rgb.Z));
            if(hi<.24f)continue;
            float weight=p.A/255f*(.3f+along)*hi;
            neutral+=rgb*weight;total+=weight;
            if((hi-lo)/hi<.18f)continue;
            Vector3 hsl=Main.rgbToHsl(p);int bin=Math.Min(35,(int)(hsl.X*36));
            weight*=.5f+(hi-lo)*3;
            weights[bin]+=weight;colors[bin]+=rgb*weight;colored+=weight;
        }
        if(total<=0)return fallback;
        Vector3 chosen=neutral/total;
        if(colored>total*.12f)
        {
            int best=0;float score=-1;
            for(int i=0;i<36;i++)
            {
                float value=weights[i]+weights[(i+35)%36]*.5f+weights[(i+1)%36]*.5f;
                if(value>score){best=i;score=value;}
            }
            int before=(best+35)%36,after=(best+1)%36;
            chosen=(colors[best]+(colors[before]+colors[after])*.5f)/Math.Max(.001f,score);
        }
        Vector3 tone=Main.rgbToHsl(new Color(chosen));
        // 能量材质提高色彩可读性，但不凭空给银白剑分配随机彩色。
        float saturation=tone.Y<.12f?tone.Y:Math.Clamp(tone.Y*1.2f,.62f,.94f);
        return Main.hslToRgb(tone.X,saturation,.58f);
    }

    public override void Unload()=>cache.Clear();
}
