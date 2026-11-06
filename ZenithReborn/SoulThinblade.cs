using Terraria;

namespace ZenithReborn;

// 薄刃（第四道诅咒）：每把主飞剑在生成时各做一次有效性判定，结果存于弹幕实例并持续整个生命周期。
// 残缺飞剑照常生成、飞行、消失，只是不能形成有效命中。
public static class SoulThinblade
{
    // 第四道诅咒各阶段的飞剑有效概率：诅咒中 70%、已削弱 90%、解除/祝福 100%。
    public static float EffectiveChance(CurseStage stage) => stage switch {
        CurseStage.Cursed => .70f, CurseStage.Weakened => .90f, _ => 1f };

    // 每把飞剑生成时调用一次；返回 true 表示有效，false 表示残缺。
    // 只在拥有者客户端掷一次随机，结果随后经 SendExtraAI 同步，远端只读取不重掷。
    public static bool Roll(SoulItem soul) =>
        Main.rand.NextFloat() < EffectiveChance(soul.Curse(SoulCurse.Thinblade));
}
