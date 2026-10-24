using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.UI;

namespace ZenithReborn;

// 仅供攻击试作；不从已废弃的WJ1推导种族奖励，关闭后恢复普通战斗。
public class MartialConfig : ModConfig
{
    public override ConfigScope Mode=>ConfigScope.ServerSide;
    [DefaultValue(true)] public bool EnableAttackPrototype=true;
}

public static class MartialTuning
{
    public const int PerTier=100,MaxCharge=300,ChargePerBlade=30;
    public const int FirstFormFrames=360,OrbitFrames=28,MaxPins=24;
    public const int GiantReadyFrames=MartialMotion.ThunderImpact+MartialMotion.ChargeFrames,DashFrames=12,HoldFrames=120,SlashFrames=22;
    public const float FreezeRadius=1600,GiantLength=330;
    public static bool Enabled=>ModContent.GetInstance<MartialConfig>().EnableAttackPrototype;
    public static int Cost(int tier)=>Math.Clamp(tier,1,3)*PerTier;
    public static int Blades(int tier)=>tier>=3?6:tier==2?4:3;
    public static bool Target(NPC n)=>n!=null && n.active && !n.friendly && !n.dontTakeDamage && n.life>0 && n.lifeMax>5 && !n.SpawnedFromStatue;
    public static NPC AimTarget(Player p,Vector2 aim)
    {
        NPC result=null;float best=700*700;
        foreach(var n in Main.ActiveNPCs)
            if(Target(n) && Vector2.DistanceSquared(n.Center,p.Center)<FreezeRadius*FreezeRadius)
            {float d=Vector2.DistanceSquared(n.Center,aim);if(d<best){best=d;result=n;}}
        return result;
    }
}

public static class MartialInput
{
    // 旧输入档可能没有新增按键；仅未绑定时回退默认键，玩家自设优先。
    public static bool Pressed(ModKeybind bind,Keys fallback)=>bind?.JustPressed==true ||
        (bind==null || bind.GetAssignedKeys().Count==0) && Main.keyState.IsKeyDown(fallback) && !Main.oldKeyState.IsKeyDown(fallback);
    public static string Name(ModKeybind bind,string fallback)=>bind?.GetAssignedKeys().FirstOrDefault()??fallback;
}

public enum MartialMode { Normal,Formation,Giant }

