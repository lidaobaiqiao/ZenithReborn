using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 「薄刃」诅咒隔离夹具：四阶段有效概率、生成时一次性随机且每剑独立、
// 残缺飞剑在 CanHitNPC 处统一拦截、实例默认值与 extra AI 网络往返、
// 与偏锋状态共存、地形惩罚移除后不再退场、穿界祝福链仍可达。
internal static class ThinbladeCheck
{
    // 与 DeviationCheck/CurseCheck 同口径造魂；显式清零 Seals/LegacyReleased，
    // 避免夹具兼容路径把 LegacyReleased 置 63 而将「诅咒中」抬成已解除，掩盖薄刃概率。
    private static (Player, SoulItem) Setup(int hosts = 4)
    {
        foreach (var proj in Main.projectile) proj.active = false;
        foreach (var npc in Main.npc) npc.active = false;
        Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
        Main.rand = new UnifiedRandom(20261106);
        var player = new Player { whoAmI = 0, active = true, position = new(1000, 1000) };
        Main.player[0] = player; player.ResetEffects();
        var soul = (SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached = true; soul.Kills = SoulProgress.All; soul.Cores = 63;
        soul.Seals = 0; soul.LegacyReleased = 0;
        for (int i = 0; i < hosts; i++) soul.Hosts[i] = new Item(SoulProgress.ZenithWeapons[i]);
        player.inventory[0] = soul.Item; soul.RefreshStats();
        return (player, soul);
    }

    private static SoulBlade Blade(Player player, SoulItem soul, int type)
    {
        Main.mouseX = (int)player.Center.X + 200; Main.mouseY = (int)player.Center.Y;
        Main.screenPosition = Vector2.Zero;
        return (SoulBlade)Main.projectile[soul.EmitBlade(player, new Item(type), 0, 1)].ModProjectile;
    }

    internal static void Run(Action<bool, string> check)
    {
        var oldRand = Main.rand;
        try
        {
            Stages(check);
            Rolls(check);
            Interception(check);
            RoundTrip(check);
            Terrain(check);
            Blessing(check);
        }
        finally
        {
            foreach (var proj in Main.projectile) proj.active = false;
            foreach (var npc in Main.npc) npc.active = false;
            if (oldRand != null) Main.rand = oldRand;
        }
    }

    // 四阶段全可达（本组 3 Boss）：0 Cursed / 蜂王 Weakened / +世纪之花 Released / +石巨人 Blessed。
    private static void Stages(Action<bool, string> check)
    {
        var (_, soul) = Setup();
        var cases = new[]
        {
            (stage: CurseStage.Cursed,   seals: 0,                                                     chance: .70f),
            (stage: CurseStage.Weakened, seals: SoulCurses.Bee,                                      chance: .90f),
            (stage: CurseStage.Released, seals: SoulCurses.Bee | SoulCurses.Flower,                 chance: 1f),
            (stage: CurseStage.Blessed,  seals: SoulCurses.Bee | SoulCurses.Flower | SoulCurses.Temple, chance: 1f),
        };
        foreach (var c in cases)
        {
            soul.Seals = c.seals;
            check(soul.Curse(SoulCurse.Thinblade) == c.stage,
                $"Boss seals produce the {c.stage} thinblade stage");
            check(Math.Abs(SoulThinblade.EffectiveChance(c.stage) - c.chance) < 1e-3f,
                $"Effective chance is {c.chance:0.##} at the {c.stage} stage");
        }
    }

    // 每把飞剑生成时各掷一次；无保底队列、无动态补偿，解除后恒有效。
    private static void Rolls(Action<bool, string> check)
    {
        var (_, soul) = Setup();
        Main.rand = new UnifiedRandom(20261106);
        var stages = new[]
        {
            (stage: CurseStage.Cursed,   seals: 0,                                     chance: .70f),
            (stage: CurseStage.Weakened, seals: SoulCurses.Bee,                      chance: .90f),
            (stage: CurseStage.Released, seals: SoulCurses.Bee | SoulCurses.Flower, chance: 1f),
            (stage: CurseStage.Blessed,  seals: SoulCurses.Bee | SoulCurses.Flower | SoulCurses.Temple, chance: 1f),
        };
        foreach (var c in stages)
        {
            soul.Seals = c.seals;
            int effective = 0, varied = 0;
            bool previous = false;
            const int samples = 2000;
            for (int i = 0; i < samples; i++)
            {
                bool rolled = SoulThinblade.Roll(soul);
                if (rolled) effective++;
                if (i > 0 && rolled != previous) varied++;
                previous = rolled;
            }
            check(Math.Abs(effective / (float)samples - c.chance) < .05f,
                $"Roll matches the {c.stage} chance within 5 percent (observed {effective / (float)samples:0.###})");
            if (c.chance < 1f)
                check(varied > 0, $"Consecutive blades roll independently at the {c.stage} stage");
            else
                check(effective == samples, $"Roll is always effective at the {c.stage} stage");
        }
    }

    // 残缺飞剑在 CanHitNPC 处一票否决；tML 下 Projectile.Damage() 会整段跳过该 NPC，
    // 因此 ModifyHitNPC/OnHitNPC 的木质施加与五族扇出、targetHits、免疫都不发生。
    private static void Interception(Action<bool, string> check)
    {
        var (player, soul) = Setup();
        var blade = Blade(player, soul, ItemID.CopperShortsword);
        var npc = new NPC();
        npc.SetDefaults(NPCID.Zombie); npc.whoAmI = 90; npc.active = true; Main.npc[90] = npc;

        check(!blade.ThinbladeDisabled,
            "A freshly emitted blade defaults to effective (SetDefaults clears the disabled flag)");
        check(blade.CanHitNPC(npc) != false,
            "An effective blade is not rejected before the hit-limit check");

        blade.ThinbladeDisabled = true;
        check(blade.CanHitNPC(npc) == false,
            "A thinblade-disabled blade is rejected at the unified hit-eligibility gate");
        // targetHits 是私有字段，用反射确认否决后没有写入任何成功命中计数（写入点在 OnHitNPC）。
        var hits = (Dictionary<long, int>)typeof(SoulBlade)
            .GetField("targetHits", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(blade)!;
        check(hits.Count == 0,
            "A disabled blade consumes no successful hit count");
    }

    // 残缺标记随 SendExtraAI/ReceiveExtraAI 往返一致并精确消费负载；偏锋偏移与它互不覆盖。
    private static void RoundTrip(Action<bool, string> check)
    {
        var (player, soul) = Setup();
        var blade = Blade(player, soul, ItemID.CopperShortsword);
        blade.CurseOffset = new Vector2(13.5f, -27.25f);
        blade.ThinbladeDisabled = true;
        using (var stream = new MemoryStream())
        {
            blade.SendExtraAI(new BinaryWriter(stream));
            stream.Position = 0;
            var clone = new Projectile();
            clone.SetDefaults(blade.Projectile.type);
            var received = (SoulBlade)clone.ModProjectile;
            received.ReceiveExtraAI(new BinaryReader(stream));
            check(received.ThinbladeDisabled == blade.ThinbladeDisabled,
                "ThinbladeDisabled survives the SendExtraAI/ReceiveExtraAI round trip");
            check(received.CurseOffset == blade.CurseOffset,
                "Deviation offset and thinblade state coexist without overwriting each other");
            check(stream.Position == stream.Length,
                "The extra AI payload is consumed exactly, with no trailing bool");
        }
    }

    // 地形惩罚移除后，飞剑穿过实体地形不再被 Retire；穿界祝福的地形判定仍由 SolidAt 独立处理。
    private static void Terrain(Action<bool, string> check)
    {
        Main.tileSolid[TileID.Stone] = true;
        foreach (bool released in new[] { false, true })
        {
            var (player, soul) = Setup();
            if (released) soul.Seals = SoulCurses.Bee | SoulCurses.Flower;
            var blade = Blade(player, soul, ItemID.CopperShortsword);
            blade.TargetOffset = new Vector2(200, 0); blade.Vertical = 10;
            for (int k = 0; k < 4; k++) blade.AI();
            for (int x = 55; x < 90; x++) for (int y = 55; y < 80; y++)
            {
                var t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.Stone;
            }
            for (int k = 0; k < 8 && !blade.Retired; k++) blade.AI();
            check(!blade.Retired,
                $"No terrain retirement after the barrier curse is replaced (released={released})");
            for (int x = 55; x < 90; x++) for (int y = 55; y < 80; y++)
                Main.tile[x, y].ClearEverything();
        }
    }

    // 改名后穿界祝福判定仍用第四道枚举且可达（石巨人齐备时为 Blessed）。
    private static void Blessing(Action<bool, string> check)
    {
        var (_, soul) = Setup();
        soul.Seals = SoulCurses.Bee | SoulCurses.Flower | SoulCurses.Temple;
        check(soul.Curse(SoulCurse.Thinblade) == CurseStage.Blessed &&
              BlessingTuning.Blessed(soul, SoulCurse.Thinblade),
            "The crossing blessing still resolves through the renamed fourth curse");
    }
}
