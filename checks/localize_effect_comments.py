"""把特效链路文件的英文注释替换成中文（一次性整理，之后不再出现英文注释）。"""
import sys

PAIRS = {
    'ZenithReborn/OreFlyingSword.cs': [
        ("// Collection ages differ across members. Geometry must share one phase,\n    // including the closing gap, instead of rotating each blade by its own age.",
         "// 各成员的收集年龄不同，但几何必须共用同一个相位——闭合缺口也一样，\n    // 不能让每把剑按自己的年龄各转各的。"),
        ("// No custom DrawBehind queue: let the normal projectile pass invoke PreDraw.",
         "// 不另开 DrawBehind 队列，交给普通弹幕绘制流程调用 PreDraw。"),
        ("// Per-member ready ages correspond to one common release tick,\n            // after the final collected blade has finished settling.",
         "// 每把剑的就绪年龄对应同一个齐射时刻：等最后一把收集到的剑稳定下来再放。"),
        ("// Small supplemental hits scale defense proportionally; strong flying\n        // swords never amplify enemy armor beyond the normal 100% calculation.",
         "// 小额补充伤害按比例折算防御；强力飞剑不会把敌人护甲放大到正常100%计算之外。"),
        ("// Native local NPC immunity allows repeated hits every six frames.\n        // Never stop, retarget or consume penetration on contact.",
         "// 原版本地NPC免伤计时允许每6帧重复命中；接触时绝不停止、不换目标、不消耗穿透。"),
        ("// Mask-only pass: color field x animated Alpha behind the opaque blade.\n        // The legacy ribbon, halo and sheet wake are intentionally off.",
         "// 纯蒙版绘制：色场×动态Alpha画在不透明剑身背后；\n        // 旧的光带、光晕和片状拖尾已刻意关闭。"),
    ],
    'ZenithReborn/OreSwordEffects.cs': [
        ("// Client-only drawing helpers. No extra projectiles, combat state or blend-state changes.",
         "// 仅客户端使用的绘制辅助函数。不产生额外弹幕、不改战斗状态、不改混合状态。"),
        ("// Preserve the vanilla silhouette, alpha and relief. Remove the source\n    // sprite's competing hue once, then light it with the blade's own palette.",
         "// 保留原版轮廓、透明度和明暗层次；先把原贴图自带的杂色去掉，\n    // 再用这把剑自己的配色重新照亮。"),
        ("// Two lines, not three: a single body and a thin bright core. The wide\n            // halo layering is what turned the wake into a sheet.",
         "// 只画两层而不是三层：一条主体加一条细亮芯。\n            // 之前那层宽光晕正是把拖尾糊成一片的原因。"),
        ("// The blade keeps its own vanilla pixels at full opacity. It is the subject;\n        // effects only sit behind it. No recolour, no 60% wash.",
         "// 剑身保留自己的原版像素、完全不透明。剑才是主角，特效只在它背后；\n        // 不重新染色，也不再盖一层60%透明度的灰。"),
        ("// Anticipation is display-only while CanDamage is false. The launched\n        // blade stays centered on its real projectile/collision position.",
         "// 蓄势阶段只是视觉表现（此时 CanDamage 为 false）；\n        // 出剑后的剑身始终对准真实的弹幕/碰撞位置。"),
    ],
    'ZenithReborn/OreVisualRenderer.cs': [
        ("// One masked material connects wake, blade and leading tip.",
         "// 用同一套蒙版材质把拖尾、剑身和剑尖破空连成一个整体。"),
        ("// A narrow wake behind a crisp blade beats a wide glowing sheet.",
         "// 清晰的剑身配一条窄拖尾，远胜一张宽大的发光片。"),
    ],
    'ZenithReborn/MartialCrescentMaterial.cs': [
        ("// Adapter for HoCha113's ready-made MIT crescent material. The unchanged\n// shader, compiled effect, brush/noise and license live in Assets/ThirdParty.\n// This submits one standard textured quad; no new mesh/rendering framework.",
         "// HoCha113 现成 MIT 月牙材质的适配层。Shader、编译好的 Effect、笔刷/噪声和许可证\n// 都原样放在 Assets/ThirdParty 下；这里只提交一个标准四边形，不新建网格或渲染框架。"),
        ("// Erode inward from every edge at once, not just the tail. Push erosion\n        // early and near-linear so the whole outline starts irregularly\n        // shrinking immediately instead of waiting until the end to vanish.",
         "// 从每条边同时向内侵蚀，而不是只侵蚀尾部。侵蚀提前且接近线性，\n        // 让整个轮廓一开始就不规则地收缩，而不是等到最后一刻才消失。"),
        ("// Created on first draw, on the graphics thread. Never mutate the\n            // shared loaded Effect, nor the other mod's runtime shader instance.",
         "// 首次绘制时在图形线程创建。绝不修改共享的已加载 Effect，\n            // 也不碰另一个模组的运行时 Shader 实例。"),
        ("// One existing compiled pass: low base color plus rolling brush/noise and\n    // a moving erosion threshold. No extra white core, arc or V layer.",
         "// 只用一个现成的编译 Pass：低亮度底色 + 滚动的笔刷/噪声 + 移动的侵蚀阈值；\n    // 不额外加白芯、弧线或V形层。"),
    ],
    'ZenithReborn/OreWeaponEnding.cs': [
        ("// Mask-only pass: the legacy crescent sheet at the end of a swing is off.\n        // The fading blade ghost below carries the exit instead.",
         "// 纯蒙版模式：旧的挥砍收尾月牙片已关闭，收尾改由下面渐隐的剑影承担。"),
    ],
    'ZenithReborn/SoulFlowParticles.cs': [
        ("// Shared palette for the approved flowing material and its detached sparks.",
         "// 已认可的流动材质与它甩出的碎光共用的配色表。"),
        ("// A small emitter, not a particle framework: vanilla Dust owns allocation,\n// update, drawing and eviction. Only visual poses are sampled here.",
         "// 一个小型发射器，不是粒子框架：分配、更新、绘制和回收全部交给原版 Dust，\n// 这里只负责采样视觉姿态。"),
        ("// One small terminal accent, synchronized to the existing visual sweep.",
         "// 一次小型的收尾强调，与现有挥砍视觉同步。"),
        ("// Reuses the game's Dust pool and vanilla SharpTears sprite. No extra mod,\n// texture, shader, projectile, network packet or custom render pass is needed.",
         "// 复用游戏的 Dust 池和原版 SharpTears 贴图：不需要额外模组、贴图、Shader、\n// 弹幕、网络包或自定义渲染流程。"),
    ],
}

failed = 0
for path, pairs in PAIRS.items():
    text = open(path, encoding='utf-8').read()
    for old, new in pairs:
        if old not in text:
            print('MISS:', path, '|', old.split('\n')[0][:60])
            failed += 1
            continue
        text = text.replace(old, new, 1)
    open(path, 'w', encoding='utf-8').write(text)
    print('done:', path)
sys.exit(1 if failed else 0)
