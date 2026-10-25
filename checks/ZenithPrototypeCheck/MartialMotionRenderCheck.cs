using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 隔离菜单内调用真实弹幕AI与PreDraw；十字标记玩家中心，方框标记真实NPC碰撞框。
// 这是动作GPU回放，不是自然游玩或世界实测，也不操作鼠标键盘。
public class MartialMotionRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_MARTIAL_RENDER");
        if(ran || string.IsNullOrEmpty(output) || !Main.gameMenu || Main.menuMode!=0 ||
            !Main.SavePath.Contains("martial-render-profile",StringComparison.OrdinalIgnoreCase))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;
        var screen=Main.screenPosition;bool menu=Main.gameMenu;
        var samples=new List<object>();int frames=0;
        try
        {
            Main.screenPosition=Vector2.Zero;Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
            using var target=new RenderTarget2D(device,1280,800,false,SurfaceFormat.Color,DepthFormat.None);
            using var textHook=new MonoMod.RuntimeDetour.Hook(typeof(CombatText).GetMethod(nameof(CombatText.NewText),
                [typeof(Rectangle),typeof(Color),typeof(string),typeof(bool),typeof(bool)]),
                typeof(HardOreCheck).GetMethod("HeadlessText",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic));
            bool thunder=false,dash=false,returning=false,returned=false;
            for(int tier=1;tier<=3;tier++)
            {
                foreach(var q in Main.projectile)q.active=false;foreach(var enemy in Main.npc)enemy.active=false;
                var p=new Player{active=true,whoAmI=0,position=new(350,430),direction=1};Main.player[0]=p;p.ResetEffects();
                var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
                soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Seals=SoulCurses.All;
                int[] weapons=[ItemID.Katana,ItemID.Muramasa,ItemID.BloodButcherer,ItemID.FalconBlade];
                for(int i=0;i<weapons.Length;i++)soul.Hosts[i]=new Item(weapons[i]);
                p.inventory[0]=soul.Item;soul.RefreshStats();soul.MartialTier=tier;soul.MartialCharge=300;
                var npc=new NPC();npc.SetDefaults(NPCID.KingSlime);npc.whoAmI=20;npc.active=true;
                npc.width=180;npc.height=160;npc.life=npc.lifeMax=10000000;npc.defense=0;npc.HideStrikeDamage=true;
                npc.Center=p.Center+new Vector2(420,0);Main.npc[20]=npc;
                Main.mouseX=(int)npc.Center.X;Main.mouseY=(int)npc.Center.Y;
                var state=p.GetModPlayer<MartialPlayer>();state.TryActivate();state.BeginAttack(soul);
                int limit=tier==1?100:tier==2?200:360;
                bool sawReturn=false;
                for(int tick=0;tick<limit;tick++)
                {
                    Main.gameMenu=false;
                    var giant=Main.projectile.FirstOrDefault(q=>q.active && q.ModProjectile is MartialGiant)?.ModProjectile as MartialGiant;
                    if(giant?.ReadyForCut==true)giant.StartTimeCut(npc.Center);
                    foreach(var q in Main.projectile.Where(q=>q.active && q.ModProjectile is MartialBlade or MartialGiant or MartialWave).ToArray())
                    {
                        q.ModProjectile.AI();if(!q.active)continue;
                        if(q.ModProjectile is MartialWave)q.Center+=q.velocity;
                        if(q.ModProjectile.CanDamage()!=false && q.ModProjectile.Colliding(q.Hitbox,npc.Hitbox)==true)
                        {Array.Clear(q.localNPCImmunity);Array.Clear(npc.immune);q.Damage();}
                        if(--q.timeLeft<=0)q.Kill();
                    }
                    giant=Main.projectile.FirstOrDefault(q=>q.active && q.ModProjectile is MartialGiant)?.ModProjectile as MartialGiant;
                    thunder|=giant?.Phase==0 && giant.Age==MartialMotion.ThunderImpact;
                    dash|=p.GetModPlayer<MartialDashPlayer>().IsLightDash;
                    sawReturn|=Main.projectile.Any(q=>q.active && q.ModProjectile is MartialBlade b && b.State==4);
                    returning|=sawReturn;
                    if(sawReturn && !Main.projectile.Any(q=>q.active && q.ModProjectile is MartialBlade))returned=true;
                    Main.gameMenu=true;
                    if(tick%2!=0)continue;
                    device.SetRenderTarget(target);device.Clear(new Color(12,17,29));
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend);
                    for(int x=0;x<1280;x+=80)MartialVisual.Line(new(x,0),new(x,800),new Color(22,31,45),1);
                    for(int y=0;y<800;y+=80)MartialVisual.Line(new(0,y),new(1280,y),new Color(22,31,45),1);
                    var rect=npc.Hitbox;
                    MartialVisual.Line(rect.TopLeft(),rect.TopRight(),Color.Gray,1);
                    MartialVisual.Line(rect.TopRight(),rect.BottomRight(),Color.Gray,1);
                    MartialVisual.Line(rect.BottomRight(),rect.BottomLeft(),Color.Gray,1);
                    MartialVisual.Line(rect.BottomLeft(),rect.TopLeft(),Color.Gray,1);
                    if(!p.GetModPlayer<MartialDashPlayer>().IsLightDash)
                    {
                        MartialVisual.Line(p.Center-new Vector2(8,0),p.Center+new Vector2(8,0),Color.White*.7f,2);
                        MartialVisual.Line(p.Center-new Vector2(0,18),p.Center+new Vector2(0,18),Color.White*.7f,2);
                    }
                    Utils.DrawBorderString(Main.spriteBatch,$"{tier}档 · 实际AI / GPU回放 · 第{tick}帧",new(25,25),Color.White,.8f);
                    Utils.DrawBorderString(Main.spriteBatch,"十字：角色中心  方框：目标  /  60次逻辑更新每秒",new(25,65),Color.LightGray,.6f);
                    Main.spriteBatch.End();
                    if(giant!=null)MartialSlashRenderer.DrawWithRestoredState(()=>giant.SlashTrail.DrawPrimitives(tier,1280,800,true,tick/60f));
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend);
                    Color light=Color.White;
                    foreach(var q in Main.projectile.Where(q=>q.active && q.ModProjectile is MartialBlade or MartialGiant or MartialWave))q.ModProjectile.PreDraw(ref light);
                    Main.spriteBatch.End();
                    device.SetRenderTarget(null);
                    string dir=Path.Combine(output,$"tier{tier}");Directory.CreateDirectory(dir);
                    using(var stream=File.Create(Path.Combine(dir,$"{tick/2:000}.png")))target.SaveAsPng(stream,1280,800);
                    frames++;
                    if(tick%10==0)samples.Add(new{tier,tick,phase=giant?.Phase,age=giant?.Age,player=new{p.Center.X,p.Center.Y},
                        pins=Main.projectile.Count(q=>q.active && q.ModProjectile is MartialBlade b && b.State==2),
                        returns=Main.projectile.Count(q=>q.active && q.ModProjectile is MartialBlade b && b.State==4)});
                }
            }
            string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant();
            File.WriteAllText(Path.Combine(output,"motion-gpu.json"),JsonSerializer.Serialize(new{passed=thunder && dash && returning && returned,
                actualGpu=true,actualProjectileAI=true,worldTested=false,liveMultiplayerTested=false,frames,thunder,dash,returning,returned,
                tested_package_sha256=hash,samples},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());}
        finally
        {
            Main.gameMenu=menu;Main.screenPosition=screen;device.SetRenderTargets(targets);device.Viewport=viewport;
            Main.instance.Exit();
        }
    }
}
