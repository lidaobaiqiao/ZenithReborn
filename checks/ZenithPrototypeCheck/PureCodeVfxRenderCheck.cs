using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 纯代码专项：复用实际带面入口，核对资源状态、开关限制和实际GPU像素。
public sealed class PureCodeVfxRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_PURE_CODE_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||
            !Main.SavePath.Contains("pure-code-vfx-profile",StringComparison.OrdinalIgnoreCase))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        bool oldDay=Main.dayTime;double oldTime=Main.time;
        var points=new Vector2[SoulMaterialTrail.HistoryLength];var rotations=new float[points.Length];
        var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxVertices];
        var hashes=new Dictionary<string,string>();var checks=new Dictionary<string,bool>();
        var ablations=new Dictionary<string,int>();
        try
        {
            // 主光方向还读取世界时刻，跨客户端对照必须同时固定，不能只固定材质动画时间。
            Main.dayTime=true;Main.time=16200;
            VfxIsolation.ApplyPureCodePreset();
            checks["pureCodePolicy"]=VfxIsolation.PureCodeOnly;
            checks["allAllowedDefaultsEnabled"]=VfxIsolation.Branches.All(b=>b.Get()==b.Allowed)&&
                VfxIsolation.Details.All(d=>d.Get()==d.Allowed)&&
                VfxIsolation.Parts.Select((p,i)=>VfxIsolation.PartValues[i]==(p.Allowed?1f:0f)).All(x=>x);
            // 故意传入不存在的素材，验证制作端绑定也不能绕过纯代码限制。
            var rejected=new SurfaceMaterialDefinition();rejected.Assets.NormalMap="ZenithReborn/DOES_NOT_EXIST";
            SwordFlowMaterial.SetMaterialBinding(ItemID.TerraBlade,rejected);
            checks["materialBindingRejected"]=!SwordFlowMaterial.HasSurfaceMaterials;
            var initial=Render("default",.3f);
            checks["visible"]=initial.Count(c=>c.A>0)>1000;
            checks["spriteMaskDisabled"]=!VfxSwordMask.Has(ItemID.CopperBroadsword);
            VfxIsolation.SetAll(true);VfxIsolation.SetAllDetails(true);VfxIsolation.SetAllParts(1);VfxIsolation.SetAllGlassParts(1);
            checks["allOnRespectsPolicy"]=VfxIsolation.Branches.All(b=>b.Allowed||!b.Get())&&
                VfxIsolation.Details.All(d=>d.Allowed||!d.Get())&&
                VfxIsolation.Parts.Select((p,i)=>p.Allowed||VfxIsolation.PartValues[i]==0).All(x=>x)&&
                VfxIsolation.GlassPartValues.All(x=>x==0);
            var all=Render("all-allowed",.3f);
            // 排查预设可以有意关闭部分程序项，不能再假设“允许项全开”等于默认画面。
            checks["allAllowedVisible"]=all.Count(c=>c.A>0)>1000;
            // 绕过面板直接写数组，真正提交shader时仍必须拒绝外部输入项。
            Array.Fill(VfxIsolation.PartValues,1);
            var forced=Render("forced-array",.3f);
            checks["forcedExternalPartsBlocked"]=all.SequenceEqual(forced);
            VfxIsolation.ApplyPureCodePreset();
            checks["resetRestoresDefaultPixels"]=initial.SequenceEqual(Render("restored-defaults",.3f));
            for(int i=0;i<VfxIsolation.Parts.Length;i++)
            {
                if(!VfxIsolation.Parts[i].Allowed)continue;
                float savedValue=VfxIsolation.PartValues[i];
                VfxIsolation.PartValues[i]=0;
                var pixels=Render("without-P"+(i+1).ToString("00"),.3f);
                ablations[VfxIsolation.Parts[i].Uniform]=pixels.Zip(initial).Count(p=>p.First!=p.Second);
                VfxIsolation.PartValues[i]=savedValue;
            }
            checks["shapeOffEmpty"]=ablations["uPartShape"]==initial.Count(c=>c.A>0);
            for(int i=0;i<12;i++)Render("motion-"+i.ToString("00"),i/30f);
            checks["animated"]=hashes["motion-00"]!=hashes["motion-11"];
            foreach(var (name,tint) in new[]{("green",SoulVisuals.Tint(ItemID.TerraBlade)),
                ("blue",SoulVisuals.Tint(ItemID.EnchantedSword)),("pink",SoulVisuals.Tint(ItemID.Starfury))})
                Render(name,.3f,tint:tint);
            checks["threeColorsDistinct"]=new[]{hashes["green"],hashes["blue"],hashes["pink"]}.Distinct().Count()==3;
            checks["opacityZeroEmpty"]=Render("expired",.3f,opacity:0).All(c=>c.A==0);
            var material=typeof(SwordFlowMaterial);var flags=BindingFlags.Static|BindingFlags.NonPublic;
            int Count(string name)=>((ICollection)material.GetField(name,flags).GetValue(null)).Count;
            checks["noArtworkCollections"]=new[]{"artwork","textures","maps","surfaceMaterials","runtimeMaterials","ownedData"}.All(name=>Count(name)==0);
            checks["noDefaultArtwork"]=material.GetField("defaultBrush",flags).GetValue(null)==null&&material.GetField("defaultNoise",flags).GetValue(null)==null;
            var effect=(Effect)material.GetField("effect",flags).GetValue(null);
            string[] uses={"uUseBrush","uUseNoise","uUseNormal","uUseColorField","uUseSurface","uUseHeight","uUseEmission","uUseEnvironment","uUseZoneMask","uUseMotionMask","uHasScene"};
            checks["allArtworkUniformsZero"]=uses.All(name=>(effect.Parameters[name]?.GetValueSingle()??0)==0);
            var white=(Texture2D)material.GetField("white",flags).GetValue(null);
            string[] samplers={"uBrushTex","uNoiseTex","uNormalTex","uSurfaceTex","uHeightTex","uEmissionTex","uEnvironmentTex","uZoneMaskTex","uColorField","uMotionMask","uScene"};
            checks["onlyNeutralRuntimeTexture"]=white.Width==1&&white.Height==1&&samplers.All(name=>effect.Parameters[name]==null||ReferenceEquals(effect.Parameters[name].GetValueTexture2D(),white));
            var package=Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod");
            File.WriteAllText(Path.Combine(output,"pure-code.json"),JsonSerializer.Serialize(new{
                passed=checks.Values.All(v=>v),actualGpu=true,productionRenderer=true,checks,hashes,ablations,
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))).ToLowerInvariant(),
                fixedDayTime=true,fixedWorldTime=16200,worldCombatTested=false,artApproved=false},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{Main.dayTime=oldDay;Main.time=oldTime;VfxIsolation.ApplyPureCodePreset();device.SetRenderTargets(saved);device.Viewport=viewport;Main.instance.Exit();}

        Color[] Render(string name,float clock,Color? tint=null,float opacity=1)
        {
            for(int i=0;i<points.Length;i++)
            {float t=(36-i)*.046f;points[i]=new(450+MathF.Sin(t)*280,280+MathF.Sin(t*1.8f)*100);rotations[i]=t*1.1f+MathHelper.PiOver4;}
            int count=SoulMaterialTrail.Build(points,rotations,points.Length,62,Vector2.Zero,vertices,clock);
            using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None);
            device.SetRenderTarget(target);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>SoulMaterialTrail.Draw(vertices,count,tint??SoulVisuals.Tint(ItemID.TerraBlade),opacity,clock,960,540,true,0,ItemID.TerraBlade));
            var pixels=new Color[960*540];target.GetData(pixels);
            hashes[name]=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())));
            using(var png=File.Create(Path.Combine(output,name+".png")))target.SaveAsPng(png,960,540);
            device.SetRenderTarget(null);return pixels;
        }
    }
}
