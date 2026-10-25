using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 剑影「亮灭时间窗」与「真实历史年龄」的偏差实测，用途是确认与原版 933 保持一致。
// 采样位置按更新次数索引（SoulTrailUnits.Spacing = 15），可见度按 phase - index*Spacing 计算；
// 两者只在每次更新恰好推进 1 相位时相等，本项目每次更新推进 0.7~2（随距离变化），因此存在偏差。
// 该偏差是原版行为，不是缺陷：原版 AI_182_FinalFractal 同样以 t = localAI[0] - j（j = 15/30/45）
// 计算剑影渐入渐出，且 localAI[0] 增长率同为 Lerp(0.7, 2, GetLerpValue(900, 0, 速度 * 2))。
// 见 docs/原版天顶飞剑源码.md 第 111~113 行与第 400~405 行。
// 不要把这个偏差当成 bug 修掉，否则会偏离原版天顶剑的实际表现。
internal static class EchoPhaseProbe
{
    internal static string Run()
    {
        var lines = new List<string>();
        bool server = Main.dedServ, menu = Main.gameMenu;
        try
        {
            Main.dedServ = true; Main.gameMenu = false; Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            foreach (var p in Main.projectile) p.active = false;
            foreach (var n in Main.npc) n.active = false;
            var player = new Player { whoAmI = 0, active = true, position = new Vector2(1000, 1000) };
            Main.player[0] = player; player.ResetEffects();
            var soul = (SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached = true; soul.LegacyReleased = 63;
            soul.Hosts[0] = new Item(ItemID.CopperBroadsword); soul.RefreshStats();
            player.inventory[0] = soul.Item;
            foreach (float reach in new[] { 60f, 200f, 360f, 600f, 900f })
            {
                int id = soul.EmitBlade(player, soul.Hosts[0], 0, 1);
                var projectile = Main.projectile[id];
                var blade = (SoulBlade)projectile.ModProjectile;
                projectile.damage = projectile.originalDamage = blade.SpecialDamage = 100;
                blade.TargetOffset = new Vector2(reach, 0); blade.Vertical = 60; projectile.CritChance = 0;
                // 只统计真正推进相位并写入姿态历史的更新；发射前的等待帧不记录轨迹。
                var phaseAt = new List<float>();
                while (blade.MotionPhase < 61)
                {
                    float before = blade.MotionPhase;
                    blade.AI();
                    if (blade.MotionPhase > before) phaseAt.Add(blade.MotionPhase);
                }
                int last = phaseAt.Count - 1;
                float step = last >= 1 ? phaseAt[last] - phaseAt[last - 1] : 0f;
                lines.Add($"reach={reach:F0} step={step:F3} phase={blade.MotionPhase:F2} samples={phaseAt.Count} bodyCanDamage={blade.BodyCanDamage}");
                for (int echo = 1; echo < 4; echo++)
                {
                    int back = echo * SoulTrailUnits.Spacing;
                    bool available = blade.TryGetEchoPose(echo, out var center, out _, out float opacity);
                    float nominal = back;
                    // 该采样点写入时的相位，与当前相位之差才是这把剑影的真实年龄。
                    float actual = last >= back ? phaseAt[last] - phaseAt[last - back] : -1f;
                    float distance = available ? Vector2.Distance(center, projectile.Center) : -1f;
                    lines.Add($"  echo{echo}: available={available} nominal={nominal:F1} actual={actual:F2} drift={actual - nominal:F2} opacity={opacity:F3} distance={distance:F1}");
                }
                projectile.active = false;
            }
        }
        finally
        {
            foreach (var p in Main.projectile) p.active = false;
            foreach (var n in Main.npc) n.active = false;
            Main.dedServ = server; Main.gameMenu = menu;
        }
        return string.Join("\n", lines);
    }
}
