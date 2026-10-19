using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class WoodCheck
{
    private static SoulItem Soul(bool fullProgress = true)
    {
        var soul = (SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached = true;
        if (fullProgress) { soul.Kills = SoulProgress.All; soul.Cores = 63; }
        return soul;
    }
    private static void FullWood(SoulItem soul)
    {
        for (int i = 0; i < 7; i++) soul.Hosts[i] = new Item(WoodTypes.Types[i]);
        for (int i = 0; i < 5; i++) soul.SupportHosts[i] = new Item(WoodTypes.Types[i+7]);
    }
    internal static void Run(Action<bool,string> check)
    {
        Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
        Player player = new() { whoAmI = 0, active = true };
        Main.player[0] = player; player.ResetEffects();
        var soul = Soul(); player.inventory[0] = soul.Item; player.selectedItem = 0;
        check(WoodTypes.Types.Distinct().Count() == 12, "Twelve unique wood swords, no seasonal Pine");
        var spooky = new Item(ModContent.ItemType<SpookyWoodSword>());
        check(spooky.damage == 90 && spooky.useTime == 18 && spooky.knockBack == 6, "Post-Plantera Spooky sword stats");
        foreach (int type in WoodTypes.Types) check(SoulItem.CanHost(new Item(type)) && WoodTypes.CanSupport(new Item(type)), "Each wood supports both slot types: " + type);
        foreach (var pair in new[] { (1,1,1.1f,5), (2,2,1.2f,10), (4,3,1.35f,15), (6,4,1.5f,20), (8,5,1.75f,30), (12,5,3f,100) })
        {
            int mask = (1 << pair.Item1) - 1;
            var load = new WoodLoadout(mask, pair.Item2);
            check(load.Count == pair.Item1 && load.SupportCount == pair.Item2 && Math.Abs(load.Strength-pair.Item3)<.001 && load.Life==pair.Item4, "Threshold values: " + pair.Item1);
        }
        var bootstrap = Soul();
        for (int i=0;i<5;i++) bootstrap.SupportHosts[i] = new Item(WoodTypes.Types[i]);
        for (int i=0;i<2;i++) bootstrap.ForestHosts[i] = new Item(WoodTypes.Types[i+5]);
        check(bootstrap.Wood.Count == 0 && bootstrap.SupportCapacity == 0 && bootstrap.BonusCapacity == 0, "Support and bonus slots cannot bootstrap themselves");
        bootstrap.Hosts[0] = new Item(WoodTypes.Types[7]);
        check(bootstrap.Wood.Count==3 && bootstrap.SupportCapacity==2, "Least fixed point ignores formerly open support slots");
        FullWood(soul);
        check(soul.Wood.Full && soul.SupportCapacity==5 && soul.BonusCapacity==2 && soul.Active.Count()==7, "Full wood gives five support plus two real bonus slots");
        Item cursor = new(ItemID.TerraBlade); cursor.Prefix(PrefixID.Legendary);
        check(soul.TrySwapHost(15,ref cursor,out _) && cursor.IsAir && soul.ForestHosts[0].prefix==PrefixID.Legendary, "Real bonus slot accepts and preserves actual weapon");
        cursor = new(ItemID.StarWrath);
        check(soul.TrySwapHost(16,ref cursor,out _) && soul.Active.Count()==9, "Both reward slots join main attacks");
        cursor = new(ItemID.TerraBlade);
        check(!soul.TrySwapHost(7,ref cursor,out _) && !cursor.IsAir, "Duplicate rejected across ordinary and bonus arrays");
        var clone=(SoulItem)soul.Item.Clone().ModItem;
        clone.SupportHosts[0].TurnToAir();clone.ForestHosts[0].TurnToAir();
        check(!soul.SupportHosts[0].IsAir && !soul.ForestHosts[0].IsAir, "Support and bonus arrays deep-cloned");
        var tag=new TagCompound();soul.SaveData(tag);
        var loaded=Soul(false);loaded.LoadData(tag);
        check(loaded.Stored.Select(i=>(i.type,i.prefix)).SequenceEqual(soul.Stored.Select(i=>(i.type,i.prefix))) && loaded.SoulId==soul.SoulId, "All 17 slots save and load intact");
        using(var bytes=new MemoryStream())
        {
            soul.NetSend(new BinaryWriter(bytes));bytes.Position=0;loaded.NetReceive(new BinaryReader(bytes));
            check(bytes.Position==bytes.Length && loaded.Stored.Select(i=>(i.type,i.prefix)).SequenceEqual(soul.Stored.Select(i=>(i.type,i.prefix))), "All 17 slots network round-trip");
        }
        var legacy=new TagCompound();soul.SaveData(legacy);legacy.Remove("supports");legacy.Remove("forestHosts");legacy.Remove("awakenedMemory");
        loaded.LoadData(legacy);
        check(loaded.SupportHosts.All(i=>i.IsAir) && loaded.ForestHosts.All(i=>i.IsAir) && loaded.Hosts[0].type==soul.Hosts[0].type, "v0.2.4 migration preserves original slots, initializes new arrays empty");
        soul.PreReforge();soul.Item.ResetPrefix();soul.PostReforge();
        check(soul.Wood.Full && soul.ForestHosts[0].prefix==PrefixID.Legendary && soul.SupportHosts[3].type==spooky.type, "Goblin reforge preserves new slots and prefixes");
        var compat=typeof(SoulItem).Assembly.GetType("ZenithReborn.HeroPrefixCompat");
        compat.GetMethod("Remember",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,[soul]);
        var fresh=Soul(false);fresh.Attached=false;
        fresh.ApplyPrefix(PrefixID.Legendary);
        check(fresh.Wood.Full && fresh.ForestHosts[0].prefix==PrefixID.Legendary && fresh.SoulId==soul.SoulId,"HERO prefix reconstruction restores all new arrays");
        compat.GetMethod("Clear",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
        var awakened=Soul();
        for(int i=0;i<10;i++) awakened.Hosts[i]=new Item(SoulProgress.ZenithWeapons[i]);
        awakened.RefreshStats();awakened.Hosts[0].TurnToAir();
        check(awakened.Awakened && awakened.AwakeningMemory,"Awakening remains permanent after changing hosts");
        var woodPlayer=player.GetModPlayer<WoodPlayer>();
        woodPlayer.RecordHit(soul);
        for(int frame=0;frame<3600;frame++) { woodPlayer.RecordHit(soul);woodPlayer.AdvanceGrowth(); }
        check(woodPlayer.Layers==12 && woodPlayer.LayersFor(soul)==12,"60 seconds of valid combat reaches twelve layers");
        for(int frame=0;frame<600;frame++) { woodPlayer.RecordHit(soul);woodPlayer.AdvanceGrowth(); }
        check(woodPlayer.Layers==12,"Growth cannot exceed twelve layers");
        player.statLifeMax2=100;player.statDefense=Player.DefenseStat.Default;player.endurance=0;woodPlayer.PostUpdateEquips();
        check(player.statLifeMax2==200 && player.statDefense==36 && Math.Abs(player.endurance-.12f)<.001,"Full forest life, defense and DR use actual player stats");
        woodPlayer.GrantHitBuffs(soul);
        check(player.HasBuff(ModContent.BuffType<ForestBreath>()) && player.HasBuff(ModContent.BuffType<DynastyOrder>()) && player.HasBuff(ModContent.BuffType<BloodFragrance>()),"Primary hit enables all three wood player buffs");
        player.lifeRegen=0;for(int i=0;i<4;i++) woodPlayer.UpdateLifeRegen();
        check(player.lifeRegen==54,"Fractional regen totals 6.75 HP/sec without per-frame minimum");
        player.AddBuff(BuffID.Slow,600);
        check(player.buffTime[player.FindBuffIndex(BuffID.Slow)]==528,"Dynasty hook shortens newly applied debuff by twelve percent");
        player.AddBuff(BuffID.PotionSickness,600);
        check(player.buffTime[player.FindBuffIndex(BuffID.PotionSickness)]==600,"Potion sickness excluded from debuff shortening");
        check(woodPlayer.BoostSoulHealing(100,105)==105 && woodPlayer.BoostSoulHealing(100,200)==112,"Blood fragrance boosts soul healing within original budget");
        player.statLife=50;float healBudget=105;Main.myPlayer=255;
        check(woodPlayer.HealFromSoul(soul,100,ref healBudget)==105 && player.statLife==155 && healBudget==0,"Blood fragrance direct-heal entry spends original budget and actually heals");
        Main.myPlayer=0;
        int beforeShots=Main.projectile.Count(p=>p.active && p.ModProjectile is SoulBlade);
        soul.Shoot(player,new EntitySource_ItemUse_WithAmmo(player,soul.Item,0),player.Center,Vector2.UnitX,ModContent.ProjectileType<SoulBlade>(),1,1);
        var shots=Main.projectile.Where(p=>p.active && p.ModProjectile is SoulBlade).ToArray();
        check(shots.Length-beforeShots==9 && shots.Any(p=>(int)p.ai[0]==ItemID.TerraBlade) && shots.Any(p=>(int)p.ai[0]==ItemID.StarWrath),"Actual Shoot spawns both real-slot blades, support slots spawn none");
        var terra=shots.First(p=>(int)p.ai[0]==ItemID.TerraBlade);
        int expectedBase=Math.Max(1,(int)(player.GetWeaponDamage(soul.ForestHosts[0])*soul.Potency/3));
        check(((SoulBlade)terra.ModProjectile).SpecialDamage==expectedBase && terra.damage==(int)(expectedBase*1.36f),"Main-only growth and nine-sword sharing use real source damage");
        foreach(var shot in shots) shot.Kill();
        NPC npc=new();npc.SetDefaults(NPCID.Zombie);npc.active=true;npc.whoAmI=20;Main.npc[20]=npc;
        WoodCombat.Apply(npc,player,soul,100);
        var combat=npc.GetGlobalNPC<WoodCombat>();
        var contribution=combat.Contributions[(0,soul.SoulId)];
        check(contribution.Time[3]==330 && contribution.Time[4]==390 && contribution.Time[7]==330,"Fey extension applied once from base durations");
        check(contribution.BleedLayers==1 && Math.Abs(contribution.Poison-9)<.001 && Math.Abs(contribution.Fire-10.5f)<.001,"Wood DoT derives from main P and strength, not support weapon stats");
        WoodCombat.Apply(npc,player,soul,100);
        check(contribution.BleedLayers==1,"Multiple primary blades share per-target application cooldown");
        for(int hit=0;hit<4;hit++) { contribution.Next[4]=0;WoodCombat.Apply(npc,player,soul,100); }
        check(contribution.BleedLayers==3 && Math.Abs(contribution.Rate-73.5f)<.01,"Bleed capped at three; source DoT enhancements add to 96 percent");
        int damage=0;npc.lifeRegen=0;combat.UpdateLifeRegen(npc,ref damage);
        check(npc.lifeRegen==-147,"Only owned DoT contribution converted to lifeRegen, correct half-rate units");
        NPC transferTarget=new();transferTarget.SetDefaults(NPCID.Zombie);transferTarget.active=true;
        var receiver=transferTarget.GetGlobalNPC<WoodCombat>().Entry(0,soul.SoulId);
        var transferMethod=typeof(WoodCombat).GetMethod("AddTransfer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(var channel in new[]{(3,contribution.Poison,BuffID.Poisoned),(4,contribution.Bleed.Take(3).Sum(),0),(7,contribution.Fire,BuffID.OnFire)})
            transferMethod.Invoke(null,[transferTarget,receiver,contribution,channel.Item1,channel.Item2,channel.Item3,2]);
        double transferred=receiver.Transfers.Sum(t=>(double)t.Rate*t.Frames/60);
        check(Math.Abs(transferred-contribution.RemainingBudget()*.75/2)<.01,"Mourning transfer conserves remaining budget at seventy-five percent split across two targets");
        check(receiver.RemainingBudget()==0 && receiver.Time[10]==0,"Transferred contribution cannot propagate again or create a new Mourning mark");
        using(var bytes=new MemoryStream())
        {
            combat.SendExtraAI(npc,null,new BinaryWriter(bytes));bytes.Position=0;
            var remote=new NPC();remote.SetDefaults(NPCID.Zombie);
            var remoteCombat=remote.GetGlobalNPC<WoodCombat>();remoteCombat.ReceiveExtraAI(remote,null,new BinaryReader(bytes));
            check(bytes.Position==bytes.Length && Math.Abs(remoteCombat.Contributions[(0,soul.SoulId)].Rate-contribution.Rate)<.001,"NPC wood state, layers, snapshots and remaining durations network round-trip");
        }
        var proj=new Projectile();proj.SetDefaults(ModContent.ProjectileType<SoulBlade>());proj.owner=0;proj.damage=100;proj.GetGlobalProjectile<SoulSource>().SoulId=soul.SoulId;
        var modifiers=npc.GetIncomingStrikeModifiers(DamageClass.Melee,1);WoodCombat.ModifyPrimaryHit(proj,npc,ref modifiers);
        check(Math.Abs(modifiers.ArmorPenetration.Value-6)<.001 && Math.Abs(modifiers.FinalDamage.ApplyTo(100)-107.5f)<.01,"Palm armor penetration and combined Boreal/Pearl vulnerability");
        NPC immune=new();immune.SetDefaults(NPCID.Zombie);immune.active=true;immune.buffImmune[BuffID.Poisoned]=true;immune.buffImmune[BuffID.OnFire]=true;
        WoodCombat.Apply(immune,player,soul,100);
        var immuneState=immune.GetGlobalNPC<WoodCombat>().Contributions[(0,soul.SoulId)];
        check(immuneState.Time[3]==0 && immuneState.Time[7]==0 && immuneState.Time[4]>0,"Poison/fire immunities respected independently of custom bleed");
        NPC dummy=new();dummy.SetDefaults(NPCID.TargetDummy);WoodCombat.Apply(dummy,player,soul,100);
        check(dummy.GetGlobalNPC<WoodCombat>().Contributions.Count==0,"Target dummy excluded from combat proc farm");
        var frozen=new NPC();frozen.SetDefaults(NPCID.Zombie);frozen.active=true;frozen.velocity=new Vector2(10,0);
        var frozenState=frozen.GetGlobalNPC<WoodCombat>().Entry(0,soul.SoulId);frozenState.Time[1]=240;frozenState.Power[1]=3;
        Vector2 before=frozen.position;frozen.GetGlobalNPC<WoodCombat>().PostAI(frozen);
        check(Math.Abs(frozen.position.X-before.X+1.2f)<.001 && frozen.velocity.X==10,"Boreal slows movement without exponential velocity collapse");
        frozen.boss=true;before=frozen.position;frozen.GetGlobalNPC<WoodCombat>().PostAI(frozen);
        check(frozen.position==before,"Boss is not forcibly slowed");
        var tiny=new NPC();tiny.SetDefaults(NPCID.Zombie);tiny.active=true;
        var tinyCombat=tiny.GetGlobalNPC<WoodCombat>();var tinyState=tinyCombat.Entry(0,soul.SoulId);tinyState.Poison=.1f;tinyState.Time[3]=600;
        int totalUnits=0;for(int i=0;i<300;i++){tiny.lifeRegen=0;tinyCombat.UpdateLifeRegen(tiny,ref damage);totalUnits-=tiny.lifeRegen;}
        check(Math.Abs(totalUnits-60)<=1,"Sub-point DoT accumulates, neither rounds to zero nor minimum one damage per frame");
        Item removed=new();check(soul.TrySwapHost(0,ref removed,out _) && !soul.Wood.Full && soul.BonusCapacity==0,"Removing any prerequisite disables forest rewards");
        check(soul.Active.Count()==6 && !soul.ForestHosts[0].IsAir,"Paused real weapons preserved and excluded from active attacks");
        woodPlayer.AdvanceGrowth();check(woodPlayer.Layers==0,"Loss of full set immediately resets forest growth");
        cursor=new();check(soul.TrySwapHost(15,ref cursor,out _) && cursor.type==ItemID.TerraBlade && cursor.prefix==PrefixID.Legendary,"Paused bonus item can be retrieved intact");
        cursor=new(ItemID.TerraBlade);check(!soul.TrySwapHost(15,ref cursor,out _) && !cursor.IsAir,"Paused bonus slot rejects insertion without consuming cursor");
        soul.Hosts[0]=removed;woodPlayer.RecordHit(soul);
        for(int i=0;i<600;i++){woodPlayer.RecordHit(soul);woodPlayer.AdvanceGrowth();}
        for(int i=0;i<419;i++) woodPlayer.AdvanceGrowth();
        check(woodPlayer.Layers==1,"After grace expires, combat layers decay one per two seconds");
        soul.Hosts[0].TurnToAir();woodPlayer.PreUpdateBuffs();
        check(!player.HasBuff(ModContent.BuffType<ForestPower>()) && !player.HasBuff(ModContent.BuffType<ForestBreath>()),"Missing source revokes forest and personal wood buff");
        // Each slot hitbox stays inside the panel and no two slots overlap.
        Main.screenWidth=1600;Main.screenHeight=900;var panel=SoulUI.PanelBounds();
        var slots=Enumerable.Range(0,17).Select(SoulUI.SlotBounds).ToArray();
        check(slots.All(r=>panel.Contains(r)) && !slots.Where((r,i)=>slots.Skip(i+1).Any(r.Intersects)).Any(),"All seventeen UI slots fit without overlap");
        player.inventory[0]=new Item();woodPlayer.AdvanceGrowth();check(woodPlayer.Layers==0,"Switching away resets growth");
    }
}
