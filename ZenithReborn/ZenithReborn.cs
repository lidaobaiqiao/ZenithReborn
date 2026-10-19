using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public class ZenithReborn : Mod
{
    internal static ModKeybind OpenSoul;
    public override void Load()
    {
        if (!Main.dedServ) OpenSoul = KeybindLoader.RegisterKeybind(this, "OpenSoul", "P");
    }
    public override void Unload() => OpenSoul = null;
    public override void HandlePacket(BinaryReader reader,int whoAmI)
    {
        byte action=reader.ReadByte();
        if (action == 3) { WoodCombat.ReceiveHit(reader, whoAmI); return; }
        if (action == 4)
        {
            int player = reader.ReadByte();
            if (Main.netMode == NetmodeID.MultiplayerClient && player < Main.maxPlayers)
                Main.player[player].GetModPlayer<WoodPlayer>().ReceiveGrowth(reader);
            return;
        }
        if (action == 6)
        {
            int index=reader.ReadInt16();byte line=reader.ReadByte(),state=reader.ReadByte();
            if (Main.netMode==NetmodeID.MultiplayerClient && index>=0 && index<Main.maxNPCs &&
                Main.npc[index].ModNPC is GuardianSlimeKing king && (int)Main.npc[index].ai[0]==Main.myPlayer)
            {
                Main.npc[index].ai[1]=Math.Clamp((int)line,0,3);Main.npc[index].ai[2]=Math.Clamp((int)state,0,2);
                if(Main.LocalPlayer.talkNPC==index) Main.npcChatText=king.GetChat();
            }
            return;
        }
        if (action == 8) { SoulBossCredit.ReceiveCredit(reader,whoAmI); return; }
        if(Main.netMode!=NetmodeID.Server || whoAmI<0 || whoAmI>=Main.maxPlayers) return;
        if(action==1)
        { string id=reader.ReadString();if(id.Length==32) AttachmentStory.Start(Main.player[whoAmI],id); }
        else if(action==2) AttachmentStory.Advance(Main.player[whoAmI],reader.ReadInt16());
        else if(action==5)
        { string id=reader.ReadString();if(id.Length==32) SoulProgress.RecoverCore(Main.player[whoAmI],id); }
        else if(action==7)
        { string id=reader.ReadString();if(id.Length==32) AttachmentStory.Start(Main.player[whoAmI],id,true); }
        else if(action==9) LunarSummonRecovery.RequestRepair(Main.player[whoAmI]);
    }
}
