using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Controlled replays of the production drawing functions, not world combat.
internal static class OreRenderCheck
{
    internal static void Run(string folder)
    {
        var device=Main.graphics.GraphicsDevice;var camera=Main.screenPosition;
        var samples=new List<object>();var hashes=new List<string>();bool restored=true;
        var targetStates=device.GetRenderTargets();var viewport=device.Viewport;
        string lastHash="";int lastPixels=0,lastRightmost=0;Color[] lastColors=null;
        for(int i=0;i<14;i++)Main.instance.LoadItem(OreFamily.DefaultType[i]);
        try
        {
            for(int tick=0;tick<112;tick++)Capture($"ore-volley/ore-{tick:000}.png",()=>Volley(tick));
            Capture("ore-volley.png",()=>Volley(56));
            Capture("ore-formation.png",()=>Volley(35));
            Capture("ore-launch.png",()=>Volley(50));
            Capture("ore-pierce.png",()=>Volley(60));
            Capture("ore-ending.png",()=>Volley(78));
            Capture("ore-tip-closeup.png",()=>TipCloseup(0));
            Capture("ore-tip-rotated.png",()=>TipCloseup(MathHelper.PiOver2));
            Capture("ore-tip-normal.png",()=>TipNormal());
            for(int tick=0;tick<96;tick++)Capture($"ore-burning/ore-{tick:000}.png",()=>Burning(tick/60f));
            Capture("ore-mask-a.png",()=>Burning(0,false));var maskA=lastColors;
            Capture("ore-mask-b.png",()=>Burning(.2f,false));var maskB=lastColors;
            bool Visible(Color c)=>c.R>25 || c.G>30 || c.B>42;
            int animatedPixels=Enumerable.Range(0,maskA.Length).Count(i=>
                Math.Abs(maskA[i].R-maskB[i].R)+Math.Abs(maskA[i].G-maskB[i].G)+Math.Abs(maskA[i].B-maskB[i].B)>18);
            int movingMaskPixels=Enumerable.Range(0,maskA.Length).Count(i=>Visible(maskA[i])!=Visible(maskB[i]));
            Capture("ore-native-tip.png",()=>Sprite(()=>{
                var tex=TextureAssets.Item[OreFamily.DefaultType[13]].Value;
                Main.spriteBatch.Draw(tex,new Vector2(450,360),null,Color.White,MathHelper.PiOver4,tex.Size()*.5f,5.6f,SpriteEffects.None,0);
            }));
            float nativeTipDelta=Math.Abs(lastRightmost-450-OreSwordEffects.BladeReach(TextureAssets.Item[OreFamily.DefaultType[13]].Value,5.6f));
            for(int tick=0;tick<112;tick++)Capture($"ore-skyfall/ore-{tick:000}.png",()=>Skyfall(tick));
            Capture("ore-skyfall.png",()=>Skyfall(44));
            Capture("ore-impact.png",()=>Skyfall(54));
            Capture("ore-giant-ending.png",()=>Skyfall(69));
            Capture("ore-shard-ending.png",()=>EndingSample());
            Capture("ore-traits.png",Traits);
            for(int i=0;i<14;i++)
            {
                int material=i;
                Capture($"ore-material-{i:00}.png",()=>Material(material,Vector2.Zero));
                hashes.Add(lastHash);
            }
            string originHash=hashes[3];
            Main.screenPosition=new(3200,1800);
            Capture("ore-camera-translation.png",()=>Material(3,Main.screenPosition));
            bool cameraStable=lastHash==originHash;
            Main.screenPosition=Vector2.Zero;
            Capture("ore-expired.png",()=>Skyfall(112));int expired=lastPixels;
            bool callbacks=CheckHitCallbacks();
            bool textureCopies=CheckTextureCopies();
            var package=File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod"));
            File.WriteAllText(Path.Combine(folder,"ore-gpu.json"),JsonSerializer.Serialize(new{
                passed=restored && cameraStable && hashes.Distinct().Count()==14 && expired==0 && callbacks && textureCopies && nativeTipDelta<9 && animatedPixels>120 && movingMaskPixels>20,
                version=ModLoader.GetMod("ZenithReborn").Version.ToString(),
                tested_package_sha256=Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
                actualGpu=true,actualSharedRenderer=true,materials=14,paletteImagesDistinct=hashes.Distinct().Count(),
                graphicsStateRestored=restored,cameraTranslationStable=cameraStable,expiredPixels=expired,
                actualHitCallbacks=14,duplicateHitFeedbackSuppressed=callbacks,actualEndCallbacks=17,prelaunchCancellationIncluded=true,
                singleMaskedMaterial=true,fixedPoseAnimatedPixels=animatedPixels,fixedPoseMaskCoverageChanges=movingMaskPixels,
                paletteNeutralCopiesAndSourcePreserved=textureCopies,
                nativeSpriteTipErrorPixels=nativeTipDelta,
                volleyFrames=112,skyfallFrames=112,burningFrames=96,samples,gameplayTested=false,liveMultiplayerTested=false},
                new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{Main.screenPosition=camera;device.SetRenderTargets(targetStates);device.Viewport=viewport;}

        void Capture(string name,Action draw)
        {
            using var target=new RenderTarget2D(device,960,720,false,SurfaceFormat.Color,DepthFormat.None);
            device.SetRenderTarget(target);device.Clear(new Color(12,17,29));
            draw();device.SetRenderTarget(null);
            var colors=new Color[960*720];target.GetData(colors);lastColors=colors;
            lastPixels=colors.Count(c=>c.R>25 || c.G>30 || c.B>42);
            if(name=="ore-native-tip.png")lastRightmost=Enumerable.Range(0,colors.Length)
                .Where(i=>colors[i].R!=12 || colors[i].G!=17 || colors[i].B!=29).Max(i=>i%960);
            lastHash=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(colors.AsSpan())));
            string path=Path.Combine(folder,name);Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var output=File.Create(path))target.SaveAsPng(output,960,720);
            samples.Add(new{name,coloredPixels=lastPixels});
        }
        void Primitives(Action draw)
        {
            var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
            var scissor=device.ScissorRectangle;var textures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
            var samplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
            MartialSlashRenderer.DrawWithRestoredState(draw);
            restored&=device.BlendState==blend && device.DepthStencilState==depth && device.RasterizerState==raster && device.ScissorRectangle==scissor;
            for(int i=0;i<3;i++)restored&=device.Textures[i]==textures[i] && device.SamplerStates[i]==samplers[i];
        }
        void Sprite(Action draw){Main.spriteBatch.Begin();draw();Main.spriteBatch.End();}
        void Blade(int material,Vector2 at,float rotation,float scale,float alpha=1,int launchAge=30,float prepare=0)
        {
            int type=OreFamily.DefaultType[material];var tex=TextureAssets.Item[type].Value;
            OreSwordEffects.FlyingBlade(tex,at,rotation,scale,OreFamily.Tints[material],alpha,1,launchAge,prepare);
        }
        void Coat(int material,Vector2 at,float rotation,float scale,float alpha,float clock,Color? tint=null)
        {
            var texture=TextureAssets.Item[OreFamily.DefaultType[material]].Value;
            OreVisualRenderer.Sheath(at,rotation,OreSwordEffects.BladeReach(texture,scale),scale,tint??OreFamily.Tints[material],
                alpha,clock,960,720,true);
        }
        SwordFlightTrail MakeTrail(Vector2 head,Vector2 direction,float step)
        {
            var trail=new SwordFlightTrail();
            for(int i=7;i>=0;i--)trail.Record(head-direction*step*i,direction.ToRotation());
            return trail;
        }
        void Foreground(List<OreWeaponEnd> ends,float clock)
        {
            Primitives(()=>{
                foreach(var end in ends)OreWeaponEnding.DrawEnd(end,clock,960,720,true);
            });
            Sprite(()=>{
                foreach(var end in ends)
                {
                    OreWeaponEnding.DrawEndGhost(end);
                    foreach(var spawn in OreWeaponEnding.Sparks(end))
                    {
                        var dust=new Dust{active=true};SoulFlowSpark.Configure(dust,spawn);
                        for(int i=0;i<end.Age;i++)SoulFlowSpark.Step(dust);
                        if(dust.active)SoulFlowSpark.DrawSpark(Main.spriteBatch,dust);
                    }
                }
            });
        }
        void TipCloseup(float angle)
        {
            Vector2 at=new(520,360),axis=angle.ToRotationVector2();float reach=OreSwordEffects.BladeReach(TextureAssets.Item[OreFamily.DefaultType[0]].Value,5.55f);
            Sprite(()=>{Blade(0,at,angle,5.55f);Utils.DrawBorderString(Main.spriteBatch,"3x detail",new(24,24),Color.White);});
            Primitives(()=>OreVisualRenderer.Flight(MakeTrail(at,axis,54),OreFamily.Tints[0],42,.35f,960,720,true,tailOffset:96,tipReach:reach));
        }
        void TipNormal()
        {
            Vector2 at=new(480,360);float reach=OreSwordEffects.BladeReach(TextureAssets.Item[OreFamily.DefaultType[0]].Value,1.85f);
            Sprite(()=>Blade(0,at,0,1.85f));
            Primitives(()=>OreVisualRenderer.Flight(MakeTrail(at,Vector2.UnitX,18),OreFamily.Tints[0],14,.35f,960,720,true,tailOffset:32,tipReach:reach));
        }
        void Burning(float clock,bool blade=true)
        {
            Vector2 at=new(450,360);var tex=TextureAssets.Item[OreFamily.DefaultType[13]].Value;
            if(blade)Sprite(()=>OreSwordEffects.Blade(tex,at,0,5.6f,OreVisualRenderer.ForgeGold,1,1));
            Primitives(()=>OreVisualRenderer.Flight(MakeTrail(at,Vector2.UnitX,28),OreVisualRenderer.ForgeGold,43,clock,
                960,720,true,tailOffset:140,tipReach:OreSwordEffects.BladeReach(tex,5.6f)));
        }
        void EndingSample()
        {
            var ends=new List<OreWeaponEnd>();
            for(int i=0;i<3;i++)ends.Add(new(new(230+i*250,360),new(230+i*250,360),Vector2.UnitX,
                OreFamily.Tints[8+i*2],-Terraria.ID.ProjectileID.CrystalShard,1,false,5));
            Foreground(ends,.35f);
        }
        void Volley(int tick)
        {
            Vector2 center=new(480,360);float clock=tick/60f;
            float Distance(int steps){float d=0;for(int i=0;i<steps;i++)d+=OreFlyingSword.LungeSpeed*OreFlightMotion.SpeedMultiplier(i);return d;}
            int flightSteps=Math.Max(0,tick-48);
            float travelled=Distance(flightSteps),radius=tick<=48?250+OreFlightMotion.Pullback(tick,48):272-travelled;
            Sprite(()=>{
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle(444,310,72,100),new Rectangle(0,0,1,1),new Color(31,40,55));
                Utils.DrawBorderString(Main.spriteBatch,"TARGET",new Vector2(445,415),new Color(130,143,161),.8f);
                for(int i=0;i<14;i++)
                {
                    float local=tick-i*1.5f;if(local<=0 || travelled>560)continue;
                    float angle=OreFlyingSword.FormationAngle(i,14);Vector2 radial=angle.ToRotationVector2();
                    Vector2 at=center+radial*radius;
                    Blade(i,at,angle+MathHelper.Pi,1.85f*(tick<=48?OreFlyingSword.BloomScale(local):1),Math.Min(1,local/6),
                        tick>=49?tick-49:30);
                }
            });
            Primitives(()=>{
                for(int i=0;i<14;i++)
                {
                    float angle=OreFlyingSword.FormationAngle(i,14);Vector2 radial=angle.ToRotationVector2();
                    Vector2 at=center+radial*radius;
                    if(tick>48)
                    {
                        var history=new SwordFlightTrail();
                        for(int sample=Math.Max(0,flightSteps-7);sample<=flightSteps;sample++)
                            history.Record(center+radial*(272-Distance(sample)),angle+MathHelper.Pi);
                        if(travelled<=560)
                        {
                            float snap=tick<53?1-(tick-49)/4f:0;
                            float reach=OreSwordEffects.BladeReach(TextureAssets.Item[OreFamily.DefaultType[i]].Value,1.85f*(1+snap*.12f));
                            OreVisualRenderer.Flight(history,OreFamily.Tints[i],14,clock,960,720,true,tailOffset:32,tipReach:reach);
                        }
                    }
                    float local=tick-i*1.5f;
                    if(tick<=48 && local>0 && travelled<=560)
                    {
                        float snap=tick>=49 && tick<53?1-(tick-49)/4f:0;
                        Coat(i,at,angle+MathHelper.Pi,1.85f*(tick<=48?OreFlyingSword.BloomScale(local):1)*(1+snap*.12f),
                            Math.Min(1,local/6),clock);
                    }
                }
            });
            var ends=new List<OreWeaponEnd>();
            int endTick=49+OreFlightMotion.TravelFrames(560,OreFlyingSword.LungeSpeed);
            for(int i=0;i<14;i++)
            {
                Vector2 radial=OreFlyingSword.FormationAngle(i,14).ToRotationVector2();
                if(tick>=endTick && tick<endTick+36)
                {
                    Vector2 terminal=center+radial*(272-Distance(endTick-48));
                    ends.Add(new(terminal,terminal,-radial,OreFamily.Tints[i],OreFamily.DefaultType[i],1.85f,false,tick-endTick));
                }
            }
            Foreground(ends,clock);
        }
        void Skyfall(int tick)
        {
            Vector2 aim=new(480,400);float clock=tick/60f;
            Vector2 at=aim-new Vector2(0,600)+new Vector2(0,Math.Max(0,tick-30)*32);
            Sprite(()=>{
                if(tick<62)OreSwordEffects.Blade(TextureAssets.Item[OreFamily.DefaultType[13]].Value,at,
                    MathHelper.PiOver2,5.6f,OreVisualRenderer.ForgeGold,tick<=30?tick/30f*.55f:1,1);
            });
            Primitives(()=>{
                if(tick>30 && tick<62)OreVisualRenderer.Flight(MakeTrail(at,Vector2.UnitY,32),OreVisualRenderer.ForgeGold,43,clock,960,720,true,tailOffset:140,tipReach:OreSwordEffects.BladeReach(TextureAssets.Item[OreFamily.DefaultType[13]].Value,5.6f));
                if(tick<=30)Coat(13,at,MathHelper.PiOver2,5.6f,tick<=30?tick/30f*.55f:1,clock,OreVisualRenderer.ForgeGold);
            });
            var ends=new List<OreWeaponEnd>();
            if(tick>=62 && tick<98)
                ends.Add(new(aim+new Vector2(0,424),aim,Vector2.UnitY,OreVisualRenderer.ForgeGold,
                    OreFamily.DefaultType[13],5.6f,true,tick-62));
            Foreground(ends,clock);
        }
        void Material(int i,Vector2 offset)
        {
            Vector2 at=offset+new Vector2(600,360);var trail=MakeTrail(at,Vector2.UnitX,18);
            Sprite(()=>Blade(i,at,0,2));
            Primitives(()=>OreVisualRenderer.Flight(trail,OreFamily.Tints[i],18,.35f,960,720,true,tailOffset:32,tipReach:OreSwordEffects.BladeReach(TextureAssets.Item[OreFamily.DefaultType[i]].Value,2)));
        }
        void Traits()
        {
            Primitives(()=>{
                OreVisualRenderer.Shard(new(240,210),-.25f,HardOreKind.Cobalt,12,.35f,960,720,true);
                for(int i=0;i<3;i++)OreVisualRenderer.Shard(new(220+i*34,450+i*12),-.6f+i*.5f,HardOreKind.Orichalcum,12,.35f,960,720,true);
                for(int i=0;i<3;i++)OreVisualRenderer.Shard(new Vector2(690,210)+(i*MathHelper.TwoPi/3).ToRotationVector2()*24,
                    i*MathHelper.TwoPi/3,HardOreKind.Titanium,12,.35f,960,720,true);
            });
            Sprite(()=>{
                for(int k=0;k<3;k++)for(int i=0;i<8;i++)
                {
                    Color tint=OreFamily.Tints[k==0?9:k==1?10:12];
                    Vector2 center=new(170+k*250,600),axis=(i*MathHelper.TwoPi/8).ToRotationVector2();
                    var d=new Dust{active=true};SoulFlowSpark.Configure(d,new(center+axis*25,axis*3,tint,1,36,true));
                    for(int n=0;n<7;n++)SoulFlowSpark.Step(d);SoulFlowSpark.DrawSpark(Main.spriteBatch,d);
                }
            });
        }
        bool CheckHitCallbacks()
        {
            bool menu=Main.gameMenu;OrePierceFeedback.Endings.Clear();
            try
            {
                Main.gameMenu=false;
                var n=new NPC();n.SetDefaults(Terraria.ID.NPCID.Zombie);n.active=true;n.life=n.lifeMax=10000;n.position=new(440,320);
                int Sparks()=>Main.dust.Count(d=>d.active && d.type==ModContent.DustType<SoulFlowSpark>());
                int before=Sparks();var blades=new List<OreFlyingSword>();
                for(int i=0;i<14;i++)
                {
                    var p=new Projectile();p.SetDefaults(ModContent.ProjectileType<OreFlyingSword>());
                    p.Center=n.Center;p.velocity=Vector2.UnitX*OreFlyingSword.LungeSpeed;
                    var f=(OreFlyingSword)p.ModProjectile;f.Material=i;
                    f.OnHitNPC(n,default,1);blades.Add(f);
                }
                bool all=Sparks()-before==56;int first=Sparks();
                foreach(var f in blades)f.OnHitNPC(n,default,1);
                bool contacts=all && Sparks()==first;
                foreach(var f in blades)
                {
                    typeof(OreFlyingSword).GetProperty(nameof(OreFlyingSword.Launched)).SetValue(f,true);
                    f.OnKill(0);f.Projectile.active=false;
                }
                var gp=new Projectile();gp.SetDefaults(ModContent.ProjectileType<OreSkyfall>());gp.Center=new(480,800);
                var giant=(OreSkyfall)gp.ModProjectile;giant.OnHitNPC(n,default,1);giant.OnKill(0);
                var sp=new Projectile();sp.SetDefaults(ModContent.ProjectileType<HardOreShot>());sp.localAI[0]=12;
                ((HardOreShot)sp.ModProjectile).OnKill(0);
                var cp=new Projectile();cp.SetDefaults(ModContent.ProjectileType<OreFlyingSword>());
                typeof(OreFlyingSword).GetField("age",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                    .SetValue(cp.ModProjectile,10);
                ((OreFlyingSword)cp.ModProjectile).OnKill(0);
                return contacts && OrePierceFeedback.Endings.Ends.Count==17 &&
                    OrePierceFeedback.Endings.Ends.Count(e=>e.Giant)==1 &&
                    OrePierceFeedback.Endings.Ends.Count(e=>e.VisualType<0)==1;
            }
            finally{Main.gameMenu=menu;OrePierceFeedback.Endings.Clear();}
        }
        bool CheckTextureCopies()
        {
            foreach(int type in OreFamily.DefaultType)
            {
                var source=TextureAssets.Item[type].Value;var before=new Color[source.Width*source.Height];source.GetData(before);
                var copy=OreSwordEffects.SpectralTexture(source);var colors=new Color[before.Length];copy.GetData(colors);
                var after=new Color[before.Length];source.GetData(after);
                if(ReferenceEquals(source,copy) || !ReferenceEquals(copy,OreSwordEffects.SpectralTexture(source)) ||
                    !before.SequenceEqual(after) || colors.Where((c,i)=>c.A!=before[i].A || c.R!=c.G || c.G!=c.B).Any())return false;
            }
            return true;
        }
    }
}
