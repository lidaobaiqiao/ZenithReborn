using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

public partial class SoulItem : ModItem
{
    public Item[] Hosts = Enumerable.Range(0, 12).Select(_ => new Item()).ToArray();
    public Item[] SupportHosts = EmptySlots(11), ForestHosts = EmptySlots(2);
    public bool AwakeningMemory;
    public WoodLoadout Wood => WoodTypes.Evaluate(this);
    public int BaseCapacity => Attached ? Math.Min(10, SoulProgress.Capacity(Kills, Cores)) : 0;
    public int Capacity => BaseCapacity;
    public int WoodSupportCapacity => Wood.SupportCount;
    public int SupportCapacity => WoodSupportCapacity;
    // Extended arrays are retained solely to recover items saved by 0.3.9/10.
    // They never grant active slots or contribute to combat/set counts.
    public bool HasLegacyExpansion => Hosts.Skip(10).Concat(SupportHosts.Skip(5)).Any(i => !i.IsAir);
    public int BonusCapacity => Wood.Full ? 2 : 0;
    public IEnumerable<Item> Stored => Hosts.Concat(SupportHosts).Concat(ForestHosts);
    public IEnumerable<Item> Supports => SupportHosts.Take(SupportCapacity).Where(i => !i.IsAir);
    public Item ItemAt(int slot) =>
        slot < 10 ? Hosts[slot] :
        slot < 15 ? SupportHosts[slot - 10] :
        slot < 17 ? ForestHosts[slot - 15] :
        slot < 19 ? Hosts[10 + slot - 17] :
        slot < 25 ? SupportHosts[5 + slot - 19] : new Item();
    public Item[] SlotItems() => Enumerable.Range(0, 25).Select(ItemAt).ToArray();
    private static Item[] EmptySlots(int count) => Enumerable.Range(0, count).Select(_ => new Item()).ToArray();
    public string SoulId = Guid.NewGuid().ToString("N");
    public int Kills, Cores;
    public bool Attached, KingMemory;
    public IEnumerable<Item> Active => Hosts.Take(Capacity).Concat(ForestHosts.Take(BonusCapacity)).Where(i => !i.IsAir);
    public int Stage => SoulProgress.Milestones(Kills);
    public bool Awakened => AwakeningMemory || CanAwaken;
    private bool CanAwaken => Attached && Cores == 63 && SoulProgress.Has(Kills, SoulProgress.All) &&
        SoulProgress.ZenithWeapons.All(type => Hosts.Take(Capacity).Any(i => i.type == type));
    public float Potency => !Attached ? 0 : Awakened ? 1.1f : .65f + Stage * .04f;
    public override string Texture => "Terraria/Images/Item_" + ItemID.Zenith;
    public static bool CanHost(Item item) => !item.IsAir && item.damage > 0 &&
        item.CountsAsClass(DamageClass.Melee) && item.pick == 0 && item.axe == 0 && item.hammer == 0 &&
        item.ModItem is not SoulItem && item.type != ItemID.Zenith && !item.consumable && item.maxStack == 1;
    public bool TrySwapHost(int slot, ref Item cursor, out string error)
    {
        error = "";
        if (slot < 0 || slot >= 25) return false;
        Item[] slots;
        int local;
        bool unlocked;
        if (slot < 10)
        { slots = Hosts; local = slot; unlocked = local < Math.Max(1, Capacity); }
        else if (slot < 15)
        { slots = SupportHosts; local = slot - 10; unlocked = local < SupportCapacity; }
        else if (slot < 17)
        { slots = ForestHosts; local = slot - 15; unlocked = local < BonusCapacity; }
        else if (slot < 19)
        { slots = Hosts; local = 10 + slot - 17; unlocked = false; }
        else
        { slots = SupportHosts; local = 5 + slot - 19; unlocked = false; }
        if (!cursor.IsAir)
        {
            int incomingType = cursor.type;
            if (!unlocked) error = slot >= 17 ? "旧扩展已停用，只可空手取回，不能再放入。" : "这个槽位尚未解锁；暂停槽中的武器仍可用空鼠标取回。";
            else if (!CanHost(cursor)) error = "请放入不可堆叠的近战武器；工具、消耗武器和天顶剑不可寄宿。";
            else if (((slot >= 10 && slot < 15) || (slot >= 19 && slot < 25)) && !WoodTypes.CanSupport(cursor))
                error = "这把武器尚未登记支援特性。可放木质剑、矿石剑、五类早期武器或天顶材料剑。";
            else
            {
                int same = Stored.Count(h => !h.IsAir && h.type == incomingType);
                if (!slots[local].IsAir && slots[local].type == incomingType) same--;
                if (same > 0) error = "同种武器最多寄宿一把。";
            }
            if (error.Length > 0) return false;
        }
        AwakeningMemory |= CanAwaken;
        Item outgoing = slots[local];
        slots[local] = cursor;
        cursor = outgoing;
        RefreshStats();
        return true;
    }
    public override void SetDefaults()
    {
        SoulId = Guid.NewGuid().ToString("N");
        Hosts = Enumerable.Range(0, 12).Select(_ => new Item()).ToArray();
        SupportHosts = EmptySlots(11); ForestHosts = EmptySlots(2); AwakeningMemory = false;
        Kills = Cores = 0;
        Attached = KingMemory = false;
        Item.width = Item.height = 40; Item.damage = 20; Item.knockBack = 3;
        Item.DamageType = DamageClass.Melee; Item.useTime = Item.useAnimation = 25;
        Item.useStyle = ItemUseStyleID.Shoot; Item.autoReuse = true;
        Item.noMelee = true; Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<SoulBlade>(); Item.shootSpeed = 1;
        Item.rare = ItemRarityID.Blue; Item.value = 0;
    }
    public override ModItem Clone(Item newEntity)
    {
        var copy = (SoulItem)base.Clone(newEntity);
        copy.Hosts = Hosts.Select(i => i.Clone()).ToArray();
        copy.SupportHosts = SupportHosts.Select(i => i.Clone()).ToArray();
        copy.ForestHosts = ForestHosts.Select(i => i.Clone()).ToArray();
        return copy;
    }

