using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using E=ZenithReborn.SwordEnvironment;

namespace ZenithReborn;

public class EnvironmentPlayer : ModPlayer
{
    public SoulItem Held=>!Player.dead && Player.HeldItem.ModItem is SoulItem s && s.Attached?s:null;
    public bool Owner=>Main.netMode!=NetmodeID.Server && Player.whoAmI==Main.myPlayer;
    public int NaturalMask=>EnvironmentRules.Natural(Player);
    public int Terrain=>Held?.DomainEnvironment is E domain && domain!=E.None?EnvironmentRules.Bit(domain):NaturalMask;
    private Dictionary<string,float> stats=[];
    private string current="";
    private readonly Dictionary<E,int> hits=[];
    private readonly Dictionary<E,ulong> cooldown=[];
    private int lastWeapon=-1,recentCombat,nectarClock;
    private float leafCharge,iceCharge,healFraction,regenFraction,leechBudget,nectar,repair;
    private int nectarLeft,repairLeft;
    private int pendingIceAbsorption;
    public bool NectarReady {get;private set;}
    public float IceArmor {get;private set;}
    public bool LeafReady=>leafCharge>=1800;
    public float Stat(string key)=>Held==null?0:stats.GetValueOrDefault(key);
    public float Strength(E e)
    {
        var s=Held;if(s==null || !EnvironmentRules.Has(s.EnvironmentCollection,e))return 0;
        return EnvironmentRules.Shared(s.EnvironmentCollection)*EnvironmentRules.Forest(s.EnvironmentCollection)*
            (s.DomainEnvironment==e?1.5f:1)*EnvironmentRules.Adaptation(e,Terrain);
    }
    public bool Mechanism(E e,Vector2? at=null)
    {
        var s=Held;if(s==null || !EnvironmentRules.Has(s.EnvironmentCollection,e))return false;
        if(EnvironmentRules.Has(s.EnvironmentBlessings,e))return true;
        bool inside=!at.HasValue || Vector2.DistanceSquared(Player.Center,at.Value)<=EnvironmentRules.DomainRadius*EnvironmentRules.DomainRadius;
        return s.DomainEnvironment!=E.None && inside?s.DomainEnvironment==e:EnvironmentRules.Has(NaturalMask,e);
    }
    public void RefreshEnvironmentStats()
    {
        stats.Clear();var s=Held;if(s==null)return;s.RegisterEnvironments();
        foreach(var d in EnvironmentAttributes.All)
            if(EnvironmentRules.Has(s.EnvironmentCollection,d.Id))foreach(var v in d.Values)
                stats[v.Key]=stats.GetValueOrDefault(v.Key)+v.Value*Strength(d.Id);
    }
    public override void PostUpdateEquips()
    {
        RefreshEnvironmentStats();if(Held==null)return;
        Player.statDefense+=(int)Stat("Defense");Player.statLifeMax2+=(int)Stat("Life");
        Player.GetDamage(DamageClass.Melee)+=Stat("Damage");Player.GetCritChance(DamageClass.Melee)+=Stat("Crit");
        Player.GetAttackSpeed(DamageClass.Melee)+=Stat("Speed");Player.GetArmorPenetration(DamageClass.Melee)+=Stat("Penetration");
        Player.GetKnockback(DamageClass.Melee)+=Stat("Knockback");Player.moveSpeed+=Stat("Move");
        Player.endurance+=Stat("Reduction");
    }
    public override void UpdateLifeRegen()
    {
        if(Held==null){regenFraction=0;return;}
        regenFraction+=2*Stat("Regen")*(1+Stat("Healing"));
        int units=(int)regenFraction;regenFraction-=units;Player.lifeRegen+=units;
        if(Stat("Recovery")>0 && Player.lifeRegenTime>0)Player.lifeRegenTime+=(int)Math.Ceiling(Stat("Recovery"));
    }
    public override void GetHealLife(Item item,bool quickHeal,ref int healValue)
    {if(Held!=null)healValue=(int)(healValue*(1+Stat("Healing")));}
    public float HealingMultiplier=>1+Stat("Healing");
    public override void PostUpdate()
    {
        var s=Held;
        if(s?.SoulId!=current){ResetTransient();current=s?.SoulId??"";}
        if(s==null)return;
        if(recentCombat>0)recentCombat--;
        if(Mechanism(E.Forest))leafCharge=Math.Min(1800,leafCharge+Strength(E.Forest));
        if(Mechanism(E.Ice) && IceArmor<=0 && ++iceCharge>=300)
        {IceArmor=Player.statLifeMax2*.04f*Strength(E.Ice)*(1+Stat("Shield"));iceCharge=0;}
        if(Mechanism(E.Jungle) && recentCombat>0 && !NectarReady && ++nectarClock>=720)
        {NectarReady=true;nectarClock=0;}
        leechBudget=Math.Min(Player.statLifeMax2*.02f*Math.Max(1,Strength(E.Underworld)),
            leechBudget+Player.statLifeMax2*.01f*Math.Max(1,Strength(E.Underworld))/60f);
        if(Owner)
        {
            if(nectarLeft>0){float part=nectar/nectarLeft--;nectar-=part;Recover(part);}
            if(repairLeft>0){float part=repair/repairLeft--;repair-=part;Recover(part);}
            if(s.DomainEnvironment!=E.None && !Main.projectile.Any(p=>p.active && p.owner==Player.whoAmI && p.ModProjectile is EnvironmentDomain))
                Projectile.NewProjectile(Player.GetSource_Misc("EnvironmentDomain"),Player.Center,Vector2.Zero,
                    ModContent.ProjectileType<EnvironmentDomain>(),0,0,Player.whoAmI);
        }
        if(!Main.dedServ && LeafReady && Main.GameUpdateCount%12==0)
        {var d=Dust.NewDustPerfect(Player.Center+Main.rand.NextVector2CircularEdge(25,32),DustID.Grass,Vector2.Zero,0,EnvironmentRules.Color(E.Forest),.7f);d.noGravity=true;}
    }
    private void ResetTransient()
    {hits.Clear();cooldown.Clear();leafCharge=iceCharge=healFraction=regenFraction=leechBudget=nectar=repair=0;nectarLeft=repairLeft=recentCombat=nectarClock=0;IceArmor=0;NectarReady=false;lastWeapon=-1;}
    public override void UpdateDead(){ResetTransient();stats.Clear();current="";}
    public override void OnEnterWorld(){ResetTransient();stats.Clear();current="";}
    public override bool ConsumableDodge(Player.HurtInfo info)
    {
        if(!LeafReady || !Mechanism(E.Forest))return false;
        leafCharge=0;Player.immune=true;Player.immuneTime=Math.Max(Player.immuneTime,40);
        if(Owner)EnvironmentEffect.Spawn(Player,Held,E.Forest,Player.Center,Vector2.Zero,0,0);
        return true;
    }
    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        modifiers.Knockback*=1/(1+Stat("KnockbackResistance"));
        if(IceArmor<=0 || !Mechanism(E.Ice))return;
        pendingIceAbsorption=0;
        modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>{
            pendingIceAbsorption=Math.Min(Math.Max(0,info.Damage-1),(int)IceArmor);info.Damage-=pendingIceAbsorption;
        };
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        if(Held==null || info.Damage<=0)return;
        IceArmor=Math.Max(0,IceArmor-pendingIceAbsorption);pendingIceAbsorption=0;
        if(Mechanism(E.Jungle) && NectarReady)
        {NectarReady=false;nectar+=Player.statLifeMax2*.03f*Strength(E.Jungle);nectarLeft=240;}
        if(Mechanism(E.UndergroundCrimson)){repair+=info.Damage*.1f*Strength(E.UndergroundCrimson);repairLeft=240;}
        if(Mechanism(E.Ice) && Owner)
            EnvironmentEffect.Spawn(Player,Held,E.Ice,Player.Center,Vector2.Zero,Math.Max(1,(int)(info.Damage*.5f*Strength(E.Ice))),0);
    }
    public int Recover(float raw,bool leech=false)
    {
        if(!Owner || Player.dead || Player.HasBuff(BuffID.MoonLeech) || Player.statLife>=Player.statLifeMax2){healFraction=0;return 0;}
        float amount=raw*HealingMultiplier;
        if(leech)amount=Math.Min(amount,Math.Min(leechBudget,Math.Max(0,Player.lifeSteal)));
        if(amount<=0)return 0;
        if(leech){leechBudget-=amount;Player.lifeSteal-=amount;}
        healFraction+=amount;int whole=Math.Min((int)healFraction,Player.statLifeMax2-Player.statLife);
        if(whole>0){healFraction-=whole;Player.Heal(whole);}return whole;
    }
    public void ModifyHit(Projectile p,NPC target,ref NPC.HitModifiers m)
    {
        if(Held?.SoulId!=p.GetGlobalProjectile<SoulSource>().SoulId)return;
        m.CritDamage+=Stat("CritDamage");
        if(Mechanism(E.Hallow,target.Center))m.CritDamage+=Math.Max(0,p.CritChance-100)*.005f*Strength(E.Hallow);
        var mark=target.GetGlobalNPC<EnvironmentTarget>().Find(Player.whoAmI,Held.SoulId);
        if(mark==null)return;
        m.ArmorPenetration+=mark.Penetration;
        if(mark.FrostUntil>Main.GameUpdateCount && Mechanism(E.Snow,target.Center))m.FinalDamage*=1+.04f*Strength(E.Snow);
    }
    public void DiscoverNaturalKill(NPC target)
    {
        var s=Held;if(s==null || target.friendly || target.SpawnedFromStatue || target.lifeMax<=5)return;
        int before=s.EnvironmentCollection;s.EnvironmentCollection|=NaturalMask;s.NormalizeEnvironments();
        if(before!=s.EnvironmentCollection && Owner)
        {SoulUI.Sync(s);if(!Main.dedServ)CombatText.NewText(Player.Hitbox,new Color(156,233,181),"收集环境之力");}
    }
    private bool Proc(E e,int threshold,int frames=0)
    {
        hits[e]=hits.GetValueOrDefault(e)+1;
        if(hits[e]<threshold || cooldown.GetValueOrDefault(e)>Main.GameUpdateCount)return false;
        hits[e]=0;cooldown[e]=Main.GameUpdateCount+(ulong)frames;return true;
    }
    public void Hit(Projectile p,NPC target,NPC.HitInfo hit,int damageDone,bool first,bool remote=false)
    {
        var s=Held;if(s==null || s.SoulId!=p.GetGlobalProjectile<SoulSource>().SoulId || damageDone<=0)return;
        if(!remote && !Owner)return;
        recentCombat=90;
        if(!remote && Mechanism(E.Underworld,target.Center))Recover(damageDone*.001f*Strength(E.Underworld)*(1+Stat("LeechPower")),true);
        if(target.life<=0 && !remote)DiscoverNaturalKill(target);
        if(!first || p.ModProjectile is not SoulBlade)return;
        var state=target.GetGlobalNPC<EnvironmentTarget>().Get(Player.whoAmI,s.SoulId);
        state.Expires=Main.GameUpdateCount+(ulong)(360*(1+Stat("DebuffDuration")));
        float D=Math.Max(1,p.damage);int weapon=p.GetGlobalProjectile<SoulSource>().HostSlot;
        void Shot(E e,float factor,int shape=0,Vector2? position=null,Vector2? velocity=null)
        {if(!remote)EnvironmentEffect.Spawn(Player,s,e,position??target.Center,velocity??Vector2.Zero,Math.Max(1,(int)(D*factor*Strength(e))),shape,p);}
        if(Mechanism(E.Underground,target.Center) && Proc(E.Underground,6,30))Shot(E.Underground,.6f);
        if(Mechanism(E.Caverns,target.Center))
        {state.Rock=Math.Min(6,state.Rock+1);state.Penetration=Math.Max(state.Penetration,state.Rock*Strength(E.Caverns));if(state.Rock==6 && Proc(E.Caverns,6,60))Shot(E.Caverns,1);}
        if(Mechanism(E.Space,target.Center) && lastWeapon!=weapon && Proc(E.Space,3,30))
            Shot(E.Space,.8f,2,target.Center-new Vector2(0,260),Vector2.UnitY*13);
        if(Mechanism(E.Snow,target.Center))
        {state.FrostUntil=Main.GameUpdateCount+180;if(++state.Frost>=5){state.Frost=0;Shot(E.Snow,.7f);}if(!target.boss)target.AddBuff(BuffID.Chilled,120);}
        if(Mechanism(E.Desert,target.Center) && Proc(E.Desert,3,25))
            Shot(E.Desert,.6f,3,target.Center-new Vector2(90*Player.direction,0),Vector2.UnitX*Player.direction*8);
        if(Mechanism(E.UndergroundDesert,target.Center) && Proc(E.UndergroundDesert,4,90))Shot(E.UndergroundDesert,.15f,4);
        if(Mechanism(E.Ocean,target.Center) && Proc(E.Ocean,8,60))
            Shot(E.Ocean,.5f,5,Player.Center,(target.Center-Player.Center).SafeNormalize(Vector2.UnitX)*9);
        if(Mechanism(E.UndergroundJungle,target.Center))
        {state.Poison=Math.Max(state.Poison,D*.12f*Strength(E.UndergroundJungle));if(state.LastPoisonWeapon!=weapon){state.LastPoisonWeapon=weapon;if(++state.Buds>=5){state.Buds=0;Shot(E.UndergroundJungle,.8f);}}}
        if(Mechanism(E.JungleTemple,target.Center) && Proc(E.JungleTemple,12,90))
            Shot(E.JungleTemple,1.2f,6,Player.Center,(target.Center-Player.Center).SafeNormalize(Vector2.UnitX)*16);
        if(Mechanism(E.GlowingMushroom,target.Center) && Proc(E.GlowingMushroom,8,120))Shot(E.GlowingMushroom,.1f,4);
        if(Mechanism(E.Corruption,target.Center))
        {
            state.Corrosion=Math.Min(5,state.Corrosion+1);state.Penetration=Math.Max(state.Penetration,state.Corrosion*1.5f*Strength(E.Corruption));
            if(state.Corrosion==5 && !remote)foreach(var n in Main.npc.Where(n=>n.active && !n.friendly && n.whoAmI!=target.whoAmI && Vector2.DistanceSquared(n.Center,target.Center)<180*180))
            {var spread=n.GetGlobalNPC<EnvironmentTarget>().Get(Player.whoAmI,s.SoulId);spread.Penetration=Math.Max(spread.Penetration,state.Penetration*.5f);spread.Expires=state.Expires;}
        }
        if(Mechanism(E.UndergroundCorruption,target.Center) && Proc(E.UndergroundCorruption,6,45))
            Shot(E.UndergroundCorruption,.7f,7,target.Center-new Vector2(0,80),Vector2.UnitX*Player.direction*5);
        if(Mechanism(E.Crimson,target.Center))state.Bleed=Math.Max(state.Bleed,D*.1f*Strength(E.Crimson));
        if(Mechanism(E.UndergroundHallow,target.Center) && state.LastCrystalWeapon!=weapon)
        {state.LastCrystalWeapon=weapon;if(++state.Crystals>=4){state.Crystals=0;Shot(E.UndergroundHallow,1);}}
        if(Mechanism(E.Dungeon,target.Center))
        {state.Seals.Add(weapon);if(state.Seals.Count>=4){state.Seals.Clear();Shot(E.Dungeon,1.8f);}}
        lastWeapon=weapon;
        if(!remote && Main.netMode==NetmodeID.MultiplayerClient)
        {
            var packet=Mod.GetPacket();packet.Write((byte)11);packet.Write((short)p.identity);packet.Write((short)target.whoAmI);
            packet.Write(Math.Clamp(damageDone,1,200000000));packet.Send();
        }
    }
    public static void ReceiveHit(BinaryReader reader,int sender)
    {
        int identity=reader.ReadInt16(),npc=reader.ReadInt16(),damage=reader.ReadInt32();
        if(Main.netMode!=NetmodeID.Server || sender<0 || sender>=Main.maxPlayers || npc<0 || npc>=Main.maxNPCs || damage<=0)return;
        var p=Main.projectile.FirstOrDefault(q=>q.active && q.owner==sender && q.identity==identity && q.ModProjectile is SoulBlade);
        if(p==null || !Main.npc[npc].active)return;
        var source=p.GetGlobalProjectile<SoulSource>();if(source.EnvironmentServerHit)return;source.EnvironmentServerHit=true;
        Main.player[sender].GetModPlayer<EnvironmentPlayer>().Hit(p,Main.npc[npc],default,damage,true,true);
    }
}

