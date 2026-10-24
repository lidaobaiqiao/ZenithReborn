using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using ZenithReborn;

namespace ZenithMaterialLab;

// 所有示例贴图均数学生成；输入目录已有文件时绝不覆盖。
internal sealed class LabResources : IDisposable
{
    internal readonly string DirectoryPath;
    private readonly List<Texture2D> owned=new();
    internal SurfaceMaterialRenderer.Inputs Inputs {get;private set;}=new();
    internal Texture2D Background {get;private set;}
    internal string[] MapNames=["ColorField","MotionMask","NormalMap","SurfaceMap","HeightMap","EmissionMap","EnvironmentMap","LayerMask"];
    internal string[] MapLabels=["底色","形状蒙版","法线","表面图","高度","自发光","环境","材质分区"];
    internal string[] FileNames=["color.png","mask.png","normal.png","surface.png","height.png","emission.png","environment.png","regions.zrdata"];
    private static string[] AssetSlots=>typeof(SurfaceMaterialDefinition.MaterialArtwork).GetProperties().Where(p=>p.PropertyType==typeof(string)).Select(p=>p.Name).ToArray();
    internal LabResources(string root)
    {
        DirectoryPath=Path.Combine(root,"Input");Directory.CreateDirectory(DirectoryPath);
        for(int slot=0;slot<FileNames.Length;slot++)
        {
            string path=Path.Combine(DirectoryPath,FileNames[slot]);if(File.Exists(path))continue;
            int width=slot==6?512:256,height=slot==6?256:256;
            using var texture=new Texture2D(Main.graphics.GraphicsDevice,width,height);
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=x/(float)(width-1),v=y/(float)(height-1);
                float wave=.5f+.5f*MathF.Sin(u*MathHelper.TwoPi*4)*MathF.Cos(v*MathHelper.TwoPi*3);
                pixels[y*width+x]=slot switch
                {
                    0=>Color.Lerp(new Color(50,85,150),new Color(170,235,245),v)*(.7f+wave*.3f),
                    1=>Color.White*MathHelper.SmoothStep(0,1,Math.Clamp((.85f+.07f*MathF.Sin(u*25)-v)*10,0,1)),
                    2=>new Color(Vector3.Normalize(new Vector3(MathF.Cos(u*25)*.65f,MathF.Sin(v*19)*.65f,1))*.5f+new Vector3(.5f)),
                    3=>new Color(.35f+wave*.65f,.15f+v*.8f,.55f+wave*.45f),
                    4=>new Color(wave,wave,wave),
                    5=>Color.White*(MathF.Sin(u*40+v*30)>.75f?1:.04f),
                    7=>new Color(v<.15f?1f:0,u>.25f&&u<.65f?1f:0,v>.60f?1f:0,u>.82f?1f:0),
                    _=>Environment(u,v)
                };
            }
            texture.SetData(pixels);using var file=File.Create(path);
            if(slot==7)
            {
                using var writer=new BinaryWriter(file);writer.Write(0x5444525A);writer.Write(1);writer.Write(width);writer.Write(height);
                foreach(var pixel in pixels){writer.Write(pixel.R);writer.Write(pixel.G);writer.Write(pixel.B);writer.Write(pixel.A);}
            }
            else texture.SaveAsPng(file,width,height);
        }
        static Color Environment(float u,float v)
        {
            var color=Color.Lerp(new Color(12,22,60),new Color(100,155,210),MathF.Sin(v*MathHelper.Pi));
            if((u>.12f&&u<.19f&&v>.13f&&v<.78f)||(u>.62f&&u<.72f&&v>.30f&&v<.62f))color=new Color(250,239,210);
            if(u>.80f&&u<.88f)color=new Color(110,28,135);
            return color;
        }
    }
    internal void Reload(SurfaceMaterialDefinition definition,int background)
    {
        Dispose();Inputs=new();
        foreach(string slot in AssetSlots)
        {
            string name=(string)definition.Assets.GetType().GetProperty(slot).GetValue(definition.Assets);
            if(string.IsNullOrWhiteSpace(name))continue;
            string path=Path.GetFullPath(Path.Combine(DirectoryPath,name));
            if(!path.StartsWith(Path.GetFullPath(DirectoryPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("素材必须位于Input目录内");
            if(!File.Exists(path))throw new FileNotFoundException("素材不存在："+name);
            using var file=File.OpenRead(path);Texture2D texture;
            if(slot=="LayerMask")
            {
                using var reader=new BinaryReader(file);
                if(reader.ReadUInt32()!=0x5444525A||reader.ReadUInt32()!=1)throw new ArgumentException("分区图需使用制作工具转换为zrdata");
                int width=reader.ReadInt32(),height=reader.ReadInt32();
                if(width<1||height<1||width>8192||height>8192||file.Length-file.Position!=(long)width*height*4)throw new ArgumentException("分区图数据无效");
                texture=new Texture2D(Main.graphics.GraphicsDevice,width,height);texture.SetData(reader.ReadBytes(width*height*4));
            }
            else texture=Texture2D.FromStream(Main.graphics.GraphicsDevice,file);
            owned.Add(texture);
            Inputs.GetType().GetField(slot).SetValue(Inputs,texture);
        }
        Background=CreateBackground(background);owned.Add(Background);
    }
    private Texture2D CreateBackground(int kind)
    {
        string custom=Path.Combine(DirectoryPath,"background.png");
        if(kind==2&&File.Exists(custom)){using var file=File.OpenRead(custom);return Texture2D.FromStream(Main.graphics.GraphicsDevice,file);}
        const int width=960,height=540;
        var texture=new Texture2D(Main.graphics.GraphicsDevice,width,height);var pixels=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            var color=((x/24+y/24)%2==0)?new Color(52,69,88):new Color(170,185,193);
            if(kind==1)
            {
                color=Color.Lerp(new Color(80,112,143),new Color(115,150,128),y/(float)height);
                if(x%180>=80&&x%180<99)color=new Color(104,77,48);
                if(y%100<8&&x%180>86&&x%180<133)color=new Color(108,78,46);
                if(y>475)color=new Color(48,61,39);
            }
            pixels[y*width+x]=color;
        }
        texture.SetData(pixels);return texture;
    }
    internal string Export(string root,SurfaceMaterialDefinition material)
    {
        string output=Path.Combine(root,"Exports",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"material.json"),material.ToJson());
        var resources=new List<object>();
        foreach(string slot in AssetSlots)
        {
            string name=(string)material.Assets.GetType().GetProperty(slot).GetValue(material.Assets);
            if(string.IsNullOrWhiteSpace(name))continue;
            string source=Path.Combine(DirectoryPath,name),target=Path.Combine(output,name);
            Directory.CreateDirectory(Path.GetDirectoryName(target));if(!File.Exists(target))File.Copy(source,target);
            resources.Add(new{slot,file=name,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),origin="author-input"});
        }
        File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new{
            schemaVersion=1,kind="ZenithReborn.SurfaceMaterial",renderer=SurfaceMaterialRenderer.EffectPath,
            scope="developer-authoring-only",resources,notes="输入素材的来源由作者确认；导出不自动安装正式包。"},SurfaceMaterialDefinition.JsonOptions));
        return output;
    }
    public void Dispose()
    {
        var device=Main.graphics.GraphicsDevice;
        for(int slot=0;slot<16;slot++)if(device.Textures[slot] is Texture2D texture&&owned.Contains(texture))device.Textures[slot]=null;
        foreach(var texture in owned)texture.Dispose();owned.Clear();
    }
}
