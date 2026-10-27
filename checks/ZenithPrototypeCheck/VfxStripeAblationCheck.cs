using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 针对用户弧面格纹截图做固定弧面消融；使用生产网格，不修改正式几何。
public sealed class VfxStripeAblationCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_STRIPE_ABLATION");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||
            !Main.SavePath.Contains("stripe-ablation-profile",StringComparison.OrdinalIgnoreCase))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        bool oldDay=Main.dayTime;double oldTime=Main.time;var oldParts=VfxIsolation.PartValues.ToArray();
        try
        {
            Main.dayTime=false;Main.time=12000;
            var centers=new Vector2[13];var rotations=new float[13];
            for(int i=0;i<13;i++)
            {
                float t=1-i/12f;var tip=new Vector2(90+730*t,55+370*t*t);
                var axis=Vector2.Normalize(new Vector2(740*t,-730));
                centers[i]=tip-axis*68;rotations[i]=axis.ToRotation()+MathHelper.PiOver4;
            }
            var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
            var layers=new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)};
            int count=SoulMaterialTrail.BuildLayered(centers,rotations,13,68,Vector2.Zero,vertices,layers,0.3f);
            var variants=new Dictionary<string,int[]>{
                ["baseline-P24-off"]=[],["without-P03"]=[3],["without-P05"]=[5],
                ["without-P09"]=[9],["without-P15"]=[15],["without-P17"]=[17],
                ["without-P18"]=[18],["without-P19"]=[19],
                ["without-P03-P05"]=[3,5],["without-P17-P18-P19"]=[17,18,19],
                ["without-P03-P05-P17-P18-P19"]=[3,5,17,18,19]};
            var hashes=new Dictionary<string,string>();var pixelsByName=new Dictionary<string,Color[]>();
            VfxIsolation.ResetParts();
            Render("installed-defaults",vertices,count);
            foreach(var (name,disabled) in variants)
            {
                SetParts(disabled);
                pixelsByName[name]=Render(name,vertices,count);
            }
            // 同样的输入与高光模型，在仿射平面上不应出现弧面网格的分段。
            SetParts([]);
            var a=new VertexPositionColorTexture(new Vector3(90,90,0),Color.White,new Vector2(0,0));
            var b=new VertexPositionColorTexture(new Vector3(820,90,0),Color.White,new Vector2(1,0));
            var c=new VertexPositionColorTexture(new Vector3(90,410,0),Color.White,new Vector2(0,1));
            var d=new VertexPositionColorTexture(new Vector3(820,410,0),Color.White,new Vector2(1,1));
            Render("affine-plane",[a,b,c,b,d,c],6);
            // 导出生产法线缓冲，直接查看三角形内UV导数带来的方向分段。
            var mask=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            try{mask.SetValue(null,true);Render("curve-normal-output",vertices,count);}
            finally{mask.SetValue(null,false);}
            var baseline=pixelsByName["baseline-P24-off"];
            // 固定亮度与透明度，不经过生产高光/形状计算，检查网格是否重叠叠亮。
            var solid=RenderUniform("geometry-uniform",vertices,count,false);
            int overdraw=solid.Count(c=>c.A>65);
            RenderUniform("geometry-wireframe",vertices,count,true);
            // 每个四边形只翻转内部对角线；边界位置、UV和值域不变。
            var flipped=new VertexPositionColorTexture[count];
            for(int i=0;i<count;i+=6)
            {
                var aa=vertices[i];var bb=vertices[i+1];var cc=vertices[i+2];var dd=vertices[i+4];
                flipped[i]=aa;flipped[i+1]=bb;flipped[i+2]=dd;
                flipped[i+3]=aa;flipped[i+4]=dd;flipped[i+5]=cc;
            }
            SetParts([]);Render("flipped-diagonal",flipped,count);
            SetParts([18]);Render("flipped-diagonal-P18-off",flipped,count);
            var changed=pixelsByName.ToDictionary(p=>p.Key,p=>p.Value.Zip(baseline).Count(pair=>pair.First!=pair.Second));
            var package=Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod");
            File.WriteAllText(Path.Combine(output,"ablation.json"),JsonSerializer.Serialize(new{
                passed=baseline.Any(c=>c.R>20)&&hashes.Count==variants.Count+7,actualGpu=true,productionMesh=true,
                syntheticArcNotExactUserPose=true,P24=0,changed,hashes,vertices=count,uniformOverdrawPixels=overdraw,
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))).ToLowerInvariant(),
                worldTested=false,artApproved=false},new JsonSerializerOptions{WriteIndented=true}));

            void SetParts(int[] disabled)
            {
                for(int i=0;i<VfxIsolation.Parts.Length;i++)
                    VfxIsolation.PartValues[i]=VfxIsolation.Parts[i].Allowed&&i!=23&&!disabled.Contains(i+1)?1:0;
            }
            Color[] RenderUniform(string name,VertexPositionColorTexture[] mesh,int vertexCount,bool wireframe)
            {
                using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None);
                using var effect=new BasicEffect(device){World=Matrix.Identity,View=Matrix.Identity,
                    Projection=Matrix.CreateOrthographicOffCenter(0,960,540,0,-1,1),
                    TextureEnabled=false,VertexColorEnabled=false,LightingEnabled=false,
                    DiffuseColor=Vector3.One,Alpha=.25f};
                using var raster=new RasterizerState{CullMode=CullMode.None,FillMode=wireframe?FillMode.WireFrame:FillMode.Solid};
                device.SetRenderTarget(target);device.Clear(Color.Transparent);
                MartialSlashRenderer.DrawWithRestoredState(()=>{
                    device.BlendState=BlendState.AlphaBlend;device.RasterizerState=raster;
                    foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,mesh,0,vertexCount/3);}
                });
                var pixels=new Color[960*540];target.GetData(pixels);
                hashes[name]=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())));
                using(var file=File.Create(Path.Combine(output,name+".png")))target.SaveAsPng(file,960,540);
                device.SetRenderTarget(null);return pixels;
            }
            Color[] Render(string name,VertexPositionColorTexture[] mesh,int vertexCount)
            {
                using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None);
                device.SetRenderTarget(target);device.Clear(new Color(10,13,17));
                MartialSlashRenderer.DrawWithRestoredState(()=>SoulMaterialTrail.Draw(mesh,vertexCount,new Color(107,139,250),1,.3f,960,540,true));
                var pixels=new Color[960*540];target.GetData(pixels);
                hashes[name]=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())));
                using(var file=File.Create(Path.Combine(output,name+".png")))target.SaveAsPng(file,960,540);
                device.SetRenderTarget(null);return pixels;
            }
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{Array.Copy(oldParts,VfxIsolation.PartValues,oldParts.Length);Main.dayTime=oldDay;Main.time=oldTime;device.SetRenderTargets(saved);device.Viewport=viewport;Main.instance.Exit();}
    }
}
