namespace ZenithReborn;

// 封印与祝福的玩家说明集中维护，不在界面另写一套战斗数值。
public static partial class SoulCurses
{
    public static string Description(SoulItem soul,SoulCurse curse)
    {
        string effect=StageDescription(soul,curse,Stage(soul,curse));
        if((soul.LegacyReleased&(1<<(int)curse))!=0 && RecordedStage(soul,curse)<CurseStage.Released)
            effect+=" 旧档能力兼容解咒，不代表补齐Boss铭刻或祝福。";
        return effect;
    }
    // 当前说明和未来阶段共用同一来源；预测只读参数，不临时修改剑魂铭刻。
    public static string StageDescription(SoulItem soul,SoulCurse curse,CurseStage stage)
    {
        bool free=stage>=CurseStage.Released;
        string effect=curse switch{
            SoulCurse.Binding=>stage==CurseStage.Blessed?"御剑距离不再设上限，仍可鼠标自由选点。":free?"御剑距离最少到屏幕左右边界，保留正常鼠标选点。":"御剑距离最少能打到屏幕左右边界的中点；仍可鼠标自由选点。",
            SoulCurse.Switching=>free?"所有有效寄宿武器的特性正常生效，全部有效主剑可参与百炼飞剑。":"普通特性仅首把主剑生效；百炼飞剑例外允许前4把有效主剑计入矿材并各自首击积攒，其余主剑仍保留独立伤害。支援仍受普通特性限制。",
            SoulCurse.Sheathing=>free?"新轮不打断旧轮，主剑各自完成攻击。":"开始新轮时，旧轮主剑提前停止攻击并淡出；不锁攻击键。",
            SoulCurse.Barrier=>free?"主剑正常穿越实体地形。":$"每段连续实体地形可穿过{TerrainAllowanceAtStage(stage)}像素，超出则退出攻击；背景墙、平台不阻断。",
            SoulCurse.Solitude=>$"每轮最多{BladesAtStage(soul,stage)}把主剑，不足留空。",
            _=>stage==CurseStage.Blessed?"本体和三个剑影每次均造成100%伤害，按正常8帧间隔重复命中。":free?
                "本体100%；三个剑影逐次乘0.8，分别为80%、64%、51.2%伤害，按正常8帧间隔重复命中。":
                $"本体100%；后续三个剑影各造成20%伤害。同剑本趟对同目标最多{HitLimitAtStage(stage)}次直接命中，不限制沿途其他敌人。"};
        if(stage==CurseStage.Blessed)effect+=" "+(curse switch{
            SoulCurse.Binding=>"战斗约15秒蓄满御剑条，满条保留；按独立御剑键（默认O）手动开启10秒，视野翻倍，按命中距离最高独立增伤50%。",
            SoulCurse.Switching=>"百刃接力：不同寄宿主剑首击同一敌人接力，每层强化后续首击特性10%，最多5层；3秒断连清空，同剑帧伤不叠层。",
            SoulCurse.Sheathing=>"归鞘留锋：主剑完整往返后为自身存1次强化，下次登场首击释放50%自身伤害的短距离斩痕。",
            SoulCurse.Barrier=>"破障：每把主剑穿过实体后，下次命中附加4秒破甲（15护甲穿透），每剑只触发一次。",
            SoulCurse.Bluntness=>"连斩：剑影不再衰减，所有伤害单元保持所属主剑的全量伤害。",
            _=>""});
        if(stage==CurseStage.Blessed && curse==SoulCurse.Solitude)effect+=" 三王后4把，世花6、石巨人7、教徒8、月总10；只取此剑魂铭刻。";
        return effect;
    }
    public static string Requirement(SoulItem soul,SoulCurse curse)
    {
        int i=(int)curse;string marks=string.Join("、",Groups[i].Select((b,n)=>((soul.Seals&b)==b?"✓":"○")+BossNames[i][n]));
        return marks+"；"+(Groups[i].Length==2?"第一位解除，第二位祝福。":"第一位削弱，第二位解除，第三位祝福。");
    }
}
