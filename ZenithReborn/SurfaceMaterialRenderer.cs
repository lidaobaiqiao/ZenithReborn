using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// 无界面的共用渲染入口。测试模组与实际飞剑调用同一Effect、参数及贴图约定。
[Autoload(Side=ModSide.Client)]
public sealed class SurfaceMaterialRenderer : ModSystem
{
    public sealed class Inputs
    {
        public Texture2D ColorField,MotionMask,NormalMap,SurfaceMap,HeightMap,EmissionMap,EnvironmentMap,Brush,Noise,LayerMask;
    }
    [StructLayout(LayoutKind.Sequential,Pack=1)]
    private struct Vertex : IVertexType
    {
        public Vector3 Position;public Color Color;public Vector2 UV,Along,Across;
        public static readonly VertexDeclaration Declaration=new(
            new VertexElement(0,VertexElementFormat.Vector3,VertexElementUsage.Position,0),
            new VertexElement(12,VertexElementFormat.Color,VertexElementUsage.Color,0),
            new VertexElement(16,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,0),
            new VertexElement(24,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,1),
            new VertexElement(32,VertexElementFormat.Vector2,VertexElementUsage.TextureCoordinate,2));
        VertexDeclaration IVertexType.VertexDeclaration=>Declaration;
    }
    private sealed class HeightPixels
    {
        public readonly Color[] Pixels;public readonly int Width,Height;
        public HeightPixels(Texture2D texture)
        {Width=texture.Width;Height=texture.Height;Pixels=new Color[Width*Height];texture.GetData(Pixels);}
        public float Sample(Vector2 uv)
        {
            float x=(uv.X-MathF.Floor(uv.X))*(Width-1),y=Math.Clamp(uv.Y,0,1)*(Height-1);
            int ix=(int)x,iy=(int)y,jx=Math.Min(ix+1,Width-1),jy=Math.Min(iy+1,Height-1);
            return MathHelper.Lerp(MathHelper.Lerp(Pixels[iy*Width+ix].R,Pixels[iy*Width+jx].R,x-ix),
                MathHelper.Lerp(Pixels[jy*Width+ix].R,Pixels[jy*Width+jx].R,x-ix),y-iy)/255f;
        }
    }
    private static Effect effect;
    private static Texture2D white;
    private static Vertex[] work=[];
    private static readonly Dictionary<string,Texture2D> assets=new();
    private static readonly List<Texture2D> ownedData=new();
    private static ConditionalWeakTable<Texture2D,HeightPixels> heights=new();
    public static string EffectPath=>"ZenithReborn/Assets/Effects/ZRMaterialSurface";
    public static Inputs LoadInputs(SurfaceMaterialDefinition material)
    {
        Texture2D Get(string path)
        {
            if(string.IsNullOrEmpty(path))return null;
            if(!assets.TryGetValue(path,out var texture))
            {
                if(path.EndsWith(".zrdata",StringComparison.OrdinalIgnoreCase))
                {
                    // 标量通道直接上传原始RGBA，绕过图像解码/转换对透明像素RGB的处理。
                    int split=path.IndexOf('/');
                    if(split<1||!ModLoader.TryGetMod(path[..split],out var owner))throw new ArgumentException("数据贴图所属模组不存在");
                    using var stream=new MemoryStream(owner.GetFileBytes(path[(split+1)..]));
                    using var reader=new BinaryReader(stream);
                    if(reader.ReadUInt32()!=0x5444525A||reader.ReadUInt32()!=1)throw new ArgumentException("数据贴图格式无效");
                    int width=reader.ReadInt32(),height=reader.ReadInt32();
                    if(width<1||height<1||width>8192||height>8192||stream.Length-stream.Position!=(long)width*height*4)
                        throw new ArgumentException("数据贴图尺寸或长度无效");
                    texture=new Texture2D(Main.graphics.GraphicsDevice,width,height);texture.SetData(reader.ReadBytes(width*height*4));ownedData.Add(texture);
                }
                else texture=ModContent.Request<Texture2D>(path,AssetRequestMode.ImmediateLoad).Value;
                assets[path]=texture;
            }
            return texture;
        }
        var a=material.Assets;
        return new(){ColorField=Get(a.ColorField),MotionMask=Get(a.MotionMask),NormalMap=Get(a.NormalMap),
            SurfaceMap=Get(a.SurfaceMap),HeightMap=Get(a.HeightMap),EmissionMap=Get(a.EmissionMap),EnvironmentMap=Get(a.EnvironmentMap),
            Brush=Get(a.Brush),Noise=Get(a.Noise),LayerMask=Get(a.LayerMask)};
    }
    public static void Draw(VertexPositionColorTexture[] vertices,int count,SurfaceMaterialDefinition material,
        Inputs inputs,Texture2D scene,Color weaponColor,float opacity,float clock,int width,int height,
        bool unscaled=false,float dissolve=0,int mode=0,int shape=2)
    {
        if(Main.dedServ||count<3||opacity<=0)return;
        material.Validate();inputs??=new();
        var device=Main.graphics.GraphicsDevice;
        if(effect==null)
        {
            effect=ModContent.Request<Effect>(EffectPath,AssetRequestMode.ImmediateLoad).Value.Clone();
            white=new Texture2D(device,1,1);white.SetData(new[]{Color.White});
        }
        if(work.Length<count)work=new Vertex[count];
        HeightPixels heightData=inputs.HeightMap!=null&&material.Displacement!=0?heights.GetValue(inputs.HeightMap,t=>new(t)):null;
        for(int i=0;i<count;i+=3)
        {
            var a=vertices[i];var b=vertices[i+1];var c=vertices[i+2];
            Vector2 e1=new(b.Position.X-a.Position.X,b.Position.Y-a.Position.Y),e2=new(c.Position.X-a.Position.X,c.Position.Y-a.Position.Y);
            var uv1=b.TextureCoordinate-a.TextureCoordinate;var uv2=c.TextureCoordinate-a.TextureCoordinate;
            float determinant=uv1.X*uv2.Y-uv1.Y*uv2.X;
            var along=Vector2.UnitX;var across=Vector2.UnitY;
            if(Math.Abs(determinant)>.000001f)
            {
                along=(e1*uv2.Y-e2*uv1.Y)/determinant;across=(-e1*uv2.X+e2*uv1.X)/determinant;
                along=along.LengthSquared()>.000001f?Vector2.Normalize(along):Vector2.UnitX;
                across=across.LengthSquared()>.000001f?Vector2.Normalize(across):Vector2.UnitY;
            }
            for(int j=0;j<3;j++)
            {
                var source=vertices[i+j];var position=source.Position;
                // 只移动绘制网格，不移动实体或碰撞。高度贴图CPU副本按纹理弱引用缓存。
                if(heightData!=null)
                {var offset=across*((heightData.Sample(source.TextureCoordinate)-.5f)*material.Displacement);position.X+=offset.X;position.Y+=offset.Y;}
                work[i+j]=new(){Position=position,Color=source.Color,UV=source.TextureCoordinate,Along=along,Across=across};
            }
        }
        var p=effect.Parameters;var m=material;
        p["transformMatrix"].SetValue((unscaled?Matrix.Identity:Main.GameViewMatrix.TransformationMatrix)*Matrix.CreateOrthographicOffCenter(0,width,height,0,-1,1));
        var baseColor=m.BaseColor*(m.UseWeaponColor?weaponColor.ToVector3():Vector3.One);
        p["uBase"].SetValue(new Vector4(baseColor,Math.Clamp(VfxIsolation.GlassOpacity(m.Opacity*opacity),0,1)));
        p["uOptics"].SetValue(new Vector4(m.Metallic,m.Roughness,m.Ior,m.Transmission));
        // 色散与屏幕反射：shader 还没实现时参数不存在，安全跳过，不影响其他参数
        p["uDispersion"]?.SetValue(m.Dispersion);
        p["uSsrStrength"]?.SetValue(m.SsrStrength);
        // 质感细节层：最多四层，未填的层权重为 0，着色器直接跳过
        var detailA=new Vector4[4];var detailB=new Vector4[4];var detailColor=new Vector4[4];var detailMaterial=new Vector4[4];
        for(int i=0;i<4&&i<m.DetailLayers.Count;i++)
        {
            var layer=m.DetailLayers[i];
            detailA[i]=new Vector4(Math.Clamp(layer.Weight,0,1),Math.Clamp(layer.Blend,0,1),
                layer.ScaleU,layer.ScrollU);
            detailB[i]=new Vector4(layer.ScaleV,layer.ScrollV,Math.Clamp(layer.Threshold,0,1),0);
            detailColor[i]=new Vector4(layer.Tint,1);
            detailMaterial[i]=new Vector4(layer.RoughnessDelta,layer.MetallicDelta,layer.EmissionStrength,0);
        }
        p["uUseBrush"]?.SetValue(inputs.Brush!=null?1f:0f);
        p["uUseNoise"]?.SetValue(inputs.Noise!=null?1f:0f);
        p["uBrushTex"].SetValue(inputs.Brush??white);
        p["uNoiseTex"].SetValue(inputs.Noise??white);
        p["uLayerMask"].SetValue(inputs.LayerMask??white);
        p["uUseLayerMask"].SetValue(inputs.LayerMask!=null?1f:0f);
        p["uDetailA"]?.SetValue(detailA);
        p["uDetailB"]?.SetValue(detailB);
        p["uDetailColor"]?.SetValue(detailColor);
        p["uDetailMaterial"].SetValue(detailMaterial);
        p["uSurface"].SetValue(new Vector4(m.Specular,m.AnisotropyRotation,m.NormalStrength,m.BumpStrength));
        p["uCoat"].SetValue(new Vector4(m.CoatRoughness,m.CoatIor,m.Sheen,m.Subsurface));
        p["uSheen"].SetValue(new Vector4(m.SheenColor,m.SheenRoughness));
        p["uFilm"].SetValue(new Vector4(m.FilmWeight,m.FilmThickness,m.FilmIor,m.Anisotropy));
        p["uEmission"].SetValue(new Vector4(m.EmissionColor*m.EmissionStrength,m.ClearCoat));
        p["uAbsorption"].SetValue(new Vector4(m.AbsorptionColor,m.AbsorptionDensity));
        p["uSubsurface"].SetValue(new Vector4(m.SubsurfaceColor,m.SubsurfaceRadius));
        p["uVolume"].SetValue(new Vector4(m.VolumeDensity,m.VolumeAnisotropy,m.VolumeEmission,0));
        p["uVolumeColor"].SetValue(m.VolumeColor);
        // 玻璃平面基线：曲率与倾角归零，得到真正平整的表面（法线垂直向上）。
        // 厚度保留，它是折射强度而不是形状。
        bool plainGlass=VfxIsolation.GlassPlaneOnly;
        p["uShape"].SetValue(new Vector4(shape,plainGlass?0f:m.CrossCurvature,plainGlass?0f:m.AlongTilt,m.Thickness));
        // 液态玻璃条带起伏：取 P26 的当前值（0 关闭 / 1 开启），
        // 峰值坡度由 ZRLiquidGlass.fxh 里的三条带参数决定。
        p["uRidgeStrength"].SetValue(VfxIsolation.PartValue("uPartRidges"));
        float az=MathHelper.ToRadians(m.LightAzimuth),el=MathHelper.ToRadians(m.LightElevation);
        p["uLight"].SetValue(new Vector4(MathF.Cos(az)*MathF.Cos(el),MathF.Sin(az)*MathF.Cos(el),MathF.Sin(el),m.EnvironmentStrength));
        p["uLightColor"].SetValue(m.LightColor*m.LightStrength);p["uAmbient"].SetValue(m.AmbientColor);
        // 玻璃平面基线不采样任何外来贴图：色场／动态遮罩／法线／表面／高度／自发光／环境全部关掉。
        p["uMaps"].SetValue(plainGlass?Vector4.Zero:new Vector4(inputs.ColorField!=null?1:0,inputs.MotionMask!=null?1:0,inputs.NormalMap!=null?1:0,inputs.SurfaceMap!=null?1:0));
        p["uMoreMaps"].SetValue(plainGlass?Vector4.Zero:new Vector4(inputs.HeightMap!=null?1:0,inputs.EmissionMap!=null?1:0,inputs.EnvironmentMap!=null?1:0,0));
        var ainfo=m.Assets;var mask=inputs.MotionMask??white;
        int rows=(ainfo.Frames+ainfo.Columns-1)/ainfo.Columns,frame=(int)(Math.Max(0,clock)/ainfo.LoopSeconds*ainfo.Frames)%ainfo.Frames;
        float fw=mask.Width/(float)ainfo.Columns,fh=mask.Height/(float)rows;
        p["uMaskFrame"].SetValue(new Vector4((frame%ainfo.Columns*fw+.5f)/mask.Width,(frame/ainfo.Columns*fh+.5f)/mask.Height,Math.Max(0,fw-1)/mask.Width,Math.Max(0,fh-1)/mask.Height));
        // 厚度以输出像素计；导入更高分辨率的背景时，折射强度不应随源图尺寸改变。
        p["uSceneTexel"].SetValue(new Vector2(1f/width,1f/height));
        p["uTime"].SetValue(clock);p["uDissolve"].SetValue(Math.Clamp(dissolve,0,1));p["uMode"].SetValue((float)mode);
        // 保存全部像素纹理槽，不能让多贴图材质污染下一位绘制者。
        var textures=Enumerable.Range(0,16).Select(i=>device.Textures[i]).ToArray();
        var samplers=Enumerable.Range(0,16).Select(i=>device.SamplerStates[i]).ToArray();
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var buffers=device.GetVertexBuffers();var indices=device.Indices;
        try
        {
            p["uScene"].SetValue(scene??white);p["uColorMap"].SetValue(inputs.ColorField??white);p["uMaskMap"].SetValue(mask);
            p["uNormalMap"].SetValue(inputs.NormalMap??white);p["uSurfaceMap"].SetValue(inputs.SurfaceMap??white);
            p["uHeightMap"].SetValue(inputs.HeightMap??white);p["uEmissionMap"].SetValue(inputs.EmissionMap??white);p["uEnvironment"].SetValue(inputs.EnvironmentMap??white);
            device.BlendState=BlendState.AlphaBlend;device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
            effect.CurrentTechnique.Passes[0].Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,work,0,count/3);
        }
        finally
        {
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;device.SetVertexBuffers(buffers);device.Indices=indices;
            for(int i=0;i<16;i++){device.Textures[i]=textures[i];device.SamplerStates[i]=samplers[i];}
        }
    }
    public override void Unload()
    {
        var old=effect;var oldWhite=white;var data=ownedData.ToArray();ownedData.Clear();effect=null;white=null;assets.Clear();work=[];heights=new();
        Main.QueueMainThreadAction(()=>{old?.Dispose();oldWhite?.Dispose();foreach(var texture in data)texture.Dispose();});
    }
}
