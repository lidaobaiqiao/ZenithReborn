using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

public enum BondKind { Wood, Ore }

// 单个羁绊族的实时状态。
public sealed record BondState(
    BondKind Kind,
    string DisplayName,
    int Count,
    int MaxCount,
    int Tier,
    int[] Thresholds,
    Color BaseColor,
    bool IsMax,
    float Progress)
{
    // 是否已激活任意羁绊。
    public bool Active => Count > 0;
    // 当前阶段的下一档阈值；已满时返回最大数量。
    public int NextThreshold => Tier < Thresholds.Length ? Thresholds[Tier] : MaxCount;
    // 当前阶段起始阈值；未激活时为0。
    public int PrevThreshold => Tier >= 1 ? Thresholds[Tier - 1] : 0;
    // 阶段名（木质Ⅰ～Ⅴ，矿石沿用一～五阶与满羁绊），供核心与面板共用。
    public string StageName => BondSnapshot.StageNameOf(Kind, Tier);
    // 阶段罗马数字；矿石无编号，返回空串。
    public string StageCode => BondSnapshot.StageCodeOf(Kind, Tier);
    // 面板用的完整阶段行，如“IV 森罗”；未激活时只显示“未激活”。
    public string StageLine => Tier <= 0 || StageCode.Length == 0 ? StageName : StageCode + " " + StageName;
}

// 羁绊快照：把各族的数量、阶段和进度统一成可绘制数据。
public static class BondSnapshot
{
    // 木质族阶段阈值（V2）：1/2/4/6/8 共五档，8 种即满羁绊。
    private static readonly int[] WoodThresholds = [1, 2, 4, 6, 8];
    // 矿石族阶段阈值：1/2/4/6/8/10。
    private static readonly int[] OreThresholds = [1, 2, 4, 6, 8, 10];
    // 木质五阶段名，索引同 Tier-1。
    private static readonly string[] WoodStageNames = ["萌芽", "生根", "繁枝", "森罗", "森林共鸣"];
    private static readonly string[] WoodStageCodes = ["I", "II", "III", "IV", "V"];
    // 矿石沿用旧称谓：一～五阶，10 种为满羁绊。
    private static readonly string[] OreStageNames = ["一阶", "二阶", "三阶", "四阶", "五阶", "满羁绊"];

    // 阶段名称查询；tier 0 表示尚未激活。
    public static string StageNameOf(BondKind kind, int tier)
    {
        if (tier <= 0) return "未激活";
        string[] names = kind == BondKind.Wood ? WoodStageNames : OreStageNames;
        return names[Math.Clamp(tier - 1, 0, names.Length - 1)];
    }

    // 木质阶段罗马数字；矿石没有编号。
    public static string StageCodeOf(BondKind kind, int tier) =>
        kind == BondKind.Wood && tier > 0 ? WoodStageCodes[Math.Clamp(tier - 1, 0, WoodStageCodes.Length - 1)] : "";

    public static BondState Wood(SoulItem soul)
    {
        // 与战斗侧口径一致：主攻槽＋已生效支援槽＋森林/圣林奖励槽＋传奇位（FamilyItems），
        // 奖励位同样计入，不设例外；也不受切换诅咒过滤。
        int count = System.Numerics.BitOperations.PopCount((uint)WoodTypes.Mask(SoulSupport.FamilyItems(soul)));
        return Build(BondKind.Wood, "木质族", count, 8, WoodThresholds);
    }

    public static BondState Ore(SoulItem soul)
    {
        int count = OreFamily.Count(soul);
        // 羁绊最高阶段为10，分母用10；14种全收集是另一套奖励，不计入羁绊分母。
        return Build(BondKind.Ore, "矿石族", count, 10, OreThresholds);
    }

    private static BondState Build(BondKind kind, string name, int count, int max, int[] thresholds)
    {
        int tier = 0;
        for (int i = 0; i < thresholds.Length; i++)
            if (count >= thresholds[i]) tier = i + 1;
        bool isMax = tier >= thresholds.Length;
        float progress;
        if (isMax) progress = 1f;
        else if (tier <= 0) progress = count <= 0 ? 0f : count / (float)thresholds[0];
        else
        {
            int prev = thresholds[tier - 1], next = thresholds[tier];
            progress = (count - prev) / (float)Math.Max(1, next - prev);
        }
        return new BondState(kind, name, Math.Clamp(count, 0, max), max, tier, thresholds,
            BondColors.Base(kind), isMax, MathHelper.Clamp(progress, 0f, 1f));
    }

    // 当前全部已实装羁绊的族。
    public static IReadOnlyList<BondState> Evaluate(SoulItem soul) => new List<BondState> { Wood(soul), Ore(soul) };

    // 主导羁绊：阶段最高；阶段相同则数量多；再相同则阶段内进度高。
    public static BondState Leading(IReadOnlyList<BondState> bonds)
    {
        BondState best = null;
        foreach (BondState b in bonds)
        {
            if (best == null ||
                b.Tier > best.Tier ||
                b.Tier == best.Tier && b.Count > best.Count ||
                b.Tier == best.Tier && b.Count == best.Count && b.Progress > best.Progress)
                best = b;
        }
        return best;
    }
}

// 羁绊阶段颜色与棱彩。
public static class BondColors
{
    // 木质族：绿系，从暗到亮，索引0对应一档；木质只有五档。
    private static readonly Color[] Wood =
    [
        new(96, 150, 110),
        new(118, 178, 128),
        new(138, 202, 146),
        new(162, 224, 168),
        new(210, 248, 214),
    ];
    // 矿石族：青系，从暗到亮，索引0对应一档。
    private static readonly Color[] Ore =
    [
        new(82, 142, 166),
        new(98, 172, 196),
        new(112, 198, 222),
        new(134, 220, 238),
        new(172, 240, 248),
        new(206, 250, 252),
    ];
    // 各族基础色。
    public static Color Base(BondKind kind) => kind == BondKind.Wood ? new(126, 222, 151) : new(116, 224, 244);

    // 某一阶段的颜色；木质tier为1～5，矿石为1～6。
    public static Color Tier(BondKind kind, int tier)
    {
        Color[] palette = kind == BondKind.Wood ? Wood : Ore;
        return palette[Math.Clamp(tier - 1, 0, palette.Length - 1)];
    }

    // 棱彩：色相随时间旋转，offset用于让同画面不同位置错开色相。
    public static Color Prismatic(float time, float offset = 0f)
    {
        float hue = (time * 0.12f + offset) % 1f;
        if (hue < 0f) hue += 1f;
        return Main.hslToRgb(hue, 0.85f, 0.62f);
    }
}