    // The soul draws itself as a projectile (noUseGraphic = true), so vanilla's
    // default MeleePrefix() returns false and the reforge pool becomes the
    // AnyWeapon (spear) pool, which has no Legendary. Force the sword pool.
    public override bool MeleePrefix() => true;

    // ResetPrefix() -> Refresh() serializes/reloads the item through ItemIO.
    // Keep the soul's data across that round-trip even if the reload replaces
    // the ModItem instance.
    private static Item[] reforgeHosts;
    private static Item[] reforgeSupports, reforgeForest;
    private static bool reforgeAwakened;
    private static string reforgeSoulId;
    private static int reforgeKills, reforgeCores;
    private static bool reforgeAttached, reforgeMemory;
    public override void PreReforge()
    {
        reforgeHosts = Hosts.Select(i => i.Clone()).ToArray();
        reforgeSupports = SupportHosts.Select(i => i.Clone()).ToArray();
        reforgeForest = ForestHosts.Select(i => i.Clone()).ToArray();
        reforgeAwakened = Awakened;
        reforgeSoulId = SoulId;
        reforgeKills = Kills;
        reforgeCores = Cores;
        reforgeAttached = Attached; reforgeMemory = KingMemory;
        RefreshStats();
        // Reforge rolls reject damage prefixes when the integer result does not
        // change (damage 1-3 + Legendary's 1.15x often rounds back to itself).
        // The soul's real damage is computed in Shoot; this value only feeds the
        // Goblin Tinkerer's prefix roll.
        Item.damage = Math.Max(Item.damage, 20);
    }
    public override void PostReforge()
    {
        if (reforgeHosts != null)
        {
            for (int i = 0; i < Hosts.Length; i++)
                if (Hosts[i].IsAir && !reforgeHosts[i].IsAir)
                    Hosts[i] = reforgeHosts[i].Clone();
            if (!string.IsNullOrEmpty(reforgeSoulId)) SoulId = reforgeSoulId;
            Kills = reforgeKills & SoulProgress.All;
            Cores = reforgeCores & 63;
            Attached = reforgeAttached; KingMemory = reforgeMemory;
            SupportHosts = reforgeSupports.Select(i => i.Clone()).ToArray();
            ForestHosts = reforgeForest.Select(i => i.Clone()).ToArray();
            AwakeningMemory = reforgeAwakened;
        }
        reforgeHosts = null;
        reforgeSupports = reforgeForest = null;
        reforgeSoulId = null;
        RefreshStats();
    }
    // HEROsMod's prefix editor rebuilds the soul with netDefaults + Prefix, so
    // ApplyPrefix is the only hook that runs on the new empty item.
    public override void ApplyPrefix(int pre)
    {
        if (HeroPrefixCompat.TryRestore(this)) RefreshStats();
    }
    public override void SaveData(TagCompound tag)
    {
        AwakeningMemory |= CanAwaken;
        tag["version"] = 3; tag["id"] = SoulId; tag["kills"] = Kills; tag["cores"] = Cores;
        tag["attachment"] = Attached; tag["kingMemory"] = KingMemory;
        tag["hosts"] = Hosts.Select(ItemIO.Save).ToList();
        tag["supports"] = SupportHosts.Select(ItemIO.Save).ToList();
        tag["forestHosts"] = ForestHosts.Select(ItemIO.Save).ToList();
        tag["awakenedMemory"] = AwakeningMemory;
    }
    public override void LoadData(TagCompound tag)
    {
        SoulId = tag.GetString("id");
        if (string.IsNullOrEmpty(SoulId)) SoulId = Guid.NewGuid().ToString("N");
        Kills = tag.GetInt("kills") & SoulProgress.All; Cores = tag.GetInt("cores") & 63;
        var stored = tag.GetList<TagCompound>("hosts");
        Hosts = Enumerable.Range(0, 12).Select(i => i < stored.Count ? ItemIO.Load(stored[i]) : new Item()).ToArray();
        // Keep older, already-used souls playable; an empty old remnant starts the new prologue.
        Attached = tag.ContainsKey("attachment") ? tag.GetBool("attachment") : Hosts.Any(i=>!i.IsAir) || Kills!=0 || Cores!=0;
        KingMemory = tag.GetBool("kingMemory");
        SupportHosts = LoadSlots(tag, "supports", 11);
        ForestHosts = LoadSlots(tag, "forestHosts", 2);
        AwakeningMemory = tag.GetBool("awakenedMemory") || CanAwaken;
    }
    private static Item[] LoadSlots(TagCompound tag, string key, int count)
    {
        var items = tag.GetList<TagCompound>(key);
        return Enumerable.Range(0, count).Select(i => i < items.Count ? ItemIO.Load(items[i]) : new Item()).ToArray();
    }
    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(SoulId); writer.Write(Kills); writer.Write(Cores);
        writer.Write(Attached); writer.Write(KingMemory);
        writer.Write(Awakened);
        foreach (var host in Stored) ItemIO.Send(host, writer, writeStack: true, writeFavorite: true);
    }
    public override void NetReceive(BinaryReader reader)
    {
        SoulId = reader.ReadString(); Kills = reader.ReadInt32() & SoulProgress.All; Cores = reader.ReadInt32() & 63;
        Attached=reader.ReadBoolean();KingMemory=reader.ReadBoolean();
        AwakeningMemory = reader.ReadBoolean();
        Hosts = Enumerable.Range(0, 12).Select(_ => ItemIO.Receive(reader, readStack: true, readFavorite: true)).ToArray();
        SupportHosts = Enumerable.Range(0, 11).Select(_ => ItemIO.Receive(reader, readStack: true, readFavorite: true)).ToArray();
        ForestHosts = Enumerable.Range(0, 2).Select(_ => ItemIO.Receive(reader, readStack: true, readFavorite: true)).ToArray();
    }
    public override bool AltFunctionUse(Player player) => true;
    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse == 2)
        {
            if (player.whoAmI == Main.myPlayer && !SoulUI.IsEditing(this)) SoulUI.Toggle(this);
            return false;
        }
        if (!Attached) return false;
        if (!Active.Any())
        {
            if (player.whoAmI == Main.myPlayer) Main.NewText("残魂无法独自攻击。按P或手持右键，放入一把近战武器。", 190, 210, 255);
            return false;
        }
        RefreshStats();
        return !SoulUI.IsEditing(this);
    }
    public void RefreshStats()
    {
        AwakeningMemory |= CanAwaken;
        var active = Active.ToArray();
        Item.damage = active.Length == 0 ? 1 : Math.Max(1, (int)(active.Average(i => i.damage) * Potency));
        bool swiftSupport = Supports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Swift);
        Item.useTime = Item.useAnimation = active.Length == 0 ? 25 : Math.Clamp((int)active.Average(i => Math.Max(1, i.useTime)*(swiftSupport ? 1 : WeaponFamilies.Interval(i.type))), 6, 60);
        Item.SetNameOverride(Awakened ? "复生·天顶剑" : SoulProgress.Mechs(Kills) == 3 ? "复苏的天顶剑魂" : "诅咒的天顶残魂");
        Item.rare = Awakened ? ItemRarityID.Red : Stage >= 4 ? ItemRarityID.Yellow : ItemRarityID.Blue;
    }
    public override void UpdateInventory(Player player) => RefreshStats();
    public override void HoldItem(Player player)
    {
        if(!Attached && !Main.dedServ && player.whoAmI==Main.myPlayer && Main.rand.NextBool(12))
        {
            int dust=Dust.NewDust(player.Center-new Vector2(8,8),16,16,DustID.BlueCrystalShard,0,-.3f,160,default,.65f);
            Main.dust[dust].noGravity=true;
        }
    }
    public override void ModifyWeaponCrit(Player player, ref float crit)
    {
        if (Active.Any()) crit += (float)Active.Average(i => i.crit);
    }
    public override float UseSpeedMultiplier(Player player)
    {
        float flow = player.GetModPlayer<SoulPlayer>().Flow;
        int remaining = 3 - SoulProgress.Mechs(Kills);
        float baseSpeed = remaining == 0 ? 1 + .2f * flow : 1 - .35f * remaining / 3 * flow;
        return baseSpeed * (1 + player.GetModPlayer<WoodPlayer>().LayersFor(this) * .02f) /
            (Supports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Swift) ? .85f : 1);
    }
    public float Reach(Player player)
    {
        float flow = player.GetModPlayer<SoulPlayer>().Flow;
        int remaining = 3 - SoulProgress.Mechs(Kills);
        if(!Attached) return 0;
        float range = 192 + Stage * 45 + (Awakened ? 150 : 0);
        if(Active.Any() && !Supports.Any(i=>WeaponFamilies.Of(i.type)==BladeFamily.Swift)) range*=(float)Active.Average(i=>WeaponFamilies.Range(i.type));
        return range * (remaining == 0 ? 1 + .2f * flow : 1 - .3f * remaining / 3 * flow) *
            (Supports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Swift) ? .85f : 1);
    }
    public override void UseAnimation(Player player) => player.GetModPlayer<SoulSwingPlayer>().Begin(this);
    public override void UseItemFrame(Player player)
    {
        int phase = SoulSwingPlayer.Phase(player);
        float angle = MathHelper.Lerp(-2.6f, .65f, phase / 5f);
        player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, angle * player.direction);
    }
    // Actual projectiles are emitted by the five animation thresholds, never
    // pre-created as a hidden batch by vanilla's first-frame Shoot call.
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
        Vector2 velocity, int type, int damage, float knockback) => false;

    public int EmitBlade(Player player, Item host, int ordinal, int hostCount)
    {
        if (player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server) return Main.maxProjectiles;
        Vector2 delta = Main.MouseWorld - player.Center;
        float reach = Reach(player);
        if (delta.Length() > reach) delta = Vector2.Normalize(delta) * reach;
        if (delta.LengthSquared() < 2500) delta = delta.SafeNormalize(Vector2.UnitX * player.direction) * 50;
        int baseHostDamage = BladeDamage(player, host);
        int hostDamage = Math.Max(1, (int)(baseHostDamage * (1 + player.GetModPlayer<WoodPlayer>().LayersFor(this) * .03f)));
        float baseDuration = Math.Max(host.useAnimation, host.useTime)*WeaponFamilies.Interval(host.type);
        int duration = Math.Clamp((int)(baseDuration / player.GetAttackSpeed(DamageClass.Melee)), 18, 55);
        float hostKnockback = player.GetWeaponKnockback(host);
        int index = Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, Vector2.Zero, ModContent.ProjectileType<SoulBlade>(),
            hostDamage, hostKnockback*(WeaponFamilies.Of(host.type)==BladeFamily.Solid ||
                Supports.Any(h=>WeaponFamilies.Of(h.type)==BladeFamily.Solid)?1.2f:1), player.whoAmI, host.type, duration, 0);
        if (index >= Main.maxProjectiles) return index;
        var blade = (SoulBlade)Main.projectile[index].ModProjectile;
        blade.ConfigureReborn(Awakened);
        blade.TargetOffset = delta.RotatedBy((ordinal - (hostCount - 1) * .5f) * .06f);
        blade.SwordScale = host.scale;
        blade.SpecialDamage = baseHostDamage;
        // Vanilla Zenith 933: one ellipse, ai[0] = Random.Next(-100, 101).
        blade.Vertical = Main.rand.Next(-100, 101);
        Main.projectile[index].CritChance = player.GetWeaponCrit(host);
        Main.projectile[index].GetGlobalProjectile<SoulSource>().SoulId = SoulId;
        Main.projectile[index].GetGlobalProjectile<SoulSource>().SourceWeapon = host.type;
        Main.projectile[index].netUpdate = true;
        return index;
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        tooltips.Add(new(Mod,"SoulControl","按P或手持右键：管理寄宿武器；放入和取回都不会消耗武器"));
        tooltips.Add(new(Mod,"SoulSwing","每次挥臂分五段，依次释放最多五把剑；下次接续轮换。不足五把时各出一次，余下节点留空。"));
        tooltips.Add(new(Mod,"BladeCombat","逐剑计算：各剑继承自身伤害、暴击、击退与特性；无统一伤害下限，不按剑数分摊；同一剑对同一敌人每8帧可再次命中。\n伤害栏仅为平均参考，实际命中由当前出剑的寄宿武器决定。"));
        if(!Attached) tooltips.Add(new(Mod,"Unstable","残魂未稳：尚不能攻击，第一槽仅供预览。\n白天地表使用凝胶信物，右键友善史莱姆王对话，接受「附着」。\n凝胶信物：开局附带，或凝胶10＋坠落之星1在工作台制作。"));
        else tooltips.Add(new(Mod,"Attachment","祝福·附着：已经能够稳定依附武器，永久生效。"));
        tooltips.Add(new(Mod,"SoulStats",$"普通槽 {Capacity}/10＋森林奖励{BonusCapacity}/2 · 支援槽{SupportCapacity}/5 · 武器效能 {Potency:P0} · 魂芯 {System.Numerics.BitOperations.PopCount((uint)Cores)}/6"));
        int oreCount = OreFamily.Count(this);
        if (oreCount > 0)
            tooltips.Add(new(Mod,"OreFamily",$"锻金族 {oreCount}/14 · 飞剑 {oreCount} 把 · 每多一把飞剑伤害 +5%\n"+
                "矿石主剑命中后生成环绕飞剑；仍使用原17槽与木系支援解锁，不增加槽位"));
        if (Active.Concat(Supports).Any(i => HardOreTypes.IsOre(i.type)))
            tooltips.Add(new(Mod,"HardOre", "肉后矿石：主剑每8帧可再次命中，机制每来源、每目标主体30帧计一次，3秒未命中清空连击；支援槽按实际主剑取伤害。"));
        var wood = Wood;
        tooltips.Add(new(Mod, "WoodSet", $"木质族 {wood.Count}/12 · 环境强度{wood.Strength:0.##}倍 · 生命＋{wood.Life}"));
        if (wood.Full) tooltips.Add(new(Mod, "ForestPower", "森林之力：额外2真实剑位；60秒满层：伤害＋36%、速度＋24%、防御＋36、回复＋6/秒、持续伤害＋60%、减伤＋12个百分点"));
        else tooltips.Add(new(Mod, "WoodMissing", "木质缺少：" + string.Join("、", Enumerable.Range(0, 12).Where(i => (wood.Mask & (1 << i)) == 0).Select(i => WoodTypes.Names[i]))));
        int mechs = SoulProgress.Mechs(Kills);
        tooltips.Add(new(Mod,"MechanicalCurse",mechs < 3 ?
            $"迟滞械缓 [{mechs}/3]：连续轮换时，攻速最多降低{35 * (3-mechs)/3f:0.#}%，距离最多缩短{30*(3-mechs)/3f:0.#}%" :
            "机魂协律 [祝福]：连续轮换时，攻速与距离逐渐提高，最多20%"));
        tooltips.Add(new(Mod,"NextGoal","下一步："+SoulProgress.Next(this)+"\n按P→成长路线，查看全部槽位条件或补办缺失魂芯。"));
        foreach(var host in Hosts.Take(Math.Max(1,Capacity)).Where(i=>!i.IsAir))
            tooltips.Add(new(Mod,"Family"+host.type,$"{host.Name} · {WeaponFamilies.Name(host.type)}：{WoodTypes.Description(host.type)}"));
        tooltips.Add(new(Mod,"Hosts","当前寄宿：" + (Active.Any() ? string.Join("、", Active.Select(i => i.Name)) : "空；残魂无法独自攻击")));
        tooltips.Add(new(Mod,"Awakening",Awakened ? "复生已经完成：永久保留，可以自由更换寄宿武器" : "复生条件：完成主线铭刻、六枚魂芯归位、基础普通槽同时寄宿十种天顶材料武器；完成后永久保留"));
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.CopperBar,5)
        .AddIngredient(ItemID.FallenStar).AddTile(TileID.WorkBenches).Register();
}
