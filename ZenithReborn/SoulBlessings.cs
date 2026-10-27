using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ZenithReborn;

// 祝福的运行参数集中在此处，不作为个人进度标记保存。
public static class BlessingTuning
{
    public const int ChargeFrames=900, BurstFrames=600, CombatGrace=45;
    public const int RelayTimeout=180, RelayMax=5;
    public const float RelayStep=.10f, ReturnDamage=.50f;
    public const int BreachArmor=15, BreachFrames=240;
    public static bool Blessed(SoulItem soul,SoulCurse curse)=>soul?.Attached==true && soul.Curse(curse)==CurseStage.Blessed;
}

public readonly record struct BlessingHost(string Soul,int Slot,int Type);

public class BlessingPlayer : ModPlayer
{
    private sealed class Relay { public BlessingHost Host; public int Layers; public long Last; }
    private readonly Dictionary<(string Soul,long Target),Relay> relays=[];
    private readonly HashSet<BlessingHost> returns=[];
    private long clock,lastCombat=-1000;
    private string combatSoul="";
    public string BurstSoul { get; private set; }="";
    public int BurstLeft { get; private set; }
    public float ZoomBlend { get; private set; }
    private bool Owner=>Player.whoAmI==Main.myPlayer && Main.netMode!=NetmodeID.Server;
    public SoulItem Held=>Player.active && !Player.dead && Player.HeldItem.ModItem is SoulItem s && s.Attached?s:null;
    public bool BurstFor(SoulItem soul)=>BurstLeft>0 && BurstSoul==soul?.SoulId && BlessingTuning.Blessed(soul,SoulCurse.Binding);
    public static BlessingHost Host(Projectile p)
    { var source=p.GetGlobalProjectile<SoulSource>();return new(source.SoulId,source.HostSlot,source.SourceWeapon); }
    public bool ValidSource(Projectile p,out SoulItem soul)
    {
        soul=Held;var source=p.GetGlobalProjectile<SoulSource>();var held=soul;
        return Owner && held!=null && held.SoulId==source.SoulId && source.HostSlot>=0 &&
            held.Active.Any(i=>ReferenceEquals(i,held.ItemAt(source.HostSlot)) && i.type==source.SourceWeapon);
    }
    public void RecordCombat(SoulItem soul)
    { if(Owner && Held==soul){combatSoul=soul.SoulId;lastCombat=clock;} }
    public override void ProcessTriggers(TriggersSet triggersSet)
    { if(ZenithReborn.ActivateYujian?.JustPressed==true)TryActivate(); }
    public bool TryActivate()
    {
        var soul=Held;
        if(!Owner || Main.gameMenu || Player.CCed || SoulUI.IsEditing(soul) ||
            !BlessingTuning.Blessed(soul,SoulCurse.Binding) || soul.YujianCharge<BlessingTuning.ChargeFrames || BurstLeft>0)return false;
        soul.YujianCharge=0;BurstSoul=soul.SoulId;BurstLeft=BlessingTuning.BurstFrames;
        SyncCharge(soul);SyncBurst();
        if(!Main.dedServ){CombatText.NewText(Player.Hitbox,new Color(144,225,255),"御剑 · 开");
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29,Player.Center);}
        return true;
    }
    private void SyncCharge(SoulItem soul)
    {
        if(Main.netMode!=NetmodeID.MultiplayerClient)return;
        int slot=Array.FindIndex(Player.inventory,i=>ReferenceEquals(i.ModItem,soul));
        if(slot>=0)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Player.whoAmI,slot);
    }
    private void SyncBurst(int to=-1,int ignore=-1)
    {
        if(Main.netMode==NetmodeID.SinglePlayer)return;
        var packet=Mod.GetPacket();packet.Write((byte)10);packet.Write((byte)Player.whoAmI);
        packet.Write(BurstSoul);packet.Write((ushort)BurstLeft);packet.Send(to,ignore);
    }
    public override void SyncPlayer(int toWho,int fromWho,bool newPlayer)=>SyncBurst(toWho,fromWho);
    public static void Receive(BinaryReader reader,int who)
    {
        int index=reader.ReadByte();string id=reader.ReadString();int left=reader.ReadUInt16();
        if(Main.netMode==NetmodeID.Server)index=who;
        if(index<0 || index>=Main.maxPlayers || id.Length>32 || left>BlessingTuning.BurstFrames)return;
        var state=Main.player[index].GetModPlayer<BlessingPlayer>();
        if(left>0 && (state.Held?.SoulId!=id || !BlessingTuning.Blessed(state.Held,SoulCurse.Binding)))return;
        state.BurstSoul=id;state.BurstLeft=left;
        if(Main.netMode==NetmodeID.Server)state.SyncBurst(-1,who);
    }
    public override void PostUpdate()
    {
        clock++;var soul=Held;bool editing=soul!=null && SoulUI.IsEditing(soul);
        if(Owner && !editing && BurstLeft==0 && BlessingTuning.Blessed(soul,SoulCurse.Binding) &&
            combatSoul==soul.SoulId && clock-lastCombat<=BlessingTuning.CombatGrace && soul.YujianCharge<BlessingTuning.ChargeFrames)
        {
            soul.YujianCharge++;
            if(soul.YujianCharge==BlessingTuning.ChargeFrames)
            {SyncCharge(soul);if(!Main.dedServ)CombatText.NewText(Player.Hitbox,new Color(180,235,255),"御剑已就绪");}
        }
        // 打开魂域暂停已经消耗的爆发时间；换武器不会清空未消耗的蓄力，也不会自动启动。
        if(BurstLeft>0 && !editing && --BurstLeft==0){BurstSoul="";if(Owner)SyncBurst();}
        if(Owner && BurstLeft>0 && clock%60==0)SyncBurst();
        bool wide=soul!=null && BurstFor(soul) && !editing && !Main.mapFullscreen;
        ZoomBlend=Math.Clamp(ZoomBlend+(wide?1:-1)/30f,0,1);
        foreach(var key in relays.Where(p=>clock-p.Value.Last>BlessingTuning.RelayTimeout).Select(p=>p.Key).ToArray())relays.Remove(key);
    }
    public float RelayPower(Projectile p,NPC target)
    {
        if(!ValidSource(p,out var soul) || !BlessingTuning.Blessed(soul,SoulCurse.Switching))return 1;
        var key=(soul.SoulId,WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id);
        if(!relays.TryGetValue(key,out var relay) || clock-relay.Last>BlessingTuning.RelayTimeout)return 1;
        return 1+Math.Min(BlessingTuning.RelayMax,relay.Layers+(relay.Host!=Host(p)?1:0))*BlessingTuning.RelayStep;
    }
    public void CommitRelay(Projectile p,NPC target)
    {
        if(!ValidSource(p,out var soul) || !BlessingTuning.Blessed(soul,SoulCurse.Switching))return;
        var key=(soul.SoulId,WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id);var host=Host(p);
        if(!relays.TryGetValue(key,out var relay) || clock-relay.Last>BlessingTuning.RelayTimeout)
            relays[key]=relay=new Relay{Host=host};
        else if(relay.Host!=host)relay.Layers=Math.Min(BlessingTuning.RelayMax,relay.Layers+1);
        relay.Host=host;relay.Last=clock;
    }
    public void StoreReturn(Projectile p)
    { if(ValidSource(p,out var soul) && BlessingTuning.Blessed(soul,SoulCurse.Sheathing))returns.Add(Host(p)); }
    public bool ConsumeReturn(Projectile p)=>ValidSource(p,out var soul) &&
        BlessingTuning.Blessed(soul,SoulCurse.Sheathing) && returns.Remove(Host(p));
    // 祝福后射程已无上限，距离增伤改用屏幕边界作参考，否则除以无限大后恒为基础值。
    public float DistanceMultiplier(SoulItem soul,NPC target)=>BurstFor(soul)?
        1+.5f*Math.Clamp(Vector2.Distance(Player.Center,target.Center)/Math.Max(1,SoulCurses.ScreenEdge(Player)),0,1):1;
    private void ClearRuntime()
    {BurstSoul="";BurstLeft=0;ZoomBlend=0;clock=0;lastCombat=-1000;combatSoul="";relays.Clear();returns.Clear();}
    public override void OnEnterWorld()=>ClearRuntime();
    public override void UpdateDead(){ClearRuntime();} // 未消耗的御剑蓄力由剑魂自身保留。
}