public class MartialPlayer : ModPlayer
{
    public MartialMode Mode { get; private set; }
    public int Tier { get; private set; }
    public int FormAge { get; private set; }
    public string FormSoul { get; private set; }="";
    public string Notice { get; private set; }="";
    private int noticeLeft,cursor,emitted;
    private Item[] weapons=[];
    private int[] slots=[];
    private int giantIdentity=-1;
    public SoulItem Held=>Player.active && !Player.dead && Player.HeldItem.ModItem is SoulItem s && s.Attached?s:null;
    public bool Owner=>Player.whoAmI==Main.myPlayer && Main.netMode!=NetmodeID.Server;
    public bool SuppressNative(SoulItem soul)=>Mode!=MartialMode.Normal && soul?.SoulId==FormSoul;
    public void Tell(string text){Notice=text;noticeLeft=150;}
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if(!Owner || Held==null || !MartialTuning.Enabled || Main.gameMenu || Main.drawingPlayerChat || Main.editSign || Main.editChest ||
            Main.blockInput || Main.ingameOptionsWindow || Player.CCed || SoulUI.IsEditing(Held))return;
        if(MartialInput.Pressed(ZenithReborn.CycleMartial,Keys.V))
        {Held.MartialTier=Held.MartialTier%3+1;Tell("武技选档 · "+Held.MartialTier);SyncItem(Held);}
        if(MartialInput.Pressed(ZenithReborn.ActivateMartial,Keys.G))
        {if(Mode!=MartialMode.Normal){Cancel();Tell("武技收势");}else TryActivate();}
    }
    private void SyncItem(SoulItem s)
    {
        if(Main.netMode!=NetmodeID.MultiplayerClient)return;
        int slot=Array.FindIndex(Player.inventory,i=>ReferenceEquals(i.ModItem,s));
        if(slot>=0)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,Player.whoAmI,slot);
    }
    public void PrimaryHit(Projectile p,NPC target,int damage)
    {
        var s=Held;var source=p.GetGlobalProjectile<SoulSource>();
        if(source.MartialCharged || p.ModProjectile is not SoulBlade || !Owner || !MartialTuning.Enabled ||
            s==null || damage<=0 || !MartialTuning.Target(target) || source.SoulId!=s.SoulId || Mode!=MartialMode.Normal)return;
        if(source.HostSlot<0 || !s.Active.Any(i=>ReferenceEquals(i,s.ItemAt(source.HostSlot)) && i.type==source.SourceWeapon))return;
        source.MartialCharged=true;
        s.MartialCharge=Math.Clamp(s.MartialCharge+MartialTuning.ChargePerBlade,0,MartialTuning.MaxCharge);
        if(s.MartialCharge%100<MartialTuning.ChargePerBlade || s.MartialCharge==MartialTuning.MaxCharge)SyncItem(s);
    }
    public bool TryActivate()
    {
        var s=Held;
        if(!Owner || s==null || !MartialTuning.Enabled || SoulUI.IsEditing(s) || Player.CCed || Mode!=MartialMode.Normal)return false;
        int tier=Math.Clamp(s.MartialTier,1,3),cost=MartialTuning.Cost(tier);
        if(s.MartialCharge<cost){Tell($"剑势不足 · {s.MartialCharge}/{cost}");return false;}
        weapons=s.Active.ToArray();if(weapons.Length==0)return false;
        slots=weapons.Select(w=>s.VisibleSlotIds.FirstOrDefault(slot=>ReferenceEquals(s.ItemAt(slot),w),-1)).ToArray();
        s.MartialCharge-=cost;Tier=tier;FormSoul=s.SoulId;FormAge=cursor=emitted=0;giantIdentity=-1;
        Mode=MartialMode.Formation;Player.itemAnimation=Player.itemTime=0;
        foreach(var q in Main.ActiveProjectiles)
            if(q.owner==Player.whoAmI && q.GetGlobalProjectile<SoulSource>().SoulId==FormSoul && q.ModProjectile is SoulBlade blade)blade.Retire();
        SyncItem(s);Tell(tier==1?"一档 · 背剑刺阵":tier==2?"二档 · 天顶重斩":"三档 · 凝时绝斩");return true;
    }
    public bool BeginAttack(SoulItem soul)
    {
        if(!SuppressNative(soul))return false;
        if(!Owner)return true;
        if(Mode==MartialMode.Giant)
        {
            var giant=FindGiant();
            if(Tier==3 && giant?.ReadyForCut==true)giant.StartTimeCut(Main.MouseWorld);
            return true;
        }
        int count=MartialTuning.Blades(Tier);Vector2 aim=Main.MouseWorld;
        NPC target=MartialTuning.AimTarget(Player,aim);
        for(int i=0;i<count;i++)
        {
            int n=cursor%weapons.Length;var weapon=weapons[n];
            if(slots[n]<0 || !ReferenceEquals(soul.ItemAt(slots[n]),weapon)){Cancel();return true;}
            MartialBlade.Spawn(Player,soul,weapon,slots[n],i,count,target,aim);
            cursor++;emitted++;
        }
        if(Tier>=2 && emitted>=weapons.Length)
        {
            int n=(cursor-1)%weapons.Length;
            giantIdentity=MartialGiant.Spawn(Player,soul,weapons[n],slots[n],Tier,target,aim);
            Mode=MartialMode.Giant;
        }
        return true;
    }
    private MartialGiant FindGiant()=>Main.projectile.FirstOrDefault(q=>q.active && q.owner==Player.whoAmI && q.identity==giantIdentity &&
        q.ModProjectile is MartialGiant && q.GetGlobalProjectile<SoulSource>().SoulId==FormSoul)?.ModProjectile as MartialGiant;
    public override void PostUpdate()
    {
        if(noticeLeft>0)noticeLeft--;else Notice="";
        if(!Owner || Mode==MartialMode.Normal)return;
        var s=Held;
        if(!MartialTuning.Enabled || s?.SoulId!=FormSoul || SoulUI.IsEditing(s) || !s.Active.SequenceEqual(weapons))
        {Cancel();return;}
        FormAge++;
        if(Mode==MartialMode.Formation && Tier==1 && FormAge>=MartialTuning.FirstFormFrames)Complete();
        if(Mode==MartialMode.Giant && FindGiant()==null)Complete();
    }
    public void Complete(){Mode=MartialMode.Normal;Tier=FormAge=0;FormSoul="";weapons=[];slots=[];giantIdentity=-1;}
    public void Cancel()
    {
        string id=FormSoul;
        if(Owner && id.Length>0)
            foreach(var q in Main.projectile.Where(q=>q.active).ToArray())
                if(q.owner==Player.whoAmI && q.GetGlobalProjectile<SoulSource>().SoulId==id && q.ModProjectile is MartialGiant or MartialBlade or MartialWave)q.Kill();
        Complete();
    }
    public override void UpdateDead()=>Cancel();
    public override void OnEnterWorld()=>Complete();
}

