using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class StageOneCheck
{
    internal static void Run(Action<bool,string> check)
    {
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var player=new Player{whoAmI=0,active=true};Main.player[0]=player;player.ResetEffects();
        var item=new Item(ModContent.ItemType<SoulItem>());player.inventory[0]=item;
        var soul=(SoulItem)item.ModItem;
        check(!soul.Attached && soul.Capacity==0 && soul.Potency==0,"New remnant starts unstable");
        var cursor=new Item(ItemID.IronBroadsword);cursor.Prefix(PrefixID.Legendary);
        check(soul.TrySwapHost(0,ref cursor,out _) && cursor.IsAir,"Unstable soul permits weapon preview");
        check(!soul.Active.Any() && !soul.CanUseItem(player),"Preview cannot attack");
        var legacy=new TagCompound();soul.SaveData(legacy);legacy.Remove("attachment");legacy.Remove("kingMemory");
        var migrated=(SoulItem)new Item(item.type).ModItem;migrated.LoadData(legacy);
        check(migrated.Attached && migrated.Hosts[0].prefix==PrefixID.Legendary,"Used old souls migrate without losing hosts or ability");
        var npc=new NPC();npc.SetDefaults(ModContent.NPCType<GuardianSlimeKing>());npc.active=true;
        npc.whoAmI=3;npc.ai[0]=0;npc.Center=player.Center+new Vector2(100,0);Main.npc[3]=npc;
        ((GuardianSlimeKing)npc.ModNPC).SoulId=soul.SoulId;
        int cooldown=0;
        check(npc.friendly && npc.dontTakeDamage && npc.damage==0 && !npc.ModNPC.CanHitPlayer(player,ref cooldown),"Guardian is friendly and harmless");
        for(int i=0;i<3;i++) check(AttachmentStory.Advance(player,3) && !soul.Attached,"Dialogue precedes blessing: "+i);
        bool oldDowned=NPC.downedSlimeKing;
        check(AttachmentStory.Advance(player,3) && soul.Attached && soul.KingMemory,"Accepting final line grants Attachment");
        check(NPC.downedSlimeKing==oldDowned && soul.Kills==0 && soul.Cores==0,"Friendly encounter does not fake boss kills or cores");
        check(soul.Capacity==1 && Math.Abs(soul.Potency-.65f)<.001f && soul.CanUseItem(player),"Blessing enables one slot and 65 percent potency");
        check(!AttachmentStory.Advance(player,3),"Blessing cannot be claimed repeatedly");
        var saved=new TagCompound();soul.SaveData(saved);
        var restored=(SoulItem)new Item(item.type).ModItem;restored.LoadData(saved);
        check(restored.Attached && restored.KingMemory && restored.Hosts[0].prefix==PrefixID.Legendary,"Blessing and prefixed host persist");
        using(var stream=new MemoryStream())
        {
            soul.NetSend(new BinaryWriter(stream));stream.Position=0;restored.NetReceive(new BinaryReader(stream));
            check(restored.Attached && restored.KingMemory && restored.SoulId==soul.SoulId,"Blessing network data persists");
        }
        soul.PreReforge();soul.Item.ResetPrefix();soul.PostReforge();
        check(soul.Attached && soul.KingMemory && !soul.Hosts[0].IsAir,"Goblin reforge preserves blessing and host");
        foreach(var pair in new[]{(ItemID.CopperShortsword,BladeFamily.Swift),(ItemID.Terragrim,BladeFamily.Swift),
            (ItemID.IronBroadsword,BladeFamily.Solid),(ItemID.BoneSword,BladeFamily.Solid),
            (ItemID.IceBlade,BladeFamily.Frost),(ItemID.BladeofGrass,BladeFamily.Nature),
            (ItemID.EnchantedSword,BladeFamily.Arcane),(ItemID.Starfury,BladeFamily.Arcane)})
            check(WeaponFamilies.Of(pair.Item1)==pair.Item2,"Weapon category: "+pair.Item1);
        var target=new NPC();target.SetDefaults(NPCID.Zombie);
        var blade=new Projectile();blade.SetDefaults(ModContent.ProjectileType<SoulBlade>());
        blade.ai[0]=ItemID.IceBlade;blade.ModProjectile.OnHitNPC(target,default,1);
        check(target.HasBuff(BuffID.Frostburn),"Frost sword applies Frostburn");
        blade.ai[0]=ItemID.BladeofGrass;blade.ModProjectile.OnHitNPC(target,default,1);
        check(target.HasBuff(BuffID.Poisoned),"Grass sword applies poison");
        cursor=new Item();check(soul.TrySwapHost(0,ref cursor,out _) && cursor.prefix==PrefixID.Legendary,"Host can be returned intact after blessing");
    }
}

