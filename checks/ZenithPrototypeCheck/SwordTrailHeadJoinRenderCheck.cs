using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 用原版剑图与生产带面测量刀刃接合和护手下方的多余覆盖，不用增亮代替轮廓修正。
public sealed class SwordTrailHeadJoinRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_HEAD_JOIN_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||!Main.SavePath.Contains("head-join-profile"))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        var oldParts=VfxIsolation.PartValues.ToArray();bool oldDay=Main.dayTime;double oldTime=Main.time;
        var mask=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
        try
        {
            Main.dayTime=false;Main.time=12000;VfxIsolation.ResetParts();
            VfxIsolation.PartValues[17]=1;VfxIsolation.PartValues[23]=0;
            var records=new List<object>();
            foreach(int item in new[]{ItemID.EnchantedSword,ItemID.TerraBlade,ItemID.Starfury})
            {
                Main.instance.LoadItem(item);var texture=TextureAssets.Item[item].Value;
                var frame=Main.itemAnimations[item]?.GetFrame(texture)??texture.Frame();
                float scale=1.6f,halfLength=frame.Size().Length()*.5f*scale;
                var centers=new Vector2[13];var rotations=new float[13];var tips=new Vector2[13];var axes=new Vector2[13];
                for(int i=0;i<13;i++)
                {
                    float t=1-i/12f;tips[i]=new(90+700*t,55+295*t*t);
                    axes[i]=Vector2.Normalize(new Vector2(590*t,-700));
                    centers[i]=tips[i]-axes[i]*halfLength;rotations[i]=axes[i].ToRotation()+MathHelper.PiOver4;
                }
                var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
                int count=SoulMaterialTrail.BuildLayered(centers,rotations,13,halfLength,Vector2.Zero,vertices,
                    new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)},.3f);
                var tint=SoulVisuals.Tint(item);
                Render("blade-"+item+"-trail",false,false);
                Render("blade-"+item+"-overlay",false,true);
                var coverage=Render("blade-"+item+"-coverage",true,false);
                // 同时导出正式默认的低亮度状态，避免只看打开宽高光后的接缝。
                VfxIsolation.PartValues[17]=0;
                Render("blade-"+item+"-default-overlay",false,true);
                VfxIsolation.PartValues[17]=1;
                var inside=Vector2.Normalize(tips[1]-tips[0])*2;
                var samples=new List<object>();bool connected=true;
                foreach(float progress in new[]{.15f,.4f,.65f,.75f})
                {
                    Vector2 point=tips[0]-axes[0]*(2*halfLength*progress)+inside;
                    int x=(int)MathF.Round(point.X),y=(int)MathF.Round(point.Y),alpha=AlphaAt(point);
                    connected&=alpha>=24;samples.Add(new{progress,alpha,x,y});
                }
                int guardTailAlpha=AlphaAt(tips[0]-axes[0]*(2*halfLength*.95f)+inside);
                records.Add(new{item,frameWidth=frame.Width,frameHeight=frame.Height,halfLength,connected,samples,
                    guardTailAlpha,guardTailClear=guardTailAlpha<24});

                int AlphaAt(Vector2 point)
                {
                    int x=(int)MathF.Round(point.X),y=(int)MathF.Round(point.Y),alpha=0;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                        if(x+dx>=0&&x+dx<960&&y+dy>=0&&y+dy<540)alpha=Math.Max(alpha,coverage[(y+dy)*960+x+dx].A);
                    return alpha;
                }

                Color[] Render(string name,bool coverageOnly,bool drawSword)
                {
                    using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None);
                    device.SetRenderTarget(target);device.Clear(coverageOnly?Color.Transparent:new Color(10,13,17));
                    mask.SetValue(null,coverageOnly);
                    try{MartialSlashRenderer.DrawWithRestoredState(()=>SoulMaterialTrail.Draw(vertices,count,tint,1,.3f,960,540,true,0,item));}
                    finally{mask.SetValue(null,false);}
                    if(drawSword)
                    {
                        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                        Main.spriteBatch.Draw(texture,centers[0],frame,Color.White,rotations[0],frame.Size()*.5f,scale,SpriteEffects.None,0);
                        Main.spriteBatch.End();
                    }
                    var pixels=new Color[960*540];target.GetData(pixels);
                    using(var file=File.Create(Path.Combine(output,name+".png")))target.SaveAsPng(file,960,540);
                    device.SetRenderTarget(null);return pixels;
                }
            }
            File.WriteAllText(Path.Combine(output,"head-join.json"),JsonSerializer.Serialize(new{
                actualGpu=true,productionMesh=true,syntheticPoses=true,originalSwordTextures=true,records,
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))) .ToLowerInvariant(),
                worldTested=false,artApproved=false},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{mask.SetValue(null,false);Array.Copy(oldParts,VfxIsolation.PartValues,oldParts.Length);Main.dayTime=oldDay;Main.time=oldTime;device.SetRenderTargets(saved);device.Viewport=viewport;Main.instance.Exit();}
    }
}
