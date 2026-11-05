namespace ZenithReborn;

// 铜、锡特性按有效寄宿槽判定；偏锋替换后所有武器特性正常触发，前四把矿石例外仅用于百炼飞剑（0.3.37 定稿保留），不改变铜锡特性的生效判定。
public static class OreSwordTraits
{
    private static bool Enabled(SoulItem soul,OreMaterial material)=>soul?.Attached==true &&
        soul.Active.Any() && soul.TraitItems.Any(i=>OreFamily.MaterialOf(i.type)==(int)material);
    public static bool EchoStacking(SoulItem soul)=>Enabled(soul,OreMaterial.Copper);
    public static bool StackDamage(SoulItem soul)=>Enabled(soul,OreMaterial.Tin);
}
