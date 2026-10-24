using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 测试用武器：外形是原版天顶剑，但走普通近战挥砍，用来观察粘性空腔特效。
// 正式内容请勿依赖此物；确认效果后可整文件删除。
public class SlashZenithTest : ModItem
{
    // 直接复用原版天顶剑的贴图，不新增美术资源
    public override string Texture=>"Terraria/Images/Item_"+ItemID.Zenith;

    public override void SetDefaults()
    {
        Item.width=44;
        Item.height=44;
        Item.damage=260;
        Item.DamageType=DamageClass.Melee;
        Item.useStyle=ItemUseStyleID.Swing;   // 普通横挥，才会产生挥砍轨迹
        Item.useAnimation=18;
        Item.useTime=18;
        Item.knockBack=7f;
        Item.autoReuse=true;
        Item.scale=1.15f;
        Item.rare=ItemRarityID.Purple;
        Item.value=0;
        Item.UseSound=SoundID.Item1;
    }

    public override void AddRecipes()
    {
        // 免工作台，一块木头就能造，方便反复测试
        CreateRecipe().AddIngredient(ItemID.Wood,1).Register();
    }
}
