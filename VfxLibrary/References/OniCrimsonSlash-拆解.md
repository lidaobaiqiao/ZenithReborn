# OniCrimsonSlash 拆解：为什么别人做的刀光好看

来源：Calamity Overhaul 0.9209，作者 HoCha113，MIT。
文件：`ZenithReborn/Assets/ThirdParty/CalamityOverhaul/OniCrimsonSlash.fx`（309 行，中文注释自带设计说明）。
本文只提炼方法论，不复制代码。

---

## 一句话结论

它不是"一张贴图 + 一个透明度曲线"，而是**把每一条视觉特征都定义成了语义**：

| 语义 | 代码 | 作用 |
|---|---|---|
| 边缘语义 | `aSharp` 极窄平滑 / `aDark` 噪声撕裂 | 一边永远锐利（视觉锚点），一边有机破碎 |
| 时间语义 | `uSweep` 揭开 → `uFlash` 过曝 → `uErode` 溶解 | 出现、爆发、消亡三段各有一套规则 |
| 空间语义 | `uFarSel` / `uFarDim` | 远端压暗，制造纵深 |
| 材质语义 | 双层笔刷 + 域扭曲墨场 + 飞白 + 散锋 | 表面永远在动，不读作纯色块 |

没有一条参数是"随手调的"。

---

## 八个关键设计（带代码依据）

### 1. 不对称边缘——锋利感的真正来源

```hlsl
aSharp = smoothstep(1.06, 0.96, v);                    // 过渡只有 0.10 宽
aDark  = smoothstep(-0.10, 0.18, v + jag*(0.8+uErode*2.0) + (0.5-tear)*0.42);
```

- `v=1` 是外凸缘（剃刀线）：**过渡窄到 0.1**，几乎硬边
- `v<0` 是拖尾暗侧：**叠噪声 `jag` 撕裂**，且侵蚀越深越碎
- 结果：一侧是刀，一侧是烟

我们之前的蒙版两边对称、过渡均匀 → 所以看起来"钝"。

### 2. 厚度包络：两端收尖，峰值偏收笔端

```hlsl
env = sin(pow(uc, envPow) * PI);   // envPow：弧 1.35 / 直线 1.85
w   = uThick * pow(max(env,0), 0.72);
```

`pow(uc,1.35)` 让峰值落在 0.7 附近，不是 0.5——**重心偏向收笔端**，符合"甩出去"的直觉。

### 3. 扫掠揭示：不是整体出现，是沿刃依次揭开

```hlsl
edge    = uSweep * 1.10 - 0.04;
reveal  = smoothstep(edge+0.012, edge-0.055, uRaw);      // 从起笔向收笔推进
front   = exp(-pow((uRaw-edge)/0.05, 2)) * uFrontGlow;   // 前缘一条白热带
```

刀光是"长出来"的，不是"变透明"。前缘那道白热是速度感来源。

### 4. 双层侵蚀：两条规则取先到者

```hlsl
eLife = uErode * 1.18 - (1.0 - h) * 0.30;   // 从暗侧向剃刀线推进
eTail = uTailErode * 1.35 - uc * 1.05;      // 从起笔端向后蒸发
eTh   = max(eLife, eTail);
survive = smoothstep(eTh-0.02, eTh+0.12, eN);   // 噪声阈值 → 有机溶解
burn    = smoothstep(eTh-0.16, eTh-0.02, eN) * (1-survive);  // 溶解边缘灼烧环
```

**`max` 是精髓**：两个方向同时吃，谁先到谁吃。所以形状消失得不规则、不重复。

### 5. 剃刀线最后死

```hlsl
eLife = uErode * 1.18 - (1.0 - h) * 0.30;   // h=0（刃侧）阈值高 0.30
```

**刃线比拖尾晚 0.3 才被侵蚀**——整体散尽了，那道亮线还在。这一条直接决定"够不够狠"。

