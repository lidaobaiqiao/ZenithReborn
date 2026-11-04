using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 剑阵中央的羁绊核心：实时显示主导羁绊的阶段、颜色与数量，满羁绊时呈棱彩。
public partial class SoulUI
{
    private static void DrawBondCore(SoulItem soul, Vector2 c, Rectangle chosen, float time, float opening)
    {
        var bonds = BondSnapshot.Evaluate(soul);
        var leading = BondSnapshot.Leading(bonds);
        bool max = leading.IsMax;
        bool active = leading.Active;
        float orbitRadius=ArmoryLayout.Radius(soul.VisibleSupportCount);
        bool compactCore=orbitRadius<145f;
        float ringR=compactCore?Math.Min(50f,orbitRadius-64f):58f;
        float coreScale=ringR/58f;

        // 核心颜色：满羁绊用棱彩；已激活用阶段色；未激活用暗淡灰蓝。
        Color colorAt(float offset = 0f) => max ? BondColors.Prismatic(time, offset) :
            active ? BondColors.Tier(leading.Kind, leading.Tier) : new Color(118, 132, 142);

        float bob = MathF.Sin(time * 1.4f) * 3f;
        var core = c + new Vector2(0f, bob);

        // 选中剑位与核心的连线和流动光点，保留选中反馈。
        if (selected < 10)
        {
            Vector2 toward = (chosen.Center.ToVector2() - c).SafeNormalize(Vector2.UnitY);
            Line(c + toward * (ringR+12f), chosen.Center.ToVector2() - toward * 30f, colorAt() * .5f, 1.4f);
            for (int i = 0; i < 3; i++)
            {
                float p = (time * .35f + i / 3f) % 1f;
                var point = Vector2.Lerp(c + toward * (ringR+14f), chosen.Center.ToVector2() - toward * 30f, p);
                Glow(point, 4f, colorAt(), MathF.Sin(p * MathHelper.Pi) * .7f);
            }
        }

        // 外发光。
        Glow(core, max ? 118f : 96f, colorAt(), max ? .34f : .24f);

        // 阶段环：木质 5 段、矿石 6 段，都从顶部开始顺时针；段数与各自档位上限一致。
        int segments = leading.Kind == BondKind.Wood ? 5 : 6;
        for (int i = 0; i < segments; i++)
        {
            float a0 = -MathHelper.PiOver2 + MathHelper.TwoPi * i / segments;
            float a1 = -MathHelper.PiOver2 + MathHelper.TwoPi * (i + 1) / segments;
            bool lit = leading.Tier >= i + 1;
            Arc(core, ringR, a0 + .07f, (a1 - a0) - .14f,
                lit ? colorAt(i / (float)segments) * .85f : new Color(52, 62, 72) * .6f, lit ? 2.4f : 1.4f);
            Vector2 node = core + a0.ToRotationVector2() * ringR;
            ArmoryDiamond(node, lit ? 5.5f : 4f, lit ? colorAt(i / (float)segments) : new Color(78, 90, 100) * .6f, 1.2f);
        }

        // 核心内圈与族徽。
        Arc(core, 30f*coreScale, 0f, MathHelper.TwoPi, colorAt() * .55f, 1.4f);
        SoulGlyph glyph = !active ? SoulGlyph.Core :
            leading.Kind == BondKind.Wood ? SoulGlyph.Leaf : SoulGlyph.Ingot;
        Glyph(glyph, core, 42f*coreScale, colorAt());

        // 满羁绊时，环外旋转的棱彩光点。
        if (max)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = time * .5f + i * MathHelper.TwoPi / 8f;
                Vector2 p = core + a.ToRotationVector2() * (ringR + 12f + MathF.Sin(time * 1.6f + i) * 3f);
                Glow(p, 4f, BondColors.Prismatic(time, i / 8f), .9f);
            }
        }

        // 文字：族名、阶段、数量；未激活时不显示具体族名。
        CenterText(active ? leading.DisplayName : "等 待 觉 醒", new(c.X, core.Y - ringR - 18f), colorAt(), .62f);
        if(compactCore)
            CenterText($"{leading.StageName} · {leading.Count}/{leading.MaxCount}",new(c.X,core.Y+ringR+12f),colorAt(),.61f);
        else
        {
            CenterText(leading.StageName, new(c.X, core.Y + 70f), colorAt(), .74f);
            CenterText($"{leading.Count} / {leading.MaxCount}", new(c.X, core.Y + 90f),max ? colorAt() : InfoWhite, .66f);
        }
    }

    // 剑位流入中央羁绊核心的流光；按事件播放，支持快速连续放入。
    private static void DrawCoreFlow(SoulItem soul, SoulArmoryLayout layout)
    {
        if (coreFlows.Count == 0) return;
        Vector2 end = layout.Center(soul.VisibleSupportCount, armoryScroll);
        var bonds = BondSnapshot.Evaluate(soul);
        var leading = BondSnapshot.Leading(bonds);
        // 流光沿用当前羁绊色，与核心呼应；满羁绊用棱彩。
        Color bondColor = leading.IsMax ? BondColors.Prismatic((float)UiTime) :
            leading.Active ? BondColors.Tier(leading.Kind, leading.Tier) : Accent;

        const float duration = .7f;
        for (int i = coreFlows.Count - 1; i >= 0; i--)
        {
            CoreFlowEvent ev = coreFlows[i];
            float age = (float)(UiTime - ev.At);
            if (age < 0f) continue; // 延迟事件尚未开始。
            if (age > duration) { coreFlows.RemoveAt(i); continue; }
            Vector2 start = layout.Slot(ev.Slot, soul.VisibleSupportCount, armoryScroll).Center.ToVector2();
            if (!layout.Stage.Contains(start.ToPoint())) continue;
            // 加速曲线表现被吸入核心。
            float t = MathF.Pow(age / duration, 1.7f);
            Vector2 Position(float p)
            {
                Vector2 linear = Vector2.Lerp(start, end, p);
                Vector2 dir = (end - start).SafeNormalize(Vector2.UnitY);
                Vector2 tangent = new(-dir.Y, dir.X); // 顺时针切向，统一弯曲方向。
                return linear + tangent * MathF.Sin(p * MathHelper.Pi) * 16f;
            }
            Vector2 prev = Position(Math.Max(0f, t - .4f));
            for (int s = 1; s <= 18; s++)
            {
                float p = MathHelper.Lerp(Math.Max(0f, t - .4f), t, s / 18f);
                Vector2 next = Position(p);
                float f = s / 18f;
                Line(prev, next, bondColor * (.45f + f * .55f), 1.5f + f * 3f);
                prev = next;
            }
            Vector2 headPos = Position(t);
            Glow(headPos, 14f, bondColor, .9f);
            Glow(headPos, 6f, Color.White, 1f);
        }
    }
}
