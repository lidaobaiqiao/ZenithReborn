using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Two deterministic visual checkpoints, only in the isolated local profile.
// Escape resumes from the bloom checkpoint to the piercing-flight checkpoint.
public class CombatFixture : ModSystem
{
    private int ticks,checkpoint;
    private SoulItem soul;
    private NPC target;
    private static bool Enabled=>!Main.dedServ && Environment.GetEnvironmentVariable("ZENITH_COMBAT_GUI")=="1" &&
        Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase);
    public override void OnWorldLoad() { ticks=checkpoint=0;soul=null;target=null; }
    public override void PostUpdateWorld()
    {
        if(!Enabled) return;
        Player player=Main.LocalPlayer;if(!player.active) return;
        player.immune=true;player.immuneTime=60;player.statLife=player.statLifeMax2;
        Main.mapStyle=0;
        if(++ticks==60)
        {
            soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
            foreach(int type in OreFamily.DefaultType) { soul.Hosts[0]=new Item(type);soul.RefreshStats(); }
            for(int i=0;i<soul.Hosts.Length;i++)soul.Hosts[i]=new Item();
            int[] types=[ItemID.CobaltSword,ItemID.MythrilSword,ItemID.OrichalcumSword,ItemID.TitaniumSword,ItemID.AdamantiteSword];
            for(int i=0;i<types.Length;i++)soul.Hosts[i]=new Item(types[i]);
            soul.RefreshStats();player.inventory[0]=soul.Item;player.selectedItem=0;
            int id=NPC.NewNPC(player.GetSource_Misc("CombatRenderFixture"),(int)player.Center.X+220,(int)player.Center.Y,NPCID.Zombie);
            target=Main.npc[id];target.aiStyle=-1;target.noGravity=target.noTileCollide=true;
            target.damage=0;target.life=target.lifeMax=100000;target.HideStrikeDamage=true;
            target.width=80;target.height=100;target.Center=player.Center+new Vector2(220,0);
            Main.dayTime=false;Main.time=8000;Main.autoPause=false;Main.playerInventory=false;
        }
        if(target!=null)target.timeLeft=750;
        if(soul!=null && (ticks==75 || ticks==90 || ticks==105))
        {
            player.selectedItem=0;
            int ordinal=(ticks-75)/15;
            int id=soul.EmitBlade(player,soul.Hosts[ordinal],ordinal,5);
            var blade=Main.projectile[id];var mod=(SoulBlade)blade.ModProjectile;
            mod.TargetOffset=new Vector2(240,-40);mod.Vertical=70-ordinal*35;
            if(ordinal==0) { mod.AI();blade.Center=target.Center;blade.Damage(); }
        }
    }
    public override void PostUpdateProjectiles()
    {
        if(!Enabled || soul==null) return;
        var shots=Main.projectile.Where(p=>p.active && p.ModProjectile is OreFlyingSword).ToArray();
        if(shots.Length==0) return;
        var first=(OreFlyingSword)shots[0].ModProjectile;
        bool freeze=checkpoint==0 && first.Age>=10 || checkpoint==1 && first.Launched && first.Age>=first.GatherFrames+5;
        if(!freeze) return;
        checkpoint++;Main.autoPause=true;Main.playerInventory=true;
        System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,$"combat-render-checkpoint-{checkpoint}.json"),
            System.Text.Json.JsonSerializer.Serialize(new{checkpoint,age=first.Age,gather=first.GatherFrames,
                launched=first.Launched,oreModels=shots.Length,mainModels=Main.projectile.Count(p=>p.active&&p.ModProjectile is SoulBlade),
                lightParticles=Main.dust.Count(d=>d.active&&d.type==ModContent.DustType<SwordSpark>()),
                collected=soul.OreCollected,mastered=soul.OreMastered,damage=shots[0].damage,distance=first.FlightDistance,
                scale=first.DisplayScale,velocity=shots[0].velocity.Length(),profile="isolated-stage-one-playtest"}));
    }
}
