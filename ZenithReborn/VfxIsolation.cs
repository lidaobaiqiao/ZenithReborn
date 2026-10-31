using System;
using System.Linq;
using Terraria;

namespace ZenithReborn;

// 特效隔离总闸：所有渲染分支的开关集中在本文件，不再散落到各自的实现里。
// 逐项排查画面来源时，只改这里；生产环境应全部为 true。
public static class VfxIsolation
{
    // 用户已允许恢复贴图与屏幕材质；切回纯代码排查时仍由这一处统一限制输入。
    public static bool PureCodeOnly => false;

    // 当前迁移采用79材质；保留平面试验参数供其他材质接口使用。
    public static bool GlassPlaneOnly => false;
    public static float GlassOpacity(float normalOpacity) => normalOpacity;

    // ================= 一、渲染分支总闸 =================
    // 主飞剑拖尾带面
    public static bool SwordTrail = true;
    // 三个剑影（15/30/45 次更新前的残影）
    public static bool EchoBlades = !PureCodeOnly;
    // 剑尖星芒
    public static bool TipGlow = !PureCodeOnly;
    // 独立消散片：父弹幕销毁后的余迹
    public static bool TrailEnding = true;
    // 飞行表现入口：纯代码档只执行光照，不发射贴图粒子。
    public static bool FlyingParticles = true;
    // 刃缘碎光
    public static bool FlowSpark = !PureCodeOnly;
    // 玻璃屏幕折射滤镜
    public static bool GlassFilter = !PureCodeOnly;

    // 一键全开或全关，避免逐个漏改
    public static void SetAll(bool value)
    {
        SetAllBranches(value);
    }

    // 7 个分支开关的统一遍历入口：调试面板据此把它们和 25 个构成项放进同一个清单。
    // 用委托读写静态字段，避免再维护一份平行状态导致两边不一致。
    public readonly record struct Branch(string Label, Func<bool> Get, Action<bool> Set, bool UsesExternalInput=false)
    {
        public bool Allowed => !PureCodeOnly || !UsesExternalInput;
    }

    // 分支内部构成项同样用委托读写，热键面板与分支共用一套遍历逻辑
    public readonly record struct Detail(string Label, Func<bool> Get, Action<bool> Set, bool UsesExternalInput=false)
    {
        public bool Allowed => !PureCodeOnly || !UsesExternalInput;
    }

    public static readonly Branch[] Branches =
    {
        new("B1 主飞剑拖尾带面",  () => SwordTrail,      value => SwordTrail = value),
        new("B2 三个剑影（15/30/45）", () => EchoBlades,  value => EchoBlades = value, true),
        new("B3 剑尖星芒",        () => TipGlow,         value => TipGlow = value, true),
        new("B4 独立消散片",      () => TrailEnding,     value => TrailEnding = value),
        new("B5 飞行粒子与光照",  () => FlyingParticles, value => FlyingParticles = value),
        new("B6 刃缘碎光",        () => FlowSpark,       value => FlowSpark = value, true),
        new("B7 玻璃屏幕折射滤镜", () => GlassFilter,     value => GlassFilter = value, true),
    };

    // 回到当前输入策略下的全开预设，默认值和面板使用同一套元数据。
    public static void ResetAll() => ApplyDefaultPreset();

    public static void ApplyDefaultPreset()
    {
        SetAllBranches(true);
        SetAllDetails(true);
        ResetParts();
        SetAllGlassParts(1f);
    }

    // 兼容旧版隔离夹具；实际限制与默认状态仍以当前统一策略为准。
    public static void ApplyPureCodePreset() => ApplyDefaultPreset();

    public static void SetAllBranches(bool value)
    {
        for (int i = 0; i < Branches.Length; i++) Branches[i].Set(value && Branches[i].Allowed);
    }

    // ================= 二、带面构成逐项开关 P01~P25 =================
    // 每一项对应 ZRSwordFlow.fx 中一个独立的 float uPartXxx：1 = 生效，0 = 从画面摘除。
    // 全部为 1 时与生产形态完全一致（着色器里每一项都只是原有表达式乘自己）。
    // 程序构成默认全开并允许逐项切换，外部输入项保持禁用。
    // 顺序必须与 ZRSwordFlow.fx 的声明顺序一致，热键按此顺序逐项切换。
    public readonly record struct Part(string Uniform, string Label, float Default, bool UsesExternalInput=false)
    {
        public bool Allowed => !PureCodeOnly || !UsesExternalInput;
    }

