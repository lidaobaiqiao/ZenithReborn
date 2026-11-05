using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;

namespace ZenithReborn;

// 木质族面板：贴在剑阵左缘，用图形化方式展示 8 把木剑的寄宿与特性、5 阶段成长、
// 关键数值与 4 复合羁绊。本文件只负责绘制，不修改任何战斗或寄宿状态。
public partial class SoulUI
{
    // 4 复合羁绊的展示色（仅本面板 UI 用）：锻根暖铜、腐林紫、灰烬橙、归冠沿用圣林绿。
    private static readonly Color ForgeLight = new(198, 158, 96);
    private static readonly Color RotLight = new(170, 112, 198);
    private static readonly Color EmberLight = new(230, 130, 72);

    // 单个复合羁绊的展示状态。
    private readonly record struct WoodBondLight(string Name, bool Active, Color Color);

    // 实时计算 4 复合羁绊的点亮状态；归冠颜色在激活时改为棱彩。
    private static WoodBondLight[] WoodBondLights(SoulItem soul, float time)
    {
        bool crown = WoodBondCore.ActiveCrown(soul);
        return
        [
            new("锻根共生", WoodBondCore.ActiveForge(soul), ForgeLight),
            new("腐林凋蚀", WoodBondCore.ActiveRot(soul), RotLight),
            new("灰烬繁生", WoodBondCore.ActiveEmber(soul), EmberLight),
            // 归冠激活后用棱彩，未激活时用圣林绿。
            new("圣林归冠", crown, crown ? BondColors.Prismatic(time) : CrownHue),
        ];
    }

    private static void DrawWoodPanel(SoulItem soul, SoulArmoryLayout layout)
    {
        Rectangle r = layout.WoodPanel(soul.VisibleSupportCount, armoryScroll);
        if (r.IsEmpty) return;
        float time = (float)UiTime;

        // 两套口径分开：hostedMask 是全部已寄宿木剑（未过滤，含森林/圣林与传奇等奖励位，用于阶段、倍率、生命与收集展示），
        // CombatWood.Mask 是特性口径的木剑（只用于剑格子与特性列表）；偏锋替换后 TraitEnabled 恒 true，与 hostedMask 数值相同，两套口径仍保留分离。
        int hostedMask = WoodTypes.Mask(SoulSupport.FamilyItems(soul));
        WoodLoadout activeWood = soul.CombatWood;
        int activeMask = activeWood.Mask;
        BondState bond = BondSnapshot.Wood(soul);
        WoodBondLight[] bondLights = WoodBondLights(soul, time);
        bool crown = activeWood.Crown;

        // 面板底色与边框；归冠时边框带棱彩微光，其余用圣林绿。
        RoundedBox(r, new Color(13, 22, 31) * .96f, 10);
        Border(r, (crown ? BondColors.Prismatic(time) : CrownHue) * .34f);

        const int pad = 10;
        int innerX = r.X + pad, innerRight = r.Right - pad, innerW = innerRight - innerX;

        // 悬停提示文本在裁剪区外绘制，先在裁剪绘制期间记录。
        string hoverTip = null;

        // 各内容区块固定高度合计 153（标题20+阶段24+网格53+数值40+羁绊16），
        // 加上顶部 8、底部预留 10，剩余高度按 4 个间隔均分；面板越矮间距越小，最小为 2。
        const int contentHeight = 20 + 24 + 53 + 40 + 16;
        int gap = Math.Max(2, (r.Height - 8 - 10 - contentHeight) / 4);

        InfoClip(r, () =>
        {
            int y = r.Y + 8;

            // 1. 标题行：族徽与族名，右侧显示当前阶段。
            Color titleColor = crown ? BondColors.Prismatic(time) : CrownHue;
            Glyph(SoulGlyph.Leaf, new(innerX + 8, y + 10), 16, titleColor);
            Text("木质族", new(innerX + 22, y + 4), InfoWhite, .6f);
            string stage = bond.StageLine;
            Vector2 stageSize = FontAssets.MouseText.Value.MeasureString(stage) * .46f;
            Text(stage, new(innerRight - stageSize.X, y + 5),
                bond.Active ? BondColors.Tier(BondKind.Wood, bond.Tier) : InfoQuiet, .46f);
            y += 20;
            y += gap; // 标题与阶段轨道的间距。

            // 2. 5 阶段节点：对应阈值 1/2/4/6/8，已达成点亮，当前阶段节点放大并发光。
            y = DrawWoodStageTrack(bond, innerX, innerRight, y, time);
            y += gap; // 阶段轨道与剑网格的间距。

            // 3. 8 剑图标网格：2 行 4 列，已寄宿点亮、未寄宿暗显，被诅咒压制的加小锁。
            y = DrawWoodSwordGrid(hostedMask, activeMask, innerX, innerW, y, ref hoverTip);

            // 分隔线：网格后留 2 像素再画，线后用剩余间距到数值区（矮面板时紧贴）。
            y += 2;
            Line(new(innerX, y), new(innerRight, y), CrownHue * .2f);
            y += Math.Max(0, gap - 2);

            // 4. 关键数值：2 列 2 行，倍率、生命、支援、森林。
            // 倍率与生命属数值口径，按有效寄宿计（与 WoodPlayer 的实际加成同源）；不再受诅咒过滤。
            WoodLoadout hostedWood = soul.HostedWood;
            int forestCap = crown ? ForestPower.CrownCap : ForestPower.BaseCap;
            int forestLayers = ForestPower.Layers(Main.LocalPlayer);
            (string Label, string Value)[] metrics =
            [
                ("倍率", "×" + hostedWood.Strength.ToString("0.##")),
                ("生命", "+" + hostedWood.Life),
                ("支援", activeWood.SupportCount + "/" + soul.SupportCapacity),
                ("森林", forestLayers + "/" + forestCap),
            ];
            y = DrawWoodMetrics(metrics, innerX, innerW, y);
            y += gap; // 数值与羁绊灯的间距。

            // 5. 4 复合羁绊状态灯：激活发光，未激活暗显。
            y = DrawWoodBondLights(bondLights, innerX, innerRight, y, time, ref hoverTip);
        });

        // 悬停提示在裁剪区外绘制，避免被面板边界切掉。
        if (hoverTip != null) DrawWoodTooltip(hoverTip);
    }

