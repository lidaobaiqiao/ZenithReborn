using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using E = ZenithReborn.SwordEnvironment;

namespace ZenithReborn;

public static class EnvironmentRules
{
    public const int All=(1<<21)-1;
    public const float DomainRadius=960;
    public static int Bit(E e)=>(byte)e is >=1 and <=21?1<<((int)e-1):0;
    public static bool Has(int mask,E e)=>(mask&Bit(e))!=0;
    public static int Count(int mask)=>System.Numerics.BitOperations.PopCount((uint)(mask&All));
    public static int BlessingSlots(int mask)=>Count(mask)/4; // 4/8/12/16/20: five optional persistent mechanisms.
    public static float Shared(int mask)=>1+.2f*Count(mask);
    public static float Forest(int mask)=>Has(mask,E.Forest)?1.15f:1;
    public static readonly string[] MechanismNames={"","林荫庇护","岩层回声","地脉裂隙","狱火汲魂","星轨引力","风雪覆身","冰晶甲壳","海市蜃楼","流沙陷阵","潮汐回响","蜜露滋养","毒芽繁生","日轮机关","荧光孢群","腐蚀蔓延","噬魂回响","鲜血烙痕","血肉再生","极光棱镜","水晶共鸣","幽蓝封印"};
    private static readonly (string,float)[][] stats={[],
        [("Defense",6)],[("Size",.06f),("Knockback",.15f)],[("Penetration",4),("CritDamage",.06f)],
        [("Damage",.03f),("LeechPower",.08f)],[("Reach",.08f),("FlightSpeed",.06f)],
        [("Crit",2),("DebuffResistance",.1f)],[("Defense",4),("Shield",.08f)],
        [("Move",.05f),("Speed",.03f)],[("Penetration",3),("DebuffDuration",.12f)],
        [("Life",20),("KnockbackResistance",.15f)],[("Regen",1),("Healing",.08f)],
        [("Life",20),("Dot",.08f)],[("Reduction",.01f),("Knockback",.15f)],
        [("Dot",.08f),("DebuffResistance",.1f)],[("Damage",.03f),("Penetration",3)],
        [("Penetration",3),("DebuffDuration",.12f)],[("Life",20),("Damage",.03f)],
        [("Life",20),("Recovery",.1f)],[("Crit",2),("CritDamage",.06f)],
        [("CritDamage",.06f),("DebuffResistance",.1f)],[("Penetration",3),("Reduction",.01f)]};
    public static IReadOnlyDictionary<string,float> Values(E e)=>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,float>(stats[(int)e].ToDictionary(x=>x.Item1,x=>x.Item2));
    public static string StatName(string key)=>key switch {
        "Defense"=>"防御","Size"=>"武器尺寸","Knockback"=>"击退强度","Penetration"=>"护甲穿透",
        "CritDamage"=>"暴击伤害","Damage"=>"近战伤害","LeechPower"=>"汲取效力","Reach"=>"操控距离",
        "FlightSpeed"=>"辅助飞剑速度","Crit"=>"暴击率","DebuffResistance"=>"减益时间缩短","Shield"=>"冰甲容量",
        "Move"=>"移动速度","Speed"=>"攻击速度","DebuffDuration"=>"环境减益持续时间","Life"=>"最大生命",
        "KnockbackResistance"=>"击退抵抗","Regen"=>"生命/秒","Healing"=>"恢复效力","Dot"=>"环境持续伤害",
        "Reduction"=>"伤害减免","Recovery"=>"恢复等待缩短",_=>key};
    public static string StatsText(E e,float strength=1)=>string.Join(" · ",Values(e).Select(v=>
        StatName(v.Key)+" +"+(v.Value*strength*(v.Key is "Defense" or "Life" or "Regen" or "Penetration" or "Crit"?1:100)).ToString("0.##")+
        (v.Key is "Crit" or "CritDamage" or "Reduction"?"个百分点":v.Key is "Defense" or "Life" or "Regen" or "Penetration"?"":"%")));
    public static readonly string[] Descriptions={"",
        "护叶生长成熟后抵挡一次受击；森林使所有激活环境效果额外增加15%，无虚弱环境。",
        "主剑首击积攒震动，六次命中释放范围岩震。","连续命中叠加地脉破甲，六层引发岩裂。",
        "有效剑魂伤害按0.1%基础比例汲取生命，受吸血资源与禁疗约束。",
        "不同主剑交错命中点亮星轨，三次交错唤来星辉追击。","五次首击凝霜脆化，随后碎冰；普通敌人受到减速。",
        "凝结冰甲，受击时吸收部分伤害，并产生冰裂。","每三次主剑首击留下额外斩击蜃影，原攻击伤害完整保留。",
        "首击留下流沙，砂刃周期性伤害区域内敌人。","八次首击掀起往返潮波，沿途穿透。",
        "战斗积攒蜜露，受伤后消耗，四秒内持续恢复生命；恢复效力强化剑魂治疗与药水。",
        "种下毒芽，不同主剑促进成熟，绽放毒花。","十二次首击点亮日轮，释放定向机关光束。",
        "孢子场周期性伤害并强化本魂施加的持续伤害。","腐蚀逐步破甲，并传播给附近敌人。",
        "六次首击剥离魂影，对敌人发动追魂攻击。","主剑反复撕裂伤痕，造成持续流血。",
        "受伤后逐步修复本次损失生命的一部分。","超过100%的暴击率，每溢出1个百分点基础转为0.5个百分点爆伤。",
        "不同主剑凝结晶簇，四次共鸣后晶爆。","不同主剑编织幽蓝锁链，四把汇聚封印斩。"};
    public static Color Color(E e)=>e switch {
        E.Forest=>new(108,210,106),E.Underground or E.Caverns=>new(190,156,108),E.Underworld=>new(255,104,62),
        E.Space=>new(174,175,255),E.Snow or E.Ice=>new(125,220,255),E.Desert or E.UndergroundDesert=>new(245,206,105),
        E.Ocean=>new(75,175,245),E.Jungle=>new(241,188,69),E.UndergroundJungle=>new(116,235,65),
        E.JungleTemple=>new(239,180,70),E.GlowingMushroom=>new(96,135,255),E.Corruption or E.UndergroundCorruption=>new(168,95,218),
        E.Crimson or E.UndergroundCrimson=>new(232,75,101),E.Hallow or E.UndergroundHallow=>new(232,158,248),_=>new(102,208,229)};
    public static int Natural(Player p)
    {
        int mask=0;void Add(E e,bool yes){if(yes)mask|=Bit(e);}
        bool deep=p.ZoneDirtLayerHeight || p.ZoneRockLayerHeight;
        // 群系优先于深度：有群系时地下层与洞穴层不成立。
        // 丛林地下只有地下丛林，洞穴层不在那里生效；地下腐化、地下沙漠、地牢同理。
        bool biome=p.ZoneJungle||p.ZoneCorrupt||p.ZoneCrimson||p.ZoneHallow||p.ZoneSnow
                 ||p.ZoneDesert||p.ZoneGlowshroom||p.ZoneDungeon||p.ZoneLihzhardTemple;
        Add(E.Forest,p.ZoneOverworldHeight && p.ZonePurity);Add(E.Underground,p.ZoneDirtLayerHeight && !biome);
        Add(E.Caverns,p.ZoneRockLayerHeight && !biome);Add(E.Underworld,p.ZoneUnderworldHeight);Add(E.Space,p.ZoneSkyHeight);
        Add(E.Snow,p.ZoneSnow && !deep);Add(E.Ice,p.ZoneSnow && deep);
        Add(E.Desert,p.ZoneDesert && !deep);Add(E.UndergroundDesert,p.ZoneUndergroundDesert || (p.ZoneDesert && p.ZoneDirtLayerHeight));
        Add(E.Ocean,p.ZoneBeach);Add(E.Jungle,p.ZoneJungle && !deep);Add(E.UndergroundJungle,p.ZoneJungle && deep);
        Add(E.JungleTemple,p.ZoneLihzhardTemple);Add(E.GlowingMushroom,p.ZoneGlowshroom);
        Add(E.Corruption,p.ZoneCorrupt && !deep);Add(E.UndergroundCorruption,p.ZoneCorrupt && deep);
        Add(E.Crimson,p.ZoneCrimson && !deep);Add(E.UndergroundCrimson,p.ZoneCrimson && deep);
        Add(E.Hallow,p.ZoneHallow && !deep);Add(E.UndergroundHallow,p.ZoneHallow && deep);Add(E.Dungeon,p.ZoneDungeon);
        return mask;
    }
    public static float Adaptation(E e,int terrain)
    {
        bool Any(params E[] es)=>es.Any(x=>Has(terrain,x));
        bool weak=e switch {
            E.Forest=>false,
            E.Underground or E.Caverns=>Any(E.Space),E.Space=>Any(E.Underground,E.Caverns),
            E.Underworld=>Any(E.Ice,E.Snow,E.Ocean),E.Snow or E.Ice=>Any(E.Desert,E.UndergroundDesert,E.Underworld),
            E.Desert or E.UndergroundDesert=>Any(E.Ice,E.Snow,E.Ocean),E.Ocean=>Any(E.Desert,E.Underworld),
            E.Jungle or E.UndergroundJungle or E.GlowingMushroom=>Any(E.Desert,E.Underworld),
            E.Corruption or E.UndergroundCorruption or E.Crimson or E.UndergroundCrimson=>Any(E.Hallow,E.UndergroundHallow),
            E.Hallow or E.UndergroundHallow=>Any(E.Corruption,E.UndergroundCorruption,E.Crimson,E.UndergroundCrimson),_=>false};
        return weak?.5f:1;
    }
}

