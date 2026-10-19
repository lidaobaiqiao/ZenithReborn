using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class ProgressionCheck
{
    internal static void Run(Action<bool,string> check)
    {
        Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.dayTime=true;
        Main.maxTilesX=200;Main.maxTilesY=200;Main.worldSurface=80;
        Main.tile=(Tilemap)Activator.CreateInstance(typeof(Tilemap),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,
            null,new object[]{(ushort)200,(ushort)200},null);
        foreach(var npc in Main.npc) npc.active=false;
        var player=new Player{active=true,whoAmI=0,position=new Vector2(1000,900),ZoneOverworldHeight=true};
        Main.player[0]=player;player.ResetEffects();player.ZoneOverworldHeight=true;
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        var fresh=(SoulItem)new Item(soul.Type).ModItem;
        player.inventory[0]=soul.Item;player.inventory[1]=new Item(ModContent.ItemType<GuardianGel>());player.inventory[2]=fresh.Item;player.selectedItem=1;
        var token=(GuardianGel)player.inventory[1].ModItem;
        check(player.GetModPlayer<SoulPlayer>().AddStartingItems(false).Any(i=>i.ModItem is GuardianGel),"New players receive the friendly king token");
        check(!player.GetModPlayer<SoulPlayer>().AddStartingItems(true).Any(),"Mediumcore death does not duplicate starting gifts");
        check(!AttachmentStory.CanStart(player,soul) && token.CanUseItem(player),"Token opens friendly encounter without a nearby slime");
        Main.dayTime=false;check(!token.CanUseItem(player),"Friendly token explains daytime restriction");Main.dayTime=true;
        bool downed=NPC.downedSlimeKing;
        check(AttachmentStory.Start(player,soul.SoulId,true),"Actual NPC.NewNPC spawns the friendly king in isolated tilemap");
        NPC king=AttachmentStory.FindKing(player,soul.SoulId);var guardian=(GuardianSlimeKing)king.ModNPC;
        check(king.friendly && !king.boss && king.damage==0 && king.dontTakeDamage && guardian.CanChat(),"Friendly king is harmless, unkillable and directly talkable");
        check(!token.CanUseItem(player) && !AttachmentStory.Start(player,soul.SoulId,true),"Repeated token use cannot create duplicate kings");
        string shop="";
        for(int i=0;i<3;i++)
        {
            guardian.OnChatButtonClicked(true,ref shop);
            check(!soul.Attached && king.ai[1]==i+1,"Direct NPC chat advances one story line: "+i);
        }
        guardian.OnChatButtonClicked(true,ref shop);
        check(soul.Attached && soul.KingMemory && soul.Capacity==1 && king.ai[2]==1,"Direct NPC chat grants Attachment and first ordinary slot");
        check(NPC.downedSlimeKing==downed && soul.Kills==0 && soul.Cores==0,"Friendly blessing does not fake vanilla boss defeat or progression");
        check(!AttachmentStory.Advance(player,king.whoAmI),"Blessing cannot be granted twice");
        for(int i=0;i<601;i++) guardian.AI();
        check(!king.active,"King fades away safely after the blessing");
        check(token.CanUseItem(player) && AttachmentStory.Start(player,soul.SoulId,true),"Completed player can recall the friendly guide");
        king=AttachmentStory.FindKing(player,soul.SoulId);guardian=(GuardianSlimeKing)king.ModNPC;
        string button="",button2="";guardian.SetChatButtons(ref button,ref button2);
        check(king.ai[2]==2 && button.Contains("成长路线") && button2.Contains("寄宿"),"Recalled king offers progression and hosting instead of a second blessing");
        player.selectedItem=0;
        check(SoulProgress.Capacity(SoulProgress.Moon,63)==1,"Late boss alone cannot bypass earlier slot gates");
        check(!SoulProgress.AcceptCore(fresh,0) && !SoulProgress.RecoverCore(player,fresh.SoulId),"Fresh unblessed soul cannot absorb or recover a core");
        void Defeat(int type)
        {
            var npc=new NPC();npc.SetDefaults(type);npc.whoAmI=80;npc.active=true;Main.npc[80]=npc;
            var projectile=new Projectile();projectile.SetDefaults(ModContent.ProjectileType<SoulBlade>());projectile.owner=0;
            projectile.GetGlobalProjectile<SoulSource>().SoulId=soul.SoulId;
            var hit=npc.GetIncomingStrikeModifiers(DamageClass.Melee,1);
            var credit=new SoulBossCredit();credit.ModifyHitByProjectile(npc,projectile,ref hit);
            npc.active=false;credit.OnKill(npc);
        }
        void UseCore(int index,int capacity)
        {
            var item=new Item(SoulCore.TypeFor(index));var core=(SoulCore)item.ModItem;
            check(core.Target(player)==soul && core.CanUseItem(player),"Correct defeated soul selected for core "+index);
            core.UseItem(player);
            check(soul.Capacity==capacity && fresh.Cores==0,"Core unlocks intended slots without modifying another soul: "+index);
        }
        Defeat(NPCID.EyeofCthulhu);
        check(soul.Capacity==1 && SoulProgress.Next(soul).Contains("初光魂芯") && SoulProgress.RecoverableCore(soul)==0,"Eye kill offers exact missing core and recovery");
        UseCore(0,2);
        check(SoulProgress.Next(soul).Contains("邪恶Boss"),"Next action advances to evil boss for slot three");
        Defeat(NPCID.BrainofCthulhu);check(soul.Capacity==3,"Evil boss unlocks third slot without a redundant core");
        Defeat(NPCID.SkeletronHead);check(soul.Capacity==4,"Skeletron unlocks fourth slot with Dawn core reused");
        Defeat(NPCID.WallofFlesh);check(soul.Capacity==4,"Wall kill waits for Flesh core");UseCore(1,5);
        Defeat(NPCID.TheDestroyer);Defeat(NPCID.Retinazer);Defeat(NPCID.SkeletronPrime);
        check(!SoulProgress.CanAcceptCore(soul,2) && soul.Capacity==5,"One Twin is not enough for machine core or sixth slot");
        Defeat(NPCID.Spazmatism);UseCore(2,6);
        Defeat(NPCID.Plantera);UseCore(3,7);
        Defeat(NPCID.Golem);UseCore(4,8);
        Defeat(NPCID.CultistBoss);check(soul.Capacity==9,"Cultist unlocks ninth slot with Temple core reused");
        Defeat(NPCID.MoonLordCore);UseCore(5,10);
        check(soul.Kills==SoulProgress.All && soul.Cores==63 && SoulProgress.Next(soul).Contains("十把天顶材料剑"),"All slot gates lead to the actual permanent-awakening objective");
        check(!SoulProgress.RecoverCore(player,soul.SoulId) && !SoulProgress.AcceptCore(soul,5),"Completed core recovery cannot be repeated");
        for(int slot=0;slot<17;slot++) check(!string.IsNullOrWhiteSpace(SoulProgress.SlotHint(soul,slot)),"Every ordinary/support/bonus slot has a usable condition hint: "+slot);
        var recovery=(SoulItem)new Item(soul.Type).ModItem;recovery.Attached=true;recovery.Kills=SoulProgress.Eye;
        player.inventory[0]=recovery.Item;player.inventory[1]=new Item(SoulCore.TypeFor(0),2);
        check(SoulProgress.RecoverCore(player,recovery.SoulId) && recovery.Capacity==2 && player.inventory[1].stack==1,"Recovery consumes an existing core when available");
        check(!SoulProgress.RecoverCore(player,recovery.SoulId),"Recovery button cannot spend another core after unlock");
        var lost=(SoulItem)new Item(soul.Type).ModItem;lost.Attached=true;lost.Kills=SoulProgress.Eye;
        player.inventory[0]=lost.Item;player.inventory[1]=new Item();
        check(SoulProgress.RecoverCore(player,lost.SoulId) && lost.Capacity==2,"Lost core can be reconstructed from this soul's earned victory");
        check(!SoulProgress.RecoverCore(player,Guid.NewGuid().ToString("N")),"Cannot recover for an unowned soul identity");
        foreach(int count in new[]{1,2,4,6,8})
        {
            var wooden=(SoulItem)new Item(soul.Type).ModItem;wooden.Attached=true;wooden.Kills=SoulProgress.All;wooden.Cores=63;
            for(int i=0;i<count;i++) wooden.Hosts[i]=new Item(WoodTypes.Types[i]);
            int expected=new[]{1,2,4,6,8}.Count(n=>count>=n);
            check(wooden.SupportCapacity==expected,"Each real wood threshold unlocks the corresponding support slot: "+count);
        }
        Main.netMode=NetmodeID.SinglePlayer;
    }
}
