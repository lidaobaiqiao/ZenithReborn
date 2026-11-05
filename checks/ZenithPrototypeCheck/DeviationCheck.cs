using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 「偏锋」诅咒隔离夹具：按 Boss 记录数的三档瞄准偏移上限、圆形采样范围与方向覆盖、每剑独立性、
// CurseOffset 的 extra AI 网络往返、TraitEnabled 恒 true 后所有主剑特性生效，
// 以及偏移只叠加到目标点、不改动飞剑运动参数。
internal static class DeviationCheck
{
    // 与 CurseCheck/SwingCheck 同口径造魂；显式清零 Seals/LegacyReleased，
    // 避免夹具兼容路径把 LegacyReleased 置 63 而将「诅咒中」抬成「已解除」，掩盖偏移压制。
    private static (Player, SoulItem) Setup(int hosts = 4)
    {
        foreach (var proj in Main.projectile) proj.active = false;
        foreach (var npc in Main.npc) npc.active = false;
        Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
        Main.rand = new UnifiedRandom(20261105);
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
            RoundTrip(check);
            Traits(check);
            Motion(check);
        }
        finally
        {
            foreach (var proj in Main.projectile) proj.active = false;
            foreach (var npc in Main.npc) npc.active = false;
            if (oldRand != null) Main.rand = oldRand;
        }
    }

    // 三档按个人 Boss 记录数：未击败任一 48 → 击败其中一位 24 → 两位齐 0。
    // 不按 CurseStage：本组只有两位 Boss，StageForCount(total=2) 的 count=1 直接判 Released，Weakened 永不可达。
    private static void Stages(Action<bool, string> check)
    {
        var (_, soul) = Setup();
        // 旧档该位为 1 表示已解除过切换诅咒，不能因为换机制就重新给老玩家上满诅咒。
        soul.LegacyReleased = 1 << (int)SoulCurse.Switching;
        check(SoulDeviation.MaxOffset(soul) == 0f && SoulDeviation.NextOffset(soul) == Vector2.Zero,
            "Legacy-released saves never get the deviation curse re-applied");
        soul.LegacyReleased = 0;

        var cases = new[]
        {
            (label: "no boss",      seals: 0,                                        max: 48f),
            (label: "brain only",   seals: SoulCurses.Brain,                        max: 24f),
            (label: "cultist only", seals: SoulCurses.Cultist,                      max: 24f),
            (label: "both bosses",  seals: SoulCurses.Brain | SoulCurses.Cultist,   max: 0f),
        };
        foreach (var c in cases)
        {
            soul.Seals = c.seals;
            check(Math.Abs(SoulDeviation.MaxOffset(soul) - c.max) < 1e-3f,
                $"Deviation max offset is {c.max} with {c.label}");
            Sample(check, soul, c.label, c.max);
        }
    }

    // 每阶段采样 256 次：半径落在 [0.35*max, max]，方向覆盖四象限，连续两次不总相同；
    // 解除/祝福阶段恒为 Vector2.Zero。
    private static void Sample(Action<bool, string> check, SoulItem soul, string stage, float max)
    {
        const int samples = 256;
        bool inRing = true, allZero = true, varied = false;
        int[] quadrants = new int[4];
        Vector2 previous = default;
        for (int i = 0; i < samples; i++)
        {
            Vector2 offset = SoulDeviation.NextOffset(soul);
            float length = offset.Length();
            if (!(length <= max + 1e-3f && length >= 0.35f * max - 1e-3f)) inRing = false;
            if (offset != Vector2.Zero) allZero = false;
            if (max > 0f)
            {
                quadrants[(offset.X >= 0 ? 0 : 1) + (offset.Y >= 0 ? 0 : 2)]++;
                if (i > 0 && offset != previous) varied = true;
                previous = offset;
            }
        }
        check(inRing, $"256 deviation samples stay inside the [0.35*max, max] ring at the {stage} stage");
        if (max > 0f)
        {
            check(quadrants.All(q => q > 0), $"Deviation directions cover all four quadrants at the {stage} stage");
            check(varied, $"Consecutive deviations are independent per blade at the {stage} stage");
        }
        else
            check(allZero, $"Deviation is always Vector2.Zero at the {stage} stage");
    }

    // CurseOffset 经 SendExtraAI/ReceiveExtraAI 往返一致，并精确消费整段负载。
    private static void RoundTrip(Action<bool, string> check)
    {
        var (player, soul) = Setup();
        var blade = Blade(player, soul, ItemID.CopperShortsword);
        blade.CurseOffset = new Vector2(13.5f, -27.25f);
        using (var stream = new MemoryStream())
        {
            blade.SendExtraAI(new BinaryWriter(stream));
            stream.Position = 0;
            var clone = new Projectile();
            clone.SetDefaults(blade.Projectile.type);
            var received = (SoulBlade)clone.ModProjectile;
            received.ReceiveExtraAI(new BinaryReader(stream));
            check(received.CurseOffset == blade.CurseOffset && stream.Position == stream.Length,
                "CurseOffset survives the SendExtraAI/ReceiveExtraAI round trip and consumes the exact payload");
        }
    }

    // TraitEnabled 恒 true 后，未解除诅咒时所有有效主剑特性都正常触发。
    private static void Traits(Action<bool, string> check)
    {
        var (_, soul) = Setup(3);
        soul.Seals = 0; soul.LegacyReleased = 0; soul.RefreshStats();
        int effective = soul.Active.Count();
        check(effective == 3, "Three distinct primaries stay effective while the deviation curse is cursed");
        check(soul.TraitItems.Count() == effective && soul.Active.All(i => soul.TraitEnabled(i.type)),
            "Every effective primary keeps its trait after TraitEnabled becomes unconditional");
        check(soul.Curse(SoulCurse.Switching) == CurseStage.Cursed && soul.TraitEnabled(soul.Hosts[2].type),
            "The third primary stays enabled while the deviation curse is cursed");
    }

    // 偏移只改目标位置：给同一 blade 设不同 CurseOffset，AI() 前后
    // TargetOffset、Vertical、Projectile.velocity 全部不变，证明不改运动速度。
    private static void Motion(Action<bool, string> check)
    {
        var (player, soul) = Setup();
        var blade = Blade(player, soul, ItemID.CopperShortsword);
        bool unchanged = true;
        foreach (var offset in new[] { new Vector2(0, 0), new Vector2(40, -30), new Vector2(-48, 12) })
        {
            blade.CurseOffset = offset;
            var target = blade.TargetOffset;
            float vertical = blade.Vertical;
            var velocity = blade.Projectile.velocity;
            blade.AI();
            if (blade.TargetOffset != target || blade.Vertical != vertical || blade.Projectile.velocity != velocity)
                unchanged = false;
        }
        check(unchanged, "CurseOffset never changes TargetOffset, Vertical or projectile velocity across AI");
    }
}
