using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

// Progress belongs to this soul, not to the world's pre-existing downed flags.
public static partial class SoulProgress
{
    public const int Eye = 1, Evil = 2, Skeleton = 4, Flesh = 8;
    public const int Destroyer = 16, Retinazer = 32, Spazmatism = 64, Prime = 128;
    public const int Flower = 256, Temple = 512, Cultist = 1024, Moon = 2048;
    public const int All = 4095;
    public static readonly int[] ZenithWeapons = [ItemID.CopperShortsword, ItemID.EnchantedSword,
        ItemID.Starfury, ItemID.BeeKeeper, ItemID.Seedler, ItemID.TheHorsemansBlade,
        ItemID.InfluxWaver, ItemID.TerraBlade, ItemID.StarWrath, ItemID.Meowmere];
    public static readonly string[] CoreNames = ["初光", "血肉", "机魂", "生息", "神庙", "天界"];
    public static bool Has(int mask, int bits) => (mask & bits) == bits;
    public static int Mechs(int mask) => (Has(mask, Destroyer) ? 1 : 0) +
        (Has(mask, Retinazer | Spazmatism) ? 1 : 0) + (Has(mask, Prime) ? 1 : 0);
    public static int Milestones(int mask) => new[] { Eye, Evil, Skeleton, Flesh, Flower, Temple, Cultist, Moon }
        .Count(bit => Has(mask, bit)) + (Mechs(mask) == 3 ? 1 : 0);
    public static int Capacity(int kills, int cores)
    {
        int capacity = 1;
        foreach (var gate in SlotGates)
        {
            if (!Has(kills, gate.Kills) || !Has(cores, gate.Cores)) break;
            capacity++;
        }
        return capacity;
    }
    public static int BossBit(int type) => type switch
    {
        NPCID.EyeofCthulhu => Eye,
        NPCID.BrainofCthulhu or NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail => Evil,
        NPCID.SkeletronHead => Skeleton, NPCID.WallofFlesh => Flesh,
        NPCID.TheDestroyer or NPCID.TheDestroyerBody or NPCID.TheDestroyerTail => Destroyer,
        NPCID.Retinazer => Retinazer, NPCID.Spazmatism => Spazmatism, NPCID.SkeletronPrime => Prime,
        NPCID.Plantera => Flower, NPCID.Golem => Temple, NPCID.CultistBoss => Cultist,
        NPCID.MoonLordCore => Moon, _ => 0
    };
    public static string Next(SoulItem soul)
    {
        if (!soul.Attached) return "白天地表使用凝胶信物，与友善史莱姆王对话获得「附着」";
        if (soul.Capacity < 10) return $"第{soul.Capacity+1}普通槽：" + Missing(soul, soul.Capacity);
        return soul.Awakened ? "复生已永久完成，可以自由更换寄宿武器" : "十个普通槽已齐；同时寄宿十把天顶材料剑以完成永久复生";
    }
}

public class SoulPlayer : ModPlayer
{
    internal float Flow;
    internal int ArcaneCooldown;
    private int idle;
    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    {
        if (!mediumCoreDeath)
        {
            yield return new Item(ModContent.ItemType<SoulItem>());
            yield return new Item(ModContent.ItemType<GuardianGel>());
        }
    }
    public override void PostUpdate()
    {
        if(ArcaneCooldown>0) ArcaneCooldown--;
        if (Player.whoAmI == Main.myPlayer && Main.mouseItem.ModItem is SoulItem held)
            HeroPrefixCompat.Remember(held);
        if (++idle > 35 || Player.HeldItem.ModItem is not SoulItem) Flow = Math.Max(0, Flow - .015f);
        if (Player.dead) Flow = 0;
    }
    internal void Cycle(int count)
    {
        idle = 0;
        if (count > 1) Flow = Math.Min(1, Flow + .07f * Math.Min(count, 4));
    }
}

// Keep encounter participation across worm segments, and clear it on despawn.
public class SoulEncounters : ModSystem
{
    internal static readonly Dictionary<int, HashSet<(int Player, string Soul)>> Hits = [];
    public override void OnWorldLoad() => Hits.Clear();
    public override void OnWorldUnload() => Hits.Clear();
    public override void PostUpdateNPCs()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || Hits.Count == 0) return;
        foreach (int bit in Hits.Keys.ToArray())
            if (!Main.npc.Any(n => n.active && SoulProgress.BossBit(n.type) == bit)) Hits.Remove(bit);
    }
}

