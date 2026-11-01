using System.IO;
using Terraria;
using Terraria.ID;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 飞刃族候选普查：遍历全部原版可寄宿近战物品，逐项记录弹幕字段与合成配方情况。
// 只读引擎数据，不改游戏行为、不写入档；结果供飞刃族名册按"可合成 / 仅购买或掉落"分组使用。
// 弹幕判定依据 Item.shoot 原始字段；挥舞弹幕若走特殊机制而非 shoot，将在数据中显示为无弹幕，需另行核对。
public static class FlyingBladeSurveyCheck
{
    public static void Run(string outputPath)
    {
        var rows = new List<string>();
        for (int id = 1; id < ItemID.Count; id++)
        {
            var it = new Item();
            it.SetDefaults(id);
            if (it.ModItem != null || !SoulItem.CanHost(it)) continue; // 只普查原版可寄宿项
            bool hasRecipe = Main.recipe.Any(r => r != null && !r.Disabled && r.createItem.type == id);
            rows.Add("{" + string.Join(",",
                $"\"id\":{id}",
                $"\"name\":\"{it.Name}\"",
                $"\"shoot\":{it.shoot}",
                $"\"shootSpeed\":{it.shootSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"\"recipe\":{(hasRecipe ? "true" : "false")}",
                $"\"wood\":{(WoodTypes.IsWood(id) ? "true" : "false")}",
                $"\"ore\":{(OreFamily.IsOre(id) ? "true" : "false")}",
                $"\"heaven\":{(HolyHeavenFamily.IsMember(id) ? "true" : "false")}") + "}");
        }
        File.WriteAllText(outputPath, "[\n" + string.Join(",\n", rows) + "\n]");
    }
}
