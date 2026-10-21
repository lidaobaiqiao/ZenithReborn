using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;
internal static class OreCheck
{
    private static (Player Player,SoulItem Soul,NPC Target,Projectile Blade) Setup(int count=2)
    {
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
        ModContent.GetInstance<OreSystem>().OnWorldUnload();
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var player=new Player {active=true,whoAmI=0,position=new Vector2(1000,1000)};
        Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
        for(int i=0;i<count;i++)soul.Hosts[i]=new Item(OreFamily.DefaultType[i]);
        player.inventory[0]=soul.Item;player.selectedItem=0;soul.RefreshStats();
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
        target.life=target.lifeMax=100000;target.HideStrikeDamage=true;target.defense=0;
        target.width=80;target.height=120;target.knockBackResist=0;target.Center=player.Center+new Vector2(120,0);Main.npc[20]=target;
        int index=soul.EmitBlade(player,soul.Hosts[0],0,count);var blade=Main.projectile[index];
        blade.damage=100;blade.CritChance=0;blade.ModProjectile.AI();
        return(player,soul,target,blade);
    }
    private static Projectile[] Shots()=>Main.projectile.Where(p=>p.active&&p.ModProjectile is OreFlyingSword).ToArray();
    private static void ExpireCooldowns()
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var global=(Dictionary<string,uint>)typeof(OreSystem).GetField("LastGlobal",flags).GetValue(null);
        var target=(Dictionary<(string Soul,long Target),uint>)typeof(OreSystem).GetField("LastTarget",flags).GetValue(null);
        uint expired=unchecked(Main.GameUpdateCount-61);
        foreach(var key in global.Keys.ToArray())global[key]=expired;
        foreach(var key in target.Keys.ToArray())target[key]=expired;
    }
    internal static void Run(Action<bool,string> check)
    {
        var(player,soul,target,blade)=Setup(10);
        check(OreFamily.MaterialOf(ItemID.CopperShortsword)==OreFamily.MaterialOf(ItemID.CopperBroadsword),"Short/broad count as one material");
        check(soul.Capacity==10 && soul.SupportCapacity==6 && soul.BonusCapacity==0,"Ten ore primaries grant six common supports without wood");
        for(int i=0;i<4;i++)soul.SupportHosts[i]=new Item(OreFamily.DefaultType[10+i]);
        soul.Hosts[10]=new Item(ItemID.TerraBlade);soul.Hosts[11]=new Item(ItemID.StarWrath);
        for(int i=5;i<11;i++)soul.SupportHosts[i]=new Item(ItemID.CopperShortsword);
        check(OreFamily.Count(soul)==14 && soul.Active.Count()==10 && soul.Supports.Count()==4,"Four legal ore supports count alongside ten ore primaries while retired storage stays excluded");
        check(soul.HasLegacyExpansion,"Old expansion data is recognized for safe recovery");
        Item cursor=new(ItemID.EnchantedSword);
        check(!soul.TrySwapHost(17,ref cursor,out _) && cursor.type==ItemID.EnchantedSword,"Legacy ordinary storage rejects incoming item");
        check(!soul.TrySwapHost(19,ref cursor,out _) && cursor.type==ItemID.EnchantedSword,"Legacy support storage rejects incoming item");
        var tag=new TagCompound();soul.SaveData(tag);
        var loaded=(SoulItem)new Item(soul.Type).ModItem;loaded.LoadData(tag);
        check(loaded.Stored.Select(i=>(i.type,i.prefix)).SequenceEqual(soul.Stored.Select(i=>(i.type,i.prefix))),"All inactive legacy items survive save/load without truncation");
        using(var bytes=new MemoryStream())
        {
            soul.NetSend(new BinaryWriter(bytes));bytes.Position=0;loaded.NetReceive(new BinaryReader(bytes));
            check(bytes.Position==bytes.Length && loaded.Stored.Select(i=>i.type).SequenceEqual(soul.Stored.Select(i=>i.type)),"Legacy storage network roundtrip consumes exactly the packet");
        }
        var clone=(SoulItem)soul.Item.Clone().ModItem;clone.Hosts[10].TurnToAir();
        check(!soul.Hosts[10].IsAir,"Legacy storage clone is deep");
        soul.PreReforge();soul.PostReforge();check(soul.Hosts[10].type==ItemID.TerraBlade,"Reforge retains inactive expansion weapon");
        for(int i=17;i<25;i++)
        {
            cursor=new Item();check(soul.TrySwapHost(i,ref cursor,out _) && !cursor.IsAir && soul.ItemAt(i).IsAir,"Each legacy slot supports empty-cursor recovery: "+i);
        }
        check(!soul.HasLegacyExpansion,"Recovery tab disappears after all eight old slots are cleared");
        check(OreFamily.PerSwordDamage(100,2)==157 && OreFamily.PerSwordDamage(100,14)==371,"Flying damage uses the confirmed 150 percent base plus material scaling and ten-tier bonus");
        (player,soul,target,blade)=Setup(6);
        blade.Center=target.Center;blade.Damage();
        check(Shots().Length==1,"One real primary hit creates exactly one ore sword, not six");
        var first=(OreFlyingSword)Shots()[0].ModProjectile;
        for(int i=0;i<50;i++)first.AI();
        check(first.WaitingForVolley && !first.Launched && first.CanDamage()==false,"Partial collection holds fire even after the first blade finishes forming");
        ExpireCooldowns();OreFamily.OnMainHit(blade,target,100);
        check(Shots().Length==1,"Repeated frame damage on an emitted primary cannot add another sword");
        for(int n=1;n<6;n++)NextHit(player,soul,target);
        check(Shots().Length==6 && Shots().Select(p=>((OreFlyingSword)p.ModProjectile).Material).Distinct().Count()==6,
            "Six separate first-hit emissions collect six distinct materials");
        check(Shots().All(p=>!((OreFlyingSword)p.ModProjectile).WaitingForVolley),"Completed collection releases the whole volley");
        var wave=Shots();
        for(int i=0;i<26;i++)foreach(var q in wave)q.ModProjectile.AI();
        check(wave.All(q=>!((OreFlyingSword)q.ModProjectile).Launched),"Older and newer collected blades wait for the same last-sword settle and charge");
        foreach(var q in wave)q.ModProjectile.AI();
        check(wave.All(q=>((OreFlyingSword)q.ModProjectile).Launched && Math.Abs(q.velocity.Length()-11.55f)<.01),"All collected swords launch together at ten-percent faster flight speed");
        check(Vector2.Distance(wave[0].Center,target.Center)>=169 && OreFlyingSword.BloomScale(10)>OreFlyingSword.BloomScale(18),"Distant formation retains the grow-then-settle animation");
        check(Math.Abs(first.DisplayScale-1.85f*wave[0].scale)<.001f && wave[0].width==20,"Display scale stays large without enlarging the original hitbox");
        check(!wave[0].hide && first.VisualType==soul.Hosts[0].type,"Actual hosted sword visual has a visible normal drawing path");
        var shot=wave[0];var mod=(OreFlyingSword)shot.ModProjectile;
        var other=new NPC();other.SetDefaults(NPCID.Zombie);other.active=true;other.whoAmI=21;
        other.life=other.lifeMax=100000;other.HideStrikeDamage=true;other.Center=target.Center;Main.npc[21]=other;
        int before=target.life,otherBefore=other.life;Vector2 velocity=shot.velocity;
        shot.Center=target.Center;target.immune[0]=other.immune[0]=0;shot.Damage();
        check(target.life<before && other.life<otherBefore && shot.active && shot.penetrate==-1 && shot.velocity==velocity,"Actual hit pierces multiple enemies and retains velocity");
        int after=target.life;shot.Damage();check(target.life==after,"Same sword cannot hit twice on one frame");
        for(int i=0;i<6;i++)for(int id=0;id<Main.maxNPCs;id++)if(shot.localNPCImmunity[id]>0)shot.localNPCImmunity[id]--;
        target.immune[0]=other.immune[0]=0;shot.Damage();
        check(target.life<after && Shots().Length==6,"Repeated six-frame damage works and never recursively creates swords");
        (player,soul,target,blade)=Setup(3);NextHit(player,soul,target);
        int blocked=soul.EmitBlade(player,soul.Hosts[0],0,3);
        OreFamily.OnMainHit(Main.projectile[blocked],target,100);
        check(Shots().Length==2,"A fresh primary's first hit is not blocked by another sword's proc");
        ExpireCooldowns();OreFamily.OnMainHit(Main.projectile[blocked],target,100);
        check(Shots().Length==2,"The same primary cannot retry on a later damage frame");
        foreach(var reason in new[]{"zero","friendly","immune","dummy","child","source","held","remote"})
        {
            (player,soul,target,blade)=Setup();Projectile trigger=blade;int damage=100;
            if(reason=="zero")damage=0;if(reason=="friendly")target.friendly=true;
            if(reason=="immune")target.dontTakeDamage=true;if(reason=="dummy")target.type=NPCID.TargetDummy;
            if(reason=="child"){trigger=new Projectile();trigger.SetDefaults(ModContent.ProjectileType<OreFlyingSword>());trigger.owner=0;}
            if(reason=="source")soul.Hosts[0].TurnToAir();if(reason=="held")player.selectedItem=1;
            if(reason=="remote")Main.myPlayer=1;
            OreFamily.OnMainHit(trigger,target,damage);check(Shots().Length==0,"Invalid trigger rejected: "+reason);
        }
        foreach(var reason in new[]{"reused","removed","material","friendly"})
        {
            (player,soul,target,blade)=Setup();NextHit(player,soul,target);NextHit(player,soul,target);
            shot=Shots()[1];mod=(OreFlyingSword)shot.ModProjectile;
            if(reason=="reused"){var fresh=new NPC();fresh.SetDefaults(NPCID.Zombie);fresh.active=true;fresh.whoAmI=20;Main.npc[20]=fresh;}
            if(reason=="removed")soul.Hosts[0].TurnToAir();if(reason=="material")soul.Hosts[1].TurnToAir();
            if(reason=="friendly")target.friendly=true;
            mod.AI();check(!shot.active,"Flying sword rejects reused target slot or invalid source: "+reason);
        }
        // Native movement and collision, with no teleport-to-target or direct
        // child Damage() calls: protect against a cosmetic-only flight.
        foreach(int defense in new[]{0,80})
        {
            (player,soul,target,blade)=Setup(2);target.defense=defense;
            target.velocity=new Vector2(0,.75f);NextHit(player,soul,target);NextHit(player,soul,target);
            var native=Shots();int hp=target.life;OreDamageProbe.Damages.Clear();
            for(int tick=0;tick<130;tick++)
            {
                target.Center+=target.velocity;if(target.immune[0]>0)target.immune[0]--;
                foreach(var q in native)if(q.active)q.Update(q.whoAmI);
            }
            check(hp-target.life>=4 && OreDamageProbe.Damages.Count>0,"Native Update flight damages a moving target with defense "+defense);
            if(defense==80)check(OreDamageProbe.Damages.All(d=>d>=3),"High-defense native impacts retain their source damage share instead of minimum one damage");
        }
        (player,soul,target,blade)=Setup(3);NextHit(player,soul,target);shot=Shots().Single();mod=(OreFlyingSword)shot.ModProjectile;
        target.active=false;ModContent.GetInstance<OreSystem>().PostUpdateEverything();
        for(int i=0;i<28;i++)mod.AI();
        check(shot.active && mod.Launched,"Target death releases a partial volley toward its last position instead of deleting it");
        (player,soul,target,blade)=Setup(3);NextHit(player,soul,target);shot=Shots().Single();
        ExpireCooldowns(); // Aging the formation is done through its actual system clock.
        AdvanceFormationTimeout();ModContent.GetInstance<OreSystem>().PostUpdateEverything();
        check(!((OreFlyingSword)shot.ModProjectile).WaitingForVolley,"An interrupted partial collection is released after its timeout");
        foreach(var p in Main.projectile)p.active=false;foreach(var n in Main.npc)n.active=false;
        CollectionAndFlight(check);
    }

    private static void NextHit(Player player,SoulItem soul,NPC target)
    {
        ExpireCooldowns();
        int index=soul.EmitBlade(player,soul.Hosts.First(i=>!i.IsAir),0,1);
        var blade=Main.projectile[index];blade.damage=100;
        OreFamily.OnMainHit(blade,target,100);
    }
    private static void AdvanceFormationTimeout()
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var data=(System.Collections.IDictionary)typeof(OreSystem).GetField("formations",flags).GetValue(null);
        foreach(var f in data.Values)f.GetType().GetField("LastHit").SetValue(f,unchecked(Main.GameUpdateCount-181));
    }
    private static void CollectionAndFlight(Action<bool,string> check)
    {
        var(player,soul,target,blade)=Setup(0);
        soul.Hosts[10]=new Item(ItemID.CopperBroadsword);soul.SupportHosts[5]=new Item(ItemID.TinBroadsword);
        soul.SupportHosts[0]=new Item(ItemID.IronBroadsword);soul.Hosts[9]=new Item(ItemID.LeadBroadsword);
        soul.Kills=soul.Cores=0;soul.RefreshStats();
        check(soul.OreCollected==0,"Paused and recovery slots do not register collection");
        soul.Attached=false;soul.Hosts[0]=new Item(ItemID.CopperBroadsword);soul.RefreshStats();
        check(soul.OreCollected==0,"Unattached preview does not register collection");
        soul.Attached=true;
        for(int i=0;i<14;i++) { soul.Hosts[0]=new Item(OreFamily.DefaultType[i]);soul.RefreshStats(); }
        check(soul.OreMastered && soul.OreCollected==14,"Sequential real effective hosting collects fourteen materials without extra slots");
        soul.Hosts[0].TurnToAir();soul.RefreshStats();
        check(soul.OreMastered && soul.Capacity==1 && soul.SupportCapacity==0 && soul.BonusCapacity==2,"Mastery persists after removal and unlocks the two shared reward slots");
        Item reward=new(ItemID.TerraBlade);reward.Prefix(PrefixID.Legendary);
        check(soul.TrySwapHost(15,ref reward,out _) && reward.IsAir && soul.Active.Any(i=>i.type==ItemID.TerraBlade),"Ore-only mastery activates the existing first reward slot and preserves its weapon prefix");
        reward=new(ItemID.StarWrath);check(soul.TrySwapHost(16,ref reward,out _) && soul.BonusCapacity==2,"Ore-only mastery activates both shared reward slots");
        check(soul.Active.Count()==2 && !soul.Wood.Full,"Ore mastery does not require or fake Forest Power");
        reward=new Item();soul.TrySwapHost(15,ref reward,out _);reward=new Item();soul.TrySwapHost(16,ref reward,out _);
        var both=(SoulItem)new Item(soul.Type).ModItem;both.Attached=true;both.Kills=SoulProgress.All;both.Cores=63;
        both.OreCollectionMask=OreFamily.AllMaterials;
        for(int i=0;i<7;i++)both.Hosts[i]=new Item(WoodTypes.Types[i]);
        for(int i=0;i<5;i++)both.SupportHosts[i]=new Item(WoodTypes.Types[i+7]);
        both.RefreshStats();
        check(both.Wood.Full && both.BonusCapacity==2,"Both collection families still share two reward slots rather than stacking to four");
        both.Hosts[0].TurnToAir();both.RefreshStats();
        check(!both.Wood.Full && both.BonusCapacity==2,"Removing wood revokes Forest Power but ore mastery keeps shared reward slots usable");
        var tag=new TagCompound();soul.SaveData(tag);var restored=(SoulItem)new Item(soul.Type).ModItem;restored.LoadData(tag);
        check(restored.OreMastered,"Collection survives saving and loading");
        var clone=(SoulItem)soul.Item.Clone().ModItem;check(clone.OreMastered,"Collection survives item cloning");
        using(var data=new MemoryStream())
        {
            soul.NetSend(new BinaryWriter(data));data.Position=0;restored.NetReceive(new BinaryReader(data));
            check(restored.OreMastered && data.Position==data.Length,"Collection network field consumes exactly its packet");
        }
        soul.PreReforge();soul.PostReforge();check(soul.OreMastered,"Collection survives reforge protection");
        var compat=typeof(SoulItem).Assembly.GetType("ZenithReborn.HeroPrefixCompat");
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        compat.GetMethod("Remember",flags).Invoke(null,[soul]);var rebuilt=(SoulItem)new Item(soul.Type).ModItem;
        compat.GetMethod("TryRestore",flags).Invoke(null,[rebuilt]);check(rebuilt.OreMastered,"HERO prefix rebuild restores cumulative collection");
        compat.GetMethod("Clear",flags).Invoke(null,null);
        tag.Remove("oreCollection");restored.LoadData(tag);check(!restored.OreMastered && restored.OreCollected==0,"Old saves never infer full collection from boss progression or inactive slots");

        soul.SupportHosts[0].TurnToAir();soul.Hosts[0]=new Item(ItemID.CopperBroadsword);soul.RefreshStats();
        int source=soul.EmitBlade(player,soul.Hosts[0],0,1);blade=Main.projectile[source];blade.damage=100;
        OreFamily.OnMainHit(blade,target,100);var shot=Shots().Single();var mod=(OreFlyingSword)shot.ModProjectile;
        check(mod.Mastered && shot.damage==OreFamily.PerSwordDamage(100,1,true) && mod.FlightDistance==840,
            "Earned mastery actually applies thirty-percent damage and fifty-percent flight distance to new waves");
        for(int i=0;i<=mod.GatherFrames;i++)mod.AI();
        target.active=false;Vector2 launch=shot.Center;Vector2 direction=shot.velocity.SafeNormalize(Vector2.UnitX);
        shot.Center=launch+direction*800;mod.AI();check(shot.active,"Launched sword continues beyond dead original target across its mastered range");
        using(var data=new MemoryStream())
        {
            mod.SendExtraAI(new BinaryWriter(data));data.Position=0;
            var received=new Projectile();received.SetDefaults(ModContent.ProjectileType<OreFlyingSword>());
            var remote=(OreFlyingSword)received.ModProjectile;remote.ReceiveExtraAI(new BinaryReader(data));
            check(remote.Launched && remote.Mastered && remote.FlightDistance==840 && remote.WaveId==mod.WaveId && data.Position==data.Length,
                "Wave phase, mastery and distance metadata survive network serialization");
        }
        shot.Center=launch+direction*841;mod.AI();check(!shot.active,"Mastered flying sword ends at its bounded flight distance");
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
    }
}

// Actual native impact evidence, separate from position/collision assertions.
public class OreDamageProbe : GlobalNPC
{
    internal static readonly List<int> Damages=[];
    public override void OnHitByProjectile(NPC npc,Projectile projectile,NPC.HitInfo hit,int damageDone)
    {
        if(projectile.ModProjectile is OreFlyingSword)Damages.Add(damageDone);
    }
}
