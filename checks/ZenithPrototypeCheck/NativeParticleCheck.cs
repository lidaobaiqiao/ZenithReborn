using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class NativeParticleCheck
{
    private record Particle(int type,Vector2 position,Vector2 velocity,Color color,float scale,float fadeIn,bool noGravity,int alpha);
    private static Particle[] Capture()=>Main.dust.Where(d=>d.active).Select(d=>new Particle(d.type,d.position,d.velocity,d.color,d.scale,d.fadeIn,d.noGravity,d.alpha)).ToArray();
    private static void Clear(){foreach(var d in Main.dust)d.active=false;}
    internal static void Run(Action<bool,string> check)
    {
        var nativeAI=typeof(Projectile).GetMethod("AI_182_FinalFractal",BindingFlags.Instance|BindingFlags.NonPublic);
        bool oldServer=Main.dedServ,oldMenu=Main.gameMenu,oldPause=Main.gamePaused;
        var oldRandom=Main.rand;int particles=0;
        try
        {
            // CPU-only dust generation. No SpriteBatch, window or GPU drawing.
            Main.dedServ=false;Main.gameMenu=false;Main.gamePaused=false;
            Main.screenPosition=new(500,500);Main.screenWidth=1920;Main.screenHeight=1080;
            var player=new Player{active=true,whoAmI=0,position=new(1000,1000),velocity=new(3,-1)};
            Main.player[0]=player;player.ResetEffects();Lighting.Initialize();
            foreach(int type in new[]{ItemID.CopperShortsword,ItemID.TerraBlade,ItemID.Meowmere})
            {
                var nativeProfile=FinalFractalHelper.GetFinalFractalProfile(type);
                var profile=SoulZenithEffects.Profile(type);
                check(profile.dustMethod.Method==nativeProfile.dustMethod.Method && profile.trailColor==nativeProfile.trailColor && profile.trailWidth==nativeProfile.trailWidth,
                    "Original sword uses its actual installed vanilla particle delegate, color and width");
                var p=new Projectile();p.SetDefaults(ModContent.ProjectileType<SoulBlade>());p.active=true;p.owner=0;p.ai[0]=type;p.localAI[1]=1;
                var blade=(SoulBlade)p.ModProjectile;blade.TargetOffset=new(400,40);blade.Vertical=60;
                // Equalize model display scale for a strict native dust comparison.
                // In normal play particle placement scales with the larger sword.
                blade.SwordScale=.625f;
                var native=new Projectile();native.SetDefaults(ProjectileID.FinalFractal);native.active=true;native.owner=0;
                native.velocity=blade.TargetOffset*.5f;native.ai[0]=blade.Vertical;native.ai[1]=type;native.localAI[1]=1;
                for(int update=0;update<60;update++)
                {
                    int seed=7123+update;
                    Clear();Main.rand=new UnifiedRandom(seed);nativeAI.Invoke(native,null);var expected=Capture();
                    Clear();Main.rand=new UnifiedRandom(seed);blade.AI();var actual=Capture();
                    check(expected.Length==actual.Length,"Actual native AI and soul emit the same number of dust particles per sub-update");
                    for(int i=0;i<actual.Length;i++)
                    {
                        var a=actual[i];var b=expected[i];
                        check(a.type==b.type && a.type==278 && a.color==b.color && a.scale==b.scale && a.fadeIn==b.fadeIn && a.noGravity==b.noGravity && a.alpha==b.alpha,
                            "Generated vanilla dust texture/type, color, scale, fade, gravity and alpha match");
                        check(Vector2.Distance(a.position,b.position)<.01f && Vector2.Distance(a.velocity,b.velocity)<.001f,
                            "Generated particle positions and launch velocities match actual native AI under the same random seed");
                        particles++;
                    }
                }
            }
            check(particles>20,"Comparison exercised real native dust generation rather than empty emissions");
            var wood=SoulZenithEffects.Profile(ItemID.WoodenSword);
            check(wood.dustMethod.Method==FinalFractalHelper.GetFinalFractalProfile(ItemID.TerraBlade).dustMethod.Method && wood.trailColor==SoulVisuals.Tint(ItemID.WoodenSword),
                "Additional hosts retain their palette through the native particle implementation");
            check(SoulZenithEffects.Flare(15)==0 && SoulZenithEffects.Flare(30)==1 && SoulZenithEffects.Flare(45)==0,
                "Native sword-tip star flare has its original phase timing");
            string report=Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report),"native-particle-comparison.json"),
                System.Text.Json.JsonSerializer.Serialize(new{native_ai_executed=true,seeded_subupdate_pairs=180,matched_actual_particles=particles,
                    fields=new[]{"type","position","velocity","color","scale","fadeIn","noGravity","alpha"},display_scale_normalized_for_comparison=true,
                    graphical_drawing_tested=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
        finally{Clear();Main.dedServ=oldServer;Main.gameMenu=oldMenu;Main.gamePaused=oldPause;Main.rand=oldRandom;}
    }
}