// 每把已发射主剑独立记录首击接力、穿障和归鞘；帧伤不能重复积累首击收益。
public sealed class BladeBlessings
{
    private readonly HashSet<long> firstTargets=[];
    private bool returned,breachPending,breachSpent;
    public bool ReturnArmed;
    public float TraitPower { get; private set; }=1;
    private static BlessingPlayer State(Projectile p)=>Main.player[p.owner].GetModPlayer<BlessingPlayer>();
    public void Prepare(Projectile p,NPC target)
    {
        long id=WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id;
        TraitPower=firstTargets.Contains(id)?1:State(p).RelayPower(p,target);
    }
    public void CrossedSolid(Projectile p,Vector2 from,Vector2 to)
    {
        if(breachSpent || breachPending || !State(p).ValidSource(p,out var soul) || !BlessingTuning.Blessed(soul,SoulCurse.Barrier))return;
        float distance=Vector2.Distance(from,to);int steps=Math.Max(1,(int)Math.Ceiling(distance/3));
        for(int i=1;i<=steps;i++)if(SoulTerrain.SolidAt(Vector2.Lerp(from,to,i/(float)steps))){breachPending=true;break;}
    }
    public void CompleteReturn(Projectile p)
    {if(returned)return;returned=true;State(p).StoreReturn(p);}
    public void Hit(Projectile p,NPC target,int damageDone)
    {
        if(damageDone<=0 || !HardOrePlayer.ValidTarget(target,false) || !State(p).ValidSource(p,out var soul))return;
        var state=State(p);state.RecordCombat(soul);var root=WoodCombat.Root(target);long id=root.GetGlobalNPC<HardOreTarget>().Id;
        if(firstTargets.Add(id))state.CommitRelay(p,target);
        if(ReturnArmed)
        {
            ReturnArmed=false;
            if(BlessingTuning.Blessed(soul,SoulCurse.Sheathing))BlessingStrike.Spawn(p,target,BlessingTuning.ReturnDamage,0);
        }
        if(breachPending && !breachSpent)
        {
            breachSpent=true;breachPending=false;
            if(BlessingTuning.Blessed(soul,SoulCurse.Barrier))
            {root.AddBuff(ModContent.BuffType<SoulBreach>(),BlessingTuning.BreachFrames);root.netUpdate=true;}
        }
    }
}

