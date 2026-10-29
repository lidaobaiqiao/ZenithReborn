using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace ZenithPrototypeCheck;

// 原始线性数据导出只用于检查；正常显示仍由正式屏幕Shader完成，不能拿此PNG当成色调审看图。
internal static class GlassBufferReadback
{
    internal static Color[] Read(Texture2D texture)
    {
        var result=new Color[texture.Width*texture.Height];
        if(texture.Format==SurfaceFormat.HalfVector4)
        {
            var half=new HalfVector4[result.Length];texture.GetData(half);
            for(int i=0;i<result.Length;i++)result[i]=new Color(half[i].ToVector4());
        }
        else texture.GetData(result);
        return result;
    }
    internal static void Save(Texture2D texture,string path)
    {
        using var file=File.Create(path);
        if(texture.Format!=SurfaceFormat.HalfVector4){texture.SaveAsPng(file,texture.Width,texture.Height);return;}
        using var display=new Texture2D(texture.GraphicsDevice,texture.Width,texture.Height);
        display.SetData(Read(texture));display.SaveAsPng(file,display.Width,display.Height);
    }
}