    // 阶段节点轨道：5 个圆点横向均匀分布，下方标阈值数字。
    private static int DrawWoodStageTrack(BondState bond, int innerX, int innerRight, int y, float time)
    {
        int span = innerRight - innerX;
        for (int i = 0; i < bond.Thresholds.Length; i++)
        {
            int threshold = bond.Thresholds[i];
            bool lit = bond.Count >= threshold;
            bool current = bond.Tier == i + 1;
            // 节点横向均匀分布，首尾对齐内边距。
            float nx = innerX + span * i / (bond.Thresholds.Length - 1f);
            var node = new Vector2(nx, y + 8);
            Color nodeColor = lit ? BondColors.Tier(BondKind.Wood, i + 1) : new Color(70, 82, 92);
            if (lit)
            {
                // 当前阶段节点放大并加发光，其余阶段只点亮。
                float radius = current ? 6.5f : 5f;
                if (current) Glow(node, 12f, nodeColor, .55f + MathF.Sin(time * 2f) * .12f);
                Glow(node, radius, nodeColor, .95f);
            }
            else
            {
                // 未达成只画暗边框空心点。
                Arc(node, 4f, 0, MathHelper.TwoPi, nodeColor * .7f, 1.2f);
            }
            // 阈值数字。
            CenterText(threshold.ToString(), new(node.X, y + 16), lit ? InfoQuiet : nodeColor, .38f);
        }
        // 轨道内容到数字底部约 24，返回精确高度，间距由调用处统一控制。
        return y + 24;
    }