    public static readonly Part[] Parts =
    {
        new("uPartShape",             "P01 主体形状（关掉整条带面消失）", 1f),
        new("uPartBend",              "P02 边界正弦弯曲", 0f),
        new("uPartThreads",           "P03 尾部拉丝细缕", 0f),
        new("uPartDepthGradient",     "P04 横向明暗梯度（刃侧亮、腹部暗）", 0f),
        new("uPartProcField",         "P05 程序色场低频噪声", 0f),
        new("uPartDissolve",          "P06 消融与回收前沿", 0f),
        new("uPartRim",               "P07 消融前沿发光", 0f),
        new("uPartCurvature",         "P08 双轴曲面法线", 1f),
        new("uPartBevel",             "P09 刃口倒角", 1f),
        new("uPartRazorCore",         "P10 刃核白线", 0f),
        new("uPartRazorHalo",         "P11 刃线中层光晕", 0f),
        new("uPartRazorMist",         "P12 刃线外层弥散", 0f),
        new("uPartInnerRim",          "P13 内缘线", 0f),
        new("uPartBodyTint",          "P14 剑身色铺底", 0f),
        new("uPartBandReflection",    "P15 局部光面边缘", 0f),
        new("uPartGlassBase",         "P16 玻璃基底不透明度", 1f),
        new("uPartGgxCore",           "P17 GGX 高光核", 0f),
        new("uPartSoftLobe",          "P18 宽高光包络", 0f),
        new("uPartWhiteShift",        "P19 高光亮色过渡", 0f),
        new("uPartHighlightNoise",    "P20 噪声调制高光（需噪声图）", 0f, true),
        new("uPartDispersion",        "P21 色散（需背景透射才可见）", 0f, true),
        new("uPartSceneTransmission", "P22 背景透射", 1f, true),
        new("uPartSsr",               "P23 屏幕空间反射", 1f, true),
        new("uPartStripe",            "P24 虚拟光面强度", 0f),
        new("uPartEmission",          "P25 虚拟光源亮度", 0f),
        new("uPartRidges",            "P26 液态玻璃条带起伏（只驱动折射）", 0f),
    };

    // 与 Parts 一一对应的当前值，下标即序号
    public static readonly float[] PartValues = Parts.Select(part => part.Allowed?part.Default:0f).ToArray();

    // 按 uniform 名取当前值：供不走 Parts 循环的着色器入口（如材质核心）读取同一份开关状态。
    public static float PartValue(string uniform)
    {
        for (int i = 0; i < Parts.Length && i < PartValues.Length; i++)
            if (Parts[i].Uniform == uniform) return Parts[i].Allowed ? PartValues[i] : 0f;
        return 0f;
    }

    public static void SetAllParts(float value)
    {
        for (int i = 0; i < PartValues.Length; i++) PartValues[i] = Parts[i].Allowed ? value : 0f;
    }

    // 回到当前预设，全部允许的程序构成开启。
    public static void ResetParts()
    {
        for (int i = 0; i < PartValues.Length; i++) PartValues[i] = Parts[i].Allowed?Parts[i].Default:0f;
    }

    // ================= 三、六个分支各自的构成项 =================
    // 每个分支拆到能单独开关的最小可见单元；外部输入依赖与分支分别登记。

    // B2 三个剑影
    public static bool EchoNear = !PureCodeOnly;       // B2-1 近位剑影（15 次更新前）
    public static bool EchoMid = !PureCodeOnly;        // B2-2 中位剑影（30）
    public static bool EchoFar = !PureCodeOnly;        // B2-3 远位剑影（45）
    public static bool EchoTint = !PureCodeOnly;       // B2-4 剑影向白染色 22%
    public static bool EchoBladeAlpha = !PureCodeOnly; // B2-5 亮度阶梯 0.75/0.50/0.25
    public static bool EchoSlotEnabled(int slot) => slot switch
    {
        1 => EchoNear,
        2 => EchoMid,
        3 => EchoFar,
        _ => false,
    };

