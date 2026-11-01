using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 飞刃族四把核心武器：每把负责一层族级开关，必须有效寄宿在剑魂里才生效。
// 启刃：激活飞刃族的掷刃攻击。没有它，本族成员即使寄宿也不掷刃。
// 叠刃：族内有效武器每多一把，飞刃伤害再提高一档（有上限，避免全收集时失衡）。
// 通刃：允许非本族主剑也使用飞刃攻击方式（弹体仍用该剑自己的贴图与伤害）。
// 印刃：族特殊效果的总开关。当前只解锁"收回期保留伤害"，印记定稿后并入此处。
// 四把都是纯挥砍剑，与族内其他成员同构，因此一并计入族名册。
public static class FlyingBladeCores
{
    // 叠刃每把加成与计入上限：+3%/把，最多计 12 把（最高 +33%），全收集也封顶。
    public const int StepPercent=3;
    public const int StepCap=12;

    // 是否有效寄宿（主槽与支援槽都算，与矿石族阶梯口径一致）。
    private static bool Held(SoulItem soul,int type)=>soul.Attached && soul.TraitItems.Any(i=>i.type==type);

    public static bool Seeded(SoulItem soul)=>Held(soul,ModContent.ItemType<FlyingBladeSeed>());
    public static bool Tally(SoulItem soul)=>Held(soul,ModContent.ItemType<FlyingBladeTally>());
    public static bool Pass(SoulItem soul)=>Held(soul,ModContent.ItemType<FlyingBladePass>());
    public static bool Sigil(SoulItem soul)=>Held(soul,ModContent.ItemType<FlyingBladeSigil>());

    // 叠刃提供的伤害倍率；未寄宿叠刃时为 1，不改变原有数值。
    public static float DamageBonus(SoulItem soul)
    {
        if(!Tally(soul))return 1;
        int extra=Math.Clamp(FlyingBladeFamily.Count(soul)-1,0,StepCap-1);
        return 1+StepPercent/100f*extra;
    }
}

// 启刃：族的入场券。伤害最低，作用是让飞刃族开始工作。
public class FlyingBladeSeed : ModItem
{
    public override string Texture => "Terraria/Images/Item_"+ItemID.CopperBroadsword;
    public override void SetDefaults()
    {
        Item.width=32;Item.height=32;
        Item.damage=18;Item.DamageType=DamageClass.Melee;
        Item.useAnimation=20;Item.useTime=20;Item.useStyle=ItemUseStyleID.Swing;
        Item.knockBack=4f;Item.autoReuse=true;Item.useTurn=true;
        Item.rare=ItemRarityID.Blue;Item.value=Item.sellPrice(silver:20);
        Item.UseSound=SoundID.Item1;
    }
    public override void AddRecipes()=>CreateRecipe()
        .AddIngredient(ItemID.Gel,3).AddIngredient(ItemID.CopperBar,5)
        .AddTile(TileID.WorkBenches).Register();
}

// 叠刃：把族内武器数量换成飞刃伤害。单把时无收益，收得越多越明显。
public class FlyingBladeTally : ModItem
{
    public override string Texture => "Terraria/Images/Item_"+ItemID.IronBroadsword;
    public override void SetDefaults()
    {
        Item.width=32;Item.height=32;
        Item.damage=20;Item.DamageType=DamageClass.Melee;
        Item.useAnimation=22;Item.useTime=22;Item.useStyle=ItemUseStyleID.Swing;
        Item.knockBack=4f;Item.autoReuse=true;Item.useTurn=true;
        Item.rare=ItemRarityID.Green;Item.value=Item.sellPrice(silver:30);
        Item.UseSound=SoundID.Item1;
    }
    public override void AddRecipes()=>CreateRecipe()
        .AddIngredient(ItemID.Gel,3).AddIngredient(ItemID.IronBar,5)
        .AddTile(TileID.WorkBenches).Register();
}

// 通刃：把掷刃权限放开到非本族主剑，用于混族配装。
public class FlyingBladePass : ModItem
{
    public override string Texture => "Terraria/Images/Item_"+ItemID.SilverBroadsword;
    public override void SetDefaults()
    {
        Item.width=32;Item.height=32;
        Item.damage=16;Item.DamageType=DamageClass.Melee;
        Item.useAnimation=18;Item.useTime=18;Item.useStyle=ItemUseStyleID.Swing;
        Item.knockBack=3.5f;Item.autoReuse=true;Item.useTurn=true;
        Item.rare=ItemRarityID.Green;Item.value=Item.sellPrice(silver:40);
        Item.UseSound=SoundID.Item1;
    }
    public override void AddRecipes()=>CreateRecipe()
        .AddIngredient(ItemID.Gel,3).AddIngredient(ItemID.SilverBar,5)
        .AddTile(TileID.WorkBenches).Register();
}

// 印刃：族特殊效果的开关。当前只让收回期的飞刃保留一半伤害，印记实装后挂到同一处。
public class FlyingBladeSigil : ModItem
{
    public override string Texture => "Terraria/Images/Item_"+ItemID.GoldBroadsword;
    public override void SetDefaults()
    {
        Item.width=32;Item.height=32;
        Item.damage=22;Item.DamageType=DamageClass.Melee;
        Item.useAnimation=24;Item.useTime=24;Item.useStyle=ItemUseStyleID.Swing;
        Item.knockBack=4.5f;Item.autoReuse=true;Item.useTurn=true;
        Item.rare=ItemRarityID.Orange;Item.value=Item.sellPrice(silver:50);
        Item.UseSound=SoundID.Item1;
    }
    public override void AddRecipes()=>CreateRecipe()
        .AddIngredient(ItemID.Gel,3).AddIngredient(ItemID.GoldBar,5)
        .AddTile(TileID.WorkBenches).Register();
}
