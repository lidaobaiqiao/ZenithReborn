using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// 特效逐项调试总面板，共 61 项（10+18+26+7，实时按数组长度统计），分四组依次遍历：
//   1) B1~B10  十个渲染分支总闸
//   2) B2-1~B6-3  五个分支（B2~B6）各自拆出的内部构成项，共 18 项
//   3) P01~P26  主飞剑带面的构成项
//   4) B7-1~B7-7  玻璃屏幕滤镜的构成项
// F7 下一项，F8 上一项，F9 切换当前项，F10 全关，Shift+F10 全开，Ctrl+F10 回基线。
public class VfxPartDebug : ModSystem
{
    private static int index;
    private static int BranchCount => VfxIsolation.Branches.Length;
    private static int DetailCount => VfxIsolation.Details.Length;
    private static int PartCount => VfxIsolation.Parts.Length;
    private static int GlassCount => VfxIsolation.GlassParts.Length;
    private static int Total => BranchCount + DetailCount + PartCount + GlassCount;

    // P01~P26 与 B7-1~B7-7 共 33 项当前没有 shader 消费方（打包的 .fx 未声明对应 uniform，
    // 生产代码也没有 PartValue/GlassPartValues 调用方）。给它们加后缀，让操作者一眼看出按下没用。
    // Branches 那 10 项是真生效的绘制前判断，不加后缀。
    private const string UnwiredSuffix = "（未接线）";

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu) return;
        // 聊天框、告示牌、箱子命名时把按键让给输入框，避免打字触发切换
        if (Main.drawingPlayerChat || Main.editSign || Main.editChest) return;
        KeyboardState now = Main.keyState, before = Main.oldKeyState;
        if (Pressed(now, before, Keys.F7)) { index = (index + 1) % Total; Report(); }
        if (Pressed(now, before, Keys.F8)) { index = (index - 1 + Total) % Total; Report(); }
        if (Pressed(now, before, Keys.F9)) { Toggle(index); Report(); }
        if (!Pressed(now, before, Keys.F10)) return;
        if (now.IsKeyDown(Keys.LeftShift) || now.IsKeyDown(Keys.RightShift))
        {
            VfxIsolation.SetAllBranches(true);
            VfxIsolation.SetAllDetails(true);
            VfxIsolation.SetAllParts(1f);
            VfxIsolation.SetAllGlassParts(1f);
            Main.NewText(VfxIsolation.PureCodeOnly?"纯代码项全开；贴图、原版粒子及屏幕采样项保持禁用":"全部构成打开", Color.LightGreen);
        }
        else if (now.IsKeyDown(Keys.LeftControl) || now.IsKeyDown(Keys.RightControl))
        {
            VfxIsolation.ResetAll();
            Main.NewText(VfxIsolation.PureCodeOnly?"恢复全部允许的纯代码项":"恢复完整材质预设：61项开启，包含贴图与屏幕效果", Color.Yellow);
        }
        else
        {
            VfxIsolation.SetAllBranches(false);
            VfxIsolation.SetAllDetails(false);
            VfxIsolation.SetAllParts(0f);
            VfxIsolation.SetAllGlassParts(0f);
            Main.NewText("全部关闭：画面应为空，只剩剑本体贴图", Color.Orange);
        }
    }

    private static bool Pressed(KeyboardState now, KeyboardState before, Keys key)
        => now.IsKeyDown(key) && !before.IsKeyDown(key);

    private static void Toggle(int position)
    {
        if (position < BranchCount)
        {
            var branch = VfxIsolation.Branches[position];
            if(!branch.Allowed){Main.NewText("纯代码档禁止开启："+branch.Label,Color.Orange);return;}
            branch.Set(!branch.Get());
            return;
        }
        position -= BranchCount;
        if (position < DetailCount)
        {
            var detail = VfxIsolation.Details[position];
            if(!detail.Allowed){Main.NewText("纯代码档禁止开启："+detail.Label,Color.Orange);return;}
            detail.Set(!detail.Get());
            return;
        }
        position -= DetailCount;
        if (position < PartCount)
        {
            if(!VfxIsolation.Parts[position].Allowed){Main.NewText("纯代码档禁止开启："+VfxIsolation.Parts[position].Label,Color.Orange);return;}
            VfxIsolation.PartValues[position] = VfxIsolation.PartValues[position] > 0.5f ? 0f : 1f;
            return;
        }
        position -= PartCount;
        if(VfxIsolation.PureCodeOnly){Main.NewText("纯代码档禁止开启屏幕滤镜构成",Color.Orange);return;}
        VfxIsolation.GlassPartValues[position] = VfxIsolation.GlassPartValues[position] > 0.5f ? 0f : 1f;
    }

    // 每次切换都把当前项与开关状态打到聊天栏，不用记自己在第几项
    private static void Report()
    {
        string label;
        bool on;
        int position = index;
        if (position < BranchCount)
        {
            var branch = VfxIsolation.Branches[position];
            label = branch.Label; on = branch.Get();
        }
        else if ((position -= BranchCount) < DetailCount)
        {
            var detail = VfxIsolation.Details[position];
            label = detail.Label; on = detail.Get();
        }
        else if ((position -= DetailCount) < PartCount)
        {
            label = VfxIsolation.Parts[position].Label + UnwiredSuffix; on = VfxIsolation.PartValues[position] > 0.5f;
        }
        else
        {
            position -= PartCount;
            label = VfxIsolation.GlassParts[position].Label + UnwiredSuffix; on = VfxIsolation.GlassPartValues[position] > 0.5f;
        }
        Main.NewText($"[{index + 1}/{Total}] {label} → {(on ? "开" : "关")}　F7 下一项 / F8 上一项 / F9 切换 / F10 全关",
            on ? Color.LightGreen : Color.Gray);
    }
}
