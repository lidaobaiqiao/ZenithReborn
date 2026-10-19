using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ZenithReborn;

public static class AttachmentStory
{
    public static readonly string[] Lines = ["我们还记得你。", "你曾替我们挡住那些……很可怕的东西。",
        "我们留不住你的剑身，只能护住这一点光。", "这一次，让我们帮你站稳。"];
    public static SoulItem CallingSoul(Player player)
    {
        if (SoulUI.EditingSoul is SoulItem editing && player.inventory.Any(i=>ReferenceEquals(i.ModItem,editing))) return editing;
        return player.HeldItem.ModItem as SoulItem ?? player.inventory.Select(i=>i.ModItem).OfType<SoulItem>()
            .OrderBy(s=>s.KingMemory).FirstOrDefault();
    }
    private static readonly HashSet<int> Slimes = [NPCID.BlueSlime,NPCID.GreenSlime,NPCID.RedSlime,
        NPCID.PurpleSlime,NPCID.YellowSlime,NPCID.BlackSlime,NPCID.IceSlime,NPCID.SandSlime,NPCID.JungleSlime];
    public static bool IsOrdinarySlime(NPC npc) => Slimes.Contains(npc.type);
    public static SoulItem FindSoul(Player player, string id) => player.inventory.Select(i => i.ModItem)
        .OfType<SoulItem>().FirstOrDefault(s => s.SoulId == id);
    public static NPC FindKing(Player player, string id) => Main.npc.FirstOrDefault(n => n.active &&
        n.ModNPC is GuardianSlimeKing king && king.SoulId == id && (int)n.ai[0] == player.whoAmI);
    public static string Requirement(Player player, SoulItem soul)
    {
        if (soul.KingMemory) return "已经记住这份善意。「附着」祝福永久保留。";
        if (!Main.dayTime) return "回应条件：白天。";
        if (!player.ZoneOverworldHeight) return "回应条件：在地表。";
        if (!Main.npc.Any(n => n.active && IsOrdinarySlime(n) && Vector2.Distance(n.Center,player.Center) <= 600))
            return "附近需要普通史莱姆；也可使用凝胶信物直接呼唤。";
        return "条件已满足：史莱姆正在回应凝胶中的残魂。";
    }
    public static bool CanStart(Player player, SoulItem soul, bool token = false) => Main.dayTime &&
        player.ZoneOverworldHeight && (token && player.HeldItem.ModItem is GuardianGel || !soul.KingMemory &&
        Main.npc.Any(n => n.active && IsOrdinarySlime(n) && Vector2.Distance(n.Center,player.Center) <= 600));
    public static void RequestStart(SoulItem soul, bool token = false)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        { var p=ModContent.GetInstance<ZenithReborn>().GetPacket();p.Write((byte)(token ? 7 : 1));p.Write(soul.SoulId);p.Send(); }
        else Start(Main.LocalPlayer,soul.SoulId, token);
    }
    public static bool Start(Player player, string id, bool token = false)
    {
        var soul=FindSoul(player,id);
        if (soul==null || !player.active || player.dead || FindKing(player,id)!=null) return false;
        if (!CanStart(player,soul,token)) { Notice(player,Requirement(player,soul));return false; }
        int spawnX = (int)player.Center.X + 120;
        foreach (int offset in new[]{120,-120,180,-180,60,-60,0})
            if (!Collision.SolidCollision(new Vector2(player.Center.X + offset - 49, player.Bottom.Y - 92), 98, 92))
            { spawnX = (int)player.Center.X + offset; break; }
        int index=NPC.NewNPC(player.GetSource_Misc("ZenithAttachment"),spawnX,
            (int)player.Bottom.Y,ModContent.NPCType<GuardianSlimeKing>(),ai0:player.whoAmI,ai2:soul.KingMemory ? 2 : 0);
        if (index >= Main.maxNPCs) return false;
        var npc=Main.npc[index];((GuardianSlimeKing)npc.ModNPC).SoulId=id;
        npc.netUpdate=true;
        Notice(player,"友善史莱姆王已到来：右键点击它对话，或按P听取回应；它不会攻击，也不需要击杀。");
        return true;
    }
    public static void RequestAdvance(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        { var p=ModContent.GetInstance<ZenithReborn>().GetPacket();p.Write((byte)2);p.Write((short)npc.whoAmI);p.Send(); }
        else Advance(Main.LocalPlayer,npc.whoAmI);
    }
    public static bool Advance(Player player,int index)
    {
        if(index<0 || index>=Main.maxNPCs || !player.active || player.dead) return false;
        NPC npc=Main.npc[index];
        if(!npc.active || npc.ModNPC is not GuardianSlimeKing king || (int)npc.ai[0]!=player.whoAmI ||
            Vector2.Distance(npc.Center,player.Center)>900 || npc.ai[2]!=0) return false;
        var soul=FindSoul(player,king.SoulId);
        if(soul==null || soul.KingMemory) return false;
        if(npc.ai[1]<3) npc.ai[1]++;
        else
        {
            soul.Attached=true;soul.KingMemory=true;soul.RefreshStats();
            npc.ai[2]=1;
            Notice(player,"获得首个祝福「附着」！第一寄宿槽已稳定，放入武器便可御剑。");
            for(int slot=0;slot<58;slot++)
                if(ReferenceEquals(player.inventory[slot].ModItem,soul) && Main.netMode==NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,player.whoAmI,slot);
            if(Main.netMode!=NetmodeID.Server) SoundEngine.PlaySound(SoundID.Item4,player.Center);
        }
        npc.netUpdate=true;
        if (Main.netMode == NetmodeID.Server)
        {
            var packet = ModContent.GetInstance<ZenithReborn>().GetPacket();
            packet.Write((byte)6); packet.Write((short)index); packet.Write((byte)npc.ai[1]); packet.Write((byte)npc.ai[2]); packet.Send(player.whoAmI);
        }
        return true;
    }
    public static void Notice(Player player,string text)
    {
        if(Main.netMode==NetmodeID.Server) ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(text),Color.LightBlue,player.whoAmI);
        else Main.NewText(text,160,220,255);
    }
}

