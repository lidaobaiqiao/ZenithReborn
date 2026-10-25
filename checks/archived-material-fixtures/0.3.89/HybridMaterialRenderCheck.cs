using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 只在专用菜单夹具内替换自有Effect，逐像素比较R3基底和融合版的光学数据。
public class HybridMaterialRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_HYBRID_GPU");
        if(ran||string.IsNullOrWhiteSpace(output)||!Main.SavePath.Contains("hybrid-render-profile")||!Main.gameMenu||Main.menuMode!=0)return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var effectField=typeof(SwordFlowMaterial).GetField("effect",flags);
        var outputField=typeof(SoulSceneRefraction).GetField("additionalOutput",flags);
        var maskField=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",flags);
        var surfaceField=typeof(SoulSceneRefraction).GetField("<BuildingSurface>k__BackingField",flags);
        Effect current=null;
        using var baseline=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/Baselines/SwordFlow0380",AssetRequestMode.ImmediateLoad).Value.Clone();
        using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        VertexPositionColorTexture[] mesh=[new(new(120,140,0),Color.White,new(0,0)),new(new(840,140,0),Color.White,new(1,0)),
            new(new(120,400,0),Color.White,new(0,1)),new(new(840,140,0),Color.White,new(1,0)),
            new(new(840,400,0),Color.White,new(1,1)),new(new(120,400,0),Color.White,new(0,1))];
        try
        {
            SoulSceneRefraction.EndCapture();
            // 先让生产路径初始化自己的白纹理，再临时替换Effect；不接触正式共享Effect。
            Render(null,0,new(120,195,255),.3f,null);
            current=(Effect)effectField.GetValue(null);
            int maxMask=0,maxProperties=0,maxTransmission=0,maxAlpha=0;
            var cases=new List<object>();
            Color[] tints=[new(120,195,255),new(110,225,90),new(235,125,195)];
            for(int index=0;index<tints.Length;index++)
            {
                Color tint=tints[index];float clock=.3f+index*.37f;
                foreach(int mode in new[]{1,3,4})
                {
                    var a=Render(baseline,mode,tint,clock,null);var b=Render(current,mode,tint,clock,null);
                    int delta=Maximum(a,b);
                    if(mode==1)maxMask=Math.Max(maxMask,delta);
                    if(mode==3)maxProperties=Math.Max(maxProperties,delta);
                    if(mode==4)maxTransmission=Math.Max(maxTransmission,delta);
                }
                var old=Render(baseline,0,tint,clock,$"baseline-{index}.png");
                var hybrid=Render(current,0,tint,clock,$"hybrid-{index}.png");
                int brighter=0,body=0,unchangedBody=0,covered=0,white=0;
                double bodySum=0,bodySquares=0;
                for(int i=0;i<old.Length;i++)
                {
                    maxAlpha=Math.Max(maxAlpha,Math.Abs(old[i].A-hybrid[i].A));
                    if(old[i].A<8)continue;
                    covered++;
                    if(hybrid[i].R>245&&hybrid[i].G>245&&hybrid[i].B>245)white++;
                    int delta=hybrid[i].R+hybrid[i].G+hybrid[i].B-old[i].R-old[i].G-old[i].B;
                    if(delta>12)brighter++;
                    int x=i%960,y=i/960;
                    if(x>150&&x<800&&y>172&&y<322)
                    {
                        body++;if(Math.Abs(hybrid[i].R-old[i].R)+Math.Abs(hybrid[i].G-old[i].G)+Math.Abs(hybrid[i].B-old[i].B)<=3)unchangedBody++;
                        double luminance=hybrid[i].R*.2126+hybrid[i].G*.7152+hybrid[i].B*.0722;
                        bodySum+=luminance;bodySquares+=luminance*luminance;
                    }
                }
                double preserved=unchangedBody/(double)Math.Max(1,body),whiteFraction=white/(double)Math.Max(1,covered);
                double contrast=Math.Sqrt(Math.Max(0,bodySquares/Math.Max(1,body)-Math.Pow(bodySum/Math.Max(1,body),2)));
                // 本轮有意增强有色流丝；不再要求颜色接近暗淡的80，改查内部仍有明暗纹理且没有大面白化。
                bool passed=brighter>1500&&contrast>6&&whiteFraction<.12;
                if(!passed)throw new Exception($"融合材质光效超出约束：{index} brighter={brighter} contrast={contrast} white={whiteFraction}");
                cases.Add(new{index,brighterPixels=brighter,unchangedInteriorFraction=preserved,interiorLuminanceStdDev=contrast,whiteFraction,passed});
            }
            bool success=maxMask==0&&maxProperties==0&&maxTransmission==0&&maxAlpha==0;
            File.WriteAllText(Path.Combine(output,"hybrid.json"),JsonSerializer.Serialize(new{
                passed=success,actualGpu=true,baseline="0.3.80",maximumNormalMaskDifference=maxMask,
                maximumOpticsDifference=maxProperties,maximumTransmissionDifference=maxTransmission,maximumAlphaDifference=maxAlpha,
                cases,testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
                actualMouseAttack=false,liveMultiplayerTested=false},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally
        {
            if(current!=null)effectField.SetValue(null,current);
            outputField.SetValue(null,0);maskField.SetValue(null,false);surfaceField.SetValue(null,false);
            device.SetRenderTargets(targets);device.Viewport=viewport;Main.instance.Exit();
        }
        Color[] Render(Effect effect,int mode,Color tint,float clock,string name)
        {
            if(effect!=null)effectField.SetValue(null,effect);
            outputField.SetValue(null,mode>=3?mode:0);maskField.SetValue(null,mode==1);surfaceField.SetValue(null,false);
            device.SetRenderTarget(target);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(mesh,6,tint,1,clock,960,540,true,
                lighting:Vector3.Normalize(new(.85f,-.25f,.45f))));
            var pixels=new Color[960*540];target.GetData(pixels);
            if(name!=null){using var file=File.Create(Path.Combine(output,name));target.SaveAsPng(file,960,540);}
            return pixels;
        }
        int Maximum(Color[] a,Color[] b)=>Enumerable.Range(0,a.Length).Max(i=>Math.Max(Math.Max(Math.Abs(a[i].R-b[i].R),Math.Abs(a[i].G-b[i].G)),Math.Max(Math.Abs(a[i].B-b[i].B),Math.Abs(a[i].A-b[i].A))));
    }
}
