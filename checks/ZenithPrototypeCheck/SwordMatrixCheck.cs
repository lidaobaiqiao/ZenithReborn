using System.IO;
using Terraria;
using Terraria.ID;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 剑矩阵普查：遍历全部原版可寄宿近战物品，逐项记录真实面板数值与族归属。
// 只读引擎数据，不改游戏行为、不写存档；结果供数值曲线重建时做"每槽放什么最优"的推演。
// 与 FlyingBladeSurveyCheck 的区别：那份只看弹幕与配方，这份看伤害/攻速/击退与族标签。
public static class SwordMatrixCheck
{
    public static void Run(string outputPath)
    {
        var rows = new List<string>();
        for (int id = 1; id < ItemID.Count; id++)
        {
            var it = new Item();
            it.SetDefaults(id);
            if (it.ModItem != null || !SoulItem.CanHost(it)) continue; // 只普查原版可寄宿项
            int material = OreFamily.MaterialOf(id);
            rows.Add("{" + string.Join(",",
                $"\"id\":{id}",
                $"\"name\":\"{it.Name}\"",
                $"\"damage\":{it.damage}",
                $"\"useTime\":{it.useTime}",
                $"\"useAnimation\":{it.useAnimation}",
                $"\"knockback\":{it.knockBack.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"\"crit\":{it.crit}",
                $"\"shoot\":{it.shoot}",
                $"\"shootSpeed\":{it.shootSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"\"rare\":{it.rare}",
                $"\"wood\":{(WoodTypes.IsWood(id) ? "true" : "false")}",
                $"\"ore\":{(material >= 0 ? "true" : "false")}",
                $"\"oreMaterial\":{material}",
                $"\"heaven\":{(HolyHeavenFamily.IsMember(id) ? "true" : "false")}",
                $"\"flying\":{(FlyingBladeFamily.IsMember(id) ? "true" : "false")}",
                $"\"living\":{(LivingArmamentFamily.IsMember(id) ? "true" : "false")}") + "}");
        }
        File.WriteAllText(outputPath, "[\n" + string.Join(",\n", rows) + "\n]");
    }
}