// 只冻结战斗范围的敌人，不停止世界时钟、友军或输入；远端从同步巨剑推导范围。
public static class MartialTime
{
    public static bool Frozen(Vector2 point)
    {
        foreach(var q in Main.ActiveProjectiles)
            if(q.ModProjectile is MartialGiant g && g.TimeStopped && q.owner>=0 && q.owner<Main.maxPlayers &&
                Main.player[q.owner].active && !Main.player[q.owner].dead &&
                Main.player[q.owner].HeldItem.ModItem is SoulItem s && s.SoulId==q.GetGlobalProjectile<SoulSource>().SoulId &&
                Vector2.DistanceSquared(point,Main.player[q.owner].Center)<=MartialTuning.FreezeRadius*MartialTuning.FreezeRadius)return true;
        return false;
    }
}

public class MartialFreezeNPC : GlobalNPC
{
    public override bool InstancePerEntity=>true;
    private bool frozen;
    private Vector2 velocity;
    public override bool PreAI(NPC npc)
    {
        bool stop=!npc.friendly && npc.active && MartialTime.Frozen(npc.Center);
        if(stop)
        {
            if(!frozen){velocity=npc.velocity;frozen=true;npc.netUpdate=true;}
            npc.velocity=Vector2.Zero;if(npc.timeLeft<int.MaxValue-1)npc.timeLeft++;return false;
        }
        if(frozen){npc.velocity=velocity;frozen=false;npc.netUpdate=true;}
        return true;
    }
    public override bool CanHitPlayer(NPC npc,Player target,ref int cooldownSlot)=>!(!npc.friendly && MartialTime.Frozen(npc.Center));
}

public class MartialFreezeProjectile : GlobalProjectile
{
    public override bool InstancePerEntity=>true;
    private bool frozen;
    private Vector2 velocity;
    public override bool PreAI(Projectile p)
    {
        if(p.hostile && MartialTime.Frozen(p.Center))
        {if(!frozen){velocity=p.velocity;frozen=true;}p.velocity=Vector2.Zero;if(p.timeLeft<int.MaxValue-1)p.timeLeft++;return false;}
        if(frozen){p.velocity=velocity;frozen=false;}return true;
    }
    public override bool ShouldUpdatePosition(Projectile p)=>!(p.hostile && MartialTime.Frozen(p.Center));
    public override bool CanHitPlayer(Projectile p,Player target)=>!(p.hostile && MartialTime.Frozen(p.Center));
}

