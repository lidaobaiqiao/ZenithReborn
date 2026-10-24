using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithReborn;

// 剑身慢底色使用的项目自有数学噪声；不读取任何图片或外部着色器。
// 只生成灰度变化，逐剑配色、慢滚动和动态轮廓仍由原来的蒙版系统负责。
public static class SwordBodyColorNoise
{
    public static float Sample(float u,float v)
        =>.14f+.80f*(Field(u,v,8)*.56f+Field(u,v,16)*.30f+Field(u,v,32)*.14f);

    private static float Field(float u,float v,int grid)
    {
        float px=u*grid,py=v*grid;
        int x=(int)MathF.Floor(px),y=(int)MathF.Floor(py);
        float fx=px-MathF.Floor(px),fy=py-MathF.Floor(py);
        fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
        float Value(int i,int j)
        {
            uint hash=unchecked((uint)((i&(grid-1))*374761393+(j&(grid-1))*668265263+grid*7919));
            hash=unchecked((hash^(hash>>13))*1274126177);
            return (hash^(hash>>16))/4294967295f;
        }
        return MathHelper.Lerp(MathHelper.Lerp(Value(x,y),Value(x+1,y),fx),
            MathHelper.Lerp(Value(x,y+1),Value(x+1,y+1),fx),fy);
    }

    public static Color[] CreatePixels(int size=512)
    {
        if(size<16||size>2048)throw new ArgumentOutOfRangeException(nameof(size));
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float light=Sample((x+.5f)/size,(y+.5f)/size);
            pixels[y*size+x]=new Color(light,light,light,1f);
        }
        return pixels;
    }

    internal static Texture2D Create(GraphicsDevice device)
    {
        // 保持原来慢底色的纹理尺寸，避免改变现有每秒滚动像素所代表的速度。
        var texture=new Texture2D(device,512,512);
        texture.SetData(CreatePixels());return texture;
    }
}
