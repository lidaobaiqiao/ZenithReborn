using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 在物块与实体绘制之间统一画轨迹带面，剑体随后覆盖在上面。
public class SoulVisuals : ModSystem
{
    private readonly VertexPositionColorTexture[] vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxVertices];

    private static readonly Color[] woodColors = [new(112,202,78), new(150,219,247), new(74,213,196),
        new(58,174,118), new(193,218,52), new(142,93,213), new(219,78,112), new(237,124,65),
        new(244,180,224), new(239,184,101), new(176,111,239), new(120,242,210)];
    private static readonly Dictionary<int, Color> palette = new()
    {
        [ItemID.CopperShortsword]=new(230,145,91), [ItemID.CopperBroadsword]=new(201,116,72),
        [ItemID.TinShortsword]=new(183,198,197), [ItemID.TinBroadsword]=new(150,176,174),
        [ItemID.IronShortsword]=new(194,191,178), [ItemID.IronBroadsword]=new(163,163,154),
        [ItemID.LeadShortsword]=new(117,137,180), [ItemID.LeadBroadsword]=new(92,111,159),
        [ItemID.SilverShortsword]=new(216,231,244), [ItemID.SilverBroadsword]=new(183,209,232),
        [ItemID.TungstenShortsword]=new(143,201,164), [ItemID.TungstenBroadsword]=new(114,174,139),
        [ItemID.GoldShortsword]=new(255,220,97), [ItemID.GoldBroadsword]=new(239,187,59),
        [ItemID.PlatinumShortsword]=new(228,223,248), [ItemID.PlatinumBroadsword]=new(196,189,223),
        [ItemID.BoneSword]=new(226,211,161), [ItemID.IceBlade]=new(98,199,250),
        [ItemID.BladeofGrass]=new(154,230,60), [ItemID.EnchantedSword]=new(107,139,250),
        [ItemID.Starfury]=new(246,157,208), [ItemID.BeeKeeper]=new(245,193,54),
        [ItemID.Seedler]=new(117,204,91), [ItemID.TheHorsemansBlade]=new(255,132,42),
        [ItemID.InfluxWaver]=new(87,224,238), [ItemID.TerraBlade]=new(139,239,48),
        [ItemID.StarWrath]=new(251,210,113), [ItemID.Meowmere]=new(229,144,244),
        [ItemID.Terragrim]=new(101,218,249), [ItemID.Arkhalis]=new(159,166,244),
        [ItemID.CobaltSword]=new(80,146,248), [ItemID.PalladiumSword]=new(235,147,85),
        [ItemID.MythrilSword]=new(101,216,180), [ItemID.OrichalcumSword]=new(238,124,192),
        [ItemID.AdamantiteSword]=new(225,85,107), [ItemID.TitaniumSword]=new(166,187,218)
    };
    private static readonly Dictionary<int, Color> fallbackColors = new();
    public static Color Tint(int itemType)
    {
        int wood = WoodTypes.Index(itemType);
        if (wood >= 0) return woodColors[wood];
        if (palette.TryGetValue(itemType, out var color)) return color;
        if (fallbackColors.TryGetValue(itemType, out color)) return color;
        // 使用稳定物品身份计算颜色，避免进程或模组加载顺序改变未登记武器的配色。
        string identity = ItemLoader.GetItem(itemType)?.FullName ?? ItemID.Search.GetName(itemType) ?? itemType.ToString();
        uint hash = 2166136261;
        foreach (char c in identity) hash = unchecked((hash ^ c) * 16777619);
        color = Main.hslToRgb((hash & 65535) / 65536f, .58f + ((hash >> 16) & 255) / 255f * .23f,
            .57f + ((hash >> 24) & 255) / 255f * .14f);
        fallbackColors[itemType] = color;
        return color;
    }
    public override void PostDrawTiles()
    {
        if(Main.dedServ || Main.gameMenu) return;
        // 已准备好原版滤镜合成时，世界中保留干净背景；只在无法安全准备时画普通材质。
        if(SoulSceneRefraction.CompositingReady)return;
        // 复用已有的完整状态恢复入口，包含纹理、采样器和顶点绑定。
        // 材质颜色在实体下方绘制；背景折射交给原版屏幕滤镜的安全合成阶段。
        bool visible=SoulTrailEnding.HasActive;
        foreach(var projectile in Main.ActiveProjectiles)
            if(projectile.ModProjectile is SoulBlade){visible=true;break;}
        if(!visible)return;
        MartialSlashRenderer.DrawWithRestoredState(()=>
        {
            foreach(var projectile in Main.ActiveProjectiles)
                if(projectile.ModProjectile is SoulBlade blade)blade.DrawRibbonShader(vertices);
            SoulTrailEnding.DrawAll(vertices);
        });
    }
    public override void Unload()
    {
        fallbackColors.Clear();
    }
}
