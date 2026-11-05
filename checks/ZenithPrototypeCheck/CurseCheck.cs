using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Existing suites exercise normal/unbound mechanics. This ONLY belongs to the
// isolated checker addon; the new curse suite turns it off and checks real defaults.
public class CurseFixture : GlobalItem
{
    internal static bool NeutralDefaults;
    public override void SetDefaults(Item item)
    {if(NeutralDefaults && item.ModItem is SoulItem soul)soul.LegacyReleased=63;}
}

internal static class CurseCheck
{
    private static (Player,SoulItem) Setup(int hosts=4)
    {
        foreach(var p in Main.projectile)p.active=false;foreach(var n in Main.npc)n.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var p1=new Player{whoAmI=0,active=true,position=new(1000,1000)};Main.player[0]=p1;p1.ResetEffects();
        var s=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;s.Attached=true;s.Kills=SoulProgress.All;s.Cores=63;
        for(int i=0;i<hosts;i++)s.Hosts[i]=new Item(SoulProgress.ZenithWeapons[i]);
        p1.inventory[0]=s.Item;s.RefreshStats();return(p1,s);
    }
    private static SoulBlade Blade(Player p,SoulItem s,int type)
    {
        Main.mouseX=(int)p.Center.X+200;Main.mouseY=(int)p.Center.Y;Main.screenPosition=Vector2.Zero;
        return(SoulBlade)Main.projectile[s.EmitBlade(p,new Item(type),0,1)].ModProjectile;
    }
    private static NPC Target(Player p,int index=20)
    {
        var n=new NPC();n.SetDefaults(NPCID.Zombie);n.active=true;n.whoAmI=index;n.Center=p.Center+new Vector2(120,0);
        n.life=n.lifeMax=1000000;n.defense=0;n.HideStrikeDamage=true;Main.npc[index]=n;return n;
    }
    private static void Hit(SoulBlade blade,NPC n)
    {
        blade.Projectile.Center=n.Center;Array.Clear(blade.Projectile.localNPCImmunity);Array.Clear(n.immune);blade.Projectile.Damage();
    }
    internal static void Run(Action<bool,string> check)
    {
        CurseFixture.NeutralDefaults=false;
        var(p,s)=Setup();
        check(s.Seals==0 && s.LegacyReleased==0,"New souls get six actual curses without world flags or compatibility exemptions");
        foreach(SoulCurse curse in Enum.GetValues<SoulCurse>())
        {
            int[] group=SoulCurses.Groups[(int)curse];
            foreach(var order in group.Length==2?new[]{group,group.Reverse().ToArray()}:new[]{group,group.Reverse().ToArray(),new[]{group[1],group[2],group[0]}})
            {
                s.Seals=0;
                check(s.Curse(curse)==CurseStage.Cursed,"Zero records means cursed");
                for(int i=0;i<order.Length;i++)
                {
                    s.Seals|=order[i];var expected=i==order.Length-1?CurseStage.Blessed:i==order.Length-2?CurseStage.Released:CurseStage.Weakened;
                    check(s.Curse(curse)==expected,"Distinct boss counts implement weakened/released/blessed stages in any order");
                    s.Seals|=order[i];check(s.Curse(curse)==expected,"Repeated boss cannot advance a stage");
                }
            }
        }
        s.Seals=SoulCurses.Retinazer;
        check(SoulCurses.Count(s,SoulCurse.Solitude)==0,"One twin cannot count as a mechanical king");
        s.Seals|=SoulCurses.Spazmatism;check(s.Curse(SoulCurse.Solitude)==CurseStage.Weakened,"Both twins count exactly one king");
        s.Seals=SoulCurses.Duke|SoulCurses.Deer;
        check(Enum.GetValues<SoulCurse>().All(c=>s.Curse(c)==CurseStage.Cursed),"Outer bosses only record separate victory and never break a curse");
        s.Seals=SoulCurses.Eye;
        check(s.Curse(SoulCurse.Binding)==CurseStage.Released,"Killing the eye alone releases binding");
        float freeReach=s.Reach(p);s.Seals=0;float bound=s.Reach(p);
        float midpoint=SoulCurses.ScreenEdge(p)*.5f;
        check(bound>=midpoint-.01f && bound<=freeReach+.01f,
            "Binding guarantees at least the screen midpoint without exceeding the released reach");
        check(s.Curse(SoulCurse.Binding)==CurseStage.Cursed,"Empty seals are still cursed");
        s.Seals=0;check(s.TraitItems.Count()==s.Active.Count() && s.Active.All(i=>s.TraitEnabled(i.type)),"Every effective primary keeps its trait after the switching curse stops filtering sources");
        int damage=s.BladeDamage(p,s.Hosts[1]);s.Seals=SoulCurses.Brain;
        check(s.TraitItems.Count()==s.Active.Count() && s.BladeDamage(p,s.Hosts[1])==damage,"Brain no longer changes the trait roster or independent damage once traits are unconditional");
        s.Seals=0;var allowed=Blade(p,s,ItemID.CopperShortsword);allowed.TargetOffset=new(200,0);allowed.Vertical=10;allowed.AI();
        s.Seals=SoulCurses.Eater;
        p.itemAnimation=p.itemAnimationMax=30;s.UseAnimation(p);
        check(!allowed.Retired,"First return-group boss allows old waves to continue before Skeletron blessing");
        s.Seals=0;s.UseAnimation(p);check(allowed.Retired && allowed.CanDamage()==false,"Cursed new gesture retires old primary without waiting or deleting host");
        for(int k=0;k<10;k++)allowed.AI();check(allowed.BladeOpacity==0,"Retired primary visually fades without absorption");
        foreach(int count in new[]{0,1,2,3})
        {
            (p,s)=Setup(10);int[] kings=SoulCurses.Groups[(int)SoulCurse.Solitude];foreach(int k in kings.Take(count))s.Seals|=k;
            check(SoulCurses.Blades(s)==new[]{2,3,4,4}[count],"Solitude blade budget follows 2/3/normal4/blessed4 progression");
            var swing=p.GetModPlayer<SoulSwingPlayer>();p.itemAnimation=p.itemAnimationMax=30;s.UseAnimation(p);
            for(int frame=0;frame<30;frame++)
            {p.itemAnimation=30-frame;if(frame%10==0)swing.FireHost();swing.PostItemCheck();}
            check(Main.projectile.Count(x=>x.active && x.ModProjectile is SoulBlade)==SoulCurses.Blades(s),"Actual gesture emits its curse or blessing budget");
        }
        foreach(var milestone in new[]{(SoulCurses.Flower,6),(SoulCurses.Temple,7),(SoulCurses.Cultist,8),(SoulCurses.Moon,10)})
        {
            (p,s)=Setup(10);s.Seals=SoulCurses.Destroyer|SoulCurses.Retinazer|SoulCurses.Spazmatism|SoulCurses.Prime|milestone.Item1;
            var swing=p.GetModPlayer<SoulSwingPlayer>();p.itemAnimation=p.itemAnimationMax=30;s.UseAnimation(p);
            for(int frame=0;frame<30;frame++){p.itemAnimation=30-frame;if(frame%10==0)swing.FireHost();swing.PostItemCheck();}
            check(Main.projectile.Count(x=>x.active && x.ModProjectile is SoulBlade)==milestone.Item2,"Blessed gesture spaces more than three independent swords up to ten");
        }
        foreach(int records in new[]{0,SoulCurses.Flesh,SoulCurses.Flesh|SoulCurses.Queen})
        {
            (p,s)=Setup();s.Seals=records;s.LegacyReleased=(1<<(int)SoulCurse.Barrier)|(1<<(int)SoulCurse.Sheathing);
            var blade=Blade(p,s,ItemID.CopperShortsword);blade.AI();var n=Target(p);int hits=0;
            for(int attempt=0;attempt<6;attempt++){int hp=n.life;Hit(blade,n);if(n.life<hp)hits++;}
            check(hits==(records==0?3:records==SoulCurses.Flesh?5:6),"Real damage path enforces 3/5/released repeated hits per primary and target");
            var n2=Target(p,21);int before=n2.life;Hit(blade,n2);check(n2.life<before,"Same blade can still hit a different enemy");
            var separate=Blade(p,s,ItemID.CopperShortsword);separate.AI();before=n.life;Hit(separate,n);
            check(n.life<before,"Each newly spawned primary owns an independent target-hit dictionary");
        }
        (p,s)=Setup();s.Hosts[1]=new Item(ItemID.IceBlade);
        var ice=Blade(p,s,ItemID.IceBlade);ice.AI();var frozen=Target(p);Hit(ice,frozen);
        check(frozen.HasBuff(BuffID.Frostburn),"Every primary keeps its trait after the switching curse stops filtering sources");
        s.Seals=SoulCurses.Brain;ice=Blade(p,s,ItemID.IceBlade);ice.AI();Hit(ice,frozen);
        check(frozen.HasBuff(BuffID.Frostburn),"Boss progress no longer changes which sword traits fire");
        AttackSpeed(check);NativeBlessing(check);Terrain(check);Persistence(check);Credit(check);
        foreach(var proj in Main.projectile)proj.active=false;foreach(var n in Main.npc)n.active=false;
    }
    // 攻速成长曲线：Flow=1 且三组机械未记录时出手变慢，三组齐后转为加速，Flow=0 恒为原速。
    private static void AttackSpeed(Action<bool,string> check)
    {
        var(p,s)=Setup();
        // 森林层数显式归零（ForestPower.Reset 是 internal，这里按同口径反射清零），避免前序用例残留层数污染 SpeedBonus。
        var wood=p.GetModPlayer<WoodPlayer>();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        foreach(var name in new[]{"Layers","CombatFrames","RecentCombat","Decay"})typeof(WoodPlayer).GetProperty(name,flags).SetMethod.Invoke(wood,[0]);
        typeof(WoodPlayer).GetProperty("CurrentSoul",flags).SetMethod.Invoke(wood,[""]);
        var flowField=typeof(SoulPlayer).GetField("Flow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var soulPlayer=p.GetModPlayer<SoulPlayer>();
        flowField.SetValue(soulPlayer,1f);s.Kills=0;
        check(Math.Abs(s.UseSpeedMultiplier(p)-.65f)<.001f,"Full flow with no mechanical group slows the swing to 65%");
        s.Kills=SoulProgress.Destroyer|SoulProgress.Retinazer|SoulProgress.Spazmatism|SoulProgress.Prime;
        check(Math.Abs(s.UseSpeedMultiplier(p)-1.2f)<.001f,"All three mechanical groups turn full flow into a 20% blessing");
        flowField.SetValue(soulPlayer,0f);
        check(Math.Abs(s.UseSpeedMultiplier(p)-1f)<.001f,"Zero flow stays exactly neutral at zero forest layers");
    }
    private static void NativeBlessing(Action<bool,string> check)
    {
        foreach(int count in new[]{1,4,10})foreach(float speed in new[]{1f,1.5f,100f})
        {
            var(p,s)=Setup(count);s.Seals=SoulCurses.All;
            p.GetAttackSpeed(DamageClass.Melee)=speed;p.controlUseItem=p.releaseUseItem=true;
            Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
            Main.mouseX=1240;Main.mouseY=1040;Main.screenPosition=Vector2.Zero;
            SwingCheck.Trace=[];SwingCheck.Recording=SwingCheck.NormalizeTraits=true;
            try{for(SwingCheck.Tick=0;SwingCheck.Tick<90;SwingCheck.Tick++)p.ItemCheck();}
            finally{SwingCheck.Recording=SwingCheck.NormalizeTraits=false;}
            var trace=SwingCheck.Trace;
            check(trace.Count>0 && trace.GroupBy(x=>x.tick).All(g=>g.Count()==1),"Blessed real ItemCheck never emits an instantaneous batch even at extreme speed");
            check(trace.Select(x=>x.host).SequenceEqual(Enumerable.Range(0,trace.Count).Select(i=>SoulProgress.ZenithWeapons[i%count])),"Blessed real firing uses unique hosts and continues the persistent cursor");
            if(speed==1)
                check(trace.Count(x=>x.tick<30)==count && trace.Take(count).All(x=>x.duration==30),"Blessed real native gesture keeps thirty frames and emits its sparse host count up to ten");
        }
    }
    private static void Terrain(Action<bool,string> check)
    {
        Main.tileSolid[TileID.Stone]=true;Main.tileSolidTop[TileID.Platforms]=true;
        for(int x=70;x<75;x++){var t=Main.tile[x,70];t.ClearEverything();t.HasTile=true;t.TileType=TileID.Stone;}
        float depth=0;
        var(p0,s0)=Setup();
        check(SoulCurses.TerrainAllowance(s0)==32,"Cursed barrier digs through about two tiles");
        s0.Seals=SoulCurses.Bee;check(SoulCurses.TerrainAllowance(s0)==64,"Weakened barrier widens to about four tiles");
        check(SoulTerrain.Sweep(new(1100,1128),new(1210,1128),0,ref depth),"Swept terrain path cannot tunnel through a solid wall");
        depth=0;check(SoulTerrain.Sweep(new(1100,1128),new(1210,1128),32,ref depth),"Cursed barrier still blocks a wall thicker than its allowance");
        depth=0;check(SoulTerrain.Sweep(new(1100,1128),new(1210,1128),64,ref depth),"Weakened barrier still blocks a wall thicker than 64 pixels");
        depth=0;check(!SoulTerrain.Sweep(new(1100,1128),new(1210,1128),int.MaxValue,ref depth),"Released barrier restores full native terrain passage");
        for(int x=70;x<75;x++)Main.tile[x,70].TileType=TileID.Platforms;
        depth=0;check(!SoulTerrain.Sweep(new(1100,1128),new(1210,1128),0,ref depth),"Platforms do not silently block sword flight");
        for(int x=70;x<75;x++)Main.tile[x,70].ClearEverything();
        foreach(bool released in new[]{false,true})
        {
            var(p,s)=Setup();if(released)s.Seals=SoulCurses.Bee|SoulCurses.Flower;
            var blade=Blade(p,s,ItemID.CopperShortsword);blade.TargetOffset=new(200,0);blade.Vertical=10;
            // Arm in open air, then cover the real curved path in solid tiles.
            for(int k=0;k<4;k++)blade.AI();
            for(int x=55;x<90;x++)for(int y=55;y<80;y++)
            {var t=Main.tile[x,y];t.HasTile=true;t.TileType=TileID.Stone;}
            // 阻隔诅咒允许先啃掉约2格厚度；连续推进直到累积穿透超过上限。
            for(int k=0;k<8 && !blade.Retired;k++)blade.AI();
            check(blade.Retired!=released && (released || blade.CanDamage()==false),"Real native curve is stopped once terrain penetration exceeds the cursed allowance");
            for(int x=55;x<90;x++)for(int y=55;y<80;y++)Main.tile[x,y].ClearEverything();
        }
    }
    private static void Persistence(Action<bool,string> check)
    {
        var(p,s)=Setup();s.Seals=SoulCurses.Brain|SoulCurses.Deer;s.LegacyReleased=4;
        var tag=new TagCompound();s.SaveData(tag);var copy=(SoulItem)new Item(s.Type).ModItem;copy.LoadData(tag);
        check(copy.Seals==s.Seals && copy.LegacyReleased==4 && copy.SoulId==s.SoulId,"Seals and explicit legacy exemptions persist without forging kills");
        using(var stream=new MemoryStream())
        {s.NetSend(new BinaryWriter(stream));stream.Position=0;copy.NetReceive(new BinaryReader(stream));check(stream.Position==stream.Length && copy.Seals==s.Seals && copy.LegacyReleased==4,"Full soul network packet retains curse records and reaches exact end");}
        var cloned=(SoulItem)s.Item.Clone().ModItem;cloned.Seals|=SoulCurses.Eye;check(s.Seals!=cloned.Seals,"Clone progress is independent");
        tag.Remove("curseSchema");tag.Remove("seals");tag.Remove("legacyReleased");tag["kills"]=SoulProgress.Evil;
        copy.LoadData(tag);
        check(copy.LegacyReleased==63 && copy.Seals==0 && copy.Curse(SoulCurse.Switching)==CurseStage.Released &&
            SoulCurses.RecordedStage(copy,SoulCurse.Switching)==CurseStage.Cursed,"Used legacy soul remains playable without assigning ambiguous Evil bit to Brain or Eater or granting blessing");
        var fresh=(SoulItem)new Item(s.Type).ModItem;var empty=new TagCompound();fresh.SaveData(empty);empty.Remove("curseSchema");
        fresh.LoadData(empty);check(fresh.LegacyReleased==0,"Empty unused old remnant still starts with the real new curses");
        ItemLoader.PreReforge(s.Item);s.Item.ResetPrefix();s.Item.Prefix(PrefixID.Legendary);ItemLoader.PostReforge(s.Item);
        var reforged=(SoulItem)s.Item.ModItem;check(reforged.Seals==s.Seals && reforged.LegacyReleased==4,"Goblin reforge keeps seal and compatibility state");
        var compat=typeof(SoulItem).Assembly.GetType("ZenithReborn.HeroPrefixCompat");
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        compat.GetMethod("Remember",flags).Invoke(null,[reforged]);
        var rebuilt=new Item(s.Type);rebuilt.Prefix(PrefixID.Legendary);
        check(((SoulItem)rebuilt.ModItem).Seals==reforged.Seals && ((SoulItem)rebuilt.ModItem).LegacyReleased==4,"Actual prefix reconstruction restores curse progress through HERO compatibility hook");
        compat.GetMethod("Clear",flags).Invoke(null,null);
    }
    private static void Credit(Action<bool,string> check)
    {
        var(p,s)=Setup();
        void Defeat(int type)
        {
            var n=new NPC();n.SetDefaults(type);n.whoAmI=90;n.active=true;Main.npc[90]=n;
            SoulBossCredit.Register(n,0,s.SoulId);n.active=false;new SoulBossCredit().OnKill(n);
        }
        Defeat(NPCID.BrainofCthulhu);
        check((s.Seals&SoulCurses.Brain)!=0 && (s.Seals&SoulCurses.Eater)==0 && s.Curse(SoulCurse.Switching)==CurseStage.Released && s.Curse(SoulCurse.Sheathing)==CurseStage.Cursed,
            "Actual encounter kill distinguishes Brain from Eater despite shared old slot bit");
        Defeat(NPCID.QueenBee);check(s.Curse(SoulCurse.Barrier)==CurseStage.Weakened,"Optional Boss participates in its real seal group");
        Defeat(NPCID.Plantera);check(s.Curse(SoulCurse.Barrier)==CurseStage.Released,"Second jungle boss fully removes barrier before Golem");
        Defeat(NPCID.Golem);check(s.Curse(SoulCurse.Barrier)==CurseStage.Blessed,"Third jungle boss records the distinct blessing stage");
        Defeat(NPCID.Deerclops);check((s.Seals&SoulCurses.Deer)!=0,"Outer victory is recorded independently");
        var worm=new NPC();worm.SetDefaults(NPCID.EaterofWorldsBody);worm.whoAmI=91;worm.active=true;Main.npc[91]=worm;
        Defeat(NPCID.EaterofWorldsHead);
        check((s.Seals&SoulCurses.Eater)==0 && !new CorruptionLure().CanUseItem(p),"Remaining worm segment prevents both premature credit and duplicate cross summon");
        worm.active=false;new SoulBossCredit().OnKill(worm);
        check((s.Seals&SoulCurses.Eater)!=0 && new CorruptionLure().CanUseItem(p),"Last worm segment grants the pending personal seal and releases cross summon");
    }
}