// Explicitly opt-in fixture commands, never installed into the user's mod list.
public class StageFixture : ModCommand
{
    public override string Command=>"zstage";
    public override CommandType Type=>CommandType.Chat;
    public override string Description=>"Isolated first-stage playtest fixture";
    public override void Action(CommandCaller caller,string input,string[] args)
    {
        if(!Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase))
        { caller.Reply("This fixture only runs in stage-one-playtest.");return; }
        var p=caller.Player;
        if(args.FirstOrDefault()=="init")
        {
            int[] items=[ModContent.ItemType<SoulItem>(),ItemID.CopperShortsword,ItemID.IronBroadsword,
                ItemID.IceBlade,ItemID.EnchantedSword,ItemID.Terragrim,ItemID.BladeofGrass,ItemID.BoneSword];
            for(int i=0;i<items.Length;i++) p.inventory[i]=new Item(items[i]);
            Main.dayTime=true;Main.time=12000;
            NPC.NewNPC(p.GetSource_Misc("StageFixture"),(int)p.Center.X+200,(int)p.Bottom.Y,NPCID.BlueSlime);
            caller.Reply("Fixture ready: fresh unstable soul, seven host weapons, daytime and nearby slime.");
        }
        else
        {
            var souls=p.inventory.Select(i=>i.ModItem).OfType<SoulItem>().ToArray();
            string json=System.Text.Json.JsonSerializer.Serialize(new{profile=Main.SavePath,player=p.name,
                souls=souls.Select(s=>new{s.SoulId,s.Attached,s.KingMemory,s.Capacity,s.Potency,s.Kills,s.Cores,hosts=s.Hosts.Select(i=>new{i.type,i.prefix}).ToArray()}).ToArray()},
                new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
            string path=Environment.GetEnvironmentVariable("ZENITH_PLAYTEST_REPORT");
            if(!string.IsNullOrEmpty(path)) File.WriteAllText(path,json);
            caller.Reply(string.Join("; ",souls.Select(s=>$"Attachment={s.Attached}, memory={s.KingMemory}, slots={s.Capacity}, host={s.Hosts[0].Name}")));
        }
    }
}

public class StageFixtureWorld : ModSystem
{
    private bool entered;
    private int ticks;
    public override void OnWorldLoad()=>entered=false;
    public override void PostUpdateWorld()
    {
        if(Environment.GetEnvironmentVariable("ZENITH_STAGE_GUI")!="1" ||
            !Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase) || Main.gameMenu) return;
        Player p=Main.LocalPlayer;
        if(!p.active) return;
        Main.dayTime=true;Main.time=12000;Main.slimeRain=false;
        p.statLife=p.statLifeMax2;p.immune=true;p.immuneTime=60;
        if(++ticks%120==0)
        {
            string output=Environment.GetEnvironmentVariable("ZENITH_PLAYTEST_REPORT");
            var souls=p.inventory.Select(i=>i.ModItem).OfType<SoulItem>().ToArray();
            if(!string.IsNullOrEmpty(output)) File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{
                ticks,profile=Main.SavePath,bladeSpawns=StageCombatProbe.Spawns,bladeHits=StageCombatProbe.Hits,
                souls=souls.Select(s=>new{s.SoulId,s.Attached,s.KingMemory,s.Capacity,s.Potency,
                    hosts=s.Hosts.Select(h=>new{h.type,h.prefix}).ToArray()}).ToArray(),
                targets=Main.npc.Where(n=>n.active && n.type==NPCID.Zombie).Select(n=>new{n.life,
                    frost=n.HasBuff(BuffID.Frostburn),poison=n.HasBuff(BuffID.Poisoned)}).ToArray()
            },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            foreach(var slime in Main.npc.Where(n=>n.active && AttachmentStory.IsOrdinarySlime(n)))
            { slime.dontTakeDamage=true;slime.damage=0; }
            if(souls.Any(s=>s.Attached) && !Main.npc.Any(n=>n.active && n.type==NPCID.Zombie))
            {
                int dummy=NPC.NewNPC(p.GetSource_Misc("StageTarget"),(int)p.Center.X+110,(int)p.Bottom.Y,NPCID.Zombie);
                if(dummy<Main.maxNPCs){Main.npc[dummy].life=Main.npc[dummy].lifeMax=50000;Main.npc[dummy].damage=0;Main.npc[dummy].aiStyle=-1;Main.npc[dummy].knockBackResist=0;}
            }
        }
        if(entered) return;
        entered=true;
        string report=Environment.GetEnvironmentVariable("ZENITH_PLAYTEST_REPORT");
        string marker=report+".initialized";
        if(!File.Exists(marker))
        {
            int[] items=[ModContent.ItemType<SoulItem>(),ItemID.CopperShortsword,ItemID.IronBroadsword,
                ItemID.IceBlade,ItemID.EnchantedSword,ItemID.Terragrim,ItemID.BladeofGrass,ItemID.BoneSword];
            for(int i=0;i<items.Length;i++)p.inventory[i]=new Item(items[i]);
            p.selectedItem=0;
            File.WriteAllText(marker,"Isolated fixture initialized once; do not reset inventory on re-entry.");
        }
        if(!Main.npc.Any(n=>n.active && AttachmentStory.IsOrdinarySlime(n) && Vector2.Distance(n.Center,p.Center)<400))
        {
            int index=NPC.NewNPC(p.GetSource_Misc("StageFixture"),(int)p.Center.X+120,(int)p.Bottom.Y,NPCID.BlueSlime);
            if(index<Main.maxNPCs){Main.npc[index].dontTakeDamage=true;Main.npc[index].damage=0;}
        }
    }
}

public class StageCombatProbe : GlobalProjectile
{
    internal static int Spawns,Hits;
    public override void OnSpawn(Projectile projectile,Terraria.DataStructures.IEntitySource source)
    { if(projectile.ModProjectile is SoulBlade)Spawns++; }
    public override void OnHitNPC(Projectile projectile,NPC target,NPC.HitInfo hit,int damageDone)
    { if(projectile.ModProjectile is SoulBlade && damageDone>0)Hits++; }
}
