using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Opt-in GPU regression, on a separate menu-only profile: no character/world
// loads, save edits, combat automation or production mod-pack interactions.
public class EnvironmentRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=CaptureMenu;}
    public override void Unload()=>Main.OnPreDraw-=CaptureMenu;
    private void CaptureMenu(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_ENV_RENDER");
        if(ran || Main.dedServ || string.IsNullOrEmpty(output) || !Main.gameMenu || Main.menuMode!=0 ||
            !Main.SavePath.Contains("environment-render-profile",StringComparison.OrdinalIgnoreCase))return;
        ran=true;
        var device=Main.graphics.GraphicsDevice;
        var targets=device.GetRenderTargets();var viewport=device.Viewport;
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var sampler=device.SamplerStates[0];var texture=device.Textures[0];
        try
        {
            Directory.CreateDirectory(output);
            var pixel=TextureAssets.MagicPixel.Value;
            var ring=typeof(EnvironmentEffect).GetMethod("Ring",BindingFlags.Static|BindingFlags.NonPublic)
                .CreateDelegate<Action<SpriteBatch,Vector2,float,Color,int,float>>();
            var old=Capture(true,"environment-ring-before.png");var fixedRing=Capture(false,"environment-ring-after.png");
            bool passed=fixedRing.Interior==0 && fixedRing.Outside==0 && fixedRing.Colored>100 &&
                fixedRing.Colored<512*512*.035 && old.Colored>fixedRing.Colored*4;
            File.WriteAllText(Path.Combine(output,"environment-gpu.json"),JsonSerializer.Serialize(new{
                passed,version=ModLoader.GetMod("ZenithReborn").Version.ToString(),
                magicPixelWidth=pixel.Width,magicPixelHeight=pixel.Height,
                before=new{old.Colored,old.Interior,old.Outside},after=new{fixedRing.Colored,fixedRing.Interior,fixedRing.Outside},
                actualSharedEnvironmentRenderer=true,gameplayTested=false,liveMultiplayerTested=false},new JsonSerializerOptions{WriteIndented=true}));

            (int Colored,int Interior,int Outside) Capture(bool legacy,string name)
            {
                using var target=new RenderTarget2D(device,512,512,false,SurfaceFormat.Color,DepthFormat.None);
                using var batch=new SpriteBatch(device);
                device.SetRenderTarget(target);device.Clear(Color.Black);batch.Begin();
                var tint=EnvironmentRules.Color(SwordEnvironment.Forest);
                if(legacy){OldRing(200,128,tint*.15f);OldRing(44,40,tint*.26f);}
                else{ring(batch,new(256,256),200,tint*.15f,128,0);ring(batch,new(256,256),44,tint*.26f,40,0);}
                batch.End();
                device.SetRenderTarget(null);
                using(var stream=File.Create(Path.Combine(output,name)))target.SaveAsPng(stream,512,512);
                var colors=new Color[512*512];target.GetData(colors);int colored=0,interior=0,outside=0;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    if(colors[y*512+x].G<8)continue;colored++;
                    float d=Vector2.Distance(new(x+.5f,y+.5f),new(256,256));
                    if(d>60 && d<180)interior++;if(d>205)outside++;
                }
                return(colored,interior,outside);
                void OldRing(float radius,int steps,Color color)
                {
                    var previous=new Vector2(256+radius,256);
                    for(int i=1;i<=steps;i++)
                    {
                        var next=new Vector2(256,256)+new Vector2(radius,0).RotatedBy(MathHelper.TwoPi*i/steps);
                        var delta=next-previous;
                        batch.Draw(pixel,previous,null,color,delta.ToRotation(),new(0,.5f),new Vector2(delta.Length(),1.4f),SpriteEffects.None,0);
                        previous=next;
                    }
                }
            }
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"environment-gpu-error.txt"),ex.ToString());}
        finally
        {
            device.SetRenderTargets(targets);device.Viewport=viewport;device.BlendState=blend;device.DepthStencilState=depth;
            device.RasterizerState=raster;device.SamplerStates[0]=sampler;device.Textures[0]=texture;
            Main.instance.Exit(); // Graceful exit of this disposable, menu-only client.
        }
    }
}
