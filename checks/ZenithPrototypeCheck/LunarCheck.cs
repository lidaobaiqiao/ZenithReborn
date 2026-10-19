using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class LunarCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var system=ModContent.GetInstance<LunarSummonRecovery>();
        Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;
        foreach(var n in Main.npc) n.active=false;
        Main.player[0].active=true;Main.player[0].dead=false;Main.player[0].position=new Vector2(1000,1000);
        Main.ActiveWorldFileData=new WorldFileData(Path.Combine(Main.SavePath,"lunar-fixture.wld"),false){UniqueId=Guid.NewGuid()};
        int[] pillars=[NPCID.LunarTowerSolar,NPCID.LunarTowerVortex,NPCID.LunarTowerNebula,NPCID.LunarTowerStardust];
        void Reset()
        {
            system.OnWorldLoad();NPC.MoonLordCountdown=0;NPC.MaxMoonLordCountdown=3600;NPC.LunarApocalypseIsUp=false;
            NPC.TowerActiveSolar=NPC.TowerActiveVortex=NPC.TowerActiveNebula=NPC.TowerActiveStardust=false;
            foreach(var n in Main.npc) n.active=false;
            Main.player[0].active=true;Main.player[0].dead=false;
        }
        void Complete()
        { foreach(int p in pillars) system.ObserveSpawn(p);foreach(int p in pillars) system.ObserveKill(p); }
        Reset();
        WorldGen.UpdateLunarApocalypse();
        check(NPC.MoonLordCountdown==0,"Reproduced vanilla: no event flag means no countdown even with no remaining pillars");
        foreach(int p in pillars) system.ObserveSpawn(p);
        foreach(int p in pillars.Take(3)) system.ObserveKill(p);
        system.UpdateRecovery();check(NPC.MoonLordCountdown==0,"Three pillars cannot trigger recovery");
        system.ObserveKill(pillars[3]);system.UpdateRecovery();
        check(NPC.MoonLordCountdown==3600 && system.PendingFrames==3600,"Four actual pillar kills restore the normal sixty-second countdown without the vanilla event flag");
        NPC.MoonLordCountdown=1200;system.UpdateRecovery();
        check(NPC.MoonLordCountdown==1200 && system.PendingFrames==1200,"Existing vanilla countdown is never reset to sixty seconds");
        var tag=new TagCompound();system.SaveWorldData(tag);Reset();system.LoadWorldData(tag);system.UpdateRecovery();
        check(NPC.MoonLordCountdown==1200 && NPC.MaxMoonLordCountdown==3600,"World reload resumes remaining countdown and original maximum");
        var moon=new NPC();moon.SetDefaults(NPCID.MoonLordCore);moon.active=true;moon.whoAmI=20;Main.npc[20]=moon;
        system.UpdateRecovery();check(system.PendingFrames==0 && system.DefeatedPillars==0,"Observed Moon Lord consumes recovery so the boss is not respawned after death");
        moon.active=false;NPC.MoonLordCountdown=0;system.UpdateRecovery();
        check(NPC.MoonLordCountdown==0,"A completed fight does not automatically repeat");
        Reset();Complete();var live=new NPC();live.SetDefaults(pillars[0]);live.active=true;live.life=100;Main.npc[21]=live;
        system.UpdateRecovery();check(NPC.MoonLordCountdown==0,"Living pillar blocks recovery even if history says all four died");
        live.active=false;Main.player[0].dead=true;system.UpdateRecovery();check(NPC.MoonLordCountdown==0,"Recovery waits for an alive active player");
        Main.player[0].dead=false;system.UpdateRecovery();check(NPC.MoonLordCountdown==3600,"Deferred recovery resumes when a player is alive");
        Reset();Complete();Main.netMode=NetmodeID.MultiplayerClient;system.UpdateRecovery();
        check(NPC.MoonLordCountdown==0 && !system.TryRepair(Main.player[0]),"Clients cannot start an authoritative boss countdown locally");Main.netMode=NetmodeID.SinglePlayer;
        Reset();NPC.downedTowerSolar=NPC.downedTowerVortex=NPC.downedTowerNebula=NPC.downedTowerStardust=true;
        system.UpdateRecovery();check(NPC.MoonLordCountdown==0,"Historical pillar flags alone never auto-summon on entering an old world");
        NPC.downedTowerStardust=false;check(!system.TryRepair(Main.player[0]),"Manual repair cannot bypass an uncompleted pillar history");
        NPC.downedTowerStardust=true;check(system.TryRepair(Main.player[0]) && NPC.MoonLordCountdown==3600,"Explicit repair for an already completed old world starts one normal countdown");
        check(!system.TryRepair(Main.player[0]),"Repeated repair cannot duplicate or restart the pending summon");
        Reset();
        var sigil=typeof(WorldGen).GetMethod("StartImpendingDoom",BindingFlags.Public|BindingFlags.Static);
        sigil.Invoke(null,[720]);
        check(system.PendingFrames==720,"Native celestial-sigil countdown is captured by the same persistence hook");
        system.ObserveSpawn(pillars[0]);check(system.PendingFrames==0,"A newly spawned pillar cycle does not inherit an old pending summon");
        Reset();
        check(Main.SavePath.Contains("wood-check-profile"),"Queued-repair fixture is confined to the isolated profile");
        string queue=LunarSummonRecovery.RequestPath;
        try
        {
            File.WriteAllText(queue,JsonSerializer.Serialize(new MoonRecoveryRequest{WorldId=Guid.NewGuid().ToString("D"),RequestedAt="fixture"}));
            system.UpdateRecovery();check(NPC.MoonLordCountdown==0 && File.Exists(queue),"Queued recovery never applies to a different world GUID");
            File.WriteAllText(queue,JsonSerializer.Serialize(new MoonRecoveryRequest{WorldId=Main.ActiveWorldFileData.UniqueId.ToString("D"),RequestedAt="fixture"}));
            system.UpdateRecovery();check(NPC.MoonLordCountdown==3600 && !File.Exists(queue) && File.Exists(queue+".applied"),"Matching-world recovery is applied once and archived");
        }
        finally
        { if(File.Exists(queue)) File.Delete(queue);if(File.Exists(queue+".applied")) File.Delete(queue+".applied"); }
        // Exercise the game's actual final-countdown spawn branch, not a replacement NPC factory.
        NPC.MoonLordCountdown=1;
        typeof(Main).GetMethod("UpdateTime",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        check(Main.npc.Any(n=>n.active && n.type==NPCID.MoonLordCore),"Vanilla countdown expiration actually creates Moon Lord core NPC");
        system.UpdateRecovery();check(system.PendingFrames==0,"Successful vanilla spawn clears persisted pending state");
        Reset();
    }
}
