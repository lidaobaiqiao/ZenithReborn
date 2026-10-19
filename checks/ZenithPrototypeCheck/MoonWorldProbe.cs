using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;

namespace ZenithPrototypeCheck;

public class MoonWorldProbe : ModSystem
{
    public override void PostAddRecipes()
    {
        string input=Environment.GetEnvironmentVariable("ZENITH_MOON_WORLD"),output=Environment.GetEnvironmentVariable("ZENITH_MOON_REPORT");
        if(string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output)) return;
        try
        {
            Main.ActiveWorldFileData=new WorldFileData(input,false);
            using var reader=new BinaryReader(File.OpenRead(input));
            var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            var format=typeof(WorldFile).GetMethod("LoadFileFormatHeader",flags);
            object[] args=[reader,null,null];
            bool valid=(bool)format.Invoke(null,args);
            if(!valid) throw new Exception("Invalid world header");
            int[] positions=(int[])args[2];reader.BaseStream.Position=positions[0];
            typeof(WorldFile).GetMethod("LoadHeader",flags).Invoke(null,[reader]);
            File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new {
                input,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))),Main.worldName,
                worldGuid=Main.ActiveWorldFileData.UniqueId.ToString("D"),
                Main.hardMode,NPC.downedGolemBoss,NPC.downedAncientCultist,NPC.downedMoonlord,NPC.LunarApocalypseIsUp,
                NPC.TowerActiveSolar,NPC.TowerActiveVortex,NPC.TowerActiveNebula,NPC.TowerActiveStardust,
                NPC.downedTowerSolar,NPC.downedTowerVortex,NPC.downedTowerNebula,NPC.downedTowerStardust,
                NPC.MoonLordCountdown,NPC.MaxMoonLordCountdown,headerEnd=reader.BaseStream.Position,
                nextSection=positions[1]
            },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            Environment.Exit(0);
        }
        catch(Exception error){File.WriteAllText(output,"FAIL: "+error);Environment.Exit(1);}
    }
}