public partial class SoulItem
{
    public int EnvironmentCollection,EnvironmentBlessings;
    public E DomainEnvironment;
    public void RegisterEnvironments()
    {
        if(Attached)foreach(var tag in InspectEnvironmentAttributes().SelectMany(x=>x.Attributes))EnvironmentCollection|=EnvironmentRules.Bit(tag.Id);
        EnvironmentCollection&=EnvironmentRules.All;
    }
    public bool SetEnvironmentDomain(E e)
    {if(e!=E.None && !EnvironmentRules.Has(EnvironmentCollection,e))return false;DomainEnvironment=e;return true;}
    public bool ToggleEnvironmentBlessing(E e)
    {
        int bit=EnvironmentRules.Bit(e);if(bit==0 || (EnvironmentCollection&bit)==0)return false;
        if((EnvironmentBlessings&bit)!=0){EnvironmentBlessings&=~bit;return true;}
        if(EnvironmentRules.Count(EnvironmentBlessings)>=EnvironmentRules.BlessingSlots(EnvironmentCollection))return false;
        EnvironmentBlessings|=bit;return true;
    }
    public void NormalizeEnvironments()
    {
        EnvironmentCollection&=EnvironmentRules.All;EnvironmentBlessings&=EnvironmentCollection;
        if(!EnvironmentRules.Has(EnvironmentCollection,DomainEnvironment))DomainEnvironment=E.None;
        while(EnvironmentRules.Count(EnvironmentBlessings)>EnvironmentRules.BlessingSlots(EnvironmentCollection))
            EnvironmentBlessings&=EnvironmentBlessings-1;
    }
}
