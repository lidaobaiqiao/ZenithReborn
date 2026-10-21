using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

// Uses vanilla's real world switches. No extra NPC damage/life multiplier.
public class SoulDifficulty : ModSystem
{
    public bool Applied { get; private set; }
    private int previousMode;
    private bool previousWorthy;
    public static bool Legendary => Main.GameMode==2 && Main.getGoodWorld;
    public static SoulDifficulty Instance => ModContent.GetInstance<SoulDifficulty>();
    public static string BlockReason()
    {
        if(Main.netMode!=NetmodeID.SinglePlayer)return "联机世界暂不提供此切换，请由服务器管理难度。";
        if(Main.gameMenu || Main.ActiveWorldFileData==null)return "进入世界后才能切换。";
        if(Main.GameMode==3)return "旅途世界保持原有难度滑块，不转换世界类型。";
        if(Main.npc.Any(n=>n.active && n.boss) || NPC.MoonLordCountdown>0)return "Boss战或月总降临期间不能切换。";
        if(Legendary && !Instance.Applied)return "这个世界本来就是传奇难度。";
        return "";
    }
    public static bool Toggle(out string message)
    {
        message=BlockReason();if(message.Length>0)return false;
        var state=Instance;
        if(!state.Applied)
        {
            state.previousMode=Main.GameMode;state.previousWorthy=Main.getGoodWorld;
            SetNative(2,true);state.Applied=true;
            message="已开启原版传奇：大师＋For the worthy。再次点击恢复原难度。";
        }
        else
        {
            SetNative(state.previousMode,state.previousWorthy);state.Applied=false;
            message="已恢复开启前的世界难度。";
        }
        return true;
    }
    private static void SetNative(int mode,bool worthy)
    {
        Main.GameMode=mode;Main.getGoodWorld=worthy;
        Main.ActiveWorldFileData.ForTheWorthy=worthy;
    }
    public override void OnWorldLoad(){Applied=false;previousMode=0;previousWorthy=false;}
    public override void OnWorldUnload(){Applied=false;previousMode=0;previousWorthy=false;}
    public override void SaveWorldData(TagCompound tag)
    {
        if(!Applied)return;
        tag["legendaryApplied"]=true;tag["previousMode"]=previousMode;tag["previousWorthy"]=previousWorthy;
    }
    public override void LoadWorldData(TagCompound tag)
    {
        Applied=tag.GetBool("legendaryApplied");previousMode=Math.Clamp(tag.GetInt("previousMode"),0,2);
        previousWorthy=tag.GetBool("previousWorthy");
        // Native world data owns the active flags; never force difficulty while
        // loading or resurrect stale choices if another tool has changed them.
        if(!Legendary)Applied=false;
    }
}