public class SoulBreach : ModBuff
{
    public override string Texture=>"Terraria/Images/Buff_"+BuffID.BrokenArmor;
    public override void SetStaticDefaults(){Main.debuff[Type]=true;Main.buffNoSave[Type]=true;}
}
public class BlessingTarget : GlobalNPC
{
    public override void ModifyIncomingHit(NPC npc,ref NPC.HitModifiers modifiers)
    {if(WoodCombat.Root(npc).HasBuff(ModContent.BuffType<SoulBreach>()))modifiers.ArmorPenetration+=BlessingTuning.BreachArmor;}
}

// 祝福斩击不调用主剑命中或触发逻辑；伤害取来源快照，距离倍率由来源模块结算一次。
public class BlessingStrike : ModProjectile
{
    private HashSet<long> struck=[];
    public override string Texture=>"Terraria/Images/Projectile_"+ProjectileID.SwordBeam;
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=100;Projectile.friendly=true;Projectile.DamageType=DamageClass.Melee;
        Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=12;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;
        struck=[];
    }
    public override bool ShouldUpdatePosition()=>false;
    public static int Spawn(Projectile parent,NPC target,float factor,int kind)
    {
        int index=Projectile.NewProjectile(parent.GetSource_FromThis(),target.Center,Vector2.Zero,
            ModContent.ProjectileType<BlessingStrike>(),Math.Max(1,(int)(parent.damage*factor)),0,parent.owner,kind,parent.rotation);
        if(index<Main.maxProjectiles){Main.projectile[index].CritChance=0;Main.projectile[index].netUpdate=true;}
        return index;
    }
    public override bool? CanHitNPC(NPC target)=>HardOrePlayer.ValidTarget(target) &&
        !struck.Contains(WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id)?null:false;
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {if(damageDone>0)struck.Add(WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id);}
    public override bool? Colliding(Rectangle projHitbox,Rectangle targetHitbox)
    {
        if(Projectile.ai[0]==1)return projHitbox.Intersects(targetHitbox);
        float point=0;Vector2 axis=Projectile.ai[1].ToRotationVector2()*55;
        return targetHitbox.Contains(Projectile.Center.ToPoint()) || Collision.CheckAABBvLineCollision(
            targetHitbox.TopLeft(),targetHitbox.Size(),Projectile.Center-axis,Projectile.Center+axis,14,ref point);
    }
    public override bool? CanDamage()=>Projectile.timeLeft<10?false:null;
    public override void AI()
    {
        if(!Main.dedServ && Projectile.timeLeft==12)
            OreSwordEffects.Impact(Projectile.Center,Vector2.UnitY,Projectile.ai[0]==0?Color.LightCyan:Color.MediumPurple,false);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade=Projectile.timeLeft/12f;Color tint=Projectile.ai[0]==0?Color.LightCyan:Color.MediumPurple;
        Vector2 axis=Projectile.ai[1].ToRotationVector2();
        OreSwordEffects.Line(Projectile.Center-axis*55,Projectile.Center+axis*55,tint,8*fade,.65f*fade);
        OreSwordEffects.Star(Projectile.Center,tint,1.3f*fade,.65f*fade,Projectile.ai[1]);
        if(Projectile.ai[0]==1)OreSwordEffects.Ring(Projectile.Center,tint,18+(1-fade)*35,.4f*fade);
        return false;
    }
}