### 6. 散锋：起笔端切 4 条锋毫，各自蒸发

```hlsl
lane     = min(floor(vc*4), 3);
laneN    = noise(lane*0.25 + uSeed*11.3) - 0.5;
eTail   += laneN * 0.34 * uSplitTail * tailZone;   // 每缕偏移不同
groove    = 1 - smoothstep(0.05, 0.17, min(laneFrac, 1-laneFrac));  // 带间刻槽
```

尾部不是"一块"在散，是**四条独立的毫**在不同时刻散。

### 7. 飞白：只骚扰暗侧

```hlsl
fbZone = smoothstep(0.40,0.72,uc) * smoothstep(0.20,0.45,h);   // 后段 + 暗侧
alpha *= 1 - feiBai * fbZone * uFeiBai * 0.85;
```

刃侧（h<0.2）**完全不碰**。所以锐利感和破碎感共存，不互相打架。

### 8. 透密调制：暗部不许读作实心黑

```hlsl
floorGloss = saturate(0.42 + streak*0.95);
floorInk   = saturate(0.25 + inkq*0.95 + streak*0.22);
alpha *= lerp(..., 1.0, uFlash*0.40);   // 白闪帧抬下限
```

暗色部分透出背景，才有"能量"而不是"塑料"。

---

## 对照：我们之前的做法缺什么

| 维度 | OniCrimsonSlash | 我们的蒙版 |
|---|---|---|
| 边缘 | 一侧锐利、一侧破碎 | 两侧对称、过渡柔和 |
| 出现 | 沿刃扫掠揭示 + 前缘白热 | 整体淡入 |
| 消失 | 双规则侵蚀（取 max）+ 刃线最后死 | 均匀 alpha 衰减 |
| 表面 | 双层笔刷 + 域扭曲 + 飞白 | 单一噪声滚动 |
| 尾部 | 4 条锋毫独立蒸发 | 一刀切收尖 |
| 暗部 | 透密（透出背景） | 半透明但发实 |

这就是"别人为什么好"——**它把每个像素都归因到了某个物理/笔法概念上**。

---

## 对粘性空腔的启示

用户要的"胶体里挥过的空腔"，本质是**侵蚀**，不是淡出。可以直接借这套语义：

1. **出现**：沿刃长扫掠揭示（`uSweep`），前缘白热 = 胶被推开的瞬间
2. **保持**：内侧暗、外侧亮（把 `h` 反过来用：外侧是"被排开的边缘"，内侧是"还没回填的空腔"）
3. **回填**：`uErode` 从外侧向内侧推进，**刃线最后合拢**——这就是"胶慢慢合上，但刀痕先散"
4. **尾迹**：起笔端先蒸发（胶的黏性让尾端拖得更久，可以反过来：尾端最后散）
5. **不对称**：迎风侧（运动方向）锐利，背风侧破碎——正好对应 K-H 不稳定

关键：**不要自己发明累积算法**。用 `MartialCrescentMaterial.Draw` 那套（已封装好 uSweep/uErode/uTailErode/uFlash），只把驱动源从"武技巨剑的姿态"换成"玩家武器的实际挥动轨迹"。

---

## 可复用清单

| 资源 | 位置 | 能否直接用 |
|---|---|---|
| OniCrimsonSlash.fx / .fxc | Assets/ThirdParty/CalamityOverhaul/ | ✅ 已适配（`MartialCrescentMaterial`） |
| SlashBrush01.rawimg | 同上 | ✅ 月牙笔刷 |
| NoiseSoft01.rawimg | 同上 | ✅ 噪声源 |
| uInk 水墨件套 | 同一 fx，`uInk>0` 时启用 | ⏳ 未启用，是一整套备选风格 |
| uSplitTail 散锋 | 同上 | ⏳ 未启用 |
| 其他 fx（tmod 内） | CalamityOverhaul.tmod | ❌ 自定义压缩格式，解不开 |
