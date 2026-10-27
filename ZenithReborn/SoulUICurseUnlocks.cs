using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ReLogic.Content;

namespace ZenithReborn;

// 成长记忆·封印铭刻：用官方 Wiki 动态头像和阶段效果，直观展示六封印的解锁路线。
public partial class SoulUI
{
    // 每个封印组对应的头像 key，顺序与 SoulCurses.Groups 一致。
    private static readonly string[][] CursePortraitKeys =
    [
        ["Eye", "Moon"],
        ["Brain", "Cultist"],
        ["Eater", "Skeleton"],
        ["Bee", "Flower", "Temple"],
        ["Destroyer", "Twins", "Prime"],
        ["Flesh", "Queen", "Empress"],
    ];

    // 单个阶段的展示：阶段名和该阶段生效的效果。
    private readonly record struct CurseUnlockStep(string Stage, string Effect);

    // 每个封印各阶段的效果，行数与该组 Boss 数一致；每行是击败该位后累计解锁的内容。
    private static readonly CurseUnlockStep[][] CurseSteps =
    [
        [ // 缚身：两Boss，第一位解除，第二位祝福
            new("解除", "御剑距离至少到屏幕左右边界，保留鼠标自由选点。"),
            new("祝福", "御剑距离不再设上限；约15秒蓄满御剑条，按 O 手动开启10秒，视野翻倍，最高增伤50%。"),
        ],
        [ // 切换
            new("解除", "所有寄宿武器特性正常生效，全部主剑可参与百炼飞剑。"),
            new("祝福", "百刃接力：不同主剑首击同一敌人接力，每层强化后续首击10%，最多5层。"),
        ],
        [ // 归鞘
            new("解除", "新轮不打断旧轮，主剑各自完成攻击。"),
            new("祝福", "归鞘留锋：主剑完整往返后存1次强化，下次登场首击释放50%伤害斩痕。"),
        ],
        [ // 阻隔：三Boss，削弱、解除、祝福
            new("削弱", "可连续穿过约64像素实体地形（原32），超出才退出攻击。"),
            new("解除", "主剑正常穿越实体地形，不再被挡住。"),
            new("祝福", "破障：主剑穿过实体后，下次命中附加4秒破甲，每剑只触发一次。"),
        ],
        [ // 独行
            new("削弱", "每轮最多3把主剑（原2把）。"),
            new("解除", "每轮最多4把主剑。"),
            new("祝福", "飞剑随成长增多：世花6、石巨人7、教徒8、月总10把。"),
        ],
        [ // 断锋
            new("削弱", "同剑本趟对同目标最多5次命中（原3次）。"),
            new("解除", "剑影逐次乘0.8：80%、64%、51.2%伤害（原各20%）。"),
            new("祝福", "连斩：剑影不再衰减，所有伤害单元保持100%。"),
        ],
    ];

    // 头像图集缓存，避免每帧重复请求。
    private static readonly Dictionary<string, Texture2D> bossPortraitTextures = [];

    // 读取并缓存指定头像图集。
    private static Texture2D BossPortraitTexture(string key)
    {
        if (bossPortraitTextures.TryGetValue(key, out var texture)) return texture;
        texture = ModContent.Request<Texture2D>("ZenithReborn/Assets/UI/BossPortraits/" + key,
            AssetRequestMode.ImmediateLoad).Value;
        bossPortraitTextures[key] = texture;
        return texture;
    }

    // 绘制单个动态头像；center 为中心，size 为目标边长，alpha 控制明暗，timeMs 决定当前帧。
    private static void DrawBossPortrait(string key, Vector2 center, float size, float alpha, double timeMs)
    {
        if (!SoulBossPortraits.Catalog.TryGetValue(key, out var animation)) return;
        var texture = BossPortraitTexture(key);
        int frame = animation.FrameAt(timeMs);
        var source = animation.FrameRectangle(frame);
        float scale = size / SoulBossAnimation.TileSize;
        Main.spriteBatch.Draw(texture, center, source, Color.White * alpha, 0f,
            new Vector2(SoulBossAnimation.TileSize * .5f), scale, SpriteEffects.None, 0f);
    }

    // 阶段行的颜色：削弱用金、解除用绿、祝福在已达成时用棱彩，否则用金。
    private static Color CurseStepColor(SoulItem soul, int curse, int step)
    {
        string stage = CurseSteps[curse][step].Stage;
        if (stage == "解除") return new(130, 208, 171);
        if (stage == "祝福" && SoulCurses.Stage(soul, (SoulCurse)curse) == CurseStage.Blessed)
            return BondColors.Prismatic((float)UiTime);
        return Champagne;
    }

