using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithPrototypeCheck;

// 仅隔离结构预览；色场使用项目数学噪声，不替换生产武器绘制。
public class MaskCompositionPreview : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Draw;}
    public override void Unload()=>Main.OnPreDraw-=Draw;
    private void Draw(GameTime gameTime)
    {
        string folder=Environment.GetEnvironmentVariable("ZENITH_MASK_PREVIEW");
        if(ran || string.IsNullOrEmpty(folder) || !Main.gameMenu || Main.menuMode!=0)return;
        ran=true;Directory.CreateDirectory(folder);
        var device=Main.graphics.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;var sampler=device.SamplerStates[0];var texture=device.Textures[0];
        try
        {
            var candidates=new List<(string name,int id,int frames,Texture2D texture)>();
            foreach(string name in new[]{"Flames","EyeFire","DD2BetsyFlameBreath","DD2FlameBurstTowerT1Shot",
                "DD2FlameBurstTowerT2Shot","DD2FlameBurstTowerT3Shot","GreekFire1","CursedFlameFriendly","FlameLash"})
            {
                var field=typeof(ProjectileID).GetField(name,BindingFlags.Public|BindingFlags.Static);
                if(field==null)continue;
                int id=Convert.ToInt32(field.GetValue(null));
                var atlas=ModContent.Request<Texture2D>("Terraria/Images/Projectile_"+id,AssetRequestMode.ImmediateLoad).Value;
                // 原版喷火器图集需要手选有效帧，其弹幕帧数元数据固定为一。
                int rows=Main.projFrames[id];
                if((name=="Flames" || name=="DD2BetsyFlameBreath") && atlas.Width==98 && atlas.Height==686)rows=7;
                candidates.Add((name,id,rows,atlas));
                using var png=File.Create(Path.Combine(folder,"source-"+name+".png"));atlas.SaveAsPng(png,atlas.Width,atlas.Height);
            }
            File.WriteAllText(Path.Combine(folder,"assets.json"),JsonSerializer.Serialize(candidates.Select(c=>new{
                c.name,c.id,atlasRows=c.frames,projectileFrameMetadata=Main.projFrames[c.id],width=c.texture.Width,height=c.texture.Height}),new JsonSerializerOptions{WriteIndented=true}));
            if(Environment.GetEnvironmentVariable("ZENITH_MASK_ASSETS_ONLY")=="1")return;
            string selected=Environment.GetEnvironmentVariable("ZENITH_MASK_SOURCE")??"Flames";
            var source=candidates.FirstOrDefault(c=>c.name==selected);
            if(source.texture==null || source.frames<2)throw new Exception("Choose an actually animated native atlas from assets.json");
            const int w=300,h=192,frameCount=120;
            using var color=new RenderTarget2D(device,w,h);
            using var mask=new RenderTarget2D(device,w,h);
            using var result=new RenderTarget2D(device,w,h);
            using var canvas=new RenderTarget2D(device,960,340);
            using var maskAtlas=new Texture2D(device,source.texture.Width,source.texture.Height);
            var pixels=new Color[source.texture.Width*source.texture.Height];source.texture.GetData(pixels);
            for(int i=0;i<pixels.Length;i++)pixels[i]=new(pixels[i].A,pixels[i].A,pixels[i].A,pixels[i].A);
            maskAtlas.SetData(pixels);
            using var noise=PreviewColorNoise.Create(Main.graphics.GraphicsDevice);
            using var addMask=new BlendState{ColorSourceBlend=Blend.One,ColorDestinationBlend=Blend.One,
                AlphaSourceBlend=Blend.One,AlphaDestinationBlend=Blend.One};
            using var applyMask=new BlendState{ColorSourceBlend=Blend.Zero,ColorDestinationBlend=Blend.SourceAlpha,
                AlphaSourceBlend=Blend.Zero,AlphaDestinationBlend=Blend.SourceAlpha};
            Color[] firstColor=null,firstMask=null;int colorChanges=0,maskChanges=0,coverageChanges=0,maxError=0;
            for(int tick=0;tick<frameCount;tick++)
            {
                float time=tick/60f;
                device.SetRenderTarget(color);device.Clear(new Color(15,18,47));
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearWrap,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                Main.spriteBatch.Draw(noise,new Rectangle(0,0,w,h),new Rectangle((int)(time*8),0,noise.Width,noise.Height),new Color(65,205,235)*.75f);
                Main.spriteBatch.Draw(noise,new Rectangle(0,0,w,h),new Rectangle((int)(-time*5),noise.Height/3,noise.Width,noise.Height),new Color(132,60,205)*.28f);
                Main.spriteBatch.End();
                device.SetRenderTarget(mask);device.Clear(Color.Transparent);
                // 只选前三个连续火焰形状，跳过后续散粒帧；交叉插值只作用于蒙版透明度。
                int usedFrames=Math.Min(3,source.frames),cycle=usedFrames*2-2;
                int Frame(int n){int f=n%cycle;return f<usedFrames?f:cycle-f;}
                float phase=time*6;int a=Frame((int)phase),b=Frame((int)phase+1);float mix=phase-MathF.Floor(phase);
                int fh=source.texture.Height/source.frames;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,addMask,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                Main.spriteBatch.Draw(maskAtlas,new Rectangle(0,0,w,h),new Rectangle(0,a*fh,source.texture.Width,fh),Color.White*(1-mix));
                Main.spriteBatch.Draw(maskAtlas,new Rectangle(0,0,w,h),new Rectangle(0,b*fh,source.texture.Width,fh),Color.White*mix);
                Main.spriteBatch.End();
                device.SetRenderTarget(result);device.Clear(Color.Transparent);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                Main.spriteBatch.Draw(color,Vector2.Zero,Color.White);Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,applyMask,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                Main.spriteBatch.Draw(mask,Vector2.Zero,Color.White);Main.spriteBatch.End();
                device.SetRenderTarget(null);
                if(tick is 0 or 12 or 60)
                {
                    var c=new Color[w*h];var m=new Color[w*h];var r=new Color[w*h];color.GetData(c);mask.GetData(m);result.GetData(r);
                    if(tick==0){firstColor=c;firstMask=m;}
                    if(tick==12)
                    {
                        maskChanges=Enumerable.Range(0,m.Length).Count(i=>Math.Abs(m[i].A-firstMask[i].A)>12);
                        coverageChanges=Enumerable.Range(0,m.Length).Count(i=>(m[i].A>100)!=(firstMask[i].A>100));
                    }
                    if(tick==60)colorChanges=Enumerable.Range(0,c.Length).Count(i=>c[i]!=firstColor[i]);
                    for(int i=0;i<c.Length;i++)
                    {
                        maxError=Math.Max(maxError,Math.Abs(r[i].R-(int)MathF.Round(c[i].R*m[i].A/255f)));
                        maxError=Math.Max(maxError,Math.Abs(r[i].G-(int)MathF.Round(c[i].G*m[i].A/255f)));
                        maxError=Math.Max(maxError,Math.Abs(r[i].B-(int)MathF.Round(c[i].B*m[i].A/255f)));
                    }
                }
                device.SetRenderTarget(canvas);device.Clear(new Color(12,17,29));
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                Utils.DrawBorderString(Main.spriteBatch,"COLOR FIELD (slow)",new Vector2(16,24),Color.White,.85f);
                Utils.DrawBorderString(Main.spriteBatch,"MASK (animated)",new Vector2(330,24),Color.White,.85f);
                Utils.DrawBorderString(Main.spriteBatch,"COLOR x MASK",new Vector2(644,24),Color.White,.85f);
                Main.spriteBatch.Draw(color,new Vector2(16,76),Color.White);
                Main.spriteBatch.Draw(mask,new Vector2(330,76),Color.White);
                Main.spriteBatch.Draw(result,new Vector2(644,76),Color.White);
                Utils.DrawBorderString(Main.spriteBatch,"Fixed position / original Terraria animated mask / 60 fps",new Vector2(16,298),new Color(150,160,180),.7f);
                Main.spriteBatch.End();device.SetRenderTarget(null);
                string path=Path.Combine(folder,"frames",$"mask-{tick:000}.png");Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var png=File.Create(path))canvas.SaveAsPng(png,960,340);
                if(tick==12)using(var png=File.Create(Path.Combine(folder,"composition.png")))canvas.SaveAsPng(png,960,340);
            }
            File.WriteAllText(Path.Combine(folder,"preview.json"),JsonSerializer.Serialize(new{
                passed=colorChanges>100 && maskChanges>100 && coverageChanges>20 && maxError<=2,
                nativeMaskSource=source.name,atlasRows=source.frames,usedRows=new[]{0,1,2},exampleTiming=true,frameCount,fps=60,positionFixed=true,
                colorChanges,maskChanges,coverageChanges,multiplicationMaxError=maxError,
                actualGpu=true,productionModified=false,installed=false,finalArtApproved=false},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());}
        finally
        {
            device.SetRenderTargets(targets);device.Viewport=viewport;device.BlendState=blend;
            device.DepthStencilState=depth;device.RasterizerState=raster;device.ScissorRectangle=scissor;
            device.SamplerStates[0]=sampler;device.Textures[0]=texture;Main.instance.Exit();
        }
    }
}
