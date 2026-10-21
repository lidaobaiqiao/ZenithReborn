using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Opt-in real graphical-world damage checkpoint, isolated from production saves.
public class OreCombatFixture : ModSystem
{
    private int tick;
    private SoulItem soul;
    private NPC target;
    private readonly List<int> counts=[];
    private bool recorded;
    private static bool TierGui=>Environment.GetEnvironmentVariable("ZENITH_ORE_TIER_GUI")=="1";
    private static bool SharedGui=>Environment.GetEnvironmentVariable("ZENITH_SHARED_SUPPORT_GUI")=="1";
    private static bool Enabled=>!Main.dedServ && Environment.GetEnvironmentVariable("ZENITH_FIX_GUI")=="1" &&
        Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase);
    public override void OnWorldLoad(){tick=0;soul=null;target=null;recorded=false;counts.Clear();}
    public override void PostUpdateWorld()
    {
        if(!Enabled)return;
        var player=Main.LocalPlayer;if(!player.active)return;
        player.immune=true;player.immuneTime=60;player.statLife=player.statLifeMax2;
        if(++tick==60)
        {
            soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
            foreach(int type in OreFamily.DefaultType){soul.Hosts[0]=new Item(type);soul.RefreshStats();}
            for(int i=0;i<soul.Hosts.Length;i++)soul.Hosts[i]=new Item();
            for(int i=0;i<5;i++)soul.Hosts[i]=new Item(OreFamily.DefaultType[i]);
            if(TierGui)
            {
                soul.Seals=SoulCurses.All;
                for(int i=0;i<9;i++)soul.Hosts[i]=new Item(OreFamily.DefaultType[i]);
                soul.Hosts[9]=new Item(ItemID.WoodenSword);soul.SupportHosts[0]=new Item(OreFamily.DefaultType[9]);
                if(SharedGui)
                {
                    soul.Hosts[9]=new Item(OreFamily.DefaultType[9]);
                    for(int j=0;j<4;j++)soul.SupportHosts[j]=new Item(OreFamily.DefaultType[j+10]);
                    soul.AdditionalSupports.Add(new Item(ItemID.FieryGreatsword));
                }
                OreTierProbe.Passes.Clear();OreTierProbe.GiantDamages.Clear();
            }
            soul.RefreshStats();player.inventory[0]=soul.Item;player.selectedItem=0;
            int id=NPC.NewNPC(player.GetSource_Misc("OreFixFixture"),(int)player.Center.X+200,(int)player.Center.Y,NPCID.Zombie);
            target=Main.npc[id];target.aiStyle=-1;target.noGravity=target.noTileCollide=true;
            target.damage=0;target.life=target.lifeMax=100000;target.defense=80;target.knockBackResist=0;
            target.width=target.height=80;target.Center=player.Center+new Vector2(200,-40);target.velocity=Vector2.Zero;
            Main.dayTime=false;Main.time=8000;Main.autoPause=false;Main.playerInventory=false;Main.mapStyle=0;
            OreDamageProbe.Damages.Clear();
        }
        if(target!=null)target.timeLeft=750;
        if(soul!=null && tick>=75 && tick<=(SharedGui?270:TierGui?210:135) && (tick-75)%15==0)
        {
            int ordinal=(tick-75)/15;
            int host=SharedGui?ordinal%10:ordinal;
            int id=soul.EmitBlade(player,soul.Hosts[host],host,5);
            var primary=Main.projectile[id];primary.damage=100;primary.CritChance=0;
            primary.ModProjectile.AI();primary.Center=target.Center;primary.Damage();primary.Kill();
            counts.Add(Main.projectile.Count(p=>p.active && p.ModProjectile is OreFlyingSword));
        }
    }
    public override void PostUpdateProjectiles()
    {
        if(Enabled && !recorded && soul!=null && tick==233 && Environment.GetEnvironmentVariable("ZENITH_CURSOR_RING_GUI")=="1")
        {
            recorded=true;
            var ring=Main.projectile.Where(p=>p.active && p.ModProjectile is OreFlyingSword).ToArray();
            System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"cursor-ring-validation.json"),
                System.Text.Json.JsonSerializer.Serialize(new{tick,formationCounts=counts,materials=OreFamily.Count(soul),
                    ring=ring.Select(p=>new{((OreFlyingSword)p.ModProjectile).RingIndex,((OreFlyingSword)p.ModProjectile).Total,
                        ((OreFlyingSword)p.ModProjectile).Age,((OreFlyingSword)p.ModProjectile).Launched,
                        angle=(p.Center-target.Center).ToRotation(),x=p.Center.X,y=p.Center.Y}).ToArray(),
                    lastPrimary=soul.Hosts[9].type,profile="isolated-stage-one-playtest"}));
            SoulUI.DomainMode=true;SoulUI.Open(soul);Main.autoPause=true;Main.playerInventory=true;
            return;
        }
        if(!Enabled || soul==null || recorded || (TierGui?OreTierProbe.GiantDamages.Count==0:OreDamageProbe.Damages.Count==0))return;
        recorded=true;
        var shots=Main.projectile.Where(p=>p.active && p.ModProjectile is OreFlyingSword).ToArray();
        System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"ore-fix-combat.json"),
            System.Text.Json.JsonSerializer.Serialize(new{tick,formationCounts=counts,
                nativeOreDamage=OreDamageProbe.Damages.ToArray(),targetDefense=target.defense,
                targetLife=target.life,oreMastered=soul.OreMastered,rewardSlots=soul.BonusCapacity,
                woodFull=soul.Wood.Full,models=shots.Length,launched=shots.All(p=>((OreFlyingSword)p.ModProjectile).Launched),
                speed=shots.FirstOrDefault()?.velocity.Length(),profile="isolated-stage-one-playtest"}));
        if(TierGui)System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"ore-tier-combat.json"),
            System.Text.Json.JsonSerializer.Serialize(new{tick,formationCounts=counts,materials=OreFamily.Count(soul),
                giantDamage=OreTierProbe.GiantDamages.ToArray(),flyingHits=OreDamageProbe.Damages.Count,
                supportCapacity=soul.SupportCapacity,supports=soul.Supports.Select(i=>i.type).ToArray(),woodCount=soul.Wood.Count,
                flyingSnapshotDamage=shots.Select(p=>p.damage).ToArray(),
                currentSpeeds=shots.Select(p=>p.velocity.Length()).ToArray(),mastered=soul.OreMastered,
                giantVisible=Main.projectile.Any(p=>p.active && p.ModProjectile is OreSkyfall),profile="isolated-stage-one-playtest"}));
        if(SharedGui){SoulUI.DomainMode=true;SoulUI.Open(soul);SoulUI.SelectSlot(25);}
        Main.autoPause=true;Main.playerInventory=true;
    }
}