    // 封印铭刻主列表：每个封印一个横向区块，左侧动态头像纵向排列，右侧对应阶段效果。
    private static int DrawSealUnlockList(SoulItem soul, int x, int y, int w)
    {
        y = InfoHeading("六封印 · 解锁路线", "动态头像 · 击败后点亮", x, y, w,
            SoulGlyph.Core, new(184, 162, 224));

        // 头像列宽度与每行高度；头像列居中放头像和 Boss 名。
        int portraitColumn = Math.Max(140, Math.Min(184, w / 6));
        const int rowHeight = 84;
        const int portraitSize = 56;
        double timeMs = UiTime * 1000.0;

        for (int curse = 0; curse < 6; curse++)
        {
            int total = SoulCurses.Groups[curse].Length;
            var currentStage = SoulCurses.Stage(soul, (SoulCurse)curse);
            int blockHeight = 46 + total * rowHeight + 14;
            var block = new Rectangle(x, y, w, blockHeight);
            if (InfoVisible(block))
            {
                InfoSurface(block, new(184, 162, 224), 0f);

                // 顶部：封印图标、名字和当前阶段。
                Glyph(CurseGlyph((SoulCurse)curse), new Vector2(x + 26, y + 23), 20, new(184, 162, 224));
                Text(SoulCurses.Names[curse], new Vector2(x + 46, y + 10), InfoWhite, .86f);
                InfoBadge("当前 · " + SoulCurses.StageName(currentStage),
                    new Vector2(x + 46, y + 29), new(184, 162, 224));

                // 每一行：左侧头像和 Boss 名，右侧阶段标签和效果。
                for (int step = 0; step < total; step++)
                {
                    int rowY = y + 46 + step * rowHeight;
                    int bit = SoulCurses.Groups[curse][step];
                    bool recorded = (soul.Seals & bit) == bit;
                    float alpha = recorded ? 1f : .2f;

                    // 头像列。
                    var portraitCenter = new Vector2(x + portraitColumn / 2, rowY + 4 + portraitSize / 2);
                    // 未击败的头像垫更暗的底，强化"未点亮"观感。
                    RoundedBox(new Rectangle((int)portraitCenter.X - 31, (int)portraitCenter.Y - 31, 62, 62),
                        Color.Black * (recorded ? .14f : .4f), 10);
                    // 给每个头像加独立相位偏移，避免所有动画完全同步。
                    DrawBossPortrait(CursePortraitKeys[curse][step], portraitCenter,
                        portraitSize, alpha, timeMs + curse * 137 + step * 53);
                    // Boss 名居中显示在头像下方。
                    string bossName = SoulCurses.BossNames[curse][step];
                    float nameWidth = FontAssets.MouseText.Value.MeasureString(bossName).X * .5f;
                    Text(bossName, new Vector2(portraitCenter.X - nameWidth / 2, rowY + 64),
                        recorded ? InfoWhite : InfoQuiet, .5f);

                    // 右侧效果区。
                    int effectX = x + portraitColumn + 20;
                    int effectW = w - portraitColumn - 34;
                    var stepColor = CurseStepColor(soul, curse, step);
                    // 阶段标签带一个小方块。
                    Box(new Rectangle(effectX, rowY + 8, 5, 5), stepColor * (recorded ? 1f : .35f));
                    Text(CurseSteps[curse][step].Stage + " · 击败该位后",
                        new Vector2(effectX + 12, rowY + 2), recorded ? stepColor : InfoQuiet, .6f);
                    // 效果文本；未击败时暗显。
                    InfoLines(CurseSteps[curse][step].Effect, effectX, rowY + 28, effectW,
                        recorded ? Ink : InfoQuiet, .66f);
                }
            }
            y += blockHeight + 12;
        }

        // 外域留痕：猪龙鱼公爵和鹿角怪不施咒，只记录胜利。
        y = InfoBlock("外域留痕",
            $"猪龙鱼公爵 · {((soul.Seals & SoulCurses.Duke) != 0 ? "已铭刻" : "未铭刻")}    鹿角怪 · {((soul.Seals & SoulCurses.Deer) != 0 ? "已铭刻" : "未铭刻")}\n二者不施咒；当前只记录胜利，特殊祝福效果尚未设计。",
            x, y, w, InfoQuiet, SoulGlyph.Memory);
        return y;
    }
}
