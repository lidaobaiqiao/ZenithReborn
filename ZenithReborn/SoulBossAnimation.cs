using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 单个 Boss 动态头像的图集信息：图集按96像素方格排列，保留原始 GIF 每帧时长。
public sealed record SoulBossAnimation(string Key, int Columns, int[] DurationsMs)
{
    // 图集里每一格的边长。
    public const int TileSize = 96;
    // 总帧数等于时长记录数。
    public int FrameCount => DurationsMs.Length;
    // 一次完整循环的总时长（毫秒）。
    public int TotalDurationMs
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < DurationsMs.Length; i++) sum += DurationsMs[i];
            return sum;
        }
    }
    // 按给定时间（毫秒）取当前循环帧；时间取模实现无限循环。
    public int FrameAt(double timeMs)
    {
        int total = TotalDurationMs;
        if (total <= 0) return 0;
        double t = timeMs % total;
        if (t < 0) t += total;
        int elapsed = 0;
        for (int i = 0; i < FrameCount; i++)
        {
            elapsed += DurationsMs[i];
            if (t < elapsed) return i;
        }
        return FrameCount - 1;
    }
    // 某一帧在图集里的96像素源矩形。
    public Rectangle FrameRectangle(int frame)
        => new((frame % Columns) * TileSize, (frame / Columns) * TileSize, TileSize, TileSize);
}
