namespace ZenithReborn;

// 武器族归属的唯一来源：tooltip 的「所属种族」行、图鉴或其他展示都从这里取，避免各处各写一套名册。
// 只读查询，不读写任何战斗状态、不缓存寄宿结果，任何界面调用都安全。
// 拼接顺序固定为 飞刃族 → 神圣天堂族 → 剑灵 → 木质族 → 矿石族，
// 多族武器（例如珍珠木剑同时是木质与神圣天堂）保证显示顺序与文本稳定。
public static class WeaponFamilyTags
{
    // 族名，顺序即上面的固定顺序；新增种族时在这里加一项，不要在其他文件里另写判断。
    public static string Of(int type)
    {
        if(type<=0)return "";
        var names=new List<string>(5);
        if(FlyingBladeFamily.IsMember(type))names.Add("飞刃族");
        if(HolyHeavenFamily.IsMember(type))names.Add("神圣天堂族");
        if(LivingArmamentFamily.IsMember(type))names.Add("剑灵");
        if(WoodTypes.IsWood(type))names.Add("木质族");
        // 矿石族以 OreFamily 的材质映射为准：它同时覆盖肉前短剑/阔剑与肉后六矿，
        // HardOreTypes 只列肉后六矿，单独使用会漏掉肉前八种。
        if(OreFamily.IsOre(type))names.Add("矿石族");
        return names.Count==0?"":string.Join("、",names);
    }

    // 是否属于至少一个已实装种族；无族的普通近战武器返回 false，展示层据此决定是否加行。
    public static bool IsAny(int type)=>Of(type).Length>0;

    // 界面与 tooltip 共用的说明行；不属于任何族时返回空串，调用方据此跳过这一行。
    // 传入 soul 时会补一句"为什么没生效"——族成员寄宿了但条件没满足时，不说明会被当成功能坏了。
    public static string Line(int type,SoulItem soul=null)
    {
        string names=Of(type);
        if(names.Length==0)return "";
        return "所属种族："+names+ActivationHint(type,soul);
    }

    // 飞刃族的激活权在名册第1把（蓝相位剑）。寄宿了族内其他剑却没开族时，明确说出差哪一把。
    private static string ActivationHint(int type,SoulItem soul)
    {
        if(soul==null || !soul.Attached)return "";
        if(FlyingBladeFamily.IsMember(type) && !FlyingBladeFamily.Activated(soul))
            return "（未激活 · 需再寄宿蓝相位剑）";
        return "";
    }
}
