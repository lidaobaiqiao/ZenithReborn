using System.Security.Cryptography;
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

// 制作端隔离 GPU 合成预览；不读取人物或世界，不替换正式包。
public class SwordBodyMaskPreview : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime gameTime)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_PREVIEW");
        string input=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_ATLAS");
        string authoring=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_AUTHORING");
        string revision=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_REVISION")??"R1";
        string colorPath=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_COLORFIELD");
        bool localStudy=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_LOCAL")=="1";
        bool emissive=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_EMISSIVE")=="1";
        if(ran || string.IsNullOrEmpty(output) || (string.IsNullOrEmpty(input)&&string.IsNullOrEmpty(authoring)) || !Main.gameMenu || Main.menuMode!=0)return;
        ran=true;Directory.CreateDirectory(output);
        string itemName=Environment.GetEnvironmentVariable("ZENITH_SWORDBODY_ITEM")??"TitaniumSword";
        var itemField=typeof(ItemID).GetField(itemName,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)??throw new InvalidOperationException("Unknown ItemID."+itemName);
        int itemId=itemField.GetValue(null) is short s?s:(int)(short)0;
        var d=Main.graphics.GraphicsDevice;var targets=d.GetRenderTargets();var viewport=d.Viewport;
        var blend=d.BlendState;var depth=d.DepthStencilState;var raster=d.RasterizerState;
        var scissor=d.ScissorRectangle;var sampler=d.SamplerStates[0];var texture=d.Textures[0];
        try
        {
            using var atlas=string.IsNullOrEmpty(authoring)?LoadAtlas():new Texture2D(d,1792,1024);
            Main.instance.LoadItem(itemId);var blade=TextureAssets.Item[itemId].Value;
            using var vector=string.IsNullOrEmpty(authoring)?null:new SwordBodyVectorMask(d,authoring,blade);
            using var noise=PreviewColorNoise.Create(Main.graphics.GraphicsDevice);
            Color baseColor=new(15,18,47),cyan=new(65,205,235),purple=new(132,60,205);
            float cyanOpacity=.75f,purpleOpacity=.28f,cyanSpeed=8,purpleSpeed=-5;string colorRecipe="color._Shared.SlowFlowM1";
            bool colorAdditive=false;
            using var gradient=new Texture2D(d,448,1);bool hasGradient=false;
            if(!string.IsNullOrEmpty(colorPath))
            {
                using var field=JsonDocument.Parse(File.ReadAllText(colorPath));var recipe=field.RootElement.GetProperty("recipe");
                if(revision=="H3"&&field.RootElement.TryGetProperty("variants",out var variants))recipe=variants.GetProperty("H3").GetProperty("recipe");
                Color ReadColor(JsonElement array)=>new(array[0].GetInt32(),array[1].GetInt32(),array[2].GetInt32());
                baseColor=ReadColor(recipe.GetProperty("clear"));var layers=recipe.GetProperty("layers");
                cyan=ReadColor(layers[0].GetProperty("tint"));purple=ReadColor(layers[1].GetProperty("tint"));
                cyanOpacity=layers[0].GetProperty("opacity").GetSingle();purpleOpacity=layers[1].GetProperty("opacity").GetSingle();
                cyanSpeed=layers[0].GetProperty("scrollPixelsPerSecond")[0].GetSingle();purpleSpeed=layers[1].GetProperty("scrollPixelsPerSecond")[0].GetSingle();
                colorRecipe=field.RootElement.GetProperty("id").GetString();
                colorAdditive=recipe.TryGetProperty("composition",out var composition)&&composition.GetString()=="additive";
                if(recipe.TryGetProperty("baseGradient",out var stops))
                {
                    var pixels=new Color[448];
                    for(int x=0;x<448;x++)
                    {
                        float u=x/447f;int j=0;
                        while(j<stops.GetArrayLength()-2&&u>stops[j+1].GetProperty("at").GetSingle())j++;
                        float from=stops[j].GetProperty("at").GetSingle(),to=stops[j+1].GetProperty("at").GetSingle();
                        pixels[x]=Color.Lerp(ReadColor(stops[j].GetProperty("color")),ReadColor(stops[j+1].GetProperty("color")),Math.Clamp((u-from)/(to-from),0,1));
                    }
                    gradient.SetData(pixels);hasGradient=true;
                }
            }
            const int w=448,h=256;
            using var color=new RenderTarget2D(d,w,h);
            using var mask=new RenderTarget2D(d,w,h);
            using var result=new RenderTarget2D(d,w,h);
            using var breakdown=new RenderTarget2D(d,1440,430);
            using var scene=new RenderTarget2D(d,960,600);
            using var envelope=new Texture2D(d,w,1);
            using var add=new BlendState{ColorSourceBlend=Blend.One,ColorDestinationBlend=Blend.One,AlphaSourceBlend=Blend.One,AlphaDestinationBlend=Blend.One};
            using var multiply=new BlendState{ColorSourceBlend=Blend.Zero,ColorDestinationBlend=Blend.SourceAlpha,AlphaSourceBlend=Blend.Zero,AlphaDestinationBlend=Blend.SourceAlpha};
            if(vector!=null)
            {
                var pixels=new Color[w*h];
                for(int i=0;i<16;i++)
                {
                    d.SetRenderTarget(mask);d.Clear(Color.Transparent);Begin(BlendState.AlphaBlend);
                    vector.Draw(i/20f);Main.spriteBatch.End();d.SetRenderTarget(null);mask.GetData(pixels);
                    atlas.SetData(0,new Rectangle(i%4*w,i/4*h,w,h),pixels,0,pixels.Length);
                    Save(mask,$"mask-export/frame-{i:000}.png");
                }
                input=Path.Combine(output,"active.png");using(var png=File.Create(input))atlas.SaveAsPng(png,atlas.Width,atlas.Height);
            }
            Color[] maskAtZero=null;int changed=0,contourChanged=0,multiplyError=0,expiredPixels=0;
            float normalScale=OreSwordEffects.BladeReach(blade,1.85f)/(vector!=null?vector.BladeCanvasPx:103f);
            Vector2 maskPivot=vector!=null?vector.Pivot:new Vector2(305,128);
            Vector2 maskTip=vector!=null?vector.Tip:new Vector2(408,128);
            int frameCount=localStudy?60:144;
            for(int tick=0;tick<frameCount;tick++)
            {
                float time=tick/60f;
                Render(time,false);
                Save(mask,$"mask-only/frame-{tick:000}.png");
                if(tick is 0 or 12)
                {
                    var m=new Color[w*h];mask.GetData(m);
                    if(tick==0)maskAtZero=m;
                    else
                    {
                        changed=Enumerable.Range(0,m.Length).Count(i=>Math.Abs(m[i].A-maskAtZero[i].A)>12);
                        contourChanged=Enumerable.Range(0,m.Length).Count(i=>(m[i].A>64)!=(maskAtZero[i].A>64));
                    }
                }
                if(tick==12)
                {
                    var c=new Color[w*h];var m=new Color[w*h];var r=new Color[w*h];
                    color.GetData(c);mask.GetData(m);result.GetData(r);
                    for(int i=0;i<c.Length;i++)
                        multiplyError=Math.Max(multiplyError,Math.Max(Math.Abs(r[i].R-(int)MathF.Round(c[i].R*m[i].A/255f)),
                            Math.Max(Math.Abs(r[i].G-(int)MathF.Round(c[i].G*m[i].A/255f)),Math.Abs(r[i].B-(int)MathF.Round(c[i].B*m[i].A/255f)))));
                }
                d.SetRenderTarget(breakdown);d.Clear(new Color(12,17,29));Begin(BlendState.AlphaBlend);
                Label("COLOR FIELD (slow)",new(24,22));Label("UNIFIED MASK (animated)",new(496,22));Label("COLOR x MASK",new(968,22));
                Main.spriteBatch.Draw(color,new Vector2(24,92),Color.White);
                Main.spriteBatch.Draw(mask,new Vector2(496,92),Color.White);
                Main.spriteBatch.Draw(result,new Vector2(968,92),Color.White);
                Label(itemName+" / "+revision+" candidate / fixed position / single shared color field",new(24,384),.72f);
                Main.spriteBatch.End();d.SetRenderTarget(null);
                Save(breakdown,$"breakdown/frame-{tick:000}.png");
                if(tick==12)Save(breakdown,"breakdown.png");
                if(tick<120)
                {
                    Render(time,false);DrawContext("ACTIVE LOOP",1);
                    Save(scene,$"loop-context/frame-{tick:000}.png");
                    if(tick==45)Save(scene,"context.png");
                }
                if(localStudy)continue;
                Render(time,true);
                float opacity=time<.3f?Smooth(time/.3f):time<1.9f?1:MathF.Pow(Math.Clamp((2.4f-time)/.5f,0,1),1.2f);
                DrawContext(time<.3f?"APPEAR":time<1.9f?"ACTIVE":"FINISH",opacity);
                Save(scene,$"lifecycle/frame-{tick:000}.png");
                if(tick==45)Save(scene,"context.png");
            }
            int motionFrames=localStudy?0:180;
            int travelFrames=OreFlightMotion.TravelFrames(560,OreFlyingSword.LungeSpeed);
            for(int tick=0;tick<motionFrames;tick++)
            {
                int local=tick%90;float distance=0;
                for(int age=0;age<Math.Min(Math.Max(0,local-18),travelFrames);age++)distance+=OreFlyingSword.LungeSpeed*OreFlightMotion.SpeedMultiplier(age);
                float x=local<18?200-OreFlightMotion.Pullback(local,18):178+distance;
                float visualTime=local<18?local/60f:local<18+travelFrames?.3f+(local-18)/60f:1.9f+(local-18-travelFrames)/60f;
                float opacity=visualTime<.3f?Smooth(visualTime/.3f):visualTime<1.9f?1:Math.Clamp((2.4f-visualTime)/.5f,0,1);
                Render(visualTime,true,tick/60f);DrawContext("MOTION / 60 fps / production acceleration curve",opacity,x-560);
                Save(scene,$"motion/frame-{tick:000}.png");
            }
            Render(2.4f,true);var endPixels=new Color[w*h];result.GetData(endPixels);expiredPixels=endPixels.Count(c=>c.A!=0);
            if(vector!=null)File.WriteAllText(Path.Combine(output,"profile.json"),JsonSerializer.Serialize(new{
                item=itemName,nativeWeapon="Terraria/ItemID."+itemName+", original sprite, no recoloring",
                pivot=new[]{(int)maskPivot.X,(int)maskPivot.Y},tip=new[]{(int)maskTip.X,(int)maskTip.Y},
                bladeCanvasPx=vector.BladeCanvasPx,revision,
                sourceMethod="native SpriteBatch vector recipe; auto-fitted from vanilla sprite, R2 recipe family"
            },new JsonSerializerOptions{WriteIndented=true}));
            var package=File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod"));
            File.WriteAllText(Path.Combine(output,"gpu.json"),JsonSerializer.Serialize(new{
                passed=changed>120 && contourChanged>100 && multiplyError<=2 && expiredPixels==0,
                actualGpu=true,revision,localStudy,maskFrames=16,breakdownFrames=frameCount,loopFrames=Math.Min(120,frameCount),lifecycleFrames=localStudy?0:144,motionFrames,fps=60,
                fixedPoseMaskChanges=changed,fixedPoseCoverageChanges=contourChanged,multiplicationMaxError=multiplyError,expiredPixels,
                normalSpriteScale=1.85,atlasPixelsPerWorldPixel=1/normalScale,anchor=new[]{(int)maskPivot.X,(int)maskPivot.Y},tip=new[]{(int)maskTip.X,(int)maskTip.Y},
                contextBlend=emissive?"premultiplied additive":"premultiplied alpha",colorRecipe,
                motionSource="controlled GPU replay of production OreFlightMotion; not Projectile.Update or world combat",travelFrames,
                maskSource=vector!=null?"native SpriteBatch vector recipe exported to atlas; exact atlas used in all previews":"image_gen registered atlas",
                maskSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant(),
                productionPackageSha256=Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
                nativeWeapon="Terraria/ItemID."+itemName+", original sprite, no recoloring",
                productionModified=false,installed=false,worldCombatTested=false,multiplayerTested=false,artApproved=false
            },new JsonSerializerOptions{WriteIndented=true}));

            Texture2D LoadAtlas()
            {
                using var file=File.OpenRead(input);var loaded=Texture2D.FromStream(d,file);
                if(loaded.Width!=1792 || loaded.Height!=1024)throw new Exception("Expected registered 4x4 448x256 frames");
                var colors=new Color[loaded.Width*loaded.Height];loaded.GetData(colors);
                for(int i=0;i<colors.Length;i++){byte a=colors[i].A;colors[i]=new(a,a,a,a);}loaded.SetData(colors);return loaded;
            }

            void Begin(BlendState state)=>Main.spriteBatch.Begin(SpriteSortMode.Deferred,state,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
            void Label(string text,Vector2 at,float scale=.85f)=>Utils.DrawBorderString(Main.spriteBatch,text,at,Color.White,scale);
            float Smooth(float value)=>MathHelper.SmoothStep(0,1,Math.Clamp(value,0,1));
            void Render(float time,bool phases,float? animationTime=null)
            {
                float clock=animationTime??time;
                d.SetRenderTarget(color);d.Clear(baseColor);
                if(hasGradient){Begin(BlendState.Opaque);Main.spriteBatch.Draw(gradient,new Rectangle(0,0,w,h),Color.White);Main.spriteBatch.End();}
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,colorAdditive?add:BlendState.AlphaBlend,SamplerState.LinearWrap,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
                Main.spriteBatch.Draw(noise,new Rectangle(0,0,w,h),new Rectangle((int)(clock*cyanSpeed),0,noise.Width,noise.Height),cyan*cyanOpacity);
                Main.spriteBatch.Draw(noise,new Rectangle(0,0,w,h),new Rectangle((int)(clock*purpleSpeed),noise.Height/3,noise.Width,noise.Height),purple*purpleOpacity);
                Main.spriteBatch.End();d.SetRenderTarget(mask);d.Clear(Color.Transparent);
                float phase=clock*20;int a=(int)phase%16,b=(a+1)%16;float f=phase-MathF.Floor(phase);
                Rectangle Frame(int index)=>new(index%4*w,index/4*h,w,h);
                Begin(add);Main.spriteBatch.Draw(atlas,Vector2.Zero,Frame(a),Color.White*(1-f));
                Main.spriteBatch.Draw(atlas,Vector2.Zero,Frame(b),Color.White*f);Main.spriteBatch.End();
                if(phases)
                {
                    var values=new Color[w];
                    for(int x=0;x<w;x++)
                    {
                        float u=x/(float)(w-1),v=1;
                        if(time<.3f){float q=Smooth(time/.3f);v=q*Smooth((u-(1-q))/.15f);}
                        else if(time>=1.9f){float q=Math.Clamp((time-1.9f)/.5f,0,1);v=(1-q)*Smooth((u-q)/.16f);}
                        byte alpha=(byte)Math.Clamp(v*255,0,255);values[x]=new(alpha,alpha,alpha,alpha);
                    }
                    envelope.SetData(values);Begin(multiply);Main.spriteBatch.Draw(envelope,new Rectangle(0,0,w,h),Color.White);Main.spriteBatch.End();
                }
                d.SetRenderTarget(result);d.Clear(Color.Transparent);Begin(BlendState.Opaque);
                Main.spriteBatch.Draw(color,Vector2.Zero,Color.White);Main.spriteBatch.End();Begin(multiply);
                Main.spriteBatch.Draw(mask,Vector2.Zero,Color.White);Main.spriteBatch.End();d.SetRenderTarget(null);
            }
            void DrawContext(string phase,float bodyOpacity,float move=0)
            {
                d.SetRenderTarget(scene);d.Clear(new Color(12,17,29));Begin(BlendState.AlphaBlend);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle(0,300,960,300),new Rectangle(0,0,1,1),new Color(158,172,164));
                Label(revision+" / "+phase+" / one mask / "+(emissive?"additive":"alpha"),new(18,18),.75f);

                Main.spriteBatch.End();Begin(emissive?add:BlendState.AlphaBlend);
                for(int row=0;row<2;row++)
                {
                    Vector2 pivot=new(560+move,155+row*300);
                    Main.spriteBatch.Draw(result,pivot-new Vector2(305,128)*normalScale,null,Color.White,0,Vector2.Zero,normalScale,SpriteEffects.None,0);
                }
                Main.spriteBatch.End();Begin(BlendState.AlphaBlend);
                // 制作端遮挡参考：气流在下，原版剑体在上；当前生产气流已撤下。
                for(int row=0;row<2;row++)
                {
                    Vector2 at=new(560+move,155+row*300);
                    Main.spriteBatch.Draw(blade,at,null,Color.White*bodyOpacity,MathHelper.PiOver4,
                        blade.Size()*.5f,1.85f,SpriteEffects.None,0);
                }
                Label("Native flying-sword size / fixed pose / isolated GPU preview, not world combat",new(18,568),.65f);
                Main.spriteBatch.End();d.SetRenderTarget(null);
            }
            void Save(RenderTarget2D target,string name)
            {
                string path=Path.Combine(output,name);Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var png=File.Create(path);target.SaveAsPng(png,target.Width,target.Height);
            }
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally
        {
            d.SetRenderTargets(targets);d.Viewport=viewport;d.BlendState=blend;d.DepthStencilState=depth;d.RasterizerState=raster;
            d.ScissorRectangle=scissor;d.SamplerStates[0]=sampler;d.Textures[0]=texture;Main.instance.Exit();
        }
    }
}
