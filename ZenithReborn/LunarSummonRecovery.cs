using System.IO;
using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

// Observe this lunar cycle, not historical downed flags: those survive a completed fight.
public class LunarSummonRecovery : ModSystem
{
    public int SeenPillars { get; private set; }
    public int DefeatedPillars { get; private set; }
    public int PendingFrames { get; private set; }
    public int PendingMaximum { get; private set; } = 3600;
    private int recoveryBackoff;
    public static int PillarBit(int type) => type switch {
        NPCID.LunarTowerSolar => 1, NPCID.LunarTowerVortex => 2,
        NPCID.LunarTowerNebula => 4, NPCID.LunarTowerStardust => 8, _ => 0
    };
    public static bool HasPillar => Main.npc.Any(n=>n.active && n.life>0 && PillarBit(n.type)!=0);
    public static bool HasMoonLord => Main.npc.Any(n=>n.active && n.type is NPCID.MoonLordCore or NPCID.MoonLordHead or NPCID.MoonLordHand);
    public static bool AllHistoricallyDefeated => NPC.downedTowerSolar && NPC.downedTowerVortex && NPC.downedTowerNebula && NPC.downedTowerStardust;
    public static string RequestPath => Path.Combine(Main.SavePath,"Mods","ZenithReborn.moon-recovery.json");
    public override void Load() => On_WorldGen.StartImpendingDoom += RememberCountdown;
    public override void Unload() => On_WorldGen.StartImpendingDoom -= RememberCountdown;
    private void RememberCountdown(On_WorldGen.orig_StartImpendingDoom orig,int frames)
    {
        orig(frames);
        if(Main.netMode==NetmodeID.MultiplayerClient) return;
        PendingFrames=Math.Max(0,NPC.MoonLordCountdown);
        PendingMaximum=Math.Max(1,NPC.MaxMoonLordCountdown);
    }
    private void Reset()
    {
        SeenPillars=DefeatedPillars=PendingFrames=recoveryBackoff=0;PendingMaximum=3600;
    }
    public override void OnWorldLoad() => Reset();
    public override void OnWorldUnload() => Reset();
    public override void SaveWorldData(TagCompound tag)
    {
        tag["lunarSeen"]=SeenPillars;tag["lunarDefeated"]=DefeatedPillars;
        tag["moonPending"]=HasMoonLord ? 0 : Math.Max(PendingFrames,NPC.MoonLordCountdown);
        tag["moonMaximum"]=Math.Max(PendingMaximum,NPC.MaxMoonLordCountdown);
    }
    public override void LoadWorldData(TagCompound tag)
    {
        SeenPillars=tag.GetInt("lunarSeen")&15;DefeatedPillars=tag.GetInt("lunarDefeated")&15;
        PendingFrames=Math.Clamp(tag.GetInt("moonPending"),0,3600);
        PendingMaximum=Math.Clamp(tag.GetInt("moonMaximum"),1,3600);
    }
    public void ObserveSpawn(int type)
    {
        int bit=PillarBit(type);
        if(bit==0 || Main.netMode==NetmodeID.MultiplayerClient) return;
        if(PendingFrames>0 || DefeatedPillars==15 || (DefeatedPillars&bit)!=0) { SeenPillars=DefeatedPillars=PendingFrames=0; }
        SeenPillars|=bit;
    }
    public void ObserveKill(int type)
    {
        int bit=PillarBit(type);
        if(bit!=0 && Main.netMode!=NetmodeID.MultiplayerClient) { SeenPillars|=bit;DefeatedPillars|=bit; }
    }
    private static void ClearStaleTowerFlags()
    {
        NPC.TowerActiveSolar=NPC.TowerActiveVortex=NPC.TowerActiveNebula=NPC.TowerActiveStardust=false;
        NPC.ShieldStrengthTowerSolar=NPC.ShieldStrengthTowerVortex=NPC.ShieldStrengthTowerNebula=NPC.ShieldStrengthTowerStardust=0;
    }
    private bool CanRepair(Player player) => Main.netMode!=NetmodeID.MultiplayerClient && player!=null && player.active && !player.dead &&
        AllHistoricallyDefeated && !HasPillar && !HasMoonLord && NPC.MoonLordCountdown<=0 && PendingFrames<=0;
    public bool TryRepair(Player player)
    {
        if(!CanRepair(player)) return false;
        ClearStaleTowerFlags();
        WorldGen.StartImpendingDoom(3600);
        Mod.Logger.Info("Recovered a requested completed-pillar Moon Lord summon with the normal 3600-frame countdown.");
        return true;
    }
    public override void PostUpdateWorld() => UpdateRecovery();
    public void UpdateRecovery()
    {
        if(Main.netMode==NetmodeID.MultiplayerClient) return;
        if(HasMoonLord)
        {
            SeenPillars=DefeatedPillars=PendingFrames=0;
            return;
        }
        // Loading an active vanilla event can recreate pillar NPCs before this hook.
        if(NPC.LunarApocalypseIsUp)
        {
            SeenPillars=15;
            int active=(NPC.TowerActiveSolar?1:0)|(NPC.TowerActiveVortex?2:0)|(NPC.TowerActiveNebula?4:0)|(NPC.TowerActiveStardust?8:0);
            DefeatedPillars|=15^active;
        }
        if(NPC.MoonLordCountdown>0)
        {
            PendingFrames=NPC.MoonLordCountdown;PendingMaximum=Math.Max(1,NPC.MaxMoonLordCountdown);
            return;
        }
        if(HasPillar) return;
        Player player=Main.player.FirstOrDefault(p=>p.active && !p.dead);
        if(player==null) return;
        if(recoveryBackoff>0) { recoveryBackoff--;return; }
        if(PendingFrames>0)
        {
            int maximum=PendingMaximum;
            WorldGen.StartImpendingDoom(PendingFrames);
            NPC.MaxMoonLordCountdown=PendingMaximum=maximum;
            recoveryBackoff=120;
            return;
        }
        if(SeenPillars==15 && DefeatedPillars==15)
        {
            ClearStaleTowerFlags();WorldGen.StartImpendingDoom(3600);
            Mod.Logger.Info("Recovered last-pillar completion without a vanilla Moon Lord countdown.");
            return;
        }
        ApplyQueuedRepair(player);
    }
    private void ApplyQueuedRepair(Player player)
    {
        string path=RequestPath;
        if(!File.Exists(path) || Main.ActiveWorldFileData==null) return;
        try
        {
            var request=JsonSerializer.Deserialize<MoonRecoveryRequest>(File.ReadAllText(path));
            if(request==null || request.WorldId!=Main.ActiveWorldFileData.UniqueId.ToString("D")) return;
            if(!CanRepair(player)) return;
            File.Move(path,path+".applied",true);
            TryRepair(player);
        }
        catch(Exception error) { Mod.Logger.Warn("Unable to read/apply queued Moon Lord recovery: "+error.Message);recoveryBackoff=600; }
    }
    public static void RequestRepair(Player player)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient)
        { var packet=ModContent.GetInstance<ZenithReborn>().GetPacket();packet.Write((byte)9);packet.Send();return; }
        var system=ModContent.GetInstance<LunarSummonRecovery>();
        bool done=system.TryRepair(player);
        AttachmentStory.Notice(player,done ? "四柱召唤已恢复，月亮领主将在正常倒计时约60秒后降临。" : Status());
    }
    public static string Status()
    {
        if(HasMoonLord) return "月亮领主已经存在，无需再次召唤。";
        if(NPC.MoonLordCountdown>0) return $"月亮领主倒计时：{(NPC.MoonLordCountdown+59)/60}秒。";
        if(HasPillar) return "仍有存活的天界柱，不能跳过四柱。";
        return AllHistoricallyDefeated ? "四柱已有击败记录。若上次召唤卡住，可输入 /zmoon repair 恢复一次正常倒计时。" : "需要先实际击败全部四种天界柱。";
    }
}

public class MoonRecoveryRequest { public string WorldId { get; set; } public string RequestedAt { get; set; } }
public class LunarPillarTracker : GlobalNPC
{
    public override void OnSpawn(NPC npc,Terraria.DataStructures.IEntitySource source) => ModContent.GetInstance<LunarSummonRecovery>().ObserveSpawn(npc.type);
    public override void OnKill(NPC npc) => ModContent.GetInstance<LunarSummonRecovery>().ObserveKill(npc.type);
}
public class MoonRecoveryCommand : ModCommand
{
    public override string Command => "zmoon";
    public override CommandType Type => CommandType.Chat;
    public override string Description => "查看月亮领主召唤状态；/zmoon repair 修复已完成四柱但未启动的召唤";
    public override void Action(CommandCaller caller,string input,string[] args)
    {
        if(args.Length==1 && args[0].Equals("repair",StringComparison.OrdinalIgnoreCase)) LunarSummonRecovery.RequestRepair(caller.Player);
        else caller.Reply(LunarSummonRecovery.Status());
    }
}