    // B3 剑尖星芒
    public static bool TipHalo = !PureCodeOnly;   // B3-1 光晕（两笔）
    public static bool TipCore = !PureCodeOnly;   // B3-2 白核心（两笔）
    public static bool TipPulse = !PureCodeOnly;  // B3-3 15~45 脉动包络（关掉则全程常亮）
    public static bool TipOffset = !PureCodeOnly; // B3-4 贴剑尖偏移（关掉则贴剑心）

    // B4 独立消散片
    public static bool EndingShape = true;      // B4-1 带面本体
    public static bool EndingFadeCurve = true;  // B4-2 末端 6% 快速消失曲线
    public static bool EndingDissolve = true;   // B4-3 从尾部被吞噬

    // B5 飞行粒子
    public static bool FlyingDust = !PureCodeOnly; // B5-1 飞行粒子本体
    public static bool FlyingBladeLight = true;  // B5-2 剑身光照
    public static bool FlyingPlayerLight = true; // B5-3 玩家身上光照

    // B6 刃缘碎光
    public static bool SparkBody = !PureCodeOnly; // B6-1 碎片本体
    public static bool SparkCore = !PureCodeOnly; // B6-2 白色亮芯
    public static bool SparkMote = !PureCodeOnly; // B6-3 横向第二笔（十字）

    // 统一遍历：热键面板据此把 18 个构成项接在 7 个分支之后
    public static readonly Detail[] Details =
    {
        new("B2-1 近位剑影（15）", () => EchoNear, value => EchoNear = value, true),
        new("B2-2 中位剑影（30）", () => EchoMid, value => EchoMid = value, true),
        new("B2-3 远位剑影（45）", () => EchoFar, value => EchoFar = value, true),
        new("B2-4 剑影向白染色 22%", () => EchoTint, value => EchoTint = value, true),
        new("B2-5 剑影亮度阶梯", () => EchoBladeAlpha, value => EchoBladeAlpha = value, true),
        new("B3-1 星芒光晕", () => TipHalo, value => TipHalo = value, true),
        new("B3-2 星芒白核心", () => TipCore, value => TipCore = value, true),
        new("B3-3 星芒脉动包络", () => TipPulse, value => TipPulse = value, true),
        new("B3-4 星芒贴剑尖偏移", () => TipOffset, value => TipOffset = value, true),
        new("B4-1 消散片本体", () => EndingShape, value => EndingShape = value),
        new("B4-2 消散片消失曲线", () => EndingFadeCurve, value => EndingFadeCurve = value),
        new("B4-3 消散片尾部吞噬", () => EndingDissolve, value => EndingDissolve = value),
        new("B5-1 飞行粒子", () => FlyingDust, value => FlyingDust = value, true),
        new("B5-2 剑身光照", () => FlyingBladeLight, value => FlyingBladeLight = value),
        new("B5-3 玩家光照", () => FlyingPlayerLight, value => FlyingPlayerLight = value),
        new("B6-1 碎光本体", () => SparkBody, value => SparkBody = value, true),
        new("B6-2 碎光亮芯", () => SparkCore, value => SparkCore = value, true),
        new("B6-3 碎光横向第二笔", () => SparkMote, value => SparkMote = value, true),
    };

    public static void SetAllDetails(bool value)
    {
        for (int i = 0; i < Details.Length; i++) Details[i].Set(value && Details[i].Allowed);
    }

    // B7 玻璃屏幕滤镜：构成项在 ZRGlassScreen.fx 里，由 GlassScreenData.Apply 下发
    public static readonly Part[] GlassParts =
    {
        new("uGlassPartCoverage",      "B7-1 覆盖度（玻璃是否存在）", 1f),
        new("uGlassPartRefraction",    "B7-2 背景折射偏移", 1f),
        new("uGlassPartBlur",          "B7-3 粗糙度模糊", 1f),
        new("uGlassPartTransmittance", "B7-4 透射吸收", 1f),
        new("uGlassPartCoating",       "B7-5 高光合成", 1f),
        new("uGlassPartBloom",         "B7-6 泛光扩散", 1f),
        new("uGlassPartSpaceWarp",     "B7-7 刀光周围空间扭曲", 1f, true),
    };

    public static readonly float[] GlassPartValues = GlassParts.Select(part => PureCodeOnly?0f:part.Default).ToArray();

    public static void SetAllGlassParts(float value)
    {
        for (int i = 0; i < GlassPartValues.Length; i++) GlassPartValues[i] = PureCodeOnly ? 0f : value;
    }

}
