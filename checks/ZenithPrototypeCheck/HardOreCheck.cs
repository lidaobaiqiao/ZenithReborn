using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class HardOreCheck
{
    private delegate int TextOriginal(Rectangle area,Color color,string text,bool dramatic,bool dot);
    private static int HeadlessText(TextOriginal original,Rectangle area,Color color,string text,bool dramatic,bool dot) =>
        Main.dedServ ? 100 : original(area,color,text,dramatic,dot);
    private static int flight;
    private static (Player Player, SoulItem Soul, NPC Target) Setup(params int[] weapons)
    {
        foreach(var p in Main.projectile) p.active=false;
        foreach(var n in Main.npc) n.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var player=new Player{active=true,whoAmI=0,position=new Vector2(1000,1000)};Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
        for(int i=0;i<weapons.Length;i++) soul.Hosts[i]=new Item(weapons[i]);
        soul.RefreshStats();player.inventory[0]=soul.Item;player.selectedItem=0;
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
        target.life=target.lifeMax=10000000;target.defense=0;target.knockBackResist=0;target.HideStrikeDamage=true;
        target.width=80;target.height=120;target.Center=player.Center+new Vector2(120,0);Main.npc[20]=target;
        return(player,soul,target);
    }
    private static Projectile Blade(Player p,SoulItem s,int type,int damage=100)
    {
        var host=s.Active.FirstOrDefault(i=>i.type==type) ?? throw new Exception($"Fixture missing host {type}; active={string.Join(',',s.Active.Select(i=>i.type))}");
        int index=s.EmitBlade(p,host,0,s.Active.Count());var blade=Main.projectile[index];
        blade.identity=++flight;blade.damage=damage;blade.CritChance=0;
        ((SoulBlade)blade.ModProjectile).SpecialDamage=damage;
        blade.ModProjectile.AI();return blade;
    }
    private static void Advance(Player player,int frames)
    { for(int i=0;i<frames;i++){player.GetModPlayer<HardOrePlayer>().PostUpdate();player.GetModPlayer<SoulPlayer>().PostUpdate();} }
    private static void Hit(Projectile blade,NPC target)
    {
        blade.Center=target.Center;blade.localNPCImmunity[target.whoAmI]=0;target.immune[blade.owner]=0;
        blade.Damage();
    }
    private static void Hits(Player player,Projectile blade,NPC target,int count)
    { for(int i=0;i<count;i++){if(i>0)Advance(player,30);Hit(blade,target);} }
    private static Projectile[] Shots(HardOreKind kind)=>Main.projectile.Where(p=>p.active && p.ModProjectile is HardOreShot && (int)p.ai[1]==(int)kind).ToArray();
    private static float Modifier(Player player,Projectile blade,NPC target)
    {
        var mods=target.GetIncomingStrikeModifiers(DamageClass.Melee,1);
        player.GetModPlayer<HardOrePlayer>().Modify(blade,target,ref mods);return mods.SourceDamage.ApplyTo(100);
    }
    internal static void Run(Action<bool,string> check)
    {
        // No font/GPU resources exist in this dedicated-server fixture. Only
        // floating text is suppressed; the real hit and healing paths still run.
        using var textHook=new MonoMod.RuntimeDetour.Hook(
            typeof(CombatText).GetMethod(nameof(CombatText.NewText),[typeof(Rectangle),typeof(Color),typeof(string),typeof(bool),typeof(bool)]),
            typeof(HardOreCheck).GetMethod(nameof(HeadlessText),System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static));
        check(HardOreTypes.Types.Distinct().Count()==6,"Six distinct hardmode ore sword IDs");
        foreach(int type in HardOreTypes.Types)
        {
            check(SoulItem.CanHost(new Item(type)) && WoodTypes.CanSupport(new Item(type)),"Ore sword admitted to attack and support slots: "+type);
            check(HardOreTypes.Description(type).Length>10 && WeaponFamilies.Name(type).Contains("肉后三矿"),"Ore trait has visible description: "+type);
        }
        var (player,soul,target)=Setup(ItemID.CobaltSword);
        var blade=Blade(player,soul,ItemID.CobaltSword);
        DamageBalanceCheck.Recording=true;DamageBalanceCheck.Hits=0;
        for(int i=0;i<20;i++)Hit(blade,target);
        check(Shots(HardOreKind.Cobalt).Length==0,"Repeated hits within one clock frame do not fill Cobalt charge");
        Advance(player,30);Hit(blade,target);Advance(player,30);Hit(blade,target);
        DamageBalanceCheck.Recording=false;
        check(Shots(HardOreKind.Cobalt).Length==1 && DamageBalanceCheck.Hits>0,"Actual main hits inside a large target charge Cobalt correctly");
        var cobalt=Shots(HardOreKind.Cobalt)[0];
        check(cobalt.damage==40 && cobalt.timeLeft==45 && cobalt.penetrate==1,"Third legal Cobalt hit emits actual bounded forty-percent blade");
        check(cobalt.GetGlobalProjectile<SoulSource>().SoulId==soul.SoulId && cobalt.GetGlobalProjectile<SoulSource>().SourceWeapon==ItemID.CobaltSword,"Child inherits soul and actual source weapon metadata");
        int lifeBeforeChild=target.life;cobalt.Center=target.Center;cobalt.Damage();
        check(target.life<lifeBeforeChild,"Cobalt child deals actual engine damage");cobalt.Kill();
        Advance(player,30);Hit(blade,target);Advance(player,30);Hit(blade,target);
        check(Shots(HardOreKind.Cobalt).Length==0,"Child hit does not charge the next Cobalt proc");
        Advance(player,30);Hit(blade,target);check(Shots(HardOreKind.Cobalt).Length==1,"Next Cobalt proc still requires three main hits");
        (player,soul,target)=Setup(ItemID.OrichalcumSword);
        blade=Blade(player,soul,ItemID.OrichalcumSword);Hit(blade,target);
        var flowers=Shots(HardOreKind.Orichalcum);
        check(flowers.Length==3 && flowers.Sum(p=>p.damage)==30 && flowers.All(p=>p.penetrate==1),"Orichalcum creates three petals sharing thirty-percent total budget");
        foreach(var flower in flowers)flower.ModProjectile.AI();
        check(flowers.All(p=>p.active && p.velocity.Length()>0),"Petal AI runs and steers with valid owned source");
        Advance(player,30);Hit(blade,target);check(Shots(HardOreKind.Orichalcum).Length==3,"Petal proc honors its own 120-frame cooldown");
        (player,soul,target)=Setup(ItemID.OrichalcumSword);blade=Blade(player,soul,ItemID.OrichalcumSword,1);Hit(blade,target);
        flowers=Shots(HardOreKind.Orichalcum);
        check(flowers.Length==3 && flowers.Sum(p=>p.damage)==1 && flowers.Count(p=>p.friendly)==1,"Low-damage petals cannot multiply minimum-one damage");

        (player,soul,target)=Setup(ItemID.MythrilSword,ItemID.EnchantedSword);
        blade=Blade(player,soul,ItemID.MythrilSword);Hits(player,blade,target,4);
        check(Math.Abs(Modifier(player,blade,target)-100)<.01,"Mythril cannot consume its own new mark on the same flight");
        var other=Blade(player,soul,ItemID.EnchantedSword);
        check(Math.Abs(Modifier(player,other,target)-112)<.01 && Math.Abs(Modifier(player,other,target)-100)<.01,"Other sword consumes Mythril mark exactly once using its own hit multiplier");
        (player,soul,target)=Setup(ItemID.MythrilSword);blade=Blade(player,soul,ItemID.MythrilSword);Hits(player,blade,target,4);
        other=Blade(player,soul,ItemID.MythrilSword);
        check(Math.Abs(Modifier(player,other,target)-112)<.01,"Single-slot Mythril may consume mark on its next flight");
        (player,soul,target)=Setup(ItemID.MythrilSword,ItemID.EnchantedSword);blade=Blade(player,soul,ItemID.MythrilSword);Hits(player,blade,target,4);
        Advance(player,181);other=Blade(player,soul,ItemID.EnchantedSword);
        check(Math.Abs(Modifier(player,other,target)-100)<.01,"Expired Mythril mark gives no bonus");

        (player,soul,target)=Setup(ItemID.AdamantiteSword);blade=Blade(player,soul,ItemID.AdamantiteSword);
        target.defense=29;check(Math.Abs(Modifier(player,blade,target)-100)<.01,"Adamantite excludes targets below defense threshold");
        target.defense=30;check(Math.Abs(Modifier(player,blade,target)-112)<.01,"Adamantite applies twelve-percent source modifier at threshold");
        (player,soul,target)=Setup(ItemID.TitaniumSword);blade=Blade(player,soul,ItemID.TitaniumSword);
        DamageBalanceCheck.Recording=true;DamageBalanceCheck.Hits=0;Hits(player,blade,target,4);DamageBalanceCheck.Recording=false;
        check(Shots(HardOreKind.Titanium).Length==1,$"Titanium spawn: hits={DamageBalanceCheck.Hits}, clock={player.GetModPlayer<HardOrePlayer>().Clock}, active={blade.active}, host={blade.ai[0]}, kind={HardOreTypes.Index((int)blade.ai[0])}, held={player.HeldItem.ModItem==soul}");
        var titanium=Shots(HardOreKind.Titanium)[0];titanium.ModProjectile.AI();
        check(titanium.damage==30 && titanium.timeLeft==30 && Math.Abs(Vector2.Distance(titanium.Center,blade.Center)-18)<.01,"Titanium shard has thirty-percent damage and actually orbits its source blade");
        Advance(player,30);Hits(player,blade,target,4);
        check(Shots(HardOreKind.Titanium).Length==1,"Titanium source channel cannot retain two live shards");
        blade.Kill();titanium.ModProjectile.AI();check(!titanium.active,"Titanium shard ends when its exact source flight ends");

        VerifySupports(check);VerifyHealing(check);VerifyGuards(check);
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
    }
    private static void VerifySupports(Action<bool,string> check)
    {
        var (player,soul,target)=Setup(ItemID.WoodenSword,ItemID.CopperShortsword,ItemID.EnchantedSword);
        Item cursor=new(ItemID.CobaltSword);
        check(soul.TrySwapHost(10,ref cursor,out _) && cursor.IsAir,"Actual support-slot swap accepts registered ore trait");
        var first=Blade(player,soul,ItemID.CopperShortsword,20);var second=Blade(player,soul,ItemID.EnchantedSword,100);
        for(int i=0;i<3;i++){if(i>0)Advance(player,30);Hit(first,target);Hit(second,target);}
        var shots=Shots(HardOreKind.Cobalt);
        check(shots.Length==2 && shots.Select(p=>p.damage).Order().SequenceEqual(new[]{8,40}),"Support Cobalt uses separate counters and the actual damage of each main source");
        check(!Main.projectile.Any(p=>p.active && p.ModProjectile is SoulBlade && (int)p.ai[0]==ItemID.CobaltSword),"Support ore never emits an independent main blade");
        soul.SupportHosts[0].TurnToAir();foreach(var shot in shots)shot.ModProjectile.AI();
        check(shots.All(p=>!p.active),"Removing support provider ends its child attacks immediately");
        (player,soul,target)=Setup(ItemID.WoodenSword,ItemID.CopperShortsword,ItemID.EnchantedSword);
        soul.SupportHosts[0]=new Item(ItemID.MythrilSword);
        first=Blade(player,soul,ItemID.CopperShortsword);second=Blade(player,soul,ItemID.EnchantedSword);
        for(int i=0;i<4;i++){if(i>0)Advance(player,30);Hit(first,target);Hit(second,target);}
        // Second source may consume the first mark on its charging hit, so
        // re-create the first source's mark before testing its independent consumer.
        Advance(player,30);Hits(player,first,target,4);
        check(Math.Abs(Modifier(player,second,target)-112)<.01,"Support Mythril creates cross-source marks without borrowing support damage");
    }
    private static void VerifyHealing(Action<bool,string> check)
    {
        var (player,soul,target)=Setup(ItemID.WoodenSword,ItemID.CopperShortsword,ItemID.IronBroadsword,ItemID.EnchantedSword,ItemID.Starfury,ItemID.BeeKeeper);
        soul.SupportHosts[0]=new Item(ItemID.PalladiumSword);
        player.statLifeMax=player.statLifeMax2=400;player.statLife=100;player.lifeSteal=80;
        var blades=soul.Active.Skip(1).Select(i=>Blade(player,soul,i.type)).ToArray();
        for(int hit=0;hit<5;hit++){if(hit>0)Advance(player,30);foreach(var p in blades)Hit(p,target);}
        check(player.statLife==108 && Math.Abs(player.lifeSteal-72)<.01,"Palladium shares eight-life rolling budget across five source channels and spends life-steal resource");
        Advance(player,600);
        for(int hit=0;hit<5;hit++){if(hit>0)Advance(player,30);foreach(var p in blades)Hit(p,target);}
        check(player.statLife==116 && Math.Abs(player.lifeSteal-64)<.01,"Palladium rolling budget becomes available after its six-second window expires");
        foreach(string reason in new[]{"leech","resource","full","dummy"})
        {
            (player,soul,target)=Setup(ItemID.PalladiumSword);player.statLifeMax=player.statLifeMax2=400;player.statLife=100;player.lifeSteal=80;
            if(reason=="leech")player.moonLeech=true;
            if(reason=="resource")player.lifeSteal=0;
            if(reason=="full")player.statLife=400;
            if(reason=="dummy")target.type=NPCID.TargetDummy;
            var blade=Blade(player,soul,ItemID.PalladiumSword);int before=player.statLife;Hits(player,blade,target,5);
            check(player.statLife==before,"Palladium excludes "+reason);
        }
    }
    private static void VerifyGuards(Action<bool,string> check)
    {
        var (player,soul,target)=Setup(ItemID.CobaltSword);var blade=Blade(player,soul,ItemID.CobaltSword);
        Hits(player,blade,target,2);Advance(player,181);Hit(blade,target);
        check(Shots(HardOreKind.Cobalt).Length==0,"Expired combo does not retain its old charge");
        (player,soul,target)=Setup(ItemID.CobaltSword);blade=Blade(player,soul,ItemID.CobaltSword);
        var segment=new NPC();segment.SetDefaults(NPCID.Zombie);segment.whoAmI=21;segment.realLife=20;segment.active=true;
        segment.life=segment.lifeMax=1000000;segment.HideStrikeDamage=true;segment.Center=target.Center;Main.npc[21]=segment;
        player.GetModPlayer<HardOrePlayer>().Hit(blade,target,100);player.GetModPlayer<HardOrePlayer>().Hit(blade,segment,100);
        Advance(player,30);player.GetModPlayer<HardOrePlayer>().Hit(blade,segment,100);
        check(Shots(HardOreKind.Cobalt).Length==0,"Worm segments share one target charge gate");
        Advance(player,30);player.GetModPlayer<HardOrePlayer>().Hit(blade,segment,100);
        check(Shots(HardOreKind.Cobalt).Length==1,"Shared worm root can charge on subsequent valid frames");
        using(var data=new MemoryStream())
        {
            var source=blade.GetGlobalProjectile<SoulSource>();source.SendExtraAI(blade,null,new BinaryWriter(data));data.Position=0;
            var copy=new Projectile();copy.SetDefaults(ModContent.ProjectileType<HardOreShot>());
            copy.GetGlobalProjectile<SoulSource>().ReceiveExtraAI(copy,null,new BinaryReader(data));
            check(data.Position==data.Length && copy.GetGlobalProjectile<SoulSource>().SourceWeapon==ItemID.CobaltSword &&
                copy.GetGlobalProjectile<SoulSource>().SoulId==soul.SoulId,"Soul and source weapon metadata survive actual packet serialization");
        }
        foreach(bool server in new[]{false,true})
        {
            (player,soul,target)=Setup(ItemID.CobaltSword);blade=Blade(player,soul,ItemID.CobaltSword);
            if(server)Main.netMode=NetmodeID.Server;else Main.myPlayer=1;
            Hits(player,blade,target,3);
            check(Shots(HardOreKind.Cobalt).Length==0,"Only owner creates ore procs: "+(server?"server":"remote"));
        }
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
    }
}
