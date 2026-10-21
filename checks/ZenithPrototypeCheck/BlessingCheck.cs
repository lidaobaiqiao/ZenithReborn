using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class BlessingCheck
{
    private static (Player,SoulItem,BlessingPlayer,NPC) Setup()
    {
        foreach(var q in Main.projectile)q.active=false;foreach(var n in Main.npc)n.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var p=new Player{active=true,whoAmI=0,position=new(1000,1000)};Main.player[0]=p;p.ResetEffects();
        var s=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        s.Attached=true;s.Kills=SoulProgress.All;s.Cores=63;s.Seals=SoulCurses.All;s.LegacyReleased=0;
        s.Hosts[0]=new Item(ItemID.WoodenSword);s.Hosts[1]=new Item(ItemID.BorealWoodSword);
        s.Hosts[2]=new Item(ItemID.CobaltSword);s.Hosts[3]=new Item(ItemID.OrichalcumSword);
        p.inventory[0]=s.Item;s.RefreshStats();
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.active=true;target.whoAmI=20;
        target.Center=p.Center+new Vector2(150,0);target.life=target.lifeMax=10000000;target.defense=0;target.HideStrikeDamage=true;Main.npc[20]=target;
        return(p,s,p.GetModPlayer<BlessingPlayer>(),target);
    }
    private static SoulBlade Blade(Player p,SoulItem s,int slot=0)
    {
        var b=(SoulBlade)Main.projectile[s.EmitBlade(p,s.ItemAt(slot),slot,s.Active.Count())].ModProjectile;
        b.Projectile.damage=b.SpecialDamage=100;b.Projectile.CritChance=0;b.AI();return b;
    }
    private static void Hit(SoulBlade b,NPC target)
    {b.Projectile.Center=target.Center;Array.Clear(target.immune);Array.Clear(b.Projectile.localNPCImmunity);b.Projectile.Damage();}
    private static Projectile[] Strikes(int kind)=>Main.projectile.Where(q=>q.active && q.ModProjectile is BlessingStrike && (int)q.ai[0]==kind).ToArray();
    internal static void Run(Action<bool,string> check)
    {
        bool menu=Main.gameMenu;Main.gameMenu=false;
        try
        {
            var(p,s,state,n)=Setup();
            check(!state.TryActivate() && state.BurstLeft==0,"御剑 rejects an empty meter");
            for(int i=0;i<BlessingTuning.ChargeFrames;i++){state.RecordCombat(s);state.PostUpdate();}
            check(s.YujianCharge==900 && state.BurstLeft==0,"Fifteen seconds of valid combat fills without automatic activation");
            for(int i=0;i<1200;i++)state.PostUpdate();
            check(s.YujianCharge==900 && state.BurstLeft==0,"A full unspent meter survives twenty seconds out of combat");
            p.inventory[0]=new Item();state.PostUpdate();state.UpdateDead();p.inventory[0]=s.Item;
            check(s.YujianCharge==900,"Switching weapons and death do not erase unspent 御剑");
            var saved=new TagCompound();s.SaveData(saved);var loaded=(SoulItem)new Item(s.Type).ModItem;loaded.LoadData(saved);
            check(loaded.YujianCharge==900 && ((SoulItem)s.Item.Clone().ModItem).YujianCharge==900,"Charge persists in item save/load and clone");
            s.PreReforge();s.YujianCharge=0;s.PostReforge();check(s.YujianCharge==900,"Reforge preserves the unspent meter");
            using(var stream=new MemoryStream())
            {s.NetSend(new BinaryWriter(stream));stream.Position=0;loaded.NetReceive(new BinaryReader(stream));
                check(loaded.YujianCharge==900 && stream.Position==stream.Length,"Item network preserves charge with exact payload consumption");}
            SoulUI.Open(s);check(!state.TryActivate() && s.YujianCharge==900,"Soul editing cannot accidentally spend a full meter");SoulUI.Toggle(s);
            float normal=s.Reach(p);check(state.TryActivate() && state.BurstLeft==600 && s.YujianCharge==0,"Manual activation alone spends the meter and starts ten seconds");
            check(Math.Abs(s.Reach(p)-normal*2)<.01f && !state.TryActivate(),"Burst doubles controllable distance and cannot be restarted while active");
            n.Center=p.Center;check(state.DistanceMultiplier(s,n)==1,"Distance multiplier starts at one next to the player");
            n.Center=p.Center+new Vector2(normal*2,0);check(Math.Abs(state.DistanceMultiplier(s,n)-1.5f)<.0001,"Far end of expanded reach gains exactly fifty percent");
            n.Center=p.Center+new Vector2(9999,0);check(state.DistanceMultiplier(s,n)==1.5f,"Distance damage cannot exceed its fifty-percent cap");
            for(int i=0;i<30;i++)state.PostUpdate();check(state.ZoomBlend==1,"Camera smoothly reaches the doubled view instead of snapping");
            SoulUI.Open(s);int remaining=state.BurstLeft;state.PostUpdate();check(state.BurstLeft==remaining,"Soul editor pauses an already activated burst");SoulUI.Toggle(s);
            for(int i=0;i<600;i++)state.PostUpdate();check(state.BurstLeft==0 && state.ZoomBlend==0 && s.Reach(p)==normal,"Burst expires and restores normal camera and reach");
            (p,s,state,n)=Setup();s.Seals&=~SoulCurses.Moon;s.YujianCharge=900;
            check(!state.TryActivate(),"Released-only or migrated old souls do not receive an unearned blessing");

            (p,s,state,n)=Setup();n.SetDefaults(NPCID.TargetDummy);n.HideStrikeDamage=true;n.active=true;n.whoAmI=20;n.Center=p.Center+new Vector2(100,0);
            Hit(Blade(p,s),n);state.PostUpdate();check(s.YujianCharge==0,"A target dummy cannot charge 御剑");

            (p,s,state,n)=Setup();var first=Blade(p,s);Hit(first,n);
            // Respect the existing wood mark application cadence; relay never bypasses it.
            typeof(Main).GetField("_gameUpdateCount",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
                .SetValue(null,Main.GameUpdateCount+31u);
            for(int i=0;i<31;i++)state.PostUpdate();
            var second=Blade(p,s,1);Hit(second,n);
            check(Math.Abs(second.Blessings.TraitPower-1.1f)<.001f,"Different primary first hits build a real ten-percent relay");
            Hit(second,n);check(second.Blessings.TraitPower==1,"Frame damage receives no repeated first-hit relay bonus");
            var wood=n.GetGlobalNPC<WoodCombat>().Contributions[(0,s.SoulId)];
            check(wood.Power[1]>s.CombatWood.Strength,"Relay actually strengthens the applied wood mark, not just display text");
            for(int i=0;i<8;i++){var b=Blade(p,s,i%2);Hit(b,n);}
            var capped=Blade(p,s,1);capped.Blessings.Prepare(capped.Projectile,n);
            check(capped.Blessings.TraitPower<=1.5f,"Relay has a fifty-percent maximum");
            for(int i=0;i<181;i++)state.PostUpdate();capped.Blessings.Prepare(capped.Projectile,n);
            check(capped.Blessings.TraitPower==1,"Three seconds without a relay first hit clears its bonus");
            (p,s,state,n)=Setup();Hit(Blade(p,s),n);var ore=Blade(p,s,3);Hit(ore,n);
            check(Main.projectile.Where(q=>q.active && q.ModProjectile is HardOreShot && (int)q.ai[1]==(int)HardOreKind.Orichalcum).Sum(q=>q.damage)==33,
                "Relay boosts the actual Orichalcum petal budget from thirty to thirty-three without changing its cooldown");

            (p,s,state,n)=Setup();first=Blade(p,s);for(int i=0;i<200 && first.MotionPhase<60;i++)first.AI();
            second=Blade(p,s);check(second.Blessings.ReturnArmed,"A native complete return arms the same physical host next time");
            var different=Blade(p,s,1);check(!different.Blessings.ReturnArmed,"Return charge cannot leak into a different hosted sword");
            Hit(second,n);check(Strikes(0).Length==1 && Strikes(0)[0].damage==50,"Charged first hit emits a real fifty-percent source slash");
            Hit(second,n);check(Strikes(0).Length==1,"Following frame hits cannot repeatedly spend one return charge");
            var slash=Strikes(0)[0];slash.Center=n.Center;int before=n.life;Array.Clear(n.immune);slash.Damage();
            check(n.life<before,"Return slash causes actual native engine damage");
            int oreBefore=Main.projectile.Count(q=>q.active && q.ModProjectile is OreFlyingSword);Array.Clear(n.immune);slash.Damage();
            check(Main.projectile.Count(q=>q.active && q.ModProjectile is OreFlyingSword)==oreBefore,"Blessing slash does not recursively charge ore flight");
            (p,s,state,n)=Setup();first=Blade(p,s);first.Retire();for(int i=0;i<200 && first.Projectile.active;i++)first.AI();
            check(!Blade(p,s).Blessings.ReturnArmed,"An interrupted or retired blade does not grant return charge");

            (p,s,state,n)=Setup();first=Blade(p,s);int x=100,y=100;
            var tile=Main.tile[x,y];tile.ClearEverything();tile.HasTile=true;tile.TileType=TileID.Stone;
            try{first.Blessings.CrossedSolid(first.Projectile,new(x*16-4,y*16+8),new(x*16+20,y*16+8));}
            finally{tile.ClearEverything();}
            Hit(first,n);check(n.HasBuff(ModContent.BuffType<SoulBreach>()),"Crossing actual solid tiles arms a real four-second breach debuff");
            var mods=n.GetIncomingStrikeModifiers(DamageClass.Melee,1);new BlessingTarget().ModifyIncomingHit(n,ref mods);
            check(mods.ArmorPenetration.Value>=15,"Breach really supplies fifteen armor penetration");
            n.DelBuff(n.FindBuffIndex(ModContent.BuffType<SoulBreach>()));Hit(first,n);
            check(!n.HasBuff(ModContent.BuffType<SoulBreach>()),"Each emitted blade spends breach once and does not reapply on frame damage");

            (p,s,state,n)=Setup();first=Blade(p,s);Hit(first,n);Hit(first,n);check(Strikes(1).Length==0,"Two direct hits do not prematurely explode a sword mark");
            Hit(first,n);check(Strikes(1).Length==1 && Strikes(1)[0].damage==60,"Third same-sword hit emits its own sixty-percent mark explosion");
            Hit(first,n);Hit(first,n);check(Strikes(1).Length==1,"One blade cannot explode the same target twice during its flight");
            var explosion=Strikes(1)[0];explosion.Center=n.Center;before=n.life;Array.Clear(n.immune);explosion.Damage();
            check(n.life<before,"Sword-mark explosion causes actual engine damage");
            (p,s,state,n)=Setup();first=Blade(p,s);Hit(first,n);Hit(first,n);
            typeof(Main).GetField("_gameUpdateCount",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
                .SetValue(null,Main.GameUpdateCount+121u);
            Hit(first,n);check(Strikes(1).Length==0,"A gap longer than two seconds resets that blade's sword marks");
            var clone=(SoulItem)s.Item.Clone().ModItem;clone.Seals=0;clone.LegacyReleased=63;
            check(!BlessingTuning.Blessed(clone,SoulCurse.Switching),"Legacy release cannot grant new blessing bonuses");
            check(Enum.GetValues<SoulCurse>().All(c=>!SoulCurses.Description(s,c).Contains("尚未设计")),"Every blessed curse now explains an implemented extra mechanism");
        }
        finally{Main.gameMenu=menu;foreach(var q in Main.projectile)q.active=false;foreach(var n in Main.npc)n.active=false;}
    }
}
