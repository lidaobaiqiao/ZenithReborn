namespace ZenithReborn;

// 铜、锡特性共用普通特性的有效槽过滤；不借前四把矿石例外绕过切换诅咒。
public static class OreSwordTraits
{
    private static bool Enabled(SoulItem soul,OreMaterial material)=>soul?.Attached==true &&
        soul.Active.Any() && soul.TraitItems.Any(i=>OreFamily.MaterialOf(i.type)==(int)material);
    public static bool EchoStacking(SoulItem soul)=>Enabled(soul,OreMaterial.Copper);
    public static bool StackDamage(SoulItem soul)=>Enabled(soul,OreMaterial.Tin);
}
