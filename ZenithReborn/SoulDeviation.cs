using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 「偏锋」诅咒：每把飞剑在生成时各自取得一个固定的二维瞄准偏移，
// 只叠加到目标点，不改动飞行速度、椭圆轨迹半径、返回、碰撞与伤害。
public static class SoulDeviation
{
    // 三档偏移的唯一数值来源：实战计算与 UI 阶段说明都从这里取，禁止再各写一份。
    private const float Full = 48f, Half = 24f;

    // 仅供 Boss 解锁界面的未来阶段预览；当前阶段说明走 `MaxOffset(soul)`。
    // 本组只有两位 Boss，StageForCount(total=2) 的 count=1 直接判 Released、Weakened 永不可达，所以 Released 与 Weakened 都对应中间档。
    public static float MaxOffsetAt(CurseStage stage) =>
        stage == CurseStage.Cursed ? Full : stage == CurseStage.Blessed ? 0f : Half;

    // 偏锋诅咒的最大瞄准偏移（世界像素）。
    // 本组只有两位 Boss：StageForCount 在 total=2 时 count=1 先命中 Released，Weakened 永不可达；
    // 若按 CurseStage 分档，24 像素的中间档永远取不到。故按个人 Boss 记录数独立分三档：
    // 未击败任何一位 48 → 击败克苏鲁之脑 24 → 再击败拜月教邪教徒 0。
    // 不改 StageForCount（其他五道诅咒不受影响）；UI 阶段标签仍走原 6 诅咒规则；当前阶段说明取本函数的实战真值，
    // 偏锋强度按 Boss 记录数单独分档，两者口径不同是有意为之。
    public static float MaxOffset(SoulItem soul)
    {
        // 旧档该位为 1 表示已解除过切换诅咒，不能因为换机制就重新给老玩家上满诅咒。
        if ((soul.LegacyReleased & (1 << (int)SoulCurse.Switching)) != 0) return 0f;
        int count = SoulCurses.Count(soul, SoulCurse.Switching);
        return count <= 0 ? Full : count == 1 ? Half : 0f;
    }

    // 每把飞剑生成时调用一次：圆形分布、外侧略密，半径严格小于上限。
    // 随机数只在拥有者客户端生成弹幕时取一次，再经 SendExtraAI 同步；
    // 其他端通过 ReceiveExtraAI 只读回，绝不重新随机，保证两端一致且飞行期间固定。
    public static Vector2 NextOffset(SoulItem soul)
    {
        float max = MaxOffset(soul);
        if (max <= 0f) return Vector2.Zero;
        float u = Main.rand.NextFloat();                        // u∈[0,1) 均匀
        float radius = max * (0.35f + 0.65f * MathF.Sqrt(u)); // 外侧略密，且严格小于 max
        float angle = Main.rand.NextFloat(MathHelper.TwoPi);   // 角度 0~2π
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }
}