public class EnvironmentTarget : GlobalNPC
{
    public override bool InstancePerEntity=>true;
    public sealed class Mark
    {
        public string Soul;public ulong Expires,FrostUntil;public int Rock,Frost,Buds,Crystals,Corrosion,LastPoisonWeapon=-1,LastCrystalWeapon=-1;
        public float Penetration,Poison,Bleed,Spore;public HashSet<int> Seals=[];
    }
    private Dictionary<int,Mark> owners=[];
    public Mark Get(int owner,string soul)
    {if(!owners.TryGetValue(owner,out var m) || m.Soul!=soul || m.Expires<Main.GameUpdateCount)owners[owner]=m=new(){Soul=soul};return m;}
    public Mark Find(int owner,string soul)=>owners.TryGetValue(owner,out var m) && m.Soul==soul && m.Expires>=Main.GameUpdateCount?m:null;
    public override void OnSpawn(NPC npc,IEntitySource source)=>owners=[];
    public override void UpdateLifeRegen(NPC npc,ref int damage)
    {
        float dps=0;
        foreach(var pair in owners.ToArray())
        {
            var m=pair.Value;if(m.Expires<Main.GameUpdateCount || pair.Key>=Main.maxPlayers){owners.Remove(pair.Key);continue;}
            var ep=Main.player[pair.Key].GetModPlayer<EnvironmentPlayer>();if(ep.Held?.SoulId!=m.Soul)continue;
            float value=(ep.Mechanism(E.UndergroundJungle,npc.Center)?m.Poison:0)+(ep.Mechanism(E.Crimson,npc.Center)?m.Bleed:0);
            dps+=value*(1+ep.Stat("Dot"))*(1+m.Spore);
        }
        if(dps<=0)return;if(npc.lifeRegen>0)npc.lifeRegen=0;npc.lifeRegen-=(int)Math.Ceiling(dps*2);damage=Math.Max(damage,(int)Math.Max(1,dps/4));
    }
}

public class EnvironmentBuffHooks : ModSystem
{
    public override void Load()=>On_Player.AddBuff+=Shorten;
    public override void Unload()=>On_Player.AddBuff-=Shorten;
    private static void Shorten(On_Player.orig_AddBuff orig,Player player,int type,int duration,bool quiet,bool foodHack)
    {
        if(WoodPlayer.IsShortenable(type))duration=Math.Max(1,(int)(duration/(1+player.GetModPlayer<EnvironmentPlayer>().Stat("DebuffResistance"))));
        orig(player,type,duration,quiet,foodHack);
    }
}