public class GuardianSlimeKing : ModNPC
{
    public string SoulId="";
    public override string Texture => "Terraria/Images/NPC_"+NPCID.KingSlime;
    public override void SetStaticDefaults() => Main.npcFrameCount[Type]=6;
    public override void SetDefaults()
    {
        NPC.width=98;NPC.height=92;NPC.lifeMax=500;NPC.damage=0;NPC.defense=0;
        NPC.friendly=true;NPC.dontTakeDamage=true;NPC.lavaImmune=true;
        NPC.knockBackResist=0;NPC.aiStyle=-1;NPC.scale=1.25f;NPC.netAlways=true;NPC.alpha=220;
    }
    public override void SendExtraAI(BinaryWriter writer)=>writer.Write(SoulId);
    public override void ReceiveExtraAI(BinaryReader reader)=>SoulId=reader.ReadString();
    public override bool CanHitPlayer(Player target,ref int cooldownSlot)=>false;
    public override bool CheckActive() => false;
    public override bool CanChat() => true;
    public override string GetChat()
    {
        if ((int)NPC.ai[0] != Main.myPlayer) return "我正在守护另一位旅人的剑魂。带上你的残魂，用凝胶信物呼唤我吧。";
        var soul = AttachmentStory.FindSoul(Main.LocalPlayer, SoulId);
        if (soul == null) return "把天顶残魂带在背包里，再来与我相见。";
        if (soul.KingMemory || NPC.ai[2] > 0) return "「附着」会一直陪着你。下一步：" + SoulProgress.Next(soul);
        return AttachmentStory.Lines[Math.Clamp((int)NPC.ai[1],0,3)];
    }
    public override void SetChatButtons(ref string button, ref string button2)
    {
        if ((int)NPC.ai[0] != Main.myPlayer) { button = "告辞"; button2 = ""; return; }
        button = NPC.ai[2] > 0 ? "查看成长路线" : NPC.ai[1] < 3 ? "继续聆听" : "接受祝福：附着";
        button2 = "打开寄宿界面";
    }
    public override void OnChatButtonClicked(bool firstButton, ref string shopName)
    {
        if ((int)NPC.ai[0] != Main.myPlayer) { Main.LocalPlayer.SetTalkNPC(-1); return; }
        SoulItem soul = AttachmentStory.FindSoul(Main.LocalPlayer,SoulId);
        if (soul == null) return;
        if (firstButton && NPC.ai[2] == 0)
        {
            AttachmentStory.RequestAdvance(NPC);
            if (Main.netMode != NetmodeID.MultiplayerClient) Main.npcChatText = GetChat();
            return;
        }
        Main.LocalPlayer.SetTalkNPC(-1); Main.npcChatText = "";
        SoulUI.Open(soul, firstButton);
    }
    public override void FindFrame(int frameHeight)
    { NPC.frameCounter++;NPC.frame.Y=((int)(NPC.frameCounter/10)%6)*frameHeight; }
    public override void AI()
    {
        int owner=(int)NPC.ai[0];
        if(owner<0 || owner>=Main.maxPlayers) { NPC.active=false;return; }
        Player player=Main.player[owner];
        bool leave=!player.active || player.dead || AttachmentStory.FindSoul(player,SoulId)==null || Vector2.Distance(NPC.Center,player.Center)>1800;
        NPC.localAI[0]++;
        NPC.alpha=Math.Max(0,NPC.alpha-8);
        if(NPC.ai[2]==1)
        {
            NPC.ai[3]++;leave|=NPC.ai[3]>600;
            if (NPC.ai[3] > 480) NPC.alpha=(int)Math.Min(255,(NPC.ai[3]-480)*2.125f);
        }
        else if (NPC.ai[2] == 2) { NPC.ai[3]++; leave |= NPC.ai[3] > 3600; }
        if(leave && Main.netMode!=NetmodeID.MultiplayerClient)
        {
            NPC.active=false;NPC.netUpdate=true;
            if(Main.netMode==NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC,-1,-1,null,NPC.whoAmI);
            return;
        }
        NPC.timeLeft=120;
        float dx=player.Center.X-NPC.Center.X;
        NPC.velocity.X=Math.Abs(dx)>260 ? MathHelper.Lerp(NPC.velocity.X,Math.Sign(dx)*2.5f,.06f) : NPC.velocity.X*.85f;
        NPC.direction=NPC.spriteDirection=dx>=0 ? 1 : -1;
        if(NPC.velocity.Y==0 && NPC.localAI[0]%90==0) NPC.velocity.Y=-3.5f;
        if(!Main.dedServ) Lighting.AddLight(NPC.Center,.12f,.24f,.4f);
    }
}

