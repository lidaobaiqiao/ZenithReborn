# 特效素材库

制作资料目录，尚未接入游戏加载器。按用户明确的结构整理：**特效种类 → 剑名 → 动态蒙版**；底色保持对应分类，用引用共享素材。

```text
VfxLibrary/
  library.json                  分类与剑名登记
  INDEX.md                      中文浏览索引
  Masks/
    SwordBody/                  剑身一体组
      CopperBroadsword/         铜宽剑
        entry.json              拖尾、剑身流动、边缘、剑头破空的统一动画
        # 蒙版制作后放在此目录，按 entry 中的 clips 登记
    Rings/                      圆环
      CopperBroadsword/
        entry.json
    Particles/                  粒子
      CopperBroadsword/
        entry.json
      _Shared/                  可复用原版素材引用
  ColorFields/
    SwordBody/
      CopperBroadsword/
        entry.json              对应剑体的底色绑定
    Rings/
      CopperBroadsword/
        entry.json              可引用该剑的同一底色
    Particles/
      CopperBroadsword/
        entry.json
    _Shared/
      SlowFlowM1/
        entry.json              已有慢流动底色示例配方
  Examples/
    M1/
      composition.json          技术演示绑定，不是正式剑效
```

## 剑身一体组

拖尾、剑身内部流动、边缘动效、剑头破空共同占用**同一套坐标、同一套动态蒙版、一个底色引用**，制作时即为连贯整体。四项是覆盖范围说明，不是四个独立特效槽，不得各塞一层贴图后称为一体组。

同一个组可有 `appear`、`active`、`finish` 动画段，段间沿用同一画布、枢轴和底色相位；这是同一效果的时间阶段，不是叠加层。坐标以剑为局部空间，+X朝剑尖，具体画布和枢轴随实际素材登记；未制作前留空。

蒙版只表达Alpha形状和它的动态，不烘焙底色。底色负责缓慢变化的颜色场。合成后才得到最终可见效果。蒙版本身必须有形状动画，固定轮廓内挖洞或整体平移不等价。

## 分类与复用

- 圆环、粒子各自归类、各按剑名放蒙版，不强行塞进剑身一体组。
- 底色也按 `类别/剑名` 查找；多处复用时 `reference` 指向同一底色 ID，不复制纹理、配方或逐帧输出。圆环／粒子默认引用同名剑身底色，实际设计需要时可单独指定。
- `_Shared`是共享来源区，不是剑名。原版素材记录资源路径；第三方素材引用原文件与许可证／来源记录，不移动、不改名、不复制原有MIT资源。
- 剑名采用稳定ItemID名称，中文名用于索引；短剑与宽剑分开登记。天铸巨剑作为独立召唤形态登记，具体显示武器由运行时决定。
- `awaiting-art`表示只建位置、没有蒙版；`unassigned`表示底色未定；`example`只是技术示例；`source-reference`只是现有素材来源；`review-candidate`表示已有可审素材及预览，尚未获得美术认可。不能把目录、引用或技术示例说成正式剑效已经完成。
- `user-preferred-baseline`表示用户已明确偏好的当前视觉，后续围绕它收敛；不等同于全部武器完成或正式安装。`withdrawn`／`withdrawn-study`为已撤回研究，不作为当前入口。钛金剑现选R2，用户否定钩子方向，详见R2说明。

本轮先登记现有矿石组22种实际武器和天铸巨剑形态。其他剑今后按同样结构加入；不由本库改变种族登记或攻击规则。

21把矿石剑的剑身一体蒙版已按钛金剑R2配方家族批量制作并登记为`review-candidate`：每把按各自原版贴图自动配准剑尖与剑身宽度，16帧Alpha图集由隔离GPU导出，技术校验通过，未经美术审核，未接入游戏。天铸巨剑无原版贴图，其蒙版仍待制作。批量方法、实测参数与证据边界见[特效库M4](../docs/特效库M4-矿石剑批量蒙版.md)。

## 查找与检查

```powershell
python checks/vfx_library.py search 钛金
python checks/vfx_library.py search CopperBroadsword --kind mask --category SwordBody
python checks/vfx_library.py search SlowFlowM1 --kind color
python checks/vfx_library.py validate
```

底色示例和动态蒙版演示见[动态蒙版M1](../docs/动态蒙版M1-底色与形状分离.md)。本库不代表正式包已换用新资源，也不代表实战／多人已验证。