public class BlessingInterface : ModSystem
{
    public override void ModifyTransformMatrix(ref SpriteViewMatrix transform)
    {
        if(Main.gameMenu || Main.dedServ)return;
        transform.Zoom/=1+Main.LocalPlayer.GetModPlayer<BlessingPlayer>().ZoomBlend;
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int at=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");
        layers.Insert(at<0?layers.Count:at,new LegacyGameInterfaceLayer("ZenithReborn: 御剑条",Draw,InterfaceScaleType.UI));
    }
    private static bool Draw()
    {
        if(Main.gameMenu || Main.hideUI || Main.mapFullscreen)return true;
        var state=Main.LocalPlayer.GetModPlayer<BlessingPlayer>();var soul=state.Held;
        if(!BlessingTuning.Blessed(soul,SoulCurse.Binding) || SoulUI.IsEditing(soul))return true;
        bool active=state.BurstFor(soul);float ratio=active?state.BurstLeft/(float)BlessingTuning.BurstFrames:soul.YujianCharge/(float)BlessingTuning.ChargeFrames;
        var keys=ZenithReborn.ActivateYujian?.GetAssignedKeys();string key=keys?.Count>0?keys[0]:"未绑定";
        string label=active?$"御剑 · {state.BurstLeft/60f:0.0}秒":soul.YujianCharge>=BlessingTuning.ChargeFrames?$"御剑就绪 · 按 {key}":$"御剑 · {ratio*100:0}%";
        Vector2 pos=new(Main.screenWidth*.5f-112,Main.screenHeight-115);
        Color tint=active?new(140,225,255):new(195,184,245);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)pos.X-2,(int)pos.Y-2,228,12),Color.Black*.65f);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)pos.X,(int)pos.Y,(int)(224*Math.Clamp(ratio,0,1)),8),tint*.85f);
        Utils.DrawBorderString(Main.spriteBatch,label,pos-new Vector2(0,23),tint,.72f);
        return true;
    }
}