public class GuardianGel : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.SlimeCrown;
    public override void SetDefaults()
    {
        Item.width=Item.height=28;Item.maxStack=1;Item.consumable=false;
        Item.useStyle=ItemUseStyleID.HoldUp;Item.useTime=Item.useAnimation=30;
        Item.rare=ItemRarityID.Blue;Item.UseSound=SoundID.Item4;
    }
    public override bool CanUseItem(Player player)
    {
        SoulItem soul=AttachmentStory.CallingSoul(player);
        return soul!=null && player.active && !player.dead && AttachmentStory.CanStart(player,soul,true) && AttachmentStory.FindKing(player,soul.SoulId)==null;
    }
    public override bool? UseItem(Player player)
    {
        if (player.whoAmI==Main.myPlayer && AttachmentStory.CallingSoul(player) is SoulItem soul) AttachmentStory.RequestStart(soul,true);
        return true;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Gel,10).AddIngredient(ItemID.FallenStar).AddTile(TileID.WorkBenches).Register();
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        tooltips.Add(new(Mod,"FriendlyCall","不消耗：白天地表，携带天顶残魂使用；无需附近史莱姆"));
        tooltips.Add(new(Mod,"FriendlyTalk","召来友善史莱姆王，右键与它对话，接受「附着」解锁第一普通剑位"));
        tooltips.Add(new(Mod,"FriendlyReturn","祝福完成后仍可呼唤它查看成长路线；不会召唤敌对Boss"));
    }
}

public class SlimeAudienceProtection : GlobalNPC
{
    public override bool CanHitPlayer(NPC npc,Player target,ref int cooldownSlot)
    {
        if(!AttachmentStory.IsOrdinarySlime(npc)) return true;
        return !Main.npc.Any(n=>n.active && n.ModNPC is GuardianSlimeKing &&
            (int)n.ai[0]==target.whoAmI && Vector2.Distance(n.Center,target.Center)<700);
    }
}