public class SoulBossCredit : GlobalNPC
{
    public static void Register(NPC npc,int owner,string id)
    {
        int bit=SoulProgress.BossBit(npc.type);
        if(bit==0 || string.IsNullOrEmpty(id) || owner<0 || owner>=Main.maxPlayers ||
            !Main.player[owner].active || AttachmentStory.FindSoul(Main.player[owner],id)==null) return;
        if(!SoulEncounters.Hits.TryGetValue(bit,out var participants)) SoulEncounters.Hits[bit]=participants=[];
        participants.Add((owner,id));
    }
    public static void ReceiveCredit(System.IO.BinaryReader reader,int owner)
    {
        int identity=reader.ReadInt32(),index=reader.ReadInt16(),netId=reader.ReadInt32();
        if(Main.netMode!=NetmodeID.Server || owner<0 || owner>=Main.maxPlayers || index<0 || index>=Main.maxNPCs) return;
        NPC npc=Main.npc[index];
        Projectile projectile=Main.projectile.FirstOrDefault(p=>p.active && p.owner==owner && p.identity==identity);
        if(projectile==null || !npc.active || npc.netID!=netId || npc.friendly || npc.dontTakeDamage) return;
        Register(npc,owner,projectile.GetGlobalProjectile<SoulSource>().SoulId);
    }
    public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        // Register before StrikeNPC/OnKill so a killing first hit also earns credit.
        int bit = SoulProgress.BossBit(npc.type);
        string id = projectile.GetGlobalProjectile<SoulSource>().SoulId;
        if (bit == 0 || string.IsNullOrEmpty(id)) return;
        if(Main.netMode==NetmodeID.MultiplayerClient)
        {
            if(projectile.owner!=Main.myPlayer) return;
            var packet=ModContent.GetInstance<ZenithReborn>().GetPacket();
            packet.Write((byte)8);packet.Write(projectile.identity);packet.Write((short)npc.whoAmI);packet.Write(npc.netID);packet.Send();
        }
        else Register(npc,projectile.owner,id);
    }
    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        int bit = SoulProgress.BossBit(npc.type);
        if (bit == 0) return;
        if (bit == SoulProgress.Destroyer && npc.type != NPCID.TheDestroyer) return;
        if (bit == SoulProgress.Evil && npc.type != NPCID.BrainofCthulhu && Main.npc.Any(n =>
            n.active && n.whoAmI != npc.whoAmI && SoulProgress.BossBit(n.type) == bit)) return;
        if (!SoulEncounters.Hits.Remove(bit, out var participants)) return;
        foreach (var entry in participants)
        {
            if (entry.Player < 0 || entry.Player >= Main.maxPlayers) continue;
            Player player = Main.player[entry.Player];
            if (!player.active) continue;
            for (int slot = 0; slot < 58; slot++)
            {
                if (player.inventory[slot].ModItem is not SoulItem soul || soul.SoulId != entry.Soul) continue;
                soul.Kills |= bit;
                int core = bit switch { SoulProgress.Eye => 0, SoulProgress.Flesh => 1,
                    SoulProgress.Flower => 3, SoulProgress.Temple => 4, SoulProgress.Moon => 5, _ => -1 };
                if (SoulProgress.Mechs(soul.Kills) == 3 &&
                    (bit & (SoulProgress.Destroyer | SoulProgress.Retinazer | SoulProgress.Spazmatism | SoulProgress.Prime)) != 0) core = 2;
                if (core >= 0 && (soul.Cores & (1 << core)) == 0)
                    player.QuickSpawnItem(npc.GetSource_Loot(), SoulCore.TypeFor(core));
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
                else Main.NewText("剑魂铭刻了胜利。" + SoulProgress.Next(soul), 160, 220, 255);
                break;
            }
        }
    }
}

public abstract class SoulCore : ModItem
{
    public abstract int Index { get; }
    public override string Texture => "Terraria/Images/Item_" + ItemID.FragmentSolar;
    public static int TypeFor(int index) => index switch { 0 => ModContent.ItemType<DawnCore>(),
        1 => ModContent.ItemType<FleshCore>(), 2 => ModContent.ItemType<MachineCore>(),
        3 => ModContent.ItemType<LifeCore>(), 4 => ModContent.ItemType<TempleCore>(),
        _ => ModContent.ItemType<CelestialCore>() };
    public override void SetDefaults()
    {
        Item.width = Item.height = 24; Item.maxStack = 999;
        Item.rare = ItemRarityID.LightPurple; Item.consumable = true;
        Item.useTime = Item.useAnimation = 25; Item.useStyle = ItemUseStyleID.HoldUp;
        Item.UseSound = SoundID.Item4;
    }
    public SoulItem Target(Player player)
    {
        SoulItem editing = SoulUI.EditingSoul;
        if (editing != null && player.inventory.Any(i => ReferenceEquals(i.ModItem, editing)) && CanAcceptCore(editing)) return editing;
        return player.inventory.Select(i => i.ModItem).OfType<SoulItem>().FirstOrDefault(CanAcceptCore);
    }
    private bool CanAcceptCore(SoulItem soul) => SoulProgress.CanAcceptCore(soul, Index);
    public override bool CanUseItem(Player player) => Target(player) != null;
    public override bool? UseItem(Player player)
    {
        var target = Target(player);
        if (target == null) return false;
        SoulProgress.AcceptCore(target, Index);
        if (player.whoAmI == Main.myPlayer)
        {
            Main.NewText(SoulProgress.CoreNames[Index] + "魂芯已归位，承载槽位：" + target.Capacity, 160, 230, 255);
            SoulUI.Sync(target);
        }
        return true;
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips) => tooltips.Add(new(Mod,"CoreHelp",
        $"使用：融入已用自身参与对应Boss战斗的剑魂，优先当前打开的剑魂\n{SoulProgress.CoreInstructions[Index]}\n魂芯遗失：按P→成长路线→补办魂芯；不能替代击杀铭刻"));
}
public class DawnCore : SoulCore { public override int Index => 0; }
public class FleshCore : SoulCore { public override int Index => 1; }
public class MachineCore : SoulCore { public override int Index => 2; }
public class LifeCore : SoulCore { public override int Index => 3; }
public class TempleCore : SoulCore { public override int Index => 4; }
public class CelestialCore : SoulCore { public override int Index => 5; }
