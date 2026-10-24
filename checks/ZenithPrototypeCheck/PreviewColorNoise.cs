using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithPrototypeCheck;

// 仅供制作工具使用的数学噪声；没有外部图片，也不装入生产模组。
internal static class PreviewColorNoise
{
    internal static Texture2D Create(GraphicsDevice device)
    {
        const int size=128,cells=8;
        var pixels=new Color[size*size];
        float Value(int x,int y)
        {
            uint hash=(uint)((x%cells)*374761393+(y%cells)*668265263);
            hash=(hash^(hash>>13))*1274126177;
            return (hash^(hash>>16))/4294967295f;
        }
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float px=x*cells/(float)size,py=y*cells/(float)size;
            int ix=(int)px,iy=(int)py;
            float sx=px-ix,sy=py-iy;
            sx=sx*sx*(3-2*sx);sy=sy*sy*(3-2*sy);
            float v=MathHelper.Lerp(MathHelper.Lerp(Value(ix,iy),Value(ix+1,iy),sx),
                MathHelper.Lerp(Value(ix,iy+1),Value(ix+1,iy+1),sx),sy);
            pixels[y*size+x]=new Color(v,v,v,1f);
        }
        var texture=new Texture2D(device,size,size);
        texture.SetData(pixels);return texture;
    }
}