public class MartialImpaled : ModBuff
{
    public override string Texture=>"Terraria/Images/Buff_"+BuffID.Bleeding;
    public override void SetStaticDefaults(){Main.debuff[Type]=true;Main.buffNoSave[Type]=true;}
}
public class MartialScar : ModBuff
{
    public override string Texture=>"Terraria/Images/Buff_"+BuffID.ShadowFlame;
    public override void SetStaticDefaults(){Main.debuff[Type]=true;Main.buffNoSave[Type]=true;}
}
public class MartialTarget : GlobalNPC
{
    public override bool InstancePerEntity=>true;
    private readonly Dictionary<(int,string),(float Dps,uint Until)> scars=[];
    public void Scar(NPC npc,Projectile source)
    {
        var key=(source.owner,source.GetGlobalProjectile<SoulSource>().SoulId);
        scars[key]=(Math.Max(2,source.damage*.08f),Main.GameUpdateCount+360);
        npc.AddBuff(ModContent.BuffType<MartialScar>(),360);npc.netUpdate=true;
    }
    public override void UpdateLifeRegen(NPC npc,ref int damage)
    {
        if(WoodCombat.Root(npc)!=npc)return;
        float dps=0;
        if(npc.HasBuff(ModContent.BuffType<MartialImpaled>()))
            foreach(var q in Main.ActiveProjectiles)
                if(q.ModProjectile is MartialBlade blade && blade.PinnedTo(npc))dps+=Math.Max(1,q.damage*.08f);
        if(npc.HasBuff(ModContent.BuffType<MartialScar>()))
            foreach(var key in scars.Keys.ToArray())
            {if(scars[key].Until<Main.GameUpdateCount)scars.Remove(key);else dps+=scars[key].Dps;}
        if(dps<=0)return;
        if(npc.lifeRegen>0)npc.lifeRegen=0;npc.lifeRegen-=(int)Math.Ceiling(dps*2);damage=Math.Max(damage,Math.Max(1,(int)dps/3));
    }
}

public class MartialHUD : ModSystem
{
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int at=layers.FindIndex(l=>l.Name=="Vanilla: Mouse Text");
        layers.Insert(at<0?layers.Count:at,new LegacyGameInterfaceLayer("ZenithReborn: 武技剑势",Draw,InterfaceScaleType.UI));
    }
    private static bool Draw()
    {
        if(Main.gameMenu || Main.hideUI || Main.mapFullscreen || !MartialTuning.Enabled)return true;
        var p=Main.LocalPlayer;var state=p.GetModPlayer<MartialPlayer>();var s=state.Held;
        if(s==null || SoulUI.IsEditing(s))return true;
        float ui=Main.UIScale;var at=new Vector2(28,Main.screenHeight/ui-185);
        Color color=state.Tier==3?new(216,169,255):new(117,224,243);
        string phase=state.Mode==MartialMode.Normal?"就绪":state.Mode==MartialMode.Formation?"背剑刺阵":"天顶巨剑";
        Utils.DrawBorderString(Main.spriteBatch,$"武技试作 · {s.MartialTier}档 · {phase}",at,color,.72f);
        for(int i=0;i<3;i++)
        {
            var rect=new Rectangle((int)at.X+i*88,(int)at.Y+27,80,7);
            Main.spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value,rect,new Rectangle(0,0,1,1),new Color(25,32,50)*.85f);
            rect.Width=(int)(80*Math.Clamp((s.MartialCharge-i*100)/100f,0,1));
            Main.spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value,rect,new Rectangle(0,0,1,1),color*.85f);
        }
        string key=MartialInput.Name(ZenithReborn.ActivateMartial,"G");
        string cycle=MartialInput.Name(ZenithReborn.CycleMartial,"V");
        string keys=state.Notice.Length>0?state.Notice:$"{key}启动/收势 · {cycle}选档 · 剑势 {s.MartialCharge}/300";
        Utils.DrawBorderString(Main.spriteBatch,keys,at+new Vector2(0,41),new Color(196,210,230),.62f);
        return true;
    }
}
