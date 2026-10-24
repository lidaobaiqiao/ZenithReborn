using Microsoft.Xna.Framework;
using Terraria.ID;

namespace ZenithReborn;

// 本文件由 checks/emit_vfx_mask_data.py 依据 VfxLibrary 登记自动生成。
// 每行对应一把矿石剑：图集名、实测剑身几何、金属色场配方。
// 不要手工改这个文件，改配方请改 VfxLibrary 后重新生成。
public static class VfxMaskData
{
    // ItemId：物品ID；TipPx：剑尖到剑柄的画布像素长；PivotX：剑柄在画布上的X坐标；
    // Clear：色场底色；Primary/Secondary：主色与补色；
    // PrimaryOpacity/SecondaryOpacity：两层不透明度；PrimaryScroll/SecondaryScroll：滚动速度。
    public readonly record struct Row(int ItemId,float TipPx,float PivotX,Color Clear,Color Primary,Color Secondary,float PrimaryOpacity,float SecondaryOpacity,float PrimaryScroll,float SecondaryScroll,float LoopSeconds);
    public static readonly Row[] Rows =
    {
        new(ItemID.AdamantiteSword,94f,497f,new Color(177,58,41),new Color(240,84,60),new Color(148,40,28),0.46f,0.4f,8f,-5f,1.1f),
        new(ItemID.CobaltSword,96f,497f,new Color(51,93,195),new Color(72,132,255),new Color(38,68,182),0.46f,0.4f,9f,-6f,1.1f),
        new(ItemID.CopperBroadsword,60f,497f,new Color(193,101,46),new Color(255,138,62),new Color(176,82,38),0.46f,0.4f,7f,-4f,1.1f),
        new(ItemID.CopperShortsword,53f,497f,new Color(193,101,46),new Color(255,138,62),new Color(176,82,38),0.46f,0.4f,7f,-4f,1.1f),
        new(ItemID.GoldBroadsword,66f,497f,new Color(191,152,54),new Color(255,212,82),new Color(168,118,30),0.48f,0.42f,6f,-4f,1.1f),
        new(ItemID.GoldShortsword,53f,497f,new Color(191,152,54),new Color(255,212,82),new Color(168,118,30),0.48f,0.42f,6f,-4f,1.1f),
        new(ItemID.IronBroadsword,60f,497f,new Color(118,127,142),new Color(162,172,188),new Color(96,106,128),0.44f,0.38f,8f,-5f,1.1f),
        new(ItemID.IronShortsword,53f,497f,new Color(118,127,142),new Color(162,172,188),new Color(96,106,128),0.44f,0.38f,8f,-5f,1.1f),
        new(ItemID.LeadBroadsword,60f,497f,new Color(87,103,127),new Color(122,142,172),new Color(66,82,108),0.44f,0.38f,8f,-5f,1.1f),
        new(ItemID.LeadShortsword,53f,497f,new Color(87,103,127),new Color(122,142,172),new Color(66,82,108),0.44f,0.38f,8f,-5f,1.1f),
        new(ItemID.MythrilSword,97f,497f,new Color(57,171,143),new Color(82,230,190),new Color(38,148,128),0.46f,0.4f,9f,-6f,1.1f),
        new(ItemID.OrichalcumSword,90f,497f,new Color(180,77,118),new Color(240,112,160),new Color(158,50,100),0.46f,0.4f,8f,-5f,1.1f),
        new(ItemID.PalladiumSword,92f,497f,new Color(191,90,50),new Color(255,130,70),new Color(170,60,38),0.46f,0.4f,7f,-4f,1.1f),
        new(ItemID.PlatinumBroadsword,66f,497f,new Color(174,182,195),new Color(236,241,250),new Color(148,163,190),0.48f,0.4f,9f,-5f,1.1f),
        new(ItemID.PlatinumShortsword,53f,497f,new Color(174,182,195),new Color(236,241,250),new Color(148,163,190),0.48f,0.4f,9f,-5f,1.1f),
        new(ItemID.SilverBroadsword,63f,497f,new Color(164,173,188),new Color(222,231,245),new Color(138,153,178),0.48f,0.4f,9f,-5f,1.1f),
        new(ItemID.SilverShortsword,53f,497f,new Color(164,173,188),new Color(222,231,245),new Color(138,153,178),0.48f,0.4f,9f,-5f,1.1f),
        new(ItemID.TinBroadsword,60f,497f,new Color(126,144,165),new Color(172,192,214),new Color(104,126,158),0.44f,0.38f,8f,-5f,1.1f),
        new(ItemID.TinShortsword,53f,497f,new Color(126,144,165),new Color(172,192,214),new Color(104,126,158),0.44f,0.38f,8f,-5f,1.1f),
        new(ItemID.TungstenBroadsword,63f,497f,new Color(172,147,90),new Color(232,202,132),new Color(148,118,58),0.46f,0.4f,7f,-4f,1.1f),
        new(ItemID.TungstenShortsword,53f,497f,new Color(172,147,90),new Color(232,202,132),new Color(148,118,58),0.46f,0.4f,7f,-4f,1.1f),
    };
}
