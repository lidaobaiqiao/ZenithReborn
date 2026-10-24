using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 调用生产合成入口的隔离 GPU 验证，检查 Alpha 相乘、状态恢复和原版剑体的遮挡顺序。
public class AirflowMaterialCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string root=Environment.GetEnvironmentVariable("ZENITH_AIRFLOW_MATERIAL");
        if(ran||string.IsNullOrEmpty(root)||!Main.gameMenu||Main.menuMode!=0)return;
        ran=true;Run(root);
    }
    // 延迟解析生产合成 API，普通素材导出模式不引用旧包中不存在的方法。
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void Run(string root)
    {
        Directory.CreateDirectory(root);
        var d=Main.graphics.GraphicsDevice;var saved=d.GetRenderTargets();var viewport=d.Viewport;
        var screen=Main.screenPosition;
        try
        {
            int[] items=VfxMaskData.Rows.Select(row=>row.ItemId).ToArray();
            int[] previewItems=[ItemID.CopperShortsword,ItemID.CobaltSword];
            var diagnostics=new List<object>();bool passed=true;
            // 绑定另一绘制者的丢弃型目标，检查合成返回时原画面是否仍在。
            using var sentinel=new RenderTarget2D(d,16,16,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.DiscardContents);
            d.SetRenderTarget(sentinel);var marker=new Color(37,91,143);d.Clear(marker);
            VfxSwordMask.Prepare(items[0],0);
            var sentinelPixels=new Color[256];sentinel.GetData(sentinelPixels);
            bool scenePreserved=sentinelPixels.All(p=>p==marker);
            d.SetRenderTargets(saved);d.Viewport=viewport;
            passed&=scenePreserved;
            foreach(int item in items)
            {
                var beforeBlend=d.BlendState;var beforeSampler=d.SamplerStates[0];var beforeTexture=d.Textures[0];
                var result=VfxSwordMask.Prepare(item,0)??throw new Exception("缺少生产蒙版："+item);
                bool restored=ReferenceEquals(beforeBlend,d.BlendState)&&ReferenceEquals(beforeSampler,d.SamplerStates[0])&&
                    ReferenceEquals(beforeTexture,d.Textures[0])&&d.Viewport.Equals(viewport);
                var atlas=ModContent.Request<Texture2D>("ZenithReborn/Assets/Vfx/SwordBody/"+ItemID.Search.GetName(item),AssetRequestMode.ImmediateLoad).Value;
                var frame=new Color[640*256];var composed=new Color[frame.Length];
                atlas.GetData(0,new Rectangle(0,0,640,256),frame,0,frame.Length);result.GetData(composed);
                int alphaError=0,invalidPremultiply=0;
                for(int i=0;i<frame.Length;i++)
                {
                    alphaError=Math.Max(alphaError,Math.Abs(frame[i].A-composed[i].A));
                    if(composed[i].R>composed[i].A||composed[i].G>composed[i].A||composed[i].B>composed[i].A)invalidPremultiply++;
                }
                passed&=restored&&alphaError<=2&&invalidPremultiply==0;
                diagnostics.Add(new{item,restored,alphaError,invalidPremultiply});
            }
            using var target=new RenderTarget2D(d,960,600);
            for(int tick=0;tick<96;tick++)
            {
                float clock=tick/60f;
                foreach(int item in previewItems)VfxSwordMask.Prepare(item,clock);
                d.SetRenderTarget(target);d.Clear(new Color(12,17,29));
                Main.screenPosition=Vector2.Zero;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,
                    DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle(0,300,960,300),new Rectangle(0,0,1,1),new Color(158,172,164));
                for(int row=0;row<2;row++)for(int column=0;column<2;column++)
                {
                    int item=previewItems[column];Main.instance.LoadItem(item);
                    Vector2 at=new(300+column*460,150+row*300);
                    float alpha=tick<18?0:tick<78?1:MathF.Pow((96-tick)/18f,2);
                    VfxSwordMask.Draw(item,at,0,1.85f,clock,alpha);
                    OreSwordEffects.Blade(TextureAssets.Item[item].Value,at,0,1.85f,Color.White,tick<78?1:alpha,1);
                }
                Main.spriteBatch.End();d.SetRenderTarget(null);
                string path=Path.Combine(root,$"frames/frame-{tick:000}.png");Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var png=File.Create(path);target.SaveAsPng(png,960,600);
            }
            File.WriteAllText(Path.Combine(root,"gpu.json"),JsonSerializer.Serialize(new{
                passed,actualGpu=true,productionRenderer=true,frames=96,fps=60,diagnostics,scenePreserved,
                checkedSwordCount=items.Length,noiseOrigin="project-authored: SwordBodyColorNoise",
                restoredMaskComposition=true,legacyShaderRestored=false,
                testedPackageSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
                scene="原始1.85倍显示尺寸；深浅背景；准备、持续流动、结束衰减",worldCombatTested=false,multiplayerTested=false
            },new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){File.WriteAllText(Path.Combine(root,"error.txt"),error.ToString());}
        finally{d.SetRenderTargets(saved);d.Viewport=viewport;Main.screenPosition=screen;Main.instance.Exit();}
    }
}
