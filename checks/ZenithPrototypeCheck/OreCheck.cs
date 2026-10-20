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
    internal static void Run(Action<bool,string> check)
    {
        var(player,soul,target,blade)=Setup(10);
        check(OreFamily.MaterialOf(ItemID.CopperShortsword)==OreFamily.MaterialOf(ItemID.CopperBroadsword),"Short/broad count as one material");
        check(soul.Capacity==10 && soul.SupportCapacity==0 && soul.BonusCapacity==0,"Ore never expands original active 17-slot layout or unlocks wood support");
        for(int i=0;i<4;i++)soul.SupportHosts[i]=new Item(OreFamily.DefaultType[10+i]);
        soul.Hosts[10]=new Item(ItemID.TerraBlade);soul.Hosts[11]=new Item(ItemID.StarWrath);
        for(int i=5;i<11;i++)soul.SupportHosts[i]=new Item(ItemID.CopperShortsword);
        check(OreFamily.Count(soul)==10 && soul.Active.Count()==10 && soul.Supports.Count()==0,"Paused/legacy storage cannot contribute damage, materials or unlocks");
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
        check(OreFamily.PerSwordDamage(100,2)==8 && OreFamily.PerSwordDamage(100,14)==13,"Damage remains source snapshot times per-material scale");
        (player,soul,target,blade)=Setup();
        blade.Center=target.Center;blade.Damage();
        check(Shots().Length==2,"Actual main Projectile.Damage triggers one child per material");
        var shot=Shots()[0];var mod=(OreFlyingSword)shot.ModProjectile;
        check(!shot.hide && mod.Texture.Contains("Terraria/Images/Item_"),"Flying sword has a valid fallback texture and normal drawing path");
        check(mod.VisualType==soul.Hosts[0].type,"Flying sword retains actual hosted sword visual");
        for(int i=0;i<20;i++)mod.AI();
        check(shot.active && mod.CanDamage()==false && shot.velocity==Vector2.Zero,"No premature damage during the full twenty-frame forming phase");
        mod.AI();check(shot.active && Math.Abs(shot.velocity.Length()-24)<.01 && mod.CanDamage()!=false,"Lunge starts after forming at expected speed");
        var other=new NPC();other.SetDefaults(NPCID.Zombie);other.whoAmI=21;other.active=true;other.Center=target.Center;Main.npc[21]=other;
        check(mod.CanHitNPC(other)==false && mod.CanHitNPC(target)!=false,"Child excludes unrelated enemies even when overlapping");
        int before=target.life;shot.Center=target.Center;target.immune[0]=0;shot.Damage();
        check(target.life<before && !shot.active,"Child deals actual one-hit damage and finishes");
        check(Shots().Length==1,"Child hit does not recursively create a new wave");
        OreFamily.OnMainHit(blade,target,100);check(Shots().Length==1,"Repeated same-frame main hit respects cooldown");
        foreach(var reason in new[]{"zero","friendly","immune","dummy","child","source","held","remote"})
        {
            (player,soul,target,blade)=Setup();
            Projectile trigger=blade;int damage=100;
            if(reason=="zero")damage=0;
            if(reason=="friendly")target.friendly=true;
            if(reason=="immune")target.dontTakeDamage=true;
            if(reason=="dummy")target.type=NPCID.TargetDummy;
            if(reason=="child") {trigger=new Projectile();trigger.SetDefaults(ModContent.ProjectileType<OreFlyingSword>());trigger.owner=0;}
            if(reason=="source")soul.Hosts[0].TurnToAir();
            if(reason=="held")player.selectedItem=1;
            if(reason=="remote")Main.myPlayer=1;
            OreFamily.OnMainHit(trigger,target,damage);check(Shots().Length==0,"Invalid trigger rejected: "+reason);
        }
        foreach(var reason in new[]{"reused","removed","material","target","friendly"})
        {
            (player,soul,target,blade)=Setup();OreFamily.OnMainHit(blade,target,100);shot=Shots()[1];mod=(OreFlyingSword)shot.ModProjectile;
            if(reason=="reused") {var fresh=new NPC();fresh.SetDefaults(NPCID.Zombie);fresh.active=true;fresh.whoAmI=20;Main.npc[20]=fresh;}
            if(reason=="removed")soul.Hosts[0].TurnToAir();
            if(reason=="material")soul.Hosts[1].TurnToAir();
            if(reason=="target")target.active=false;
            if(reason=="friendly")target.friendly=true;
            mod.AI();check(!shot.active,"Child ends when target/source changes: "+reason);
        }
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
    }
}
