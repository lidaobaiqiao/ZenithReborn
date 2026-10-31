using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// 材质质量档位：同屏剑多了以后自动降低细节，避免 20 把剑同屏掉帧。
// 只调材质参数，不改攻击、轨迹或任何玩法数值。
public static class MaterialQuality
{
    public enum Level{Full,Medium,Low}

    // 阈值：<=8 把满质量，9~16 把中等，>16 把低
    private const int MediumThreshold=8;
    private const int LowThreshold=16;
    private static Level level=Level.Full;
    private static ulong stamp=ulong.MaxValue;

    public static Level Current
    {
        get{Refresh();return level;}
    }

    // 厚度压缩：折射位移按比例缩小，低档几乎不再弯折背景
    public static float ThicknessScale=>level switch{Level.Low=>.50f,Level.Medium=>.75f,_=>1f};
    // 细节系数：法线强度、色散、屏幕反射同乘
    public static float DetailScale=>level switch{Level.Low=>.35f,Level.Medium=>.65f,_=>1f};
    // 低档不再读环境图，直接吃基础色
    public static bool UseEnvironmentMaps=>level==Level.Full;
    public static string Label=>level switch{Level.Low=>"低",Level.Medium=>"中",_=>"满"};

    private static void Refresh()
    {
        if(Main.dedServ)return;
        if(stamp==Main.GameUpdateCount)return;   // 同一帧只统计一次
        stamp=Main.GameUpdateCount;
        int swords=0;
        foreach(var projectile in Main.ActiveProjectiles)
            if(projectile.ModProjectile is OreFlyingSword or OreSkyfall or HardOreShot)swords++;
        level=swords>LowThreshold?Level.Low:swords>MediumThreshold?Level.Medium:Level.Full;
    }
}
