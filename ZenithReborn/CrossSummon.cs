using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// Vanilla Worm Food / Bloody Spine are hard-locked to their evil biomes inside
// Player.ItemCheck, so these are separate summon items with the same look but no
// biome requirement. UseItem spawns the boss directly instead of entering the
// vanilla type == 70 / 1331 branches.
public abstract class CrossSummonItem : ModItem
{
    public abstract int BossType { get; }
    public override void SetDefaults()
    {
        Item.width = Item.height = 24;
        Item.maxStack = 20;
        Item.consumable = true;
        Item.useTime = Item.useAnimation = 45;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.UseSound = SoundID.Item15;
        Item.rare = ItemRarityID.Blue;
        Item.value = 0;
    }
    public override bool CanUseItem(Player player) => !NPC.AnyNPCs(BossType);
    public override bool? UseItem(Player player)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            NPC.SpawnOnPlayer(player.whoAmI, BossType);
        else
            NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, -1, -1, null,
                player.whoAmI, BossType);
        return true;
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips) =>
        tooltips.Add(new(Mod, "CrossBiomeUse", "可在任何环境召唤；不需要对应邪恶生物群落"));
}

public class CrimsonSigil : CrossSummonItem
{
    public override int BossType => NPCID.BrainofCthulhu;
    public override string Texture => "Terraria/Images/Item_" + ItemID.BloodySpine;
}

public class CorruptionLure : CrossSummonItem
{
    public override int BossType => NPCID.EaterofWorldsHead;
    public override string Texture => "Terraria/Images/Item_" + ItemID.WormFood;
}

// Only the world's native evil boss drops the other side's summon, so the two
// bosses cannot farm each other in a loop.
public class CrossSummonDrops : GlobalNPC
{
    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        if (!WorldGen.crimson && npc.type == NPCID.EaterofWorldsHead)
            Item.NewItem(npc.GetSource_Loot(), npc.Hitbox, ModContent.ItemType<CrimsonSigil>());
        else if (WorldGen.crimson && npc.type == NPCID.BrainofCthulhu)
            Item.NewItem(npc.GetSource_Loot(), npc.Hitbox, ModContent.ItemType<CorruptionLure>());
    }
}
