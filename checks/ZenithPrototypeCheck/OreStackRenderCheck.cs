using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 单张隔离对照：只检查本轮新增的矿石叠层提亮，不运行世界或主剑材质矩阵。
public class OreStackRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string root=Environment.GetEnvironmentVariable("ZENITH_ORE_STACK_GPU");
        if(ran||string.IsNullOrEmpty(root)||!Main.gameMenu||Main.menuMode!=0)return;
        ran=true;Directory.CreateDirectory(root);
        var d=Main.graphics.GraphicsDevice;var saved=d.GetRenderTargets();var viewport=d.Viewport;var screen=Main.screenPosition;
        try
        {
            int[] items=[ItemID.CopperBroadsword,ItemID.TinBroadsword,ItemID.TitaniumSword],layers=[1,4,8,14];
            using var target=new RenderTarget2D(d,640,480);
            d.SetRenderTarget(target);d.Clear(new Color(12,17,29));Main.screenPosition=Vector2.Zero;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,
                DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
            for(int row=0;row<3;row++)for(int col=0;col<4;col++)
            {
                Main.instance.LoadItem(items[row]);var texture=TextureAssets.Item[items[row]].Value;
                var growth=new OreStackGrowth();growth.Capture(100,1.5f);growth.SetLayers(layers[col],true);
                var glow=new OreStackGlow();for(int frame=0;frame<60;frame++)glow.Update(growth.Progress);
                Vector2 at=new(80+col*160,80+row*160);var tint=OreFamily.Tints[OreFamily.MaterialOf(items[row])];
                OreSwordEffects.FlyingBlade(texture,at,0,1.85f,tint,1,1,30);
                OreStackGlow.Draw(texture,at,0,1.85f,tint,1,glow.Brightness);
            }
            Main.spriteBatch.End();d.SetRenderTarget(null);
            var pixels=new Color[640*480];target.GetData(pixels);var rows=new List<object>();bool passed=true;
            for(int row=0;row<3;row++)
            {
                var sums=new long[4];
                for(int col=0;col<4;col++)for(int y=0;y<160;y++)for(int x=0;x<160;x++)
                {var c=pixels[(row*160+y)*640+col*160+x];sums[col]+=c.R+c.G+c.B;}
                passed&=Enumerable.Range(1,3).All(i=>sums[i]>sums[i-1]);
                rows.Add(new {item=items[row],layers,totalRgb=sums});
            }
            using(var png=File.Create(Path.Combine(root,"stack-brightness.png")))target.SaveAsPng(png,640,480);
            File.WriteAllText(Path.Combine(root,"gpu.json"),JsonSerializer.Serialize(new {
                passed,actualGpu=true,productionDrawHelpers=true,images=1,rows,
                testedPackageSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
                worldCombatTested=false,multiplayerTested=false
            },new JsonSerializerOptions{WriteIndented=true}));
            Environment.Exit(passed?0:1);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(root,"failure.txt"),ex.ToString());Environment.Exit(1);}
        finally{d.SetRenderTargets(saved);d.Viewport=viewport;Main.screenPosition=screen;}
    }
}