    // 8 剑图标网格：返回结束 y。悬停某格时记录该剑的说明。
    private static int DrawWoodSwordGrid(int hostedMask, int activeMask, int innerX, int innerW, int y,
        ref string hoverTip)
    {
        const int columns = 4, gap = 3;
        int cellW = (innerW - gap * (columns - 1)) / columns;
        const int cellH = 25;
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int i = row * columns + col;
                int cx = innerX + col * (cellW + gap);
                var cell = new Rectangle(cx, y, cellW, cellH);
                bool hosted = (hostedMask & (1 << i)) != 0;
                bool active = (activeMask & (1 << i)) != 0;

                // 格子底色：已寄宿稍亮，未寄宿暗底。
                RoundedBox(cell, hosted ? new Color(28, 44, 38) : new Color(17, 26, 34), 6);
                if (hosted) Border(cell, (active ? CrownHue : InfoQuiet) * .4f);

                // 木剑图标：已寄宿正常色，特性被压制略降亮，未寄宿暗灰。
                Item sample = Terraria.ID.ContentSamples.ItemsByType[WoodTypes.Types[i]];
                Color tint = hosted ? (active ? Color.White : new Color(150, 160, 150)) : new Color(96, 104, 112);
                ArmoryItem(sample, new Vector2(cell.Center.X, cell.Y + 10), 18f, tint);

                // 特性名：2 字小字，放在格子底部。
                CenterText(WoodTypes.Effects[i], new(cell.Center.X, cell.Bottom - 9),
                    hosted ? InfoWhite : InfoQuiet, .38f);

                // 已寄宿但特性被压制时右上角加小锁；偏锋替换后 TraitEnabled 恒 true，此分支不再触发，代码保留待后续清理。
                if (hosted && !active)
                    Glyph(SoulGlyph.Lock, new(cell.Right - 5, cell.Y + 5), 9, Champagne);

                // 悬停时显示该剑名称、特性与状态。
                if (cell.Contains(Main.mouseX, Main.mouseY))
                {
                    string state = !hosted ? "未寄宿" : active ? "特性生效中" : "已寄宿 · 特性被诅咒压制";
                    hoverTip = WoodTypes.Names[i] + " · " + WoodTypes.Effects[i] + "\n" + state;
                }
            }
            y += cellH + (row == 0 ? gap : 0);
        }
        return y;
    }

    // 关键数值网格：2 列 2 行，标签在左、数值在右。
    private static int DrawWoodMetrics((string Label, string Value)[] metrics, int innerX, int innerW, int y)
    {
        const int gap = 4, cellH = 18;
        int cellW = (innerW - gap) / 2;
        for (int i = 0; i < metrics.Length; i++)
        {
            int col = i % 2, row = i / 2;
            int cx = innerX + col * (cellW + gap);
            int cy = y + row * (cellH + gap);
            Text(metrics[i].Label, new(cx, cy + 2), InfoQuiet, .42f);
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(metrics[i].Label) * .42f;
            Text(metrics[i].Value, new(cx + labelSize.X + 6, cy), InfoWhite, .46f);
        }
        return y + cellH * 2 + gap;
    }

    // 复合羁绊状态灯：4 个等宽小格，激活点亮发光，未激活暗显；悬停记录羁绊名。
    private static int DrawWoodBondLights(WoodBondLight[] lights, int innerX, int innerRight, int y,
        float time, ref string hoverTip)
    {
        int span = innerRight - innerX;
        int cellW = span / lights.Length;
        for (int i = 0; i < lights.Length; i++)
        {
            int cx = innerX + i * cellW;
            var cell = new Rectangle(cx, y, cellW, 18);
            var node = new Vector2(cell.Center.X, y + 8);
            if (lights[i].Active)
            {
                // 激活：填充菱形并加发光。
                Glow(node, 12f, lights[i].Color, .5f + MathF.Sin(time * 2f + i) * .1f);
                ArmoryDiamond(node, 5.5f, lights[i].Color, 1.6f);
            }
            else
            {
                // 未激活：暗色空心菱形，颜色在深底上仍可辨认轮廓。
                ArmoryDiamond(node, 5f, new Color(108, 122, 132), 1.1f);
            }
            if (cell.Contains(Main.mouseX, Main.mouseY))
                hoverTip = lights[i].Name + "\n" + (lights[i].Active ? "已激活" : "未激活");
        }
        // 菱形中心在 y+8，半对角线约 5.5，内容高度约 16，间距由调用处控制。
        return y + 16;
    }

    // 悬停提示小浮层：在鼠标旁画暗色圆角底与文字，自动测量尺寸并限制在屏幕内。
    private static void DrawWoodTooltip(string tip)
    {
        const float scale = .46f;
        string[] lines = tip.Split('\n');
        float width = 0;
        foreach (string line in lines)
            width = Math.Max(width, FontAssets.MouseText.Value.MeasureString(line).X * scale);
        int lineH = (int)(FontAssets.MouseText.Value.MeasureString("字").Y * scale) + 4;
        var box = new Rectangle(0, 0, (int)width + 16, lineH * lines.Length + 10);
        // 浮层放在鼠标右下，超出屏幕则翻到左上。
        int tx = Main.mouseX + 14, ty = Main.mouseY + 14;
        if (tx + box.Width > Main.screenWidth) tx = Main.mouseX - box.Width - 10;
        if (ty + box.Height > Main.screenHeight) ty = Main.mouseY - box.Height - 10;
        box.Location = new Point(tx, ty);
        RoundedBox(box, new Color(14, 20, 28) * .98f, 6);
        Border(box, CrownHue * .3f);
        for (int i = 0; i < lines.Length; i++)
            Text(lines[i], new(box.X + 8, box.Y + 6 + i * lineH),
                i == 0 ? InfoWhite : InfoQuiet, scale);
    }
}
