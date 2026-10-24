using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithPrototypeCheck;

// 一个隔离 GPU 客户端顺序导出整组图集，避免逐剑重复启动游戏。
public class AirflowAtlasBatchCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_AIRFLOW_EXPORT");
        string library=Environment.GetEnvironmentVariable("ZENITH_AIRFLOW_RECIPES");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0)return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        try
        {
            foreach(string folder in Directory.GetDirectories(library).OrderBy(value=>value))
            {
                string recipe=Path.Combine(folder,"H4","authoring.json");if(!File.Exists(recipe))continue;
                string name=Path.GetFileName(folder),root=Path.Combine(output,name);Directory.CreateDirectory(root);
                using var json=JsonDocument.Parse(File.ReadAllText(recipe));
                int w=json.RootElement.GetProperty("canvas")[0].GetInt32(),h=256;
                float loop=json.RootElement.GetProperty("loopSeconds").GetSingle();
                int item=ItemID.Search.GetId(name);Main.instance.LoadItem(item);
                using var vector=new SwordBodyVectorMask(device,recipe,TextureAssets.Item[item].Value);
                using var target=new RenderTarget2D(device,w,h);
                using var atlas=new Texture2D(device,w*4,h*4);
                var pixels=new Color[w*h];Color[] first=null;int changed=0,contour=0,invalid=0;
                for(int frame=0;frame<16;frame++)
                {
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,
                        DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                    vector.Draw(frame*loop/16);Main.spriteBatch.End();device.SetRenderTarget(null);
                    target.GetData(pixels);atlas.SetData(0,new Rectangle(frame%4*w,frame/4*h,w,h),pixels,0,pixels.Length);
                    if(frame==0)first=(Color[])pixels.Clone();
                    if(frame==4)for(int i=0;i<pixels.Length;i++)
                    {
                        if(Math.Abs(pixels[i].A-first[i].A)>12)changed++;
                        if((pixels[i].A>64)!=(first[i].A>64))contour++;
                    }
                    foreach(var pixel in pixels)if(pixel.R>pixel.A||pixel.G>pixel.A||pixel.B>pixel.A)invalid++;
                }
                string path=Path.Combine(root,"active.png");using(var png=File.Create(path))atlas.SaveAsPng(png,w*4,h*4);
                bool passed=changed>120&&contour>100&&invalid==0;
                var options=new JsonSerializerOptions{WriteIndented=true};
                File.WriteAllText(Path.Combine(root,"profile.json"),JsonSerializer.Serialize(new{
                    item=name,pivot=new[]{(int)vector.Pivot.X,(int)vector.Pivot.Y},
                    tip=new[]{(int)vector.Tip.X,(int)vector.Tip.Y},bladeCanvasPx=vector.BladeCanvasPx
                },options));
                File.WriteAllText(Path.Combine(root,"gpu.json"),JsonSerializer.Serialize(new{
                    passed,actualGpu=true,revision="H4",maskFrames=16,loopSeconds=loop,
                    fixedPoseMaskChanges=changed,fixedPoseCoverageChanges=contour,invalidPremultiply=invalid,
                    maskSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                    contextBlend="premultiplied additive",artApproved=false,worldCombatTested=false,multiplayerTested=false
                },options));
                if(!passed)throw new Exception("蒙版 GPU 检查失败："+name);
            }
            File.WriteAllText(Path.Combine(output,"complete.txt"),"整组图集导出完成；逐剑 gpu.json 为验证依据。\n");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{device.SetRenderTargets(saved);device.Viewport=viewport;Main.instance.Exit();}
    }
}
