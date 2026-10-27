namespace ZenithReborn;

// 剑影的伤害规则归断锋诅咒管理，独立于绘制亮度和拖尾材质。
public static class SoulEchoDamage
{
    public const float CursedMultiplier=.20f, ReleasedStep=.80f;
    public static float Multiplier(SoulItem soul,int unit)
    {
        if(unit<=0 || soul==null)return 1;
        unit=Math.Clamp(unit,1,SoulTrailUnits.Count-1);
        return soul.Curse(SoulCurse.Bluntness) switch {
            CurseStage.Blessed=>1,
            CurseStage.Released=>MathF.Pow(ReleasedStep,unit),
            _=>CursedMultiplier
        };
    }
}
